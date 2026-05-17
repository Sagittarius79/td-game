using UnityEngine;

/// <summary>
/// Halálakor a szörny helyén N másik szörny spawol.
///
/// Unity beállítás:
///   1. Húzd rá bármely enemy prefabra
///   2. Állítsd be a spawnPrefab és spawnCount mezőket
/// </summary>
[RequireComponent(typeof(Enemy))]
public class SplitOnDeath : MonoBehaviour
{
    [Header("Szétválás")]
    [Tooltip("Melyik szörny prefab spawoljon halálakor")]
    public GameObject spawnPrefab;

    [Tooltip("Hány szörny spawoljon")]
    [Min(1)]
    public int spawnCount = 2;

    [Tooltip("Kis véletlenszerű eltolás a spawn pozícióhoz (hogy ne fedjenek át)")]
    public float spawnScatter = 0.3f;

    private Enemy   _enemy;
    private Vector3 _deathPosition;

    void Awake()
    {
        _enemy = GetComponent<Enemy>();
    }

    void OnEnable()
    {
        if (_enemy != null)
            _enemy.OnDied += OnDied;
    }

    void OnDisable()
    {
        if (_enemy != null)
            _enemy.OnDied -= OnDied;
    }

    void OnDied(Enemy dead)
    {
        if (spawnPrefab == null) return;

        // Pozíció és waypoint index elmentése azonnal
        _deathPosition = dead.transform.position;
        int waypointIndex = dead.overrideStartWaypointIndex > 0
            ? dead.overrideStartWaypointIndex
            : dead.CurrentWaypointIndex;

        var waveManager = WaveManager.Instance;

        for (int i = 0; i < spawnCount; i++)
        {
            Vector3 offset = new Vector3(
                Random.Range(-spawnScatter, spawnScatter),
                Random.Range(-spawnScatter, spawnScatter),
                0f);

            Vector3 spawnPos = _deathPosition + offset;

            if (waveManager != null)
                waveManager.SpawnEnemyAt(spawnPrefab, spawnPos, waypointIndex);
            else
                Instantiate(spawnPrefab, spawnPos, Quaternion.identity);
        }
    }
}
