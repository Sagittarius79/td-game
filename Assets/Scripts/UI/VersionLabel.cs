using UnityEngine;
using TMPro;

/// <summary>
/// Globális verziószám felirat – minden scene-ben látszik.
/// Egyszer helyezd el a MainMenu scene-ben egy üres GameObject-en.
/// </summary>
public class VersionLabel : MonoBehaviour
{
    [Tooltip("Sarok: BottomLeft / BottomRight / TopLeft / TopRight")]
    public Corner corner = Corner.BottomRight;
    [Tooltip("Betűméret")]
    public float fontSize = 18f;
    [Tooltip("Szöveg szín")]
    public Color color = new Color(1f, 1f, 1f, 0.5f);
    [Tooltip("Margó a saroktól (pixel)")]
    public Vector2 margin = new Vector2(10f, 6f);

    public enum Corner { BottomLeft, BottomRight, TopLeft, TopRight }

    static bool _created = false;

    void Awake()
    {
        if (_created) { Destroy(gameObject); return; }
        _created = true;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        BuildLabel();
    }

    void BuildLabel()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();

        var textGO = new GameObject("VersionText");
        textGO.transform.SetParent(transform, false);

        var tmp       = textGO.AddComponent<TextMeshProUGUI>();
        string ver    = Application.version;
        tmp.text      = string.IsNullOrEmpty(ver) ? "v?" : $"v{ver}";
        tmp.fontSize  = fontSize;
        tmp.color     = color;
        tmp.raycastTarget = false;

        var rt = textGO.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(300f, 50f);

        switch (corner)
        {
            case Corner.BottomLeft:
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
                rt.anchoredPosition = margin;
                tmp.alignment = TextAlignmentOptions.BottomLeft;
                break;
            case Corner.BottomRight:
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);
                rt.anchoredPosition = new Vector2(-margin.x, margin.y);
                tmp.alignment = TextAlignmentOptions.BottomRight;
                break;
            case Corner.TopLeft:
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(margin.x, -margin.y);
                tmp.alignment = TextAlignmentOptions.TopLeft;
                break;
            case Corner.TopRight:
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
                rt.anchoredPosition = new Vector2(-margin.x, -margin.y);
                tmp.alignment = TextAlignmentOptions.TopRight;
                break;
        }
    }
}
