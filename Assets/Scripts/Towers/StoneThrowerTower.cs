using UnityEngine;

/// <summary>
/// Kőhajító torony – Stone skill tree támogatással.
/// Fókusz mechanika: ugyanarra a célpontra lőve lövésenként gyorsul,
/// célpontváltáskor visszaáll az alap sebességre.
/// </summary>
public class StoneThrowerTower : Tower
{
    [Header("Skill fa")]
    [Tooltip("A StoneSkillTree ScriptableObject")]
    public SkillTreeDefinition stoneSkillTree;

    [Header("Tűzgolyó")]
    [Tooltip("Tűzgolyó lövedék prefab")]
    public GameObject fireballPrefab;
    [Tooltip("A tűzgolyó által okozott közvetlen találat sebzéstípusa")]
    public DamageType fireballDamageType = DamageType.Fire;
    [Tooltip("Tűzsebzés másodpercenként az égő ellenségen")]
    public float burnDamagePerSecond = 5f;
    [Tooltip("Égés időtartama másodpercben")]
    public float burnDuration = 5f;
    [Tooltip("Ha be van kapcsolva, a becsapódás körüli ellenségekre is kiterjed az égés")]
    public bool burnIsAOE = false;
    [Tooltip("Égés AOE sugara tile egységben (csak ha burnIsAOE be van kapcsolva)")]
    public float burnAOERadius = 1.5f;
    [Tooltip("Opcionális tűz VFX prefab – megjelenik az ellenségen égés közben")]
    public GameObject burnVfxPrefab;

    // ── Fókusz állapot ────────────────────────────────────────────
    private Enemy _focusTarget  = null;
    private float _focusBonus   = 0f;
    private float _focusTimer   = 0f;

    // ── Tűzgolyó számláló ─────────────────────────────────────────
    private int _shotCounter = 0;

    void Awake()
    {
        if (stoneSkillTree != null && UserProgressManager.Instance != null)
        {
            float armorBonus = UserProgressManager.Instance.GetTotalSkillEffect(SkillEffectType.TowerArmor, stoneSkillTree);
            armor += armorBonus;
        }
    }

    protected override float GetChargeDamageBonus()
    {
        if (stoneSkillTree == null || UserProgressManager.Instance == null) return 0f;
        var prefabProj = projectilePrefab != null ? projectilePrefab.GetComponent<Projectile>() : null;
        if (prefabProj == null || !prefabProj.stoneChargeDmgBonus) return 0f;
        float perSecond = UserProgressManager.Instance.GetTotalSkillEffect(SkillEffectType.ChargeDamageBonus, stoneSkillTree);
        return _idleTimer * perSecond;
    }

    protected override float GetEffectiveAttackSpeed()
    {
        if (stoneSkillTree == null || UserProgressManager.Instance == null)
            return attackSpeed;

        float asBonus = UserProgressManager.Instance.GetTotalSkillEffect(SkillEffectType.StoneAttackSpeed, stoneSkillTree);

        // Fókusz időmérő frissítése
        float focusPerSec = UserProgressManager.Instance.GetTotalSkillEffect(SkillEffectType.FocusAttackSpeed, stoneSkillTree);
        if (focusPerSec > 0f)
        {
            var prefabProj = projectilePrefab != null ? projectilePrefab.GetComponent<Projectile>() : null;
            bool focusEnabled = prefabProj != null && prefabProj.stoneFocusBonus;

            if (focusEnabled && currentTarget != null && !currentTarget.IsDead)
            {
                if (currentTarget != _focusTarget)
                {
                    _focusTarget = currentTarget;
                    _focusTimer  = 0f;
                    _focusBonus  = 0f;
                }
                else
                {
                    _focusTimer += Time.deltaTime;
                    _focusBonus  = _focusTimer * focusPerSec;
                }
            }
            else if (currentTarget == null || currentTarget.IsDead)
            {
                _focusTarget = null;
                _focusTimer  = 0f;
                _focusBonus  = 0f;
            }
        }

        return attackSpeed + asBonus + _focusBonus;
    }

