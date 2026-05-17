using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// CardImage zoom – pinch-to-zoom (mobil) és egér scroll (PC).
/// Simítás ugyanolyan mint a CameraZoom: targetZoom + Lerp.
/// Minimum zoom = eredeti méret, maximum = maxZoom.
/// Nagyítva a képet drag-gel lehet mozgatni.
/// </summary>
public class CardImageZoom : MonoBehaviour, IPointerDownHandler, IDragHandler, IScrollHandler
{
    [Header("Zoom határok")]
    public float minZoom = 1f;
    public float maxZoom = 3f;

    [Header("Érzékenység")]
    public float scrollSensitivity = 0.15f;
    public float pinchSensitivity  = 0.01f;

    [Header("Simítás")]
    [Tooltip("Zoom simítás sebessége (mint CameraZoom smoothSpeed)")]
    public float smoothSpeed  = 8f;
    [Tooltip("Visszarántás sebessége ha túl van zoom-olva")]
    public float snapBackSpeed = 12f;

    // ── Belső állapot ─────────────────────────────────────────────────
    private RectTransform _rect;
    private Vector3  _originalScale;
    private Vector2  _originalPosition;

    private float   _targetZoom   = 1f;
    private float   _currentZoom  = 1f;

    private Vector2 _targetPosition;

    // Drag
    private Vector2 _dragStart;
    private Vector2 _positionOnDragStart;

    // Pinch
    private float _pinchStartDist;
    private float _zoomOnPinchStart;

    // ── Életciklus ────────────────────────────────────────────────────

    void Awake()
    {
        _rect             = GetComponent<RectTransform>();
        _originalScale    = _rect.localScale;
        _originalPosition = _rect.anchoredPosition;
        _targetPosition   = _originalPosition;
    }

    void Update()
    {
        HandlePinch();
        ApplySmoothed();
        SnapBackIfOverzoomed();
    }

    // ── Egér scroll ───────────────────────────────────────────────────

    public void OnScroll(PointerEventData eventData)
    {
        _targetZoom += eventData.scrollDelta.y * scrollSensitivity;
        _targetZoom  = Mathf.Clamp(_targetZoom, minZoom, maxZoom);
    }

    // ── Pinch (mobil) ─────────────────────────────────────────────────

    void HandlePinch()
    {
        if (Input.touchCount != 2) return;

        Touch t0 = Input.GetTouch(0);
        Touch t1 = Input.GetTouch(1);

        // Delta alapú számítás – nem abszolút távolság, hanem változás
        float prevDist = Vector2.Distance(
            t0.position - t0.deltaPosition,
            t1.position - t1.deltaPosition);
        float currDist = Vector2.Distance(t0.position, t1.position);

        _targetZoom += (currDist - prevDist) * pinchSensitivity;
        _targetZoom  = Mathf.Clamp(_targetZoom, minZoom, maxZoom);
    }

    // ── Drag ──────────────────────────────────────────────────────────

    public void OnPointerDown(PointerEventData eventData)
    {
        _dragStart           = eventData.position;
        _positionOnDragStart = _rect.anchoredPosition;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_currentZoom <= minZoom + 0.05f) return;

        Vector2 delta    = eventData.position - _dragStart;
        _targetPosition  = _positionOnDragStart + delta;
    }

    // ── Simítás alkalmazása (mint CameraZoom.ApplyZoomAndPosition) ────

    void ApplySmoothed()
    {
        float dt = Time.deltaTime * smoothSpeed;

        // Zoom
        float newZoom = Mathf.Lerp(_currentZoom, _targetZoom, dt);
        if (Mathf.Abs(newZoom - _targetZoom) < 0.001f) newZoom = _targetZoom;
        _currentZoom = newZoom;
        _rect.localScale = _originalScale * _currentZoom;

        // Pozíció
        Vector2 newPos = Vector2.Lerp(_rect.anchoredPosition, _targetPosition, dt);
        if (Vector2.Distance(newPos, _targetPosition) < 0.5f) newPos = _targetPosition;
        _rect.anchoredPosition = newPos;
    }

    // ── Visszarántás ha kisebb mint minZoom (mint CameraZoom.SnapBack) ─

    void SnapBackIfOverzoomed()
    {
        if (_currentZoom < minZoom)
        {
            _currentZoom = Mathf.Lerp(_currentZoom, minZoom, Time.deltaTime * snapBackSpeed);
            _targetZoom  = _currentZoom;
            _rect.localScale = _originalScale * _currentZoom;
        }

        // Ha visszaállt az eredeti méretre → pozíció is visszaáll
        if (_currentZoom <= minZoom + 0.05f)
        {
            _targetPosition        = _originalPosition;
            _rect.anchoredPosition = Vector2.Lerp(_rect.anchoredPosition, _originalPosition,
                                                   Time.deltaTime * snapBackSpeed);
        }
    }

    // ── Reset (lapozáskor) ────────────────────────────────────────────

    public void ResetZoom()
    {
        _currentZoom           = minZoom;
        _targetZoom            = minZoom;
        _targetPosition        = _originalPosition;
        _rect.localScale       = _originalScale;
        _rect.anchoredPosition = _originalPosition;
    }
}
