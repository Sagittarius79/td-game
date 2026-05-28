using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Nagyítás + mozgatás a skill fa node területén.
/// Rakd a NodeContainer-re (RectTransform + Image Raycast Target=true szükséges).
/// </summary>
public class SkillTreeZoomPan : MonoBehaviour, IScrollHandler
{
    [Header("Nagyítás")]
    public float minZoom          = 1.0f;
    public float maxZoom          = 3.0f;
    public float scrollSpeed      = 0.15f;
    public float pinchSensitivity = 0.005f;

    [Header("Simítás")]
    public float smoothSpeed = 10f;

    private RectTransform _rect;
    private RectTransform _parentRect;

    // Célállapot – mindig ebbe lerp-elünk (mint CameraZoom targetZoom/targetPosition)
    private float   _targetScale;
    private Vector2 _targetPosition;

    // Egy ujj – mozgatás
    private int     _dragFingerId = -1;
    private Vector2 _lastDragPos;

    // Két ujj – pinch zoom
    private bool _wasPinching;

    void Awake()
    {
        _rect           = GetComponent<RectTransform>();
        _parentRect     = _rect.parent as RectTransform;
        _targetScale    = _rect.localScale.x;
        _targetPosition = _rect.anchoredPosition;
    }

    void Update()
    {
        int touchCount = Input.touchCount;

        if (touchCount == 0)
        {
            _dragFingerId = -1;
            _wasPinching  = false;
        }
        else if (touchCount >= 2)
        {
            _dragFingerId = -1;
            HandlePinch();
        }
        else if (touchCount == 1 && !_wasPinching)
        {
            Touch t = Input.GetTouch(0);

            if (t.phase == TouchPhase.Began)
            {
                _dragFingerId = t.fingerId;
                _lastDragPos  = t.position;
            }
            else if (t.phase == TouchPhase.Moved && t.fingerId == _dragFingerId)
            {
                Vector2 delta    = t.position - _lastDragPos;
                _lastDragPos     = t.position;
                _targetPosition += delta;
                ClampTargetPosition();
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
                _lastDragPos = t.position;
        }

        // Lerp alkalmazása – mint CameraZoom.ApplyZoomAndPosition
        float dt = Time.deltaTime * smoothSpeed;

        float newScale = Mathf.Lerp(_rect.localScale.x, _targetScale, dt);
        if (Mathf.Abs(newScale - _targetScale) < 0.001f) newScale = _targetScale;
        _rect.localScale = Vector3.one * newScale;

        Vector2 newPos = Vector2.Lerp(_rect.anchoredPosition, _targetPosition, dt);
        if (Vector2.Distance(newPos, _targetPosition) < 0.5f) newPos = _targetPosition;
        _rect.anchoredPosition = newPos;
    }

    // ── Pinch ─────────────────────────────────────────────────────────────

    void HandlePinch()
    {
        Touch t0 = Input.GetTouch(0);
        Touch t1 = Input.GetTouch(1);

        // Delta alapú (mint CameraZoom) – nem ratio, nem kapkod
        float prevDist = Vector2.Distance(
            t0.position - t0.deltaPosition,
            t1.position - t1.deltaPosition);
        float currDist = Vector2.Distance(t0.position, t1.position);

        if (!_wasPinching)
        {
            _wasPinching = true;
            return;
        }

        float oldTarget = _targetScale;
        _targetScale = Mathf.Clamp(_targetScale + (currDist - prevDist) * pinchSensitivity,
                                   minZoom, maxZoom);

        if (_targetScale <= minZoom)
        {
            _targetPosition = Vector2.zero;
            return;
        }

        // Pivot: a két ujj közepe felé tolja a tartalmat
        Vector2 center = (t0.position + t1.position) * 0.5f;
        Vector2 localCenter;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rect, center, null, out localCenter);

        float ratio      = _targetScale / oldTarget;
        _targetPosition += localCenter * (ratio - 1f);
        ClampTargetPosition();
    }

    // ── Egérgörgő ─────────────────────────────────────────────────────────

    public void OnScroll(PointerEventData e)
    {
        if (Mathf.Abs(e.scrollDelta.y) < 0.001f) return;

        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rect, e.position, null, out localPos);

        ApplyScrollToTarget(1f + e.scrollDelta.y * scrollSpeed, localPos);
    }

    void LateUpdate()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.001f)
        {
            Vector2 localPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rect, Input.mousePosition, null, out localPos);
            ApplyScrollToTarget(1f + scroll * scrollSpeed * 10f, localPos);
        }
    }

    void ApplyScrollToTarget(float factor, Vector2 pivotLocal)
    {
        float oldTarget = _targetScale;
        _targetScale = Mathf.Clamp(_targetScale * factor, minZoom, maxZoom);

        if (_targetScale <= minZoom)
        {
            _targetPosition = Vector2.zero;
            return;
        }

        float ratio      = _targetScale / oldTarget;
        _targetPosition += pivotLocal * (ratio - 1f);
        ClampTargetPosition();
    }

    // ── Segéd ─────────────────────────────────────────────────────────────

    void ClampTargetPosition()
    {
        if (_parentRect == null) return;

        float maxMoveX = Mathf.Max(0f, (_rect.rect.width  * _targetScale - _parentRect.rect.width)  * 0.5f);
        float maxMoveY = Mathf.Max(0f, (_rect.rect.height * _targetScale - _parentRect.rect.height) * 0.5f);

        _targetPosition.x = Mathf.Clamp(_targetPosition.x, -maxMoveX, maxMoveX);
        _targetPosition.y = Mathf.Clamp(_targetPosition.y, -maxMoveY, maxMoveY);
    }

    public void ResetView()
    {
        _targetScale           = minZoom;
        _targetPosition        = Vector2.zero;
        _rect.localScale       = Vector3.one * minZoom;
        _rect.anchoredPosition = Vector2.zero;
    }
}
