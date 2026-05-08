using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Skill leírás popup panel.
/// Helyezd a Canvas alá, kösd be a mezőket, alapból legyen inaktív.
/// </summary>
public class SkillTooltipUI : MonoBehaviour
{
    public static SkillTooltipUI Instance { get; private set; }

    [Header("UI elemek")]
    public GameObject       panel;
    public TextMeshProUGUI  titleText;
    public TextMeshProUGUI  descriptionText;
    [Tooltip("Jelenlegi hatás / következő szint hatása")]
    public TextMeshProUGUI  effectText;
    public Button           backdropButton;  // teljes képernyős átlátszó gomb

    void Awake()
    {
        Instance = this;
        if (panel          != null) panel.SetActive(false);
        if (backdropButton != null)
        {
            backdropButton.onClick.AddListener(Hide);
            backdropButton.gameObject.SetActive(false);
        }
    }

    // ── Megjelenítés – skill node-ból ────────────────────────────────

    /// <summary>
    /// Megnyitja a tooltipet a skill aktuális és következő szintjének hatásával.
    /// </summary>
    public void Show(SkillNodeDefinition def, int currentLevel)
    {
        if (def == null) return;

        // Ugyanaz a panel már nyitva van → bezár
        if (panel != null && panel.activeSelf &&
            titleText != null && titleText.text == def.displayName)
        {
            Hide();
            return;
        }

        if (titleText       != null) titleText.text       = def.displayName;
        if (descriptionText != null) descriptionText.text = def.description;

        // Hatás szöveg
        if (effectText != null)
        {
            string fx = BuildEffectText(def, currentLevel);
            bool   show = !string.IsNullOrEmpty(fx);
            effectText.gameObject.SetActive(show);
            if (show) effectText.text = fx;
        }

        if (panel          != null) panel.SetActive(true);
        if (backdropButton != null) backdropButton.gameObject.SetActive(true);
    }

    /// <summary>Régi szignatúra – visszafelé kompatibilitás, effectText rejtve.</summary>
    public void Show(string title, string description)
    {
        if (panel != null && panel.activeSelf &&
            titleText != null && titleText.text == title)
        {
            Hide();
            return;
        }

        if (titleText       != null) titleText.text       = title;
        if (descriptionText != null) descriptionText.text = description;
        if (effectText      != null) effectText.gameObject.SetActive(false);
        if (panel           != null) panel.SetActive(true);
        if (backdropButton  != null) backdropButton.gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (panel          != null) panel.SetActive(false);
        if (backdropButton != null) backdropButton.gameObject.SetActive(false);
    }

    // ── Hatás szöveg összeállítása ────────────────────────────────────

    string BuildEffectText(SkillNodeDefinition def, int currentLevel)
    {
        if (def.effectType == SkillEffectType.None) return "";

        bool isMaxed   = currentLevel >= def.maxLevel;
        float curValue  = def.GetTotalEffect(currentLevel);
        float nextValue = def.GetTotalEffect(currentLevel + 1);

        string curStr  = FormatEffectValue(def.effectType, curValue);
        string nextStr = FormatEffectValue(def.effectType, nextValue);

        if (isMaxed)
            return $"Hatás: {curStr}  (MAX)";

        if (currentLevel == 0)
            return $"No effect now\nnext Lvl: {nextStr}";

        return $"Now: {curStr}\nnext lvl: {nextStr}";
    }

    static string FormatEffectValue(SkillEffectType type, float value)
    {
        if (value == 0f) return "0";
        string numStr = (value == Mathf.Floor(value))
            ? ((int)value).ToString()
            : value.ToString("F2");

        return IsPercentType(type) ? $"+{numStr}%" : $"+{numStr}";
    }

    static bool IsPercentType(SkillEffectType type) =>
        type == SkillEffectType.CritHitChance    ||
        type == SkillEffectType.MultiShotChance  ||
        type == SkillEffectType.StunChance       ||
        type == SkillEffectType.TrapChance       ||
        type == SkillEffectType.StoneCritChance;
}
