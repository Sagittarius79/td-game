using UnityEngine;

/// <summary>
/// Prisma torony – Prisma_Tower skill tree támogatással.
/// Támogatott skill hatások:
///   PrismaDamageBonus – sebzés növelés szintenként
///   PrismaRange       – hatótávolság növelés szintenként
/// </summary>
public class PrismaTower : Tower
{
    [Header("Skill fa")]
    [Tooltip("A Prisma_Tower SkillTreeDefinition ScriptableObject")]
    public SkillTreeDefinition prismaSkillTree;

    private int _shotCounter = 0;

    protected override void Shoot()
    {
        if (projectilePrefab == null || currentTarget == null) return;

        _shotCounter++;

        var origin = shootPoint != null ? shootPoint.position : transform.position;
        var go     = Instantiate(projectilePrefab, origin, Quaternion.identity);
        var p      = go.GetComponent<Projectile>();
        if (p == null) return;

        p.Initialize(currentTarget, GetEffectiveDamage(), isAreaDamage, areaRadius, damageType);

        // Bounce: skill pont, range és max bounce szám átadása
        if (p.prismaBounce && prismaSkillTree != null && UserProgressManager.Instance != null)
        {
            float bounceBonus = UserProgressManager.Instance.GetTotalSkillEffect(
                SkillEffectType.PrismaBounce, prismaSkillTree);
            float bounceCount = UserProgressManager.Instance.GetTotalSkillEffect(
                SkillEffectType.PrismaBounceCount, prismaSkillTree);

            p.prismaBounceSkillBonus = bounceBonus;
            p.prismaBounceRange      = GetEffectiveRange() *
                (GridManager.Instance != null
                    ? (GridManager.Instance.tileWidth + GridManager.Instance.tileHeight) * 0.5f
                    : 1f);
            p.prismaBouncesLeft = p.prismaMaxBounces + Mathf.RoundToInt(bounceCount);
        }

        // Element Changer: csak ha legalább 1 pont van kiosztva, és n-edik lövésnél aktiválódik
        if (p.prismaElementChanger && prismaSkillTree != null && UserProgressManager.Instance != null)
        {
            float skillBonus = UserProgressManager.Instance.GetTotalSkillEffect(
                SkillEffectType.PrismaElementChanger, prismaSkillTree);

            if (skillBonus >= 1f)
            {
                int interval = Mathf.Max(1, p.prismaElementBaseInterval - Mathf.RoundToInt(skillBonus));

                if (_shotCounter >= interval)
                {
                    p.prismaElementActive = true;
                    _shotCounter = 0;
                }
            }
        }
    }

    protected override float GetEffectiveDamage()
    {
        if (prismaSkillTree == null || UserProgressManager.Instance == null) return damage;
        var prefabProj = projectilePrefab != null ? projectilePrefab.GetComponent<Projectile>() : null;
        if (prefabProj == null || !prefabProj.prismaDmgBonus) return damage;
        float bonus = UserProgressManager.Instance.GetTotalSkillEffect(SkillEffectType.PrismaDamageBonus, prismaSkillTree);
        return damage + bonus;
    }

    protected override float GetEffectiveRange()
    {
        if (prismaSkillTree == null || UserProgressManager.Instance == null) return attackRange;
        var prefabProj = projectilePrefab != null ? projectilePrefab.GetComponent<Projectile>() : null;
        if (prefabProj == null || !prefabProj.prismaRangeBonus) return attackRange;
        float bonus = UserProgressManager.Instance.GetTotalSkillEffect(SkillEffectType.PrismaRange, prismaSkillTree);
        return attackRange + bonus;
    }
}
