using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

/// <summary>
/// Épités menü kezelő.
/// Az ÉPITÉS gomb megnyomására megjelenik a torony választó panel,
/// drag indításakor automatikusan bezárul.
/// </summary>
public class BuildMenuUI : MonoBehaviour
{
    public static BuildMenuUI Instance { get; private set; }

    [Header("UI elemek")]
    public GameObject menuPanel;    // a középső torony választó panel
    public GameObject buildButton;  // az ÉPITÉS gomb (opcionális – elrejthetjük ha nyitva van)

    [Header("Auto bezárás")]
    [Tooltip("0 = soha nem záródik be automatikusan")]
    public float autoCloseDelay = 0f;   // ennyi mp után záródik be automatikusan (0 = kikapcsolva)

    private bool isOpen = false;
    private float autoCloseTimer = 0f;
    private bool tooltipCloseReady = false;
    public bool IsOpen => isOpen;

    [Header("Tooltip mód")]
    public bool tooltipMode = false;    // a Toggle állítja

    /// <summary>A Tooltip Toggle OnValueChanged eseményéhez kösd be.</summary>
    public void SetTooltipMode(bool value) => tooltipMode = value;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        CloseMenu();

#if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL-en egérrel nehéz drag-and-drop-ot végezni (scroll view konfliktus),
        // ezért automatikusan bekapcsoljuk a tooltip módot:
        // kattintás a toronyra → kiválasztás, kattintás a pályára → lerakás.
        tooltipMode = true;
#endif
    }

    void Update()
    {
        if (isOpen && autoCloseDelay > 0f)
        {
            autoCloseTimer -= Time.deltaTime;
            if (autoCloseTimer <= 0f)
                CloseMenu();
        }

        // Tooltip kártya bezárása következő érintésre –
        // de csak akkor, ha NEM UI elemen történt a kattintás
        // (a BUILD/BACK gomb saját maga kezeli a bezárást)
        if (tooltipCloseReady)
        {
            bool anyTap = Input.GetMouseButtonDown(0) ||
                         (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began);
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (anyTap && !overUI)
            {
                tooltipCloseReady = false;
                if (TowerTooltipUI.Instance != null)
                    TowerTooltipUI.Instance.Hide();
            }
        }
    }

    /// <summary>Érintéskor visszaállítja a timert – a TowerShopItem hívja.</summary>
    public void ResetAutoClose()
    {
        autoCloseTimer = autoCloseDelay;
    }

    /// <summary>Egy frame késleltetés után elkezdi figyelni a következő érintést.</summary>
    public void StartTooltipCloseListener()
    {
        StartCoroutine(EnableTooltipCloseNextFrame());
    }

    IEnumerator EnableTooltipCloseNextFrame()
    {
        yield return null; // megvárja hogy az aktuális érintés véget érjen
        tooltipCloseReady = true;
    }

    // ── Gomb callback ─────────────────────────────────────────────

    /// <summary>Az ÉPITÉS gombhoz kösd be az Inspectorban (OnClick).</summary>
    public void OnBuildButtonPressed()
    {
        if (isOpen) CloseMenu();
        else OpenMenu();
    }

    // ── Menü állapot ──────────────────────────────────────────────

    public void OpenMenu()
    {
        isOpen = true;
        autoCloseTimer = autoCloseDelay;
        if (menuPanel != null) menuPanel.SetActive(true);
    }

    public void CloseMenu()
    {
        isOpen = false;
        if (menuPanel != null) menuPanel.SetActive(false);
    }
}
