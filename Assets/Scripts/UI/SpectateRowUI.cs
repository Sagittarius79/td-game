using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;

/// <summary>
/// Egy sor a SpectatePanel-ben – egy ellenfél neve, kastély HP-ja,
/// legközelebbi szörny távolsága + HP-ja, és torony-típus darabszámok.
/// </summary>
public class SpectateRowUI : MonoBehaviour
{
    public TextMeshProUGUI playerNameText;
    public TextMeshProUGUI castleHpText;
    public TextMeshProUGUI closestEnemyText;
    public TextMeshProUGUI towerCountsText;

    public void SetData(string playerName, int castleHp, float enemyDist, float enemyHp,
                        Dictionary<string, int> towerCounts = null)
    {
        if (playerNameText != null)
            playerNameText.text = playerName;

        if (castleHpText != null)
            castleHpText.text = castleHp >= 0 ? $"{castleHp} HP" : "Castle: ?";

        if (closestEnemyText != null)
        {
            closestEnemyText.text = enemyDist < 0f
                ? "No enemies"
                : $"{enemyDist:F1}m | {Mathf.CeilToInt(enemyHp)} HP";
        }

        if (towerCountsText != null)
        {
            if (towerCounts == null || towerCounts.Count == 0)
            {
                towerCountsText.text = "–";
            }
            else
            {
                var sb = new System.Text.StringBuilder();
                foreach (var kv in towerCounts.OrderByDescending(x => x.Value))
                    sb.AppendLine($"{kv.Key}  ×{kv.Value}");
                towerCountsText.text = sb.ToString().TrimEnd();
            }
        }
    }
}
