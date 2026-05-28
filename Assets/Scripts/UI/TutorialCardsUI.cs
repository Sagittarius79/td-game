using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Tutorial kártyák – 3 fix bevezető kép + véletlenszerű ToolTip kártyák.
///
/// Indexelés a history listában:
///   0..3          → introSprites[0..3]  (fix, sorrendben)
///   INTRO_COUNT.. → cards[index - INTRO_COUNT]  (véletlenszerű ToolTip)
///
/// Auto mód (CharacterSheet bezáráskor, lvl5-ig):
///   - Mindig a 4 fix képpel kezd, sorrendben
///   - Utána véletlenszerű ToolTip kártyák
///   - Bezárás gomb csak az első random kártyától
///
/// Manuális mód (ToolTips gomb, bármely szinten):
///   - Véletlenszerűen az összes kártya közül (intro + ToolTip együtt)
///   - Bezárás gomb az első kártyától
/// </summary>
public class TutorialCardsUI : MonoBehaviour
{
    public static TutorialCardsUI Instance { get; private set; }

    private const int INTRO_COUNT = 1;

    // ── Adatforrás ────────────────────────────────────────────────────
    [Header("Adatforrás")]
    [Tooltip("Közös kártyaadatbázis – minden scene-ben ugyanaz az asset")]
    public TutorialCardDatabase database;

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
    private List<int> _history = new();
    private System.Action _onCompleted;
    private bool _isShowing  = false;
    private bool _isAutoMode = false;

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
    /// true  = auto mód: szintkorlát, 4 fix bevezető + random ToolTip.
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

        bool hasIntro    = HasIntroSprites();
        bool hasTooltips = database != null && database.cards != null && database.cards.Length > 0;

        if (!hasIntro && !hasTooltips)
        {
            _onCompleted?.Invoke();
            return;
        }

        _isShowing = true;
        _history.Clear();

        if (auto && hasIntro)
            _history.Add(0);
        else
            _history.Add(RandomIndexFromAll());

        tutorialPanel.SetActive(true);
        RefreshCard();
    }

    public void Hide()
    {
        _isShowing = false;
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
        _history.Add(NextIndex());
        RefreshCard();
    }

    void OnDone()
    {
        _isShowing = false;
        Hide();
        _onCompleted?.Invoke();
    }

    // ── Index logika ──────────────────────────────────────────────────

    int NextIndex()
    {
        if (_isAutoMode)
        {
            int current = _history[_history.Count - 1];
            if (current < INTRO_COUNT - 1)  return current + 1;
            return INTRO_COUNT + RandomTooltipOffset();
        }
        return RandomIndexFromAll();
    }

    int RandomTooltipOffset()
    {
        int count = database?.cards?.Length ?? 0;
        return count == 0 ? 0 : Random.Range(0, count);
    }

    int RandomIndexFromAll()
    {
        int introCount   = HasIntroSprites() ? INTRO_COUNT : 0;
        int tooltipCount = database?.cards?.Length ?? 0;
        int total        = introCount + tooltipCount;
        return total == 0 ? 0 : Random.Range(0, total);
    }

    bool HasIntroSprites() => database != null && database.introSprite1 != null;

    Sprite GetIntroSprite(int i) => i == 0 ? database?.introSprite1 : null;

    // ── Megjelenítés ──────────────────────────────────────────────────

    void RefreshCard()
    {
        if (_history.Count == 0) return;

        int  encodedIndex = _history[_history.Count - 1];
        bool isIntro      = encodedIndex < INTRO_COUNT;

        Sprite sprite = null;
        string title  = "";

        if (isIntro)
        {
            sprite = GetIntroSprite(encodedIndex);
        }
        else
        {
            int tooltipIndex = encodedIndex - INTRO_COUNT;
            var cards = database?.cards;
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

        if (prevButton != null) prevButton.gameObject.SetActive(_history.Count > 1);
        if (nextButton != null) nextButton.gameObject.SetActive(true);

        bool showDone = true;
        if (doneButton != null) doneButton.gameObject.SetActive(showDone);
    }
}
