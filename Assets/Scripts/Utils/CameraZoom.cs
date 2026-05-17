using UnityEngine;

/// <summary>
/// ═══════════════════════════════════════════════════════
///  KAMERA ZOOM ÉS MOZGATÁS
///
///  Zoom:
///   - Egér görgő (PC) / kétujjas pinch (mobil)
///   - Ha túl van zoom-olva, finoman visszarántja maxZoom-ra
///
///  Mozgatás:
///   - 1 ujj húzás (ha nem épül torony)
///   - Jobb egérgomb húzás (PC)
///   - 2 ujjas húzás (mobil, zoom közben is)
///
///  Unity beállítás:
///   - Main Camera objektumra kell rakni
///   - Orthographic módban kell lennie a kamerának
///   - A szerkesztőben beállított Orthographic Size és pozíció
///     lesz az indulási állapot és a max zoom határ
/// ═══════════════════════════════════════════════════════
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraZoom : MonoBehaviour
{
    // ══════════════════════════════════════════════════════
    //  INSPECTOR MEZŐK
    // ══════════════════════════════════════════════════════

    [Header("Zoom határok")]
    [Tooltip("Maximális beközelítés (kisebb szám = közelebb). Pl. 3")]
    public float minZoom = 3f;

    [Header("Érzékenység")]
    public float pinchSensitivity  = 0.05f;
    public float scrollSensitivity = 1f;

    [Header("Simítás")]
    [Tooltip("Zoom és mozgás simítás sebessége")]
    public float smoothSpeed = 8f;
    [Tooltip("Visszarántás ereje ha túl van zoom-olva (rugó hatás)")]
    public float snapBackSpeed = 12f;

    [Header("Mozgatás")]
    [Tooltip("Ennyi pixel húzás után indul el a kamera mozgatás (tap védelme)")]
    public float panThreshold = 15f;

    // ══════════════════════════════════════════════════════
    //  BELSŐ ÁLLAPOT
    // ══════════════════════════════════════════════════════

    private Camera  cam;
    private float   targetZoom;
    private float   maxZoom;
    private Vector3 targetPosition;
    private Vector3 initialPosition;  // szerkesztőben beállított kezdő pozíció
    private bool    initialized;

    // Jobb egérgomb mozgatás (PC)
    private Vector3 rightDragOrigin;
    private bool    isRightDragging;

    // Egy ujjas mozgatás (mobil + bal egér)
    private Vector2 singleTouchStart;
    private bool    isSinglePanning;

    // ══════════════════════════════════════════════════════
    //  INICIALIZÁLÁS
    // ══════════════════════════════════════════════════════

    void Awake()
    {
        cam = GetComponent<Camera>();

        // Azonnal beolvassuk a szerkesztőben beállított értékeket
        // → Update() már helyes értékekkel indul, nincs ugrás
        maxZoom         = cam.orthographicSize;
        targetZoom      = cam.orthographicSize;
        initialPosition = transform.position;
        targetPosition  = transform.position;
    }

    void Start()
    {
        // Ha van kiválasztott pálya, a kamerát az ahhoz tartozó startpozícióra helyezzük.
        // Start() az összes Awake() után fut, tehát GridManager.SelectedMap már be van állítva.
        if (GridManager.Instance != null && GridManager.Instance.SelectedMap != null)
        {
            var map = GridManager.Instance.SelectedMap;

            // Pozíció
            Vector3 mapCamPos = map.cameraStartPosition;
            mapCamPos.z = transform.position.z;
            transform.position = mapCamPos;
            initialPosition    = mapCamPos;
            targetPosition     = mapCamPos;

            // Orthographic size (zoom szint) – csak ha be van állítva
            if (map.cameraOrthographicSize > 0f)
            {
                cam.orthographicSize = map.cameraOrthographicSize;
                maxZoom              = map.cameraOrthographicSize;
                targetZoom           = map.cameraOrthographicSize;
            }

            Debug.Log($"CameraZoom: '{map.name}' → pos:{mapCamPos}, orthoSize:{cam.orthographicSize}");
        }

        initialized = true;
    }

    // ══════════════════════════════════════════════════════
    //  UPDATE
    // ══════════════════════════════════════════════════════

    void Update()
    {
        if (!initialized) return;

        HandleZoom();
        HandlePan();
        ApplyZoomAndPosition();
        SnapBackIfOverzoomed();
    }

    // ══════════════════════════════════════════════════════
    //  ZOOM
    // ══════════════════════════════════════════════════════

    void HandleZoom()
    {
        // ── Mobil: kétujjas pinch ──────────────────────────────────
        if (Input.touchCount == 2)
        {
            Touch t0 = Input.GetTouch(0);
            Touch t1 = Input.GetTouch(1);

            float prevDist = Vector2.Distance(
                t0.position - t0.deltaPosition,
                t1.position - t1.deltaPosition);
            float currDist = Vector2.Distance(t0.position, t1.position);

            targetZoom += (prevDist - currDist) * pinchSensitivity;
        }

        // ── PC: egér görgő ────────────────────────────────────────
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.001f)
            targetZoom -= scroll * scrollSensitivity * 10f;

        // Szigorú clamp – túl messzire nem mehet
        targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
    }

    // ══════════════════════════════════════════════════════
    //  MOZGATÁS
    // ══════════════════════════════════════════════════════

    void HandlePan()
    {
        bool isPlacingTower = TowerShopUI.Instance != null && TowerShopUI.Instance.IsDragging;

        // ── Mobil: 2 ujjas húzás ───────────────────────────────────
        if (Input.touchCount == 2)
        {
            Touch t0 = Input.GetTouch(0);
            Touch t1 = Input.GetTouch(1);

            Vector2 prevMid = ((t0.position - t0.deltaPosition) +
                               (t1.position - t1.deltaPosition)) * 0.5f;
            Vector2 currMid = (t0.position + t1.position) * 0.5f;

            Vector3 prev = cam.ScreenToWorldPoint(new Vector3(prevMid.x, prevMid.y, 0f));
            Vector3 curr = cam.ScreenToWorldPoint(new Vector3(currMid.x, currMid.y, 0f));
            targetPosition += prev - curr;

            // 2 ujj közben ne indítson 1 ujjas pant
            isSinglePanning = false;
        }
        // ── Mobil: 1 ujjas húzás (csak ha nem épül torony) ────────
        else if (Input.touchCount == 1 && !isPlacingTower)
        {
            Touch t = Input.GetTouch(0);

            if (t.phase == TouchPhase.Began)
            {
                singleTouchStart = t.position;
                isSinglePanning  = false;
            }
            else if (t.phase == TouchPhase.Moved)
            {
                // Threshold után indul el a pan (tap-ot ne rontsa el)
                if (!isSinglePanning &&
                    Vector2.Distance(t.position, singleTouchStart) > panThreshold)
                    isSinglePanning = true;

                if (isSinglePanning)
                {
                    Vector3 prev = cam.ScreenToWorldPoint(
                        new Vector3(t.position.x - t.deltaPosition.x,
                                    t.position.y - t.deltaPosition.y, 0f));
                    Vector3 curr = cam.ScreenToWorldPoint(
                        new Vector3(t.position.x, t.position.y, 0f));
                    targetPosition += prev - curr;
                }
            }
            else if (t.phase == TouchPhase.Ended)
            {
                isSinglePanning = false;
            }
        }

        // ── PC: jobb egérgomb húzás ───────────────────────────────
        if (Input.GetMouseButtonDown(1))
        {
            rightDragOrigin = cam.ScreenToWorldPoint(Input.mousePosition);
            isRightDragging = true;
        }
        if (Input.GetMouseButton(1) && isRightDragging)
        {
            Vector3 curr    = cam.ScreenToWorldPoint(Input.mousePosition);
            targetPosition += rightDragOrigin - curr;
            rightDragOrigin = cam.ScreenToWorldPoint(Input.mousePosition);
        }
        if (Input.GetMouseButtonUp(1))
            isRightDragging = false;

        ClampPosition();
    }

    void ClampPosition()
    {
        // Mennyivel van beközelítve a kamera a kezdő nézethez képest?
        // Ha zoom = maxZoom → allowance = 0 → nem lehet mozgatni
        // Ha zoom = minZoom → maximális mozgástér
        float allowX = (maxZoom - targetZoom) * cam.aspect;
        float allowY =  maxZoom - targetZoom;

        targetPosition.x = Mathf.Clamp(targetPosition.x,
            initialPosition.x - allowX,
            initialPosition.x + allowX);
        targetPosition.y = Mathf.Clamp(targetPosition.y,
            initialPosition.y - allowY,
            initialPosition.y + allowY);
    }

    // ══════════════════════════════════════════════════════
    //  ALKALMAZÁS
    // ══════════════════════════════════════════════════════

    void ApplyZoomAndPosition()
    {
        float dt = Time.deltaTime * smoothSpeed;

        // Zoom
        float newZoom = Mathf.Lerp(cam.orthographicSize, targetZoom, dt);
        if (Mathf.Abs(newZoom - targetZoom) < 0.001f) newZoom = targetZoom;
        cam.orthographicSize = newZoom;

        // Pozíció
        Vector3 newPos = Vector3.Lerp(transform.position,
            new Vector3(targetPosition.x, targetPosition.y, transform.position.z), dt);
        if (Vector3.Distance(newPos, targetPosition) < 0.001f)
            newPos = new Vector3(targetPosition.x, targetPosition.y, transform.position.z);
        transform.position = newPos;
    }

    /// <summary>
    /// Ha a kamera zoom nagyobb mint maxZoom (pl. lendület miatt),
    /// finoman visszarántja a megengedett értékre.
    /// </summary>
    void SnapBackIfOverzoomed()
    {
        if (cam.orthographicSize > maxZoom)
        {
            cam.orthographicSize = Mathf.Lerp(
                cam.orthographicSize, maxZoom,
                Time.deltaTime * snapBackSpeed);
            targetZoom = cam.orthographicSize;
        }
    }
}
