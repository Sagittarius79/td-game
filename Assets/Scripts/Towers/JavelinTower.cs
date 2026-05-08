using UnityEngine;

/// <summary>
/// Dárda torony – Javelin_Tower skill tree támogatással.
/// Támogatott skill hatások:
///   JavelinDamageBonus  – sebzés növelés szintenként
///   JavelinAttackSpeed  – támadási sebesség növelés szintenként
///   JavelinCritChance   – kritikus találat esély szintenként
///   JavelinRange        – hatótávolság növelés szintenként
///   JavelinPierceCount  – áthatoló lövedék extra célpontok száma szintenként
/// </summary>
public class JavelinTower : Tower
{
    [Header("Skill fa")]
    [Tooltip("A Javelin_Tower SkillTreeDefinition ScriptableObject")]
    public SkillTreeDefinition javelinSkillTree;

    protected override float GetEffectiveAttackSpeed()
    {
        if (javelinSkillTree == null || UserProgressManager.Instance == null) return attackSpeed;
        float bonus        = UserProgressManager.Instance.GetTotalSkillEffect(SkillEffectType.JavelinAttackSpeed,  javelinSkillTree);
        float chargeBonus  = UserProgressManager.Instance.GetTotalSkillEffect(SkillEffectType.JavelinChargeSpeed,  javelinSkillTree);
        return attackSpeed + bonus + chargeBonus;
    }

    protected override float GetEffectiveDamage()
    {
        if (javelinSkillTree == null || UserProgressManager.Instance == null) return damage;
        float bonus = UserProgressManager.Instance.GetTotalSkillEffect(SkillEffectType.JavelinDamageBonus, javelinSkillTree);
        return damage + bonus;
    }

    protected override float GetEffectiveRange()
    {
        if (javelinSkillTree == null || UserProgressManager.Instance == null) return attackRange;
        float bonus = UserProgressManager.Instance.GetTotalSkillEffect(SkillEffectType.JavelinRange, javelinSkillTree);
        return attackRange + bonus;
    }

    protected override void Shoot()
    {
        if (projectilePrefab == null || currentTarget == null) return;

        float critChance  = 0f;
        float pierceBonus = 0f;

        if (javelinSkillTree != null && UserProgressManager.Instance != null)
        {
            critChance  = UserProgressManager.Instance.GetTotalSkillEffect(SkillEffectType.JavelinCritChance,  javelinSkillTree) / 100f;
            pierceBonus = UserProgressManager.Instance.GetTotalSkillEffect(SkillEffectType.JavelinPierceCount, javelinSkillTree);
        }

        var origin = shootPoint != null ? shootPoint.position : transform.position;
        var go     = Instantiate(projectilePrefab, origin, Quaternion.identity);
        var p      = go.GetComponent<Projectile>();
        if (p == null) return;

        p.Initialize(currentTarget, GetEffectiveDamage(), isAreaDamage, areaRadius, damageType);

        if (p.javelinCanCrit && critChance > 0f)
            p.javelinActiveCritChance = critChance;

        if (pierceBonus > 0)
            p.pierceCount += Mathf.RoundToInt(pierceBonus);
    }
}
