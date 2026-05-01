using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dinamikus árú épület – minden épülettípus a saját darabszámát követi.
/// Adj hozzá minden olyan épület prefabjához, amelynek ára növekedjen
/// minden újabb lerakott példányonként.
///
/// A TowerShopItem "Dynamic Price" jelölőjével együtt működik:
///   - TowerShopItem olvassa a GetCount(prefabNév) értéket
///   - Ez a script tartja nyilván hány db van lerakva típusonként
/// </summary>
public class DynamicPriceBuilding : MonoBehaviour
{
    // Épülettípusonkénti darabszám – kulcs: prefab neve (Clone nélkül)
    private static readonly Dictionary<string, int> _counts = new Dictionary<string, int>();

    /// <summary>Bármelyik épület lerakásakor/eltávolításakor tűzik.</summary>
    public static event System.Action OnCountChanged;

    /// <summary>Visszaadja az adott névhez tartozó lerakott épületek számát.</summary>
    public static int GetCount(string prefabName)
    {
        return _counts.TryGetValue(prefabName, out int count) ? count : 0;
    }

    // ── Lifecycle ─────────────────────────────────────────────────

    void Awake()
    {
        string key = GetKey();
        _counts[key] = GetCount(key) + 1;
        OnCountChanged?.Invoke();
    }

    void OnDestroy()
    {
        string key = GetKey();
        _counts[key] = Mathf.Max(0, GetCount(key) - 1);
        OnCountChanged?.Invoke();
    }

    /// <summary>Prefab neve: "(Clone)" levágva, szóközök nélkül.</summary>
    string GetKey() => gameObject.name.Replace("(Clone)", "").Trim();

    // ── Reset új játéknál ─────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ResetCounts() => _counts.Clear();
}
