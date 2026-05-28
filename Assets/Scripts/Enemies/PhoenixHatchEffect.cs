using System.Collections;
using UnityEngine;

/// <summary>
/// Phoenix tojás láng effekt – procedurálisan létrehozott tűz + szikra részecskék.
///
/// Két felhasználás:
///   A) LOOP mód  (isLooping = true)  – Húzd rá a tojás prefabra közvetlenül.
///      Folyamatosan ég a tojáson amíg él. Magától leáll ha a tojás meghal.
///
///   B) BURST mód (isLooping = false) – Ezt a prefabot add a PhoenixEgg › Hatch Effect mezőbe.
///      Keltetéskor egyszer lejátssza a láng-robbanást, majd megsemmisül.
///
/// Unity beállítás:
///   1. Hozz létre üres GameObject prefabot, add hozzá ezt a komponenst
///   2. Állítsd be a kívánt mezőket (alapértelmezések működnek)
///   3. Loop módban: a prefabot húzd rá a tojás prefabra
///      Burst módban: PhoenixEgg › Hatch Effect mezőbe húzd be
/// </summary>
public class PhoenixHatchEffect : MonoBehaviour
{
    [Header("Mód")]
    [Tooltip("True = folyamatos égő láng (tojáson). False = egyszeri burst (keltetéskor).")]
    public bool isLooping = false;

    [Header("Méretezés")]
    [Tooltip("Effekt mérete – 1 = alap méret")]
    public float scale = 1f;

    [Header("Lángok – színek")]
    public Color flameCoreColor  = new Color(1.0f, 0.95f, 0.35f, 1f); // sárga mag
    public Color flameMidColor   = new Color(1.0f, 0.40f, 0.05f, 1f); // narancsvörös
    public Color flameTipColor   = new Color(0.55f, 0.08f, 0.01f, 0f); // sötétvörös, alpha=0

    [Header("Szikrák – szín")]
    public Color sparkColor      = new Color(1.0f, 0.88f, 0.25f, 1f); // arany szikra

    [Header("Lángok – erősség")]
    [Tooltip("Burst módban hány láng részecske induljon (loop: mp-enkénti kibocsátás)")]
    public int   flameBurstCount = 45;
    [Tooltip("Lángok felfelé emelkedési sebessége")]
    public float flameRiseSpeed  = 2.5f;

    [Header("Szikrák – erősség")]
    [Tooltip("Burst módban hány szikra induljon")]
    public int   sparkBurstCount = 25;
    [Tooltip("Szikrák kiröpülési sebessége")]
    public float sparkSpeed      = 3.5f;

    // ── Belső referenciák ──────────────────────────────────────
    private ParticleSystem _flamePS;
    private ParticleSystem _sparkPS;

    // ── Lifecycle ──────────────────────────────────────────────

    void Start()
    {
        _flamePS = CreateFlameSystem();
        _sparkPS = CreateSparkSystem();

        if (isLooping)
        {
            // Loop módban: ha van szülő Enemy, leállunk amikor az meghal
            var parentEnemy = GetComponentInParent<Enemy>();
            if (parentEnemy != null)
                parentEnemy.OnDied += _ => StartCoroutine(StopLoopAndFade());
            else
            {
                // Nincs szülő Enemy (pl. véletlenül hatchEffect-ként lett beállítva
                // isLooping=true-val) → fallback auto-destroy, hogy ne maradjon örökre
                Debug.LogWarning("[PhoenixHatchEffect] isLooping=true de nincs szülő Enemy! " +
                                 "Hatch Effect-ként való használathoz isLooping=false kell. Fallback destroy 6s.");
                Destroy(gameObject, 6f);
            }
        }
        else
        {
            // Burst mód: auto-destroy a hosszabb lifetime + buffer után
            float longestLife = Mathf.Max(1.2f, 2.0f) * scale + 0.5f;
            Destroy(gameObject, longestLife);
        }
    }

    // ── Stop (loop mód) ────────────────────────────────────────

    /// <summary>
    /// Azonnal törli az összes részecskét és megsemmisíti a GO-t.
    /// PhoenixEgg hívja kikeléskor, hogy a loop effekt ne maradjon árván.
    /// </summary>
    public void ForceStop()
    {
        StopAllCoroutines();
        if (_flamePS != null) _flamePS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (_sparkPS != null) _sparkPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        Destroy(gameObject);
    }

    IEnumerator StopLoopAndFade()
    {
        // Leállítjuk az emissziót, a meglévő részecskék kifutnak
        if (_flamePS != null) _flamePS.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        if (_sparkPS != null) _sparkPS.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        yield return new WaitForSeconds(1.5f);
        Destroy(gameObject);
    }

    // ── Láng ParticleSystem ────────────────────────────────────

