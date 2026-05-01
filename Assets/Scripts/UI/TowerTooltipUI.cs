using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Torony tooltip kártya – részletes info + BUILD / BACK gombok.
/// </summary>
public class TowerTooltipUI : MonoBehaviour
{
    public static TowerTooltipUI Instance { get; private set; }

    [Header("Kártya panel")]
    public GameObject cardPanel;

    [Header("Kártya elemek")]
    public Image           towerImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descriptionText;

    private TowerDefinition currentDef;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start() => Hide();

    // ── Megjelenítés ──────────────────────────────────────────────

    public void Show(TowerDefinition def)
    {
        currentDef = def;

        if (towerImage      != null) towerImage.sprite   = def.sprite;
        if (nameText        != null) nameText.text       = def.towerName;
        if (descriptionText != null) descriptionText.text = def.description ?? "";

        if (cardPanel != null) cardPanel.SetActive(true);

        // Következő érintésre bezárás beindítása
        if (BuildMenuUI.Instance != null)
            BuildMenuUI.Instance.StartTooltipCloseListener();
    }

    public void Hide()
    {
        if (cardPanel != null) cardPanel.SetActive(false);
        currentDef = null;
    }

    // ── Gomb callbackok ───────────────────────────────────────────

    /// <summary>BUILD gomb – ugyanaz mint a normál lerakás.</summary>
    public void OnBuildPressed()
    {
        if (currentDef == null) return;
        Hide();
        if (TowerShopUI.Instance != null)
            TowerShopUI.Instance.StartDragging(currentDef);
    }

    /// <summary>BACK gomb – kártya és shop bezárása.</summary>
    public void OnBackPressed()
    {
        Hide();
        if (BuildMenuUI.Instance != null)
            BuildMenuUI.Instance.CloseMenu();
    }
}
