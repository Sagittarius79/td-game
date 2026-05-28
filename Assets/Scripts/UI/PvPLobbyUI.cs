using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PvP lobby képernyő.
///
/// Panelek:
///   modeSelectPanel  – "Auto Lobby" + "HOST (QR)" + "VISSZA"
///   hostPanel        – QR kód megjelenítő + játékosszám + START gomb + Kilépés
///   waitingPanel     – visszaszámláló + státusz + Kilépés gomb
///
/// Auto Lobby folyamat:
///   1. Játékos megnyomja az "Auto Lobby" gombot
///   2. Belép a matchmaking sorba a szerveren (szint alapján párosít)
///   3. 2 másodpercenként polloz → ha talált meccset → StartClient()
///   4. Ha 10 mp után sem talál párt → SOLO módban indul
///
/// HOST (QR) folyamat:
///   1. Játékos megnyomja a "HOST" gombot
///   2. Meccs jön létre a szerveren → QR kód tartalmaz meccs ID-t
///   3. Másik játékos leolvassa a QR kódot → csatlakozik
///   4. HOST megnyomja a START gombot (korai indítás) VAGY automatikus 10s után
/// </summary>
public class PvPLobbyUI : MonoBehaviour
{
    public static PvPLobbyUI Instance { get; private set; }

    // ── Panelek ──────────────────────────────────────────────────────

    [Header("Panelek")]
    public GameObject modeSelectPanel;
    public GameObject hostPanel;
    public GameObject joinPanel;     // QR kód olvasó panel (régi JoinPanel)
    public GameObject waitingPanel;

    // ── HOST panel ───────────────────────────────────────────────────

    [Header("HOST panel")]
    public TextMeshProUGUI hostPlayerCountText;
    public TextMeshProUGUI hostStatusText;
    public Button          hostStartButton;   // korai indítás (opcionális)
    public Button          hostLeaveButton;
    public QRCodeDisplay   qrCodeDisplay;

    // ── Waiting panel (Auto Lobby) ────────────────────────────────────

    [Header("Waiting panel")]
    public TextMeshProUGUI waitStatusText;
    public TextMeshProUGUI waitPlayerCountText;
    public Button          waitLeaveButton;

    [Header("Tutorial (opcionális – várakozás közben)")]
    [Tooltip("Ha be van kötve, a várakozás alatt lapozható tutorial kártyák jelennek meg")]
    public TutorialCardsUI tutorialCards;
    public Button          tooltipsButton;

    // ── Hangok ───────────────────────────────────────────────────────

    [Header("Hangok")]
    [Tooltip("Lejátssza amikor PvP meccs indul")]
    public AudioClip pvpStartSound;

    // ── Auto Lobby beállítások ────────────────────────────────────────

    [Header("Auto Lobby")]
    [Tooltip("Ha a matchmaking API ennyi másodpercen át egyáltalán nem válaszol (hálózati hiba / szerver leállt), " +
             "a kliens feladja és SOLO módban indul. A lobby visszaszámlálást a SZERVER vezérli " +
             "(match_manager.py: LOBBY_COUNTDOWN_SECS), ez a paraméter arra nincs hatással.")]
    public float autoLobbyWaitSeconds = 60f;
    [Tooltip("Pollozás gyakorisága másodpercekben")]
    public float pollIntervalSeconds  = 2f;

    // ── Privát ───────────────────────────────────────────────────────

    private Coroutine _autoLobbyCoroutine;
    private Coroutine _hostCountdownCoroutine;

    // ── Lifecycle ────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        ShowPanel(modeSelectPanel);

