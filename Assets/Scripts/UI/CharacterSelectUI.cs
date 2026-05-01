using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Karakterválasztó képernyő – bejelentkezés után jelenik meg ha már vannak karakterek.
///
/// Mutatja a fiókhoz tartozó összes karaktert kártyákon.
/// Új karakter gomb → CharacterSetupUI
/// Kártya megnyomása → karakter aktiválása → CharacterSheetUI
///
/// Unity Editor beállítás:
///   1. MainMenu Canvas-ra adj hozzá egy Panel-t → "CharacterSelectPanel"
///   2. Erre tedd ezt a scriptet egy üres GameObject-en
///   3. Belül szükséges egy ScrollView (Content Szülő → characterListParent)
///   4. Hozz létre egy CharacterEntryPrefab-ot és kösd be
///   5. A CharacterSelectPanel legyen alapból INACTIVE
/// </summary>
public class CharacterSelectUI : MonoBehaviour
{
    public static CharacterSelectUI Instance { get; private set; }

    [Header("Panel")]
    public GameObject selectPanel;

    [Header("Fejléc")]
    public TextMeshProUGUI accountNameText;   // pl. "Kovács János  •  test@gmail.com"
    public TextMeshProUGUI titleText;         // pl. "Válassz karaktert"

    [Header("Karakterlista")]
    public Transform  characterListParent;    // ScrollView Content Transform
    public GameObject characterEntryPrefab;  // CharacterEntryPrefab
    public ScrollRect characterScrollRect;   // maga a ScrollRect komponens (a ScrollView-on)

    [Header("Gombok")]
    public Button newCharacterButton;
    public Button closeButton;               // bezárás (főmenübe visszatér, karakter választás nélkül)

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        if (newCharacterButton != null)
            newCharacterButton.onClick.AddListener(OnNewCharacterPressed);
        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);

        if (selectPanel != null) selectPanel.SetActive(false);
    }

    // ── Megjelenítés ──────────────────────────────────────────────────

    public void Show()
    {
        if (selectPanel == null) return;
        selectPanel.SetActive(true);
        Refresh();

        // Scroll visszaállítása a tetejére
        if (characterScrollRect != null)
            characterScrollRect.verticalNormalizedPosition = 1f;

        // Főmenü karakter kártya elrejtése (ne takarjon)
        FindObjectOfType<MainMenuUI>()?.SetCharacterCardVisible(false);
    }

    public void Hide()
    {
        if (selectPanel != null) selectPanel.SetActive(false);

        // Főmenü karakter kártya visszamutatása
        FindObjectOfType<MainMenuUI>()?.SetCharacterCardVisible(true);
    }

    // ── Lista frissítése ──────────────────────────────────────────────

    void Refresh()
    {
        var mgr = UserProgressManager.Instance;

        // Fejléc
        if (accountNameText != null && mgr != null)
        {
            string email = string.IsNullOrEmpty(mgr.AccountEmail) ? "" : $"  •  {mgr.AccountEmail}";
            accountNameText.text = $"{mgr.AccountDisplayName}{email}";
        }

        // Lista törlése
        if (characterListParent != null)
        {
            foreach (Transform child in characterListParent)
                Destroy(child.gameObject);
        }

        if (mgr == null || characterListParent == null || characterEntryPrefab == null) return;

        // Kártyák létrehozása
        var characters = mgr.GetAllCharacters();
        foreach (var ch in characters)
        {
            var go    = Instantiate(characterEntryPrefab, characterListParent);
            var entry = go.GetComponent<CharacterEntryUI>();
            entry?.Setup(ch, OnCharacterSelected, OnCharacterDeleted);
        }

        // Új karakter gomb – elrejtjük ha elértük a max karakterszámot (opcionális)
        if (newCharacterButton != null)
            newCharacterButton.gameObject.SetActive(true);
    }

    // ── Gomb kezelők ──────────────────────────────────────────────────

    void OnCharacterSelected(string characterId)
    {
        UserProgressManager.Instance?.SelectCharacter(characterId);
        Hide();

        // Főmenü kártyájának frissítése
        FindObjectOfType<MainMenuUI>()?.RefreshCharacterCard();

        CharacterSheetUI.Instance?.Show();
    }

    void OnCharacterDeleted(string characterId)
    {
        // Ha az utolsó karaktert törölték → névválasztóra irányít
        var mgr = UserProgressManager.Instance;
        if (mgr == null || !mgr.HasAnyCharacter)
        {
            Hide();
            CharacterSetupUI.Instance?.Show();
            return;
        }
        // Egyébként frissíti a listát
        Refresh();
    }

    void OnNewCharacterPressed()
    {
        Hide();
        CharacterSetupUI.Instance?.Show();
    }
}
