using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

/// <summary>
/// PvP hálózati munkamenet menedzser – singleton, DontDestroyOnLoad.
///
/// ARCHITEKTÚRA (dedikált szerveres mód):
///   - A szerver (kakaoo123.asuscomm.com) futtat egy Unity Dedicated Server .exe-t
///     minden meccshez (DedicatedServerBootstrap indítja).
///   - A "HOST" szerepű kliens NEM indít helyi NGO hostot – a Python matchmaking API-tól
///     kap vissza egy host:port párt, arra csatlakozik StartClient()-tel.
///   - A room owner (aki létrehozta a meccset) RequestGameStart()-on keresztül kérheti
///     az indítást, amelyet a szerver validál és terjeszti szét.
///   - Ha a room owner kiesik, a szerver automatikusan új room ownert nevez ki.
///
/// LEGACY TÁMOGATÁS:
///   - StartHost() megtartva – a régi közvetlen IP-s mód még működik (LAN teszt).
///
/// Üzenetfolyam (szörny küldés):
///   Host → MSG_SEND_ENEMY → minden kliensnek közvetlenül
///   Kliens → MSG_RELAY_ENEMY → szerver → szerver alkalmazza magára + MSG_SEND_ENEMY → többi kliensnek
///   Kastély eleste → szerver eltávolítja az élők közül → ha 1 marad, ő nyer
/// </summary>
public class NetworkGameManager : MonoBehaviour
{
    public static NetworkGameManager Instance { get; private set; }

    [Header("Scene nevek")]
    public string pvpGameSceneName  = "SampleScene";
    public string mainMenuSceneName = "MainMenu";

    [Header("Hálózat")]
    public ushort port = 7777;

    /// <summary>True, ha PvP mód aktív (nem solo).</summary>
    public bool IsPvPMode { get; set; } = false;

    /// <summary>Az utolsó meccs eredménye – UIManager.ShowGameOver() olvassa.</summary>
    public bool LastResultWon { get; private set; }

    /// <summary>A host által generált közös map seed – MapDecorator használja.</summary>
    public int SharedMapSeed { get; private set; }

    /// <summary>Az ellenfél neve – visszafelé kompatibilitáshoz megtartva.</summary>
    public string OpponentName { get; private set; } = "Opponent";

    /// <summary>Összes ismert játékos neve clientId alapján.</summary>
    private Dictionary<ulong, string> _playerNames = new Dictionary<ulong, string>();

    /// <summary>Visszaadja egy játékos nevét clientId alapján.</summary>
    public string GetPlayerName(ulong clientId)
    {
        if (_playerNames.TryGetValue(clientId, out string name)) return name;
        return "Player";
    }

    /// <summary>True ha az id valós (névvel regisztrált) játékoshoz tartozik – nem szerver ghost.</summary>
    public bool IsKnownPlayer(ulong clientId) => _playerNames.ContainsKey(clientId);

    /// <summary>
    /// Az összes ismert PvP játékos ID-ja, kivéve a megadott localId-t és a szervert.
    /// A SpyTower pool alap-listájaként használja az OpponentDataTracker.
    /// </summary>
    public IEnumerable<ulong> GetKnownPvpPlayerIds(ulong localClientId)
    {
        foreach (var id in _playerNames.Keys)
        {
            if (id == localClientId) continue;
            if (Unity.Netcode.NetworkManager.Singleton != null &&
                id == Unity.Netcode.NetworkManager.ServerClientId) continue;
            yield return id;
        }
    }

    /// <summary>Lobby UI-nak: szám frissítésekor hívódik meg (csatlakozott játékosok száma).</summary>
    public System.Action<int> OnPlayerCountChanged;

    /// <summary>
    /// Kliens csatlakozás sikertelen volt N másodpercen belül. UI-nak hibaüzenet jelzéshez.
    /// Tipikus ok: a szerver port (9000–9029) nincs forwardolva a routeren.
    /// </summary>
    public System.Action<string> OnConnectionFailed;

    /// <summary>Hány másodpercig várunk a sikeres TCP/WS kapcsolatra StartClient után.</summary>
    public float connectionTimeoutSeconds = 10f;

    /// <summary>
    /// Hány próbálkozással csatlakozzon a kliens, mielőtt feladja és OnConnectionFailed-et tüzel.
    /// Indok: a dedikált Unity szerver időnként scene load közben rövid időre nem fogad
    /// új kapcsolatot (SceneManager.LoadScene sync módban). Ilyenkor egy 2s-os retry
    /// nagy eséllyel sikerül.
    /// </summary>
    public int maxConnectionAttempts = 3;

    /// <summary>Két próbálkozás között eltelt idő (s).</summary>
    public float connectionRetryDelaySeconds = 2f;

    private Coroutine _connectionWatchdog;
    private int _connectionAttempts = 0;
    private string _lastConnectHost;
    private ushort _lastConnectPort;

    // ── Room owner (dedikált szerveres mód) ─────────────────────────

    /// <summary>
    /// A room owner clientId-ja – aki a meccset létrehozta, ő látja a START gombot.
    /// A szerver tartja nyilván, broadcastolja klienseknek.
    /// ulong.MaxValue = még nincs owner beállítva.
    /// </summary>
    private ulong _roomOwnerId = ulong.MaxValue;

    /// <summary>Visszaadja a room owner clientId-ját.</summary>
    public ulong RoomOwnerId => _roomOwnerId;

    /// <summary>True, ha a helyi kliens a room owner.</summary>
    public bool IsRoomOwner =>
        NetworkManager.Singleton != null &&
        NetworkManager.Singleton.LocalClientId == _roomOwnerId;

    /// <summary>Room owner változásakor tüzel – PvPLobbyUI frissítéséhez.</summary>
    public System.Action<ulong> OnRoomOwnerChanged;

    // ── Játék állapot ────────────────────────────────────────────────

    private bool _castleFallen    = false;
    private bool _resultShown     = false;
    private bool _gameEnded       = false;
    private bool _matchHasStarted = false;  // TriggerGameStart után true; késői csatlakozónak MSG_START_GAME kell

    /// <summary>Jelenleg élő (kastélyukat el nem veszítő) játékosok – csak a szerveren releváns.</summary>
    private HashSet<ulong> _alivePlayers = new HashSet<ulong>();

    // ── Üzenetazonosítók ────────────────────────────────────────────

    const string MSG_START_GAME              = "NGM_Start";
    const string MSG_CASTLE_FALLEN           = "NGM_Castle";
    const string MSG_GAME_RESULT             = "NGM_Result";
    const string MSG_SEND_ENEMY              = "NGM_Enemy";
    const string MSG_RELAY_ENEMY             = "NGM_Relay";
    const string MSG_PLAYER_NAME             = "NGM_Name";
    const string MSG_SENDER_GOLD             = "NGM_SdrGold";
    const string MSG_RELAY_SENDER_GOLD       = "NGM_RelaySdrG";
    const string MSG_WAVE_END_GOLD           = "NGM_WaveGold";
    const string MSG_RELAY_WAVE_END_GOLD     = "NGM_RelayWaveG";
    const string MSG_SENT_ENEMY_DELTA        = "NGM_SndDelta";
    const string MSG_RELAY_SENT_ENEMY_DELTA  = "NGM_RelaySndD";
    // ── Dedikált szerveres üzenetek ─────────────────────────────────
    const string MSG_TOWER_PLACED            = "NGM_TowerPlaced";   // kliens → szerver → kliensek: torony lerakás
    const string MSG_CASTLE_HP               = "NGM_CastleHp";     // kliens → szerver → kliensek: kastély HP
    const string MSG_PLAYER_ELIMINATED       = "NGM_Eliminated";   // szerver → kliensek: játékos kiesett
    const string MSG_REQUEST_START           = "NGM_ReqStart";   // room owner → szerver: indítás kérés
    const string MSG_ROOM_OWNER              = "NGM_RoomOwner";  // szerver → kliensek: room owner clientId
    const string MSG_PLAYER_COUNT           = "NGM_PCount";     // szerver → kliensek: jelenlegi játékosszám
    const string MSG_PLAYER_ID              = "NGM_PlayerId";   // kliens → szerver: Google player_id

    /// <summary>Kliens-oldali cache: utolsó szervertől kapott játékosszám (szerver self nélkül).</summary>
    private int _lastKnownPlayerCount = 0;

    // ── Google player_id tracking (szerveren) ────────────────────────
    /// <summary>clientId → Google player_id mapping. Csak a dedikált szerveren van feltöltve.</summary>
    private Dictionary<ulong, string> _googlePlayerIds = new Dictionary<ulong, string>();
    /// <summary>Kiesési sorrend: első elem = legelső kiesett (legrosszabb helyezés).</summary>
    private List<ulong> _eliminationOrder = new List<ulong>();
    /// <summary>Meccs indulásakor élő játékosok száma (Elo számításhoz).</summary>
    private int _initialPlayerCount = 0;
    /// <summary>Meccs kezdetének időpontja (játékidő méréshez, szerveren).</summary>
    private float _matchStartTime = 0f;
    /// <summary>clientId → (enemyName → küldési szám). Szerveren töltődik fel HandleRelayEnemy-ben.</summary>
    private Dictionary<ulong, Dictionary<string, int>> _sentEnemyCounts
        = new Dictionary<ulong, Dictionary<string, int>>();

