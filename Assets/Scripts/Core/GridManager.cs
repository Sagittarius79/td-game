using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Négyzetes felülnézetes rács kezelő.
/// A GridOrigin értékét az Inspectorban kell beállítani,
/// hogy egyezzen a Tilemap bal felső sarkával.
/// </summary>
public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    [Header("Rács mérete")]
    public int gridWidth  = 15;
    public int gridHeight = 20;

    [Header("Cella méret")]
    public float tileWidth  = 1.28f;
    public float tileHeight = 1.28f;

    [Header("Rács origó – állítsd a Tilemap bal felső sarkára!")]
    public Vector2 gridOrigin = new Vector2(-9.6f, 12.8f);

    public enum CellType { Empty, Road, Occupied, Castle }
    private CellType[,] grid;

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
        InitGrid();
        BuildFullPath();
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
