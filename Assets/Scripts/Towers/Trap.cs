using System.Collections;
using UnityEngine;

/// <summary>
/// Csapda – a nyíl becsapódási helyén marad, és az első rálépő ellenséget megsebesíti.
/// Helyezd a csapda prefabra. A prefabnak kell egy Collider2D (Is Trigger = true)
/// és egy Animator két state-tel: "Idle" és "Bite".
/// </summary>
public class Trap : MonoBehaviour
{
    [Header("Élettartam")]
    [Tooltip("Ennyi másodperc után eltűnik, ha senki sem lép rá")]
    public float lifetime = 8f;

    [Header("Aktiválási késleltetés")]
    [Tooltip("Ennyi másodperc után aktiválódik a csapda (ne triggerelődjön azonnal a szörnyre születéskor)")]
    public float activationDelay = 1f;

    [Header("Animáció")]
    [Tooltip("Az Animator Trigger neve az idle → bite váltáshoz")]
    public string biteTrigger = "bite";

    // Belső állapot – Projectile tölti fel
    private float      _damage;
    private DamageType _damageType;
    private Animator   _animator;
    private bool       _triggered = false;
    private bool       _active    = false;
    private Enemy      _ignoreEnemy = null;

    void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    /// <summary>Projectile hívja közvetlenül spawnolás után.</summary>
    public void Initialize(float damage, DamageType damageType, Enemy sourceEnemy = null)
    {
        _damage      = damage;
        _damageType  = damageType;
        _ignoreEnemy = sourceEnemy;

        Destroy(gameObject, lifetime);
        Invoke(nameof(Activate), activationDelay);
    }

    void Activate()
    {
        _active = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!_active) return;
        if (_triggered) return;

        if (!other.CompareTag("Enemy")) return;

        var enemy = other.GetComponent<Enemy>();
        if (enemy == null || enemy.IsDead) return;
        if (enemy == _ignoreEnemy) return;

        _triggered = true;
        SetCollidersEnabled(false);

        enemy.TakeDamage(_damage, _damageType);

        StartCoroutine(PlayBiteAndDestroy());
    }

    IEnumerator PlayBiteAndDestroy()
    {
        if (_animator != null && !string.IsNullOrEmpty(biteTrigger))
        {
            _animator.SetTrigger(biteTrigger);

            float elapsed = 0f;
            bool biteStarted = false;

            while (elapsed < 5f)
            {
                yield return null;
                elapsed += Time.deltaTime;

                var info = _animator.GetCurrentAnimatorStateInfo(0);

                // Várjuk meg hogy a bite state elinduljon
                if (!biteStarted && info.IsName(biteTrigger))
                    biteStarted = true;

                // Ha elindult és végigfutott → kilépünk
                if (biteStarted && info.normalizedTime >= 1f)
                    break;
            }
        }

        Destroy(gameObject);
    }

    void SetCollidersEnabled(bool enabled)
    {
        foreach (var col in GetComponentsInChildren<Collider2D>())
            col.enabled = enabled;
    }
}
