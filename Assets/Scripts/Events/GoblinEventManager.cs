using System.Collections;
using UnityEngine;

/// <summary>
/// Véletlenszerű goblin esemény – pity rendszerrel.
/// SSF/solo módban: lokálisan sorsolja ki a goblint és a rúnát.
/// PvP módban: csak a szerver sorsolja (OnWaveStarted), majd NetworkGameManager
/// broadcastolja az összes kliensnek (ReceiveGoblinEvent). Ez garantálja, hogy
/// mindkét játékosnál ugyanabban a hullámban ugyanaz a rúna jelenik meg.
/// </summary>
public class GoblinEventManager : MonoBehaviour
{
    public static GoblinEventManager Instance { get; private set; }

    [Header("Goblin prefab")]
    public GameObject goblinPrefab;

    [Header("Rúna pickup prefabok (3 verzió)")]
    public GameObject[] runePickupPrefabs;

    [Header("Pity rendszer")]
    [Tooltip("Alap esély hullámonként (0–1). Pl. 0.05 = 5%")]
    public float baseChance = 0.05f;
    [Tooltip("Esély növekedése minden kihagyott hullám után")]
    public float chanceIncrement = 0.05f;
    [Tooltip("Maximum elérhető esély (0–1). Pl. 0.95 = 95%")]
    public float maxChance = 0.95f;

    [Header("HP skálázás hullámonként")]
    [Tooltip("Alap HP szorzó az 1. hullámon (1 = prefab HP)")]
    public float hpBaseMultiplier = 1f;
    [Tooltip("HP növekedés hullámonként %-ban. Pl. 0.10 = +10% minden hullámmal")]
    public float hpPerWavePercent = 0.10f;

    [Header("Spawn késleltetés")]
    [Tooltip("Hány másodperccel a hullám indulása után jelenjen meg a goblin")]
    public float spawnDelayAfterWave = 3f;

    [Header("Hang")]
    [Tooltip("Ez a hang szól amikor a goblin megjelenik")]
    public AudioClip goblinSpawnSound;
    [Tooltip("Ez a hang szól amikor a goblin meghal")]
    public AudioClip goblinDeathSound;

    [Header("Rúna konfiguráció")]
    public RuneConfig runeConfig;

    private float _currentChance;
    private Coroutine _pendingSpawn;
    private int _currentWave;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _currentChance = baseChance;
    }

    void Start()
    {
        if (WaveManager.Instance != null)
            WaveManager.Instance.OnWaveStarted += OnWaveStarted;
    }

    void OnDestroy()
    {
        if (WaveManager.Instance != null)
            WaveManager.Instance.OnWaveStarted -= OnWaveStarted;
    }

    void OnWaveStarted(int wave)
    {
        _currentWave = wave;
        if (goblinPrefab == null) return;
        if (GameManager.Instance == null || GameManager.Instance.IsGameOver) return;

        bool isPvP = NetworkGameManager.Instance != null && NetworkGameManager.Instance.IsPvPMode;

        // PvP módban csak a szerver sorsolhat
        if (isPvP)
        {
            if (!Unity.Netcode.NetworkManager.Singleton.IsServer) return;
        }

        float roll = Random.value;
        if (roll < _currentChance)
        {
            float triggeredAt = _currentChance;
            _currentChance = baseChance;

            int runeIdx = (runePickupPrefabs != null && runePickupPrefabs.Length > 0)
                ? Random.Range(0, runePickupPrefabs.Length)
                : 0;

            Debug.Log($"[GoblinEvent] Aktiválódott! Hullám: {wave}, esély: {triggeredAt:P0}, rúna: {runeIdx}");

            if (isPvP)
                NetworkGameManager.Instance.BroadcastGoblinEvent(runeIdx);
            else
                TriggerSpawn(runeIdx);
        }
        else
        {
            _currentChance = Mathf.Min(maxChance, _currentChance + chanceIncrement);
            Debug.Log($"[GoblinEvent] Kihagyva – roll: {roll:F2}, következő esély: {_currentChance:P0}");
        }
    }

    /// <summary>NetworkGameManager hívja PvP módban, szerver broadcastja alapján.</summary>
    public void ReceiveGoblinEvent(int runeIdx)
    {
        TriggerSpawn(runeIdx);
    }

    void TriggerSpawn(int runeIdx)
    {
        if (_pendingSpawn != null) StopCoroutine(_pendingSpawn);
        _pendingSpawn = StartCoroutine(SpawnGoblinDelayed(runeIdx));
    }

    IEnumerator SpawnGoblinDelayed(int runeIdx)
    {
        if (spawnDelayAfterWave > 0f)
            yield return new WaitForSeconds(spawnDelayAfterWave);

        if (GameManager.Instance == null || GameManager.Instance.IsGameOver) yield break;
        if (GridManager.Instance == null) yield break;

        Vector3 spawnPos = GridManager.Instance.GridToWorld(GridManager.Instance.SpawnCell);
        AudioManager.Instance?.PlaySFX(goblinSpawnSound);

        GameObject go = Instantiate(goblinPrefab, spawnPos, Quaternion.identity);

        var enemy = go.GetComponent<Enemy>();
        if (enemy != null)
        {
            enemy.enemyTypeName     = "GoblinEvent";
            enemy.countAsKill       = false;
            enemy.dealsCastleDamage = false;

            float multiplier = hpBaseMultiplier * Mathf.Pow(1f + hpPerWavePercent, _currentWave - 1);
            enemy.ScaleHealth(multiplier);

            enemy.OnDied += (e) => OnGoblinKilled(e, runeIdx);
        }
    }

    void OnGoblinKilled(Enemy enemy, int runeIdx)
    {
        AudioManager.Instance?.PlaySFX(goblinDeathSound);

        if (runePickupPrefabs == null || runePickupPrefabs.Length == 0) return;
        runeIdx = Mathf.Clamp(runeIdx, 0, runePickupPrefabs.Length - 1);

        GameObject prefab = runePickupPrefabs[runeIdx];
        if (prefab == null) return;

        GameObject runeGO = Instantiate(prefab, enemy.transform.position, Quaternion.identity);
        var pickup = runeGO.GetComponent<RunePickup>();
        if (pickup != null)
        {
            pickup.sourcePrefab = prefab;
            pickup.OnCollected += OnRuneCollected;
        }
    }

    void OnRuneCollected(RunePickup pickup)
    {
        RuneBuffManager.Instance?.AddStack(pickup.sourcePrefab);
        if (runeConfig != null)
            AudioManager.Instance?.PlaySFX(runeConfig.GetCollectSound(pickup.sourcePrefab));
    }

    /// <summary>Aktuális spawn esély – UI-hoz / debug-hoz.</summary>
    public float CurrentChance => _currentChance;

}
