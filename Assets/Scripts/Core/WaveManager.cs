using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Hullám menedzser.
/// - Fix 10 másodperces hullám időköz (nem várja meg az előző végét)
/// - Véletlenszerű spawn delay az orkok között
/// </summary>
public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }

    [Header("Hullám beállítások")]
    public float waveInterval = 10f;            // fix 10 mp hullámok között
    public int startingEnemyCount = 5;
    public int enemyIncreasePerWave = 10;

    [Header("Spawn késleltetés")]
    public float minSpawnDelay = 0.2f;            // minimum 0 mp (szinte egyszerre)
    public float maxSpawnDelay = 1;          // maximum 1.5 mp

    [Header("Prefab-ok")]
    public GameObject orcPrefab;

    // Lvl2-es Ork prefab – ha 10+ sima Ork jönne, minden 10 helyett 1 Lvl2-es jön
    public GameObject orcLvl2Prefab;

    // Lvl3-as Ork prefab – ha 10+ Lvl2-es Ork jönne, minden 10 helyett 1 Lvl3-as jön
    public GameObject orcLvl3Prefab;

    [Header("Átváltás aránya")]
    // Hány alacsonyabb szintű Ork ér egy magasabb szintűt (alapból 10)
    public int orcsPerLvl2 = 10;

    [Header("Szörnyek szóródása az úton")]
    [Tooltip("Vízszintes (X) szóródás. 0 = nincs. 0.3 = ±0.3 egység balra/jobbra.")]
    public float enemyPathOffsetX = 0.3f;
    [Tooltip("Függőleges (Y) szóródás. 0 = nincs. 0.3 = ±0.3 egység fel/le.")]
    public float enemyPathOffsetY = 0.3f;
    [Tooltip("Sebesség szóródás %-ban. 10 = ±10% eltérés az alap sebességtől.")]
    [Range(0, 50)]
    public float enemySpeedVariancePercent = 10f;

    [Header("Hullámok száma (0 = végtelen)")]
    public int totalWaves = 0;

    [Header("Hang")]
    [Tooltip("Ez a hang szól, amikor egy küldött szörny megjelenik a pályán")]
    public AudioClip sentEnemySound;

    private int currentWave = 0;
    private int aliveEnemies = 0;
    private float nextWaveTime = 0f;
    private bool gameStarted = false;
    private readonly HashSet<ulong> _sentSoundPlayedBySender = new HashSet<ulong>();

    // PvP – ellenfél által küldött szörny csoportok a következő hullámhoz
    // Minden küldési akció egy önálló blokk (List) – így 3x10 patkány = 3 külön blokk
    private readonly Queue<List<(GameObject prefab, ulong senderId, float minDelay, float maxDelay)>> _pvpGroupQueue =
        new Queue<List<(GameObject prefab, ulong senderId, float minDelay, float maxDelay)>>();

    public int AliveEnemies  => aliveEnemies;
    public int CurrentWave   => currentWave;
    public float NextWaveCountdown => Mathf.Max(0f, nextWaveTime - Time.time);

    public System.Action<int> OnWaveStarted;
    public System.Action<int> OnWaveCompleted;
    public System.Action OnAllWavesCompleted;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        // Első hullám 3 másodperc múlva
        nextWaveTime = Time.time + 3f;
        gameStarted  = true;
    }

    void Update()
    {
        if (!gameStarted || GameManager.Instance == null) return;
        if (GameManager.Instance.IsGameOver) return;

        // Fix időközönként indul az új hullám
        // – nem várja meg az előző hullám végét!
        if (Time.time >= nextWaveTime)
        {
            nextWaveTime = Time.time + waveInterval;
            StartNextWave();
        }
    }

    void StartNextWave()
    {
        if (totalWaves > 0 && currentWave >= totalWaves)
        {
            OnAllWavesCompleted?.Invoke();
            return;
        }

        // PvP: ha más játékos közben kilépett és egyedül maradtunk, a host eldönti a győzelmet
        if (NetworkGameManager.Instance != null && NetworkGameManager.Instance.IsPvPMode)
            NetworkGameManager.Instance.CheckSoloVictory();

        currentWave++;
        _sentSoundPlayedBySender.Clear();
        if (GameManager.Instance != null)
        {
            GameManager.Instance.FinalizeWaveGold();   // előző hullám keresménye → LastWaveGoldEarned
            GameManager.Instance.SetWave(currentWave);
        }

        // ── Szörny mennyiség kiszámítása ──────────────────────────────
        // Alap képlet: 5 + (hullám-1) * 2  →  1. hullám=5, 2.=7, 3.=9, ...
        int totalOrcCount = startingEnemyCount + (currentWave - 1) * enemyIncreasePerWave;

        // ── Lvl2 átváltás logika ───────────────────────────────────────
        // Ha a sima Ork mennyiség eléri az orcsPerLvl2 értékét (alapból 10),
        // minden teljes 10 Ork helyett 1 Lvl2-es Ork jön.
        // Példa: 24 Ork → 24/10 = 2 db Lvl2 + 24%10 = 4 db sima Ork
        int lvl2Count   = 0;
        int normalCount = totalOrcCount;

        if (orcLvl2Prefab != null && totalOrcCount >= orcsPerLvl2)
        {
            lvl2Count   = totalOrcCount / orcsPerLvl2;   // egész osztás: hány Lvl2
            normalCount = totalOrcCount % orcsPerLvl2;   // maradék: hány sima Ork
        }

        // ── Lvl3 átváltás logika ───────────────────────────────────────
        // Ha a Lvl2-es Ork mennyiség eléri a 10-et,
        // minden teljes 10 Lvl2-es helyett 1 Lvl3-as Ork jön.
        // Példa: 24 Lvl2 → 24/10 = 2 db Lvl3 + 24%10 = 4 db Lvl2
        int lvl3Count = 0;

        if (orcLvl3Prefab != null && lvl2Count >= orcsPerLvl2)
        {
            lvl3Count = lvl2Count / orcsPerLvl2;   // egész osztás: hány Lvl3
            lvl2Count = lvl2Count % orcsPerLvl2;   // maradék: hány Lvl2
        }

        // ── Hullám szörnyek egységes típusba ─────────────────────────
        var spawnList = new List<(GameObject prefab, ulong senderId, float minDelay, float maxDelay)>();
        for (int i = 0; i < normalCount; i++) spawnList.Add((orcPrefab,     ulong.MaxValue, minSpawnDelay, maxSpawnDelay));
        for (int i = 0; i < lvl2Count;   i++) spawnList.Add((orcLvl2Prefab, ulong.MaxValue, minSpawnDelay, maxSpawnDelay));
        for (int i = 0; i < lvl3Count;   i++) spawnList.Add((orcLvl3Prefab, ulong.MaxValue, minSpawnDelay, maxSpawnDelay));

        // Fisher-Yates shuffle – csak a hullám szörnyei keverednek
        for (int i = spawnList.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (spawnList[i], spawnList[j]) = (spawnList[j], spawnList[i]);
        }

        // ── PvP csoportok – minden blokk külön, véletlenszerű pozícióba ──
        // Az indexeket az EREDETI hullám mérete alapján generáljuk,
        // majd hátulról előre szúrjuk be, hogy a korábbi beszúrások
        // ne tolják el a később szúrt indexeket.
        if (_pvpGroupQueue.Count > 0)
        {
            int waveSize = spawnList.Count;
            var groups = new List<(List<(GameObject, ulong, float, float)> group, int index)>();

            while (_pvpGroupQueue.Count > 0)
            {
                var group = _pvpGroupQueue.Dequeue();
                int insertAt = Random.Range(0, waveSize + 1);
                groups.Add((group, insertAt));
            }

            // Csökkenő index sorrendben szúrjuk be → a kisebb indexek nem tolódnak el
            groups.Sort((a, b) => b.index.CompareTo(a.index));
            foreach (var (group, idx) in groups)
                spawnList.InsertRange(idx, group);
        }

        OnWaveStarted?.Invoke(currentWave);
        Debug.Log($"Hullám {currentWave} indul – összesen {spawnList.Count} szörny (PvP csoportok vegyítve)");

        StartCoroutine(SpawnEnemies(spawnList));
    }

    IEnumerator SpawnEnemies(List<(GameObject prefab, ulong senderId, float minDelay, float maxDelay)> spawnList)
    {
        if (GridManager.Instance == null) yield break;

        Vector3 spawnWorld = GridManager.Instance.GridToWorld(GridManager.Instance.SpawnCell);

        foreach (var (prefab, senderId, minD, maxD) in spawnList)
        {
            if (GameManager.Instance != null && GameManager.Instance.IsGameOver) yield break;
            yield return new WaitForSeconds(Random.Range(minD, maxD));
            if (prefab == null) { Debug.LogWarning("WaveManager: null prefab, kihagyva."); continue; }
            SpawnSingleEnemy(prefab, spawnWorld, senderId);
        }
    }

    void SpawnSingleEnemy(GameObject prefab, Vector3 spawnWorld, ulong senderId = ulong.MaxValue)
    {
        GameObject enemyGO = Instantiate(prefab, spawnWorld, Quaternion.identity);
        var enemy = enemyGO.GetComponent<Enemy>();
        if (enemy != null)
        {
            enemy.senderClientId  = senderId;
            enemy.OnDied          += OnEnemyDied;
            enemy.OnReachedCastle += OnEnemyReachedCastle;
            aliveEnemies++;
            if (senderId != ulong.MaxValue)
                NotifySenderDelta(senderId, +1);
        }
    }

    /// <summary>PvP módban az ellenfél szörnyét adja hozzá a következő hullámhoz.</summary>
    /// <param name="senderId">A küldő kliens ID-ja – hullámonként csak az első küldésnél szól hang,
    /// és spawnoláskor az Enemy.senderClientId-ba kerül (túlélési aranyhoz).</param>
    /// <param name="silent">Ha true, nem játszik hangot (pl. saját küldésnél a küldő telefonján).</param>
    /// <summary>
    /// Egy teljes PvP csoportot ad hozzá – count db szörny = egy blokk,
    /// ami a következő hullámban egyben, véletlenszerű pozícióra kerül.
    /// </summary>
    public void AddPvPGroup(GameObject prefab, int count, ulong senderId, bool silent = false,
                            float minDelay = 0.2f, float maxDelay = 1f)
    {
        if (prefab == null || count <= 0) return;

        var group = new List<(GameObject, ulong, float, float)>();
        for (int i = 0; i < count; i++)
            group.Add((prefab, senderId, minDelay, maxDelay));

        _pvpGroupQueue.Enqueue(group);

        if (!silent && _sentSoundPlayedBySender.Add(senderId))
            AudioManager.Instance?.PlaySFX(sentEnemySound);
    }

    // Visszafelé kompatibilitás – egy darabot küld
    public void AddPvPEnemy(GameObject prefab, ulong senderId, bool silent = false,
                             float minDelay = 0.2f, float maxDelay = 1f)
        => AddPvPGroup(prefab, 1, senderId, silent, minDelay, maxDelay);

    void OnEnemyDied(Enemy enemy)
    {
        enemy.OnDied          -= OnEnemyDied;
        enemy.OnReachedCastle -= OnEnemyReachedCastle;

        if (enemy.senderClientId != ulong.MaxValue)
            NotifySenderDelta(enemy.senderClientId, -1);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddGold(GameManager.Instance.goldPerKill);
            GameManager.Instance.OnEnemyKilled?.Invoke();
        }

        EnemyFinished();
    }

    void OnEnemyReachedCastle(Enemy enemy)
    {
        enemy.OnDied          -= OnEnemyDied;
        enemy.OnReachedCastle -= OnEnemyReachedCastle;

        if (enemy.senderClientId != ulong.MaxValue)
            NotifySenderDelta(enemy.senderClientId, -1);

        EnemyFinished();
    }

    /// <summary>
    /// Értesíti a küldőt, hogy egy általa küldött szörny megjelent (+1) vagy eltűnt (-1).
    /// Solo módban lokálisan, PvP módban hálózaton keresztül.
    /// </summary>
    void NotifySenderDelta(ulong senderId, int delta)
    {
        bool isPvP = NetworkGameManager.Instance != null && NetworkGameManager.Instance.IsPvPMode;
        if (isPvP)
            NetworkGameManager.Instance.NotifySenderEnemyDelta(senderId, delta);
        else
        {
            if (delta > 0) GameManager.Instance?.IncrementSentEnemies();
            else           GameManager.Instance?.DecrementSentEnemies();
        }
    }

    void EnemyFinished()
    {
        aliveEnemies = Mathf.Max(0, aliveEnemies - 1);
    }
}