    protected override void Shoot()
    {
        if (projectilePrefab == null || currentTarget == null) return;

        // ── Skill bónuszok ─────────────────────────────────────────
        float aoeBonus      = 0f;
        float damageBonus   = 0f;
        float aoeDmgBonus   = 0f;
        float magicDmgBonus = 0f;
        float critChance    = 0f;

        var prefabProj = projectilePrefab.GetComponent<Projectile>();

        if (stoneSkillTree != null && UserProgressManager.Instance != null)
        {
            if (prefabProj != null && prefabProj.stoneAOEBonus)
                aoeBonus = UserProgressManager.Instance.GetTotalSkillEffect(SkillEffectType.AOERadius, stoneSkillTree);

            if (prefabProj != null && prefabProj.stoneDmgBonus)
                damageBonus = UserProgressManager.Instance.GetTotalSkillEffect(SkillEffectType.StoneDamageBonus, stoneSkillTree);

            aoeDmgBonus = UserProgressManager.Instance.GetTotalSkillEffect(SkillEffectType.AOEDamageBonus, stoneSkillTree);

            if (prefabProj != null && prefabProj.stoneMagicAOEDmgBonus)
                magicDmgBonus = UserProgressManager.Instance.GetTotalSkillEffect(SkillEffectType.MagicDamageBonus, stoneSkillTree);

            if (prefabProj != null && prefabProj.stoneCanCrit)
                critChance = UserProgressManager.Instance.GetTotalSkillEffect(SkillEffectType.StoneCritChance, stoneSkillTree) / 100f;


        }

        float effectiveAOERadius = areaRadius + aoeBonus;
        if (CharacterClassBonus.Is(CharacterClass.StoneThrower))
            effectiveAOERadius *= CharacterClassBonus.Config?.aoeRadiusMultiplier ?? 1.2f;
        float effectiveDamage    = damage + damageBonus + GetChargeDamageBonus();
        float effectiveAoeDamage = aoeDamage + aoeDmgBonus;

        Vector3 origin = shootPoint != null ? shootPoint.position : transform.position;

        // ── Tűzgolyó kilövése az N-edik lövésnél ──────────────────
        _shotCounter++;
        int fireballInterval = GetFireballInterval();
        if (fireballPrefab != null && fireballInterval > 0 && _shotCounter >= fireballInterval)
        {
            _shotCounter = 0;
            var fproj = Instantiate(fireballPrefab, origin, Quaternion.identity);
            var fp    = fproj.GetComponent<Projectile>();
            if (fp != null)
            {
                fp.applyBurning        = true;
                fp.burnDamagePerSecond = burnDamagePerSecond;
                fp.burnDuration        = burnDuration;
                fp.burnIsAOE           = burnIsAOE;
                fp.burnAOERadius       = burnAOERadius;
                fp.burnVfxPrefab       = burnVfxPrefab;
                fp.Initialize(currentTarget, effectiveDamage, false, 0f, fireballDamageType);
            }
            return;
        }

        // ── Sima kőlövedék ─────────────────────────────────────────
        var proj = Instantiate(projectilePrefab, origin, Quaternion.identity);
        var p    = proj.GetComponent<Projectile>();
        if (p != null)
            p.Initialize(currentTarget, effectiveDamage, isAreaDamage, effectiveAOERadius,
                         damageType, critChance, aoeDamage: effectiveAoeDamage, magicDamage: magicDmgBonus);
    }

    /// <summary>
    /// Hány sima lövés után süssön ki a tűzgolyó.
    /// Csak akkor aktív, ha legalább 1 skill pont van befektetve.
    /// Alap: 40. Skill pontonként 1-gyel csökken, minimum 1.
    /// Ha nincs pont → -1 visszatérési érték (kikapcsolt állapot).
    /// </summary>
    private int GetFireballInterval()
    {
        if (stoneSkillTree == null || UserProgressManager.Instance == null)
            return -1;
        float reduction = UserProgressManager.Instance.GetTotalSkillEffect(
            SkillEffectType.FireBallInterval, stoneSkillTree);
        if (reduction <= 0f) return -1;
        return Mathf.Max(1, 40 - Mathf.RoundToInt(reduction));
    }
}
