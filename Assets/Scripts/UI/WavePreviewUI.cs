using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ═══════════════════════════════════════════════════════
///  HULLÁM ELŐNÉZET – Következő hullám tartalmának kiírása
///  ikonokkal és darabszámmal
/// ═══════════════════════════════════════════════════════
///
/// Megjelenítés:
///   [Ork kép]  x5
///   [Troll kép] x2   ← ha lesz második szörny típus
///
/// Bővítés új szörny típussal:
///   1. Inspector → Enemy Types → + gomb
///   2. Töltsd ki: Enemy Name, Icon (sprite), Base Count,
///      Increase Per Wave, Start From Wave
///
/// Unity beállítás:
///   - Script a Managers objektumon
///   - Row Prefab: egy prefab Image + TMP szöveggel (lásd lent)
///   - Container: egy üres UI objektum Vertical Layout Group-pal
/// </summary>
public class WavePreviewUI : MonoBehaviour
{
    public static WavePreviewUI Instance { get; private set; }
    // ══════════════════════════════════════════════════════
    //  SZÖRNY TÍPUS ADAT STRUKTÚRA
    //  Új szörny: Inspector → Enemy Types → +
    // ══════════════════════════════════════════════════════
    [System.Serializable]
    public class EnemyTypeInfo
    {
        [Tooltip("A szörny neve (csak belső azonosításhoz, nem jelenik meg)")]
        public string enemyName = "Ork";

        [Tooltip("A szörny kis ikonja – ez jelenik meg a képernyőn")]
        public Sprite icon;

        [Tooltip("Hány jön az 1. hullámban")]
        public int baseCount = 5;

        [Tooltip("Hullámönként mennyivel nő a darabszám")]
        public int increasePerWave = 2;

        [Tooltip("Hanyadik hullámtól jelenik meg (1 = az elejétől)")]
        public int startFromWave = 1;

        [Header("Lvl2 átváltás (opcionális)")]
        [Tooltip("Ha be van állítva, 10 darabonként 1 Lvl2-es jelenik meg a preview-ban")]
        public Sprite lvl2Icon;
        public string lvl2Name = "";

        [Header("Lvl3 átváltás (opcionális)")]
        [Tooltip("Ha be van állítva, 10 Lvl2-enként 1 Lvl3-as jelenik meg a preview-ban")]
        public Sprite lvl3Icon;
        public string lvl3Name = "";
    }

    // ══════════════════════════════════════════════════════
    //  INSPECTOR BEÁLLÍTÁSOK
    // ══════════════════════════════════════════════════════

    [Header("Prefab és konténer")]
    [Tooltip("Sor prefab: Horizontal Layout Group > Image + TextMeshPro")]
    public GameObject rowPrefab;

    [Tooltip("A konténer ahol a sorok megjelennek (Vertical Layout Group)")]
    public Transform container;

    [Header("Szörny típusok")]
    [Tooltip("Minden szörny típus egy sort kap ikonnal és darabszámmal")]
    public List<EnemyTypeInfo> enemyTypes = new List<EnemyTypeInfo>();

    [Header("Ikon beállítások")]
    [Tooltip("Az ikon mérete (szélesség és magasság)")]
    public Vector2 iconSize = new Vector2(40f, 40f);

    [Header("Fejléc szöveg (opcionális)")]
    [Tooltip("Ha be van kötve, kiírja melyik hullám következik")]
    public TextMeshProUGUI headerText;

    // ── Belső változók ────────────────────────────────────
    private List<GameObject> activeRows = new List<GameObject>();

    // PvP – küldött extra szörnyek: (ikon, küldő neve) → darabszám
    private Dictionary<(Sprite icon, string senderName), int> _pvpExtras
        = new Dictionary<(Sprite, string), int>();

    // ══════════════════════════════════════════════════════
    //  INICIALIZÁLÁS
    // ══════════════════════════════════════════════════════

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        // Feliratkozás WaveManager eseményre
        if (WaveManager.Instance != null)
            WaveManager.Instance.OnWaveStarted += OnWaveStarted;

        // Ha nincs kézzel beállított szörny, WaveManagerből töltjük fel
        if (enemyTypes.Count == 0)
            AutoFillFromWaveManager();

