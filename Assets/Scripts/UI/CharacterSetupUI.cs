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
    public TextMeshProUGUI statusText;      // hibaüzenetek / útmutató

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

        if (nameInputField != null)
        {
            nameInputField.characterLimit = maxLength;
            nameInputField.onValueChanged.AddListener(OnNameChanged);
        }

        if (setupPanel != null) setupPanel.SetActive(false);
        if (statusText != null) statusText.gameObject.SetActive(false);
    }

    // ── Megjelenítés ──────────────────────────────────────────────────

    public void Show()
    {
        if (setupPanel == null) return;
        setupPanel.SetActive(true);
        if (nameInputField != null) nameInputField.text = "";
        SetStatus("");
        nameInputField?.Select();
        FindObjectOfType<MainMenuUI>()?.SetCharacterCardVisible(false);
    }

    public void Hide()
    {
        if (setupPanel != null) setupPanel.SetActive(false);
        FindObjectOfType<MainMenuUI>()?.SetCharacterCardVisible(true);
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
        UserProgressManager.Instance?.CreateCharacter(chosen);

        Hide();
        CharacterSheetUI.Instance?.Show();
    }

    // ── Segéd ─────────────────────────────────────────────────────────

    void SetStatus(string msg)
    {
        if (statusText == null) return;
        statusText.text = msg;
        statusText.gameObject.SetActive(!string.IsNullOrEmpty(msg));
    }
}
