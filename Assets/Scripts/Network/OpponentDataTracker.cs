using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Tárolja az ellenfelek játék közbeni állapotát (torony darabszámok, kastély HP).
/// A NetworkGameManager tölti fel üzenetek alapján.
/// </summary>
public class OpponentDataTracker : MonoBehaviour
{
    public static OpponentDataTracker Instance { get; private set; }

    // clientId → torony neve → darabszám
    private Dictionary<ulong, Dictionary<string, int>> _towerCounts
        = new Dictionary<ulong, Dictionary<string, int>>();

    // clientId → kastély HP
    private Dictionary<ulong, int> _castleHp = new Dictionary<ulong, int>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── Adatok rögzítése (NetworkGameManager hívja) ──────────────────

    public void RecordTowerPlaced(ulong clientId, string towerName)
    {
        if (IsServerClientId(clientId)) return;

        if (!_towerCounts.ContainsKey(clientId))
            _towerCounts[clientId] = new Dictionary<string, int>();

        var counts = _towerCounts[clientId];
        counts[towerName] = counts.TryGetValue(towerName, out int c) ? c + 1 : 1;
    }

    public void RecordCastleHp(ulong clientId, int hp)
    {
        if (IsServerClientId(clientId)) return;
        _castleHp[clientId] = hp;
    }

    // Solo módban a helyi játékos adatait ezen az ID-n tároljuk
    public const ulong SoloClientId = 9999UL;

    static bool IsServerClientId(ulong id) =>
        Unity.Netcode.NetworkManager.Singleton != null &&
        id == Unity.Netcode.NetworkManager.ServerClientId;

    public void RemovePlayer(ulong clientId)
    {
        _towerCounts.Remove(clientId);
        _castleHp.Remove(clientId);
    }

    public void Reset()
    {
        _towerCounts.Clear();
        _castleHp.Clear();
    }

    // ── Lekérdezés ───────────────────────────────────────────────────

    /// <summary>
    /// Visszaad egy véletlen ellenfél adatait (visszafelé kompatibilis).
    /// </summary>
    public bool TryGetRandomOpponent(ulong localClientId, out OpponentSnapshot snapshot)
    {
        var list = GetRandomOpponents(localClientId, 1);
        if (list.Count == 0) { snapshot = default; return false; }
        snapshot = list[0];
        return true;
    }

    /// <summary>
    /// Visszaad legfeljebb <paramref name="count"/> véletlen ellenfél adatait (ismétlés nélkül).
    /// </summary>
    public List<OpponentSnapshot> GetRandomOpponents(ulong localClientId, int count)
    {
        bool isPvP = NetworkGameManager.Instance != null && NetworkGameManager.Instance.IsPvPMode;

        // PvP módban az összes ismert játékos a lehetséges célpont, ne csak azok,
        // akikről adat érkezett. A _castleHp/towerCounts race-es betöltése miatt
        // előfordulhat, hogy a HP-üzenet a handler regisztrációja előtt érkezik → elvész.
        IEnumerable<ulong> candidates;
        if (isPvP && NetworkGameManager.Instance != null)
            candidates = NetworkGameManager.Instance.GetKnownPvpPlayerIds(localClientId);
        else
            candidates = _towerCounts.Keys
                .Union(_castleHp.Keys)
                .Where(id => id != localClientId && !IsServerClientId(id));

        var pool = candidates.ToList();

        var dbg = new System.Text.StringBuilder();
        dbg.Append($"[SpyTower] GetRandomOpponents | isPvP={isPvP} | kért={count} | pool ({pool.Count}): ");
        foreach (var id in pool) dbg.Append($"{id} ");
        Debug.Log(dbg.ToString());

        var result = new List<OpponentSnapshot>();
        count = Mathf.Min(count, pool.Count);

        var rng = new System.Random();
        for (int i = 0; i < count; i++)
        {
            int idx = rng.Next(0, pool.Count);
            ulong picked = pool[idx];
            pool.RemoveAt(idx);

            string playerName = NetworkGameManager.Instance != null
                ? NetworkGameManager.Instance.GetPlayerName(picked)
                : "Ellenfél";

            string mostBuilt = "–";
            if (_towerCounts.TryGetValue(picked, out var counts) && counts.Count > 0)
                mostBuilt = counts.OrderByDescending(kv => kv.Value).First().Key;

            int hp = _castleHp.TryGetValue(picked, out int h) ? h : -1;
            result.Add(new OpponentSnapshot(playerName, mostBuilt, hp));
        }

        return result;
    }
}

public struct OpponentSnapshot
{
    public string PlayerName;
    public string MostBuiltTower;
    public int    CastleHp;

    public OpponentSnapshot(string name, string tower, int hp)
    {
        PlayerName    = name;
        MostBuiltTower = tower;
        CastleHp      = hp;
    }
}
