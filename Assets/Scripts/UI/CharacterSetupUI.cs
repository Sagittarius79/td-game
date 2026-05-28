using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Karakter létrehozó képernyő – két oldalas flow:
///   Page 1: névbevitel + PvP/SSF választó + NEXT gomb
///   Page 2: karakterosztály választó (3 kártya) + DONE gomb
///
/// Unity Editor beállítás:
///   1. CharacterSetupPanel alatt hozz létre két child panel-t:
///      "Page1" (név + mód) és "Page2" (kártya választó)
///   2. Kösd be az Inspector mezőket
///   3. A CharacterSetupPanel legyen alapból INACTIVE
/// </summary>
public class CharacterSetupUI : MonoBehaviour
{
    public static CharacterSetupUI Instance { get; private set; }

    [Header("Panel")]
    public GameObject setupPanel;

    [Header("Oldalak")]
    public GameObject page1;   // névbevitel + PvP/SSF
    public GameObject page2;   // karakterosztály választó

    [Header("Page 1 – UI elemek")]
    public TMP_InputField   nameInputField;
    public Button           nextButton;
    public Button           backButton;        // → CharacterSelectUI
    public TextMeshProUGUI  statusText;

    [Header("Page 1 – Karakter mód")]
    [Tooltip("PvP karakter: Solo és PvP mód is elérhető.")]
    public Toggle pvpToggle;
    [Tooltip("SSF karakter: csak Solo mód érhető el.")]
    public Toggle ssfToggle;
    [Tooltip("A kiválasztott mód leírása")]
    public TextMeshProUGUI modeDescriptionText;

    const string PvPDescription = "You can PvP against other players for glory and rank. (You can also play solo, but you only get XP up to lvl 10)";
    const string SSFDescription = "Solo Self-Found! A challenge for those who want to explore the game alone. This Hero will never be able to play against other players.";

    [Header("Page 2 – Karakter osztály gombok")]
    public Button archerButton;
    public Button stoneThrowerButton;
    public Button mageButton;

    [Header("Page 2 – Karakter osztály sprite-ok")]
    public Sprite archerNormal;
    public Sprite archerLit;
    public Sprite stoneThrowerNormal;
    public Sprite stoneThrowerLit;
    public Sprite mageNormal;
    public Sprite mageLit;

    [Header("Page 2 – UI elemek")]
    public Button           confirmButton;        // DONE
    public Button           backButtonPage2;      // → vissza page 1-re
    public TextMeshProUGUI  classDescriptionText; // kiválasztott osztály leírása

    const string ArcherDescription       = "Every turret that fires arrows has a 10% chance to double fire all projectiles. ";
    const string StoneThrowerDescription = "Increases the AOE range of the Stone Thrower by 20%.";
    const string MageDescription         = "All mage towers have a 10% increased range.";

    private CharacterClass? _selectedClass = null;

    [Header("Korlátok")]
    public int minLength = 3;
    public int maxLength = 20;

    // ── Életciklus ────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        nextButton?.onClick.AddListener(OnNextPressed);
        backButton?.onClick.AddListener(OnBackPressed);
        confirmButton?.onClick.AddListener(OnConfirmPressed);
        backButtonPage2?.onClick.AddListener(ShowPage1);

        if (nameInputField != null)
        {
            nameInputField.characterLimit = maxLength;
            nameInputField.onValueChanged.AddListener(OnNameChanged);
        }

        pvpToggle?.onValueChanged.AddListener(OnPvPToggleChanged);
        ssfToggle?.onValueChanged.AddListener(OnSSFToggleChanged);

        archerButton?.onClick.AddListener(()      => SelectClass(CharacterClass.Archer));
        stoneThrowerButton?.onClick.AddListener(() => SelectClass(CharacterClass.StoneThrower));
        mageButton?.onClick.AddListener(()        => SelectClass(CharacterClass.Mage));

