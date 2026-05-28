using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Felhő átvonulás vizuális effekt.
///
/// Alpha-channel-es PNG textúrát görgeti a pályán.
/// Az átlátszó részeken látszik a játék, az átlátszatlan (felhős) részeken
/// a felhő textúra takarja a képet.
///
/// Fázisok:
///   1. Fade-in  – felhő alpha 0 → 1 (fokozatosan jelenik meg)
///   2. Hold     – event tartama, felhő mozog
///   3. Fade-out – felhő alpha 1 → 0 (fokozatosan eltűnik)
/// </summary>
public class CloudOverlay : MonoBehaviour
{
    public static CloudOverlay Instance { get; private set; }

    [Header("Felhő textúra")]
    [Tooltip("Alpha-channel-es PNG. Import beállítás: Wrap Mode = Repeat!")]
    public Texture2D cloudTexture;
    [Tooltip("Skála (kisebb érték = nagyobb felhők). Kb. 0.03–0.08 ajánlott.")]
    public float cloudScale = 0.05f;
    [Tooltip("Mozgás iránya és sebessége (világ unit/mp). (0.5, 0) = balról jobbra.")]
    public Vector2 cloudScroll = new Vector2(0.5f, 0f);

    [Header("Fade")]
    [Tooltip("Fade sebesség másodpercenként")]
    public float fadeSpeed = 0.8f;

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

    private SpriteRenderer _overlayRenderer;
    private Material _material;
    private float _alpha;
    private Coroutine _routine;

    private static readonly int AlphaID      = Shader.PropertyToID("_Alpha");
    private static readonly int CloudScaleID  = Shader.PropertyToID("_CloudScale");
    private static readonly int CloudScrollID = Shader.PropertyToID("_CloudScroll");

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
        var go = new GameObject("_CloudOverlaySprite");
        go.transform.position = Camera.main != null
            ? new Vector3(Camera.main.transform.position.x, Camera.main.transform.position.y, 0f)
            : Vector3.zero;
        go.transform.localScale = new Vector3(300f, 300f, 1f);

        _overlayRenderer = go.AddComponent<SpriteRenderer>();
        _overlayRenderer.sprite = CreateWhiteSquareSprite();
        _overlayRenderer.sortingLayerName = "Effects";
        _overlayRenderer.sortingOrder = 32699; // darkness (32700) fölé kerül, ha egyszerre futnak

        var shader = Shader.Find("Custom/CloudOverlay");
        if (shader == null)
        {
            Debug.LogError("[CloudOverlay] Custom/CloudOverlay shader nem található! " +
                           "Add hozzá az Always Included Shaders listához (Project Settings → Graphics).");
            return;
        }

        _material = new Material(shader);
        _material.SetFloat(AlphaID, 0f);
        _material.SetFloat(CloudScaleID, cloudScale);
        _material.SetVector(CloudScrollID, new Vector4(cloudScroll.x, cloudScroll.y, 0f, 0f));

        if (cloudTexture != null)
            _material.SetTexture("_CloudTex", cloudTexture);
        else
            Debug.LogWarning("[CloudOverlay] Nincs felhő textúra beállítva az inspektorban!");

        _overlayRenderer.material = _material;
    }

    void LateUpdate()
    {
        if (_overlayRenderer == null || Camera.main == null) return;
        var cam = Camera.main.transform.position;
        _overlayRenderer.transform.position = new Vector3(cam.x, cam.y, 0f);
    }

    // ── Publikus API ──────────────────────────────────────────────

    public void Activate(float duration)
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(CloudRoutine(duration));
    }

    // ── Fő coroutine ──────────────────────────────────────────────

    IEnumerator CloudRoutine(float duration)
    {
        if (_material == null) yield break;

        IsActive = true; // lefoglalja az eventet a warning delay alatt is

        // ── Figyelmeztetés: hang előre, majd várakozás ───────────
        AudioManager.Instance?.PlaySFX(activateSound);
        yield return new WaitForSeconds(warningDelay);

        ShowBanner("FELHŐ ÁTVONULÁS", duration);

        // Frissítjük a shader paramétereket (inspector értékek változhattak)
        _material.SetFloat(CloudScaleID, cloudScale);
        _material.SetVector(CloudScrollID, new Vector4(cloudScroll.x, cloudScroll.y, 0f, 0f));
        if (cloudTexture != null)
            _material.SetTexture("_CloudTex", cloudTexture);

        // 1. Fade-in
        while (_alpha < 1f)
        {
            _alpha = Mathf.MoveTowards(_alpha, 1f, fadeSpeed * Time.deltaTime);
            _material.SetFloat(AlphaID, _alpha);
            yield return null;
        }
        _alpha = 1f;
        _material.SetFloat(AlphaID, _alpha);

        // 2. Hold (a shader a _Time.y alapján görgeti a textúrát)
        yield return new WaitForSeconds(duration);

        // 3. Fade-out
        AudioManager.Instance?.PlaySFX(deactivateSound);
        while (_alpha > 0f)
        {
            _alpha = Mathf.MoveTowards(_alpha, 0f, fadeSpeed * Time.deltaTime);
            _material.SetFloat(AlphaID, _alpha);
            yield return null;
        }

        IsActive = false;
    }

    // ── Segédfüggvények ───────────────────────────────────────────

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
            UIManager.Instance?.ShowNotification($"{title} ({duration:F0}s)", new Color(0.6f, 0.7f, 0.9f));
        }
    }

    IEnumerator BannerRoutine()
    {
        bannerPanel.SetActive(true);
        yield return new WaitForSeconds(bannerDuration);
        bannerPanel.SetActive(false);
    }
}
