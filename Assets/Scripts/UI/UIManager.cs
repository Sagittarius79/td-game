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
    [Tooltip("Megnyitja a SpectatePanel-t (csak PvP módban látható)")]
    public Button spectateButton;
    public SpectatePanel spectatePanel;
    [Tooltip("A főmenü scene neve (ahol a Solo / PvP választó van)")]
    public string mainMenuSceneName = "MainMenu";

    [Header("Hullám kezdő felirat")]
    public GameObject waveAnnouncerPanel;
    public TextMeshProUGUI waveAnnouncerText;

    [Header("Kiesési értesítő")]
    [Tooltip("Képernyő közepén megjelenő panel – pl. félátlátszó sötét sáv")]
    public GameObject eliminationPanel;
    [Tooltip("A kiesett játékos nevét megjelenítő szöveg")]
    public TextMeshProUGUI eliminationText;
    [Tooltip("Hang, ami lejátszódik kieséskor")]
    public AudioClip eliminationSound;
    [Tooltip("Másodpercek amíg látható az értesítő")]
    public float eliminationDuration = 3f;

    [Header("Győzelmi hang")]
    [Tooltip("Hang, ami lejátszódik amikor a játékos nyer")]
    public AudioClip victorySound;

    [Header("Sebesség gomb")]
    public Button speedToggleButton;
    [Tooltip("A gomb szövege (TextMeshPro)")]
    public TextMeshProUGUI speedToggleText;

    [Header("Charm jutalom – játék vége")]
    [Tooltip("A charm jutalom panel (alapból inaktív, AwardEndGameCharm aktiválja)")]
    public GameObject      charmRewardPanel;
    [Tooltip("A charm ikon Image")]
    public Image           charmRewardIcon;
    [Tooltip("Az ikon mögötti ragyogás Image (lágy kör sprite, CharmIcon gyereke)")]
    public Image           charmGlowImage;
    [Tooltip("Pl. \"Tüzes Nyíl\"")]
    public TextMeshProUGUI charmRewardName;
    [Tooltip("Pl. \"Ritka\"")]
    public TextMeshProUGUI charmRewardRarity;
    [Tooltip("Hang ami lejátszódik charm megszerzésekor")]
    public AudioClip       charmRewardSound;

    [Header("Megölt szörnyek – játék vége")]
    public MonsterKillsUI monsterKillsUI;

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
    private Coroutine eliminationCoroutine;
    private int _levelBeforeGame = 1;
    private bool _isFastSpeed = false;

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
        if (spectateButton != null)
        {
            spectateButton.onClick.RemoveAllListeners();
            spectateButton.onClick.AddListener(() => spectatePanel?.Open(gameOverPanel));
        }
        if (speedToggleButton != null)
        {
            speedToggleButton.onClick.RemoveAllListeners();
            speedToggleButton.onClick.AddListener(ToggleSpeed);
        }
        UpdateSpeedButtonText();
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

    // ── Kiesési értesítő ─────────────────────────────────────────

    public void ShowEliminationNotice(string playerName)
    {
        AudioManager.Instance?.PlaySFX(eliminationSound);
        if (eliminationPanel == null) return;
        if (eliminationCoroutine != null) StopCoroutine(eliminationCoroutine);
        eliminationCoroutine = StartCoroutine(EliminationRoutine(playerName));
    }

    IEnumerator EliminationRoutine(string playerName)
    {
        if (eliminationText != null)
            eliminationText.text = playerName;
        eliminationPanel.SetActive(true);
        yield return new WaitForSeconds(eliminationDuration);
        eliminationPanel.SetActive(false);
    }

    // ── Játék vége ────────────────────────────────────────────────

    void ShowGameOver()
    {
        if (gameOverPanel == null) return;
        Time.timeScale = 1f;
        _isFastSpeed = false;
        UpdateSpeedButtonText();
        gameOverPanel.SetActive(true);

        bool pvpMode = NetworkGameManager.Instance != null && NetworkGameManager.Instance.IsPvPMode;
        bool won     = false;

        if (spectateButton != null)
            spectateButton.gameObject.SetActive(pvpMode);

        if (pvpMode)
        {
            won = NetworkGameManager.Instance.LastResultWon;
            if (won) AudioManager.Instance?.PlaySFX(victorySound);
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

        UpdateSSFStats();
        ShowXPResult(won);
        AwardEndGameCharm(won);
        monsterKillsUI?.Populate();
    }

    void ShowVictory()
    {
        if (gameOverPanel == null) return;
        Time.timeScale = 1f;
        _isFastSpeed = false;
        UpdateSpeedButtonText();
        gameOverPanel.SetActive(true);
        AudioManager.Instance?.PlaySFX(victorySound);
        if (gameOverTitle    != null) gameOverTitle.text    = "VICTORY!";
        if (gameOverSubtitle != null) gameOverSubtitle.text = "You defended the castle!";

        UpdateSSFStats();
        ShowXPResult(won: true);
        AwardEndGameCharm(won: true);
        monsterKillsUI?.Populate();
    }

    /// <summary>
    /// Játék végén esély-alapú charm jutalom.
    /// PvP:      hullámonként +0.5% + (győzelem ? +10%), max 70%.
    /// Solo/SSF: hullám / 3 %, max 70% (nincs győzelem bónusz).
    /// Drop esetén szint sorsolás (Lvl3 2% / Lvl2 8% / Lvl1 90%), majd véletlen charm az adott szintből.
    /// </summary>
    void AwardEndGameCharm(bool won)
    {
        if (charmRewardPanel != null) charmRewardPanel.SetActive(false);

        // ── Drop esély kiszámítása ──────────────────────────────
        int  wave  = GameManager.Instance?.CurrentWave ?? 0;
        bool isPvP = NetworkGameManager.Instance != null && NetworkGameManager.Instance.IsPvPMode;

        float chance;
        if (isPvP)
            chance = wave * 0.5f + (won ? 10f : 0f);   // PvP szabály
        else
            chance = wave / 3f;                        // Solo + SSF szabály
        chance = Mathf.Min(chance, 70f);

        bool dropped = Random.value * 100f < chance;
        Debug.Log($"[UIManager] Charm drop – mód:{(isPvP ? "PvP" : "Solo/SSF")} wave:{wave} won:{won} → esély:{chance:0.#}%  dobás:{(dropped ? "SIKER" : "nincs")}");
        if (!dropped) return;

        // ── Szint sorsolás: Lvl3 2% / Lvl2 8% / Lvl1 90% ────────
        int level = RollCharmLevel();

        // ── Véletlen charm az adott szintből ────────────────────
        var def = CharmRegistry.Instance?.GetRandomByLevel(level)
               ?? CharmRegistry.Instance?.GetRandom(); // fallback ha nincs ilyen szint
        if (def == null)
        {
            Debug.LogWarning("[UIManager] AwardEndGameCharm: nincs elérhető charm.");
            return;
        }

        bool added = UserProgressManager.Instance?.AddCharm(def.id) ?? false;

        // ── Panel feltöltése + animáció ─────────────────────────
        if (charmRewardPanel != null)
        {
            charmRewardPanel.SetActive(true);

            if (charmRewardIcon != null)
            {
                charmRewardIcon.sprite  = def.icon;
                charmRewardIcon.enabled = def.icon != null;
            }

            if (charmRewardName != null)
                charmRewardName.text = added
                    ? $"{def.displayName}  <size=70%>Lvl{def.level}</size>"
                    : $"{def.displayName}\n<size=70%><color=red>Inventory full!</color></size>";

            if (charmRewardRarity != null)
            {
                charmRewardRarity.text  = RarityLabel(def.rarity);
                charmRewardRarity.color = RarityColor(def.rarity);
            }

            StartCoroutine(CharmRewardAnimation(charmRewardPanel, def.rarity));
        }

        if (added && charmRewardSound != null)
            AudioManager.Instance?.PlaySFX(charmRewardSound);

        Debug.Log($"[UIManager] Charm jutalom: {def.displayName} (Lvl{def.level}) – added:{added}");
    }

    /// <summary>Szint sorsolás: Lvl3 2%, Lvl2 8%, Lvl1 90%.</summary>
    static int RollCharmLevel()
    {
        float r = Random.value * 100f;
        if (r < 2f)  return 3;   // 0–2%
        if (r < 10f) return 2;   // 2–10%
        return 1;                // 10–100%
    }

    /// <summary>
    /// Pop-in (pici méretről forogva nő, ragyogás megjelenik) →
    /// 3s pulzáló ragyogás → fade-out animáció.
    /// </summary>
    IEnumerator CharmRewardAnimation(GameObject panel, CharmRarity rarity)
    {
        // CanvasGroup a fade-hez – ha nincs, hozzáadjuk
        var cg = panel.GetComponent<CanvasGroup>();
        if (cg == null) cg = panel.AddComponent<CanvasGroup>();

        var rt = panel.GetComponent<RectTransform>();

        // Ragyogás alap színe = rarity szín, de kezdetben 0 alfával
        Color glowBase = RarityColor(rarity);
        if (charmGlowImage != null)
        {
            glowBase.a = 0f;
            charmGlowImage.color = glowBase;
            charmGlowImage.rectTransform.localScale = Vector3.one;
        }

        // Kezdőállapot: kicsi, teljesen látható, elforgatva
        rt.localScale    = Vector3.zero;
        rt.localRotation = Quaternion.Euler(0f, 0f, 4f * 360f);
        cg.alpha         = 1f;
        panel.SetActive(true);

        // ── Pop-in fázis (3s, 4 teljes fordulat) ─────────────────
        const float popDuration = 3f;
        float elapsed = 0f;

        while (elapsed < popDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / popDuration);

            // EaseOutBack skála + forgás
            rt.localScale    = Vector3.one * EaseOutBack(t);
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(4f * 360f, 0f, t));

            // Ragyogás alpha fokozatosan jelenik meg a pop-in alatt
            if (charmGlowImage != null)
            {
                glowBase.a = t * 0.75f;
                charmGlowImage.color = glowBase;
            }

            yield return null;
        }

        rt.localScale    = Vector3.one;
        rt.localRotation = Quaternion.identity;

        // ── Pulzáló ragyogás fázis (3s) ──────────────────────────
        const float holdDuration = 3f;
        elapsed = 0f;

        while (elapsed < holdDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            if (charmGlowImage != null)
            {
                // Alpha: 0.35 – 0.80 között lélegzik ~1.5Hz-en
                float pulse = (Mathf.Sin(elapsed * Mathf.PI * 1.5f) + 1f) * 0.5f;
                glowBase.a = Mathf.Lerp(0.35f, 0.80f, pulse);
                charmGlowImage.color = glowBase;

                // Méret is kicsit pulzál: 1.0 – 1.2×
                float gs = Mathf.Lerp(1.0f, 1.2f, pulse);
                charmGlowImage.rectTransform.localScale = Vector3.one * gs;
            }

            yield return null;
        }

        // ── Fade-out fázis (0.6s) ─────────────────────────────────
        const float fadeDuration = 0.6f;
        elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            cg.alpha = 1f - Mathf.Clamp01(elapsed / fadeDuration);
            yield return null;
        }

        cg.alpha = 0f;
        panel.SetActive(false);

        // Visszaállítás a következő megjelenítéshez
        cg.alpha = 1f;
        if (charmGlowImage != null)
        {
            glowBase.a = 0f;
            charmGlowImage.color = glowBase;
            charmGlowImage.rectTransform.localScale = Vector3.one;
        }
    }

    /// <summary>Ease-Out-Back görbe: túllő 1-en, majd visszaáll.</summary>
    static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    static string RarityLabel(CharmRarity r) => r switch
    {
        CharmRarity.Rare      => "Rare",
        CharmRarity.Epic      => "Epic",
        CharmRarity.Legendary => "Legendary",
        _                     => "Common",
    };

    static Color RarityColor(CharmRarity r) => r switch
    {
        CharmRarity.Rare      => new Color(0.2f, 0.5f, 1.0f),
        CharmRarity.Epic      => new Color(0.7f, 0.2f, 1.0f),
        CharmRarity.Legendary => new Color(1.0f, 0.7f, 0.1f),
        _                     => new Color(0.7f, 0.7f, 0.7f),
    };

    /// <summary>
    /// SSF karakternél elmenti a max hullámot és a játékban töltött időt,
    /// majd szinkronizál a szerverrel.
    /// </summary>
    void UpdateSSFStats()
    {
        var upm = UserProgressManager.Instance;
        if (upm == null || !upm.HasCharacter) return;

        // SSF-specifikus: max hullám és idő frissítése
        if (upm.Data.IsSSF)
        {
            int reachedWave    = GameManager.Instance?.CurrentWave ?? 0;
            int elapsedSeconds = GameManager.Instance?.ElapsedPlaySeconds ?? 0;

            var data = upm.Data;
            if (reachedWave > data.maxWave)
            {
                // Új rekord – az idő az ehhez a futáshoz tartozó érték
                data.maxWave          = reachedWave;
                data.totalPlaySeconds = elapsedSeconds;
            }
            else if (reachedWave == data.maxWave && elapsedSeconds > 0 &&
                     (data.totalPlaySeconds == 0 || elapsedSeconds < data.totalPlaySeconds))
            {
                // Azonos max wave, de gyorsabb idő – megőrizzük a jobbat
                data.totalPlaySeconds = elapsedSeconds;
            }
            Debug.Log($"[SSF] Stats mentve – maxWave: {data.maxWave}, bestRunSeconds: {data.totalPlaySeconds}");
        }

        // Minden módnál: kill/kristály mentés + szerver sync
        upm.Save();
        ServerSyncManager.GetOrCreate().TriggerSync();
    }

    void ToggleSpeed()
    {
        _isFastSpeed = !_isFastSpeed;
        Time.timeScale = _isFastSpeed ? 10f : 1f;
        UpdateSpeedButtonText();
    }

    void UpdateSpeedButtonText()
    {
        if (speedToggleText != null)
            speedToggleText.text = _isFastSpeed ? "10x" : "1x";
    }

    void ReturnToMainMenu()
    {
        // PvP módban a Disconnect() már maga hívja a LoadScene-t – ne tegyük kétszer
        if (NetworkGameManager.Instance != null && NetworkGameManager.Instance.IsPvPMode)
        {
            NetworkGameManager.Instance.Disconnect();
            return;
        }

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
            bool isSSF = UserProgressManager.Instance != null && UserProgressManager.Instance.HasCharacter
                         && UserProgressManager.Instance.Data.IsSSF;

            if (sentHPText != null)
            {
                sentHPText.text = isSSF
                    ? $"SSF Evil pain: {xpMgr.LastMatchSentHP}"
                    : $"Sent HP: {xpMgr.LastMatchSentHP}";
            }

            if (placementText != null)
            {
                placementText.gameObject.SetActive(!isSSF);
                if (!isSSF)
                {
                    string placement = xpMgr.LastMatchPlacement == 1
                        ? "Wictory, full XP!"
                        : $"{xpMgr.LastMatchPlacement}. place";
                    placementText.text = placement;
                }
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

        MatchmakingClient.Instance?.RefreshRank();
    }
}
