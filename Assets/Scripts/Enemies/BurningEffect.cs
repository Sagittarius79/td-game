using UnityEngine;

/// <summary>
/// Égés DoT effekt – lövedék találatkor kerül az ellenségre.
/// Tűzsebzést okoz 0.5 mp-enkénti tickekben a megadott ideig.
/// Ha az ellenség újra megkapja, az időzítő resetelődik (frissítés).
///
/// Használat: Projectile.Impact() AddComponent-tel rakja az ellenségre.
/// </summary>
public class BurningEffect : MonoBehaviour
{
    [Header("Vizuális (opcionális)")]
    [Tooltip("Tűz partikula prefab – spawnolódik az ellenségen égés közben, elpusztul mikor az égés véget ér")]
    public GameObject fireVfxPrefab;

    // ── Belső állapot ─────────────────────────────────────────────
    private Enemy  _enemy;
    private float  _damagePerSecond;
    private float  _duration;
    private float  _elapsed;
    private float  _tickAccum;
    private GameObject _activeVfx;

    private const float TICK_INTERVAL = 0.5f;   // sebzés 0.5 mp-enként

    // ── Inicializálás ─────────────────────────────────────────────

    /// <summary>Projectile.Impact() hívja közvetlenül a komponens hozzáadása után.</summary>
    public void Initialize(float damagePerSecond, float duration, GameObject vfxPrefab = null)
    {
        _enemy           = GetComponent<Enemy>();
        _damagePerSecond = damagePerSecond;
        _duration        = duration;
        _elapsed         = 0f;
        _tickAccum       = 0f;

        if (vfxPrefab != null)
            _activeVfx = Instantiate(vfxPrefab, transform.position, Quaternion.identity, transform);
    }

    /// <summary>Ha az ellenség újra kap tűzsebzést, az időzítő resetelődik.</summary>
    public void Refresh(float damagePerSecond, float duration)
    {
        _damagePerSecond = damagePerSecond;
        _duration        = duration;
        _elapsed         = 0f;
    }

    // ── Update ────────────────────────────────────────────────────

    void Update()
    {
        if (_enemy == null || _enemy.IsDead)
        {
            Cleanup();
            return;
        }

        _elapsed   += Time.deltaTime;
        _tickAccum += Time.deltaTime;

        if (_tickAccum >= TICK_INTERVAL)
        {
            _tickAccum -= TICK_INTERVAL;
            _enemy.TakeDamage(_damagePerSecond * TICK_INTERVAL, DamageType.Fire);
        }

        if (_elapsed >= _duration)
            Cleanup();
    }

    // ── Cleanup ───────────────────────────────────────────────────

    private void Cleanup()
    {
        if (_activeVfx != null)
            Destroy(_activeVfx);
        Destroy(this);
    }
}