    ParticleSystem CreateFlameSystem()
    {
        var go = new GameObject("_PhoenixFlames");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;

        var ps   = go.AddComponent<ParticleSystem>();
        var rend = ps.GetComponent<ParticleSystemRenderer>();

        // Renderer
        rend.material         = BuildMaterial(flameCoreColor, additive: true);
        rend.renderMode       = ParticleSystemRenderMode.Billboard;
        rend.sortingLayerName = "Effects";
        rend.sortingOrder     = 32760;

        // ─ Main ─
        var main = ps.main;
        main.loop            = isLooping;
        main.playOnAwake     = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local; // Local: GO megsemmisülésekor azonnali eltűnés
        main.maxParticles    = isLooping ? 120 : 80;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.45f * scale, 1.1f * scale);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.3f, 1.2f);  // kis radiális szóródás
        main.startSize       = new ParticleSystem.MinMaxCurve(0.10f * scale, 0.30f * scale);
        main.gravityModifier = 0f;

        // ─ Szín gradient: sárga → narancsvörös → sötétvörös+átlátszó ─
        var gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(flameCoreColor, 0.00f),
                new GradientColorKey(flameMidColor,  0.40f),
                new GradientColorKey(flameTipColor,  1.00f),
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0.0f, 0.00f),  // fade in
                new GradientAlphaKey(0.9f, 0.10f),
                new GradientAlphaKey(0.8f, 0.50f),
                new GradientAlphaKey(0.0f, 1.00f),  // fade out
            }
        );
        main.startColor = new ParticleSystem.MinMaxGradient(gradient);

        // ─ Shape: vékony körgyűrű a tojás tövénél ─
        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius    = 0.25f * scale;
        shape.arc       = 360f;
        shape.radiusThickness = 0.8f;   // nem csak a szél, hanem a felület is bocsát ki

        // ─ Felfelé emelkedés (velocityOverLifetime Y = +érték) ─
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space   = ParticleSystemSimulationSpace.Local;
        vel.x       = new ParticleSystem.MinMaxCurve(0f);
        vel.y       = new ParticleSystem.MinMaxCurve(flameRiseSpeed * 0.7f * scale,
                                                      flameRiseSpeed * 1.3f * scale);
        vel.z       = new ParticleSystem.MinMaxCurve(0f);

        // ─ Méret az élettartam során: kicsit nő, majd elolvad ─
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size    = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0.00f, 0.3f),
            new Keyframe(0.20f, 1.0f),
            new Keyframe(1.00f, 0.0f)
        ));

        // ─ Emisszió: burst vagy loop ─
        var emission = ps.emission;
        emission.enabled     = true;
        emission.rateOverTime = isLooping ? flameBurstCount * 0.6f : 0f;
        if (!isLooping)
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f,
                                           (short)flameBurstCount,
                                           (short)(flameBurstCount + 10)) });

        ps.Play();
        return ps;
    }

    // ── Szikra ParticleSystem ──────────────────────────────────

    ParticleSystem CreateSparkSystem()
    {
        var go = new GameObject("_PhoenixSparks");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;

        var ps   = go.AddComponent<ParticleSystem>();
        var rend = ps.GetComponent<ParticleSystemRenderer>();

        // Renderer
        rend.material         = BuildMaterial(sparkColor, additive: true);
        rend.renderMode       = ParticleSystemRenderMode.Billboard;
        rend.sortingLayerName = "Effects";
        rend.sortingOrder     = 32761;

        // ─ Main ─
        var main = ps.main;
        main.loop            = isLooping;
        main.playOnAwake     = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local; // Local: GO megsemmisülésekor azonnali eltűnés
        main.maxParticles    = isLooping ? 60 : 40;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.35f * scale, 0.85f * scale);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(sparkSpeed * 0.5f,
                                                               sparkSpeed * 1.5f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.03f * scale, 0.07f * scale);
        main.startColor      = new ParticleSystem.MinMaxGradient(sparkColor);
        main.gravityModifier = 0.4f;    // szikrák kissé lehullanak (ívelt pálya)

        // ─ Shape: szféra → szikrák minden irányba ─
        var shape = ps.shape;
        shape.enabled        = true;
        shape.shapeType      = ParticleSystemShapeType.Circle;
        shape.radius         = 0.15f * scale;
        shape.radiusThickness = 1f;

        // ─ Felfelé enyhe sodródás ─
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space   = ParticleSystemSimulationSpace.Local;
        vel.x       = new ParticleSystem.MinMaxCurve(0f);
        vel.y       = new ParticleSystem.MinMaxCurve(0.3f * scale, 1.2f * scale);
        vel.z       = new ParticleSystem.MinMaxCurve(0f);

        // ─ Elhalványulás ─
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(sparkColor, 0f),
                                     new GradientColorKey(sparkColor, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0.0f),
                                     new GradientAlphaKey(1f, 0.6f),
                                     new GradientAlphaKey(0f, 1.0f) }
        );
        col.color = new ParticleSystem.MinMaxGradient(grad);

        // ─ Emisszió ─
        var emission = ps.emission;
        emission.enabled      = true;
        emission.rateOverTime = isLooping ? sparkBurstCount * 0.4f : 0f;
        if (!isLooping)
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f,
                                           (short)sparkBurstCount,
                                           (short)(sparkBurstCount + 8)) });

        ps.Play();
        return ps;
    }

    // ── Material ───────────────────────────────────────────────

    /// <summary>
    /// Additive = részecskék egymásra vetülve fényesebbek lesznek (tűz-szerű ragyogás).
    /// </summary>
    Material BuildMaterial(Color baseColor, bool additive)
    {
        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.color        = baseColor;
        mat.mainTexture  = CreateSoftCircleTexture();   // puha kör → nem négyzet!

        if (additive)
        {
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.renderQueue = 3000;
        }

        return mat;
    }

    // ── Puha kör textúra (runtime generált) ───────────────────

    /// <summary>
    /// 64×64-es, közepén fehér, szélén átlátszó kör textúra.
    /// Így a részecskék lágy, kerekded foltok – nem négyzetek.
    /// </summary>
    static Texture2D CreateSoftCircleTexture()
    {
        const int size   = 64;
        const float half = size / 2f;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist  = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f),
                                               new Vector2(half, half));
                float t     = Mathf.Clamp01(dist / half);
                // Smoothstep: közepén teli fehér, szélén puha kifutás
                float alpha = 1f - Mathf.SmoothStep(0f, 1f, t);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        return tex;
    }
}