        if (setupPanel != null) setupPanel.SetActive(false);
        if (statusText  != null) statusText.gameObject.SetActive(false);
    }

    // ── Megjelenítés ──────────────────────────────────────────────────

    public void Show()
    {
        if (setupPanel == null) return;
        setupPanel.SetActive(true);
        if (nameInputField != null) nameInputField.text = "";
        SetSelectedMode(CharacterGameMode.PvP);
        SetModeDescription(CharacterGameMode.PvP);
        SetStatus("");
        ShowPage1();
        FindObjectOfType<MainMenuUI>()?.SetCharacterCardVisible(false);
    }

    public void Hide()
    {
        if (setupPanel != null) setupPanel.SetActive(false);
        var mainMenu = FindObjectOfType<MainMenuUI>();
        mainMenu?.RefreshCharacterCard();
        mainMenu?.SetCharacterCardVisible(true);
    }

    // ── Oldalváltás ───────────────────────────────────────────────────

    void ShowPage1()
    {
        page1?.SetActive(true);
        page2?.SetActive(false);
        SetStatus("");
        nameInputField?.Select();
    }

    void ShowPage2()
    {
        page1?.SetActive(false);
        page2?.SetActive(true);
        _selectedClass = null;
        RefreshClassButtons();
        if (confirmButton != null) confirmButton.gameObject.SetActive(false);
        if (classDescriptionText != null) classDescriptionText.text = "";
        SetStatus("");
    }

    // ── Gomb / input kezelők ──────────────────────────────────────────

    void OnNameChanged(string value)
    {
        if (value.Length > 0 && value.Length < minLength)
            SetStatus($"Még {minLength - value.Length} karakter kell...");
        else
            SetStatus("");
    }

    void OnNextPressed()
    {
        if (nameInputField == null) return;
        string chosen = nameInputField.text.Trim();

        if (chosen.Length < minLength)
        {
            SetStatus($"A név legalább {minLength} karakter legyen!");
            return;
        }

        ShowPage2();
    }

    void OnConfirmPressed()
    {
        if (_selectedClass == null)
        {
            SetStatus("Válassz karakterosztályt!");
            return;
        }

        string chosen = nameInputField != null ? nameInputField.text.Trim() : "";

        // Mentés
        UserProgressManager.Instance?.CreateCharacter(chosen, GetSelectedMode(), _selectedClass.Value);

        // Azonnal szinkronizál – az új karakter felkerül a szerverre
        ServerSyncManager.GetOrCreate().TriggerSync();

        Hide();
        CharacterSheetUI.Instance?.Show();
    }

    void OnBackPressed()
    {
        Hide();
        CharacterSelectUI.Instance?.Show();
    }

    void OnPvPToggleChanged(bool isOn)
    {
        if (isOn && ssfToggle != null) ssfToggle.isOn = false;
        if (!isOn && ssfToggle != null && !ssfToggle.isOn) pvpToggle.isOn = true;
        if (isOn) SetModeDescription(CharacterGameMode.PvP);
    }

    void OnSSFToggleChanged(bool isOn)
    {
        if (isOn && pvpToggle != null) pvpToggle.isOn = false;
        if (!isOn && pvpToggle != null && !pvpToggle.isOn) ssfToggle.isOn = true;
        if (isOn) SetModeDescription(CharacterGameMode.SSF);
    }

    void SetModeDescription(CharacterGameMode mode)
    {
        if (modeDescriptionText == null) return;
        modeDescriptionText.text = mode == CharacterGameMode.SSF ? SSFDescription : PvPDescription;
    }

    CharacterGameMode GetSelectedMode()
    {
        return ssfToggle != null && ssfToggle.isOn
            ? CharacterGameMode.SSF
            : CharacterGameMode.PvP;
    }

    void SetSelectedMode(CharacterGameMode mode)
    {
        if (pvpToggle != null) pvpToggle.isOn = mode == CharacterGameMode.PvP;
        if (ssfToggle != null) ssfToggle.isOn = mode == CharacterGameMode.SSF;
    }

    // ── Karakter osztály választó ─────────────────────────────────────

    void SelectClass(CharacterClass cls)
    {
        _selectedClass = cls;
        RefreshClassButtons();
        if (confirmButton != null) confirmButton.gameObject.SetActive(true);
        if (classDescriptionText != null)
            classDescriptionText.text = cls switch
            {
                CharacterClass.Archer       => ArcherDescription,
                CharacterClass.StoneThrower => StoneThrowerDescription,
                CharacterClass.Mage         => MageDescription,
                _                           => ""
            };
    }

    static readonly Color SelectedColor = Color.white;
    static readonly Color DimmedColor   = new Color(0.35f, 0.35f, 0.35f, 1f);

    void RefreshClassButtons()
    {
        RefreshClassButton(archerButton,       CharacterClass.Archer,       archerNormal,       archerLit);
        RefreshClassButton(stoneThrowerButton, CharacterClass.StoneThrower, stoneThrowerNormal, stoneThrowerLit);
        RefreshClassButton(mageButton,         CharacterClass.Mage,         mageNormal,         mageLit);
    }

    void RefreshClassButton(Button btn, CharacterClass cls, Sprite normal, Sprite lit)
    {
        if (btn == null) return;
        var img = btn.GetComponent<Image>();
        if (img == null) return;

        bool isSelected  = _selectedClass == cls;
        bool anySelected = _selectedClass != null;

        img.sprite = isSelected ? lit : normal;
        img.color  = (!anySelected || isSelected) ? SelectedColor : DimmedColor;
    }

    // ── Segéd ─────────────────────────────────────────────────────────

    void SetStatus(string msg)
    {
        if (statusText == null) return;
        statusText.text = msg;
        statusText.gameObject.SetActive(!string.IsNullOrEmpty(msg));
    }
}
