using UnityEngine;

/// <summary>
/// Játékonkénti XP gyűjtő – nyomon követi az aktuális menetben szerzett XP-t.
///
/// XP forrás: az ellenfélnek küldött szörnyek maxHealth értéke (össz HP).
///
/// Kiosztási szabály (PvP):
///   XP = össz küldött szörny HP / játékosok száma kieséskor
///   • Első kieső (8 főből): HP / 8
///   • Második kieső (7 főből): HP / 7
///   • Győztes (egyedüliként maradt, 1 fő): HP / 1 = teljes HP
///
/// Solo módban (soloXPEnabled = true): osztó mindig 1 → teljes HP jár.
///
/// Használat:
///   1. AddSentEnemyXP() – minden küldött szörnynél (PvPSendPanel hívja)
///   2. SetEliminationContext() – közvetlenül az AwardAndReset előtt (NetworkGameManager hívja)
///   3. AwardAndReset() – játék végén (UIManager hívja)
/// </summary>
public class XPManager : MonoBehaviour
{
    public static XPManager Instance { get; private set; }

    [Header("Tesztelés")]
    [Tooltip("Ha BE van kapcsolva, SOLO módban is jár az XP. Csak teszteléshez!")]
    public bool soloXPEnabled = false;

    // Az aktuális játékban felhalmozott, még nem juttatott XP (össz küldött szörny HP)
    private long _pendingXP    = 0;

    // Hány élő játékos volt, amikor a helyi játékos kiesett / nyert
    // Szerver állítja be SetEliminationContext()-en keresztül
    private int  _playersAlive = 1;

    // PvP módot közvetlenül a NetworkGameManager-től kérdezzük le,
    // hogy ne függjön a SetMatchType() hívás sorrendjétől.
    private bool IsPvPMatch =>
        NetworkGameManager.Instance != null && NetworkGameManager.Instance.IsPvPMode;

    /// <summary>Az aktuális meccsben eddig összegyűjtött bruttó XP (game over panelen kimutatható).</summary>
    public long PendingXP => _pendingXP;

    /// <summary>Az utolsó meccsben küldött szörnyek össz HP-ja (bruttó XP).</summary>
    public long LastMatchSentHP    { get; private set; }
    /// <summary>Hányadik lett a játékos (= élő játékosok száma kieséskor). 1 = győztes.</summary>
    public int  LastMatchPlacement { get; private set; }
    /// <summary>Az utolsó meccsben ténylegesen kapott XP (osztás után).</summary>
    public long LastMatchAwardedXP { get; private set; }
    /// <summary>Igaz, ha az utolsó Solo meccsben a 10. szint cap miatt nem (vagy kevesebb) XP járt.</summary>
    public bool LastMatchSoloLevelCapped { get; private set; }

    // ── Meccs indítás ─────────────────────────────────────────────────

