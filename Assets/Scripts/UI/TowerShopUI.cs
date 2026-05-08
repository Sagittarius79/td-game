using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class TowerShopUI : MonoBehaviour
{
    public static TowerShopUI Instance { get; private set; }

    [Header("Helyezési előnézet (opcionális)")]
    public GameObject placementGhostPrefab;
    public Color validColor   = new Color(0f, 1f, 0f, 0.5f);
    public Color invalidColor = new Color(1f, 0f, 0f, 0.5f);

    private bool isDragging = false;
    public bool IsDragging => isDragging;
    private TowerDefinition selectedTower;
    private GameObject ghostObject;
    private SpriteRenderer ghostRenderer;
    private Vector2Int hoveredCell;
    private bool isValidPlacement;
    private int _dragStartFrame = -1;  // frame delay: az indító kattintás ne zárja le azonnal

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        if (GameManager.Instance == null)
            Debug.LogError("TowerShopUI: GameManager.Instance NULL!");
        if (GridManager.Instance == null)
            Debug.LogError("TowerShopUI: GridManager.Instance NULL!");
    }

    void Update()
    {
        if (!isDragging) return;
        HandleDragInput();
    }

    public void StartDragging(TowerDefinition def)
    {
        if (def == null)
        {
            Debug.LogError("StartDragging: def null!");
            return;
        }
        if (GameManager.Instance == null)
        {
            Debug.LogError("StartDragging: GameManager.Instance null!");
            return;
        }
        if (GameManager.Instance.IsGameOver) return;
        if (!GameManager.Instance.CanAfford(def.goldCost))
        {
            if (UIManager.Instance != null)
                UIManager.Instance.ShowNotEnoughGold();
            return;
        }

        selectedTower   = def;
        isDragging      = true;
        _dragStartFrame = Time.frameCount;

        // Épités menü bezárása – hogy a pálya látható legyen
        if (BuildMenuUI.Instance != null)
            BuildMenuUI.Instance.CloseMenu();

        ghostObject   = new GameObject("PlacementGhost");
        ghostRenderer = ghostObject.AddComponent<SpriteRenderer>();
        ghostRenderer.sortingOrder = 100;

        if (def.sprite != null)
        {
            ghostRenderer.sprite = def.sprite;
            ghostRenderer.color  = validColor;
        }
        else
        {
            // Fallback: fehér négyzet, ha nincs sprite bekötve
            ghostRenderer.sprite = CreateFallbackSprite();
            ghostRenderer.color  = new Color(1f, 1f, 0f, 0.7f);  // sárga, jól látható
        }
    }

    void HandleDragInput()
    {
        if (GridManager.Instance == null) return;

        Vector3 inputWorldPos = GetInputWorldPosition();
        hoveredCell      = GridManager.Instance.WorldToGrid(inputWorldPos);
        isValidPlacement = GridManager.Instance.CanBuild(hoveredCell);

        // Lvl1-es épület dekorált cellára nem rakható
        if (isValidPlacement && selectedTower?.prefab != null)
        {
            var towerComp = selectedTower.prefab.GetComponent<Tower>();
            var tagComp   = selectedTower.prefab.GetComponent<BuildingLevelTag>();
            bool prefabIsLvl2 = (towerComp != null && towerComp.isLvl2) ||
                                 (tagComp   != null && tagComp.isLvl2);
            if (!prefabIsLvl2)
            {
                var decorator = FindFirstObjectByType<MapDecorator>();
                if (decorator != null && decorator.HasDecoration(hoveredCell))
                    isValidPlacement = false;
            }
        }

        Vector3 snapPos  = GridManager.Instance.GridToWorld(hoveredCell);

        if (ghostObject != null)
        {
            ghostObject.transform.position = snapPos;
            if (ghostRenderer != null)
                ghostRenderer.color = isValidPlacement ? validColor : invalidColor;
        }

        if (IsInputReleased())
        {
            if (isValidPlacement)
                PlaceTower(hoveredCell);
            else
                CancelDragging();
        }
    }

    void PlaceTower(Vector2Int cell)
    {
        if (GameManager.Instance == null || selectedTower == null) return;

        if (!GameManager.Instance.SpendGold(selectedTower.goldCost))
        {
            if (UIManager.Instance != null)
                UIManager.Instance.ShowNotEnoughGold();
            CancelDragging();
            return;
        }

        if (selectedTower.prefab == null)
        {
            Debug.LogError($"TowerShopUI: '{selectedTower.towerName}' prefab nincs bekötve!");
            CancelDragging();
            return;
        }

        Vector3 worldPos = GridManager.Instance.GridToWorld(cell);
        var towerGO = Instantiate(selectedTower.prefab, worldPos, Quaternion.identity);

        // Dinamikus árú épületnél automatikusan hozzáadjuk a számlálót
        if (selectedTower.dynamicPrice &&
            towerGO.GetComponent<DynamicPriceBuilding>() == null)
            towerGO.AddComponent<DynamicPriceBuilding>();

        var tower = towerGO.GetComponent<Tower>();
        if (tower != null)
        {
            tower.PlaceAt(cell);
            NetworkGameManager.Instance?.BroadcastTowerPlaced(selectedTower.towerName);
        }

        var decorator = FindFirstObjectByType<MapDecorator>();
        if (decorator != null)
            decorator.RemoveDecorationAt(cell);

        CleanupDrag();
    }

    void CancelDragging() => CleanupDrag();

    static Sprite CreateFallbackSprite()
    {
        var tex = new Texture2D(32, 32);
        var pixels = new Color[32 * 32];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
    }

    void CleanupDrag()
    {
        isDragging    = false;
        selectedTower = null;
        if (ghostObject != null)
            Destroy(ghostObject);
        ghostObject = null;
    }

    // ── Input ─────────────────────────────────────────────────────

    Vector3 GetInputWorldPosition()
    {
        if (Camera.main == null) return Vector3.zero;

        Vector2 screenPos = GetScreenPosition();

        // Fontos: a kamera Z távolságát kell megadni
        // Orthographic kameránál ez a kamera Z pozíciójának abszolút értéke
        float camDist = Mathf.Abs(Camera.main.transform.position.z);

        Vector3 world = Camera.main.ScreenToWorldPoint(
            new Vector3(screenPos.x, screenPos.y, camDist));

        world.z = 0f;
        return world;
    }

    Vector2 GetScreenPosition()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL-en a legacy Input.mousePosition megbízhatóbb:
        // garantáltan Unity screen-koordinátákban adja vissza a pozíciót
        return Input.mousePosition;
