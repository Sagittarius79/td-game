using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Eső átvonulás vizuális effekt + gameplay hatás.
///
/// Vizuális:
///   - Particle System: átlós esőcseppek (streched sprites) kamera felett spawolva
///   - Enyhe kékes-szürke overlay tint a borús égbolt hangulathoz
///
/// Gameplay:
///   - Minden szörny externalSpeedMultiplier += rainSpeedBonus az event alatt
///   - Event végén visszaáll 1.0-ra
///
/// Fázisok:
///   1. Fade-in  – tint + particle emission rámpál
///   2. Hold     – teljes eső, speed boost aktív
///   3. Fade-out – tint + particles elhalnak
/// </summary>
public class RainOverlay : MonoBehaviour
{
    public static RainOverlay Instance { get; private set; }

    [Header("Eső particle")]
    [Tooltip("Esőcsepp szín")]
    public Color dropColor = new Color(0.75f, 0.85f, 1f, 0.7f);
    [Tooltip("Másodpercenkénti cseppek száma (teljes intenzitásnál)")]
    public float emissionRate = 600f;
    [Tooltip("Csepp sebesség (világ unit/mp)")]
    public float dropSpeed = 12f;
    [Tooltip("Szél hatás: vízszintes irány (+x = jobbra)")]
    public float windX = 0.4f;
    [Tooltip("Spawn terület szélessége a kamera fölött (világ unit)")]
    public float spawnWidth = 40f;
    [Tooltip("Spawn terület magassága (világ unit) – mennyivel legyen a kamera felett")]
    public float spawnHeightAbove = 5f;

    [Header("Tint overlay")]
    [Tooltip("Borús ég szín")]
    public Color tintColor = new Color(0.25f, 0.35f, 0.55f, 1f);
    [Tooltip("Maximális tint átlátszóság (0–1)")]
    [Range(0f, 0.5f)]
    public float maxTintAlpha = 0.18f;

    [Header("Fade")]
    public float fadeSpeed = 0.6f;

    [Header("Gameplay hatás")]
    [Tooltip("Szörnyek sebességének szorzója (1.2 = +20%)")]
    public float rainSpeedBonus = 1.2f;

    [Header("Event banner (opcionális)")]
    public GameObject bannerPanel;
    public TextMeshProUGUI bannerText;
    public float bannerDuration = 3f;

    [Header("Figyelmeztetés")]
    [Tooltip("Ennyi másodperccel az event előtt szól az activateSound (előrejelzés)")]
    public float warningDelay = 5f;

    [Header("Hang")]
    public AudioClip activateSound;
    public AudioClip deactivateSound;
    [Tooltip("Végig szóló eső ambient hang (loop). Hangerő a fade-del együtt változik.")]
    public AudioClip rainLoopSound;
    [Tooltip("Maximális hangerő (0–1)")]
    [Range(0f, 1f)]
    public float rainLoopVolume = 0.7f;

    public bool IsActive { get; private set; }

    private SpriteRenderer _tintRenderer;
    private Material _tintMaterial;
    private ParticleSystem _rain;
    private float _alpha;
    private Coroutine _routine;

    private readonly HashSet<Enemy> _boostedEnemies = new();
    private int _lastEnemyCount;
    private AudioSource _loopSource;

