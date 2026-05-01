using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Képernyős debug log – telefonos teszteléshez.
/// Debug.Log üzeneteket jelenít meg a képernyőn.
/// Éles verzióban töröld ki vagy kapcsold ki az objektumot.
/// </summary>
public class ScreenLogger : MonoBehaviour
{
    [Tooltip("Hány sort mutasson egyszerre")]
    public int maxLines = 8;

    [Tooltip("Csak [NGM] prefixű logokat mutasson (szűrés)")]
    public bool onlyNGM = true;

    private TextMeshProUGUI label;
    private Queue<string> lines = new Queue<string>();

    void Awake()
    {
        // Canvas létrehozása
        var canvasGO  = new GameObject("ScreenLoggerCanvas");
        var canvas    = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        DontDestroyOnLoad(canvasGO);

        // Fekete háttér panel
        var panelGO = new GameObject("LogPanel");
        panelGO.transform.SetParent(canvasGO.transform, false);
        var img = panelGO.AddComponent<UnityEngine.UI.Image>();
        img.color = new Color(0, 0, 0, 0.7f);
        var rect = panelGO.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 0.5f);
        rect.anchorMax = new Vector2(1, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        // Szöveg
        var textGO = new GameObject("LogText");
        textGO.transform.SetParent(panelGO.transform, false);
        label = textGO.AddComponent<TextMeshProUGUI>();
        label.fontSize  = 22;
        label.color     = Color.green;
        label.alignment = TextAlignmentOptions.TopLeft;
        var tr = textGO.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(10, 10);
        tr.offsetMax = new Vector2(-10, -10);

        Application.logMessageReceived += OnLog;
    }

    void OnDestroy()
    {
        Application.logMessageReceived -= OnLog;
    }

    void OnLog(string message, string stackTrace, LogType type)
    {
        if (onlyNGM && !message.Contains("[NGM]")) return;

        string prefix = type == LogType.Error || type == LogType.Exception ? "<color=red>" :
                        type == LogType.Warning ? "<color=yellow>" : "<color=green>";
        string suffix = "</color>";

        lines.Enqueue($"{prefix}{message}{suffix}");
        while (lines.Count > maxLines)
            lines.Dequeue();

        label.text = string.Join("\n", lines);
    }
}