#endif
        // Érintés (mobil)
        var touchscreen = Touchscreen.current;
        if (touchscreen != null && touchscreen.touches.Count > 0)
            return touchscreen.touches[0].position.ReadValue();

        // Egér (PC / Editor)
        var mouse = Mouse.current;
        if (mouse != null)
            return mouse.position.ReadValue();

        return Vector2.zero;
    }

    bool IsInputReleased()
    {
        // Az indító kattintás ne zárja le azonnal a draget (frame delay)
        if (Time.frameCount <= _dragStartFrame + 1) return false;

#if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL-en GetMouseButtonDown megbízhatóbb mint wasReleasedThisFrame:
        // a következő bal klikk lenyomásakor rakja le a tornyot
        return Input.GetMouseButtonDown(0);
#endif

        // Érintés elengedés (mobil)
        var touchscreen = Touchscreen.current;
        if (touchscreen != null && touchscreen.touches.Count > 0)
            return touchscreen.touches[0].phase.ReadValue() ==
                   UnityEngine.InputSystem.TouchPhase.Ended;

        // Egér elengedés (Editor / PC)
        var mouse = Mouse.current;
        if (mouse != null)
            return mouse.leftButton.wasReleasedThisFrame;

        return false;
    }
}

[System.Serializable]
public class TowerDefinition
{
    public string     towerName;
    public int        goldCost;
    public Sprite     sprite;
    public GameObject prefab;
    [TextArea(1, 3)]
    public string description;
    public bool       dynamicPrice;
}
