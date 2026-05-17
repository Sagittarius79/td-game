using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Négyzetes felülnézetes rács kezelő.
/// Ha a maps tömb ki van töltve, induláskor random választ egyet,
/// és a MapDefinition adatait alkalmazza (gridOrigin, pathWaypoints stb.).
/// Ha a maps üres, a hardcode-olt értékek maradnak érvényben.
/// </summary>
public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    [Header("Pályák")]
    [Tooltip("SSF (solo) módban ebből a listából választ random.")]
    public MapDefinition[] ssfMaps;
    [Tooltip("PvP módban ebből a listából választ (seed alapján szinkronizálva).")]
    public MapDefinition[] pvpMaps;

    /// <summary>Az aktuálisan kiválasztott pálya (null, ha nincs maps beállítva).</summary>
    public MapDefinition SelectedMap { get; private set; }

    /// <summary>A kiválasztott pálya indexe a maps tömbben (-1, ha nincs).</summary>
    public int SelectedMapIndex { get; private set; } = -1;

    [Header("Rács mérete (felülírja a MapDefinition, ha van)")]
    public int gridWidth  = 15;
    public int gridHeight = 20;

    [Header("Cella méret")]
    public float tileWidth  = 1.28f;
    public float tileHeight = 1.28f;

    [Header("Rács origó – felülírja a MapDefinition, ha van")]
    public Vector2 gridOrigin = new Vector2(-9.6f, 12.8f);

    public enum CellType { Empty, Road, Occupied, Castle, Rubble }
    private CellType[,] grid;

    struct RubbleData
    {
        public int roundsLeft;
        public GameObject instance;
    }
    private Dictionary<Vector2Int, RubbleData> rubbleCells = new Dictionary<Vector2Int, RubbleData>();

    [Header("Törmelék")]
    [Tooltip("Törmelék sprite prefab – a lerombolódott torony helyén jelenik meg")]
    public GameObject rubblePrefab;
    [Tooltip("Hány körig marad a törmelék (alapértelmezett: 5)")]
    public int rubbleDuration = 5;

    [Header("Út waypoint-ok (col, row) – 0,0 = bal felső sarok")]
    public Vector2Int[] pathWaypoints = new Vector2Int[]
    {
        new Vector2Int(7,  0),
        new Vector2Int(7,  4),
        new Vector2Int(4,  4),
        new Vector2Int(4,  8),
        new Vector2Int(10, 8),
        new Vector2Int(10, 14),
        new Vector2Int(7,  14),
        new Vector2Int(7,  19),
    };

    private List<Vector2Int> fullPath = new List<Vector2Int>();
    public IReadOnlyList<Vector2Int> FullPath => fullPath;

    public Vector2Int SpawnCell  => pathWaypoints[0];
    public Vector2Int CastleCell => pathWaypoints[pathWaypoints.Length - 1];

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        ApplyRandomMap();
        InitGrid();
        BuildFullPath();
    }

    /// <summary>
    /// Ha van legalább egy MapDefinition a maps tömbben, random választ egyet
    /// és alkalmazza az adatait. Különben maradnak a hardcode-olt értékek.
    /// </summary>
    void ApplyRandomMap()
    {
        bool isPvP = NetworkGameManager.Instance != null
                     && NetworkGameManager.Instance.IsPvPMode;

        MapDefinition[] pool = isPvP ? pvpMaps : ssfMaps;

        if (pool == null || pool.Length == 0) return;

        SelectedMapIndex = isPvP
            ? Mathf.Abs(NetworkGameManager.Instance.SharedMapSeed) % pool.Length
            : Random.Range(0, pool.Length);

        SelectedMap = pool[SelectedMapIndex];

        if (SelectedMap == null)
        {
            Debug.LogWarning("GridManager: a kiválasztott MapDefinition null, maradnak az alapértelmezett értékek.");
            return;
        }

        gridOrigin    = SelectedMap.gridOrigin;
        gridWidth     = SelectedMap.gridWidth;
        gridHeight    = SelectedMap.gridHeight;
        pathWaypoints = SelectedMap.pathWaypoints;

        Debug.Log($"GridManager: '{SelectedMap.name}' pálya kiválasztva (index: {SelectedMapIndex})");
    }

    void InitGrid()
    {
        grid = new CellType[gridWidth, gridHeight];
    }

    void BuildFullPath()
    {
        fullPath.Clear();
        for (int i = 0; i < pathWaypoints.Length - 1; i++)
        {
            var segment = GetStraightLine(pathWaypoints[i], pathWaypoints[i + 1]);
            for (int j = (i == 0 ? 0 : 1); j < segment.Count; j++)
                fullPath.Add(segment[j]);
        }
        foreach (var cell in fullPath)
            if (IsInBounds(cell))
                grid[cell.x, cell.y] = CellType.Road;

        if (IsInBounds(CastleCell))
            grid[CastleCell.x, CastleCell.y] = CellType.Castle;
    }

    List<Vector2Int> GetStraightLine(Vector2Int from, Vector2Int to)
    {
        var result = new List<Vector2Int>();
        int dx = (to.x > from.x) ? 1 : (to.x < from.x) ? -1 : 0;
        int dy = (to.y > from.y) ? 1 : (to.y < from.y) ? -1 : 0;
        Vector2Int cur = from;
        while (cur != to) { result.Add(cur); cur += new Vector2Int(dx, dy); }
        result.Add(to);
        return result;
    }

    // ── Koordináta konverziók ─────────────────────────────────────

    /// <summary>
    /// Rács (col, row) → Unity world pozíció.
    /// col=0, row=0 = bal FELSŐ sarok (spawn oldal)
    /// </summary>
    public Vector3 GridToWorld(int col, int row, float zOffset = 0f)
    {
        float x = gridOrigin.x + col * tileWidth  + tileWidth  * 0.5f;
        float y = gridOrigin.y - row * tileHeight - tileHeight * 0.5f;
        return new Vector3(x, y, zOffset);
    }

    public Vector3 GridToWorld(Vector2Int cell, float zOffset = 0f)
        => GridToWorld(cell.x, cell.y, zOffset);

    /// <summary>World pozíció → legközelebbi rács cella</summary>
    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        int col = Mathf.FloorToInt((worldPos.x - gridOrigin.x) / tileWidth);
        int row = Mathf.FloorToInt((gridOrigin.y - worldPos.y) / tileHeight);
        return new Vector2Int(col, row);
    }

    // ── Cella lekérdezések ────────────────────────────────────────

    public bool IsInBounds(Vector2Int cell)
        => cell.x >= 0 && cell.x < gridWidth && cell.y >= 0 && cell.y < gridHeight;

    public CellType GetCell(Vector2Int cell)
        => IsInBounds(cell) ? grid[cell.x, cell.y] : CellType.Occupied;

    public bool CanBuild(Vector2Int cell)
        => IsInBounds(cell) && grid[cell.x, cell.y] == CellType.Empty;

    public void SetOccupied(Vector2Int cell)
    { if (IsInBounds(cell)) grid[cell.x, cell.y] = CellType.Occupied; }

    public void SetEmpty(Vector2Int cell)
    { if (IsInBounds(cell) && grid[cell.x, cell.y] == CellType.Occupied) grid[cell.x, cell.y] = CellType.Empty; }

    public int GetRubbleRoundsLeft(Vector2Int cell)
        => rubbleCells.TryGetValue(cell, out var d) ? d.roundsLeft : 0;

    public void PlaceRubble(Vector2Int cell)
    {
        if (!IsInBounds(cell)) return;
        grid[cell.x, cell.y] = CellType.Rubble;

        GameObject rubbleObj = null;
        if (rubblePrefab != null)
        {
            Vector3 worldPos = GridToWorld(cell);
            rubbleObj = Instantiate(rubblePrefab, worldPos, Quaternion.identity);
            var sr = rubbleObj.GetComponent<SpriteRenderer>();
            if (sr != null)
                sr.sortingOrder = GetSortingOrder(cell.x, cell.y);
        }

        var data = new RubbleData { roundsLeft = rubbleDuration, instance = rubbleObj };
        rubbleCells[cell] = data;

        if (rubbleObj != null)
        {
            var clickable = rubbleObj.GetComponent<RubbleClickable>();
            if (clickable != null)
                clickable.Init(cell);
        }
    }

    public void TickRubble()
    {
        var keys = new List<Vector2Int>(rubbleCells.Keys);
        foreach (var key in keys)
        {
            var data = rubbleCells[key];
            data.roundsLeft--;
            if (data.roundsLeft <= 0)
            {
                if (data.instance != null) Destroy(data.instance);
                if (IsInBounds(key) && grid[key.x, key.y] == CellType.Rubble)
                    grid[key.x, key.y] = CellType.Empty;
                rubbleCells.Remove(key);
            }
            else
            {
                rubbleCells[key] = data;
            }
        }
    }

    public int GetSortingOrder(int col, int row) => row;

    // ── Debug vizualizáció ────────────────────────────────────────

    void OnDrawGizmos()
    {
        for (int col = 0; col < gridWidth; col++)
        {
            for (int row = 0; row < gridHeight; row++)
            {
                Vector3 pos = GridToWorld(col, row);
                bool isPath = false;
                if (grid != null)
                    isPath = grid[col, row] == CellType.Road || grid[col, row] == CellType.Castle;

                Gizmos.color = isPath
                    ? new Color(1f, 0.5f, 0f, 0.6f)
                    : new Color(0f, 1f, 0f, 0.1f);

                Gizmos.DrawWireCube(pos, new Vector3(tileWidth * 0.9f, tileHeight * 0.9f, 0.01f));
            }
        }

        // Spawn pont jelölése
        if (pathWaypoints != null && pathWaypoints.Length > 0)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(GridToWorld(pathWaypoints[0]), 0.3f);
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(GridToWorld(pathWaypoints[pathWaypoints.Length - 1]), 0.3f);
        }
    }
}
