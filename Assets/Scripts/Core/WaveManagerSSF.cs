using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SSF (Solo Self Found) hullám konfiguráció + Gold Pool generátor.
/// Csak SSF karakternél aktív – a WaveManager mellé kerül ugyanarra a GameObject-re.
/// Start()-ban felülírja a WaveManager mezőit, majd hullámonként Gold Pool
/// alapján véletlenszerűen küld szörnyeket a játékos ellen.
/// PvP karakternél automatikusan kikapcsol.
/// </summary>
public class WaveManagerSSF : MonoBehaviour
{
    // Sentinel ID: az SSF generátor által küldött szörnyek azonosítója (WaveManager-ben definiálva)
    const ulong SSF_SENDER_ID = WaveManager.SSF_SENDER_ID;

    [Header("SSF – Hullám beállítások")]
    [Tooltip("Idő (mp) két hullám között. Alap WaveManager: 10")]
    public float waveInterval = 8f;

    [Tooltip("Hány szörny jön az első hullámban. Alap: 5")]
    public int startingEnemyCount = 8;

    [Tooltip("Hány szörnynyel nő hullámonként. Alap: 10")]
    public int enemyIncreasePerWave = 12;

    [Header("SSF – Spawn késleltetés")]
    [Tooltip("Minimum késleltetés szörnyek között (mp). Alap: 0.2")]
    public float minSpawnDelay = 0.1f;

    [Tooltip("Maximum késleltetés szörnyek között (mp). Alap: 1")]
    public float maxSpawnDelay = 0.7f;

    [Header("SSF – Szörnyek szóródása")]
    [Tooltip("Vízszintes szóródás. Alap: 0.3")]
    public float enemyPathOffsetX = 0.4f;

    [Tooltip("Függőleges szóródás. Alap: 0.3")]
    public float enemyPathOffsetY = 0.4f;

    [Tooltip("Sebesség szóródás %-ban. Alap: 10")]
    [Range(0, 50)]
    public float enemySpeedVariancePercent = 20f;

    [Header("SSF – Lvl átváltás")]
    [Tooltip("Hány sima Ork ér egy Lvl2-est. Alap: 10")]
    public int orcsPerLvl2 = 8;

    [Header("SSF – Gold Pool")]
    [Tooltip("Az első hullámban rendelkezésre álló Gold Pool.")]
    public int startingGoldPool = 4;

    [Tooltip("Hullámonként ennyivel nő a Gold Pool.")]
    public int goldPoolIncreasePerWave = 10;

    [Tooltip("Hullámonként ezzel szorozzuk a Gold Poolt (1.0 = nincs szorzó, 1.1 = +10%/hullám).")]
    [Range(1f, 2f)]
    public float goldPoolMultiplierPerWave = 1.1f;

    [Header("SSF – Hullámok száma")]
    [Tooltip("0 = végtelen. Alap: 0")]
    public int totalWaves = 0;

    // ── Belső állapot ─────────────────────────────────────────────────────
    private float _currentGoldPool = 0f;

    // ── Életciklus ────────────────────────────────────────────────────────

    void Start()
    {
        var mgr = UserProgressManager.Instance;
        if (mgr == null || !mgr.HasCharacter || !mgr.Data.IsSSF)
        {
            enabled = false;
            return;
        }

        ApplyWaveManagerSettings();

        _currentGoldPool = startingGoldPool;

        if (WaveManager.Instance != null)
            WaveManager.Instance.OnWaveStarted += OnWaveStarted;
    }

    void OnDestroy()
    {
        if (WaveManager.Instance != null)
            WaveManager.Instance.OnWaveStarted -= OnWaveStarted;
    }

    // ── WaveManager beállítások alkalmazása ───────────────────────────────

