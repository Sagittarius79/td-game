using System.Collections;
using UnityEngine;

/// <summary>
/// A tojás keltetés logikája – a PhoenixRebirth spawnolta tojáson fut.
///
/// Működés:
///   - hatchTime másodpercig életben kell maradnia a tojásnak
///   - Ha lelövik → nincs keltetés, egyszerűen meghal
///   - Ha túléli a visszaszámlálást → phoenixPrefab spawnol a tojás helyén,
///     a tojás eltűnik (Death effekt opcionálisan)
///
/// Unity beállítás:
///   1. Húzd rá a tojás prefabra
///   2. Állítsd be: Hatch Time, Phoenix Prefab
///   3. Opcionális: Hatch Effect prefab (pl. lángok, fény)
/// </summary>
[RequireComponent(typeof(Enemy))]
public class PhoenixEgg : MonoBehaviour
{
    [Header("Keltetés")]
    [Tooltip("Ennyi másodperc után kel ki, ha nem lövik le")]
    public float hatchTime = 20f;

    [Tooltip("Ez a prefab spawol keltetéskor (a Phoenix)")]
    public GameObject phoenixPrefab;

    [Tooltip("Ha true, a kikelt Phoenix is beleszámít a kill statisztikába")]
    public bool rebirthCountsAsKill = true;

    [Header("Vizuális (opcionális)")]
    [Tooltip("Keltetési effekt prefab (pl. lángok) – ha üres, nincs")]
    public GameObject hatchEffect;

    // ── Belső állapot ─────────────────────────────────────────
    private Enemy _enemy;
    private bool  _shotDown = false;

    // ── Lifecycle ─────────────────────────────────────────────

    void Awake() => _enemy = GetComponent<Enemy>();

    void Start()
    {
        if (_enemy != null)
            _enemy.OnDied += _ => _shotDown = true;

        StartCoroutine(HatchRoutine());
    }

    // ── Keltetés coroutine ─────────────────────────────────────

    IEnumerator HatchRoutine()
    {
        yield return new WaitForSeconds(hatchTime);

        // Ha lelőtték a tojást, a coroutine már leállt (GO destroy) – de extra ellenőrzés:
        if (_shotDown || _enemy == null || _enemy.IsDead) yield break;
        if (phoenixPrefab == null)
        {
            Debug.LogWarning("[PhoenixEgg] phoenixPrefab nincs beállítva!");
            yield break;
        }

        Vector3 pos         = transform.position;
        int     waypointIdx = _enemy.overrideStartWaypointIndex > 0
                                  ? _enemy.overrideStartWaypointIndex
                                  : 1;

        // Keltetési effekt
        if (hatchEffect != null)
            Instantiate(hatchEffect, pos, Quaternion.identity);

        // Loop láng effekt leállítása MIELŐTT a tojást megszüntetjük
        // (különben a looping PhoenixHatchEffect árván maradhat)
        foreach (var fx in GetComponentsInChildren<PhoenixHatchEffect>())
            fx.ForceStop();

        // Phoenix spawnolása
        Enemy reborn = null;
        if (WaveManager.Instance != null)
        {
            reborn = WaveManager.Instance.SpawnEnemyAt(phoenixPrefab, pos, waypointIdx);
            if (reborn != null)
                reborn.countAsKill = rebirthCountsAsKill;

            // Tojást kivonjuk a hullám nyomkövetéséből (számlálót egyensúlyban tartjuk)
            WaveManager.Instance.UnregisterEnemy(_enemy);
        }
        else
        {
            var go = Instantiate(phoenixPrefab, pos, Quaternion.identity);
            reborn = go?.GetComponent<Enemy>();
        }

        // Boss event lánc-csere: tojás ki, újjászületett Phoenix be
        if (BossEventManager.Instance != null && BossEventManager.Instance.IsActive)
        {
            if (reborn != null)
                BossEventManager.Instance.RegisterChainEntity(reborn);
            BossEventManager.Instance.UnregisterChainEntity(_enemy);
        }

        // Tojás eltüntetése (animáció nélkül – a keltetési effekt a vizuál)
        Destroy(gameObject);
    }
}
