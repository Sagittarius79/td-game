using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Egy sor a SpyTowerPanel-ben – egy ellenfél neve, legtöbbet épített torony ikonja, kastély HP-ja.
/// Prefabként használandó; a SpyTowerPanelUI példányosítja.
/// </summary>
public class SpyOpponentRowUI : MonoBehaviour
{
    public TextMeshProUGUI playerNameText;
    public Image           mostBuiltIcon;
    public TextMeshProUGUI castleHpText;

    [Tooltip("Ha nincs torony adat, ez a sprite jelenik meg (opcionális)")]
    public Sprite fallbackSprite;

    public void SetData(OpponentSnapshot snapshot)
    {
        if (playerNameText != null)
            playerNameText.text = snapshot.PlayerName;

        if (castleHpText != null)
            castleHpText.text = snapshot.CastleHp >= 0
                ? snapshot.CastleHp.ToString()
                : "?";

        if (mostBuiltIcon != null)
        {
            TowerShopItem.IconRegistry.TryGetValue(snapshot.MostBuiltTower, out Sprite icon);
            Sprite resolved = icon ?? fallbackSprite;
            mostBuiltIcon.sprite  = resolved;
            mostBuiltIcon.enabled = resolved != null;
        }
    }

    public void SetEmpty()
    {
        if (playerNameText != null) playerNameText.text = "–";
        if (castleHpText   != null) castleHpText.text   = "–";
        if (mostBuiltIcon  != null) mostBuiltIcon.enabled = false;
    }
}