        // Kezdeti megjelenítés: 1. hullám előnézete
        UpdatePreview(currentWave: 0);
    }

    void OnDestroy()
    {
        // Leiratkozás – memory leak elkerüléshez
        if (WaveManager.Instance != null)
            WaveManager.Instance.OnWaveStarted -= OnWaveStarted;
    }

    // ══════════════════════════════════════════════════════
    //  ESEMÉNY KEZELŐK
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// Hullám indulásakor frissítjük a KÖVETKEZŐ hullám előnézetét.
    /// </summary>
    void OnWaveStarted(int waveNumber)
    {
        // Hullám induláskor töröljük az extra listát (már bespawnolnak)
        _pvpExtras.Clear();
        UpdatePreview(currentWave: waveNumber);
    }

    // ══════════════════════════════════════════════════════
    //  ELŐNÉZET FRISSÍTÉSE
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// Törli a régi sorokat és létrehozza az újakat
    /// a következő hullám összetétele alapján.
    /// </summary>
    void UpdatePreview(int currentWave)
    {
        if (container == null || rowPrefab == null) return;

        int nextWave = currentWave + 1;

        // Fejléc szöveg frissítése
        if (headerText != null)
            headerText.text = $"{nextWave}. hullám";

        // Régi sorok törlése
        ClearRows();

        // Utolsó hullám után
        if (WaveManager.Instance != null &&
            WaveManager.Instance.totalWaves > 0 &&
            nextWave > WaveManager.Instance.totalWaves)
        {
            // ── ITT MÓDOSÍTHATÓ: mi jelenjen meg a végén ──
            if (headerText != null)
                headerText.text = "Vége!";
            return;
        }

        // Alap hullám szörnyei
        foreach (var enemy in enemyTypes)
        {
            if (nextWave < enemy.startFromWave) continue;

            // Összesített szörny mennyiség – mindig WaveManager értékeiből számol,
            // hogy a preview és a tényleges spawn 100%-ban megegyezzen.
            int count = WaveManager.Instance != null
                ? WaveManager.Instance.startingEnemyCount + (nextWave - 1) * WaveManager.Instance.enemyIncreasePerWave
                : enemy.baseCount + (nextWave - 1) * enemy.increasePerWave;
            if (count <= 0) continue;

            // ── Lvl2 átváltás ──────────────────────────────────────────
            // Ha az adott szörny típushoz be van állítva Lvl2 ikon ÉS
            // a WaveManager tudja az átváltási arányt (orcsPerLvl2):
            // pl. 24 Ork → 2 Lvl2 sor + 4 sima Ork sor
            bool hasLvl2 = enemy.lvl2Icon != null &&
                           WaveManager.Instance != null &&
                           count >= WaveManager.Instance.orcsPerLvl2;

            if (hasLvl2)
            {
                int lvl2Count   = count / WaveManager.Instance.orcsPerLvl2;  // egész osztás
                int normalCount = count % WaveManager.Instance.orcsPerLvl2;  // maradék

                // ── Lvl3 átváltás ──────────────────────────────────────────
                bool hasLvl3 = enemy.lvl3Icon != null &&
                               lvl2Count >= WaveManager.Instance.orcsPerLvl2;

                if (hasLvl3)
                {
                    int lvl3Count      = lvl2Count / WaveManager.Instance.orcsPerLvl2;
                    int remainingLvl2  = lvl2Count % WaveManager.Instance.orcsPerLvl2;

                    CreateRowFromSprite(enemy.lvl3Icon, enemy.lvl3Name, lvl3Count);

                    if (remainingLvl2 > 0)
                        CreateRowFromSprite(enemy.lvl2Icon, enemy.lvl2Name, remainingLvl2);
                }
                else
                {
                    // Csak Lvl2 – Lvl3 nincs
                    CreateRowFromSprite(enemy.lvl2Icon, enemy.lvl2Name, lvl2Count);
                }

                // Sima szörny sor csak ha van maradék
                if (normalCount > 0)
                    CreateRow(enemy, normalCount);
            }
            else
            {
                CreateRow(enemy, count);
            }
        }

        // PvP – küldött extra szörnyek (küldőnként külön sor)
        foreach (var kvp in _pvpExtras)
        {
            CreatePvPRow(kvp.Key.icon, kvp.Key.senderName, kvp.Value);
        }
    }

    // ══════════════════════════════════════════════════════
    //  SOR LÉTREHOZÁSA
    //  Egy sor = [ikon kép] [x5 szöveg]
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// Létrehoz egy sort a konténerben: ikon + darabszám.
    /// </summary>
    void CreateRow(EnemyTypeInfo enemy, int count)
    {
        // Sor instantiálása a konténerbe
        GameObject row = Instantiate(rowPrefab, container);
        activeRows.Add(row);

        // Ikon beállítása – az első Image komponens a prefabban
        Image icon = row.GetComponentInChildren<Image>();
        if (icon != null)
        {
            icon.sprite = enemy.icon;

            // Ha nincs ikon sprite, elrejtjük az Image-et
            icon.enabled = enemy.icon != null;

            // ── ITT MÓDOSÍTHATÓ: ikon mérete ──
            RectTransform iconRect = icon.rectTransform;
            iconRect.sizeDelta = iconSize;
        }

        // Szöveg beállítása – a TMP komponens a prefabban
        TextMeshProUGUI text = row.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
        {
            // ── ITT MÓDOSÍTHATÓ: szöveg formátuma ──
            text.text = $"x{count}";
        }
    }

    // ══════════════════════════════════════════════════════
    //  SEGÉD METÓDUSOK
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// PvPSendPanel hívja, amikor a játékos elküld egy szörnyet.
    /// Hozzáadja a preview-hoz és azonnal frissíti.
    /// </summary>
    public void AddPvPEnemyToPreview(Sprite icon, string enemyName, string senderName = "")
    {
        var key = (icon, senderName);
        if (_pvpExtras.ContainsKey(key))
            _pvpExtras[key]++;
        else
            _pvpExtras[key] = 1;

        // Azonnali frissítés
        UpdatePreview(WaveManager.Instance != null ? WaveManager.Instance.CurrentWave : 0);
    }

    /// <summary>Sor létrehozása közvetlenül Sprite + név + darabszám alapján (Lvl2 sorokhoz).</summary>
    void CreateRowFromSprite(Sprite icon, string label, int count)
    {
        GameObject row = Instantiate(rowPrefab, container);
        activeRows.Add(row);

        Image img = row.GetComponentInChildren<Image>();
        if (img != null)
        {
            img.sprite  = icon;
            img.enabled = icon != null;
            img.rectTransform.sizeDelta = iconSize;
        }

        TextMeshProUGUI text = row.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
            text.text = $"x{count}";
    }

    void CreatePvPRow(Sprite icon, string senderName, int count)
    {
        GameObject row = Instantiate(rowPrefab, container);
        activeRows.Add(row);

        Image img = row.GetComponentInChildren<Image>();
        if (img != null)
        {
            img.sprite  = icon;
            img.enabled = icon != null;
            img.rectTransform.sizeDelta = iconSize;
        }

        TextMeshProUGUI text = row.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
        {
            text.text = string.IsNullOrEmpty(senderName)
                ? $"x{count}"
                : $"x{count} [{senderName}]";
        }
    }

    /// <summary>
    /// Törli az összes aktív sort a konténerből.
    /// </summary>
    void ClearRows()
    {
        foreach (var row in activeRows)
            if (row != null) Destroy(row);
        activeRows.Clear();
    }

    /// <summary>
    /// Ha az Enemy Types lista üres, automatikusan beolvassa
    /// a WaveManager értékeit (startingEnemyCount, enemyIncreasePerWave).
    /// </summary>
    void AutoFillFromWaveManager()
    {
        if (WaveManager.Instance == null) return;

        // ── ITT MÓDOSÍTHATÓ: alapértelmezett ork adatok ──
        enemyTypes.Add(new EnemyTypeInfo
        {
            enemyName       = "Ork",
            icon            = null,
            baseCount       = WaveManager.Instance.startingEnemyCount,
            increasePerWave = WaveManager.Instance.enemyIncreasePerWave,
            startFromWave   = 1
        });
    }
}
