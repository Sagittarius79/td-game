using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Skills oldal – három harci stílus közül lehet választani.
/// </summary>
public class SkillsUI : MonoBehaviour
{
    public static SkillsUI Instance { get; private set; }

    [Header("Panel")]
    public GameObject skillsPanel;

    [Header("Gombok")]
    public Button archerButton;
    public Button archerLvl2Button;
    public Button stoneButton;
    public Button stoneButtonLvl2;
    public Button magicButton;
    public Button magicButtonLvl2;
    public Button backButton;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        var panel = skillsPanel != null ? skillsPanel : transform.parent?.gameObject;
        panel?.SetActive(false);
    }

    void Start()
    {
        if (PlayerPrefs.GetInt("OpenSkillsPanel", 0) == 1)
        {
            PlayerPrefs.DeleteKey("OpenSkillsPanel");
            Show();
        }

        if (archerButton     != null) archerButton.onClick.AddListener(OnArcherPressed);
        if (archerLvl2Button != null) archerLvl2Button.onClick.AddListener(OnArcherLvl2Pressed);
        if (stoneButton      != null) stoneButton.onClick.AddListener(OnStonePressed);
        if (stoneButtonLvl2  != null) stoneButtonLvl2.onClick.AddListener(OnStoneLvl2Pressed);
        if (magicButton      != null) magicButton.onClick.AddListener(OnMagicPressed);
        if (magicButtonLvl2  != null) magicButtonLvl2.onClick.AddListener(OnMagicLvl2Pressed);
        if (backButton       != null) backButton.onClick.AddListener(Hide);
    }

    // ── Megjelenítés ──────────────────────────────────────────────────

    public void Show()
    {
        if (skillsPanel == null) return;
        skillsPanel.SetActive(true);
    }

    public void Hide()
    {
        if (skillsPanel != null) skillsPanel.SetActive(false);
        DetailedCharacterUI.Instance?.Show();
    }

    // ── Gomb kezelők ──────────────────────────────────────────────────

    void OnArcherPressed()
    {
        SceneManager.LoadScene("ArcherSkillTree");
    }

    void OnArcherLvl2Pressed()
    {
        SceneManager.LoadScene("ArcherSkillTreelvl2");
    }

    void OnStonePressed()
    {
        SceneManager.LoadScene("StoneSkillTree");
    }

    void OnStoneLvl2Pressed()
    {
        SceneManager.LoadScene("StoneSkillTreeLvl2");
    }

    void OnMagicPressed()
    {
        SceneManager.LoadScene("Magic");
    }
    
    void OnMagicLvl2Pressed()
    {
        SceneManager.LoadScene("MagicSkillTreeLvl2");
    }
}