    void ApplyWaveManagerSettings()
    {
        var wm = WaveManager.Instance;
        if (wm == null)
        {
            Debug.LogWarning("WaveManagerSSF: WaveManager.Instance nem található!");
            return;
        }

        wm.waveInterval              = waveInterval;
        wm.startingEnemyCount        = startingEnemyCount;
        wm.enemyIncreasePerWave      = enemyIncreasePerWave;
        wm.minSpawnDelay             = minSpawnDelay;
        wm.maxSpawnDelay             = maxSpawnDelay;
        wm.enemyPathOffsetX          = enemyPathOffsetX;
        wm.enemyPathOffsetY          = enemyPathOffsetY;
        wm.enemySpeedVariancePercent = enemySpeedVariancePercent;
        wm.orcsPerLvl2               = orcsPerLvl2;
        wm.totalWaves                = totalWaves;

        Debug.Log("WaveManagerSSF: SSF beállítások alkalmazva.");
    }

    // ── Gold Pool generátor ───────────────────────────────────────────────

    void OnWaveStarted(int wave)
    {
        // 1. hullámban az alap pool van, utána képlet: (előző + inc) * mult
        if (wave > 1)
            _currentGoldPool = (_currentGoldPool + goldPoolIncreasePerWave) * goldPoolMultiplierPerWave;

        int pool = Mathf.FloorToInt(_currentGoldPool);

        Debug.Log($"WaveManagerSSF: {wave}. hullám – Gold Pool: {pool}");

        GenerateAndSendEnemies(pool);
    }

    void GenerateAndSendEnemies(int pool)
    {
        var panel = PvPSendPanel.Instance;
        if (panel == null || panel.sendableEnemies == null || panel.sendableEnemies.Length == 0) return;

        var enemies = panel.sendableEnemies;

        // Legalacsonyabb goldCost meghatározása – ha ez alá esik a pool, megállunk
        int minCost = int.MaxValue;
        foreach (var e in enemies)
            if (e != null && e.goldCost < minCost)
                minCost = e.goldCost;

        if (pool < minCost)
        {
            Debug.Log($"WaveManagerSSF: Gold Pool ({pool}) a minimum cost ({minCost}) alatt – nem küld szörnyet.");
            return;
        }

        var sentLog = new System.Text.StringBuilder();

        // ── 1. Első szörny: legdrágább ami belefér ────────────────────────
        int maxAffordableCost = 0;
        foreach (var e in enemies)
            if (e != null && e.goldCost <= pool && e.goldCost > maxAffordableCost)
                maxAffordableCost = e.goldCost;

        var mostExpensive = new List<SendableEnemyDefinition>();
        foreach (var e in enemies)
            if (e != null && e.goldCost == maxAffordableCost)
                mostExpensive.Add(e);

        var first = mostExpensive[Random.Range(0, mostExpensive.Count)];
        pool -= first.goldCost;
        SendEnemy(first);
        sentLog.AppendLine($"  {first.enemyName} ({first.goldCost}g)");

        // ── 2. Véletlenszerű küldés amíg belefér ─────────────────────────
        while (pool >= minCost)
        {
            var affordable = new List<SendableEnemyDefinition>();
            foreach (var e in enemies)
                if (e != null && e.goldCost <= pool)
                    affordable.Add(e);

            if (affordable.Count == 0) break;

            var picked = affordable[Random.Range(0, affordable.Count)];
            pool -= picked.goldCost;
            SendEnemy(picked);
            sentLog.AppendLine($"  {picked.enemyName} ({picked.goldCost}g)");
        }

        Debug.Log($"WaveManagerSSF: Küldött szörnyek:\n{sentLog}Maradék Gold Pool: {pool}g (elveszik).");
    }

    void SendEnemy(SendableEnemyDefinition def)
    {
        if (def.prefab == null) return;

        WaveManager.Instance?.AddPvPGroup(
            def.prefab,
            def.sendCount,
            SSF_SENDER_ID,
            silent: true,
            def.minSpawnDelay,
            def.maxSpawnDelay
        );

        // Gold Pool szörnyek megjelenítése a Wave Preview-ban (külön sorban, "SSF" küldővel)
        for (int i = 0; i < def.sendCount; i++)
            WavePreviewUI.Instance?.AddPvPEnemyToPreview(def.icon, def.enemyName, "SSF");

        string countLabel = def.sendCount > 1 ? $" x{def.sendCount}" : "";
        UIManager.Instance?.ShowNotification(
            $"[SSF] sent {def.enemyName}{countLabel}!",
            new Color(1f, 0.6f, 0f) // narancs – megkülönböztethető a PvP piros értesítéstől
        );
    }
}
