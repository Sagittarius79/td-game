using UnityEngine;

/// <summary>
/// ═══════════════════════════════════════════════════════
///  AURA VIZUÁLIS JELZŐ – teli ellipszis
///  Procedurálisan generált teli ellipszis mesh,
///  ami a farkas körül halványan látszik.
///  Automatikusan igazodik az AuraEffect.auraRadius értékéhez.
/// ═══════════════════════════════════════════════════════
/// </summary>
[RequireComponent(typeof(AuraEffect))]
public class AuraVisual : MonoBehaviour
{
    [Header("Vizuális beállítások")]
    [Tooltip("Az ellipszis színe és átlátszósága")]
    public Color auraColor = new Color(1f, 0.8f, 0f, 0.15f);

    [Tooltip("Vízszintes lapítás – izometrikus nézethez igazítás (0.5 = felére lapított)")]
    [Range(0.1f, 1f)]
    public float verticalScale = 0.5f;

    [Tooltip("Hány szegmensből álljon az ellipszis (több = simább)")]
    [Range(16, 64)]
    public int segments = 32;

    // ── Belső állapot ─────────────────────────────────────
    private MeshFilter   mf;
    private MeshRenderer mr;
    private AuraEffect   aura;
    private float        lastRadius = -1f;

    // ══════════════════════════════════════════════════════
    //  INICIALIZÁLÁS
    // ══════════════════════════════════════════════════════

    void Awake()
    {
        aura = GetComponent<AuraEffect>();

        // Gyerek objektum létrehozása a mesh-nek
        // (hogy a sorting külön kezelhető legyen)
        GameObject visual = new GameObject("AuraVisualMesh");
        visual.transform.SetParent(transform, false);
        visual.transform.localPosition = Vector3.zero;

        mf = visual.AddComponent<MeshFilter>();
        mr = visual.AddComponent<MeshRenderer>();

        // Beépített anyag, transzparens módban
        Material mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = auraColor;
        mr.material = mat;

        // Sorting – a szörny sprite mögé kerül
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        mr.sortingLayerName = sr != null ? sr.sortingLayerName : "Default";
        mr.sortingOrder     = sr != null ? sr.sortingOrder - 1 : -1;

        BuildMesh(aura.auraRadius);
    }

    // ══════════════════════════════════════════════════════
    //  UPDATE – sugár változáskor újraépíti a mesh-t
    // ══════════════════════════════════════════════════════

    void Update()
    {
        if (Mathf.Abs(aura.auraRadius - lastRadius) > 0.01f)
            BuildMesh(aura.auraRadius);
    }

    // ══════════════════════════════════════════════════════
    //  MESH GENERÁLÁS – teli ellipszis
    //  Középpont + körkörös háromszögek (tortaszelet elv)
    // ══════════════════════════════════════════════════════

    void BuildMesh(float radius)
    {
        lastRadius = radius;

        Mesh mesh = new Mesh();

        // Csúcspontok: középpont (0) + kerület pontjai (1..segments)
        Vector3[] vertices = new Vector3[segments + 1];
        vertices[0] = Vector3.zero;  // középpont

        for (int i = 0; i < segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            vertices[i + 1] = new Vector3(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius * verticalScale,
                0f);
        }

        // Háromszögek: minden szegmens egy tortaszelet
        int[] triangles = new int[segments * 3];
        for (int i = 0; i < segments; i++)
        {
            triangles[i * 3 + 0] = 0;                        // középpont
            triangles[i * 3 + 1] = i + 1;                    // jelenlegi kerület pont
            triangles[i * 3 + 2] = (i + 1) % segments + 1;  // következő kerület pont
        }

        // UV koordináták (egyszerű, nem kell textúrához)
        Vector2[] uvs = new Vector2[segments + 1];
        uvs[0] = new Vector2(0.5f, 0.5f);
        for (int i = 0; i < segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            uvs[i + 1] = new Vector2(
                Mathf.Cos(angle) * 0.5f + 0.5f,
                Mathf.Sin(angle) * 0.5f + 0.5f);
        }

        mesh.vertices  = vertices;
        mesh.triangles = triangles;
        mesh.uv        = uvs;
        mesh.RecalculateNormals();

        mf.mesh = mesh;
    }
}
