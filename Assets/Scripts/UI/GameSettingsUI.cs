using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Játék közbeni beállítások panel – fogaskerék ikonnal nyitható.
/// </summary>
public class GameSettingsUI : MonoBehaviour
{
    public static GameSettingsUI Instance { get; private set; }

    [Header("UI elemek")]
    [Tooltip("A fogaskerék gomb")]
    public Button gearButton;

    [Tooltip("A beállítások panel")]
    public GameObject settingsPanel;

    [Tooltip("Detailed Numbers toggle")]
    public Toggle detailedNumbersToggle;

    [Tooltip("Tower HP always visible toggle")]
    public Toggle towerHpAlwaysVisibleToggle;

    [Tooltip("Music Mute toggle")]
    public Toggle musicMuteToggle;

    [Header("Feladás")]
    [Tooltip("Feladás gomb a settings panelen belül")]
    public Button quitButton;

    [Tooltip("Megerősítő panel (Biztosan feladod?)")]
    public GameObject confirmPanel;

    [Tooltip("Igen gomb a confirm panelen")]
    public Button confirmYesButton;

    [Tooltip("Nem gomb a confirm panelen")]
    public Button confirmNoButton;

    public string mainMenuSceneName = "MainMenu";

    // ── Statikus elérés a többi szkript számára ───────────────────
    public static bool DetailedNumbers { get; private set; }
    public static bool TowerHpAlwaysVisible { get; private set; }
    public static bool MusicMuted { get; private set; }

    private const string PREF_KEY          = "DetailedNumbers";
    private const string PREF_KEY_TOWER_HP = "TowerHpAlwaysVisible";
    private const string PREF_KEY_MUSIC    = "MusicMuted";

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Mentett beállítások betöltése (TowerHp alapból kikapcsolva ha nincs mentve)
        DetailedNumbers      = PlayerPrefs.GetInt(PREF_KEY, 0) == 1;
        TowerHpAlwaysVisible = PlayerPrefs.GetInt(PREF_KEY_TOWER_HP, 0) == 1;
        MusicMuted           = PlayerPrefs.GetInt(PREF_KEY_MUSIC, 0) == 1;

        // Ha a toggle nincs bekötve, alapból tiltjuk és töröljük az esetleges régi értéket
        if (towerHpAlwaysVisibleToggle == null)
        {
            TowerHpAlwaysVisible = false;
            PlayerPrefs.SetInt(PREF_KEY_TOWER_HP, 0);
        }
    }

    void Start()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (confirmPanel != null) confirmPanel.SetActive(false);

        if (detailedNumbersToggle != null)
        {
            detailedNumbersToggle.isOn = DetailedNumbers;
            detailedNumbersToggle.onValueChanged.AddListener(OnDetailedNumbersChanged);
        }

        if (towerHpAlwaysVisibleToggle != null)
        {
            towerHpAlwaysVisibleToggle.isOn = TowerHpAlwaysVisible;
            towerHpAlwaysVisibleToggle.onValueChanged.AddListener(OnTowerHpVisibilityChanged);
        }

        if (musicMuteToggle != null)
        {
            musicMuteToggle.isOn = MusicMuted;
            musicMuteToggle.onValueChanged.AddListener(OnMusicMuteChanged);
        }

        // Betöltéskor is alkalmazzuk a mentett mute állapotot
        AudioManager.Instance?.SetMusicMute(MusicMuted);

        if (gearButton != null)
            gearButton.onClick.AddListener(TogglePanel);

        if (quitButton != null)
            quitButton.onClick.AddListener(OnQuitClicked);

        if (confirmYesButton != null)
            confirmYesButton.onClick.AddListener(OnConfirmYes);

        if (confirmNoButton != null)
            confirmNoButton.onClick.AddListener(OnConfirmNo);

        // Kezdeti állapot alkalmazása az összes toronyra
        ApplyTowerHpVisibility();
    }

    void OnQuitClicked()
    {
        if (confirmPanel != null)
            confirmPanel.SetActive(true);
    }

    void OnConfirmYes()
    {
        // PvP módban a NetworkGameManager.Disconnect() végzi a Shutdown()-t,
        // ami a szervernek is jelzi a kilépést (castle fallen-ként kezeli).
        if (NetworkGameManager.Instance != null && NetworkGameManager.Instance.IsPvPMode)
            NetworkGameManager.Instance.Disconnect();
        else
            SceneManager.LoadScene(mainMenuSceneName);
    }

    void OnConfirmNo()
    {
        if (confirmPanel != null)
            confirmPanel.SetActive(false);
    }

    void Update()
    {
        if (settingsPanel == null || !settingsPanel.activeSelf) return;

        bool tapped = Input.GetMouseButtonDown(0) ||
                      (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began);

        if (tapped && !IsPointerOverPanel())
            settingsPanel.SetActive(false);
    }

    bool IsPointerOverPanel()
    {
        var eventSystem = UnityEngine.EventSystems.EventSystem.current;
        return eventSystem != null && eventSystem.IsPointerOverGameObject();
    }

    void TogglePanel()
    {
        if (settingsPanel == null) return;
        settingsPanel.SetActive(!settingsPanel.activeSelf);
    }

    void OnDetailedNumbersChanged(bool value)
    {
        DetailedNumbers = value;
        PlayerPrefs.SetInt(PREF_KEY, value ? 1 : 0);
        PlayerPrefs.Save();
    }

    void OnTowerHpVisibilityChanged(bool value)
    {
        TowerHpAlwaysVisible = value;
        PlayerPrefs.SetInt(PREF_KEY_TOWER_HP, value ? 1 : 0);
        PlayerPrefs.Save();
        ApplyTowerHpVisibility();
    }

    void OnMusicMuteChanged(bool value)
    {
        MusicMuted = value;
        PlayerPrefs.SetInt(PREF_KEY_MUSIC, value ? 1 : 0);
        PlayerPrefs.Save();
        AudioManager.Instance?.SetMusicMute(value);
    }

    /// <summary>
    /// Frissíti az összes torony HP sávjának láthatóságát a beállítás alapján.
    /// </summary>
    public static void ApplyTowerHpVisibility()
    {
        foreach (var tower in Tower.AllTowers)
            tower.RefreshHpBarVisibility();
    }
}