    private static readonly int AlphaID = Shader.PropertyToID("_Alpha");
    private static readonly int ColorID = Shader.PropertyToID("_Color");

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        CreateTintOverlay();
        CreateRainParticles();
        CreateLoopSource();
    }

    void CreateLoopSource()
    {
        _loopSource             = gameObject.AddComponent<AudioSource>();
        _loopSource.clip        = rainLoopSound;
        _loopSource.loop        = true;
        _loopSource.playOnAwake = false;
        _loopSource.volume      = 0f;
        _loopSource.spatialBlend = 0f; // 2D hang
    }

    // ── Tint overlay ──────────────────────────────────────────────

    void CreateTintOverlay()
    {
        var go = new GameObject("_RainTintSprite");
        go.transform.position = Camera.main != null
            ? new Vector3(Camera.main.transform.position.x, Camera.main.transform.position.y, 0f)
            : Vector3.zero;
        go.transform.localScale = new Vector3(300f, 300f, 1f);

        _tintRenderer = go.AddComponent<SpriteRenderer>();
        _tintRenderer.sprite = CreateWhiteSquareSprite();
        _tintRenderer.sortingLayerName = "Effects";
        _tintRenderer.sortingOrder = 32698;

        var shader = Shader.Find("Custom/RainOverlay");
        if (shader == null)
        {
            Debug.LogError("[RainOverlay] Custom/RainOverlay shader nem található! Adj hozzá Always Included Shaders listához.");
            return;
        }

        _tintMaterial = new Material(shader);
        _tintMaterial.SetColor(ColorID, tintColor);
        _tintMaterial.SetFloat(AlphaID, 0f);
        _tintRenderer.material = _tintMaterial;
    }

    // ── Particle System ───────────────────────────────────────────

    void CreateRainParticles()
    {
        var go = new GameObject("_RainParticles");
        _rain = go.AddComponent<ParticleSystem>();

        // ─ Main ─
        var main = _rain.main;
        main.loop              = true;
        main.playOnAwake       = false;
        main.simulationSpace   = ParticleSystemSimulationSpace.Local; // kamerával együtt mozog
        main.maxParticles      = 3000;
        main.startLifetime     = new ParticleSystem.MinMaxCurve(2f, 2.5f); // LateUpdate-ben felülírjuk
        main.startSpeed        = new ParticleSystem.MinMaxCurve(0f);        // csak velocityOverLifetime mozgat
        main.startSize         = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
        main.startColor        = new ParticleSystem.MinMaxGradient(dropColor);
        main.gravityModifier   = 0f;

        // Irány: enyhén átlós (windX + lefelé)
        Vector3 dir = new Vector3(windX, -1f, 0f).normalized;
        main.startRotation = new ParticleSystem.MinMaxCurve(
            Mathf.Atan2(dir.x, dir.y),   // szög radián
            Mathf.Atan2(dir.x, dir.y)
        );

        // ─ Emission ─
        var emission = _rain.emission;
        emission.rateOverTime = 0f; // Start-kor 0 – fade-in közben állítjuk

        // ─ Shape: vízszintes vonal a kamera felett ─
        var shape = _rain.shape;
        shape.enabled       = true;
        shape.shapeType     = ParticleSystemShapeType.Rectangle;
        shape.scale         = new Vector3(spawnWidth, 0.1f, 1f);
        shape.position      = Vector3.zero; // LateUpdate-ben követi a kamerát

        // ─ Velocity: Local space-ben mozog (kamerával együtt) ─
        var vel = _rain.velocityOverLifetime;
        vel.enabled = true;
        vel.space   = ParticleSystemSimulationSpace.Local;
        vel.x       = new ParticleSystem.MinMaxCurve(dir.x * dropSpeed);
        vel.y       = new ParticleSystem.MinMaxCurve(dir.y * dropSpeed);
        vel.z       = new ParticleSystem.MinMaxCurve(0f);

        // ─ Renderer ─
        var rend = _rain.GetComponent<ParticleSystemRenderer>();
        rend.sortingLayerName = "Effects";
        rend.sortingOrder     = 32701; // legfelül – a darkness overlay felett is
        rend.renderMode       = ParticleSystemRenderMode.Stretch;
        rend.velocityScale    = 0.08f;
        rend.lengthScale      = 2.5f;
        rend.material         = CreateRainMaterial();

        // ─ Color over lifetime (alpha fade a csepp végén) ─
        var col = _rain.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = new ParticleSystem.MinMaxGradient(grad);

        // ─ Sub Emitter: becsapódási fröccsenés ─
        AddSplashSubEmitter();
    }

    void AddSplashSubEmitter()
    {
        // Child GO – sub-emitter mindig a főrendszer gyereke legyen
        var splashGO = new GameObject("_RainSplash");
        splashGO.transform.SetParent(_rain.transform);
        splashGO.transform.localPosition = Vector3.zero;

        var splash = splashGO.AddComponent<ParticleSystem>();

        // ─ Main ─
        var sm = splash.main;
        sm.loop             = false;
        sm.playOnAwake      = false;
        sm.simulationSpace  = ParticleSystemSimulationSpace.World; // becsapódás helyén marad
        sm.startLifetime    = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
        sm.startSpeed       = new ParticleSystem.MinMaxCurve(0.8f, 2.5f);
        sm.startSize        = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
        sm.startColor       = new ParticleSystem.MinMaxGradient(dropColor);
        sm.gravityModifier  = 0f;
        sm.maxParticles     = 1000;

        // ─ Emission: burst halálakor (sub-emitter triggeri) ─
        var se = splash.emission;
        se.rateOverTime = 0f;
        se.SetBursts(new[] { new ParticleSystem.Burst(0f, 3, 5) });

        // ─ Shape: apró kör → részecskék kifelé repülnek ─
        var ss = splash.shape;
        ss.enabled        = true;
        ss.shapeType      = ParticleSystemShapeType.Circle;
        ss.radius         = 0.05f;
        ss.radiusThickness = 0f; // csak a kerületről → kifelé irányul

        // ─ Color over lifetime: gyors elhalványulás ─
        var sc = splash.colorOverLifetime;
        sc.enabled = true;
        var sg = new Gradient();
        sg.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        sc.color = new ParticleSystem.MinMaxGradient(sg);

        // ─ Size over lifetime: kicsit zsugorodik ─
        var ssl = splash.sizeOverLifetime;
        ssl.enabled = true;
        var szCurve = new AnimationCurve();
        szCurve.AddKey(0f, 1f);
        szCurve.AddKey(1f, 0.2f);
        ssl.size = new ParticleSystem.MinMaxCurve(1f, szCurve);

        // ─ Renderer ─
        var sr = splash.GetComponent<ParticleSystemRenderer>();
        sr.sortingLayerName = "Effects";
        sr.sortingOrder     = 32701;
        sr.material         = CreateRainMaterial();

        // ─ Főrendszerhez kötés: csepp halálakor tüzel ─
        var sub = _rain.subEmitters;
        sub.enabled = true;
        sub.AddSubEmitter(splash, ParticleSystemSubEmitterType.Death,
                          ParticleSystemSubEmitterProperties.InheritNothing);
    }

    Material CreateRainMaterial()
    {
        // Egyszerű fehér material a cseppekhez
        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = Color.white;
        return mat;
    }

    // ── LateUpdate: kamera követés ────────────────────────────────

    void LateUpdate()
    {
        if (Camera.main == null) return;
        var cam = Camera.main.transform.position;

        if (_tintRenderer != null)
            _tintRenderer.transform.position = new Vector3(cam.x, cam.y, 0f);

        if (_rain != null && Camera.main != null)
        {
            // PS transform kövesse a kamerát (Local sim → minden csepp vele mozog)
            _rain.transform.position = new Vector3(cam.x, cam.y, 0f);

            float halfH = Camera.main.orthographicSize;
            float halfW = halfH * Camera.main.aspect;

            // Izometrikus megközelítés: spawn az EGÉSZ képernyőn szétszórva,
            // rövid csíkok → mindenhol egyenletes lefedettség, splash mindenhol látszik.
            Vector3 dir = new Vector3(windX, -1f, 0f).normalized;
            float speedY = Mathf.Abs(dir.y * dropSpeed);

            // Rövid élettartam: egy csepp a képernyőmagasság ~25-45%-át teszi meg
            // → rövid csík jelenik meg véletlenszerű pozícióban, majd splashel
            float streakFractionMin = 0.25f;
            float streakFractionMax = 0.45f;
            float lifetimeMin = halfH * 2f * streakFractionMin / speedY;
            float lifetimeMax = halfH * 2f * streakFractionMax / speedY;
            var psMain = _rain.main;
            psMain.startLifetime = new ParticleSystem.MinMaxCurve(lifetimeMin, lifetimeMax);

            // Spawn terület: teljes képernyő + margó, hogy a képernyőn kívülről
            // beúszó csíkok is legyenek (szél hatás)
            float windMargin   = Mathf.Abs(dir.x * dropSpeed * lifetimeMax) + 2f;
            float spawnOffsetY = halfH * 0.3f; // kicsit feljebb tolva a spawn közép

            var shape = _rain.shape;
            shape.position = new Vector3(0f, spawnOffsetY, 0f);
            shape.scale    = new Vector3(halfW * 2f + windMargin * 2f,
                                         halfH * 2f + windMargin,
                                         1f);
        }
    }

    // ── Publikus API ──────────────────────────────────────────────

    public void Activate(float duration)
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(RainRoutine(duration));
    }

    // ── Fő coroutine ──────────────────────────────────────────────

    IEnumerator RainRoutine(float duration)
    {
        if (_tintMaterial == null || _rain == null) yield break;

        IsActive = true; // lefoglalja az eventet a warning delay alatt is

        // ── Figyelmeztetés: hang előre, majd várakozás ───────────
        AudioManager.Instance?.PlaySFX(activateSound);
        yield return new WaitForSeconds(warningDelay);

        ShowBanner("ESŐ ÁTVONULÁS", duration);

        _rain.Play();

        if (_loopSource != null && rainLoopSound != null)
        {
            _loopSource.volume = 0f;
            _loopSource.Play();
        }

        // 1. Fade-in
        while (_alpha < 1f)
        {
            _alpha = Mathf.MoveTowards(_alpha, 1f, fadeSpeed * Time.deltaTime);
            ApplyAlpha(_alpha);
            yield return null;
        }
        _alpha = 1f;
        ApplyAlpha(_alpha);

        // 2. Hold – speed boost aktív, new enemies is gyorsulnak
        ApplySpeedBoost(rainSpeedBonus);
        _lastEnemyCount = Enemy.AllEnemies.Count;

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            // Újonnan spawnolt ellenségeket is gyorsítjuk
            if (Enemy.AllEnemies.Count != _lastEnemyCount)
            {
                _lastEnemyCount = Enemy.AllEnemies.Count;
                ApplySpeedBoost(rainSpeedBonus);
            }
            yield return null;
        }

        // Speed boost visszaállítás
        RestoreSpeedBoost();

        // 3. Fade-out
        AudioManager.Instance?.PlaySFX(deactivateSound);

        var emission = _rain.emission;
        while (_alpha > 0f)
        {
            _alpha = Mathf.MoveTowards(_alpha, 0f, fadeSpeed * Time.deltaTime);
            ApplyAlpha(_alpha);
            // Particle emission arányosan csökken
            emission.rateOverTime = emissionRate * _alpha;
            yield return null;
        }

        _rain.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (_loopSource != null) _loopSource.Stop();
        IsActive = false;
    }

    void ApplyAlpha(float t)
    {
        if (_tintMaterial != null)
            _tintMaterial.SetFloat(AlphaID, maxTintAlpha * t);

        var emission = _rain.emission;
        emission.rateOverTime = emissionRate * t;

        if (_loopSource != null)
            _loopSource.volume = rainLoopVolume * t;
    }

    // ── Speed boost ───────────────────────────────────────────────

    void ApplySpeedBoost(float multiplier)
    {
        foreach (var enemy in Enemy.AllEnemies)
        {
            if (enemy == null || _boostedEnemies.Contains(enemy)) continue;
            enemy.externalSpeedMultiplier = multiplier;
            _boostedEnemies.Add(enemy);
        }
    }

    void RestoreSpeedBoost()
    {
        foreach (var enemy in _boostedEnemies)
        {
            if (enemy != null)
                enemy.externalSpeedMultiplier = 1f;
        }
        _boostedEnemies.Clear();
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
            UIManager.Instance?.ShowNotification($"{title} ({duration:F0}s)", new Color(0.4f, 0.55f, 0.8f));
        }
    }

    IEnumerator BannerRoutine()
    {
        bannerPanel.SetActive(true);
        yield return new WaitForSeconds(bannerDuration);
        bannerPanel.SetActive(false);
    }
}
