using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Lerakott toronyra érintéskor megmutatja a hatótávolság kört.
/// SpyTowerBuilding esetén 3 másodperces nyomásra nyit panelt.
/// </summary>
public class TowerSelector : MonoBehaviour
{
    private Tower selectedTower;

    void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.IsGameOver) return;
        if (TowerShopUI.Instance != null && TowerShopUI.Instance.IsDragging) return;
        if (PvPSendPanel.Instance != null && PvPSendPanel.Instance.IsOpen) return;

        bool tapped = false;

#if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL-en a legacy Input API megbízhatóbb, mint a Mouse.current
        if (Input.GetMouseButtonDown(0))
            tapped = true;
#else
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            tapped = true;

        if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
            if (Touchscreen.current.touches[0].phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began)
                tapped = true;
#endif

        if (!tapped) return;

        Vector3 worldPos = GetWorldPosition();
        Tower hit = GetTowerAt(worldPos);
        SelectTower(hit);

        if (hit == null)
            TryClickRubble(worldPos);
    }

    void SelectTower(Tower tower)
    {
        if (selectedTower != null)
            selectedTower.ShowRange(false);

        if (tower is SpyTowerBuilding spy)
        {
            selectedTower = null;
            spy.OpenSpyPanel();
            return;
        }

        selectedTower = tower;

        if (selectedTower != null)
            selectedTower.ShowRange(true);
    }

    void TryClickRubble(Vector3 worldPos)
    {
        if (GridManager.Instance == null) return;
        Vector2Int cell = GridManager.Instance.WorldToGrid(worldPos);
        if (GridManager.Instance.GetCell(cell) != GridManager.CellType.Rubble) return;

        int rounds = GridManager.Instance.GetRubbleRoundsLeft(cell);
        if (rounds <= 0) return;

        // Ha a rubble prefabon van RubbleClickable, azt használjuk
        var rubble = GetRubbleClickableAt(worldPos);
        if (rubble != null)
        {
            rubble.ShowInfo();
            return;
        }

        // Fallback: nincs RubbleClickable komponens a prefabon
        string msg = rounds == 1 ? "Még 1 kör" : $"Még {rounds} kör";
        Debug.Log($"[Rubble] {cell}: {msg}");
    }

    RubbleClickable GetRubbleClickableAt(Vector3 worldPos)
    {
        float radius = GridManager.Instance.tileWidth * 0.5f;
        var cols = Physics2D.OverlapCircleAll(worldPos, radius);
        foreach (var col in cols)
        {
            var rc = col.GetComponent<RubbleClickable>();
            if (rc != null) return rc;
        }
        return null;
    }

    Tower GetTowerAt(Vector3 worldPos)
    {
        if (GridManager.Instance == null) return null;

        float radius = GridManager.Instance.tileWidth * 0.5f;

        foreach (var tower in Tower.AllTowers)
        {
            if (Vector3.Distance(worldPos, tower.transform.position) <= radius)
                return tower;
        }
        return null;
    }

    Vector3 GetWorldPosition()
    {
        if (Camera.main == null) return Vector3.zero;

        Vector2 screenPos = Vector2.zero;

#if UNITY_WEBGL && !UNITY_EDITOR
        screenPos = Input.mousePosition;
#else
        if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
            screenPos = Touchscreen.current.touches[0].position.ReadValue();
        else if (Mouse.current != null)
            screenPos = Mouse.current.position.ReadValue();
#endif

        float camDist = Mathf.Abs(Camera.main.transform.position.z);
        Vector3 world = Camera.main.ScreenToWorldPoint(
            new Vector3(screenPos.x, screenPos.y, camDist));
        world.z = 0f;
        return world;
    }
}
