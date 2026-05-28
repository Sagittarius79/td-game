using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// GameOver után megnyitható panel – az élő ellenfelek aktuális állapotát mutatja
/// (kastély HP, legközelebbi szörny távolsága és HP-ja).
/// Másodpercenként frissül, az OpponentDataTracker adataiból olvas.
/// </summary>
public class SpectatePanel : MonoBehaviour
{
    [Header("Sorok")]
    [Tooltip("SpectateRowUI prefab – ebből példányosodik minden ellenfél sor")]
    public GameObject rowPrefab;
    [Tooltip("A sorok szülő Transform-ja (VerticalLayoutGroup ajánlott)")]
    public Transform rowContainer;

    [Header("Vissza gomb")]
    public Button backButton;

    [Header("Debug")]
    [Tooltip("Ideiglenes debug szöveg – éles buildből töröld ki")]
    public TMPro.TextMeshProUGUI debugText;

    private GameObject _gameOverPanel;
    private Coroutine  _refreshCoroutine;
    private readonly List<SpectateRowUI> _rows = new List<SpectateRowUI>();
    private int _refreshCount = 0;

    void Awake()
    {
        gameObject.SetActive(false);
        if (backButton != null)
            backButton.onClick.AddListener(Close);
    }

    public void Open(GameObject gameOverPanel)
    {
        _gameOverPanel = gameOverPanel;
        if (_gameOverPanel != null) _gameOverPanel.SetActive(false);
        gameObject.SetActive(true);

        NetworkGameManager.Instance?.StartSpectating();

        if (_refreshCoroutine != null) StopCoroutine(_refreshCoroutine);
        _refreshCoroutine = StartCoroutine(RefreshLoop());
    }

    public void Close()
    {
        if (_refreshCoroutine != null) { StopCoroutine(_refreshCoroutine); _refreshCoroutine = null; }
        gameObject.SetActive(false);
        if (_gameOverPanel != null) _gameOverPanel.SetActive(true);

        NetworkGameManager.Instance?.StopSpectating();
    }

    IEnumerator RefreshLoop()
    {
        var wait = new WaitForSeconds(1f);
        while (true)
        {
            Refresh();
            yield return wait;
        }
    }

    void Refresh()
    {
        _refreshCount++;
        var ngm     = NetworkGameManager.Instance;
        var tracker = OpponentDataTracker.Instance;

        if (ngm == null || tracker == null)
        {
            SetDebug($"#{_refreshCount} NGM={ngm != null} Tracker={tracker != null}");
            return;
        }

        ulong localId = Unity.Netcode.NetworkManager.Singleton != null
            ? Unity.Netcode.NetworkManager.Singleton.LocalClientId
            : ulong.MaxValue;

        var ids = new List<ulong>(ngm.GetKnownPvpPlayerIds(localId));
        BuildRows(ids.Count);

        var sb = new System.Text.StringBuilder();
        sb.Append($"#{_refreshCount} localId={localId} opponents={ids.Count} totalUpdates={tracker.TotalClosestEnemyUpdates}\n");
        for (int i = 0; i < ids.Count; i++)
        {
            ulong id = ids[i];
            float dist  = tracker.GetClosestEnemyDist(id);
            float hp    = tracker.GetClosestEnemyHp(id);
            int   count = tracker.GetClosestEnemyCount(id);
            sb.Append($"  [{id}] enemies={count} dist={dist:F1} hp={hp:F0}\n");
            _rows[i].SetData(ngm.GetPlayerName(id), tracker.GetCastleHp(id), dist, hp,
                             tracker.GetTowerCounts(id));
        }
        SetDebug(sb.ToString());
    }

    void SetDebug(string msg)
    {
        if (debugText != null) debugText.text = msg;
    }

    void BuildRows(int needed)
    {
        while (_rows.Count > needed)
        {
            Destroy(_rows[_rows.Count - 1].gameObject);
            _rows.RemoveAt(_rows.Count - 1);
        }
        while (_rows.Count < needed)
        {
            if (rowPrefab == null || rowContainer == null) break;
            var go  = Instantiate(rowPrefab, rowContainer);
            var row = go.GetComponent<SpectateRowUI>();
            if (row == null) row = go.AddComponent<SpectateRowUI>();
            _rows.Add(row);
        }
    }
}
