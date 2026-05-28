using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Mágikus Sötétség vizuális effekt – shader-alapú lágy átmenettel.
///
/// Egyetlen fullscreen overlay (Custom/DarknessOverlay shader), ami pixel-szinten
/// számolja a tornyok távolságát és smoothstep-pel adja meg a sötétség mértékét.
/// Nincs SpriteMask → nincs karika artefakt, igazi soft edge.
///
/// Fázisok:
///   1. Fade-in  – alpha 0 → maxDarkness (lights végig aktívak)
///   2. Hold     – event tartama, HP bar láthatóság torony hatókör szerint
///   3. Fade-out – alpha maxDarkness → 0
/// </summary>
public class DarknessOverlay : MonoBehaviour
{
    public static DarknessOverlay Instance { get; private set; }

    [Header("Sötétség")]
    [Tooltip("Maximális sötétség (0-1)")]
    public float maxDarkness = 0.92f;
    [Tooltip("Fade sebesség – másodpercenként")]
    public float fadeSpeed = 1.5f;

    [Header("Fénykör")]
    [Tooltip("A torony attackRange-jéhez képest mekkora a látható kör")]
    public float lightRadiusMultiplier = 1.05f;
    [Tooltip("Puha szél a kör szélen (0 = éles, 0.8 = nagyon puha)")]
    [Range(0f, 0.95f)]
    public float softEdge = 0.5f;

    [Header("Felhők (opcionális)")]
    [Tooltip("Felhő textúra (Wrap Mode = Repeat!). Ha null → nincs felhő.")]
    public Texture2D cloudTexture;
    [Tooltip("Felhő méret skála (kisebb = nagyobb felhők). 0.05 = ~20 world unit tile méret.")]
    public float cloudScale = 0.05f;
    [Tooltip("Felhők haladási iránya és sebessége (világ unit/mp). (0.5, 0) = balról jobbra lassan.")]
    public Vector2 cloudScroll = new Vector2(0.5f, 0f);
    [Tooltip("Felhő intenzitása (0 = nem látszik, 1 = teljesen elfedi a sötétet)")]
    [Range(0f, 1f)]
    public float cloudIntensity = 0.7f;

    [Header("Event banner (opcionális)")]
    [Tooltip("Ha null → ShowNotification kerül meghívásra")]
    public GameObject bannerPanel;
    public TextMeshProUGUI bannerText;
    public float bannerDuration = 3f;

    [Header("Figyelmeztetés")]
    [Tooltip("Ennyi másodperccel az event előtt szól az activateSound (előrejelzés)")]
    public float warningDelay = 5f;

    [Header("Hang")]
    public AudioClip activateSound;
    public AudioClip deactivateSound;

    public bool IsActive { get; private set; }

    private const int MAX_LIGHTS = 64;

    private SpriteRenderer _overlayRenderer;
    private Material _material;
    private float _alpha;
    private Coroutine _routine;

    private readonly Vector4[] _lightData = new Vector4[MAX_LIGHTS];
    private readonly Dictionary<Enemy, CanvasGroup> _hpBarCache = new();

