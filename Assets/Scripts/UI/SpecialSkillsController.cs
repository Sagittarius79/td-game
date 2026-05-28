using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// SpecialSkills panel – a DetailedCharacterPanel SpecialBuildings gombjára nyílik meg.
/// </summary>
public class SpecialSkillsController : MonoBehaviour
{
    public static SpecialSkillsController Instance { get; private set; }

    [Header("Panel")]
    public GameObject specialSkillsPanel;

    [Header("Gombok")]
    public Button poisonButton;
    public Button javelinButton;
    public Button prismaButton;
    public Button buildingsButton;
    public Button turulButton;
    public Button twoDragonButton;
    public Button backButton;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (specialSkillsPanel != null) specialSkillsPanel.SetActive(false);
    }

    void Start()
    {
        if (PlayerPrefs.GetInt("OpenSpecialSkillsPanel", 0) == 1)
        {
            PlayerPrefs.DeleteKey("OpenSpecialSkillsPanel");
            Show();
        }

        if (poisonButton != null)
            poisonButton.onClick.AddListener(() => SceneManager.LoadScene("Poison_Skill_Tree"));

        if (javelinButton != null)
            javelinButton.onClick.AddListener(() => SceneManager.LoadScene("Javelin_skill_tree"));

        if (prismaButton != null)
            prismaButton.onClick.AddListener(() => SceneManager.LoadScene("Prisma_skill_tree"));

        if (buildingsButton != null)
            buildingsButton.onClick.AddListener(() => SceneManager.LoadScene("Buildings_skill_tree"));

        if (turulButton != null)
            turulButton.onClick.AddListener(() => SceneManager.LoadScene("Turul_skill_tree"));

        if (twoDragonButton != null)
            twoDragonButton.onClick.AddListener(() => SceneManager.LoadScene("Two_Dragon_Skill_Tree"));

        if (backButton != null)
            backButton.onClick.AddListener(Hide);
    }

    public void Show()
    {
        if (specialSkillsPanel == null) return;
        specialSkillsPanel.SetActive(true);
    }

    public void Hide()
    {
        if (specialSkillsPanel != null) specialSkillsPanel.SetActive(false);
        DetailedCharacterUI.Instance?.Show();
    }
}
