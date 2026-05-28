using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

/// <summary>
/// Dedikált szerver bootstrap – csak a Unity Server build-ben fut (#if UNITY_SERVER).
///
/// Parancssori argumentumok:
///   -port  &lt;szám&gt;    : melyik WebSocket porton figyeljen (pl. 9000)
///   -matchId &lt;szöveg&gt; : a Python matchmaking által adott meccs azonosító
///
/// Életciklus:
///   1. Start() → ParseCommandLineArgs() → StartDedicatedServer()
///   2. A szerver vár kliensekre (NetworkGameManager kezeli a logikát)
///   3. Ha 60 másodpercig 0 kliens csatlakozik → Application.Quit()
///   4. Ha a meccs véget ér (_gameEnded) → 30 mp türelmi idő → Application.Quit()
///
/// A Python matchmaking a folyamat leállásán keresztül tudja, hogy a meccs véget ért,
/// és felszabadítja a portot.
/// </summary>
public class DedicatedServerBootstrap : MonoBehaviour
{
#if UNITY_SERVER

    [Header("Alapértelmezett értékek (ha nincs parancssori arg)")]
    public ushort defaultPort   = 9000;
    public string defaultMatchId = "test";

    private ushort _assignedPort;
    private string _matchId;

    /// <summary>
    /// Ha true, az auto-start timer le van tiltva.
    /// A játékot kizárólag a room owner RequestGameStart() üzenete indítja el.
    /// Parancssori: -manualStart (flag, nincs értéke).
    /// </summary>
    private bool _manualStart = false;

    /// <summary>Ennyi másodperc üresjárat után lép ki a szerver (lobbyban várva).</summary>
    private const float EMPTY_LOBBY_TIMEOUT = 60f;

    /// <summary>Ennyi másodperccel a meccs vége után lép ki (statsmentés ideje).</summary>
    private const float POST_GAME_SHUTDOWN_DELAY = 30f;

    /// <summary>
    /// Ha a meccs vége után minden valódi kliens lecsatlakozott, már nem várunk
    /// 30 másodpercet – ennyi után állunk le (mobilról a kliensek azonnal lépnek ki).
    /// </summary>
    private const float POST_GAME_FAST_SHUTDOWN_DELAY = 3f;

    /// <summary>
    /// Játék közben (a `_gameStarted == true` után), ha 0 valódi kliens marad,
    /// ennyi másodperccel később biztonsági shutdown – arra az esetre, ha a
    /// `_gameEnded` flag valamilyen race miatt nem áll be (8 fős, vegyes hálón
    /// több párhuzamos disconnect előfordulhat).
    /// </summary>
    private const float IN_GAME_EMPTY_SHUTDOWN_DELAY = 20f;

    /// <summary>
    /// Az első kliens csatlakozása után ennyi másodperccel automatikusan
    /// elindítja a játékot (auto lobby módban), ha az elvárt játékosszám nem telt be.
    /// </summary>
    private const float AUTO_START_GRACE = 20f;

    private float _emptyTimer        = 0f;
    private float _postGameTimer     = -1f;  // -1 = még nincs meccs vége
    private float _inGameEmptyTimer  = -1f;  // -1 = van kliens (vagy nem indult még a meccs)
    private bool  _gameStarted       = false;
    private bool  _autoStartPending  = false;
    private bool  _shuttingDown      = false;

    /// <summary>
    /// Hány valódi klienst várunk (a -expectedPlayers parancssori arg alapján).
    /// Ha mindenki megvan, a grace period lejárta előtt azonnal indul a meccs.
    /// </summary>
    private int _expectedPlayers = 0;

    void Start()
    {
        ParseCommandLineArgs();
        StartDedicatedServer();
    }

    void ParseCommandLineArgs()
    {
        _assignedPort = defaultPort;
        _matchId      = defaultMatchId;
        _manualStart  = false;

        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-port" && i + 1 < args.Length)
            {
                if (ushort.TryParse(args[i + 1], out ushort p))
                    _assignedPort = p;
            }
            else if (args[i] == "-matchId" && i + 1 < args.Length)
            {
                _matchId = args[i + 1];
            }
            else if (args[i] == "-manualStart")
            {
                _manualStart = true;
            }
            else if (args[i] == "-expectedPlayers" && i + 1 < args.Length)
            {
                if (int.TryParse(args[i + 1], out int ep))
                    _expectedPlayers = ep;
            }
        }

