using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Tutorial kártyák – 3 fix bevezető kép + véletlenszerű ToolTip kártyák.
///
/// Indexelés a history listában:
///   0..2          → introSprites[0..2]  (fix, sorrendben)
///   INTRO_COUNT.. → cards[index - INTRO_COUNT]  (véletlenszerű ToolTip)
///
/// Auto mód (CharacterSheet bezáráskor, lvl5-ig):
///   - Mindig a 3 fix képpel kezd, sorrendben
///   - Utána véletlenszerű ToolTip kártyák
///   - Bezárás gomb csak a 4. kártyától (első random-tól)
///
/// Manuális mód (ToolTips gomb, bármely szinten):
///   - Véletlenszerűen az összes kártya közül (intro + ToolTip együtt)
///   - Bezárás gomb az első lapozás után
/// </summary>
public class TutorialCardsUI : MonoBehaviour
{
    public static TutorialCardsUI Instance { get; private set; }

    private const int INTRO_COUNT = 4;

    // ── Kártya adatok ─────────────────────────────────────────────────
    [System.Serializable]
    public class TutorialCard
    {
        public string title;
        public Sprite image;
    }

    [Header("Fix bevezető képek (mindig először, sorrendben)")]
    public Sprite introSprite1;
    public Sprite introSprite2;
    public Sprite introSprite3;
    public Sprite introSprite4;

    [Header("Véletlenszerű ToolTip kártyák")]
    public TutorialCard[] cards;

    // ── UI elemek ─────────────────────────────────────────────────────
    [Header("Fő panel")]
    public GameObject tutorialPanel;

    [Header("Kártya tartalom")]
    public Image           cardImage;
    public TextMeshProUGUI titleText;
    private CardImageZoom  _zoom;

    [Header("Navigáció")]
    public Button prevButton;
    public Button nextButton;

    [Header("Befejező gomb")]
    public Button doneButton;

    [Header("Beállítás")]
    [Tooltip("Auto mód csak ennyi szintig jelenik meg (0 = korlátlan)")]
    public int maxLevel = 5;

    // ── Belső állapot ─────────────────────────────────────────────────
    private List<int> _history = new();   // encoded index (lásd fent)
    private System.Action _onCompleted;
    private bool _isShowing   = false;
    private bool _isAutoMode  = false;

    // ── Életciklus ────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        if (tutorialPanel == null) tutorialPanel = gameObject;
        if (cardImage != null) _zoom = cardImage.GetComponent<CardImageZoom>();

        if (prevButton != null) prevButton.onClick.AddListener(OnPrev);
        if (nextButton != null) nextButton.onClick.AddListener(OnNext);
        if (doneButton != null) doneButton.onClick.AddListener(OnDone);

