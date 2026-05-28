using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Rúna buff állapot – prefab-referencia alapján számolja a stackeket.
/// altalanos_fejlesztesek.Start() kérdezi le a stack számot.
/// </summary>
public class RuneBuffManager : MonoBehaviour
{
    public static RuneBuffManager Instance { get; private set; }

    private readonly Dictionary<GameObject, int> _stacks = new Dictionary<GameObject, int>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public System.Action OnStackChanged;

    public void AddStack(GameObject runePrefab)
    {
        if (runePrefab == null) return;
        _stacks.TryGetValue(runePrefab, out int current);
        _stacks[runePrefab] = current + 1;
        Debug.Log($"[RuneBuff] {runePrefab.name}: {_stacks[runePrefab]} stack");

        foreach (var tower in Tower.AllTowers)
            tower?.AddRuneStack(runePrefab);

        OnStackChanged?.Invoke();
    }

    public int GetStack(GameObject runePrefab)
    {
        if (runePrefab == null) return 0;
        _stacks.TryGetValue(runePrefab, out int count);
        return count;
    }

    /// <summary>
    /// Visszaadja az effektív küldési árat a kedvezmény alkalmazása után.
    /// RuneConfig nélkül az eredeti ár jön vissza.
    /// </summary>
    public int GetEffectiveSendCost(int baseCost, RuneConfig config)
    {
        if (config == null || config.sendDiscountRunePrefab == null) return baseCost;
        int stacks = GetStack(config.sendDiscountRunePrefab);
        if (stacks == 0) return baseCost;
        float discount = Mathf.Min(config.sendDiscountMax, config.sendDiscountPerStack * stacks);
        return Mathf.Max(1, Mathf.RoundToInt(baseCost * (1f - discount)));
    }
}
