using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Buildings oldal – épületek / fejlesztések kezelése.
/// (Stub – tartalom később töltendő fel)
/// </summary>
public class BuildingsUI : MonoBehaviour
{
    public static BuildingsUI Instance { get; private set; }

    [Header("Panel")]
    public GameObject buildingsPanel;

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

        if (buildingsPanel != null) buildingsPanel.SetActive(false);
    }

    public void Show()
    {
        if (buildingsPanel == null) return;
        buildingsPanel.SetActive(true);
    }

    public void Hide()
    {
        if (DetailedCharacterUI.Instance != null)
        {
            if (buildingsPanel != null) buildingsPanel.SetActive(false);
            DetailedCharacterUI.Instance.Show();
        }
        else
        {
            PlayerPrefs.SetInt("OpenDetailedCharacter", 1);
            SceneManager.LoadScene("MainMenu");
        }
    }
}
