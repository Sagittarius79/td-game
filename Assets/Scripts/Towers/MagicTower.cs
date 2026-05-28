using UnityEngine;

/// <summary>
/// Varázs torony – értékek az Inspectorban állíthatók
/// </summary>
public class MagicTower : Tower
{
    [Tooltip("A MagicSkillTree ScriptableObject")]
    public SkillTreeDefinition magicSkillTree;

    // Az alap Tower.Shoot() mindent kezel (damageType, teleportOnHit, stb.)
    // A damageType-ot az Inspectorban kell Magic-re állítani a prefabon.

    protected override float GetEffectiveRange()
    {
        float r = attackRange;
        if (CharacterClassBonus.Is(CharacterClass.Mage))
            r *= CharacterClassBonus.Config?.rangeMultiplier ?? 1.1f;
        return r;
    }

    protected override float GetEffectiveAttackSpeed()
    {
        if (magicSkillTree == null || UserProgressManager.Instance == null) return attackSpeed;
        return attackSpeed + UserProgressManager.Instance.GetTotalSkillEffect(SkillEffectType.MagicAttackSpeed, magicSkillTree);
    }

    protected override float GetEffectiveDamage()
    {
        if (magicSkillTree == null || UserProgressManager.Instance == null) return damage;
        return damage + UserProgressManager.Instance.GetTotalSkillEffect(SkillEffectType.MagicDmgBonus, magicSkillTree);
    }

    protected override void Shoot()
    {
        if (projectilePrefab == null || currentTarget == null) return;

        var origin = shootPoint != null ? shootPoint.position : transform.position;
        var go     = Instantiate(projectilePrefab, origin, Quaternion.identity);
        var p      = go.GetComponent<Projectile>();
        if (p == null) return;

        p.Initialize(currentTarget, GetEffectiveDamage(), isAreaDamage, areaRadius, damageType);

        // Pull csak akkor aktív, ha legalább 1 skill pont van a MagicPullRadius node-on
        if (p.pullOnHit)
        {
            float bonus = (magicSkillTree != null && UserProgressManager.Instance != null)
                ? UserProgressManager.Instance.GetTotalSkillEffect(SkillEffectType.MagicPullRadius, magicSkillTree)
                : 0f;

            if (bonus <= 0f)
                p.pullOnHit = false;   // nincs pont → pull kikapcsol, pullDuration nem fut le
            else
                p.pullRadius += bonus;
        }
    }
}