    // ── Lifecycle ────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (IsPvPMode && NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening)
            StartCoroutine(ReRegisterHandlersNextFrame());
    }

    IEnumerator ReRegisterHandlersNextFrame()
    {
        yield return null;
        RegisterMessageHandlers();
        Debug.Log("[NGM] Üzenetkezelők újra regisztrálva scene betöltés után.");
    }

    // ── Kapcsolat indítás ────────────────────────────────────────────

    /// <summary>
    /// Legacy host mód (LAN / közvetlen IP).
    /// Éles szerveres módban ezt NEM hívja a kliens UI –
    /// a DedicatedServerBootstrap hívja a szerveren.
    /// </summary>
    public void StartHost()
    {
        ResetRoundState();
        _playerNames.Clear();
        _googlePlayerIds.Clear();
        IsPvPMode    = true;
        _roomOwnerId = ulong.MaxValue;

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.UseWebSockets      = true;   // WebSocket → WebGL + desktop egységes
        transport.DisconnectTimeoutMS = 5000;
        transport.SetConnectionData("0.0.0.0", port);

        NetworkManager.Singleton.OnClientConnectedCallback  += OnClientConnected_HostOnly;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected_HostOnly;
        NetworkManager.Singleton.StartHost();
        RegisterMessageHandlers();
    }

    /// <summary>
    /// Dedikált szerveres indítás – csak DedicatedServerBootstrap hívja.
    /// A szerveren fut, portot parancssori argumentumból kapja.
    /// </summary>
    public void StartHostAsServer(ushort serverPort)
    {
        ResetRoundState();
        _playerNames.Clear();
        _googlePlayerIds.Clear();
        IsPvPMode    = true;
        _roomOwnerId = ulong.MaxValue;

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.UseWebSockets       = true;
        transport.UseEncryption       = false;  // Caddy WSS proxy terminálja a TLS-t, plain WS jön be
        // Szerver oldal: 30s elég mobil-tűrésre, viszont gyorsabban kiderül, ha
        // egy kliens "elfelejtette" lebontani a kapcsolatot (pl. crash, app
        // killer). 60s alatt 8 fős meccs vége túl sokáig húzódna.
        transport.DisconnectTimeoutMS = 30000;
        transport.SetConnectionData("0.0.0.0", serverPort);

        NetworkManager.Singleton.OnClientConnectedCallback  += OnClientConnected_HostOnly;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected_HostOnly;
        NetworkManager.Singleton.StartHost();
        RegisterMessageHandlers();

        Debug.Log($"[NGM] Dedikált szerver indult: port={serverPort}");
    }

    /// <summary>
    /// Kliens csatlakozás a szerverhez.
    /// hostIP: szerver publikus domain/IP (pl. "kakaoo123.asuscomm.com")
    /// serverPort: matchmaking API által visszaadott port (0 = Inspector-ban beállított port)
    ///
    /// Hibakezelés: ha a kapcsolat nem épül ki connectionTimeoutSeconds alatt,
    /// még maxConnectionAttempts-szer újraépítjük (connectionRetryDelaySeconds szünettel),
    /// mielőtt OnConnectionFailed-et tüzelünk. Ez kezeli azt az esetet, amikor a Unity
    /// szerver épp scene load közben van és pár másodpercig nem fogad új kapcsolatot.
    /// </summary>
    public void StartClient(string hostIP, ushort serverPort = 0)
    {
        _connectionAttempts = 0;
        _lastConnectHost    = hostIP;
        _lastConnectPort    = serverPort;
        AttemptStartClient();
    }

    void AttemptStartClient()
    {
        _connectionAttempts++;
        string hostIP     = _lastConnectHost;
        ushort serverPort = _lastConnectPort;

        ResetRoundState();
        _playerNames.Clear();
        _googlePlayerIds.Clear();
        IsPvPMode = true;

        ushort connectPort = serverPort > 0 ? serverPort : port;

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.UseWebSockets       = true;   // WebSocket → WebGL + desktop egységes
        transport.DisconnectTimeoutMS = 60000;  // mobil hálón – CGNAT/packet loss tűrés

        // Mobil-barát mód: ha a port 443 (Caddy WSS proxy), TLS-sel csatlakozunk.
        // Egyébként (LAN, közvetlen game port) plain WebSocket.
        bool useTls = connectPort == 443;
        transport.UseEncryption = useTls;

        // TLS (port 443, Caddy WSS proxy):
        //   - Android/native: kézzel feloldjuk DNS-en (Dns.GetHostAddresses), IP-t adunk át.
        //   - WebGL: System.Net.Dns nem elérhető böngésző sandboxban → a hostnevet közvetlenül
        //     adjuk át, a böngésző JS WebSocket API kezeli a DNS feloldást saját maga.
        // LAN kliensnek a matchmaking szerver külön TCP connect IP-t küld (server_connect_ip)
        // a Caddy LAN IP-jére, hogy NAT loopback nélkül elérje a Caddy 443-as portját –
        // a TLS SNI viszont továbbra is a pNNNN.kakaoo123.duckdns.org hosztnév, így a
        // SNI-route megtalálja a meccs Unity portját. Mindkét esetben SetClientSecrets(hostname)
        // adja meg a TLS SNI-t és cert CN-elvárást.
        string connectionAddress = hostIP;
        if (useTls)
        {
            transport.SetClientSecrets(hostIP, null);

            string overrideIp = MatchmakingClient.Instance != null
                ? MatchmakingClient.Instance.LastConnectOverrideIp
                : null;

            if (!string.IsNullOrEmpty(overrideIp))
            {
                // LAN út: a matchmaking szerver az általunk elérhető Caddy LAN IP-t küldte.
                connectionAddress = overrideIp;
                Debug.Log($"[NGM] LAN TLS: connect={overrideIp}:{connectPort} (SNI={hostIP})");
            }
            else
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                // WebGL: System.Net.Dns.GetHostAddresses() nem elérhető – hostname direkt átadása.
                // A böngésző WebSocket implementációja elvégzi a DNS feloldást.
                connectionAddress = hostIP;
                Debug.Log($"[NGM] WebGL TLS: hostname direkt átadva: {hostIP}:{connectPort}");
#else
                // Android / Desktop publikus út: kézi DNS feloldás, IP átadása Unity Transportnak.
                string resolvedIp = ResolveHostToIPv4(hostIP);
                if (resolvedIp == null)
                {
                    Debug.LogError($"[NGM] DNS feloldási hiba: {hostIP} – nem található IPv4 cím.");
                    OnConnectionFailed?.Invoke($"Nem sikerült feloldani a szerver címét: {hostIP}");
                    return;
                }
                connectionAddress = resolvedIp;
                Debug.Log($"[NGM] DNS: {hostIP} → {resolvedIp} (SNI={hostIP})");
#endif
            }
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL-en a UnityTransport.ClientBindAndConnect csak IPv4/IPv6-ot fogad
        // el (NetworkEndpoint.TryParse-on átment) – hosztnévre "Invalid network
        // endpoint" hibával bukik, ami után a Netcode állapotgép sérül és wasm
        // memory access OOB crash jön a következő frame-en.
        //
        // Workaround: a WebGLHostnameTransport (custom UnityTransport subclass)
        // a Connect()-et override-olja, és reflectionnel a privát m_Driver-en
        // hívja meg a hosztnév-támogató NetworkDriver.Connect(FixedString512Bytes, ushort)
        // overloadot. A validációt átejtjük egy dummy "127.0.0.1" IP-vel a
        // ConnectionData.Address mezőben – ami csak addig kell, amíg a Family
        // check átmegy; a tényleges WebSocket URL-t a hosztnévből építi a
        // baselib NetworkInterface.
        if (transport is WebGLHostnameTransport)
        {
            WebGLHostnameTransport.PendingHostname = connectionAddress;
            WebGLHostnameTransport.PendingPort     = connectPort;
            transport.SetConnectionData("127.0.0.1", connectPort);
            Debug.Log($"[NGM] WebGL custom transport: hosztnév={connectionAddress}, port={connectPort}");
        }
        else
        {
            Debug.LogError(
                "[NGM] WebGL build, de a NetworkManager Transport komponense nem " +
                "WebGLHostnameTransport! A stock UnityTransport hosztnevet nem fogad. " +
                "Cseréld le a NetworkManager Inspectorban a Transport komponenst " +
                "WebGLHostnameTransport-ra (Assets/Scripts/Network/WebGLHostnameTransport.cs).");
            transport.SetConnectionData(connectionAddress, connectPort);   // így biztos hibázni fog, de lássuk
        }
#else
        transport.SetConnectionData(connectionAddress, connectPort);
#endif

        NetworkManager.Singleton.OnClientDisconnectCallback += OnServerDisconnected_ClientOnly;
        NetworkManager.Singleton.StartClient();
        RegisterMessageHandlers();

        Debug.Log($"[NGM] Kliens csatlakozik: {hostIP}:{connectPort}");

        // Watchdog: ha a kapcsolat connectionTimeoutSeconds-en belül nem épül ki,
        // hibát jelzünk a UI-nak (pl. nincs port forward).
        if (_connectionWatchdog != null) StopCoroutine(_connectionWatchdog);
        _connectionWatchdog = StartCoroutine(ConnectionWatchdog(hostIP, connectPort));
    }

    IEnumerator ConnectionWatchdog(string host, ushort port)
    {
        float waited = 0f;
        while (waited < connectionTimeoutSeconds)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            {
                _connectionWatchdog = null;
                yield break;
            }
            if (NetworkManager.Singleton.IsConnectedClient)
            {
                Debug.Log($"[NGM] Kapcsolat kiépült: {host}:{port} (próba {_connectionAttempts}/{maxConnectionAttempts})");
                _connectionWatchdog = null;
                yield break;
            }
            yield return new WaitForSeconds(0.5f);
            waited += 0.5f;
        }

        // Időtúllépés. Ha még van retry, indítsuk újra a csatlakozást.
        if (_connectionAttempts < maxConnectionAttempts)
        {
            Debug.LogWarning(
                $"[NGM] Kapcsolat időtúllépés ({connectionTimeoutSeconds}s, próba " +
                $"{_connectionAttempts}/{maxConnectionAttempts}) – {connectionRetryDelaySeconds}s múlva újra: {host}:{port}"
            );
            try { NetworkManager.Singleton?.Shutdown(); } catch { }
            _connectionWatchdog = null;
            yield return new WaitForSeconds(connectionRetryDelaySeconds);
            AttemptStartClient();
            yield break;
        }

        // Minden próbálkozás kimerült → végleges hiba
        Debug.LogError(
            $"[NGM] Kapcsolat sikertelen {maxConnectionAttempts} próbálkozás után: {host}:{port}. " +
            "Tipp: ellenőrizd hogy a 9000–9029 TCP portok forwardolva vannak-e a routeren."
        );
        OnConnectionFailed?.Invoke(
            $"Nem sikerült csatlakozni a játékszerverhez ({host}:{port}).\n" +
            "Mobilnetről játszani csak akkor lehet, ha a router a játékszerver portjait továbbítja."
        );
        try { NetworkManager.Singleton?.Shutdown(); } catch { }
        _connectionWatchdog = null;
    }

    /// <summary>Kapcsolat bontása és visszatérés a főmenübe.</summary>
    public void Disconnect()
    {
        IsPvPMode    = false;
        _roomOwnerId = ulong.MaxValue;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.OnClientConnectedCallback  -= OnClientConnected_HostOnly;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected_HostOnly;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnServerDisconnected_ClientOnly;
            NetworkManager.Singleton.Shutdown();
        }
        SceneManager.LoadScene(mainMenuSceneName);
    }

    // ── Üzenetkezelők regisztrációja ─────────────────────────────────

    void RegisterMessageHandlers()
    {
        var msg = NetworkManager.Singleton.CustomMessagingManager;

        msg.UnregisterNamedMessageHandler(MSG_START_GAME);
        msg.UnregisterNamedMessageHandler(MSG_CASTLE_FALLEN);
        msg.UnregisterNamedMessageHandler(MSG_GAME_RESULT);
        msg.UnregisterNamedMessageHandler(MSG_SEND_ENEMY);
        msg.UnregisterNamedMessageHandler(MSG_RELAY_ENEMY);
        msg.UnregisterNamedMessageHandler(MSG_PLAYER_NAME);
        msg.UnregisterNamedMessageHandler(MSG_SENDER_GOLD);
        msg.UnregisterNamedMessageHandler(MSG_RELAY_SENDER_GOLD);
        msg.UnregisterNamedMessageHandler(MSG_WAVE_END_GOLD);
        msg.UnregisterNamedMessageHandler(MSG_RELAY_WAVE_END_GOLD);
        msg.UnregisterNamedMessageHandler(MSG_SENT_ENEMY_DELTA);
        msg.UnregisterNamedMessageHandler(MSG_RELAY_SENT_ENEMY_DELTA);
        msg.UnregisterNamedMessageHandler(MSG_TOWER_PLACED);
        msg.UnregisterNamedMessageHandler(MSG_CASTLE_HP);
        msg.UnregisterNamedMessageHandler(MSG_PLAYER_ELIMINATED);
        msg.UnregisterNamedMessageHandler(MSG_REQUEST_START);
        msg.UnregisterNamedMessageHandler(MSG_ROOM_OWNER);
        msg.UnregisterNamedMessageHandler(MSG_PLAYER_COUNT);
        msg.UnregisterNamedMessageHandler(MSG_PLAYER_ID);

        msg.RegisterNamedMessageHandler(MSG_START_GAME,             (_, reader)         => HandleStartGame(reader));
        msg.RegisterNamedMessageHandler(MSG_CASTLE_FALLEN,          (senderId, _)       => HandleCastleFallen_Server(senderId));
        msg.RegisterNamedMessageHandler(MSG_GAME_RESULT,            (_, reader)         => HandleGameResult(reader));
        msg.RegisterNamedMessageHandler(MSG_SEND_ENEMY,             (_, reader)         => HandleIncomingEnemy(reader));
        msg.RegisterNamedMessageHandler(MSG_RELAY_ENEMY,            (senderId, reader)  => HandleRelayEnemy(senderId, reader));
        msg.RegisterNamedMessageHandler(MSG_PLAYER_NAME,            (senderId, reader)  => HandlePlayerName(senderId, reader));
        msg.RegisterNamedMessageHandler(MSG_SENDER_GOLD,            (_, reader)         => HandleSenderGold(reader));
        msg.RegisterNamedMessageHandler(MSG_RELAY_SENDER_GOLD,      (_, reader)         => HandleRelaySenderGold(reader));
        msg.RegisterNamedMessageHandler(MSG_WAVE_END_GOLD,          (_, reader)         => HandleWaveEndGold(reader));
        msg.RegisterNamedMessageHandler(MSG_RELAY_WAVE_END_GOLD,    (_, reader)         => HandleRelayWaveEndGold(reader));
        msg.RegisterNamedMessageHandler(MSG_SENT_ENEMY_DELTA,       (_, reader)         => HandleSentEnemyDelta(reader));
        msg.RegisterNamedMessageHandler(MSG_RELAY_SENT_ENEMY_DELTA, (senderId, reader)  => HandleRelaySentEnemyDelta(senderId, reader));
        msg.RegisterNamedMessageHandler(MSG_TOWER_PLACED,           (senderId, reader)  => HandleTowerPlaced(senderId, reader));
        msg.RegisterNamedMessageHandler(MSG_CASTLE_HP,              (senderId, reader)  => HandleCastleHp(senderId, reader));
        msg.RegisterNamedMessageHandler(MSG_PLAYER_ELIMINATED,      (_, reader)         => HandlePlayerEliminated(reader));
        msg.RegisterNamedMessageHandler(MSG_REQUEST_START,          (senderId, _)       => HandleRequestStart(senderId));
        msg.RegisterNamedMessageHandler(MSG_ROOM_OWNER,             (_, reader)         => HandleRoomOwner(reader));
        msg.RegisterNamedMessageHandler(MSG_PLAYER_COUNT,           (_, reader)         => HandlePlayerCount(reader));
        msg.RegisterNamedMessageHandler(MSG_PLAYER_ID,              (senderId, reader)  => HandlePlayerId(senderId, reader));
    }

    // ── Host: játékos számláló + room owner kezelés ──────────────────

    void OnClientConnected_HostOnly(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsHost) return;

        int count = GetRealPlayerCount_Server();

        // Az első csatlakozó kliens jelzi az új lobby-session kezdetét – régi meccs adatait töröljük
        if (count == 1)
        {
            _playerNames.Clear();
            _googlePlayerIds.Clear();
            _matchHasStarted = false;
            Debug.Log("[NGM] Új lobby-session – playerNames/googlePlayerIds törölve");
        }

        OnPlayerCountChanged?.Invoke(count);
        BroadcastPlayerCount(count);
        Debug.Log($"[NGM] Csatlakozott: {clientId} | Összes játékos: {count}");

        // Host elküldi a saját nevét az új kliensnek
        SendPlayerName(clientId);

        // A már ismert játékosok neveit is elküldi az új kliensnek,
        // hogy az IsKnownPlayer szűrő minden ellenfelet megtaláljon.
        foreach (var kv in _playerNames)
        {
            if (kv.Key == clientId || kv.Key == NetworkManager.Singleton.LocalClientId) continue;
            using var fwd = new FastBufferWriter(FixedString64Bytes.UTF8MaxLengthInBytes + 2 + 8, Allocator.Temp);
            fwd.WriteValueSafe(new FixedString64Bytes(kv.Value));
            fwd.WriteValueSafe(kv.Key);
            NetworkManager.Singleton.CustomMessagingManager
                .SendNamedMessage(MSG_PLAYER_NAME, clientId, fwd);
        }

        // Az első valódi kliens (nem a szerver saját id-ja) lesz a room owner
        if (_roomOwnerId == ulong.MaxValue && clientId != NetworkManager.Singleton.LocalClientId)
        {
            _roomOwnerId = clientId;
            BroadcastRoomOwner();
            Debug.Log($"[NGM] Room owner beállítva: {clientId}");
        }
        else if (_roomOwnerId != ulong.MaxValue)
        {
            // Már van room owner – az új kliensnek is elküldjük
            SendRoomOwnerTo(clientId);
        }

        // Késői csatlakozás (pl. lassú WebGL): a játék már elindult, de ez a kliens lemaradt
        // a MSG_START_GAME-ről → most pótoljuk.
        if (_matchHasStarted && !_gameEnded && clientId != NetworkManager.Singleton.LocalClientId)
        {
            _alivePlayers.Add(clientId);
            using var startWriter = new FastBufferWriter(4, Allocator.Temp);
            startWriter.WriteValueSafe(SharedMapSeed);
            NetworkManager.Singleton.CustomMessagingManager
                .SendNamedMessage(MSG_START_GAME, clientId, startWriter);
            Debug.Log($"[NGM] Késői csatlakozás – MSG_START_GAME újraküldve: clientId={clientId}, seed={SharedMapSeed}");
        }
    }

    void OnClientDisconnected_HostOnly(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsHost) return;

        int count = GetRealPlayerCount_Server();
        // A kilépő kliens még benne lehet az NGO listában – kivonjuk.
        if (NetworkManager.Singleton.ConnectedClientsIds.Contains(clientId)) count--;
        if (count < 0) count = 0;
        OnPlayerCountChanged?.Invoke(count);
        BroadcastPlayerCount(count);
        Debug.Log($"[NGM] Lecsatlakozott: {clientId} | Összes játékos: {count}");

        // Játék közben kiesett → kastély elesett
        if (_alivePlayers.Contains(clientId))
        {
            Debug.Log($"[NGM] Játék közben lépett ki: {clientId} → kastély elesettnek számít");
            HandleCastleFallen_Server(clientId);
        }

        // Ha a room owner lépett ki (lobby vagy játék előtt) → új owner
        if (clientId == _roomOwnerId)
        {
            AssignNewRoomOwner(excludeId: clientId);
        }
    }

    /// <summary>
    /// Kliens oldalon tüzel, ha a szerverrel megszakad a kapcsolat.
    /// Dedikált szerveres módban: hibaüzenet, NEM automatikus győzelem (mert a
    /// másik játékos kapcsolata is élhet még – a szerver a kastély elesésekor
    /// dönt). Legacy host módban: a host kiesett → győzelem.
    /// </summary>
    void OnServerDisconnected_ClientOnly(ulong clientId)
    {
        if (NetworkManager.Singleton.IsHost) return;
        if (!IsPvPMode || _resultShown) return;
        if (GameManager.Instance == null) return;

        Debug.LogWarning("[NGM] Kapcsolat a szerverrel megszakadt játék közben → hibaüzenet.");
        OnConnectionFailed?.Invoke(
            "A kapcsolat megszakadt a szerverrel.\n" +
            "Mobilneten gyakran előfordul – próbáld újra, és ha lehet, WiFi-t használj."
        );
        // Visszadobjuk a klienst főmenübe – nem győzelemmel, hanem hiba state-ben.
        try { NetworkManager.Singleton?.Shutdown(); } catch { }
        SceneManager.LoadScene(mainMenuSceneName);
    }

    /// <summary>Csatlakozott játékosok száma (host + kliensek).</summary>
    public int GetConnectedPlayerCount()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) return 0;
        // Szerveren a valódi listából, kliensen a szervertől cache-elt értékből.
        if (NetworkManager.Singleton.IsServer) return GetRealPlayerCount_Server();
        return _lastKnownPlayerCount;
    }

    /// <summary>Szerver-oldali számláló: ConnectedClients - szerver self id.</summary>
    int GetRealPlayerCount_Server()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return 0;
        int count = nm.ConnectedClientsIds.Count;
        // Dedikált szerveres módban a szerver self id-ja is benne van – levonjuk,
        // hogy csak a valódi játékosokat számoljuk.
        if (nm.ConnectedClientsIds.Contains(nm.LocalClientId)) count--;
        return count < 0 ? 0 : count;
    }

    /// <summary>Szerver → minden kliens: aktuális játékosszám broadcast.</summary>
    void BroadcastPlayerCount(int count)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer || nm.CustomMessagingManager == null) return;
        using var writer = new FastBufferWriter(4, Allocator.Temp);
        writer.WriteValueSafe(count);
        nm.CustomMessagingManager.SendNamedMessageToAll(MSG_PLAYER_COUNT, writer);
    }

    /// <summary>Kliens: szervertől kapott játékosszám alkalmazása + UI esemény.</summary>
    void HandlePlayerCount(FastBufferReader reader)
    {
        reader.ReadValueSafe(out int count);
        _lastKnownPlayerCount = count;
        OnPlayerCountChanged?.Invoke(count);
    }

    // ── Room owner kezelés ───────────────────────────────────────────

    /// <summary>Szerveren: elküldi a room owner id-t minden csatlakozott kliensnek.</summary>
    void BroadcastRoomOwner()
    {
        using var writer = new FastBufferWriter(8, Allocator.Temp);
        writer.WriteValueSafe(_roomOwnerId);
        NetworkManager.Singleton.CustomMessagingManager
            .SendNamedMessageToAll(MSG_ROOM_OWNER, writer);
    }

    /// <summary>Szerveren: elküldi a room owner id-t egyetlen kliensnek (pl. újonnan csatlakozó).</summary>
    void SendRoomOwnerTo(ulong targetClientId)
    {
        using var writer = new FastBufferWriter(8, Allocator.Temp);
        writer.WriteValueSafe(_roomOwnerId);
        NetworkManager.Singleton.CustomMessagingManager
            .SendNamedMessage(MSG_ROOM_OWNER, targetClientId, writer);
    }

    /// <summary>Szerveren: új room ownert rendel, ha a régi kiesett.</summary>
    void AssignNewRoomOwner(ulong excludeId)
    {
        _roomOwnerId = ulong.MaxValue;

        foreach (ulong id in NetworkManager.Singleton.ConnectedClientsIds)
        {
            if (id == NetworkManager.Singleton.LocalClientId) continue; // kihagyja a szerver id-t
            if (id == excludeId) continue;
            _roomOwnerId = id;
            break;
        }

        if (_roomOwnerId != ulong.MaxValue)
        {
            Debug.Log($"[NGM] Új room owner: {_roomOwnerId}");
            BroadcastRoomOwner();
        }
        else
        {
            Debug.Log("[NGM] Nincs több kliens – room owner üres.");
        }
    }

    /// <summary>Kliensen: fogadja a room owner frissítést a szervertől.</summary>
    void HandleRoomOwner(FastBufferReader reader)
    {
        reader.ReadValueSafe(out ulong ownerId);
        _roomOwnerId = ownerId;
        bool isMine = NetworkManager.Singleton.LocalClientId == ownerId;
        Debug.Log($"[NGM] Room owner fogadva: {ownerId} | Ez vagyok én: {isMine}");
        OnRoomOwnerChanged?.Invoke(ownerId);
    }

    // ── Játék indítás ────────────────────────────────────────────────

    /// <summary>
    /// Kliens hívja a START gombra – dedikált szerveres módban kérést küld a szervernek.
    /// Legacy módban (host=kliens) helyben hívja TriggerGameStart()-ot.
    /// PvPLobbyUI.OnStartPressed() ezt hívja.
    /// </summary>
    public void RequestGameStart()
    {
        if (NetworkManager.Singleton.IsHost)
        {
            // Legacy mód: a kliens egyben host is → helyi indítás
            TriggerGameStart();
            return;
        }

        // Dedikált szerveres mód: kérés a szervernek
        using var writer = new FastBufferWriter(0, Allocator.Temp);
        NetworkManager.Singleton.CustomMessagingManager
            .SendNamedMessage(MSG_REQUEST_START, NetworkManager.ServerClientId, writer);
        Debug.Log("[NGM] RequestGameStart → szerverre küldve.");
    }

    /// <summary>Szerveren fogadja a room owner indítási kérését.</summary>
    void HandleRequestStart(ulong senderId)
    {
        if (!NetworkManager.Singleton.IsHost) return;

        if (senderId != _roomOwnerId)
        {
            Debug.LogWarning($"[NGM] START kérés nem room ownertől! ({senderId} != {_roomOwnerId}) → elutasítva.");
            return;
        }

        Debug.Log($"[NGM] Room owner ({senderId}) indítja a játékot.");
        TriggerGameStart();
    }

    /// <summary>
    /// Szerver hívja (közvetlen legacy módban a host, dedikált módban HandleRequestStart).
    /// Elküldi az indítási jelet minden kliensnek, majd saját maga is betölti a jelenetet.
    /// </summary>
    public void TriggerGameStart()
    {
        if (!NetworkManager.Singleton.IsHost) return;

        _matchHasStarted = true;
        _alivePlayers.Clear();
        _eliminationOrder.Clear();
        _sentEnemyCounts.Clear();
        ulong serverSelfId = NetworkManager.Singleton.LocalClientId;
        foreach (ulong id in NetworkManager.Singleton.ConnectedClientsIds)
        {
            // A dedikált szerver saját self-id-jét NEM játékosként kezeljük –
            // a szerveren is fut Castle.cs, de az nem egy "játékos kastélya".
            if (id == serverSelfId) continue;
            _alivePlayers.Add(id);
        }
        _initialPlayerCount = _alivePlayers.Count;

        Debug.Log($"[NGM] Játék indul! Élő játékosok: {_alivePlayers.Count}");

        SharedMapSeed = Random.Range(0, int.MaxValue);

        using var writer = new FastBufferWriter(4, Allocator.Temp);
        writer.WriteValueSafe(SharedMapSeed);
        NetworkManager.Singleton.CustomMessagingManager
            .SendNamedMessageToAll(MSG_START_GAME, writer);

        OpponentDataTracker.Instance?.Reset();
        LoadGameScene();
        // _matchStartTime beállítása UTÁN a ResetRoundState()-et hívó LoadGameScene(),
        // különben a reset visszaírja 0-ra és play_seconds mindig 0 lesz.
        _matchStartTime = Time.realtimeSinceStartup;
    }

    void HandleStartGame(FastBufferReader reader)
    {
        reader.ReadValueSafe(out int seed);
        SharedMapSeed = seed;
        OpponentDataTracker.Instance?.Reset();
        Debug.Log($"[NGM] Map seed fogadva: {seed}");
        LoadGameScene();
    }

    void LoadGameScene()
    {
        ResetRoundState();
        SceneManager.LoadScene(pvpGameSceneName);
    }

    // ── Kastély megsemmisülés ────────────────────────────────────────

    /// <summary>Castle.cs hívja PvP módban, ha a helyi kastély megsemmisül.</summary>
    public void OnLocalCastleDestroyed()
    {
        if (!IsPvPMode || _castleFallen) return;
        _castleFallen = true;

        if (NetworkManager.Singleton.IsHost)
        {
            HandleCastleFallen_Server(NetworkManager.Singleton.LocalClientId);
        }
        else
        {
            using var writer = new FastBufferWriter(0, Allocator.Temp);
            NetworkManager.Singleton.CustomMessagingManager
                .SendNamedMessage(MSG_CASTLE_FALLEN, NetworkManager.ServerClientId, writer);
        }
    }

    void HandleCastleFallen_Server(ulong loserId)
    {
        if (!NetworkManager.Singleton.IsHost) return;
        if (_gameEnded)
        {
            // Idekésve érkezett castle-fallen / disconnect üzenet – csak takarítunk.
            if (_alivePlayers.Remove(loserId))
                Debug.Log($"[NGM] Késői castle-fallen ({loserId}) – meccs már véget ért, kitakarítva.");
            return;
        }

        // A dedikált szerver saját kastélya nem játékos – ignoráljuk.
        if (loserId == NetworkManager.Singleton.LocalClientId)
        {
            Debug.Log($"[NGM] Szerver-self kastély elesett – figyelmen kívül hagyva (nem játékos).");
            return;
        }

        int playersBeforeRemoval = _alivePlayers.Count;
        Debug.Log($"[NGM] Kastély elesett: {loserId} | Élők előtte: {playersBeforeRemoval} | alive=[{string.Join(",", _alivePlayers)}]");

        // Csak akkor küldünk "vesztettél!" üzenetet, ha tényleg élő játékos volt.
        // (Lobby-ban kilépő, vagy duplikált disconnect race-ek elkerülése.)
        bool wasAlive = _alivePlayers.Contains(loserId);
        if (wasAlive)
        {
            SendResultToClient(loserId, won: false, playersAlive: playersBeforeRemoval);
            _eliminationOrder.Add(loserId);
        }
        _alivePlayers.Remove(loserId);

        // Értesítjük a maradék klienseket, hogy távolítsák el a kiesett játékost a trackerből
        if (wasAlive)
        {
            int alivePlayers = _alivePlayers.Count;
            BroadcastPlayerEliminated(loserId, alivePlayers);
            // Host saját UI-ján is megjelenítjük, ugyanazokkal a feltételekkel mint a klienseken
            if (alivePlayers > 1)
            {
                string loserName = GetPlayerName(loserId);
                UIManager.Instance?.ShowEliminationNotice(loserName);
            }
        }

        // Biztonsági szinkronizálás: ha bármilyen okból az _alivePlayers olyan
        // ID-kat tartalmaz, akik már nincsenek a ConnectedClients-ben (pl. egy
        // disconnect callback kimaradt valami race miatt), itt korrigáljuk.
        // Enélkül 8 fős mobil meccsen örökké "alive" maradhat egy szellem-ID,
        // és a `Count <= 1` feltétel sosem teljesül.
        SyncAlivePlayersWithConnected();

        Debug.Log($"[NGM] Élők száma a vesztes után: {_alivePlayers.Count} | alive=[{string.Join(",", _alivePlayers)}]");

        if (_alivePlayers.Count == 1)
        {
            _gameEnded = true;
            foreach (ulong winnerId in _alivePlayers)
            {
                Debug.Log($"[NGM] Győztes: {winnerId}");
                SendResultToClient(winnerId, won: true, playersAlive: 1);
            }
            NotifyServerGameEnded();
        }
        else if (_alivePlayers.Count == 0)
        {
            _gameEnded = true;
            Debug.Log("[NGM] Mindenki kiesett / lecsatlakozott – meccs vége (nincs győztes).");
            NotifyServerGameEnded();
        }
    }

    /// <summary>
    /// Eltávolítja az `_alivePlayers`-ből azokat az ID-kat, akik már nincsenek
    /// a NetworkManager.ConnectedClients listájában. 8 fős vegyes hálón
    /// (WiFi+mobil) előfordulhat hogy a disconnect callback és a castle-fallen
    /// üzenet sorrendje vagy duplikációja miatt egy ID "ragad" – ez a háló.
    /// </summary>
    void SyncAlivePlayersWithConnected()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost) return;

        var connected = NetworkManager.Singleton.ConnectedClientsIds;
        // Másolaton iteráljunk, mert módosítjuk a HashSet-et.
        foreach (ulong id in new System.Collections.Generic.List<ulong>(_alivePlayers))
        {
            bool stillConnected = false;
            foreach (ulong c in connected)
            {
                if (c == id) { stillConnected = true; break; }
            }
            if (!stillConnected)
            {
                _alivePlayers.Remove(id);
                Debug.LogWarning($"[NGM] Szellem-ID eltávolítva _alivePlayers-ből: {id} (nincs a ConnectedClients-ben).");
            }
        }
    }

    /// <summary>
    /// A szerver felé jelez, hogy a meccs véget ért. Hívja a GameManager.TriggerGameOver-t,
    /// ami a Bootstrap OnGameOver eseményét tüzeli → POST_GAME_SHUTDOWN_DELAY után a process kilép.
    /// Enélkül a Unity szerver process örökké futna a meccs vége után.
    /// </summary>
    void NotifyServerGameEnded()
    {
        // Meccs eredmény bejelentése a Python API-nak (csak a dedikált szerveren)
#if UNITY_SERVER
        StartCoroutine(ReportMatchResultToApi());
#endif
        if (GameManager.Instance != null)
        {
            Debug.Log("[NGM] Szerver: GameManager.TriggerGameOver() – Bootstrap shutdown timer indul.");
            GameManager.Instance.TriggerGameOver();
        }
    }

    /// <summary>
    /// Ellenőrzi, hogy maradt-e egyetlen élő játékos.
    /// WaveManager hívja minden hullám elején – biztonsági háló kilépések esetére.
    /// </summary>
    public void CheckSoloVictory()
    {
        if (!IsPvPMode || _gameEnded) return;
        if (!NetworkManager.Singleton.IsHost) return;
        if (_alivePlayers.Count != 1) return;

        _gameEnded = true;
        foreach (ulong winnerId in _alivePlayers)
        {
            Debug.Log($"[NGM] CheckSoloVictory – egyedüli győztes: {winnerId}");
            SendResultToClient(winnerId, won: true, playersAlive: 1);
        }
        NotifyServerGameEnded();
    }

    void SendResultToClient(ulong clientId, bool won, int playersAlive)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            ApplyResult(won, playersAlive);
        }
        else
        {
            using var writer = new FastBufferWriter(2, Allocator.Temp);
            writer.WriteValueSafe((byte)(won ? 1 : 0));
            writer.WriteValueSafe((byte)Mathf.Clamp(playersAlive, 1, 255));
            NetworkManager.Singleton.CustomMessagingManager
                .SendNamedMessage(MSG_GAME_RESULT, clientId, writer);
        }
    }

    void HandleGameResult(FastBufferReader reader)
    {
        reader.ReadValueSafe(out byte result);
        reader.ReadValueSafe(out byte playersAlive);
        ApplyResult(won: result == 1, playersAlive: playersAlive);
    }

    void ApplyResult(bool won, int playersAlive = 1)
    {
        if (_resultShown) return;
        _resultShown  = true;
        LastResultWon = won;

        XPManager.Instance?.SetEliminationContext(playersAlive);

        if (GameManager.Instance != null)
            GameManager.Instance.TriggerGameOver();

        // ── Server kill request (OS szintű) ───────────────────────────────
        // A Unity dedikált szerver Environment.Exit() néha bent ragad headless
        // módban. Megbízhatóbb, ha a Python matchmaking nyírja ki a process-t
        // (process.terminate() → process.kill()) DELETE /match/{matchId} hívásra.
        //
        // FIGYELEM: csak akkor szabad close-t kérni, ha a meccs TÉNYLEG véget ért!
        // 4 fős meccsben az első kieső (playersAlive=4) után még 3 játékos játszik.
        // Ha ő küldené a DELETE-et, mindenki kapcsolata megszakadna.
        // A vesztes csak akkor küldje, ha playersAlive <= 2 (azaz utána már
        // csak 1 élő marad → győztes deklarálva, meccs vége).
        bool matchActuallyEnded = won || playersAlive <= 2;
        if (!matchActuallyEnded)
        {
            Debug.Log($"[NGM] Kiestem, de a meccs még megy ({playersAlive - 1} élő hátra) – NEM kérek szerver close-t.");
            return;
        }

        // A győztes 10 mp múlva küldi (mutathatja még a "GYŐZTÉL!" UI-t addig).
        // A vesztes 20 mp múlva, redundánsan – ha a győztes crash-elne / app
        // killer megölné a folyamatát, a port akkor is felszabadul. A Python
        // endpoint idempotens: ha már nincs meccs, csendben sikert jelez.
        float delaySeconds = won ? 10f : 20f;
        StartCoroutine(RequestServerCloseAfterDelay(delaySeconds));
    }

    System.Collections.IEnumerator RequestServerCloseAfterDelay(float delaySeconds)
    {
        yield return new WaitForSeconds(delaySeconds);

        if (MatchmakingClient.Instance == null)
        {
            Debug.LogWarning("[NGM] MatchmakingClient.Instance null – nem tudjuk lezárni a meccset HTTP-n.");
            yield break;
        }

        string matchId = MatchmakingClient.Instance.CurrentMatchId;
        if (string.IsNullOrEmpty(matchId))
        {
            Debug.LogWarning("[NGM] CurrentMatchId üres – meccs lezárás kihagyva.");
            yield break;
        }

        Debug.Log($"[NGM] Meccs lezárás kérése (HTTP DELETE) | matchId={matchId} | won={LastResultWon} | delay={delaySeconds}s");
        MatchmakingClient.Instance.CloseMatch();
    }

    // ── PvP szörny küldés ────────────────────────────────────────────

    public void SendEnemyToAllOpponents(int enemyIndex)
    {
        if (NetworkManager.Singleton.IsHost)
        {
            ulong hostId    = NetworkManager.Singleton.LocalClientId;
            string hostName = UserProgressManager.Instance?.CharacterName ?? "Host";
            foreach (ulong id in NetworkManager.Singleton.ConnectedClientsIds)
            {
                if (id == hostId) continue;

                using var writer = new FastBufferWriter(80, Allocator.Temp);
                writer.WriteValueSafe((byte)enemyIndex);
                writer.WriteValueSafe(hostId);
                writer.WriteValueSafe(new FixedString64Bytes(hostName));
                NetworkManager.Singleton.CustomMessagingManager
                    .SendNamedMessage(MSG_SEND_ENEMY, id, writer);
            }
            Debug.Log($"[NGM] Host küldte szörnyet: index={enemyIndex}");
        }
        else
        {
            string eName = PvPSendPanel.Instance?.GetEnemyDefinition(enemyIndex)?.enemyName
                           ?? $"enemy_{enemyIndex}";
            using var writer = new FastBufferWriter(FixedString64Bytes.UTF8MaxLengthInBytes + 4, Allocator.Temp);
            writer.WriteValueSafe((byte)enemyIndex);
            writer.WriteValueSafe(new FixedString64Bytes(eName));
            NetworkManager.Singleton.CustomMessagingManager
                .SendNamedMessage(MSG_RELAY_ENEMY, NetworkManager.ServerClientId, writer);
            Debug.Log($"[NGM] Kliens relay szörnyet: index={enemyIndex} ({eName}) → szerver");
        }
    }

    void HandleRelayEnemy(ulong senderId, FastBufferReader reader)
    {
        if (!NetworkManager.Singleton.IsHost) return;

        reader.ReadValueSafe(out byte enemyIndex);
        // Szörny név kiolvasása (új build küldi; régi build esetén üres marad)
        string enemyName = $"enemy_{enemyIndex}";
        if (reader.Length > reader.Position)
        {
            reader.ReadValueSafe(out FixedString64Bytes eNameFixed);
            string parsed = eNameFixed.ToString();
            if (!string.IsNullOrEmpty(parsed)) enemyName = parsed;
        }

        // Szörny küldési számláló frissítése
#if UNITY_SERVER
        if (!_sentEnemyCounts.ContainsKey(senderId))
            _sentEnemyCounts[senderId] = new Dictionary<string, int>();
        _sentEnemyCounts[senderId].TryGetValue(enemyName, out int prev);
        _sentEnemyCounts[senderId][enemyName] = prev + 1;
#endif

        string senderName = GetPlayerName(senderId);

        HandleIncomingEnemyInternal(enemyIndex, senderId, senderName);

        foreach (ulong id in NetworkManager.Singleton.ConnectedClientsIds)
        {
            if (id == NetworkManager.Singleton.LocalClientId) continue;
            if (id == senderId) continue;

            using var writer = new FastBufferWriter(80, Allocator.Temp);
            writer.WriteValueSafe(enemyIndex);
            writer.WriteValueSafe(senderId);
            writer.WriteValueSafe(new FixedString64Bytes(senderName));
            NetworkManager.Singleton.CustomMessagingManager
                .SendNamedMessage(MSG_SEND_ENEMY, id, writer);
        }
    }

    void HandleIncomingEnemy(FastBufferReader reader)
    {
        reader.ReadValueSafe(out byte enemyIndex);
        reader.ReadValueSafe(out ulong senderId);
        reader.ReadValueSafe(out FixedString64Bytes senderNameFixed);
        HandleIncomingEnemyInternal(enemyIndex, senderId, senderNameFixed.ToString());
    }

    // ── Névcsere ─────────────────────────────────────────────────────

    void SendPlayerName(ulong targetClientId)
    {
        string localName = UserProgressManager.Instance?.CharacterName ?? "Player";
        ulong localId    = NetworkManager.Singleton.LocalClientId;
        using var writer = new FastBufferWriter(FixedString64Bytes.UTF8MaxLengthInBytes + 2 + 8, Allocator.Temp);
        writer.WriteValueSafe(new FixedString64Bytes(localName));
        writer.WriteValueSafe(localId);
        NetworkManager.Singleton.CustomMessagingManager
            .SendNamedMessage(MSG_PLAYER_NAME, targetClientId, writer);
    }

    void HandlePlayerName(ulong senderId, FastBufferReader reader)
    {
        reader.ReadValueSafe(out FixedString64Bytes name);
        reader.ReadValueSafe(out ulong originalId);
        string nameStr = name.ToString();

        OpponentName             = nameStr;
        _playerNames[originalId] = nameStr;
        Debug.Log($"[NGM] Játékos neve: {nameStr} (id: {originalId})");

        if (NetworkManager.Singleton.IsHost)
        {
            // Szerver: továbbítja a többi kliensnek (originalId már benne van az üzenetben)
            foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                if (clientId == originalId || clientId == NetworkManager.ServerClientId) continue;
                using var fwd = new FastBufferWriter(FixedString64Bytes.UTF8MaxLengthInBytes + 2 + 8, Allocator.Temp);
                fwd.WriteValueSafe(name);
                fwd.WriteValueSafe(originalId);
                NetworkManager.Singleton.CustomMessagingManager
                    .SendNamedMessage(MSG_PLAYER_NAME, clientId, fwd);
            }
        }
        else
        {
            // Csak a host KÖZVETLEN bemutatkozására válaszolunk a saját nevünkkel.
            // A szerver által TOVÁBBÍTOTT peer-nevekre tilos echózni, különben
            // exponenciális broadcast-vihar keletkezik (3+ játékossal disconnect).
            if (originalId == NetworkManager.ServerClientId)
            {
                SendPlayerName(NetworkManager.ServerClientId);
                SendGooglePlayerId(NetworkManager.ServerClientId);
            }
        }
    }

    void HandleIncomingEnemyInternal(byte enemyIndex, ulong senderId, string senderName = "")
    {
        Debug.Log($"[NGM] Bejövő szörny – index: {enemyIndex} | sender: {senderId} ({senderName})");

        if (PvPSendPanel.Instance == null || WaveManager.Instance == null) return;

        var prefab = PvPSendPanel.Instance.GetEnemyPrefab(enemyIndex);
        if (prefab == null)
        {
            Debug.LogWarning($"[NGM] Bejövő szörny – prefab null! (index: {enemyIndex})");
            return;
        }

        var def = PvPSendPanel.Instance.GetEnemyDefinition(enemyIndex);
        int count = def != null ? Mathf.Max(1, def.sendCount) : 1;
        float minD = def != null ? def.minSpawnDelay : 0.2f;
        float maxD = def != null ? def.maxSpawnDelay : 1f;

        WaveManager.Instance.AddPvPGroup(prefab, count, senderId,
            silent: false, minDelay: minD, maxDelay: maxD);

        if (WavePreviewUI.Instance != null && def != null)
            WavePreviewUI.Instance.AddPvPEnemyToPreview(def.icon, def.enemyName, senderName);
    }

    // ── Küldött szörny túlélési arany ───────────────────────────────

    public void ReportEnemySurvival(ulong senderId, float aliveSeconds)
    {
        if (!IsPvPMode || NetworkManager.Singleton == null) return;

        float goldAmount = aliveSeconds * 0.1f;
        if (goldAmount <= 0f) return;

        if (NetworkManager.Singleton.IsHost)
        {
            ulong localId = NetworkManager.Singleton.LocalClientId;
            if (senderId == localId)
            {
                GameManager.Instance?.AddFractionalGold(goldAmount);
            }
            else
            {
                using var writer = new FastBufferWriter(4, Allocator.Temp);
                writer.WriteValueSafe(aliveSeconds);
                NetworkManager.Singleton.CustomMessagingManager
                    .SendNamedMessage(MSG_SENDER_GOLD, senderId, writer);
            }
        }
        else
        {
            using var writer = new FastBufferWriter(12, Allocator.Temp);
            writer.WriteValueSafe(senderId);
            writer.WriteValueSafe(aliveSeconds);
            NetworkManager.Singleton.CustomMessagingManager
                .SendNamedMessage(MSG_RELAY_SENDER_GOLD, NetworkManager.ServerClientId, writer);
        }
    }

    void HandleSenderGold(FastBufferReader reader)
    {
        reader.ReadValueSafe(out float aliveSeconds);
        GameManager.Instance?.AddFractionalGold(aliveSeconds * 0.1f);
    }

    void HandleRelaySenderGold(FastBufferReader reader)
    {
        if (!NetworkManager.Singleton.IsHost) return;
        reader.ReadValueSafe(out ulong senderId);
        reader.ReadValueSafe(out float aliveSeconds);

        ulong localId = NetworkManager.Singleton.LocalClientId;
        if (senderId == localId)
        {
            GameManager.Instance?.AddFractionalGold(aliveSeconds * 0.1f);
        }
        else
        {
            using var writer = new FastBufferWriter(4, Allocator.Temp);
            writer.WriteValueSafe(aliveSeconds);
            NetworkManager.Singleton.CustomMessagingManager
                .SendNamedMessage(MSG_SENDER_GOLD, senderId, writer);
        }
    }

    // ── Hullám végi survival gold ────────────────────────────────────

    public void AwardWaveEndGoldToSender(ulong senderId, int amount)
    {
        if (!IsPvPMode || NetworkManager.Singleton == null) return;

        if (NetworkManager.Singleton.IsHost)
        {
            if (senderId == NetworkManager.Singleton.LocalClientId)
                GameManager.Instance?.AddGold(amount);
            else
            {
                using var writer = new FastBufferWriter(4, Allocator.Temp);
                writer.WriteValueSafe(amount);
                NetworkManager.Singleton.CustomMessagingManager
                    .SendNamedMessage(MSG_WAVE_END_GOLD, senderId, writer);
            }
        }
        else
        {
            using var writer = new FastBufferWriter(12, Allocator.Temp);
            writer.WriteValueSafe(senderId);
            writer.WriteValueSafe(amount);
            NetworkManager.Singleton.CustomMessagingManager
                .SendNamedMessage(MSG_RELAY_WAVE_END_GOLD, NetworkManager.ServerClientId, writer);
        }
    }

    void HandleWaveEndGold(FastBufferReader reader)
    {
        reader.ReadValueSafe(out int amount);
        GameManager.Instance?.AddGold(amount);
    }

    void HandleRelayWaveEndGold(FastBufferReader reader)
    {
        if (!NetworkManager.Singleton.IsHost) return;
        reader.ReadValueSafe(out ulong senderId);
        reader.ReadValueSafe(out int amount);

        if (senderId == NetworkManager.Singleton.LocalClientId)
            GameManager.Instance?.AddGold(amount);
        else
        {
            using var writer = new FastBufferWriter(4, Allocator.Temp);
            writer.WriteValueSafe(amount);
            NetworkManager.Singleton.CustomMessagingManager
                .SendNamedMessage(MSG_WAVE_END_GOLD, senderId, writer);
        }
    }

    // ── Küldött szörny számlálók ─────────────────────────────────────

    public void NotifySenderEnemyDelta(ulong senderId, int delta)
    {
        if (!IsPvPMode || NetworkManager.Singleton == null) return;

        ulong localId = NetworkManager.Singleton.LocalClientId;

        if (senderId == localId)
        {
            ApplySenderDelta(delta);
            return;
        }

        if (NetworkManager.Singleton.IsHost)
        {
            using var writer = new FastBufferWriter(4, Allocator.Temp);
            writer.WriteValueSafe(delta);
            NetworkManager.Singleton.CustomMessagingManager
                .SendNamedMessage(MSG_SENT_ENEMY_DELTA, senderId, writer);
        }
        else
        {
            using var writer = new FastBufferWriter(12, Allocator.Temp);
            writer.WriteValueSafe(senderId);
            writer.WriteValueSafe(delta);
            NetworkManager.Singleton.CustomMessagingManager
                .SendNamedMessage(MSG_RELAY_SENT_ENEMY_DELTA, NetworkManager.ServerClientId, writer);
        }
    }

    void HandleSentEnemyDelta(FastBufferReader reader)
    {
        reader.ReadValueSafe(out int delta);
        ApplySenderDelta(delta);
    }

    void HandleRelaySentEnemyDelta(ulong relayerId, FastBufferReader reader)
    {
        if (!NetworkManager.Singleton.IsHost) return;
        reader.ReadValueSafe(out ulong senderId);
        reader.ReadValueSafe(out int delta);

        ulong localId = NetworkManager.Singleton.LocalClientId;
        if (senderId == localId)
            ApplySenderDelta(delta);
        else
        {
            using var writer = new FastBufferWriter(4, Allocator.Temp);
            writer.WriteValueSafe(delta);
            NetworkManager.Singleton.CustomMessagingManager
                .SendNamedMessage(MSG_SENT_ENEMY_DELTA, senderId, writer);
        }
    }

    void ApplySenderDelta(int delta)
    {
        if (GameManager.Instance == null) return;
        if (delta > 0) GameManager.Instance.IncrementSentEnemies();
        else           GameManager.Instance.DecrementSentEnemies();
    }

    // ── Segédfüggvények ──────────────────────────────────────────────

    void ResetRoundState()
    {
        _castleFallen        = false;
        _resultShown         = false;
        _gameEnded           = false;
        _eliminationOrder.Clear();
        _sentEnemyCounts.Clear();
        _initialPlayerCount  = 0;
        _matchStartTime      = 0f;
    }

    // ── Google player_id kezelés ─────────────────────────────────────

    /// <summary>
    /// Kliens hívja: elküldi a saját Google player_id-ját a szervernek.
    /// HandlePlayerName-ből hívódik (a szerver neve-üzenetére válaszolva).
    /// </summary>
    void SendGooglePlayerId(ulong targetClientId)
    {
        string pid = MatchmakingClient.Instance?.PlayerId ?? GoogleAuthManager.Instance?.UserId ?? "";
        if (string.IsNullOrEmpty(pid)) return;

        using var writer = new FastBufferWriter(FixedString64Bytes.UTF8MaxLengthInBytes + 2, Allocator.Temp);
        writer.WriteValueSafe(new FixedString64Bytes(pid));
        NetworkManager.Singleton.CustomMessagingManager
            .SendNamedMessage(MSG_PLAYER_ID, targetClientId, writer);
    }

    /// <summary>Szerveren fogadja a kliens Google player_id-ját.</summary>
    void HandlePlayerId(ulong senderId, FastBufferReader reader)
    {
        if (!NetworkManager.Singleton.IsHost) return;
        reader.ReadValueSafe(out FixedString64Bytes pid);
        string pidStr = pid.ToString();
        _googlePlayerIds[senderId] = pidStr;
        Debug.Log($"[NGM] Google player_id fogadva: {senderId} → {pidStr}");
    }

    /// <summary>Visszaadja egy clientId-hoz tartozó Google player_id-t (vagy üres stringet).</summary>
    string GetGooglePlayerId(ulong clientId)
    {
        return _googlePlayerIds.TryGetValue(clientId, out string pid) ? pid : "";
    }

    /// <summary>JSON tömb az adott kliens szörny-küldési statisztikáiból.</summary>
    string BuildEnemyJson(ulong clientId)
    {
        if (!_sentEnemyCounts.TryGetValue(clientId, out var counts) || counts.Count == 0)
            return "[]";

        var sb = new StringBuilder("[");
        bool first = true;
        foreach (var kv in counts)
        {
            if (!first) sb.Append(",");
            string escaped = kv.Key.Replace("\\", "\\\\").Replace("\"", "\\\"");
            sb.Append($"{{\"name\":\"{escaped}\",\"count\":{kv.Value}}}");
            first = false;
        }
        sb.Append("]");
        return sb.ToString();
    }

    // ── Meccs eredmény bejelentés (csak dedikált szerveren) ───────────

    /// <summary>
    /// Meccs végén elküldi a helyezési sorrendet a Python API-nak (POST /match/result, localhost).
    /// Csak UNITY_SERVER buildben fut. Hiányzó player_id esetén az adott játékos kimarad.
    /// </summary>
    IEnumerator ReportMatchResultToApi()
    {
        // match_id kinyerése a parancssor argumentumból (DedicatedServerBootstrap-pel megegyező logika)
        string matchId = "";
        string[] args  = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "-matchId") { matchId = args[i + 1]; break; }

        if (string.IsNullOrEmpty(matchId))
        {
            Debug.LogWarning("[NGM] ReportMatchResult: nincs -matchId parancssori arg – kihagyva.");
            yield break;
        }

        // Helyezési lista összeállítása
        // _eliminationOrder[0] = legelső kiesett = legrosszabb helyezés
        // _alivePlayers utolsó tagja = győztes = 1. helyezés
        int total = _initialPlayerCount > 0 ? _initialPlayerCount
                  : _eliminationOrder.Count + _alivePlayers.Count;

        var sb = new System.Text.StringBuilder();
        sb.Append("{");
        sb.Append($"\"match_id\":\"{matchId}\",");
        sb.Append("\"results\":[");

        bool first = true;

        int playSeconds = _matchStartTime > 0f
            ? Mathf.RoundToInt(Time.realtimeSinceStartup - _matchStartTime) : 0;

        // Győztes(ek): placement = 1
        foreach (ulong winnerId in _alivePlayers)
        {
            string pid = GetGooglePlayerId(winnerId);
            if (string.IsNullOrEmpty(pid)) continue;
            if (!first) sb.Append(",");
            sb.Append($"{{\"player_id\":\"{pid}\",\"placement\":1,\"sent_hp\":0,");
            sb.Append($"\"play_seconds\":{playSeconds},");
            sb.Append("\"sent_enemies\":");
            sb.Append(BuildEnemyJson(winnerId));
            sb.Append("}");
            first = false;
        }

        // Kiesők: az _eliminationOrder utolsó tagja = 2. helyezés, első tagja = total. helyezés
        for (int i = _eliminationOrder.Count - 1; i >= 0; i--)
        {
            ulong loserId = _eliminationOrder[i];
            string pid = GetGooglePlayerId(loserId);
            if (string.IsNullOrEmpty(pid)) continue;
            int placement = total - i;
            if (!first) sb.Append(",");
            sb.Append($"{{\"player_id\":\"{pid}\",\"placement\":{placement},\"sent_hp\":0,");
            sb.Append($"\"play_seconds\":{playSeconds},");
            sb.Append("\"sent_enemies\":");
            sb.Append(BuildEnemyJson(loserId));
            sb.Append("}");
            first = false;
        }

        sb.Append("]}");

        if (first)
        {
            Debug.LogWarning("[NGM] ReportMatchResult: nincs egyetlen ismert player_id sem – kihagyva.");
            yield break;
        }

        string json = sb.ToString();
        Debug.Log($"[NGM] ReportMatchResult → {json}");

        using var req = new UnityWebRequest("http://127.0.0.1:8080/match/result", "POST");
        req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.timeout = 10;

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
            Debug.Log($"[NGM] ReportMatchResult OK: {req.downloadHandler.text}");
        else
            Debug.LogWarning($"[NGM] ReportMatchResult hiba: {req.responseCode} – {req.error}");
    }

    /// <summary>Hostnevet IPv4 címre old fel. Ha már IP, vagy ha hibázik, null/eredeti.</summary>
    public static string ResolveHostToIPv4(string hostOrIp)
    {
        if (string.IsNullOrEmpty(hostOrIp)) return null;
        // Ha már IP, ne foglalkozzunk DNS-szel
        if (IPAddress.TryParse(hostOrIp, out _)) return hostOrIp;
        try
        {
            var addresses = Dns.GetHostAddresses(hostOrIp);
            foreach (var addr in addresses)
                if (addr.AddressFamily == AddressFamily.InterNetwork)
                    return addr.ToString();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[NGM] DNS hiba ({hostOrIp}): {e.Message}");
        }
        return null;
    }

    /// <summary>Visszaadja az eszköz lokális WiFi IP-jét (legacy lobby kijelzőhöz).</summary>
    public string GetLocalIPAddress()
    {
        try
        {
            var addresses = Dns.GetHostEntry(Dns.GetHostName()).AddressList;
            foreach (var ip in addresses)
                if (ip.AddressFamily == AddressFamily.InterNetwork &&
                    ip.ToString().StartsWith("192.168."))
                    return ip.ToString();
            foreach (var ip in addresses)
                if (ip.AddressFamily == AddressFamily.InterNetwork &&
                    ip.ToString().StartsWith("10."))
                    return ip.ToString();
            foreach (var ip in addresses)
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                    return ip.ToString();
        }
        catch { }
        return "127.0.0.1";
    }

    // ── Játékos kiesés broadcast ─────────────────────────────────────

    void BroadcastPlayerEliminated(ulong eliminatedId, int alivePlayers)
    {
        if (!NetworkManager.Singleton.IsHost) return;

        string eliminatedName = GetPlayerName(eliminatedId);
        using var writer = new FastBufferWriter(256, Allocator.Temp);
        writer.WriteValueSafe(eliminatedId);
        writer.WriteValueSafe(eliminatedName);
        writer.WriteValueSafe(alivePlayers);

        foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            if (clientId == NetworkManager.ServerClientId) continue;
            NetworkManager.Singleton.CustomMessagingManager
                .SendNamedMessage(MSG_PLAYER_ELIMINATED, clientId, writer);
        }
    }

    void HandlePlayerEliminated(FastBufferReader reader)
    {
        reader.ReadValueSafe(out ulong eliminatedId);
        reader.ReadValueSafe(out string eliminatedName);
        reader.ReadValueSafe(out int alivePlayers);
        OpponentDataTracker.Instance?.RemovePlayer(eliminatedId);

        // Ne mutassuk ha: a saját magunk esett ki, vagy már csak 1 maradt (győztes kap saját hangot)
        bool isSelf = NetworkManager.Singleton?.LocalClientId == eliminatedId;
        if (!isSelf && alivePlayers > 1)
            UIManager.Instance?.ShowEliminationNotice(eliminatedName);
    }

    // ── Spy Tower: torony lerakás + kastély HP szinkronizálás ────────

    /// <summary>
    /// Kliens hívja torony lerakáskor – elküldi a szerver felé, onnan broadcast-ol a többi kliensnek.
    /// </summary>
    public void BroadcastTowerPlaced(string towerName)
    {
        if (!IsPvPMode || NetworkManager.Singleton == null) return;

        // 4 (int hossz) + towerName.Length * 2 (UTF-16) + 8 (ulong clientId)
        using var writer = new FastBufferWriter(4 + towerName.Length * 2 + 8, Allocator.Temp);
        writer.WriteValueSafe(towerName);
        writer.WriteValueSafe(NetworkManager.Singleton.LocalClientId);
        NetworkManager.Singleton.CustomMessagingManager
            .SendNamedMessage(MSG_TOWER_PLACED, NetworkManager.ServerClientId, writer);
        Debug.Log($"[SpyTower] BroadcastTowerPlaced | tower={towerName} | localId={NetworkManager.Singleton.LocalClientId}");
    }

    void HandleTowerPlaced(ulong senderId, FastBufferReader reader)
    {
        reader.ReadValueSafe(out string towerName);
        reader.ReadValueSafe(out ulong originalSender);

        Debug.Log($"[SpyTower] HandleTowerPlaced | isHost={NetworkManager.Singleton.IsHost} | sender={originalSender} | tower={towerName} | tracker={(OpponentDataTracker.Instance != null ? "OK" : "NULL")}");

        if (NetworkManager.Singleton.IsHost)
        {
            OpponentDataTracker.Instance?.RecordTowerPlaced(originalSender, towerName);

            foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                if (clientId == originalSender || clientId == NetworkManager.ServerClientId) continue;
                using var fwd = new FastBufferWriter(64, Allocator.Temp);
                fwd.WriteValueSafe(towerName);
                fwd.WriteValueSafe(originalSender);
                NetworkManager.Singleton.CustomMessagingManager
                    .SendNamedMessage(MSG_TOWER_PLACED, clientId, fwd);
            }
        }
        else
        {
            // Kliens: rögzíti az ellenfél tornyát
            OpponentDataTracker.Instance?.RecordTowerPlaced(originalSender, towerName);
        }
    }

    /// <summary>
    /// Castle hívja HP változáskor – szétküld minden kliensnek.
    /// </summary>
    public void BroadcastCastleHp(int hp)
    {
        if (!IsPvPMode || NetworkManager.Singleton == null) return;

        using var writer = new FastBufferWriter(12, Allocator.Temp);
        writer.WriteValueSafe(hp);
        writer.WriteValueSafe(NetworkManager.Singleton.LocalClientId);
        NetworkManager.Singleton.CustomMessagingManager
            .SendNamedMessage(MSG_CASTLE_HP, NetworkManager.ServerClientId, writer);
    }

    void HandleCastleHp(ulong senderId, FastBufferReader reader)
    {
        reader.ReadValueSafe(out int hp);
        reader.ReadValueSafe(out ulong originalSender);

        Debug.Log($"[SpyTower] HandleCastleHp | isHost={NetworkManager.Singleton.IsHost} | sender={originalSender} | hp={hp} | tracker={(OpponentDataTracker.Instance != null ? "OK" : "NULL")}");

        if (NetworkManager.Singleton.IsHost)
        {
            OpponentDataTracker.Instance?.RecordCastleHp(originalSender, hp);

            foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                if (clientId == originalSender || clientId == NetworkManager.ServerClientId) continue;
                using var fwd = new FastBufferWriter(12, Allocator.Temp);
                fwd.WriteValueSafe(hp);
                fwd.WriteValueSafe(originalSender);
                NetworkManager.Singleton.CustomMessagingManager
                    .SendNamedMessage(MSG_CASTLE_HP, clientId, fwd);
            }
        }
        else
        {
            OpponentDataTracker.Instance?.RecordCastleHp(originalSender, hp);
        }
    }
}
