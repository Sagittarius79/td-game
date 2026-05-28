using UnityEngine;

/// <summary>
/// Halálakor tojást spawnol a Phoenix jelenlegi pozíciójában.
///
/// Unity beállítás:
///   1. Húzd rá a Phoenix enemy prefabra
///   2. Állítsd be az Egg Prefab mezőt (a tojás prefabra)
///      – a tojás prefabon legyen PhoenixEgg komponens
/// </summary>
[RequireComponent(typeof(Enemy))]
public class PhoenixRebirth : MonoBehaviour
{
    [Header("Tojás")]
    [Tooltip("A tojás prefab – Enemy + PhoenixEgg komponenssel")]
    public GameObject eggPrefab;

    private Enemy _enemy;

    void Awake() => _enemy = GetComponent<Enemy>();

    void OnEnable()
    {
        if (_enemy != null) _enemy.OnDied += OnDied;
    }

    void OnDisable()
    {
        if (_enemy != null) _enemy.OnDied -= OnDied;
    }

    void OnDied(Enemy dead)
    {
        if (eggPrefab == null) return;

        Vector3 deathPos    = dead.transform.position;
        int     waypointIdx = dead.CurrentWaypointIndex;

        Enemy egg;
        if (WaveManager.Instance != null)
        {
            egg = WaveManager.Instance.SpawnEnemyAt(eggPrefab, deathPos, waypointIdx);
        }
        else
        {
            var go = Instantiate(eggPrefab, deathPos, Quaternion.identity);
            egg = go.GetComponent<Enemy>();
        }

        if (egg != null)
        {
            egg.isStationary      = true;   // Start() előtt állítjuk be → nem teleportál waypontra
            egg.dealsCastleDamage = false;  // tojás nem sebzi a kastélyt

            // Ha aktív boss event van, a tojás is a láncba kerül → zene tovább szól
            if (BossEventManager.Instance != null && BossEventManager.Instance.IsActive)
                BossEventManager.Instance.RegisterChainEntity(egg);
        }
    }
}
