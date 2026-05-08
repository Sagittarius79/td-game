using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Varázs torony Lvl2 – több (alapértelmezetten 3) párhuzamos lövést tölt.
/// Minden lövésnek saját töltési időzítője és töltés sávja van.
/// Ha egy időzítő tele van és van célpont a hatótávban, az adott "csőre" lő.
/// </summary>
public class MagicTowerLvl2 : MagicTower
{
    [Header("Lvl2 – több párhuzamos lövés")]
    [Tooltip("Egy töltés sáv lövésenként. A tömb hossza = lövések száma (pl. 3).")]
    public Image[] chargeBarFills;

    [Tooltip("A sávok kezdő töltöttsége (0..1) – staggered indítás. Ha üres, mind 0-ról indul.")]
    public float[] initialChargeOffsets;

    [Tooltip("Lövésenkénti egyedi attack speed (lövés/mp). Ha 0 vagy a tömb rövidebb, a torony alap attackSpeed-jét használja az adott csőre.")]
    public float[] perShotAttackSpeed;

    [Tooltip("Minimum idő (mp) két lövés között – akkor is, ha több cső is fel van töltve. Megakadályozza, hogy egyszerre süljön el az összes.")]
    public float minGapBetweenShots = 0.1f;

    private float[] _timers;
    private float   _lastShotTime = -999f;

    float GetIntervalFor(int i)
    {
        float spd = (perShotAttackSpeed != null && i < perShotAttackSpeed.Length && perShotAttackSpeed[i] > 0f)
            ? perShotAttackSpeed[i]
            : GetEffectiveAttackSpeed();
        return 1f / spd;
    }

    void Start()
    {
        int count = chargeBarFills != null ? chargeBarFills.Length : 0;
        _timers = new float[count];

        for (int i = 0; i < count; i++)
        {
            float offset = (initialChargeOffsets != null && i < initialChargeOffsets.Length)
                ? Mathf.Clamp01(initialChargeOffsets[i])
                : 0f;
            _timers[i] = offset * GetIntervalFor(i);
        }
    }

    protected override void Update()
    {
        if (GameManager.Instance.IsGameOver) return;
        if (_timers == null) return;

        if (currentTarget == null || currentTarget.IsDead || !IsInRange(currentTarget))
            currentTarget = FindBestTarget();
        else if (targetingMode != TargetingMode.Alapertelmezett)
            currentTarget = FindBestTarget();

        if (currentTarget != null)
            FaceTarget(currentTarget.transform.position);

        for (int i = 0; i < _timers.Length; i++)
        {
            float interval = GetIntervalFor(i);

            // Töltés célponttól függetlenül megy, de cap-pelve interval-nél,
            // hogy a sáv ne menjen 100% fölé és ne nőjön végtelenül.
            if (_timers[i] < interval)
                _timers[i] = Mathf.Min(interval, _timers[i] + Time.deltaTime);

            if (chargeBarFills[i] != null)
                chargeBarFills[i].fillAmount = Mathf.Clamp01(_timers[i] / interval);

            if (_timers[i] >= interval && currentTarget != null
                && Time.time - _lastShotTime >= minGapBetweenShots)
            {
                Shoot();
                _lastShotTime = Time.time;
                // Fázist megőrizzük: ne 0-ra resetelünk, hanem levonjuk az intervallumot.
                _timers[i] -= interval;
                _idleTimer = 0f;
            }
        }

        if (currentTarget == null)
            _idleTimer += Time.deltaTime;
    }
}