    /// <summary>Meccs indításkor hívható reset – visszafelé kompatibilitás miatt megtartva.</summary>
    public void SetMatchType(bool isPvP)
    {
        _pendingXP    = 0;
        _playersAlive = 1;
        Debug.Log($"XPManager: reset, IsPvPMatch={IsPvPMatch}, soloXP: {soloXPEnabled}");
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // ── XP gyűjtés ───────────────────────────────────────────────────

    /// <summary>
    /// Hozzáadja az ellenfélnek küldött szörny HP-ját a függőben lévő XP-hez.
    /// A PvPSendPanel.TrySendEnemy() hívja sikeres küldés után.
    /// </summary>
    public void AddSentEnemyXP(float enemyMaxHP)
    {
        if (!IsPvPMatch && !soloXPEnabled) return;

        long xp = Mathf.CeilToInt(enemyMaxHP);
        _pendingXP += xp;
        Debug.Log($"XPManager: +{xp} XP (szörny HP) → összesen {_pendingXP} XP ebben a meccsben");
    }

    /// <summary>
    /// SSF Gold Pool ellenség megölésért jár XP.
    /// Csak SSF módban aktív – a normál wave-szörnyek ölése NEM ad XP-t.
    /// WaveManager.OnEnemyDied hívja, ha a szörny SSF_SENDER_ID-val volt megjelölve.
    /// </summary>
    public void AddSSFGoldPoolXP(float enemyMaxHP)
    {
        bool isSsf = UserProgressManager.Instance != null
                     && UserProgressManager.Instance.HasCharacter
                     && UserProgressManager.Instance.Data.IsSSF;
        if (!isSsf) return;

        long xp = Mathf.CeilToInt(enemyMaxHP);
        _pendingXP += xp;
    }

    // ── Kiesési kontextus ─────────────────────────────────────────────

    /// <summary>
    /// NetworkGameManager hívja közvetlenül az AwardAndReset előtt.
    /// Meghatározza, hány játékossal kell elosztani az XP-t.
    ///   • Első kieső 8 főnél: playersAlive = 8
    ///   • Győztes: playersAlive = 1
    /// Solo módban automatikusan 1 marad.
    /// </summary>
    public void SetEliminationContext(int playersAlive)
    {
        _playersAlive = Mathf.Max(1, playersAlive);
        Debug.Log($"XPManager: kiesési kontextus → {_playersAlive} játékos volt életben");
    }

    // ── Játék vége ───────────────────────────────────────────────────

    /// <summary>
    /// Juttatja a kiszámított XP-t a UserProgressManager-nek és visszaállítja a számlálót.
    /// Az UIManager hívja ShowGameOver() / ShowVictory() belsejéből.
    /// </summary>
    /// <param name="won">Igaz, ha a játékos nyert.</param>
    /// <returns>Az ebben a meccsben ténylegesen kiosztott XP.</returns>
    /// <summary>Solo módban a maximálisan elérhető szint (e fölött nem jár XP – farm-védelem).</summary>
    public const int SOLO_XP_LEVEL_CAP = 10;

    public long AwardAndReset(bool won)
    {
        bool isPvP     = IsPvPMatch;
        bool xpAllowed = isPvP || soloXPEnabled;

        long awarded = 0;
        if (xpAllowed && _pendingXP > 0)
        {
            // Solo: osztó = 1 → teljes HP; PvP: osztó = játékosok száma kieséskor
            int divisor = isPvP ? _playersAlive : 1;
            awarded = Mathf.Max(1, Mathf.RoundToInt(_pendingXP / (float)divisor));
        }

        // ── Solo XP cap (anti-farm) ──────────────────────────────────────
        // Solo módban max. SOLO_XP_LEVEL_CAP szintig lehet XP-t szerezni.
        // 10-es szint felett 0 XP, 10-es szint alatt clamp annyira, hogy
        // épp elérje a 10-es szintet, ne lépje túl.
        bool soloCapped = false;
        bool isSsfChar = UserProgressManager.Instance != null
                         && UserProgressManager.Instance.HasCharacter
                         && UserProgressManager.Instance.Data.IsSSF;
        if (!isPvP && !isSsfChar && awarded > 0 && UserProgressManager.Instance != null)
        {
            int currentLevel = UserProgressManager.Instance.Level;
            if (currentLevel >= SOLO_XP_LEVEL_CAP)
            {
                Debug.Log($"XPManager: Solo XP cap – már {currentLevel}. szint, nem jár több solo XP.");
                awarded = 0;
                soloCapped = true;
            }
            else
            {
                long capTotalXP   = UserProgressManager.XPForLevel(SOLO_XP_LEVEL_CAP);
                long currentTotal = UserProgressManager.Instance.TotalXP;
                long roomLeft     = System.Math.Max(0, capTotalXP - currentTotal);
                if (awarded > roomLeft)
                {
                    Debug.Log($"XPManager: Solo XP clamp – {awarded} → {roomLeft} (10. szint határ).");
                    awarded = roomLeft;
                    if (awarded == 0) soloCapped = true;
                }
            }
        }

        if (UserProgressManager.Instance != null)
        {
            if (awarded > 0)
                UserProgressManager.Instance.AddXP(awarded);

            if (won) UserProgressManager.Instance.RecordWin();
            else     UserProgressManager.Instance.RecordLoss();

            ServerSyncManager.GetOrCreate().TriggerSync();
        }

        // Utolsó meccs statisztikák mentése (game over képernyőhöz)
        LastMatchSentHP          = _pendingXP;
        LastMatchPlacement       = _playersAlive;
        LastMatchAwardedXP       = awarded;
        LastMatchSoloLevelCapped = soloCapped;

        if (!xpAllowed)
            Debug.Log("XPManager: Solo meccs – XP nem jár (soloXPEnabled = false).");
        else
            Debug.Log($"XPManager: Meccs vége – bruttó XP: {_pendingXP}, " +
                      $"osztó: {(isPvP ? _playersAlive : 1)}, " +
                      $"kiosztott XP: {awarded}, győzelem: {won}");

        _pendingXP    = 0;
        _playersAlive = 1;
        return awarded;
    }

    /// <summary>Visszaállítja a számlálót juttatás nélkül (pl. scene újratöltéskor).</summary>
    public void Reset()
    {
        _pendingXP    = 0;
        _playersAlive = 1;
    }
}