        if (NetworkGameManager.Instance != null)
        {
            NetworkGameManager.Instance.OnPlayerCountChanged += UpdateHostPlayerCount;
            NetworkGameManager.Instance.OnRoomOwnerChanged   += OnRoomOwnerChanged;
            NetworkGameManager.Instance.OnConnectionFailed   += OnConnectionFailed;
        }

        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
    }

    void OnDestroy()
    {
        if (NetworkGameManager.Instance != null)
        {
            NetworkGameManager.Instance.OnPlayerCountChanged -= UpdateHostPlayerCount;
            NetworkGameManager.Instance.OnRoomOwnerChanged   -= OnRoomOwnerChanged;
            NetworkGameManager.Instance.OnConnectionFailed   -= OnConnectionFailed;
        }

        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    /// <summary>NGM watchdog jelzi: nem épült ki a játékszerverhez a kapcsolat.</summary>
    void OnConnectionFailed(string message)
    {
        Debug.LogWarning($"[Lobby] Kapcsolódási hiba: {message}");

        // Auto Lobby polling leállítása ha fut
        if (_autoLobbyCoroutine != null) { StopCoroutine(_autoLobbyCoroutine); _autoLobbyCoroutine = null; }
        if (_hostCountdownCoroutine != null) { StopCoroutine(_hostCountdownCoroutine); _hostCountdownCoroutine = null; }

        tutorialCards?.Hide();
        MatchmakingClient.Instance?.LeaveMatchmakingQueue();

        SetWaitStatus(message);
        if (hostStatusText != null) hostStatusText.text = message;
        ShowPanel(waitingPanel);

        StartCoroutine(ReturnToMenuAfter(5f));
    }

    IEnumerator ReturnToMenuAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        ShowPanel(modeSelectPanel);
    }

    // ── Gombok (Inspectorból kösd be) ─────────────────────────────────

    /// <summary>"Auto Lobby" gomb – szint alapú matchmaking.</summary>
    public void OnAutoLobbyPressed()
    {
        if (MatchmakingClient.Instance == null) return;
        if (!IsSessionReady(onReady: StartAutoLobby)) return;
        StartAutoLobby();
    }

    void StartAutoLobby()
    {
        SetWaitStatus("Joining queue...");
        ShowPanel(waitingPanel);
        if (waitLeaveButton != null) waitLeaveButton.interactable = true;

        int level = GetPlayerLevel();

        MatchmakingClient.Instance.JoinMatchmakingQueue(level,
            onSuccess: () =>
            {
                SetWaitStatus("Játékos keresése...");
                if (_autoLobbyCoroutine != null) StopCoroutine(_autoLobbyCoroutine);
                _autoLobbyCoroutine = StartCoroutine(AutoLobbyPollCoroutine());
            },
            onError: err =>
            {
                SetWaitStatus($"Hiba: {err}");
                ShowPanel(modeSelectPanel);
            }
        );
    }

    /// <summary>"HOST (QR)" gomb – meccs létrehozása, QR kód megjelenítése.</summary>
    public void OnHostPressed()
    {
        if (MatchmakingClient.Instance == null) return;
        if (!IsSessionReady(onReady: StartHost)) return;
        StartHost();
    }

    void StartHost()
    {
        if (hostStatusText != null) hostStatusText.text = "Meccs létrehozása...";
        ShowPanel(hostPanel);

        MatchmakingClient.Instance.CreateMatch("Host Meccs", maxPlayers: 10,
            manualStart: true,
            onSuccess: (host, port, matchId) =>
            {
                NetworkGameManager.Instance.StartClient(host, port);

                if (hostStatusText    != null) hostStatusText.text    = "Várakozás játékosokra...";
                if (hostStartButton   != null) hostStartButton.interactable = false;
                if (qrCodeDisplay     != null) qrCodeDisplay.ShowQR(matchId);

                // 10 mp után automatikusan indul
                if (_hostCountdownCoroutine != null) StopCoroutine(_hostCountdownCoroutine);
                _hostCountdownCoroutine = StartCoroutine(HostCountdownCoroutine());
            },
            onError: err =>
            {
                if (hostStatusText != null) hostStatusText.text = $"Hiba: {err}";
                ShowPanel(modeSelectPanel);
            }
        );
    }

    /// <summary>HOST panel START gomb – korai indítás (legalább 2 játékosnál).</summary>
    public void OnHostStartPressed()
    {
        if (_hostCountdownCoroutine != null)
        {
            StopCoroutine(_hostCountdownCoroutine);
            _hostCountdownCoroutine = null;
        }
        StartMatch();
    }

    /// <summary>HOST panel Kilépés gomb.</summary>
    public void OnHostLeavePressed()
    {
        if (_hostCountdownCoroutine != null) StopCoroutine(_hostCountdownCoroutine);
        if (qrCodeDisplay != null) qrCodeDisplay.HideQR();
        MatchmakingClient.Instance?.CloseMatch();
        NetworkGameManager.Instance?.Disconnect();
        ShowPanel(modeSelectPanel);
    }

    /// <summary>Waiting panel Kilépés gomb (Auto Lobby megszakítása).</summary>
    public void OnWaitLeavePressed()
    {
        if (_autoLobbyCoroutine != null) StopCoroutine(_autoLobbyCoroutine);
        _autoLobbyCoroutine = null;
        _autoLobbyMatched   = true;   // a még in-flight callbackek ne hívjanak StartClient-et
        _autoLobbyInFlight  = false;
        tutorialCards?.Hide();
        MatchmakingClient.Instance?.LeaveMatchmakingQueue();
        NetworkGameManager.Instance?.Disconnect();
        ShowPanel(modeSelectPanel);
    }

    /// <summary>"JOIN (QR)" gomb – megnyitja a QR olvasó panelt.</summary>
    public void OnJoinPressed() => ShowPanel(joinPanel);

    /// <summary>ToolTips gomb – tutorial kártyák megjelenítése.</summary>
    public void OnTooltipsPressed() => tutorialCards?.Show(null, auto: false);

    /// <summary>VISSZA gomb a modeSelectPanel-en – főmenübe navigál.</summary>
    public void OnBackPressed()
    {
        if (modeSelectPanel != null && modeSelectPanel.activeSelf)
        {
            string scene = NetworkGameManager.Instance != null
                ? NetworkGameManager.Instance.mainMenuSceneName
                : "MainMenu";
            UnityEngine.SceneManagement.SceneManager.LoadScene(scene);
        }
        else
        {
            ShowPanel(modeSelectPanel);
        }
    }

    /// <summary>
    /// QRCodeScanner hívja sikeres leolvasás után.
    /// A QR kód tartalmazza a meccs ID-t – a szerveren csatlakozunk hozzá.
    /// </summary>
    public void OnQRScanned(string matchId)
    {
        if (string.IsNullOrEmpty(matchId)) return;
        SetWaitStatus("QR kód alapján csatlakozás...");
        ShowPanel(waitingPanel);

        MatchmakingClient.Instance?.JoinMatch(matchId,
            onSuccess: (host, port, id) =>
            {
                NetworkGameManager.Instance?.StartClient(host, port);
                SetWaitStatus("You joined! Wait for start...");
            },
            onError: err =>
            {
                SetWaitStatus($"Something wrong: {err}");
                ShowPanel(modeSelectPanel);
            }
        );
    }

    // ── Auto Lobby coroutine ──────────────────────────────────────────

    // Coroutine-szintű flag-ek a race condition elkerülésére:
    // - _autoLobbyMatched: ha már egyszer matched-et kaptunk és StartClient elindult,
    //   minden további (késett) callbacket eldobunk.
    // - _autoLobbyInFlight: van-e jelenleg pending HTTP kérés a /matchmaking/status-ra
    //   (megakadályozza hogy lassú WebGL hálón párhuzamos kérések halmozódjanak fel).
    private bool _autoLobbyMatched = false;
    private bool _autoLobbyInFlight = false;

    IEnumerator AutoLobbyPollCoroutine()
    {
        // SOLO fallback: csak akkor lép be, ha a szerver több poll-on át sem válaszol
        // (matchmaking API leállt vagy hálózati gond). A "várjunk N másodpercig 2.
        // játékosra" logikát a SZERVER vezeti a countdown mezőn keresztül — ezt
        // jelenítjük meg pontosan, hogy a kliens-óra ne térjen el a szerverétől.
        _autoLobbyMatched  = false;
        _autoLobbyInFlight = false;
        int consecutiveTimeouts = 0;
        const int maxConsecutiveTimeouts = 8;   // ~8 poll cikluson át nincs válasz → SOLO

        while (true)
        {
            if (_autoLobbyMatched) yield break;
            if (_autoLobbyInFlight)
            {
                // Még él az előző kérés — ne indítsunk újat, várjuk meg.
                yield return new WaitForSeconds(0.5f);
                continue;
            }

            _autoLobbyInFlight = true;
            bool responseReceived = false;
            int  polledCount      = 0;
            int  polledCountdown  = 0;
            string[] polledNames  = null;
            int[]    polledRanks  = null;
            int[]    polledLevels = null;
            string   pollError    = null;

            MatchmakingClient.Instance?.PollMatchmakingStatus(
                onMatched: (host, port, matchId, playerCount, playerNames, playerRanks, playerLevels, goldBonus) =>
                {
                    _autoLobbyInFlight = false;
                    if (_autoLobbyMatched) return;   // késett duplikált válasz – eldobjuk
                    _autoLobbyMatched = true;
                    responseReceived = true;
                    SetWaitPlayerCount(playerCount);
                    SetWaitStatus($"Match found!\n{FormatPlayerNames(playerNames, playerRanks, playerLevels)}");
                    if (NetworkGameManager.Instance != null)
                        NetworkGameManager.Instance.LevelGoldBonus = goldBonus;
                    tutorialCards?.Hide();
                    AudioManager.Instance?.PlaySFX(pvpStartSound);
                    NetworkGameManager.Instance?.StartClient(host, port);
                },
                onWaiting: (countdown, playerCount, playerNames, playerRanks, playerLevels) =>
                {
                    _autoLobbyInFlight = false;
                    if (_autoLobbyMatched) return;   // már matched, ne írjuk felül a UI-t
                    responseReceived = true;
                    SetWaitPlayerCount(playerCount);
                    polledCount     = playerCount;
                    polledCountdown = countdown;
                    polledNames     = playerNames;
                    polledRanks     = playerRanks;
                    polledLevels    = playerLevels;
                },
                onError: err =>
                {
                    _autoLobbyInFlight = false;
                    if (_autoLobbyMatched) return;
                    responseReceived = true;
                    pollError = err;
                }
            );

            // Várjuk a HTTP választ. WebGL-en a böngésző fetch lassabb is lehet,
            // így 8 mp-ig tartjuk magunkat, mielőtt timeoutként könyveljük.
            float waited = 0f;
            while (!responseReceived && _autoLobbyInFlight && waited < 8f)
            {
                yield return new WaitForSeconds(0.1f);
                waited += 0.1f;
            }

            if (_autoLobbyMatched) yield break;

            if (!responseReceived)
            {
                // Kérés még in-flight — időtúllépésnek tekintjük, de NEM lőjük el
                // kívülről (úgyis lefut, és a callbackek `_autoLobbyMatched` alapján
                // dobják el a stale választ). Csak addig várunk az új poll előtt,
                // amíg ez beérkezik vagy a SOLO fallback eldönti.
                consecutiveTimeouts++;
                SetWaitStatus($"Slow network... ({consecutiveTimeouts}/{maxConsecutiveTimeouts})");
                if (consecutiveTimeouts >= maxConsecutiveTimeouts)
                {
                    SetWaitStatus("No opponent found – starting SOLO mode...");
                    yield return new WaitForSeconds(1f);
                    MatchmakingClient.Instance?.LeaveMatchmakingQueue();
                    StartSoloMode();
                    _autoLobbyCoroutine = null;
                    yield break;
                }
                yield return new WaitForSeconds(1f);
                continue;
            }

            consecutiveTimeouts = 0;

            if (pollError != null)
            {
                SetWaitStatus($"Error: {pollError}");
            }
            else
            {
                string nameList = FormatPlayerNames(polledNames, polledRanks, polledLevels);
                if (polledCount < 2)
                {
                    // 1 játékos van a sorban: a szerver még nem indította el a countdown-t,
                    // így nem mutatunk konkrét másodpercszámot.
                    SetWaitStatus($"Waiting for players...\n{nameList}");
                }
                else
                {
                    // 2+ játékos: a szerver `countdown` értékét mutatjuk – a meccs pontosan
                    // akkor indul, amikor ez eléri a 0-t (és a következő poll már "matched").
                    SetWaitStatus($"Waiting: {polledCountdown}s  |  Players: {polledCount}\n{nameList}");
                }
            }

            yield return new WaitForSeconds(1f);
        }
    }

    // ── HOST várakozó hurok (nincs auto-start – csak a START gomb indítja) ──

    IEnumerator HostCountdownCoroutine()
    {
        while (true)
        {
            int count = NetworkGameManager.Instance?.GetConnectedPlayerCount() ?? 0;

            if (hostStatusText != null)
                hostStatusText.text = count >= 2
                    ? $"Játékosok: {count} – nyomd meg a START gombot!"
                    : "Várakozás csatlakozásra...";

            // START gomb csak akkor aktív, ha legalább 2 játékos van (szerver + 1 kliens)
            if (hostStartButton != null)
                hostStartButton.interactable = count >= 2;

            yield return new WaitForSeconds(1f);
        }
        // Nincs auto-start: a HOST manuálisan nyomja meg a START gombot.
    }

    // ── Indítás ───────────────────────────────────────────────────────

    void StartMatch()
    {
        if (hostStatusText != null) hostStatusText.text = "Játék indul...";
        if (hostStartButton != null) hostStartButton.interactable = false;

        MatchmakingClient.Instance?.StartMatch(
            onSuccess: () => { AudioManager.Instance?.PlaySFX(pvpStartSound); NetworkGameManager.Instance?.RequestGameStart(); },
            onError:   err =>
            {
                if (hostStatusText != null) hostStatusText.text = $"Hiba: {err}";
            }
        );
    }

    void StartSoloMode()
    {
        // SOLO módban a NetworkGameManager PvP nélkül tölti be a játékot
        NetworkGameManager.Instance.IsPvPMode = false;
        // Direktben betölti a játék scene-t
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            NetworkGameManager.Instance.pvpGameSceneName);
    }

    // ── Hálózati callbackok ───────────────────────────────────────────

    void OnClientDisconnected(ulong clientId)
    {
        if (NetworkManager.Singleton == null) return;
        if (clientId == NetworkManager.Singleton.LocalClientId)
            SetWaitStatus("Kapcsolat megszakadt!");
    }

    void UpdateHostPlayerCount(int count)
    {
        if (hostPlayerCountText != null)
            hostPlayerCountText.text = $"Játékosok: {count}";

        if (hostStartButton != null)
            hostStartButton.interactable = count >= 2;
    }

    void OnRoomOwnerChanged(ulong ownerId)
    {
        bool isMine = NetworkGameManager.Instance?.IsRoomOwner ?? false;
        if (hostStartButton != null)
            hostStartButton.gameObject.SetActive(isMine);
    }

    // ── Segéd ─────────────────────────────────────────────────────────

    void SetWaitStatus(string msg)
    {
        if (waitStatusText != null) waitStatusText.text = msg;
    }

    void SetWaitPlayerCount(int count)
    {
        if (waitPlayerCountText != null) waitPlayerCountText.text = $"{count}";
    }

    string FormatPlayerNames(string[] names, int[] ranks = null, int[] levels = null)
    {
        if (names == null || names.Length == 0) return "";

        string localName = (UserProgressManager.Instance?.CharacterName ?? "").Trim();

        // Párosítjuk a neveket a rangokkal és szintekkel, majd növekvő rang szerint rendezünk
        var entries = new (string name, int rank, int level)[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            entries[i] = (
                names[i],
                ranks  != null && i < ranks.Length  ? ranks[i]  : 0,
                levels != null && i < levels.Length ? levels[i] : 0
            );
        }

        System.Array.Sort(entries, (a, b) => a.rank.CompareTo(b.rank));

        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < entries.Length; i++)
        {
            if (i > 0) sb.Append('\n');

            bool isMe = !string.IsNullOrEmpty(localName) &&
                        string.Equals(entries[i].name.Trim(), localName, System.StringComparison.OrdinalIgnoreCase);

            if (isMe) sb.Append("<color=#5CFF8A>");
            sb.Append(entries[i].name);
            if (isMe) sb.Append("</color>");

            if (entries[i].rank > 0)
                sb.Append($" | Rank {entries[i].rank}");

            if (entries[i].level > 0)
                sb.Append($" | Lvl {entries[i].level}");
        }
        return sb.ToString();
    }

    void ShowPanel(GameObject show)
    {
        if (modeSelectPanel != null) modeSelectPanel.SetActive(show == modeSelectPanel);
        if (hostPanel       != null) hostPanel.SetActive(show       == hostPanel);
        if (joinPanel       != null) joinPanel.SetActive(show       == joinPanel);
        if (waitingPanel    != null) waitingPanel.SetActive(show    == waitingPanel);
    }

    int GetPlayerLevel()
    {
        return UserProgressManager.Instance?.Level ?? 1;
    }

    /// <summary>
    /// Ellenőrzi hogy a matchmaking session kész-e.
    /// Ha nem → vár a sessionre, majd automatikusan meghívja az onReady callbacket.
    /// </summary>
    bool IsSessionReady(System.Action onReady = null)
    {
        if (!string.IsNullOrEmpty(MatchmakingClient.Instance?.SessionToken))
            return true;

        SetWaitStatus("Bejelentkezés folyamatban...");
        ShowPanel(waitingPanel);
        StartCoroutine(WaitForSessionThenContinue(onReady));
        return false;
    }

    IEnumerator WaitForSessionThenContinue(System.Action onReady)
    {
        // Ha Google-ba be van jelentkezve de még nincs szerver session → kényszer-login
        if (GoogleAuthManager.Instance != null && GoogleAuthManager.Instance.IsSignedIn
            && string.IsNullOrEmpty(MatchmakingClient.Instance?.SessionToken))
        {
            SetWaitStatus("Szerver session megszerzése...");
            string idToken = GoogleAuthManager.Instance.IdToken;
            if (!string.IsNullOrEmpty(idToken))
            {
                MatchmakingClient.Instance.VerifyGoogleToken(idToken,
                    onSuccess: (dn, _) => Debug.Log($"[UI] Kényszer VerifyGoogleToken OK: {dn}"),
                    onError:   err     => Debug.LogWarning($"[UI] Kényszer VerifyGoogleToken hiba: {err}"));
            }
            else
            {
                MatchmakingClient.Instance.DevLogin(
                    GoogleAuthManager.Instance.UserId,
                    GoogleAuthManager.Instance.DisplayName,
                    onSuccess: () => Debug.Log("[UI] Kényszer DevLogin OK."),
                    onError:   err => Debug.LogWarning($"[UI] Kényszer DevLogin hiba: {err}"));
            }
        }

        // Várjuk a session tokent (max 8 mp)
        float waited = 0f;
        while (string.IsNullOrEmpty(MatchmakingClient.Instance?.SessionToken) && waited < 8f)
        {
            SetWaitStatus($"Bejelentkezés... ({Mathf.CeilToInt(8f - waited)}s)");
            yield return new WaitForSeconds(1f);
            waited += 1f;
        }

        if (!string.IsNullOrEmpty(MatchmakingClient.Instance?.SessionToken))
        {
            // Session megvan → automatikusan folytatjuk
            SetWaitStatus("Csatlakozás...");
            onReady?.Invoke();
        }
        else
        {
            bool googleOk = GoogleAuthManager.Instance?.IsSignedIn ?? false;
            bool clientOk = MatchmakingClient.Instance != null;
            SetWaitStatus(
                !clientOk ? "MatchmakingClient hiányzik – ellenőrizd az Inspector beállítást!" :
                !googleOk ? "Google bejelentkezés nem sikerült – próbáld újra a főmenüből." :
                            "Szerver nem elérhető – ellenőrizd a hálózatot.");
            yield return new WaitForSeconds(3f);
            ShowPanel(modeSelectPanel);
        }
    }

    // ── Legacy QR kompatibilitás ──────────────────────────────────────

    // (QRCodeScanner.cs-nek szüksége van erre a metódusra)
    // OnQRScanned már fent definiálva van.
}
