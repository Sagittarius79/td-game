using UnityEngine;

/// <summary>
/// Méreg torony – a Buildings_lvl1 skill fából olvassa a PoisonTowerDmg bónuszt
/// és alkalmazza a lövedék poisonDamagePerSecond értékére lövéskor.
/// </summary>
public class PoisonTower : Tower
{
    [Header("Skill fa")]
    [Tooltip("A Buildings skill tree ScriptableObject (Buildings_lvl1)")]
    public SkillTreeDefinition poisonSkillTree;

    protected override void Shoot()
    {
        if (projectilePrefab == null || currentTarget == null) return;

        var origin = shootPoint != null ? shootPoint.position : transform.position;
        var go     = Instantiate(projectilePrefab, origin, Quaternion.identity);
        var p      = go.GetComponent<Projectile>();
        if (p == null) return;

        p.Initialize(currentTarget, GetEffectiveDamage(), isAreaDamage, areaRadius, damageType);

        if (p.applyPoison && poisonSkillTree != null && UserProgressManager.Instance != null)
        {
            float dmgBonus      = UserProgressManager.Instance.GetTotalSkillEffect(SkillEffectType.PoisonTowerDmg,      poisonSkillTree);
            float tickBonus     = UserProgressManager.Instance.GetTotalSkillEffect(SkillEffectType.PoisonTowerTick,     poisonSkillTree);
            float durationBonus = UserProgressManager.Instance.GetTotalSkillEffect(SkillEffectType.PoisonTowerDuration, poisonSkillTree);
            p.poisonDamagePerSecond += dmgBonus;
            p.maxPoisonStacks       += Mathf.RoundToInt(tickBonus);
            p.poisonDuration        += durationBonus;
        }
    }
}
