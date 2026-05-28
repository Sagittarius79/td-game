using UnityEngine;

/// <summary>
/// Szerkesztőben megjeleníti a grid slot-okat előnézetként.
/// Tedd a CharmGrid és az EquipSlotsParent GameObject-re.
/// Futásidőben nem csinál semmit – a BuildSlots() veszi át.
/// </summary>
[ExecuteAlways]
public class GridPreview : MonoBehaviour
{
    [Tooltip("A CharmSlotPrefab – ugyanaz, mint a CharacterInventoryUI-ban")]
    public GameObject slotPrefab;

    [Tooltip("Hány slot jelenjen meg előnézetként (Grid=24, Equip=5)")]
    public int count = 24;

    void OnValidate()
    {
        if (Application.isPlaying) return;
        RefreshPreview();
    }

    void RefreshPreview()
    {
        // Régi előnézet törlése
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        if (slotPrefab == null || count <= 0) return;

        for (int i = 0; i < count; i++)
            Instantiate(slotPrefab, transform);
    }

    // Futásidőben ne csináljon semmit
    void Start() { }
}
