using UnityEngine;

/// <summary>
/// Turul torony – alap Tower funkcionalitással, skill fa támogatással.
/// Különleges képességek később kerülnek bele.
///
/// Unity beállítás:
///   1. Húzd rá ezt a scriptet a Turul Tower prefabra
///   2. Állítsd be az Inspector mezőket (Tower alap értékek)
///   3. Skill fa: kösd be a TurulSkillTree ScriptableObject-et ha van
/// </summary>
public class TurulTower : Tower
{
    [Header("Skill fa")]
    [Tooltip("A Turul torony skill tree ScriptableObject")]
    public SkillTreeDefinition turulSkillTree;

    [Header("Lövedék limit")]
    [Tooltip("Egyszerre maximum ennyi lövedéke lehet a toronynak (0 = korlátlan)")]
    [Min(0)]
    public int maxActiveProjectiles = 1;

    private readonly System.Collections.Generic.List<GameObject> _activeProjectiles
        = new System.Collections.Generic.List<GameObject>();

    protected override void Shoot()
    {
        if (projectilePrefab == null || currentTarget == null) return;

        // Lejárt/megsemmisült lövedékek eltávolítása a listából
        _activeProjectiles.RemoveAll(p => p == null);

        // Maximum: prefab alap + skill bónusz
        int effectiveMax = maxActiveProjectiles;
        if (turulSkillTree != null && UserProgressManager.Instance != null)
        {
            int countBonus = Mathf.RoundToInt(UserProgressManager.Instance.GetTotalSkillEffect(
                SkillEffectType.TurulCount, turulSkillTree));
            effectiveMax += countBonus;
        }

        // Charm: TurulExtraBirdChance – X% eséllyel +1 madár, ha a prefab jogosult
        var prefabProj = projectilePrefab.GetComponent<Projectile>();
        if (prefabProj != null && prefabProj.eligibleForTurulExtraBirdCharm
            && CharmEffects.RollPercentChance(CharmEffectType.TurulExtraBirdChance))
        {
            effectiveMax += 1;
            CharmEffects.PlayEffectSound(CharmEffectType.TurulExtraBirdChance);
        }

        if (effectiveMax > 0 && _activeProjectiles.Count >= effectiveMax) return;

        Vector3 origin = shootPoint != null ? shootPoint.position : transform.position;
        var go = Instantiate(projectilePrefab, origin, Quaternion.identity);
        var p  = go.GetComponent<Projectile>();
        if (p == null) { Destroy(go); return; }

        // Skill bónuszok INITIALIZE előtt – mert Initialize() másolja be a mezőket a belső állapotba
        if (turulSkillTree != null && UserProgressManager.Instance != null)
        {
            float dmgBoost = UserProgressManager.Instance.GetTotalSkillEffect(
                SkillEffectType.TurulDmgBoost, turulSkillTree);
            p.repeatDamageIncrement += dmgBoost;

            int outRangeBonus = Mathf.RoundToInt(UserProgressManager.Instance.GetTotalSkillEffect(
                SkillEffectType.TurulRepeatOutRange, turulSkillTree));
            p.repeatOutOfRangeHits += outRangeBonus;

            float speedBonus = UserProgressManager.Instance.GetTotalSkillEffect(
                SkillEffectType.TurulRepeatSpeed, turulSkillTree);
            p.repeatArcWidth = Mathf.Max(0.1f, p.repeatArcWidth - speedBonus);
        }

        p.Initialize(currentTarget, GetEffectiveDamage(), isAreaDamage, areaRadius, damageType);
        p.towerPosition = transform.position;
        p.towerRange    = GetEffectiveRange();

        var af = go.GetComponent<altalanos_fejlesztesek>();
        if (af != null)
            af.SetTowerData(transform.position, GetEffectiveRange(), this);

        _activeProjectiles.Add(go);
    }
}