        tutorialPanel.SetActive(false);
    }

    // ── Publikus API ──────────────────────────────────────────────────

    /// <param name="onCompleted">Callback amikor bezárják.</param>
    /// <param name="auto">
    /// true  = auto mód: szintkorlát, 3 fix bevezető + random ToolTip.
    /// false = manuális: minden kártya véletlenszerűen, szintkorlát nélkül.
    /// </param>
    public void Show(System.Action onCompleted, bool auto = true)
    {
        if (_isShowing) return;

        _onCompleted = onCompleted;
        _isAutoMode  = auto;

        if (auto && maxLevel > 0 && (UserProgressManager.Instance?.Level ?? 0) > maxLevel)
        {
            _onCompleted?.Invoke();
            return;
        }

        bool hasIntro   = HasIntroSprites();
        bool hasTooltips = cards != null && cards.Length > 0;

        if (!hasIntro && !hasTooltips)
        {
            _onCompleted?.Invoke();
            return;
        }

        _isShowing = true;
        _history.Clear();

        if (auto && hasIntro)
        {
            // Auto: 3 fix képpel kezd (index 0, 1, 2)
            _history.Add(0);
        }
        else
        {
            // Manuális: véletlenszerűen az összes kártya közül
            _history.Add(RandomIndexFromAll());
        }

        tutorialPanel.SetActive(true);
        RefreshCard();
    }

    public void Hide()
    {
        if (tutorialPanel != null) tutorialPanel.SetActive(false);
    }

    // ── Navigáció ─────────────────────────────────────────────────────

    void OnPrev()
    {
        if (_history.Count <= 1) return;
        _history.RemoveAt(_history.Count - 1);
        RefreshCard();
    }

    void OnNext()
    {
        int next = NextIndex();
        _history.Add(next);
        RefreshCard();
    }

    void OnDone()
    {
        _isShowing = false;
        Hide();
        _onCompleted?.Invoke();
    }

    // ── Index logika ──────────────────────────────────────────────────

    /// <summary>Következő index a history-ba a jelenlegi pozíció alapján.</summary>
    int NextIndex()
    {
        if (_isAutoMode)
        {
            int current = _history[_history.Count - 1];

            // Ha még az intro fázisban vagyunk (0,1,2) → következő intro vagy első random
            if (current < INTRO_COUNT - 1)
                return current + 1;                    // következő intro kép

            if (current == INTRO_COUNT - 1)
                return INTRO_COUNT + RandomTooltipOffset(); // első random ToolTip

            // Már random fázisban: újabb random
            return INTRO_COUNT + RandomTooltipOffset();
        }
        else
        {
            // Manuális: mindenből random
            return RandomIndexFromAll();
        }
    }

    /// <summary>Random offset a cards[] tömbhöz (INTRO_COUNT-tól kezdődik).</summary>
    int RandomTooltipOffset()
    {
        if (cards == null || cards.Length == 0) return 0;
        return Random.Range(0, cards.Length);
    }

    /// <summary>Random index az összes kártyából (intro + tooltip együtt).</summary>
    int RandomIndexFromAll()
    {
        int introCount   = HasIntroSprites() ? INTRO_COUNT : 0;
        int tooltipCount = cards != null ? cards.Length : 0;
        int total        = introCount + tooltipCount;
        if (total == 0) return 0;
        return Random.Range(0, total);
    }

    bool HasIntroSprites() => introSprite1 != null || introSprite2 != null || introSprite3 != null || introSprite4 != null;

    Sprite GetIntroSprite(int i)
    {
        return i switch { 0 => introSprite1, 1 => introSprite2, 2 => introSprite3, 3 => introSprite4, _ => null };
    }

    // ── Megjelenítés ──────────────────────────────────────────────────

    void RefreshCard()
    {
        if (_history.Count == 0) return;

        int encodedIndex = _history[_history.Count - 1];
        bool isIntro     = encodedIndex < INTRO_COUNT;

        Sprite  sprite = null;
        string  title  = "";

        if (isIntro)
        {
            sprite = GetIntroSprite(encodedIndex);
        }
        else
        {
            int tooltipIndex = encodedIndex - INTRO_COUNT;
            if (cards != null && tooltipIndex < cards.Length)
            {
                sprite = cards[tooltipIndex].image;
                title  = cards[tooltipIndex].title ?? "";
            }
        }

        if (cardImage != null)
        {
            cardImage.gameObject.SetActive(sprite != null);
            if (sprite != null) cardImage.sprite = sprite;
        }

        if (titleText != null) titleText.text = title;

        _zoom?.ResetZoom();

        // Prev gomb: csak ha van előzmény
        if (prevButton != null)
            prevButton.gameObject.SetActive(_history.Count > 1);

        // Next gomb: mindig látható
        if (nextButton != null)
            nextButton.gameObject.SetActive(true);

        // Done gomb:
        //   Auto módban: csak a 4. kártyától (első random ToolTip-től)
        //   Manuális módban: azonnal, az első kártyától
        bool showDone;
        if (_isAutoMode)
            showDone = !isIntro;
        else
            showDone = true;

        if (doneButton != null) doneButton.gameObject.SetActive(showDone);
    }
}
