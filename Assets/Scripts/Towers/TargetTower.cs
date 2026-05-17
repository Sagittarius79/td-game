using UnityEngine;

/// <summary>
/// Target Tower – lehetővé teszi a kézi célzást (focus fire).
/// Charge bar: feltöltés után a játékos egy ellenségre kattintva
/// rákényszeríti az összes tornyot annak célzására.
/// Minden használat elfogyasztja a töltést; újratöltés után lehet ismét.
/// </summary>
public class TargetTower : Tower
{
    [Header("Charge (feltöltési idő)")]
    [Tooltip("Mennyi másodperc alatt tölt fel teljesen")]
    public float chargeDuration = 10f;

    private float _chargeTimer = 0f;

    public bool IsCharged => _chargeTimer >= chargeDuration;

    // ── Statikus segédek ──────────────────────────────────────────

    public static bool AnyCharged()
    {
        foreach (var t in AllTowers)
        {
            if (t is TargetTower tt && tt.IsCharged) return true;
        }
        return false;
    }

    public static bool TryConsumeCharge()
    {
        foreach (var t in AllTowers)
        {
            if (t is TargetTower tt && tt.IsCharged)
            {
                tt._chargeTimer = 0f;
                tt.RefreshChargeBar();
                return true;
            }
        }
        return false;
    }

    // ── Életciklus ────────────────────────────────────────────────

    void Awake()
    {
        isTargetingTower = true;
    }

    protected override void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.IsGameOver) return;

        if (_chargeTimer < chargeDuration)
            _chargeTimer = Mathf.Min(_chargeTimer + Time.deltaTime, chargeDuration);

        RefreshChargeBar();
    }

    void RefreshChargeBar()
    {
        if (chargeBarFill != null)
            chargeBarFill.fillAmount = _chargeTimer / chargeDuration;
    }
}
