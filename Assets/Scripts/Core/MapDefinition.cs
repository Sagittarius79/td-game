using UnityEngine;

/// <summary>
/// Egy pálya összes adatát tárolja.
/// Inspector-ból vagy kódból átadható a GridManager-nek.
/// </summary>
[CreateAssetMenu(menuName = "TD/Map Definition", fileName = "NewMapDefinition")]
public class MapDefinition : ScriptableObject
{
    [Header("Rács pozíció")]
    [Tooltip("A Tilemap bal felső sarkának world-space koordinátája")]
    public Vector2 gridOrigin = new Vector2(-9.6f, 12.8f);

    [Header("Rács méret")]
    public int gridWidth  = 15;
    public int gridHeight = 20;

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

    [Header("Kamera")]
    [Tooltip("A kamera startpozíciója erre a pályára nézve")]
    public Vector3 cameraStartPosition = new Vector3(0f, 0f, -10f);

    [Tooltip("Orthographic size induláskor (nagyobb = távolabb/kizoomolva). 0 = marad az Inspector értéke.")]
    public float cameraOrthographicSize = 0f;
}
