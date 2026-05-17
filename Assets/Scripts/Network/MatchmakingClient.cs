using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// HTTP kliens a Python matchmaking API-hoz.
///
/// Felelős:
///   - Google session token tárolása
///   - Meccs létrehozás / csatlakozás / listázás / indítás / bezárás
///   - Minden API hívás coroutine-ként fut (nem blokkolja a UI threadet)
///
/// Szerver alap URL: https://kakaoo123.asuscomm.com/api
///
/// Használat (PvPLobbyUI-ból):
///   MatchmakingClient.Instance.CreateMatch("Meccs neve", 4,
///       onSuccess: (host, port, matchId) => NetworkGameManager.Instance.StartClient(host, port),
///       onError:   (err) => ShowError(err));
/// </summary>
public class MatchmakingClient : MonoBehaviour
{
    public static MatchmakingClient Instance { get; private set; }

    [Header("API konfiguráció")]
    [Tooltip("A matchmaking szerver alap URL-je (trailing slash nélkül)")]
    public string apiBaseUrl = "https://kakaoo123.asuscomm.com/api";

    [Tooltip("Kérés timeout másodpercekben")]
    public float timeoutSeconds = 10f;

    // ── Tárolt adatok ────────────────────────────────────────────────

    /// <summary>Google auth után kapott session token – minden kérésbe bekerül.</summary>
    public string SessionToken { get; private set; }

    /// <summary>A belépett játékos neve (Google display_name).</summary>
    public string DisplayName { get; private set; }

    /// <summary>A belépett játékos Google player_id-ja.</summary>
    public string PlayerId { get; private set; }

    /// <summary>Az aktuálisan csatlakozott meccs ID-ja.</summary>
    public string CurrentMatchId { get; private set; }

    /// <summary>
    /// Session token sikeresen megszerezve (VerifyGoogleToken / DevLogin után).
    /// ServerSyncManager iratkozik fel rá az automatikus szinkronizáláshoz.
    /// </summary>
    public event Action OnSessionReady;

    /// <summary>Utoljára lekért ranglistás helyezés (0 = még nem ismert).</summary>
    public int CachedRank { get; private set; } = 0;

    /// <summary>Utoljára lekért SSF ranglistás helyezés (0 = még nem ismert).</summary>
    public int CachedSSFRank { get; private set; } = 0;

    /// <summary>
    /// LAN kliensnek a Caddy belső LAN IP-je TCP connect célpontként
    /// (a server_host SNI hosztnév DNS-feloldása NAT loopback miatt nem működne).
    /// Üres string → a kliens a server_host-ot oldja fel DNS-en (publikus út).
    /// A legutóbbi /matchmaking/status, /match/create vagy /match/join hívás állítja be.
    /// </summary>
    public string LastConnectOverrideIp { get; private set; } = "";

    /// <summary>Friss ranglistás helyezés érkezett a szervertől.</summary>
    public event Action<int> OnRankRefreshed;

    // 401 esetén újra-bejelentkezéshez tárolt adatok
    private string _savedUserId;
    private string _savedDisplayName;
    private string _savedIdToken;   // null = dev login, nem null = Google login
    private bool   _isRelogging = false;

    // ── Lifecycle ────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        // Ha a GoogleAuthManager már bejelentkezett mielőtt ez a komponens létrejött
        // (pl. a login más scene-ben történt, akkor itt azonnal elvégezzük a session szerzést.)
        if (GoogleAuthManager.Instance != null && GoogleAuthManager.Instance.IsSignedIn)
        {
            string idToken = GoogleAuthManager.Instance.IdToken;
            if (!string.IsNullOrEmpty(idToken))
            {
                // Éles Android bejelentkezés – valódi Google token
                Debug.Log("[MMC] GoogleAuthManager már bejelentkezett (Google token) – VerifyGoogleToken.");
                VerifyGoogleToken(idToken,
                    onSuccess: (dn, pid) => Debug.Log($"[MMC] Session OK (Google): {dn}"),
                    onError:   err       => Debug.LogWarning($"[MMC] VerifyGoogleToken hiba: {err}")
                );
            }
            else
            {
                // Editor / szimulált bejelentkezés – dev login
                Debug.Log("[MMC] GoogleAuthManager már bejelentkezett (dev/editor) – DevLogin.");
                DevLogin(
                    GoogleAuthManager.Instance.UserId,
                    GoogleAuthManager.Instance.DisplayName,
                    onSuccess: () => Debug.Log("[MMC] Dev session OK."),
                    onError:   err => Debug.LogWarning($"[MMC] DevLogin hiba: {err}")
                );
            }
        }

