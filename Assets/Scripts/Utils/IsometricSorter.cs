using UnityEngine;

/// <summary>
/// Izometrikus mélységi rendezés.
/// Ráhelyezve bármely GameObject-re, automatikusan frissíti a SpriteRenderer
/// sortingOrder értékét az izometrikus pozíció alapján.
/// Statikus objektumokon (fák, díszítés) elég egyszer futtatni (runOnce = true).
/// </summary>
public class IsometricSorter : MonoBehaviour
{
    [Header("Beállítások")]
    public SpriteRenderer spriteRenderer;

    [Tooltip("Igaz: csak Start()-ban fut (statikus objektumok). Hamis: minden frame-ben frissül.")]
    public bool runOnce = false;

    [Tooltip("Finomhangolás az alap sorting értékhez.")]
    public int sortingOffset = 0;

    [Tooltip("Ha igaz, a GridManager-ből számítja. Ha hamis, world Y-t használja.")]
    public bool useGridCoordinates = true;

    void Start()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        UpdateSorting();
    }

    void LateUpdate()
    {
        if (!runOnce) UpdateSorting();
    }

    void UpdateSorting()
    {
        if (spriteRenderer == null) return;

        int order;
        if (useGridCoordinates && GridManager.Instance != null)
        {
            var cell = GridManager.Instance.WorldToGrid(transform.position);
            order = GridManager.Instance.GetSortingOrder(cell.x, cell.y);
        }
        else
        {
            // Egyszerű Y-alapú sorting (izometrikus: minél lejjebb, annál előrébb)
            order = Mathf.RoundToInt(-transform.position.y * 100f);
        }

        spriteRenderer.sortingOrder = order + sortingOffset;
    }
}
