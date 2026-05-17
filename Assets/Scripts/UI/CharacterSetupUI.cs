using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Névválasztó képernyő – akkor jelenik meg, ha a játékos első alkalommal
/// jelentkezik be Google-lal és még nincs karakterneve.
///
/// Unity Editor beállítás:
///   1. MainMenu Canvas-ra adj hozzá egy Panel-t → "CharacterSetupPanel"
///   2. Adj hozzá egy üres GameObject-et, erre tedd ezt a scriptet
///   3. Kösd be az Inspector mezőket
///   4. A CharacterSetupPanel legyen alapból INACTIVE
/// </summary>
public class CharacterSetupUI : MonoBehaviour
{
    public static CharacterSetupUI Instance { get; private set; }

    [Header("Panel")]
    public GameObject setupPanel;

    [Header("UI elemek")]
    public TMP_InputField nameInputField;   // a névbeviteli mező
    public Button         confirmButton;
    public Button         backButton;
    public TextMeshProUGUI statusText;      // hibaüzenetek / útmutató

    [Header("Karakter mód")]
    [Tooltip("PvP karakter: Solo és PvP mód is elérhető.")]
    public Toggle pvpToggle;
    [Tooltip("SSF karakter: csak Solo mód érhető el.")]
    public Toggle ssfToggle;
    [Tooltip("A kiválasztott mód leírása")]
    public TextMeshProUGUI modeDescriptionText;

    const string PvPDescription = "You can PvP against other players for glory and rank. (You can also play solo, but you only get XP up to lvl 10)";
    const string SSFDescription = "A challenge for those who want to explore the game alone. This Hero will never be able to play against other players.";

    [Header("Korlátok")]
    public int minLength = 3;
    public int maxLength = 20;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmPressed);

        if (backButton != null)
            backButton.onClick.AddListener(OnBackPressed);

        if (nameInputField != null)
        {
            nameInputField.characterLimit = maxLength;
            nameInputField.onValueChanged.AddListener(OnNameChanged);
        }

        if (pvpToggle != null)
            pvpToggle.onValueChanged.AddListener(OnPvPToggleChanged);
        if (ssfToggle != null)
            ssfToggle.onValueChanged.AddListener(OnSSFToggleChanged);

        if (setupPanel != null) setupPanel.SetActive(false);
        if (statusText != null) statusText.gameObject.SetActive(false);
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
        nameInputField?.Select();
        FindObjectOfType<MainMenuUI>()?.SetCharacterCardVisible(false);
    }

    public void Hide()
    {
        if (setupPanel != null) setupPanel.SetActive(false);
        var mainMenu = FindObjectOfType<MainMenuUI>();
        mainMenu?.RefreshCharacterCard();
        mainMenu?.SetCharacterCardVisible(true);
    }

    // ── Gomb / input kezelők ──────────────────────────────────────────

    void OnNameChanged(string value)
    {
        // Valós idejű visszajelzés
        if (value.Length > 0 && value.Length < minLength)
            SetStatus($"Még {minLength - value.Length} karakter kell...");
        else
            SetStatus("");
    }

    void OnConfirmPressed()
    {
        if (nameInputField == null) return;
        string chosen = nameInputField.text.Trim();

        if (chosen.Length < minLength)
        {
            SetStatus($"A név legalább {minLength} karakter legyen!");
            return;
        }

        // Mentés
        UserProgressManager.Instance?.CreateCharacter(chosen, GetSelectedMode());

        // Azonnal szinkronizál – az új karakter (game_mode-dal együtt) felkerül a szerverre
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

    // ── Segéd ─────────────────────────────────────────────────────────

    void SetStatus(string msg)
    {
        if (statusText == null) return;
        statusText.text = msg;
        statusText.gameObject.SetActive(!string.IsNullOrEmpty(msg));
    }
}
