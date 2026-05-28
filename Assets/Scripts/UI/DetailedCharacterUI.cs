using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Részletes karakter oldal – a főmenüben a karakter kártyára kattintva nyílik meg.
/// A karakter kártyát saját slotjában jeleníti meg.
/// </summary>
public class DetailedCharacterUI : MonoBehaviour
{
    public static DetailedCharacterUI Instance { get; private set; }

    [Header("Panel")]
    public GameObject detailPanel;

    [Header("Karakter kártya slot")]
    [Tooltip("Ide kerül a kártya a DetailedCharacterPanel-en belül")]
    public Transform  characterCardSlot;
    public GameObject characterEntryPrefab;

    [Header("Gombok")]
    public Button characterSheetButton;
    public Button skillsButton;
    public Button specialBuildingsButton;
    public Button monstersButton;
    public Button backButton;

    private CharacterEntryUI _card;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        if (characterSheetButton != null)
            characterSheetButton.onClick.AddListener(OnCharacterSheetPressed);

        if (skillsButton != null)
            skillsButton.onClick.AddListener(OnSkillsPressed);

        if (specialBuildingsButton != null)
            specialBuildingsButton.onClick.AddListener(OnSpecialBuildingsPressed);

        if (monstersButton != null)
            monstersButton.onClick.AddListener(OnMonstersPressed);

        if (backButton != null)
            backButton.onClick.AddListener(Hide);

        if (detailPanel != null) detailPanel.SetActive(false);

        if (PlayerPrefs.GetInt("OpenDetailedCharacter", 0) == 1)
        {
            PlayerPrefs.DeleteKey("OpenDetailedCharacter");
            Show();
        }
    }

    public void Show()
    {
        if (detailPanel == null) return;
        detailPanel.SetActive(true);
        RefreshCard();
        FindObjectOfType<MainMenuUI>()?.SetCharacterCardVisible(false);
    }

    public void Hide()
    {
        if (detailPanel != null) detailPanel.SetActive(false);
        FindObjectOfType<MainMenuUI>()?.SetCharacterCardVisible(true);
    }

    void RefreshCard()
    {
        if (characterCardSlot == null || characterEntryPrefab == null) return;

        var mgr = UserProgressManager.Instance;
        if (mgr == null || !mgr.HasCharacter) return;

        if (_card != null) Destroy(_card.gameObject);

        var go = Instantiate(characterEntryPrefab, characterCardSlot);
        _card = go.GetComponent<CharacterEntryUI>();
        _card?.SetupDisplay(mgr.Data);
    }

    void OnCharacterSheetPressed()
    {
        if (detailPanel != null) detailPanel.SetActive(false);
        CharacterInventoryUI.Instance?.Show();
    }

    void OnSkillsPressed()
    {
        if (SkillsUI.Instance == null)
        {
            Debug.LogError("OnSkillsPressed: SkillsUI.Instance NULL – a SkillsController nincs a scene-ben!");
            return;
        }
        if (detailPanel != null) detailPanel.SetActive(false);
        SkillsUI.Instance.Show();
    }
    
    void OnSpecialBuildingsPressed()
    {
        if (SpecialSkillsController.Instance == null)
        {
            Debug.LogError("OnSpecialBuildingsPressed: SpecialSkillsController.Instance NULL – nincs a scene-ben!");
            return;
        }
        if (detailPanel != null) detailPanel.SetActive(false);
        SpecialSkillsController.Instance.Show();
    }

    void OnMonstersPressed()
    {
        if (MonstersUI.Instance == null)
        {
            Debug.LogError("OnMonstersPressed: MonstersUI.Instance NULL – a MonstersUI nincs a scene-ben!");
            return;
        }
        if (detailPanel != null) detailPanel.SetActive(false);
        MonstersUI.Instance.Show();
    }
}