        Debug.Log($"[Bootstrap] port={_assignedPort} | matchId={_matchId} | manualStart={_manualStart} | expectedPlayers={_expectedPlayers}");
    }

    void StartDedicatedServer()
    {
        if (NetworkGameManager.Instance == null)
        {
            Debug.LogError("[Bootstrap] NetworkGameManager.Instance null! Győzödj meg, hogy a scene-ben van.");
            Application.Quit(1);
            return;
        }

        NetworkGameManager.Instance.StartHostAsServer(_assignedPort);

        if (_manualStart)
        {
            // Manuális mód: csak a room owner RequestGameStart() jelére indul.
            // Az auto-start timert nem regisztráljuk.
            Debug.Log("[Bootstrap] Manuális indítási mód – auto-start letiltva.");
        }
        else
        {
            // Auto-start mód (Auto Lobby): az első kliens csatlakozásától
            // AUTO_START_GRACE másodperccel automatikusan elindítja a játékot.
            NetworkManager.Singleton.OnClientConnectedCallback += OnAnyClientConnected;
        }

        // Figyeljük a meccs végét. A GameManager.Instance jelenleg még valószínűleg
        // null (lobby scene-ben vagyunk), ezért scene-betöltésre is feliratkozunk
        // és ott újra próbáljuk a subscribe-ot.
        TrySubscribeOnGameOver();
        SceneManager.sceneLoaded += OnSceneLoadedSubscribeHook;

        Debug.Log($"[Bootstrap] Dedikált szerver elindult | port={_assignedPort} | matchId={_matchId}");
    }

    void OnAnyClientConnected(ulong clientId)
    {
        // A szerver saját host-loopbackja (LocalClientId) NEM számít valódi kliensnek –
        // különben a grace periódus már a StartHost pillanatában elindulna,
        // és sokszor lejárna mielőtt valódi kliens befér.
        if (NetworkManager.Singleton != null &&
            clientId == NetworkManager.Singleton.LocalClientId) return;

        if (_gameStarted) return;

        // Ha mindenki megérkezett → azonnal indul, nem kell várni a grace periodet.
        int realClients = NetworkManager.Singleton != null
            ? Mathf.Max(0, NetworkManager.Singleton.ConnectedClients.Count - 1)
            : 0;
        if (_expectedPlayers > 0 && realClients >= _expectedPlayers)
        {
            if (_autoStartPending)
                StopAllCoroutines(); // grace coroutine leállítása
            _autoStartPending = true;
            _gameStarted = true;
            Debug.Log($"[Bootstrap] Minden várt játékos csatlakozott ({realClients}/{_expectedPlayers}) – azonnali indítás.");
            NetworkGameManager.Instance?.TriggerGameStart();
            return;
        }

        if (_autoStartPending) return;
        _autoStartPending = true;
        Debug.Log($"[Bootstrap] Első kliens csatlakozott (id={clientId}) – {AUTO_START_GRACE}s grace indul (várt: {_expectedPlayers}).");
        StartCoroutine(AutoStartCoroutine());
    }

    System.Collections.IEnumerator AutoStartCoroutine()
    {
        yield return new WaitForSeconds(AUTO_START_GRACE);

        if (_gameStarted) yield break;

        int realClients = NetworkManager.Singleton != null
            ? Mathf.Max(0, NetworkManager.Singleton.ConnectedClients.Count - 1)
            : 0;
        _gameStarted = true;
        Debug.Log($"[Bootstrap] Grace period lejárt – indítás {realClients} játékossal (várt: {_expectedPlayers}).");
        NetworkGameManager.Instance?.TriggerGameStart();
    }

    void Update()
    {
        if (_shuttingDown) return;

        // FONTOS: a NetworkManager elveszhet (transport hiba, mobil instabilitás).
        // Ilyenkor a futó timereket NEM fagyasztjuk be – továbbjárnak, hogy a
        // process biztosan kilépjen még akkor is, ha a hálózati réteg már halott.
        bool nmAlive = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;

        // Játékosok száma (a szerver saját lokális id-ját nem számoljuk)
        int clientCount = 0;
        if (nmAlive)
        {
            clientCount = NetworkManager.Singleton.ConnectedClients.Count - 1;
            if (clientCount < 0) clientCount = 0;
        }

        // ── Üres lobby timeout ───────────────────────────────────────
        if (!_gameStarted && nmAlive)
        {
            if (clientCount == 0)
            {
                _emptyTimer += Time.deltaTime;
                if (_emptyTimer >= EMPTY_LOBBY_TIMEOUT)
                {
                    Debug.Log($"[Bootstrap] {EMPTY_LOBBY_TIMEOUT}s üresjárat – leállás.");
                    Shutdown();
                    return;
                }
            }
            else
            {
                _emptyTimer = 0f;
            }
        }

        // ── Meccs közben "kiürült" biztonsági shutdown ───────────────
        // Ha a meccs elindult és a `_gameEnded` flag a NGM-ben nem áll be (race
        // 8 fős vegyes hálón), de már 0 valódi kliens van bent → kilépünk.
        if (_gameStarted && _postGameTimer < 0f && nmAlive)
        {
            if (clientCount == 0)
            {
                if (_inGameEmptyTimer < 0f)
                {
                    _inGameEmptyTimer = 0f;
                    Debug.Log($"[Bootstrap] Játék közben 0 kliens – {IN_GAME_EMPTY_SHUTDOWN_DELAY}s biztonsági timer indul.");
                }
                else
                {
                    _inGameEmptyTimer += Time.deltaTime;
                    if (_inGameEmptyTimer >= IN_GAME_EMPTY_SHUTDOWN_DELAY)
                    {
                        Debug.Log("[Bootstrap] Játék közben senki nem maradt – kényszer-leállás.");
                        Shutdown();
                        return;
                    }
                }
            }
            else if (_inGameEmptyTimer >= 0f)
            {
                _inGameEmptyTimer = -1f;
            }
        }

        // ── Meccs vége utáni leállás ─────────────────────────────────
        if (_postGameTimer >= 0f)
        {
            _postGameTimer += Time.deltaTime;

            // Ha közben már mindenki lecsatlakozott, ne várjuk ki a 30s-et.
            float threshold = (nmAlive && clientCount > 0)
                ? POST_GAME_SHUTDOWN_DELAY
                : POST_GAME_FAST_SHUTDOWN_DELAY;

            if (_postGameTimer >= threshold)
            {
                Debug.Log($"[Bootstrap] Meccs vége utáni türelmi idő lejárt ({threshold:0.#}s, kliensek={clientCount}) – leállás.");
                Shutdown();
                return;
            }
        }

        // ── Ha a hálózati réteg meghalt és semmi nem fut, kilépünk ────
        if (!nmAlive && _postGameTimer < 0f && _inGameEmptyTimer < 0f)
        {
            // Csak akkor, ha a meccs már elindult – induláskor a NM még nem listening.
            if (_gameStarted)
            {
                Debug.LogWarning("[Bootstrap] NetworkManager halott a meccs közben – azonnali leállás.");
                Shutdown();
            }
        }
    }

    void OnGameEnded()
    {
        // Csak az ELSŐ meccs-vége esemény indítsa a timert. 8 fős meccsen több
        // OnGameOver tüzelhet (pl. egymás utáni eliminációk + disconnect-ek);
        // ha minden alkalommal nulláznánk, a 30s newer-érne véget.
        if (_postGameTimer >= 0f)
        {
            Debug.Log($"[Bootstrap] Ismételt OnGameOver – timer már fut ({_postGameTimer:0.#}s).");
            return;
        }
        _postGameTimer = 0f;
        Debug.Log($"[Bootstrap] Meccs vége detektálva | matchId={_matchId} | leállás {POST_GAME_SHUTDOWN_DELAY}s múlva (üres szerveren {POST_GAME_FAST_SHUTDOWN_DELAY}s).");
    }

    /// <summary>
    /// Megpróbál feliratkozni a GameManager.OnGameOver eventre. Csak akkor fut
    /// le, ha az Instance már létezik (a játékscene betöltődése után).
    /// Idempotent: az event subscription duplikációt elkerüljük előbb leiratkozással.
    /// </summary>
    void TrySubscribeOnGameOver()
    {
        if (GameManager.Instance == null) return;
        GameManager.Instance.OnGameOver -= OnGameEnded;
        GameManager.Instance.OnGameOver += OnGameEnded;
        Debug.Log("[Bootstrap] GameManager.OnGameOver feliratkozás OK.");
    }

    void OnSceneLoadedSubscribeHook(Scene scene, LoadSceneMode mode)
    {
        TrySubscribeOnGameOver();
    }

    void Shutdown()
    {
        if (_shuttingDown) return;
        _shuttingDown = true;

        try { NetworkManager.Singleton?.Shutdown(); } catch { /* ignore */ }

        // Application.Quit() headless szervereken megbízhatatlan – előfordulhat
        // hogy a process "fent marad" a háttérben (pl. stuck Unity thread miatt).
        // System.Environment.Exit() azonnal megszünteti a folyamatot, a Python
        // matchmaking a returncode alapján észleli és felszabadítja a portot.
        // Ha az Exit() valamilyen finalizer-blokkon megakadna, Process.Kill() a
        // végső eszköz – mindenképp leáll a process.
        Debug.Log("[Bootstrap] Leállás – System.Environment.Exit(0)");
        try
        {
            System.Environment.Exit(0);
        }
        catch { /* ignore – fall through to hard kill */ }

        try
        {
            System.Diagnostics.Process.GetCurrentProcess().Kill();
        }
        catch { /* utolsó esély elbukott – nincs más hátra */ }
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoadedSubscribeHook;
        if (GameManager.Instance != null)
            GameManager.Instance.OnGameOver -= OnGameEnded;
        if (NetworkManager.Singleton != null && !_manualStart)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnAnyClientConnected;
    }

#else
    // Nem szerver build: ez a komponens nem csinál semmit.
    void Awake()
    {
        // Fejlesztői tipp: a DedicatedServerBootstrap csak UNITY_SERVER buildben aktív.
        // Ha tesztelni akarod szerver módban, használj: Build Settings → Server Build.
        Debug.Log("[Bootstrap] Nem szerver build – bootstrap inaktív.");
        Destroy(this);
    }
#endif
}
