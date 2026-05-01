using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Lerakott toronyra érintéskor megmutatja a hatótávolság kört.
/// Máshova érintéskor elrejti.
/// </summary>
public class TowerSelector : MonoBehaviour
{
    private Tower selectedTower;

    void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.IsGameOver) return;

        // Torony lerakás közben ne válasszon
        if (TowerShopUI.Instance != null && TowerShopUI.Instance.IsDragging) return;

        // PvP küldő panel nyitva → ne válasszon tornyot
        if (PvPSendPanel.Instance != null && PvPSendPanel.Instance.IsOpen) return;

        bool tapped = false;

        // Egér
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            tapped = true;

        // Érintés
        if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
            if (Touchscreen.current.touches[0].phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began)
                tapped = true;

        if (!tapped) return;

        Vector3 worldPos = GetWorldPosition();
        Tower hit = GetTowerAt(worldPos);
        SelectTower(hit);
    }

    void SelectTower(Tower tower)
    {
        // Előző torony range körének elrejtése
        if (selectedTower != null)
            selectedTower.ShowRange(false);

        selectedTower = tower;

        // Új torony range körének megjelenítése
        if (selectedTower != null)
            selectedTower.ShowRange(true);
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

        if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
            screenPos = Touchscreen.current.touches[0].position.ReadValue();
        else if (Mouse.current != null)
            screenPos = Mouse.current.position.ReadValue();

        float camDist = Mathf.Abs(Camera.main.transform.position.z);
        Vector3 world = Camera.main.ScreenToWorldPoint(
            new Vector3(screenPos.x, screenPos.y, camDist));
        world.z = 0f;
        return world;
    }
}
