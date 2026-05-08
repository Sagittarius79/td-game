using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PvP szörny küldő panel – kerek gomb megnyomásakor jelenik meg.
/// Hasonló a BuildMenuUI-hoz: gomb → panel nyílik → bezárul küldés után.
///
/// Unity Editor beállítás:
///   1. Adj hozzá egy kerek Button-t bal alulra → sendButton mezőbe
///   2. Hozz létre egy Panel-t → sendPanel mezőbe
///   3. A sendPanel-be tedd bele a PvPSendItem-eket (egy per szörny)
///   4. sendableEnemies listába add hozzá a SendableEnemyDefinition asset-eket
///      (ugyanolyan sorrendben mint a PvPSendItem-ek a panelben!)
/// </summary>
public enum SendTarget
{
    OpponentOnly,   // csak az ellenfélnek küldi
    Everyone        // az ellenfelnek ÉS saját magának is
}

public class PvPSendPanel : MonoBehaviour
{
    public static PvPSendPanel Instance { get; private set; }

    [Header("UI elemek")]
    public GameObject sendButton;   // kerek gomb bal alul
    public GameObject sendPanel;    // szörny kínálat panel
    [Tooltip("Teljes képernyős átlátszó Image – blokkolja a kattintásokat a panel mögött.\n" +
             "Hozz létre egy UI Image-et a Canvas-on, Raycast Target = true, alpha = 0,\n" +
             "mérete töltse ki a teljes képernyőt, és legyen a sendPanel ALATT a hierarchiában.")]
    public Button backdropButton;   // kattintásra bezárja a panelt

    [Header("Küldhető szörnyek (sorrendben!)")]
    public SendableEnemyDefinition[] sendableEnemies;

    [Header("Küldés célpontja")]
    [Tooltip("Csak ellenfél: PvP módban csak az ellenfelnek küldi a szörnyet.\n" +
             "Mindenki: az ellenfelnek ÉS saját magának is küldi (mindkét játékosnál megjelenik).")]
    public SendTarget sendTarget = SendTarget.OpponentOnly;


    [Header("Küldési zárolás")]
    [Tooltip("Ennyi másodperccel a hullám előtt már nem lehet küldeni")]
    public float sendLockBeforeWave = 4f;
    [Tooltip("Ez a hang szól, ha valaki a zárolás alatt próbál küldeni")]
    public AudioClip sendLockedSound;
    [Tooltip("A SendPanel Image komponense – zároláskor színt vált")]
    public Image sendPanelImage;
    [Tooltip("Panel színe normál állapotban")]
    public Color panelNormalColor = Color.white;
    [Tooltip("Panel színe zároláskor (nem lehet küldeni)")]
    public Color panelLockedColor = new Color(1f, 0.3f, 0.3f, 1f);

    public bool IsOpen { get; private set; } = false;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        if (backdropButton != null)
            backdropButton.onClick.AddListener(ClosePanel);

        ClosePanel();

        // TESZT: mindig látható – éles verzióhoz visszaállítani PvP ellenőrzésre
        if (sendButton != null) sendButton.SetActive(true);
    }

    void Update()
    {
        UpdatePanelColor();
    }

    void UpdatePanelColor()
    {
        if (sendPanelImage == null) return;
        sendPanelImage.color = IsSendLocked ? panelLockedColor : panelNormalColor;
    }

    // ── Gomb callback (Inspectorból kösd be) ────────────────────────

    public void OnSendButtonPressed()
    {
        if (IsOpen) ClosePanel();
        else        OpenPanel();
    }

    // ── Panel állapot ────────────────────────────────────────────────

    public void OpenPanel()
    {
        IsOpen = true;
        if (sendPanel      != null) sendPanel.SetActive(true);
        if (backdropButton != null) backdropButton.gameObject.SetActive(true);
    }

    public void ClosePanel()
    {
        IsOpen = false;
        if (sendPanel      != null) sendPanel.SetActive(false);
        if (backdropButton != null) backdropButton.gameObject.SetActive(false);
    }

    // ── Szörny küldés (PvPSendItem hívja) ───────────────────────────

    /// <summary>Igaz, ha a hullámig kevesebb mint sendLockBeforeWave másodperc van hátra.</summary>
    public bool IsSendLocked =>
        WaveManager.Instance != null &&
        WaveManager.Instance.NextWaveCountdown <= sendLockBeforeWave &&
        WaveManager.Instance.NextWaveCountdown > 0f;

    /// <summary>A PvPSendItem hívja, amikor a játékos rábök egy szörnyre.</summary>
    public void TrySendEnemy(int enemyIndex)
    {
        if (sendableEnemies == null || enemyIndex < 0 || enemyIndex >= sendableEnemies.Length) return;

        // Küldés zárolva – hullám közeledik
        if (IsSendLocked)
        {
            AudioManager.Instance?.PlaySFX(sendLockedSound);
            return;
        }

        var def = sendableEnemies[enemyIndex];

        if (!GameManager.Instance.CanAfford(def.goldCost))
        {
            UIManager.Instance?.ShowNotEnoughGold();
            return;
        }

        GameManager.Instance.SpendGold(def.goldCost);

        int count = Mathf.Max(1, def.sendCount);

        // XP gyűjtés: az összes küldött szörny maxHealth-je = szerzett XP
        if (def.prefab != null)
        {
            var enemyComp = def.prefab.GetComponent<Enemy>();
            if (enemyComp != null)
                XPManager.Instance?.AddSentEnemyXP(enemyComp.maxHealth * count);
        }
        bool isPvP = NetworkGameManager.Instance != null && NetworkGameManager.Instance.IsPvPMode;

        if (sendTarget == SendTarget.Everyone && !isPvP)
        {
            if (WaveManager.Instance != null)
                WaveManager.Instance.AddPvPGroup(def.prefab, count, 0,
                    silent: false, def.minSpawnDelay, def.maxSpawnDelay);
            if (WavePreviewUI.Instance != null)
                WavePreviewUI.Instance.AddPvPEnemyToPreview(def.icon, def.enemyName);
        }

        // PvP módban elküldi az összes ellenfélnek (egyszer – a count a def-ben van)
        if (isPvP)
            NetworkGameManager.Instance.SendEnemyToAllOpponents(enemyIndex);
        else if (!isPvP && sendTarget == SendTarget.OpponentOnly)
        {
            if (WaveManager.Instance != null)
                WaveManager.Instance.AddPvPGroup(def.prefab, count, 0,
                    silent: false, def.minSpawnDelay, def.maxSpawnDelay);
            if (WavePreviewUI.Instance != null)
                WavePreviewUI.Instance.AddPvPEnemyToPreview(def.icon, def.enemyName);
        }

        VibrateShort();
    }

    // ── Rezgés ──────────────────────────────────────────────────────

    void VibrateShort() => VibrationHelper.VibrateShort();

    // ── NetworkGameManager-nek: prefab lekérdezés ───────────────────

    /// <summary>NetworkGameManager használja bejövő szörny spawnolásakor.</summary>
    public GameObject GetEnemyPrefab(int enemyIndex)
    {
        if (sendableEnemies == null || enemyIndex < 0 || enemyIndex >= sendableEnemies.Length) return null;
        return sendableEnemies[enemyIndex].prefab;
    }

    /// <summary>NetworkGameManager használja a preview frissítéséhez.</summary>
    public SendableEnemyDefinition GetEnemyDefinition(int enemyIndex)
    {
        if (sendableEnemies == null || enemyIndex < 0 || enemyIndex >= sendableEnemies.Length) return null;
        return sendableEnemies[enemyIndex];
    }
}