    private static readonly int AlphaID          = Shader.PropertyToID("_Alpha");
    private static readonly int SoftEdgeID       = Shader.PropertyToID("_SoftEdge");
    private static readonly int LightCountID    = Shader.PropertyToID("_LightCount");
    private static readonly int LightsID         = Shader.PropertyToID("_Lights");
    private static readonly int CloudTexID       = Shader.PropertyToID("_CloudTex");
    private static readonly int CloudScaleID     = Shader.PropertyToID("_CloudScale");
    private static readonly int CloudScrollID    = Shader.PropertyToID("_CloudScroll");
    private static readonly int CloudIntensityID = Shader.PropertyToID("_CloudIntensity");

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        CreateOverlayRenderer();
    }

    void CreateOverlayRenderer()
    {
        var go = new GameObject("_DarknessOverlaySprite");
        go.transform.position = Camera.main != null
            ? new Vector3(Camera.main.transform.position.x, Camera.main.transform.position.y, 0f)
            : Vector3.zero;
        go.transform.localScale = new Vector3(300f, 300f, 1f);

        _overlayRenderer = go.AddComponent<SpriteRenderer>();
        _overlayRenderer.sprite = CreateWhiteSquareSprite();
        _overlayRenderer.sortingLayerName = "Effects";
        _overlayRenderer.sortingOrder = 32700;

        var shader = Shader.Find("Custom/DarknessOverlay");
        if (shader == null)
        {
            Debug.LogError("[DarknessOverlay] Custom/DarknessOverlay shader nem található! Add hozzá az Always Included Shaders listához (Project Settings → Graphics).");
            return;
        }

        _material = new Material(shader);
        _material.SetColor("_Color", new Color(0f, 0f, 0f, 1f));
        _material.SetFloat(AlphaID, 0f);
        _material.SetFloat(SoftEdgeID, softEdge);
        _material.SetInt(LightCountID, 0);
        ApplyCloudSettings();
        _overlayRenderer.material = _material;
    }

    void ApplyCloudSettings()
    {
        if (_material == null) return;
        if (cloudTexture != null)
        {
            cloudTexture.wrapMode = TextureWrapMode.Repeat;
            _material.SetTexture(CloudTexID, cloudTexture);
            _material.SetFloat(CloudIntensityID, cloudIntensity);
        }
        else
        {
            _material.SetFloat(CloudIntensityID, 0f);
        }
        _material.SetFloat(CloudScaleID, cloudScale);
        _material.SetVector(CloudScrollID, new Vector4(cloudScroll.x, cloudScroll.y, 0f, 0f));
    }

    void LateUpdate()
    {
        if (_overlayRenderer == null || Camera.main == null) return;
        var cam = Camera.main.transform.position;
        _overlayRenderer.transform.position = new Vector3(cam.x, cam.y, 0f);

        if (IsActive && _material != null)
            UpdateLights();
    }

    void UpdateLights()
    {
        float tileSize = GetTileSize();
        int count = 0;
        foreach (var tower in Tower.AllTowers)
        {
            if (tower == null) continue;
            if (count >= MAX_LIGHTS) break;
            float range = tower.attackRange * tileSize * lightRadiusMultiplier;
            var pos = tower.transform.position;
            _lightData[count] = new Vector4(pos.x, pos.y, range, 0f);
            count++;
        }
        _material.SetInt(LightCountID, count);
        _material.SetVectorArray(LightsID, _lightData);
        _material.SetFloat(SoftEdgeID, softEdge);
        ApplyCloudSettings();
    }

    // ── Publikus API ──────────────────────────────────────────────

    public void Activate(float duration)
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(DarknessRoutine(duration));
    }

    // ── Fő coroutine ──────────────────────────────────────────────

    IEnumerator DarknessRoutine(float duration)
    {
        if (_material == null) yield break;

        IsActive = true; // lefoglalja az eventet a warning delay alatt is

        // ── Figyelmeztetés: hang előre, majd várakozás ───────────
        AudioManager.Instance?.PlaySFX(activateSound);
        yield return new WaitForSeconds(warningDelay);

        ShowBanner("MÁGIKUS SÖTÉTSÉG", duration);

        // 1. Fade-in
        while (_alpha < maxDarkness)
        {
            _alpha = Mathf.MoveTowards(_alpha, maxDarkness, fadeSpeed * Time.deltaTime);
            _material.SetFloat(AlphaID, _alpha);
            yield return null;
        }
        _alpha = maxDarkness;
        _material.SetFloat(AlphaID, _alpha);

        // 2. Hold
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            UpdateHpBars();
            yield return null;
        }
        RestoreHpBars();

        // 3. Fade-out
        AudioManager.Instance?.PlaySFX(deactivateSound);
        while (_alpha > 0f)
        {
            _alpha = Mathf.MoveTowards(_alpha, 0f, fadeSpeed * Time.deltaTime);
            _material.SetFloat(AlphaID, _alpha);
            yield return null;
        }

        _hpBarCache.Clear();
        IsActive = false;
    }

    // ── HP sávok (Canvas) ─────────────────────────────────────────

    void UpdateHpBars()
    {
        float tileSize = GetTileSize();
        foreach (var enemy in Enemy.AllEnemies)
        {
            if (enemy?.healthBarRoot == null) continue;
            bool inRange = IsInAnyTowerRange(enemy, tileSize);
            GetOrAddCanvasGroup(enemy).alpha = inRange ? 1f : 0f;
        }
    }

    void RestoreHpBars()
    {
        foreach (var enemy in Enemy.AllEnemies)
        {
            if (enemy?.healthBarRoot == null) continue;
            GetOrAddCanvasGroup(enemy).alpha = 1f;
        }
    }

    bool IsInAnyTowerRange(Enemy enemy, float tileSize)
    {
        Vector3 pos = enemy.transform.position;
        foreach (var tower in Tower.AllTowers)
        {
            if (tower == null) continue;
            if (Vector3.Distance(tower.transform.position, pos) <= tower.attackRange * tileSize)
                return true;
        }
        return false;
    }

    CanvasGroup GetOrAddCanvasGroup(Enemy enemy)
    {
        if (_hpBarCache.TryGetValue(enemy, out var cg) && cg != null) return cg;
        cg = enemy.healthBarRoot.GetComponent<CanvasGroup>();
        if (cg == null) cg = enemy.healthBarRoot.AddComponent<CanvasGroup>();
        _hpBarCache[enemy] = cg;
        return cg;
    }

    // ── Segédfüggvények ───────────────────────────────────────────

    static float GetTileSize() =>
        GridManager.Instance != null
            ? (GridManager.Instance.tileWidth + GridManager.Instance.tileHeight) * 0.5f
            : 1.28f;

    static Sprite CreateWhiteSquareSprite()
    {
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }

    // ── Banner ───────────────────────────────────────────────────

    void ShowBanner(string title, float duration)
    {
        if (bannerPanel != null && bannerText != null)
        {
            bannerText.text = title;
            StartCoroutine(BannerRoutine());
        }
        else
        {
            UIManager.Instance?.ShowNotification($"{title} ({duration:F0}s)", new Color(0.3f, 0f, 0.6f));
        }
    }

    IEnumerator BannerRoutine()
    {
        bannerPanel.SetActive(true);
        yield return new WaitForSeconds(bannerDuration);
        bannerPanel.SetActive(false);
    }
}
