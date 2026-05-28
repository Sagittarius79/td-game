using UnityEngine;
using System.Collections;

/// <summary>
/// Ijjász torony – kritikus találat és multi-shot skill támogatással.
/// </summary>
public class ArcherTower : Tower
{
    [Header("Skill fa")]
    [Tooltip("Az ArcherSkillTree ScriptableObject")]
    public SkillTreeDefinition archerSkillTree;

    private bool _isDoubleShot = false;

    protected override float GetEffectiveAttackSpeed()
    {
        if (archerSkillTree == null || UserProgressManager.Instance == null) return attackSpeed;

        float bonus = UserProgressManager.Instance.GetTotalSkillEffect(SkillEffectType.AttackSpeed, archerSkillTree);
        return attackSpeed + bonus;
    }

    protected override float GetEffectiveRange()
    {
        if (archerSkillTree == null || UserProgressManager.Instance == null) return attackRange;

        float bonus = UserProgressManager.Instance.GetTotalSkillEffect(SkillEffectType.AttackRange, archerSkillTree);

        var prefabProj = projectilePrefab?.GetComponent<Projectile>();
        if (prefabProj != null && prefabProj.archerRangeBonus)
            bonus += UserProgressManager.Instance.GetTotalSkillEffect(SkillEffectType.ArcherRange, archerSkillTree);

        return attackRange + bonus;
    }

    protected override void Shoot()
    {
        if (projectilePrefab == null || currentTarget == null) return;

        float critChance      = 0f;
        float multiShotChance = 0f;
        float armorPierce     = 0f;
        float rangeBonus      = 0f;
        float stunChance      = 0f;
        float damageBonus     = 0f;
        float trapChance      = 0f;

        if (archerSkillTree != null && UserProgressManager.Instance != null)
        {
            critChance      = UserProgressManager.Instance.GetTotalSkillEffect(SkillEffectType.CritHitChance,   archerSkillTree) / 100f;
            multiShotChance = UserProgressManager.Instance.GetTotalSkillEffect(SkillEffectType.MultiShotChance, archerSkillTree) / 100f;
            armorPierce     = UserProgressManager.Instance.GetTotalSkillEffect(SkillEffectType.ArmorPierce,     archerSkillTree);
            rangeBonus      = UserProgressManager.Instance.GetTotalSkillEffect(SkillEffectType.ArcherRange,     archerSkillTree);
            stunChance      = UserProgressManager.Instance.GetTotalSkillEffect(SkillEffectType.StunChance,      archerSkillTree) / 100f;
            damageBonus     = UserProgressManager.Instance.GetTotalSkillEffect(SkillEffectType.DamageBonus,     archerSkillTree);
            trapChance      = UserProgressManager.Instance.GetTotalSkillEffect(SkillEffectType.TrapChance,      archerSkillTree) / 100f;
        }

        float effectiveRange  = attackRange + rangeBonus;
        float effectiveDamage = damage + damageBonus;

        Vector3 origin = shootPoint != null ? shootPoint.position : transform.position;

        // Multi-shot aktiválódott – a prefabban beállított számú lövedék indul
        if (multiShotChance > 0f && Random.value < multiShotChance)
        {
            var prefabProj = projectilePrefab.GetComponent<Projectile>();
            int shotCount  = (prefabProj != null && prefabProj.canMultiShot) ? prefabProj.multiShotCount : 1;

            float worldRange = effectiveRange * (GridManager.Instance != null
                ? (GridManager.Instance.tileWidth + GridManager.Instance.tileHeight) * 0.5f
                : 1f);

            int fired = 0;
            foreach (var enemy in Enemy.AllEnemies)
            {
                if (fired >= shotCount) break;
                if (enemy == null || enemy.IsDead) continue;
                if (Vector3.Distance(transform.position, enemy.transform.position) > worldRange) continue;

                SpawnProjectile(enemy, origin, effectiveDamage, critChance, armorPierce, stunChance, trapChance);
                fired++;
            }
        }
        else
        {
            SpawnProjectile(currentTarget, origin, effectiveDamage, critChance, armorPierce, stunChance, trapChance);
        }

        TryDoubleShot();
    }

    void SpawnProjectile(Enemy target, Vector3 origin, float dmg, float critChance, float armorPierce, float stunChance, float trapChance)
    {
        var proj = Instantiate(projectilePrefab, origin, Quaternion.identity);
        var p    = proj.GetComponent<Projectile>();
        if (p != null)
            p.Initialize(target, dmg, isAreaDamage, areaRadius, damageType,
                         p.canCrit ? critChance : 0f, armorPierce, stunChance, trapChance);
    }

    // ── Archer karakter bónusz: kettős lövés ─────────────────────────

    void TryDoubleShot()
    {
        if (_isDoubleShot) return;
        if (!CharacterClassBonus.Is(CharacterClass.Archer)) return;

        var cfg = CharacterClassBonus.Config;
        float chance = cfg?.doubleShotChance ?? 0.1f;
        if (Random.value < chance)
            StartCoroutine(DoubleShotCoroutine(cfg?.doubleShotDelay ?? 0.1f));
    }

    IEnumerator DoubleShotCoroutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (currentTarget != null && !currentTarget.IsDead)
        {
            _isDoubleShot = true;
            Shoot();
            _isDoubleShot = false;
        }
    }
}
