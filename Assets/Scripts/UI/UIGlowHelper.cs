using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime-ban generál egy lágy radial gradient sprite-ot a glow Image-ekhez.
/// Külső asset nem szükséges – egyszer készül el és cache-elve van.
/// </summary>
public static class UIGlowHelper
{
    private static Sprite _cachedSprite;

    /// <summary>
    /// Visszaad egy fehér, lágy szélű körös sprite-ot.
    /// A tényleges színt az Image.color-on állíts be.
    /// </summary>
    public static Sprite GetGlowSprite()
    {
        if (_cachedSprite != null) return _cachedSprite;

        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp
        };

        float center = size * 0.5f;
        var pixels   = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(
                    new Vector2(x + 0.5f, y + 0.5f),
                    new Vector2(center, center));

                float t     = Mathf.Clamp01(1f - dist / center);
                byte  alpha = (byte)(t * t * t * 255f); // cubic falloff – lágy él
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, true); // nem kell mipmap, feltöltjük a GPU-ra

        _cachedSprite = Sprite.Create(
            tex,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f));

        return _cachedSprite;
    }

    /// <summary>
    /// Beállítja a soft glow sprite-ot az Image-re.
    /// Center anchorra állítja és közvetlen sizeDelta-val adja meg a méretet,
    /// így sem Layout Group, sem stretch anchor nem tudja felülírni.
    /// </summary>
    public static void ApplyTo(Image image, float size = 96f)
    {
        if (image == null) return;

        if (image.sprite == null)
            image.sprite = GetGlowSprite();

        image.raycastTarget = false;

        // Layout Group-tól függetlenítés
        var le = image.GetComponent<LayoutElement>();
        if (le == null) le = image.gameObject.AddComponent<LayoutElement>();
        le.ignoreLayout = true;

        // Anchor és méret kódból – így se stretch, se layout nem bírja felülírni
        var rt = image.rectTransform;
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = new Vector2(size, size);
    }
}
