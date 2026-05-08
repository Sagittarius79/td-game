using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Méreg DoT stack rendszer – minden lövedék-találat egy új stack-et ad.
/// Minden stack 1 mp-enként sebez (damagePerSecond értékkel).
/// Ha elérjük a maxStacks határt, a legrégebbi (legkisebb maradék idejű) stack
/// időzítője resetelődik az új találatkor.
/// </summary>
public class PoisonEffect : MonoBehaviour
{
    // ── Belső állapot ─────────────────────────────────────────────
    private Enemy         _enemy;
    private float         _damagePerSecond;
    private float         _stackDuration;
    private int           _maxStacks;
    private List<float>   _stacks = new List<float>();  // minden elem: az adott stack maradék ideje
    private float         _tickAccum;
    private GameObject    _activeVfx;

    private const float TICK_INTERVAL = 1f;

    // ── Inicializálás ─────────────────────────────────────────────

    /// <summary>Projectile.Impact() hívja közvetlenül a komponens hozzáadása után.</summary>
    public void Initialize(float damagePerSecond, float stackDuration, int maxStacks, GameObject vfxPrefab = null)
    {
        _enemy           = GetComponent<Enemy>();
        _damagePerSecond = damagePerSecond;
        _stackDuration   = stackDuration;
        _maxStacks       = Mathf.Max(1, maxStacks);
        _tickAccum       = 0f;

        _stacks.Clear();
        _stacks.Add(_stackDuration);

        if (vfxPrefab != null)
        {
            _activeVfx = Instantiate(vfxPrefab, transform.position, Quaternion.identity, transform);

            var sr = GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                float spriteHeight = sr.bounds.size.y;
                float spriteWidth  = sr.bounds.size.x;

                Vector3 offset = _enemy != null ? _enemy.poisonVfxOffset : Vector3.zero;
                float   scale  = _enemy != null ? _enemy.poisonVfxScale  : 1f;

                _activeVfx.transform.localPosition = new Vector3(
                    offset.x,
                    spriteHeight * 0.5f + offset.y,
                    offset.z);

                float finalScale = spriteWidth * scale;
                _activeVfx.transform.localScale = new Vector3(finalScale, finalScale, finalScale);
            }
        }
    }

    /// <summary>
    /// Új találat érkezett. Ha van szabad stack hely, újat ad hozzá.
    /// Ha tele van, a legrégebbi (legkisebb maradék idejű) stack időzítője resetelődik.
    /// </summary>
    public void AddStack(float damagePerSecond, float stackDuration, int maxStacks)
    {
        _damagePerSecond = damagePerSecond;
        _stackDuration   = stackDuration;
        _maxStacks       = Mathf.Max(1, maxStacks);

        if (_stacks.Count < _maxStacks)
        {
            _stacks.Add(_stackDuration);
        }
        else
        {
            // legrégebbi = legkisebb maradék idő → reseteljük
            int oldestIdx = 0;
            for (int i = 1; i < _stacks.Count; i++)
                if (_stacks[i] < _stacks[oldestIdx]) oldestIdx = i;
            _stacks[oldestIdx] = _stackDuration;
        }
    }

    // ── Update ────────────────────────────────────────────────────

    void Update()
    {
        if (_enemy == null || _enemy.IsDead)
        {
            Cleanup();
            return;
        }

        // Minden stack öregedése
        for (int i = _stacks.Count - 1; i >= 0; i--)
        {
            _stacks[i] -= Time.deltaTime;
            if (_stacks[i] <= 0f)
                _stacks.RemoveAt(i);
        }

        if (_stacks.Count == 0)
        {
            Cleanup();
            return;
        }

        // Tick: minden élő stack sebez
        _tickAccum += Time.deltaTime;
        if (_tickAccum >= TICK_INTERVAL)
        {
            _tickAccum -= TICK_INTERVAL;
            _enemy.TakeDamage(_stacks.Count * _damagePerSecond, DamageType.Poison);
        }
    }

    // ── Segéd ─────────────────────────────────────────────────────

    public int StackCount => _stacks.Count;

    private void Cleanup()
    {
        if (_activeVfx != null)
            Destroy(_activeVfx);
        Destroy(this);
    }
}