        // Feliratkozás jövőbeli bejelentkezésekre
        if (GoogleAuthManager.Instance != null)
            GoogleAuthManager.Instance.OnSignInSuccess += OnGoogleSignedIn;
    }

    void OnDestroy()
    {
        if (GoogleAuthManager.Instance != null)
            GoogleAuthManager.Instance.OnSignInSuccess -= OnGoogleSignedIn;
    }

    void OnGoogleSignedIn(string userId, string displayName, string email)
    {
        // Ha még nincs session (pl. ApplySignIn-kor az Instance null volt)
        if (!string.IsNullOrEmpty(SessionToken)) return;

        string idToken = GoogleAuthManager.Instance?.IdToken;
        if (!string.IsNullOrEmpty(idToken))
        {
            Debug.Log("[MMC] OnSignInSuccess → VerifyGoogleToken (Android éles mód).");
            VerifyGoogleToken(idToken,
                onSuccess: (dn, pid) => Debug.Log($"[MMC] Session OK (Google, callback): {dn}"),
                onError:   err       => Debug.LogWarning($"[MMC] VerifyGoogleToken hiba (callback): {err}")
            );
        }
        else
        {
            Debug.Log("[MMC] OnSignInSuccess → DevLogin (editor/szimulált mód).");
            DevLogin(userId, displayName,
                onSuccess: () => Debug.Log("[MMC] Dev session OK (callback)."),
                onError:   err => Debug.LogWarning($"[MMC] DevLogin hiba (callback): {err}")
            );
        }
    }

    // ── Auth ─────────────────────────────────────────────────────────

    /// <summary>
    /// Google id_token beküldése a szervernek.
    /// Sikeres válasz esetén elmenti a SessionToken-t és hívja az onSuccess callbacket.
    /// </summary>
    public void VerifyGoogleToken(string idToken,
        Action<string, string> onSuccess,
        Action<string> onError)
    {
        StartCoroutine(PostCoroutine(
            url:     $"{apiBaseUrl}/auth/verify",
            body:    $"{{\"id_token\":\"{idToken}\"}}",
            auth:    false,
            onDone: (json, err) =>
            {
                if (err != null) { onError?.Invoke(err); return; }

                var resp = JsonUtility.FromJson<SessionResponseJson>(json);
                SessionToken      = resp.session_token;
                DisplayName       = resp.display_name;
                PlayerId          = resp.player_id;
                _savedIdToken     = idToken;  // retry-hoz mentjük

                Debug.Log($"[MMC] Bejelentkezve: {DisplayName} ({PlayerId})");
                onSuccess?.Invoke(resp.display_name, resp.player_id);
                OnSessionReady?.Invoke();
                RefreshRank();
            }
        ));
    }

    /// <summary>
    /// Editor / tesztelési bejelentkezés Google token nélkül.
    /// GoogleAuthManager hívja editor módban és szimuláció esetén.
    /// </summary>
    public void DevLogin(string playerId, string displayName,
        Action onSuccess = null, Action<string> onError = null)
    {
        // Mentjük az adatokat automatikus újra-bejelentkezéshez
        _savedUserId      = playerId;
        _savedDisplayName = displayName;
        _savedIdToken     = null;

        StartCoroutine(PostCoroutine(
            url:    $"{apiBaseUrl}/auth/dev-login?player_id={UnityWebRequest.EscapeURL(playerId)}&display_name={UnityWebRequest.EscapeURL(displayName)}",
            body:   "{}",
            auth:   false,
            onDone: (json, err) =>
            {
                if (err != null) { onError?.Invoke(err); return; }
                var resp = JsonUtility.FromJson<SessionResponseJson>(json);
                SessionToken = resp.session_token;
                DisplayName  = resp.display_name;
                PlayerId     = resp.player_id;
                Debug.Log($"[MMC] Dev bejelentkezés OK: {DisplayName}");
                onSuccess?.Invoke();
                OnSessionReady?.Invoke();
                RefreshRank();
            }
        ));
    }

    // ── Match műveletek ───────────────────────────────────────────────

    /// <summary>
    /// Nyitott meccsek lekérdezése.
    /// onSuccess: JSON tömb string-ként (PvPLobbyUI maga parseol).
    /// </summary>
    public void FetchMatchList(Action<MatchInfoJson[]> onSuccess, Action<string> onError)
    {
        StartCoroutine(GetCoroutine(
            url:    $"{apiBaseUrl}/match/list",
            onDone: (json, err) =>
            {
                if (err != null) { onError?.Invoke(err); return; }
                // Unity JsonUtility nem tud top-level tömböt – wrapper trick
                string wrapped = $"{{\"items\":{json}}}";
                var list = JsonUtility.FromJson<MatchListWrapper>(wrapped);
                onSuccess?.Invoke(list.items ?? Array.Empty<MatchInfoJson>());
            }
        ));
    }

    /// <summary>
    /// Meccs létrehozása. A szerver spawnolja a Unity Dedicated Server process-t.
    /// onSuccess: (serverHost, serverPort, matchId)
    /// </summary>
    public void CreateMatch(string matchName, int maxPlayers,
        Action<string, ushort, string> onSuccess,
        Action<string> onError,
        bool manualStart = false)
    {
        string body = $"{{\"match_name\":\"{EscapeJson(matchName)}\",\"max_players\":{maxPlayers},\"manual_start\":{(manualStart ? "true" : "false")}}}";
        StartCoroutine(PostCoroutine(
            url:    $"{apiBaseUrl}/match/create",
            body:   body,
            auth:   true,
            onDone: (json, err) =>
            {
                if (err != null) { onError?.Invoke(err); return; }
                var resp = JsonUtility.FromJson<JoinMatchResponseJson>(json);
                CurrentMatchId = resp.match_id;
                LastConnectOverrideIp = resp.server_connect_ip ?? "";
                Debug.Log($"[MMC] Meccs létrehozva: {resp.match_id} | {resp.server_host}:{resp.server_port}" +
                          (string.IsNullOrEmpty(LastConnectOverrideIp) ? "" : $" (LAN connect={LastConnectOverrideIp})"));
                onSuccess?.Invoke(resp.server_host, (ushort)resp.server_port, resp.match_id);
            }
        ));
    }

    /// <summary>
    /// Csatlakozás meglévő meccshez.
    /// onSuccess: (serverHost, serverPort, matchId)
    /// </summary>
    public void JoinMatch(string matchId,
        Action<string, ushort, string> onSuccess,
        Action<string> onError)
    {
        StartCoroutine(PostCoroutine(
            url:    $"{apiBaseUrl}/match/join/{matchId}",
            body:   "{}",
            auth:   true,
            onDone: (json, err) =>
            {
                if (err != null) { onError?.Invoke(err); return; }
                var resp = JsonUtility.FromJson<JoinMatchResponseJson>(json);
                CurrentMatchId = resp.match_id;
                LastConnectOverrideIp = resp.server_connect_ip ?? "";
                Debug.Log($"[MMC] Csatlakozva: {resp.match_id} | {resp.server_host}:{resp.server_port}" +
                          (string.IsNullOrEmpty(LastConnectOverrideIp) ? "" : $" (LAN connect={LastConnectOverrideIp})"));
                onSuccess?.Invoke(resp.server_host, (ushort)resp.server_port, resp.match_id);
            }
        ));
    }

    /// <summary>
    /// Room owner elindítja a meccset (API állapot frissítés).
    /// Utána a kliens NGO-n keresztül hívja RequestGameStart()-ot.
    /// </summary>
    public void StartMatch(Action onSuccess, Action<string> onError)
    {
        if (string.IsNullOrEmpty(CurrentMatchId))
        {
            onError?.Invoke("Nincs aktív meccs.");
            return;
        }

        StartCoroutine(PostCoroutine(
            url:    $"{apiBaseUrl}/match/start/{CurrentMatchId}",
            body:   "{}",
            auth:   true,
            onDone: (json, err) =>
            {
                if (err != null) { onError?.Invoke(err); return; }
                Debug.Log($"[MMC] Meccs elindítva: {CurrentMatchId}");
                onSuccess?.Invoke();
            }
        ));
    }

    // ── Matchmaking sor ───────────────────────────────────────────────

    /// <summary>
    /// Belép a szint alapú matchmaking sorba.
    /// A szerver hasonló szintű játékosokat párosít (±5 szint).
    /// </summary>
    public void JoinMatchmakingQueue(int level,
        Action onSuccess, Action<string> onError)
    {
        StartCoroutine(PostCoroutine(
            url:    $"{apiBaseUrl}/matchmaking/join?level={level}",
            body:   "{}",
            auth:   true,
            onDone: (json, err) =>
            {
                if (err != null) { onError?.Invoke(err); return; }
                onSuccess?.Invoke();
            }
        ));
    }

    /// <summary>
    /// Lekérdezi az Auto Lobby státuszát.
    /// onMatched:  megtalálta a meccset → (serverHost, serverPort, matchId, playerCount, playerNames, playerElos)
    /// onWaiting:  még vár             → (countdown másodpercek, playerCount, playerNames, playerElos)
    /// </summary>
    public void PollMatchmakingStatus(
        Action<string, ushort, string, int, string[], int[]> onMatched,
        Action<int, int, string[], int[]> onWaiting,
        Action<string> onError)
    {
        // WebGL böngészőből ws:// LAN IP nem elérhető (mixed content + private IP tiltás)
        // → platform=webgl paraméter jelzi a szervernek, hogy DuckDNS WSS proxy-t adjon vissza
#if UNITY_WEBGL && !UNITY_EDITOR
        string statusUrl = $"{apiBaseUrl}/matchmaking/status?platform=webgl";
#else
        string statusUrl = $"{apiBaseUrl}/matchmaking/status";
#endif

        StartCoroutine(GetCoroutine(
            url:    statusUrl,
            onDone: (json, err) =>
            {
                if (err != null) { onError?.Invoke(err); return; }

                var resp  = JsonUtility.FromJson<MatchmakingStatusJson>(json);
                var names = resp.player_names  ?? Array.Empty<string>();
                var ranks = resp.player_ranks  ?? Array.Empty<int>();
                if (resp.status == "matched")
                {
                    CurrentMatchId = resp.match_id;
                    LastConnectOverrideIp = resp.server_connect_ip ?? "";
                    onMatched?.Invoke(resp.server_host, (ushort)resp.server_port, resp.match_id, resp.player_count, names, ranks);
                }
                else
                {
                    onWaiting?.Invoke(resp.countdown, resp.player_count, names, ranks);
                }
            }
        ));
    }

    /// <summary>
    /// Lekéri a ranglistás helyezést, cache-eli és tüzeli az OnRankRefreshed eventet.
    /// Bejelentkezés után és meccs végén automatikusan hívódik.
    /// </summary>
    public void RefreshRank()
    {
        if (string.IsNullOrEmpty(SessionToken)) return;
        StartCoroutine(GetCoroutine($"{apiBaseUrl}/leaderboard/my_rank", (json, err) =>
        {
            if (err != null) { Debug.LogWarning($"[MMC] Rank lekérés hiba: {err}"); return; }
            try
            {
                var resp = JsonUtility.FromJson<MyRankResponseJson>(json);
                CachedRank = resp.rank;
                OnRankRefreshed?.Invoke(CachedRank);
            }
            catch (Exception e) { Debug.LogWarning($"[MMC] Rank parse hiba: {e.Message}"); }
        }));
    }

    /// <summary>Ranglistás helyezés lekérése egyedi callbackkel (pl. CharacterEntryUI).</summary>
    public void FetchMyRank(Action<int> onSuccess, Action<string> onError)
    {
        StartCoroutine(GetCoroutine($"{apiBaseUrl}/leaderboard/my_rank", (json, err) =>
        {
            if (err != null) { onError?.Invoke(err); return; }
            try
            {
                var resp = JsonUtility.FromJson<MyRankResponseJson>(json);
                CachedRank = resp.rank;
                OnRankRefreshed?.Invoke(CachedRank);
                onSuccess?.Invoke(resp.rank);
            }
            catch (Exception e) { onError?.Invoke(e.Message); }
        }));
    }

    /// <summary>SSF ranglistás helyezés lekérése egyedi callbackkel (pl. CharacterEntryUI SSF kártyához).</summary>
    public void FetchMySSFRank(Action<int> onSuccess, Action<string> onError)
    {
        StartCoroutine(GetCoroutine($"{apiBaseUrl}/leaderboard/my_ssf_rank", (json, err) =>
        {
            if (err != null) { onError?.Invoke(err); return; }
            try
            {
                var resp = JsonUtility.FromJson<MyRankResponseJson>(json);
                CachedSSFRank = resp.rank;
                onSuccess?.Invoke(resp.rank);
            }
            catch (Exception e) { onError?.Invoke(e.Message); }
        }));
    }

    /// <summary>Kilép a matchmaking sorból (pl. mégse gomb).</summary>
    public void LeaveMatchmakingQueue()
    {
        StartCoroutine(DeleteCoroutine($"{apiBaseUrl}/matchmaking/leave"));
    }

    /// <summary>
    /// Meccs bezárása (room owner kilép vagy a meccs véget ér).
    /// Nem várja meg a választ – fire-and-forget.
    /// </summary>
    public void CloseMatch()
    {
        if (string.IsNullOrEmpty(CurrentMatchId)) return;
        StartCoroutine(DeleteCoroutine($"{apiBaseUrl}/match/{CurrentMatchId}"));
        CurrentMatchId = null;
    }

    // ── HTTP segédfüggvények ──────────────────────────────────────────

    IEnumerator GetCoroutine(string url, Action<string, string> onDone)
    {
        using var req = UnityWebRequest.Get(url);
        SetHeaders(req, auth: true);
        req.timeout = (int)timeoutSeconds;
        yield return req.SendWebRequest();

        if (req.responseCode == 401)
        {
            yield return ReloginCoroutine();
            // Retry
            using var req2 = UnityWebRequest.Get(url);
            SetHeaders(req2, auth: true);
            req2.timeout = (int)timeoutSeconds;
            yield return req2.SendWebRequest();
            if (req2.result != UnityWebRequest.Result.Success)
                onDone(null, ParseError(req2));
            else
                onDone(req2.downloadHandler.text, null);
            yield break;
        }

        if (req.result != UnityWebRequest.Result.Success)
            onDone(null, $"HTTP hiba: {req.responseCode} – {req.error}");
        else
            onDone(req.downloadHandler.text, null);
    }

    IEnumerator PostCoroutine(string url, string body, bool auth, Action<string, string> onDone)
    {
        byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler   = new UploadHandlerRaw(bodyBytes);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        SetHeaders(req, auth);
        req.timeout = (int)timeoutSeconds;
        yield return req.SendWebRequest();

        if (req.responseCode == 401 && auth)
        {
            yield return ReloginCoroutine();
            // Retry
            byte[] bodyBytes2 = Encoding.UTF8.GetBytes(body);
            using var req2 = new UnityWebRequest(url, "POST");
            req2.uploadHandler   = new UploadHandlerRaw(bodyBytes2);
            req2.downloadHandler = new DownloadHandlerBuffer();
            req2.SetRequestHeader("Content-Type", "application/json");
            SetHeaders(req2, auth: true);
            req2.timeout = (int)timeoutSeconds;
            yield return req2.SendWebRequest();
            if (req2.result != UnityWebRequest.Result.Success)
                onDone(null, ParseError(req2));
            else
                onDone(req2.downloadHandler.text, null);
            yield break;
        }

        if (req.result != UnityWebRequest.Result.Success)
            onDone(null, ParseError(req));
        else
            onDone(req.downloadHandler.text, null);
    }

    /// <summary>401 esetén automatikusan újra-bejelentkezik a tárolt adatokkal.</summary>
    IEnumerator ReloginCoroutine()
    {
        if (_isRelogging) yield break;
        _isRelogging = true;
        SessionToken = null;
        Debug.Log("[MMC] 401 – automatikus újra-bejelentkezés...");

        bool done = false;
        if (!string.IsNullOrEmpty(_savedIdToken))
        {
            VerifyGoogleToken(_savedIdToken,
                onSuccess: (_, __) => done = true,
                onError:   _       => done = true);
        }
        else if (!string.IsNullOrEmpty(_savedUserId))
        {
            DevLogin(_savedUserId, _savedDisplayName,
                onSuccess: () => done = true,
                onError:   _  => done = true);
        }
        else
        {
            done = true;
        }

        float t = 0f;
        while (!done && t < 5f) { yield return new WaitForSeconds(0.1f); t += 0.1f; }
        _isRelogging = false;
        Debug.Log($"[MMC] Újra-bejelentkezés kész. Token: {(string.IsNullOrEmpty(SessionToken) ? "HIÁNYZIK" : "OK")}");
    }

    IEnumerator DeleteCoroutine(string url)
    {
        using var req = UnityWebRequest.Delete(url);
        SetHeaders(req, auth: true);
        req.timeout = (int)timeoutSeconds;
        yield return req.SendWebRequest();
        Debug.Log($"[MMC] DELETE {url} → {req.responseCode}");
    }

    void SetHeaders(UnityWebRequest req, bool auth)
    {
        req.SetRequestHeader("Accept", "application/json");
        if (auth && !string.IsNullOrEmpty(SessionToken))
            req.SetRequestHeader("Authorization", $"Bearer {SessionToken}");
    }

    string ParseError(UnityWebRequest req)
    {
        // Próbáljuk kiolvasni a FastAPI hibaüzenetet
        try
        {
            if (!string.IsNullOrEmpty(req.downloadHandler?.text))
            {
                var errJson = JsonUtility.FromJson<ErrorJson>(req.downloadHandler.text);
                if (!string.IsNullOrEmpty(errJson?.detail))
                    return errJson.detail;
            }
        }
        catch { }
        return $"Hiba {req.responseCode}: {req.error}";
    }

    static string EscapeJson(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    // ── JSON segédosztályok ──────────────────────────────────────────

    [Serializable] class SessionResponseJson
    {
        public string session_token;
        public string player_id;
        public string display_name;
    }

    [Serializable] public class MatchInfoJson
    {
        public string match_id;
        public string match_name;
        public string host_name;
        public int    current_players;
        public int    max_players;
        public string state;
        public string server_host;
        public int    server_port;
    }

    [Serializable] class MatchListWrapper
    {
        public MatchInfoJson[] items;
    }

    [Serializable] class JoinMatchResponseJson
    {
        public string match_id;
        public string server_host;
        public int    server_port;
        public string server_connect_ip;   // LAN kliensnek a Caddy LAN IP-je (NAT loopback elkerülés); üres = DNS feloldás
    }

    [Serializable] class ErrorJson
    {
        public string detail;
    }

    [Serializable] class MyRankResponseJson
    {
        public int rank;
    }

    [Serializable] class MatchmakingStatusJson
    {
        public string status;        // "waiting" | "matched" | "not_queued"
        public int    countdown;     // visszaszámlálás (másodperc, 0 ha matched)
        public int    player_count;  // jelenlegi létszám a lobby-ban
        public string match_id;
        public string server_host;
        public int    server_port;
        public string server_connect_ip;   // LAN kliensnek a Caddy LAN IP-je (NAT loopback elkerülés); üres = DNS feloldás
        public string[] player_names;
        public int[]    player_ranks;
    }
}
