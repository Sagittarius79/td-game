using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

/// <summary>
/// UI Menedzser – HUD elemek frissítése.
/// Arany, hullám számláló, kastély HP, visszaszámláló, játék vége képernyő.
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("HUD – Felső sáv")]
    public TextMeshProUGUI goldText;
    public TextMeshProUGUI waveText;
    public TextMeshProUGUI nextWaveText;
    [Tooltip("Pl. \"Last wave: +12 G\" – az előző hullámban szerzett arany. Az 1. hullámnál rejtve.")]
    public TextMeshProUGUI lastWaveGoldText;

    [Header("Kastély HP")]
    public Image castleHPBar;
    public TextMeshProUGUI castleHPText;

    [Header("Visszaszámláló (következő hullám)")]
    public GameObject countdownPanel;
    public TextMeshProUGUI countdownText;

    [Header("Értesítések")]
    public TextMeshProUGUI notificationText;
    public float notificationDuration = 2f;

    [Header("Játék vége képernyő")]
    public GameObject gameOverPanel;
    public TextMeshProUGUI gameOverTitle;
    public TextMeshProUGUI gameOverSubtitle;
    public Button restartButton;
    [Tooltip("Megnyitja a leaderboard weboldalt")]
    public Button statisticButton;
    [Tooltip("A főmenü scene neve (ahol a Solo / PvP választó van)")]
    public string mainMenuSceneName = "MainMenu";

    [Header("Hullám kezdő felirat")]
    public GameObject waveAnnouncerPanel;
    public TextMeshProUGUI waveAnnouncerText;

    [Header("XP kijelzés – játék vége")]
    [Tooltip("Pl. \"+150 XP\" – az ebben a meccsben szerzett XP")]
    public TextMeshProUGUI xpGainedText;
    [Tooltip("Pl. \"Összesen: 850 XP • Szint 3\" – aktuális összesítés")]
    public TextMeshProUGUI xpTotalText;
    [Tooltip("Pl. \"Level Up! 2 → 3\" – csak szintlépéskor látható, egyébként rejtve")]
    public TextMeshProUGUI levelUpText;
    [Tooltip("Pl. \"+2 skill pont\" – ebben a meccsben szerzett skill pontok")]
    public TextMeshProUGUI skillPointsGainedText;
    [Tooltip("Pl. \"Küldött HP: 1200\" – össz elküldött szörny HP")]
    public TextMeshProUGUI sentHPText;
    [Tooltip("Pl. \"Helyezés: 3.\" – hányadik lett a játékos")]
    public TextMeshProUGUI placementText;
    [Tooltip("Pl. \"XP: 1200 / 3 = 400\" – a számítás lebontva")]
    public TextMeshProUGUI xpBreakdownText;

    private Coroutine notificationCoroutine;
    private int _levelBeforeGame = 1;   // szint a meccs előtt – szintlépés detektáláshoz

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        // Esemény feliratkozások
        GameManager.Instance.OnGoldChanged  += UpdateGold;
        GameManager.Instance.OnWaveChanged  += UpdateWave;
        GameManager.Instance.OnGameOver     += ShowGameOver;
        GameManager.Instance.OnVictory      += ShowVictory;

        Castle.Instance.OnHealthChanged     += UpdateCastleHP;

        WaveManager.Instance.OnWaveStarted  += OnWaveStarted;
        WaveManager.Instance.OnWaveCompleted+= OnWaveCompleted;

        // Kezdeti értékek
        UpdateGold(GameManager.Instance.CurrentGold);
        UpdateCastleHP(Castle.Instance.CurrentHealth, Castle.Instance.maxHealth);
        UpdateWave(0);

        if (gameOverPanel    != null) gameOverPanel.SetActive(false);
        if (levelUpText      != null) levelUpText.gameObject.SetActive(false);
        if (lastWaveGoldText != null) lastWaveGoldText.gameObject.SetActive(false);

        // XP szint a meccs elején – szintlépés detektáláshoz
        _levelBeforeGame = UserProgressManager.Instance?.Level ?? 1;
        if (waveAnnouncerPanel != null) waveAnnouncerPanel.SetActive(false);
        if (restartButton != null)
        {
            restartButton.onClick.RemoveAllListeners();
            restartButton.onClick.AddListener(ReturnToMainMenu);
        }
        if (statisticButton != null)
        {
            statisticButton.onClick.RemoveAllListeners();
            statisticButton.onClick.AddListener(OpenLeaderboardWeb);
        }
    }

    void Update()
    {
        // Visszaszámláló frissítése
        if (WaveManager.Instance != null && countdownText != null)
        {
            float countdown = WaveManager.Instance.NextWaveCountdown;
            bool show = countdown > 0f && WaveManager.Instance.AliveEnemies == 0;
            if (countdownPanel != null) countdownPanel.SetActive(show);
            if (show) countdownText.text = $"Következő hullám: {countdown:F1}s";
        }

        // Survival gold frissítése – minden frame, de az érték csak másodpercenként változik
        if (lastWaveGoldText != null && GameManager.Instance != null)
            RefreshSurvivalGoldDisplay();
    }

    void RefreshSurvivalGoldDisplay()
    {
        float current = GameManager.Instance.CurrentWaveSurvivalGold;
        bool show = current >= 0.05f;
        lastWaveGoldText.gameObject.SetActive(show);
        if (show)
            lastWaveGoldText.text = $"Troop Gold: +{current:F1} G";
    }

    // ── Arany ─────────────────────────────────────────────────────

    void UpdateGold(int amount)
    {
        if (goldText != null) goldText.text = $"{amount} G";
    }

    // ── Hullám ────────────────────────────────────────────────────

    void UpdateWave(int wave)
    {
        if (waveText != null) waveText.text = wave == 0 ? "Felkészülj!" : $"Hullám {wave}";
    }

    void OnWaveStarted(int wave)
    {
        UpdateWave(wave);
        StartCoroutine(ShowWaveAnnouncer(wave));
    }

    void OnWaveCompleted(int wave)
    {
        ShowNotification($"Hullám {wave} visszaverve! ✓", Color.green);
    }

    IEnumerator ShowWaveAnnouncer(int wave)
    {
        if (waveAnnouncerPanel == null) yield break;
        if (waveAnnouncerText != null)
            waveAnnouncerText.text = $"HULLÁM {wave}";

        waveAnnouncerPanel.SetActive(true);
        yield return new WaitForSeconds(2f);
        waveAnnouncerPanel.SetActive(false);
    }

    // ── Kastély HP ────────────────────────────────────────────────

    void UpdateCastleHP(int current, int max)
    {
        if (castleHPBar != null)
            castleHPBar.fillAmount = (float)current / max;
        if (castleHPText != null)
            castleHPText.text = $"{current}/{max}";
    }

    // ── Értesítések ───────────────────────────────────────────────

    public void ShowNotEnoughGold()
    {
        ShowNotification("Nincs elég arany!", Color.yellow);
    }

    public void ShowNotification(string msg, Color color)
    {
        if (notificationText == null) return;
        if (notificationCoroutine != null)
            StopCoroutine(notificationCoroutine);
        notificationCoroutine = StartCoroutine(NotificationRoutine(msg, color));
    }

    IEnumerator NotificationRoutine(string msg, Color color)
    {
        notificationText.text  = msg;
        notificationText.color = color;
        notificationText.gameObject.SetActive(true);
        yield return new WaitForSeconds(notificationDuration);
        notificationText.gameObject.SetActive(false);
    }

    // ── Játék vége ────────────────────────────────────────────────

    void ShowGameOver()
    {
        if (gameOverPanel == null) return;
        gameOverPanel.SetActive(true);

        bool pvpMode = NetworkGameManager.Instance != null && NetworkGameManager.Instance.IsPvPMode;
        bool won     = false;

        if (pvpMode)
        {
            won = NetworkGameManager.Instance.LastResultWon;
            if (gameOverTitle    != null) gameOverTitle.text    = won ? "You WIN!" : "You LOSE!";
            if (gameOverSubtitle != null) gameOverSubtitle.text = won
                ? "Your opponent's castle has fallen!"
                : "Your castle has fallen!";
        }
        else
        {
            // Solo módban a ShowGameOver = vereség (a kastély elesett)
            if (gameOverTitle    != null) gameOverTitle.text    = "DEFEAT";
            if (gameOverSubtitle != null) gameOverSubtitle.text =
                $"You reached wave {GameManager.Instance.CurrentWave}.";
        }

        ShowXPResult(won);
    }

    void ShowVictory()
    {
        if (gameOverPanel == null) return;
        gameOverPanel.SetActive(true);
        if (gameOverTitle    != null) gameOverTitle.text    = "VICTORY!";
        if (gameOverSubtitle != null) gameOverSubtitle.text = "You defended the castle!";

        ShowXPResult(won: true);
    }

    void ReturnToMainMenu()
    {
        // PvP módban előbb lecsatlakozunk, utána töltjük a főmenüt
        if (NetworkGameManager.Instance != null && NetworkGameManager.Instance.IsPvPMode)
            NetworkGameManager.Instance.Disconnect();

        SceneManager.LoadScene(mainMenuSceneName);
    }

    void OpenLeaderboardWeb()
    {
        Application.OpenURL("https://kakaoo123.asuscomm.com/#board");
    }

    /// <summary>XP juttatása és megjelenítése a játék vége képernyőn.</summary>
    void ShowXPResult(bool won)
    {
        if (XPManager.Instance == null) return;

        long gained   = XPManager.Instance.AwardAndReset(won);
        long total    = UserProgressManager.Instance?.TotalXP ?? 0;
        int  newLevel = UserProgressManager.Instance?.Level ?? 1;

        if (xpGainedText != null)
            xpGainedText.text = gained > 0 ? $"+{gained} XP" : "0 XP";

        if (xpTotalText != null)
            xpTotalText.text = $"Total: {total} XP  •  Level {newLevel}";

        // Szintlépés jelzés
        if (levelUpText != null)
        {
            bool leveledUp = newLevel > _levelBeforeGame;
            levelUpText.gameObject.SetActive(leveledUp);
            if (leveledUp)
                levelUpText.text = $"LEVEL UP!  {_levelBeforeGame} → {newLevel}";
        }

        // Skill pont nyeremény
        if (skillPointsGainedText != null)
        {
            int gainedSkillPoints = newLevel - _levelBeforeGame;
            skillPointsGainedText.text = gainedSkillPoints > 0
                ? $"+{gainedSkillPoints} skill point"
                : "0 skill pont";
        }

        // Küldött HP, helyezés, XP lebontás
        var xpMgr = XPManager.Instance;
        if (xpMgr != null)
        {
            if (sentHPText != null)
                sentHPText.text = $"Sent HP: {xpMgr.LastMatchSentHP}";

            if (placementText != null)
            {
                string placement = xpMgr.LastMatchPlacement == 1
                    ? "Wictory, full XP!"
                    : $"{xpMgr.LastMatchPlacement}. place";
                placementText.text = placement;
            }

            if (xpBreakdownText != null)
            {
                int divisor = xpMgr.LastMatchPlacement;
                string capNote = xpMgr.LastMatchSoloLevelCapped ? "(in Solo after Lvl10) " : "";
                if (divisor > 1)
                    xpBreakdownText.text = $"{xpMgr.LastMatchSentHP} HP / {divisor} = {capNote}{xpMgr.LastMatchAwardedXP} XP";
                else
                    xpBreakdownText.text = $"{xpMgr.LastMatchSentHP} HP = {capNote}{xpMgr.LastMatchAwardedXP} XP";
            }
        }
    }
}
