using UnityEngine;
using UnityEngine.UI;
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

    // ── Statikus elérés a többi szkript számára ───────────────────
    public static bool DetailedNumbers { get; private set; }
    public static bool TowerHpAlwaysVisible { get; private set; }

    private const string PREF_KEY         = "DetailedNumbers";
    private const string PREF_KEY_TOWER_HP = "TowerHpAlwaysVisible";

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Mentett beállítások betöltése (TowerHp alapból kikapcsolva ha nincs mentve)
        DetailedNumbers      = PlayerPrefs.GetInt(PREF_KEY, 0) == 1;
        TowerHpAlwaysVisible = PlayerPrefs.GetInt(PREF_KEY_TOWER_HP, 0) == 1;

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

        if (gearButton != null)
            gearButton.onClick.AddListener(TogglePanel);

        // Kezdeti állapot alkalmazása az összes toronyra
        ApplyTowerHpVisibility();
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

    /// <summary>
    /// Frissíti az összes torony HP sávjának láthatóságát a beállítás alapján.
    /// </summary>
    public static void ApplyTowerHpVisibility()
    {
        foreach (var tower in Tower.AllTowers)
            tower.RefreshHpBarVisibility();
    }
}
