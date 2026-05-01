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
    public Button          waitLeaveButton;

    // ── Auto Lobby beállítások ────────────────────────────────────────

    [Header("Auto Lobby")]
    [Tooltip("Hány másodpercig polloz meccset (utána SOLO indul)")]
    public float autoLobbyWaitSeconds = 10f;
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
        SetWaitStatus("Csatlakozás a sorhoz...");
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
        MatchmakingClient.Instance?.LeaveMatchmakingQueue();
        NetworkGameManager.Instance?.Disconnect();
        ShowPanel(modeSelectPanel);
    }

    /// <summary>"JOIN (QR)" gomb – megnyitja a QR olvasó panelt.</summary>
    public void OnJoinPressed() => ShowPanel(joinPanel);

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

    IEnumerator AutoLobbyPollCoroutine()
    {
        // Helyi fallback számláló arra az esetre, ha a server nem küld countdown-t
        int localCountdown = Mathf.CeilToInt(autoLobbyWaitSeconds);

        while (true)
        {
            bool responseReceived = false;
            bool done = false;

            MatchmakingClient.Instance?.PollMatchmakingStatus(
                onMatched: (host, port, matchId, playerCount) =>
                {
                    responseReceived = true;
                    done = true;
                    SetWaitStatus($"Join... ({playerCount} Player)");
                    NetworkGameManager.Instance?.StartClient(host, port);
                },
                onWaiting: (countdown, playerCount) =>
                {
                    responseReceived = true;
                    localCountdown = countdown;
                    SetWaitStatus($"Waiting: {countdown}s  |  Players: {playerCount}");
                },
                onError: err =>
                {
                    responseReceived = true;
                    SetWaitStatus($"Error: {err}");
                }
            );

            // Várjuk a HTTP választ (max 2 mp)
            float waited = 0f;
            while (!responseReceived && waited < 2f)
            {
                yield return new WaitForSeconds(0.1f);
                waited += 0.1f;
            }

            if (done) yield break;

            // Ha a szerver nem válaszolt → helyi countdown csökkentés
            if (!responseReceived) localCountdown--;

            // Ha helyi countdown lejárt és a szerver nem jelezte a matched-et → SOLO mód
            if (localCountdown <= 0)
            {
                SetWaitStatus("Nem találtunk partnert – SOLO mód indul...");
                yield return new WaitForSeconds(1f);
                MatchmakingClient.Instance?.LeaveMatchmakingQueue();
                StartSoloMode();
                _autoLobbyCoroutine = null;
                yield break;
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
            onSuccess: () => NetworkGameManager.Instance?.RequestGameStart(),
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
