using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Nagyítás + mozgatás a skill fa node területén.
/// Rakd a NodeContainer-re (RectTransform + Image Raycast Target=true szükséges).
/// </summary>
public class SkillTreeZoomPan : MonoBehaviour, IScrollHandler
{
    [Header("Nagyítás")]
    public float minZoom     = 1.0f;
    public float maxZoom     = 3.0f;
    public float scrollSpeed = 0.15f;

    private RectTransform _rect;
    private RectTransform _parentRect;

    // Egy ujj – mozgatás
    private int     _dragFingerId  = -1;
    private Vector2 _lastDragPos;

    // Két ujj – pinch zoom
    private bool  _wasPinching;
    private float _lastPinchDist;

    void Awake()
    {
        _rect       = GetComponent<RectTransform>();
        _parentRect = _rect.parent as RectTransform;
    }

    void Update()
    {
        int touchCount = Input.touchCount;

        if (touchCount == 0)
        {
            _dragFingerId = -1;
            _wasPinching  = false;
            return;
        }

        if (touchCount >= 2)
        {
            // Pinch zoom – drag törlése
            _dragFingerId = -1;
            HandlePinch();
            return;
        }

        // Egy ujj – mozgatás (csak ha nem volt épp pinch)
        if (touchCount == 1 && !_wasPinching)
        {
            Touch t = Input.GetTouch(0);

            if (t.phase == TouchPhase.Began)
            {
                _dragFingerId = t.fingerId;
                _lastDragPos  = t.position;
            }
            else if (t.phase == TouchPhase.Moved && t.fingerId == _dragFingerId)
            {
                Vector2 delta          = t.position - _lastDragPos;
                _lastDragPos           = t.position;
                _rect.anchoredPosition += delta;
                ClampPosition();
            }
            else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
            {
                _dragFingerId = -1;
            }
        }

        // Ha felemeltük az egyik ujjat pinch után, ne ugorjon a kép
        if (touchCount == 1 && _wasPinching)
        {
            Touch t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                _wasPinching = false;
            else
                _lastDragPos = t.position; // szinkronban tartjuk, ne ugorjon
        }
    }

    void HandlePinch()
    {
        Touch t0 = Input.GetTouch(0);
        Touch t1 = Input.GetTouch(1);

        float dist = Vector2.Distance(t0.position, t1.position);

        if (!_wasPinching)
        {
            _lastPinchDist = dist;
            _wasPinching   = true;
            return;
        }

        if (Mathf.Approximately(dist, 0f)) return;

        float factor = dist / _lastPinchDist;
        _lastPinchDist = dist;

        Vector2 center = (t0.position + t1.position) * 0.5f;
        Vector2 localCenter;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rect, center, null, out localCenter);

        ApplyZoom(factor, localCenter);
    }

    // ── Egérgörgő (PC / Editor) ───────────────────────────────────────

    public void OnScroll(PointerEventData e)
    {
        float scroll = e.scrollDelta.y;
        if (Mathf.Abs(scroll) < 0.001f) return;

        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rect, e.position, null, out localPos);

        ApplyZoom(1f + scroll * scrollSpeed, localPos);
    }

    void LateUpdate()
    {
        // Input.GetAxis fallback PC-re
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.001f)
        {
            Vector2 localPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rect, Input.mousePosition, null, out localPos);
            ApplyZoom(1f + scroll * scrollSpeed * 10f, localPos);
        }
    }

    // ── Zoom alkalmazása ──────────────────────────────────────────────

    void ApplyZoom(float factor, Vector2 pivotLocal)
    {
        float oldScale = _rect.localScale.x;
        float newScale = Mathf.Clamp(oldScale * factor, minZoom, maxZoom);

        if (newScale <= minZoom)
        {
            _rect.localScale       = Vector3.one * minZoom;
            _rect.anchoredPosition = Vector2.zero;
            return;
        }

        if (Mathf.Approximately(newScale, oldScale)) return;

        float ratio            = newScale / oldScale;
        _rect.localScale       = Vector3.one * newScale;
        _rect.anchoredPosition += pivotLocal * (ratio - 1f);

        ClampPosition();
    }

    void ClampPosition()
    {
        if (_parentRect == null) return;

        float scale    = _rect.localScale.x;
        float maxMoveX = (_rect.rect.width  * scale - _parentRect.rect.width)  * 0.5f;
        float maxMoveY = (_rect.rect.height * scale - _parentRect.rect.height) * 0.5f;

        maxMoveX = Mathf.Max(0f, maxMoveX);
        maxMoveY = Mathf.Max(0f, maxMoveY);

        Vector2 pos = _rect.anchoredPosition;
        pos.x = Mathf.Clamp(pos.x, -maxMoveX, maxMoveX);
        pos.y = Mathf.Clamp(pos.y, -maxMoveY, maxMoveY);
        _rect.anchoredPosition = pos;
    }

    // ── Reset ─────────────────────────────────────────────────────────

    public void ResetView()
    {
        _rect.localScale       = Vector3.one;
        _rect.anchoredPosition = Vector2.zero;
    }
}
