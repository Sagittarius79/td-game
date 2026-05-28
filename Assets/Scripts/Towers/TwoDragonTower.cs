using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Two Dragon Tower – 2 párhuzamos töltési sávval rendelkező torony.
/// Mindkét "sárkány" önállóan tölt és lő; a lövedék prefab altalanos_fejlesztesek
/// komponensén keresztül alkalmaz tűz DoT-ot vagy egyéb effekteket.
///
/// Unity beállítás:
///   1. Húzd rá ezt a scriptet a Two Dragon Tower prefabra
///   2. Kösd be a 2 db charge bar Image-et a chargeBarFills tömbben
///   3. A lövedék prefabon állítsd be az altalanos_fejlesztesek komponenst
///   4. Skill fa (opcionális): kösd be a TwoDragonSkillTree ScriptableObject-et
/// </summary>
public class TwoDragonTower : Tower
{
    [Header("Skill fa")]
    [Tooltip("A Two Dragon torony skill tree ScriptableObject (opcionális)")]
    public SkillTreeDefinition twoDragonSkillTree;

    [Header("Charge bar-ok")]
    [Tooltip("2 db töltési sáv Image (Filled típusú). A tömb hossza határozza meg a párhuzamos lövések számát.")]
    public Image[] chargeBarFills;

    [Tooltip("Minimális szünet (mp) két lövés között, ha egyszerre töltene fel mindkét sáv.")]
    public float minGapBetweenShots = 0.15f;

    [Tooltip("Kezdő töltöttség eltolás (0..1) – staggered indítás. Pl. {0, 0.5} → a 2. sárkány félúton kezd.")]
    public float[] initialChargeOffsets;

    private float[] _timers;
    private float   _lastShotTime = -999f;

    protected override float GetEffectiveRange()
    {
        float bonus = twoDragonSkillTree != null && UserProgressManager.Instance != null
            ? UserProgressManager.Instance.GetTotalSkillEffect(SkillEffectType.TwoDragonRange, twoDragonSkillTree)
            : 0f;
        return base.GetEffectiveRange() + bonus;
    }

    float GetInterval() => 1f / Mathf.Max(0.01f, GetEffectiveAttackSpeed());

    void Start()
    {
        int count = chargeBarFills != null ? chargeBarFills.Length : 0;
        _timers = new float[count];

        for (int i = 0; i < count; i++)
        {
            float offset = (initialChargeOffsets != null && i < initialChargeOffsets.Length)
                ? Mathf.Clamp01(initialChargeOffsets[i])
                : 0f;
            _timers[i] = offset * GetInterval();
        }
    }

    protected override void Update()
    {
        if (GameManager.Instance.IsGameOver) return;
        if (_timers == null) return;

        // Célpont keresés
        if (currentTarget == null || currentTarget.IsDead || !IsInRange(currentTarget))
            currentTarget = FindBestTarget();
        else if (targetingMode != TargetingMode.Alapertelmezett)
            currentTarget = FindBestTarget();

        if (currentTarget != null)
            FaceTarget(currentTarget.transform.position);

        float interval = GetInterval();

        for (int i = 0; i < _timers.Length; i++)
        {
            // Töltés folyamatosan megy, cap: interval
            if (_timers[i] < interval)
                _timers[i] = Mathf.Min(interval, _timers[i] + Time.deltaTime);

            // Charge bar frissítése
            if (chargeBarFills != null && i < chargeBarFills.Length && chargeBarFills[i] != null)
                chargeBarFills[i].fillAmount = Mathf.Clamp01(_timers[i] / interval);

            // Lövés ha tele és van célpont
            if (_timers[i] >= interval && currentTarget != null
                && Time.time - _lastShotTime >= minGapBetweenShots)
            {
                Shoot();
                _lastShotTime = Time.time;
                _timers[i]   -= interval;
                _idleTimer    = 0f;
            }
        }

        if (currentTarget == null)
            _idleTimer += Time.deltaTime;
    }

    protected override void Shoot()
    {
        if (projectilePrefab == null || currentTarget == null) return;

        Vector3 origin = shootPoint != null ? shootPoint.position : transform.position;
        var go = Instantiate(projectilePrefab, origin, Quaternion.identity);
        var p  = go.GetComponent<Projectile>();
        if (p == null) { Destroy(go); return; }

        p.Initialize(currentTarget, GetEffectiveDamage(), isAreaDamage, areaRadius, damageType);
        p.towerPosition = transform.position;
        p.towerRange    = GetEffectiveRange();

        // Skill bónuszok – kizárólag Projectile Two Dragon Burn mezőkre
        if (twoDragonSkillTree != null && UserProgressManager.Instance != null)
        {
            int spreadGen = Mathf.RoundToInt(UserProgressManager.Instance.GetTotalSkillEffect(
                SkillEffectType.TwoDragonFireSpread, twoDragonSkillTree));
            if (spreadGen > 0)
            {
                p.twoDragonBurnSpread           = true;
                p.twoDragonBurnSpreadGeneration = spreadGen - 1;
            }

            int moreDoT = Mathf.RoundToInt(UserProgressManager.Instance.GetTotalSkillEffect(
                SkillEffectType.TwoDragonFireMoreDoT, twoDragonSkillTree));
            p.twoDragonBurnMaxDoT = 1 + moreDoT;

            p.twoDragonBurnDmgBonus = UserProgressManager.Instance.GetTotalSkillEffect(
                SkillEffectType.TwoDragonDotDmg, twoDragonSkillTree);

            p.twoDragonBurnDurationBonus = UserProgressManager.Instance.GetTotalSkillEffect(
                SkillEffectType.TwoDragonDotTime, twoDragonSkillTree);
        }

        var af = go.GetComponent<altalanos_fejlesztesek>();
        if (af != null)
            af.SetTowerData(transform.position, GetEffectiveRange(), this);
    }
}
