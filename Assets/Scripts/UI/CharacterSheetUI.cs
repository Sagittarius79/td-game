using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Karakter lap képernyő – bejelentkezés + névválasztás után jelenik meg,
/// mutatja a szintet, XP-t és statisztikákat.
///
/// Unity Editor beállítás:
///   1. MainMenu Canvas-ra adj hozzá egy Panel-t → "CharacterSheetPanel"
///   2. Adj hozzá egy üres GameObject-et, erre tedd ezt a scriptet
///   3. Kösd be az Inspector mezőket
///   4. A CharacterSheetPanel legyen alapból INACTIVE
/// </summary>
public class CharacterSheetUI : MonoBehaviour
{
    public static CharacterSheetUI Instance { get; private set; }

    [Header("Panel")]
    public GameObject sheetPanel;

    [Header("Karakternév és szint")]
    public TextMeshProUGUI characterNameText;   // pl. "DragonSlayer99"
    public TextMeshProUGUI levelText;           // pl. "5. Szint"
    public TextMeshProUGUI googleNameText;      // pl. "Kovács János  •  test@gmail.com"

    [Header("XP sáv")]
    public Image           xpBarFill;          // Filled Image (0–1)
    public TextMeshProUGUI xpText;             // pl. "720 / 1500 XP"

    [Header("Statisztikák")]
    public TextMeshProUGUI winsText;           // pl. "Győzelem: 12"
    public TextMeshProUGUI lossesText;         // pl. "Vereség: 4"
    public TextMeshProUGUI winRateText;        // pl. "Win rate: 75%"

    [Header("Gombok")]
    public Button closeButton;                 // visszatér a főmenübe

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);

        if (sheetPanel != null) sheetPanel.SetActive(false);
    }

    // ── Megjelenítés ──────────────────────────────────────────────────

    public void Show()
    {
        if (sheetPanel == null) return;
        Refresh();
        sheetPanel.SetActive(true);
        FindObjectOfType<MainMenuUI>()?.SetCharacterCardVisible(false);
    }

    public void Hide()
    {
        if (sheetPanel != null) sheetPanel.SetActive(false);
        FindObjectOfType<MainMenuUI>()?.SetCharacterCardVisible(true);
    }

    // ── Adatok frissítése ─────────────────────────────────────────────

    public void Refresh()
    {
        var mgr = UserProgressManager.Instance;
        if (mgr == null) return;

        var data  = mgr.Data;
        int level = mgr.Level;

        // Karakter és szint
        if (characterNameText != null)
            characterNameText.text = data.characterName;

        if (levelText != null)
            levelText.text = $"{level}. Szint";

        if (googleNameText != null)
        {
            string email = string.IsNullOrEmpty(mgr.AccountEmail) ? "" : $"  •  {mgr.AccountEmail}";
            googleNameText.text = $"{email}";
        }

        // XP sáv
        long xpInLevel   = data.XPInCurrentLevel;
        long xpNeeded    = data.XPForNextLevel - UserProgressManager.XPForLevel(level);

        if (xpBarFill != null)
            xpBarFill.fillAmount = data.LevelProgress;

        if (xpText != null)
            xpText.text = $"{xpInLevel} / {xpNeeded} XP";

        // Statisztikák
        if (winsText != null)
            winsText.text = $"Wins: {data.totalWins}";

        if (lossesText != null)
            lossesText.text = $"Looses: {data.totalLosses}";

        if (winRateText != null)
        {
            int total = data.totalWins + data.totalLosses;
            string rate = total > 0
                ? $"{Mathf.RoundToInt(100f * data.totalWins / total)}%"
                : "–";
            winRateText.text = $"Win rate: {rate}";
        }
    }
}
