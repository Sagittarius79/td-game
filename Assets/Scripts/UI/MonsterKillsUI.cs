using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// GameOver panelen 4 oszlopos gridben mutatja a megölt szörny típusokat.
/// Minden cella: szörny ikon + "x3" felirat a jobb alsó sarokban.
///
/// Unity beállítás:
///   1. MonsterKilled Panel alatt hozz létre egy "Grid" nevű üres GameObject-et
///   2. Grid-re rakj: GridLayoutGroup + ContentSizeFitter
///   3. GridLayoutGroup: Constraint = Fixed Column Count, Constraint Count = 4,
///      Cell Size = (80, 80), Spacing = (8, 8), Child Alignment = Upper Left
///   4. ContentSizeFitter: Vertical Fit = Preferred Size
///   5. Hozz létre egy "KillCell" prefabot (lásd lent), és húzd be a Cell Prefab mezőbe
///   6. A Grid Transform-ot húzd be a Container mezőbe
///
/// KillCell prefab felépítése:
///   KillCell (RectTransform, méret a GridLayoutGroup cell size-ából jön)
///   └── Icon (Image, Anchor = stretch-stretch, Left/Right/Top/Bottom = 0)
///   └── CountText (TextMeshProUGUI, Anchor = bottom-right, pl. "x3", félkövér)
/// </summary>
public class MonsterKillsUI : MonoBehaviour
{
    [Header("Prefab és konténer")]
    [Tooltip("Grid cella prefab: Image (ikon) + TextMeshPro (szám, jobb alsó sarokban)")]
    public GameObject cellPrefab;
    [Tooltip("A GridLayoutGroup-os konténer GameObject Transform-ja")]
    public Transform container;

    private readonly List<GameObject> _cells = new List<GameObject>();

    /// <summary>UIManager hívja ShowGameOver / ShowVictory-ban.</summary>
    public void Populate()
    {
        Clear();

        var kills = GameManager.Instance?.KillCounts;
        if (kills == null || kills.Count == 0) return;

        foreach (var kvp in kills)
            CreateCell(kvp.Value.icon, kvp.Value.count);
    }

    // ── Privát metódusok ──────────────────────────────────────────


    void CreateCell(Sprite icon, int count)
    {
        GameObject cell = Instantiate(cellPrefab, container);
        _cells.Add(cell);

        // Első Image = szörny ikon
        Image img = cell.GetComponentInChildren<Image>();
        if (img != null)
        {
            img.sprite           = icon;
            img.enabled          = icon != null;
            img.preserveAspect   = true;
        }

        // TMP szöveg = darabszám
        TextMeshProUGUI text = cell.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
            text.text = $"x{count}";
    }

    void Clear()
    {
        foreach (var c in _cells)
            if (c != null) Destroy(c);
        _cells.Clear();
    }
}
