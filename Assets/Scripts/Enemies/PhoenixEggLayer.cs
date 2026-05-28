using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Menet közben időközönként baby tojást rak le a Phoenix.
/// A tojás stacionárius (helyben marad), tornyok célozhatják,
/// és 20 mp után baby Phoenix kel ki belőle (ha nem lövik le előbb).
///
/// Unity beállítás (felnőtt Phoenix prefabon):
///   1. Add hozzá ezt a komponenst
///   2. Baby Egg Prefab → a baby tojás prefab (PhoenixEgg komponenssel)
///   3. Lay Interval → hány másodpercenként rak tojást
///   4. Max Active Eggs → max egyidejű tojás (0 = korlátlan)
///
/// A baby tojás prefabon legyen:
///   - Enemy komponens (HP, sebzés fogadás)
///   - PhoenixEgg komponens (Hatch Time = 20, Phoenix Prefab = baby phoenix prefab)
///   - Move Speed = 0
/// </summary>
[RequireComponent(typeof(Enemy))]
public class PhoenixEggLayer : MonoBehaviour
{
    [Header("Tojásrakás")]
    [Tooltip("Baby tojás prefab – Enemy + PhoenixEgg komponenssel")]
    public GameObject babyEggPrefab;

    [Tooltip("Ennyi másodpercenként rak egy tojást")]
    [Min(1f)]
    public float layInterval = 8f;

    [Tooltip("Egyidejűleg max ennyi aktív tojás (0 = korlátlan)")]
    public int maxActiveEggs = 3;

    [Header("Első tojás késleltetése")]
    [Tooltip("Ennyi másodperccel a spawn után rakja le az első tojást")]
    public float firstLayDelay = 3f;

    [Header("Vizuális (opcionális)")]
    [Tooltip("Tojásrakáskor megjelenő effekt prefab (pl. kis fény burst)")]
    public GameObject layEffect;

    // ── Belső állapot ──────────────────────────────────────────

    private Enemy _enemy;

    /// <summary>
    /// Az aktív tojások listája.
    /// A RemoveAll(e == null || e.IsDead) kezeli:
    ///   - lelőtt tojás: IsDead == true
    ///   - kikelt tojás: GO Destroy → Unity null összehasonlítás igaz lesz
    /// </summary>
    private readonly List<Enemy> _activeEggs = new List<Enemy>();

    // ── Lifecycle ──────────────────────────────────────────────

    void Awake() => _enemy = GetComponent<Enemy>();

    void Start()
    {
        if (babyEggPrefab == null)
        {
            Debug.LogWarning("[PhoenixEggLayer] Baby Egg Prefab nincs beállítva!", this);
            return;
        }

        StartCoroutine(LayRoutine());
    }

    // ── Tojásrakás coroutine ───────────────────────────────────

    IEnumerator LayRoutine()
    {
        // Első tojás előtt kis késleltetés (ne rögtön a spawn pillanatában)
        if (firstLayDelay > 0f)
            yield return new WaitForSeconds(firstLayDelay);

        while (true)
        {
            if (_enemy == null || _enemy.IsDead) yield break;

            LayEgg();

            yield return new WaitForSeconds(layInterval);
        }
    }

    // ── Egy tojás lerakása ─────────────────────────────────────

    void LayEgg()
    {
        if (_enemy == null || _enemy.IsDead) return;

        // Lejárt tojások eltávolítása a listából
        _activeEggs.RemoveAll(e => e == null || e.IsDead);

        if (maxActiveEggs > 0 && _activeEggs.Count >= maxActiveEggs)
        {
            Debug.Log($"[PhoenixEggLayer] Max aktív tojás ({maxActiveEggs}) elérve, kihagyás.");
            return;
        }

        Vector3 pos         = transform.position;
        int     waypointIdx = _enemy.CurrentWaypointIndex;

        // Spawn
        Enemy egg;
        if (WaveManager.Instance != null)
            egg = WaveManager.Instance.SpawnEnemyAt(babyEggPrefab, pos, waypointIdx);
        else
        {
            var go = Instantiate(babyEggPrefab, pos, Quaternion.identity);
            egg = go?.GetComponent<Enemy>();
        }

        if (egg == null) return;

        // Stacionárius beállítás – Start() előtt, így nem teleportál waypontra
        egg.isStationary      = true;
        egg.dealsCastleDamage = false;

        _activeEggs.Add(egg);

        // Ha aktív boss event van, a baby tojás is a zenei láncba kerül
        if (BossEventManager.Instance != null && BossEventManager.Instance.IsActive)
            BossEventManager.Instance.RegisterChainEntity(egg);

        // Opcionális vizuális effekt
        if (layEffect != null)
            Instantiate(layEffect, pos, Quaternion.identity);

        Debug.Log($"[PhoenixEggLayer] Tojás lerakva @ {pos} | Aktív: {_activeEggs.Count}/{(maxActiveEggs > 0 ? maxActiveEggs.ToString() : "∞")}");
    }
}
