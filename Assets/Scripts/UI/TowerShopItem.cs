using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Egy torony gomb a jobb oldali bolt panelben.
/// A Tower Definition-t közvetlenül az Inspectorban kell beállítani!
/// </summary>
public class TowerShopItem : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    [Header("Torony adatok – töltsd ki az Inspectorban!")]
    public string towerName = "Torony";
    public int goldCost = 10;
    public Sprite towerSprite;
    public GameObject towerPrefab;
    [TextArea(2, 4)]
    public string description = "";

    [Header("Skill zárolás")]
    [Tooltip("Ha meg van adva, ez a skill node ID kell hogy fel legyen oldva az építéshez (pl. Unlock_archer_lvl2)")]
    public string requiredSkillNodeId = "";


    [Header("Dynamic price")]
    [Tooltip("Ha be van kapcsolva, az ár automatikusan nő minden lerakott példányonként.\n" +
             "Minden épülettípus a saját darabszámát követi – nem keverednek egymással.\n" +
             "A goldCost mezőt ilyenkor alap árként használja.\n" +
             "A prefabhoz add hozzá a DynamicPriceBuilding scriptet is!")]
    public bool dynamicPrice = false;

    [Header("UI Hivatkozások")]
    public Image iconImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI costText;

    [Header("Vizuális visszajelzés")]
    public Color affordableColor   = Color.white;
    public Color unaffordableColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    public Color textUnaffordableColor = new Color(0.5f, 0.5f, 0.5f, 1f);

    private CanvasGroup canvasGroup;
    private Color costTextOriginalColor;

    private bool pointerDown = false;
    private bool dragStarted = false;
    private Vector2 pointerDownPos;
    private const float DRAG_THRESHOLD = 20f;

    // Editor-ban automatikusan frissíti a szövegeket, ha az Inspector értékek változnak
    void OnValidate()
    {
        if (costText != null)
            costText.text = goldCost + " G";
        if (nameText != null && !string.IsNullOrEmpty(towerName))
            nameText.text = towerName;
    }

    // towerName → ikon sprite kereséshez (SpyTowerPanelUI használja)
    public static readonly Dictionary<string, Sprite> IconRegistry = new Dictionary<string, Sprite>();

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (!string.IsNullOrEmpty(towerName) && towerSprite != null)
            IconRegistry[towerName] = towerSprite;

        // Az Inspectorban beállított costText szín mentése
        if (costText != null)
            costTextOriginalColor = costText.color;
    }

    void Start()
    {
        if (nameText != null) nameText.text = towerName;
        if (iconImage != null && towerSprite != null) iconImage.sprite = towerSprite;

        if (GameManager.Instance != null)
            GameManager.Instance.OnGoldChanged += UpdateAffordability;

        // Dinamikus ár esetén feliratkozás az épület-számláló változásra
        if (dynamicPrice)
            DynamicPriceBuilding.OnCountChanged += RefreshCostDisplay;

        // Skill változáskor ár és zárolás frissítése
        if (UserProgressManager.Instance != null)
            UserProgressManager.Instance.OnSkillChanged += OnSkillChangedHandler;

        RefreshCostDisplay();
        UpdateAffordability(GameManager.Instance != null ? GameManager.Instance.CurrentGold : 0);
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnGoldChanged -= UpdateAffordability;

        if (dynamicPrice)
            DynamicPriceBuilding.OnCountChanged -= RefreshCostDisplay;

        if (UserProgressManager.Instance != null)
            UserProgressManager.Instance.OnSkillChanged -= OnSkillChangedHandler;
    }

    void OnSkillChangedHandler(string _)
    {
        RefreshCostDisplay();
    }

    /// <summary>
    /// Aktuális ár kiszámítása.
    /// Dinamikus ár esetén: goldCost × (saját típus lerakott darabszáma + 1).
    /// Skill kedvezmény esetén: le vonjuk a szintenként meghatározott összeget (minimum 1 Gold).
    /// </summary>
    int GetCurrentCost()
    {
        int baseCost = goldCost;
        if (dynamicPrice && towerPrefab != null)
        {
            int placed = DynamicPriceBuilding.GetCount(towerPrefab.name);
            baseCost = goldCost * (placed + 1);
        }

        int discount = 0;
        if (towerPrefab != null && UserProgressManager.Instance != null)
        {
            var magic = towerPrefab.GetComponent<MagicTower>();
            if (magic != null && magic.magicSkillTree != null)
            {
                float bonus = UserProgressManager.Instance.GetTotalSkillEffect(
                    SkillEffectType.CheaperMagicTower, magic.magicSkillTree);
                discount = Mathf.FloorToInt(bonus);
            }
        }

        int afterDiscount = Mathf.Max(1, baseCost - discount);

        // Skill pont alapú árszorzó: minden elköltött pont +1%
        if (towerPrefab != null && UserProgressManager.Instance != null)
        {
            var tower = towerPrefab.GetComponent<Tower>();
            if (tower != null && tower.priceSkillTree != null)
            {
                int spent = UserProgressManager.Instance.GetTotalSpentPoints(tower.priceSkillTree);
                afterDiscount = Mathf.RoundToInt(afterDiscount * (1f + spent * 0.01f));
            }
        }

        // Globális épület árengedmény %-ban (Buildings skill tree)
        if (BuildingsConfig.Instance != null)
        {
            float reductionPct = BuildingsConfig.Instance.GetCostReductionPercent();
            if (reductionPct > 0f)
                afterDiscount = Mathf.Max(1, afterDiscount - Mathf.FloorToInt(afterDiscount * reductionPct / 100f));
        }

        return afterDiscount;
    }

    /// <summary>Frissíti az ár szöveget és a megfizethetőségi jelzést.</summary>
    void RefreshCostDisplay()
    {
        if (costText != null) costText.text = GetCurrentCost() + " G";
        UpdateAffordability(GameManager.Instance != null ? GameManager.Instance.CurrentGold : 0);
    }

    bool IsSkillUnlocked()
    {
        if (string.IsNullOrEmpty(requiredSkillNodeId)) return true;
        if (UserProgressManager.Instance == null) return false;
        return UserProgressManager.Instance.GetSkillLevel(requiredSkillNodeId) >= 1;
    }

    void UpdateAffordability(int gold)
    {
        bool unlocked  = IsSkillUnlocked();
        bool canAfford = unlocked && gold >= GetCurrentCost();

        if (iconImage != null) iconImage.color = canAfford ? affordableColor : unaffordableColor;
        if (nameText  != null) nameText.color  = canAfford ? Color.white : textUnaffordableColor;
        if (costText  != null) costText.color  = canAfford ? costTextOriginalColor : textUnaffordableColor;
        if (canvasGroup != null) canvasGroup.alpha = (unlocked ? 1f : 0.4f) * (canAfford ? 1f : 0.7f);

        // Ha zárolva van, teljesen letiltjuk az interakciót
        if (canvasGroup != null) canvasGroup.interactable   = unlocked;
        if (canvasGroup != null) canvasGroup.blocksRaycasts = unlocked;
    }

    // ── Touch / Egér input ────────────────────────────────────────

    public void OnPointerDown(PointerEventData eventData)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // Jobb klik WebGL-en → tooltip megjelenítése
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            StartTowerDrag(forceBuild: false);  // tooltipMode=true → Show() hívódik
            return;
        }
        // Bal klik: szokásos flag beállítás
