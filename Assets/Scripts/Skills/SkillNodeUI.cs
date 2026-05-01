using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Egy skill fa csomópont UI eleme.
///
/// Vizuális állapotok:
///   • Elérhető  – fehér keret, gomb aktív
///   • Zárolt    – szürke overlay, gomb inaktív
///   • Maxolt    – arany keret, gomb inaktív
/// </summary>
public class SkillNodeUI : MonoBehaviour
{
    [Header("UI elemek")]
    public Image            iconImage;
    public TextMeshProUGUI  nameText;
    public TextMeshProUGUI  levelText;      // "3 / 10"
    public TextMeshProUGUI  costText;       // "Következő: 2 pont" / "MAX"
    public Button           upgradeButton;
    public Button           downgradeButton;
    public Button           infoButton;     // kattintás → leírás popup
    public Image            nodeBorder;     // a kocka keretének Image-e
    public GameObject       lockedOverlay;  // szürke réteg ha zárolt

    [Header("Keret színek")]
    public Color colorAvailable = new Color(0.2f, 0.8f, 0.2f);   // zöld
    public Color colorLocked    = new Color(0.3f, 0.3f, 0.3f);   // szürke
    public Color colorMaxed     = new Color(1.0f, 0.8f, 0.0f);   // arany

    [Header("Node azonosító")]
    [Tooltip("Pontosan egyezzen a SkillTreeDefinition-ban lévő id-val")]
    public string nodeId = "";

    private SkillNodeDefinition _definition;
    private SkillTreeDefinition _tree;
    private Action              _onUpgraded;

    public RectTransform NodeRect => GetComponent<RectTransform>();

    // ── Inicializálás ─────────────────────────────────────────────────

    /// <summary>SkillTreeUI hívja inicializáláskor – a nodeId alapján keresi meg a definíciót.</summary>
    public void Initialize(SkillTreeDefinition tree, Action onUpgraded)
    {
        _tree       = tree;
        _onUpgraded = onUpgraded;
        _definition = tree.GetNode(nodeId);

        if (_definition == null)
        {
            Debug.LogWarning($"SkillNodeUI: nem található node id: '{nodeId}'");
            return;
        }

        if (nameText  != null) nameText.text                      = _definition.displayName;
        if (iconImage != null && _definition.icon != null) iconImage.sprite = _definition.icon;

        if (upgradeButton   != null) upgradeButton.onClick.AddListener(OnUpgradeClicked);
        if (downgradeButton != null) downgradeButton.onClick.AddListener(OnDowngradeClicked);
        if (infoButton      != null) infoButton.onClick.AddListener(OnInfoClicked);

        Refresh();
    }

    // Régi Setup megtartva visszafelé kompatibilitáshoz
    public void Setup(SkillNodeDefinition def, SkillTreeDefinition tree, Action onUpgraded)
    {
        _definition = def;
        _tree       = tree;
        _onUpgraded = onUpgraded;
        nodeId      = def.id;

        if (nameText  != null) nameText.text                  = def.displayName;
        if (iconImage != null && def.icon != null) iconImage.sprite = def.icon;

        if (upgradeButton != null)
            upgradeButton.onClick.AddListener(OnUpgradeClicked);

        Refresh();
    }

    // ── Frissítés ─────────────────────────────────────────────────────

    public void Refresh()
    {
        if (_definition == null) return;

        var  mgr              = UserProgressManager.Instance;
        int  currentLevel     = mgr?.GetSkillLevel(_definition.id) ?? 0;
        bool canUpgrade       = mgr?.CanUpgradeSkill(_definition.id, _tree) ?? false;
        bool isMaxed          = currentLevel >= _definition.maxLevel;
        bool prerequisitesMet = mgr?.PrerequisitesMet(_definition.id, _tree) ?? false;
        bool isLocked         = currentLevel == 0 && !prerequisitesMet;


        // Szint szöveg
        if (levelText != null)
            levelText.text = $"{currentLevel} / {_definition.maxLevel}";

        // Következő szint ára
        if (costText != null)
        {
            if (isMaxed)
                costText.text = "MAX";
            else
                costText.text = $"Cost: {SkillNodeDefinition.GetUpgradeCost(currentLevel)}";
        }

        // Gomb státusz
        if (upgradeButton   != null) upgradeButton.interactable   = canUpgrade;
        if (downgradeButton != null) downgradeButton.interactable = currentLevel > 0;

        // Keret szín
        Color border = isMaxed ? colorMaxed : canUpgrade ? colorAvailable : colorLocked;
        if (nodeBorder != null) nodeBorder.color = border;

        // Zárolt overlay
        if (lockedOverlay != null) lockedOverlay.SetActive(isLocked);
    }

    // ── Kattintás ─────────────────────────────────────────────────────

    void OnUpgradeClicked()
    {
        if (UserProgressManager.Instance?.UpgradeSkill(_definition.id, _tree) == true)
        {
            VibrationHelper.VibrateShort();
            Refresh();
            _onUpgraded?.Invoke();
        }
    }

    void OnDowngradeClicked()
    {
        if (UserProgressManager.Instance?.DowngradeSkill(_definition.id, _tree) == true)
        {
            VibrationHelper.VibrateShort();
            Refresh();
            _onUpgraded?.Invoke();
        }
    }

    void OnInfoClicked()
    {
        if (_definition == null) return;
        int currentLevel = UserProgressManager.Instance?.GetSkillLevel(_definition.id) ?? 0;
        SkillTooltipUI.Instance?.Show(_definition, currentLevel);
    }
}
