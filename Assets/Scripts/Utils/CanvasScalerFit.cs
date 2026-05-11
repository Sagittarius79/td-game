using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Canvas Scaler automatikus Match beállítás:
/// mindig a szűkebb irány alapján skálázódik, így sem alul, sem oldalt
/// nem vág le semmit — letterbox / pillarbox üres sávok jelenhetnek meg.
/// Rakd arra a GameObject-re, amelyiken a Canvas Scaler komponens van.
/// </summary>
[RequireComponent(typeof(CanvasScaler))]
public class CanvasScalerFit : MonoBehaviour
{
    CanvasScaler _scaler;

    void Awake()
    {
        _scaler = GetComponent<CanvasScaler>();
    }

    void Update()
    {
        float refAspect = _scaler.referenceResolution.x / _scaler.referenceResolution.y;
        float screenAspect = Screen.width / (float)Screen.height;

        // Ha a képernyő szélesebb mint a referencia → magasság alapján skálázz (Match=1)
        // Ha a képernyő keskenyebb mint a referencia → szélesség alapján skálázz (Match=0)
        _scaler.matchWidthOrHeight = screenAspect >= refAspect ? 1f : 0f;
    }
}