#endif
        pointerDownPos = eventData.position;
        pointerDown    = true;
        dragStarted    = false;

        if (BuildMenuUI.Instance != null && !BuildMenuUI.Instance.tooltipMode)
            BuildMenuUI.Instance.ResetAutoClose();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!pointerDown) return;

        float dist = Vector2.Distance(eventData.position, pointerDownPos);
        if (!dragStarted && dist > DRAG_THRESHOLD)
        {
            dragStarted = true;

            bool isTooltip = BuildMenuUI.Instance != null && BuildMenuUI.Instance.tooltipMode;
            StartTowerDrag(forceBuild: isTooltip);
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // Jobb klik UP → nem csinálunk semmit (OnPointerDown kezelte)
        if (eventData.button == PointerEventData.InputButton.Right) return;

        // Bal klik WebGL-en → közvetlen torony építés (tooltip kihagyása)
        if (pointerDown && !dragStarted)
            StartTowerDrag(forceBuild: true);

        pointerDown = false;
        dragStarted = false;
        return;
#endif
        // ── Mobil / Editor ────────────────────────────────────────────
        if (pointerDown && !dragStarted &&
            BuildMenuUI.Instance != null && BuildMenuUI.Instance.tooltipMode)
        {
            StartTowerDrag(forceBuild: false);
        }

        pointerDown = false;
        dragStarted = false;
    }

    void StartTowerDrag(bool forceBuild = false)
    {
        if (!IsSkillUnlocked()) return;

        if (TowerShopUI.Instance == null)
        {
            Debug.LogError("TowerShopItem: TowerShopUI.Instance null!");
            return;
        }

        TowerDefinition def = new TowerDefinition
        {
            towerName    = this.towerName,
            goldCost     = this.GetCurrentCost(),  // dinamikus ár esetén aktuális érték
            sprite       = this.towerSprite,
            prefab       = this.towerPrefab,
            description  = this.description,
            dynamicPrice = this.dynamicPrice
        };

        if (def.prefab == null)
        {
            Debug.LogError($"TowerShopItem '{towerName}': A towerPrefab nincs bekötve az Inspectorban!");
            return;
        }

        if (!forceBuild && BuildMenuUI.Instance != null && BuildMenuUI.Instance.tooltipMode)
        {
            TowerTooltipUI.Instance?.Show(def);
            return;
        }

        TowerShopUI.Instance.StartDragging(def);
    }

    public void OnClick()
    {
        StartTowerDrag();
    }
}
