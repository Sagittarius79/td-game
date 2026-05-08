using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Kastély HP regen töltősáv – a kastély fölé helyezve mutatja
/// mikor érkezik a következő HP regen és mennyi lesz az összeg.
///
/// Unity beállítás:
///   1. Canvas gyerekébe hozz létre egy Panel-t → neve: "CastleRegenBar"
///   2. Adj hozzá egy Image-et a töltősávhoz (Image Type=Filled, Fill Method=Horizontal)
///   3. Adj hozzá egy TextMeshProUGUI-t a szöveghez (a sáv fölé/fölére)
///   4. Ezt a scriptet húzd rá a Panel-re
///   5. Kösd be a mezőket az Inspectorban
/// </summary>
public class CastleRegenBar : MonoBehaviour
{
    [Header("UI elemek")]
    [Tooltip("A töltősáv Image komponense (Image Type=Filled, Fill Method=Horizontal)")]
    public Image fillImage;
    [Tooltip("A szöveg ami mutatja a regen összegét")]
    public TextMeshProUGUI regenText;

    [Header("Skill fa")]
    [Tooltip("A Buildings skill tree ScriptableObject – a HP Regen skill ellenőrzéséhez")]
    public SkillTreeDefinition buildingsSkillTree;

    [Header("Színek")]
    public Color colorCharging = new Color(0.2f, 0.6f, 1f, 1f);    // kék – töltés közben
    public Color colorReady    = new Color(0.2f, 0.9f, 0.2f, 1f);  // zöld – kész

    private const float REGEN_INTERVAL = 30f;
    private bool _isVisible = false;

    void RefreshVisibility()
    {
        bool hasRegen = false;

        if (buildingsSkillTree != null && UserProgressManager.Instance != null)
        {
            float regenLevel = UserProgressManager.Instance.GetTotalSkillEffect(
                SkillEffectType.BuildingHPRegen, buildingsSkillTree);
            hasRegen = regenLevel >= 1f;
        }

        bool hasDamage = HasAnyDamage();

        _isVisible = hasRegen && hasDamage;

        if (fillImage != null) fillImage.enabled = _isVisible;
        if (regenText  != null) regenText.enabled  = _isVisible;
    }

    int CalculateTotalMissing()
    {
        if (Castle.Instance == null) return 0;

        int total = 0;

        total += Castle.Instance.maxHealth - Castle.Instance.CurrentHealth;

        foreach (var tower in Tower.AllTowers)
        {
            if (tower == null || tower.IsDead || tower.HealthPercent >= 1f) continue;
            total += Mathf.RoundToInt(tower.maxHealth - tower.CurrentHealth);
        }

        return total;
    }

    bool HasAnyDamage()
    {
        if (Castle.Instance == null) return false;

        if (Castle.Instance.CurrentHealth < Castle.Instance.maxHealth) return true;

        foreach (var tower in Tower.AllTowers)
        {
            if (tower == null || tower.IsDead) continue;
            if (tower.HealthPercent < 1f) return true;
        }

        return false;
    }

    void Update()
    {
        RefreshVisibility();

        if (!_isVisible || fillImage == null || Castle.Instance == null) return;

        // A kastély saját timerét olvassuk – így a csík pontosan szinkronban van a gyógyítással
        float fill = Mathf.Clamp01(Castle.Instance.RegenTimer / REGEN_INTERVAL);
        fillImage.fillAmount = fill;
        fillImage.color      = fill >= 1f ? colorReady : colorCharging;

        // Szöveg: ténylegesen gyógyított HP (min(pool, összes hiány))
        if (regenText != null)
        {
            int regenAmount  = Castle.Instance.RegenAmount;
            int totalMissing = CalculateTotalMissing();
            int actualHeal   = Mathf.Min(regenAmount, totalMissing);

            if (actualHeal > 0)
            {
                bool canAfford = GameManager.Instance != null &&
                                 GameManager.Instance.CanAfford(actualHeal);
                regenText.text  = $"+{actualHeal} HP  ({actualHeal}G)";
                regenText.color = canAfford ? colorReady : new Color(1f, 0.4f, 0.4f);
            }
        }
    }
}
