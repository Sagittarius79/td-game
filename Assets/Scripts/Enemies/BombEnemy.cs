using System.Collections;
using UnityEngine;

/// <summary>
/// Bomba ellenség – halálkor felrobban, AOE sebzést okoz a közeli tornyoknak.
/// Rakd ezt a komponenst a bomba szörny prefabjára az Enemy mellé.
/// </summary>
public class BombEnemy : Enemy
{
    // A robbanás mezők (explosionRadius, explosionDamage, explosionDamageType,
    // explosionEffect) az Enemy szülőosztályból öröklődnek – itt nem kell újra definiálni.

    protected override void Die()
    {
        if (isDead) return;
        isDead = true;

        OnDied?.Invoke(this);

        StartCoroutine(ExplodeAndDestroy());
    }

    IEnumerator ExplodeAndDestroy()
    {
        // Mozgás leállítása
        var animator = GetComponent<Animator>();

        // Animáció lejátszása
        if (animator != null && !string.IsNullOrEmpty(explosionTrigger))
        {
            animator.SetTrigger(explosionTrigger);

            // Megvárjuk amíg az explode state elindul
            yield return null;
            yield return null;

            // Megvárjuk amíg végigfut
            var info = animator.GetCurrentAnimatorStateInfo(0);
            while (info.normalizedTime < 1f)
            {
                yield return null;
                info = animator.GetCurrentAnimatorStateInfo(0);
            }
        }

        // Robbanás effekt
        if (explosionEffect != null)
            Instantiate(explosionEffect, transform.position, Quaternion.identity);

        // AOE sebzés a közeli tornyoknak
        foreach (var tower in Tower.AllTowers)
        {
            if (tower == null) continue;
            float dist = Vector3.Distance(transform.position, tower.transform.position);
            if (dist <= explosionRadius)
                tower.TakeDamage(explosionDamage, explosionDamageType);
        }

        Destroy(gameObject);
    }
}
