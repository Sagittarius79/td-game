using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Egy szörny gomb a PvP küldő panelben.
/// Rábökésre elküldi a szörnyet az ellenfélnek (ha van elég arany).
///
/// Unity Editor beállítás:
///   1. Kösd be az enemyIndex-et (0 = első szörny, 1 = második, stb.)
///   2. Kösd be az iconImage, nameText, costText UI elemeket
///   3. A Button OnClick → PvPSendPanel.TrySendEnemy() VAGY
///      hagyd üresen és használd az OnPointerClick-et (automatikus)
/// </summary>
public class PvPSendItem : MonoBehaviour
{
    [Header("Szörny index (a PvPSendPanel listájában)")]
    public int enemyIndex = 0;

    [Header("UI elemek")]
    public Image           iconImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI costText;

    [Header("Vizuális visszajelzés")]
    public Color affordableColor   = Color.white;
    public Color unaffordableColor = new Color(0.5f, 0.5f, 0.5f, 1f);

    private CanvasGroup canvasGroup;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    void Start()
    {
        // Szövegek és ikon beállítása a SendableEnemyDefinition alapján
        if (PvPSendPanel.Instance != null &&
            enemyIndex < PvPSendPanel.Instance.sendableEnemies.Length)
        {
            var def = PvPSendPanel.Instance.sendableEnemies[enemyIndex];
            if (iconImage != null && def.icon != null) iconImage.sprite = def.icon;
            if (nameText  != null) nameText.text = def.enemyName;
            if (costText  != null) costText.text = $"{def.goldCost} G";
        }

        if (GameManager.Instance != null)
            GameManager.Instance.OnGoldChanged += UpdateAffordability;

        UpdateAffordability(GameManager.Instance != null ? GameManager.Instance.CurrentGold : 0);
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnGoldChanged -= UpdateAffordability;
    }

    // ── Gomb callback (Button OnClick-hez kösd be) ───────────────────

    public void OnPressed()
    {
        PvPSendPanel.Instance?.TrySendEnemy(enemyIndex);
    }

    // ── Vizuális visszajelzés ────────────────────────────────────────

    public void RefreshPrice(int gold) => UpdateAffordability(gold);

    void UpdateAffordability(int gold)
    {
        if (PvPSendPanel.Instance == null ||
            PvPSendPanel.Instance.sendableEnemies == null ||
            enemyIndex >= PvPSendPanel.Instance.sendableEnemies.Length) return;

        int baseCost = PvPSendPanel.Instance.sendableEnemies[enemyIndex].goldCost;
        int cost = RuneBuffManager.Instance != null
            ? RuneBuffManager.Instance.GetEffectiveSendCost(baseCost, PvPSendPanel.Instance.runeConfig)
            : baseCost;

        if (costText != null) costText.text = $"{cost} G";

        bool canAfford = gold >= cost;
        if (iconImage   != null) iconImage.color = canAfford ? affordableColor : unaffordableColor;
        if (canvasGroup != null) canvasGroup.alpha = canAfford ? 1f : 0.4f;
    }
}
