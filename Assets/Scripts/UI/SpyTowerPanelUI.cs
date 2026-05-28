using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Felugró panel a SpyTowerBuilding-hez.
/// Annyi ellenfél sorát mutatja, amennyit a Spy skill szintje engedélyez.
/// </summary>
public class SpyTowerPanelUI : MonoBehaviour
{
    public static SpyTowerPanelUI Instance { get; private set; }

    [Header("Panel")]
    public GameObject panel;

    [Header("Sorok")]
    [Tooltip("SpyOpponentRowUI prefab – ebből példányosodik minden ellenfél sor")]
    public GameObject rowPrefab;
    [Tooltip("A sorok szülő Transform-ja (VerticalLayoutGroup ajánlott)")]
    public Transform  rowContainer;

    [Header("Nincs ellenfél üzenet")]
    public GameObject noOpponentRoot;

    [Header("Auto-close")]
    [Tooltip("Ennyi másodperc után tűnik el a panel automatikusan")]
    public float autoCloseDelay = 3f;

    public bool IsOpen => panel != null && panel.activeSelf;

    private Coroutine             _autoCloseCoroutine;
    private List<SpyOpponentRowUI> _rows = new List<SpyOpponentRowUI>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (panel != null) panel.SetActive(false);
    }

    /// <summary>Megmutatja az ellenfél adatait (több sor is lehet).</summary>
    public void Show(List<OpponentSnapshot> snapshots)
    {
        if (panel == null) return;
        panel.SetActive(true);

        if (noOpponentRoot != null) noOpponentRoot.SetActive(false);

        BuildRows(snapshots.Count);
        for (int i = 0; i < snapshots.Count; i++)
            _rows[i].SetData(snapshots[i]);

        RestartAutoClose();
    }

    public void ShowNoOpponent()
    {
        if (panel == null) return;
        panel.SetActive(true);

        if (noOpponentRoot != null) noOpponentRoot.SetActive(true);

        BuildRows(0);
        RestartAutoClose();
    }

    public void Hide()
    {
        if (_autoCloseCoroutine != null) StopCoroutine(_autoCloseCoroutine);
        if (panel != null) panel.SetActive(false);
    }

    // ── Sor kezelés ──────────────────────────────────────────────────

    void BuildRows(int needed)
    {
        // Felesleges sorok törlése
        while (_rows.Count > needed)
        {
            Destroy(_rows[_rows.Count - 1].gameObject);
            _rows.RemoveAt(_rows.Count - 1);
        }

        // Hiányzó sorok létrehozása
        while (_rows.Count < needed)
        {
            if (rowPrefab == null || rowContainer == null) break;
            var go  = Instantiate(rowPrefab, rowContainer);
            var row = go.GetComponent<SpyOpponentRowUI>();
            if (row == null) row = go.AddComponent<SpyOpponentRowUI>();
            _rows.Add(row);
        }
    }

    // ── Auto-close ───────────────────────────────────────────────────

    void RestartAutoClose()
    {
        if (_autoCloseCoroutine != null) StopCoroutine(_autoCloseCoroutine);
        _autoCloseCoroutine = StartCoroutine(AutoCloseRoutine());
    }

    IEnumerator AutoCloseRoutine()
    {
        yield return new WaitForSeconds(autoCloseDelay);
        Hide();
    }

#if UNITY_EDITOR
    [ContextMenu("Test: Show 1 ellenfél (fake adat)")]
    void TestShow1()
    {
        Show(new List<OpponentSnapshot>
        {
            new OpponentSnapshot("Teszt Játékos", "ArcherTower", 18),
        });
    }

    [ContextMenu("Test: Show 3 ellenfél (fake adat)")]
    void TestShow3()
    {
        Show(new List<OpponentSnapshot>
        {
            new OpponentSnapshot("Játékos A", "ArcherTower",  24),
            new OpponentSnapshot("Játékos B", "LASER",         7),
            new OpponentSnapshot("Játékos C", "–",            -1),
        });
    }

    [ContextMenu("Test: ShowNoOpponent")]
    void TestNoOpponent() => ShowNoOpponent();
#endif
}
