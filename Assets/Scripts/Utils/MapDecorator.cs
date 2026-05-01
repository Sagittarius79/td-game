using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Pálya dekorátor – véletlenszerűen fákat és tisztásokat helyez el az üres cellákon.
/// A fák és díszítések toronnyal felülépíthetők (a torony lerakásakor eltávolítódnak).
/// </summary>
public class MapDecorator : MonoBehaviour
{
    [Header("Dekoráció prefab-ok")]
    public GameObject[] treePrefabs;         // kis fa variánsok
    public GameObject[] clearingPrefabs;     // tisztás variánsok (kő, bokor, stb.)

    [Header("Sűrűség (0-1)")]
    [Range(0f, 1f)] public float treeDensity     = 0.20f;
    [Range(0f, 1f)] public float clearingDensity = 0.10f;

    [Header("Darabszám cellánként")]
    [Tooltip("Minimum hány dekoráció kerülhet egy cellára")]
    public int minPerCell = 1;
    [Tooltip("Maximum hány dekoráció kerülhet egy cellára")]
    public int maxPerCell = 3;

    [Header("Eltolás cellán belül")]
    [Tooltip("Maximum vízszintes eltolás a cella közepétől")]
    public float offsetX = 0.3f;
    [Tooltip("Maximum függőleges eltolás a cella közepétől")]
    public float offsetY = 0.2f;

    [Header("Sorting")]
    [Tooltip("Fa prefabok sorting offset-je (magasabb = felül)")]
    public int treeSortingOffset = 2;
    [Tooltip("Kő/bokor prefabok sorting offset-je")]
    public int clearingSortingOffset = 0;

    [Header("Random seed")]
    public int seed = 42;

    // Dekorációk nyilvántartása cellánként (eltávolításhoz)
    private Dictionary<Vector2Int, List<GameObject>> decorations = new Dictionary<Vector2Int, List<GameObject>>();

    void Start()
    {
        // PvP módban a host által küldött közös seed-et használjuk,
        // hogy minden játékosnál ugyanott legyenek a dekorációk.
        if (NetworkGameManager.Instance != null && NetworkGameManager.Instance.IsPvPMode)
            seed = NetworkGameManager.Instance.SharedMapSeed;
        else
            seed = Random.Range(0, int.MaxValue);

        PlaceDecorations();
    }

    void PlaceDecorations()
    {
        if (GridManager.Instance == null) return;

        Random.InitState(seed);
        int w = GridManager.Instance.gridWidth;
        int h = GridManager.Instance.gridHeight;

        for (int col = 0; col < w; col++)
        {
            for (int row = 0; row < h; row++)
            {
                var cell = new Vector2Int(col, row);
                if (!GridManager.Instance.CanBuild(cell)) continue;

                float roll = Random.value;
                bool isTree = roll < treeDensity && treePrefabs.Length > 0;
                bool isClearing = !isTree && roll < treeDensity + clearingDensity && clearingPrefabs.Length > 0;

                if (!isTree && !isClearing) continue;

                int count = Random.Range(minPerCell, maxPerCell + 1);
                Vector3 basePos = GridManager.Instance.GridToWorld(cell);

                // Prefab lista: (prefab, isTree) párok
                var prefabsToPlace = new List<(GameObject prefab, bool isTreeType)>();

                if (isTree)
                    prefabsToPlace.Add((treePrefabs[Random.Range(0, treePrefabs.Length)], true));
                else
                    prefabsToPlace.Add((clearingPrefabs[Random.Range(0, clearingPrefabs.Length)], false));

                for (int i = 1; i < count; i++)
                {
                    if (isTree && clearingPrefabs.Length > 0)
                        prefabsToPlace.Add((clearingPrefabs[Random.Range(0, clearingPrefabs.Length)], false));
                    else if (!isTree && treePrefabs.Length > 0)
                        prefabsToPlace.Add((treePrefabs[Random.Range(0, treePrefabs.Length)], true));
                }

                foreach (var (prefab, isTreeType) in prefabsToPlace)
                {
                    Vector3 offset   = new Vector3(
                        Random.Range(-offsetX, offsetX),
                        Random.Range(-offsetY, offsetY), 0f);
                    Vector3 worldPos = basePos + offset;

                    var go = Instantiate(prefab, worldPos, Quaternion.identity, transform);

                    int sortingOffset = isTreeType ? treeSortingOffset : clearingSortingOffset;

                    // Ha van IsometricSorter, annak offset-jét állítjuk (nem írja felül magát)
                    var sorters = go.GetComponentsInChildren<IsometricSorter>();
                    if (sorters.Length > 0)
                    {
                        foreach (var sorter in sorters)
                            sorter.sortingOffset += sortingOffset;
                    }
                    else
                    {
                        foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>())
                            sr.sortingOrder = GridManager.Instance.GetSortingOrder(col, row) + sortingOffset;
                    }

                    if (!decorations.ContainsKey(cell))
                        decorations[cell] = new List<GameObject>();
                    decorations[cell].Add(go);
                }
            }
        }
    }

    /// <summary>
    /// Igaz, ha az adott cellán van dekoráció (fa, kő, bokor stb.)
    /// </summary>
    public bool HasDecoration(Vector2Int cell)
        => decorations.ContainsKey(cell) && decorations[cell].Count > 0;

    /// <summary>
    /// Torony lerakásakor hívd meg: eltávolítja a dekorációt az adott celláról.
    /// </summary>
    public void RemoveDecorationAt(Vector2Int cell)
    {
        if (decorations.TryGetValue(cell, out var list))
        {
            foreach (var go in list)
                if (go != null) Destroy(go);
            decorations.Remove(cell);
        }
    }
}
