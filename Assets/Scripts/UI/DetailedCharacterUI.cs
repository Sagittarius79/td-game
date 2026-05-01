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
    public Button skillsButton;
    public Button BuildingsButton;
    public Button backButton;

    private CharacterEntryUI _card;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        if (skillsButton != null)
            skillsButton.onClick.AddListener(OnSkillsPressed);

        if (BuildingsButton != null)
            BuildingsButton.onClick.AddListener(OnBuildingsPressed);

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
    
    void OnBuildingsPressed()
    {
        SceneManager.LoadScene("Buildings");
    }
}
