using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Monsters panel – az ellenségek / szörnyek kezelése.
/// A DetailedCharacterPanel "Monsters" gombjával nyílik meg.
/// </summary>
public class MonstersUI : MonoBehaviour
{
    public static MonstersUI Instance { get; private set; }

    [Header("Panel")]
    public GameObject monstersPanel;

    [Header("Gombok")]
    public Button backButton;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        if (backButton != null)
            backButton.onClick.AddListener(Hide);

        if (monstersPanel != null) monstersPanel.SetActive(false);
    }

    public void Show()
    {
        if (monstersPanel == null) return;
        monstersPanel.SetActive(true);
    }

    public void Hide()
    {
        if (DetailedCharacterUI.Instance != null)
        {
            if (monstersPanel != null) monstersPanel.SetActive(false);
            DetailedCharacterUI.Instance.Show();
        }
        else
        {
            PlayerPrefs.SetInt("OpenDetailedCharacter", 1);
            SceneManager.LoadScene("MainMenu");
        }
    }
}
