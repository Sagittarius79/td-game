using UnityEngine;

/// <summary>
/// Különleges épület – feltöltés után rákattintásra felderíti 4 véletlen ellenfél adatait.
/// Töltési idő: attackSpeed által meghatározva (Inspector).
/// Felhasználás után a töltés nulláról indul újra.
/// </summary>
public class SpyTowerBuilding : Tower
{
    // ── Töltés ──────────────────────────────────────────────

    float ChargeInterval => 1f / GetEffectiveAttackSpeed();
    bool  IsCharged      => attackTimer >= ChargeInterval;

    /// <summary>
    /// Felülírja a base Update()-et: csak tölti a charge sávot, nem lő és nem keres célpontot.
    /// </summary>
    protected override void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.IsGameOver) return;

        if (!IsCharged)
            attackTimer += Time.deltaTime;

        // Charge sáv frissítése
        if (chargeBarFill != null)
            chargeBarFill.fillAmount = Mathf.Clamp01(attackTimer / ChargeInterval);
    }

    /// <summary>Megnyitja a SpyTowerPanel-t, ha a torony teljesen fel van töltve.</summary>
    public void OpenSpyPanel()
    {
        if (SpyTowerPanelUI.Instance == null)
        {
            Debug.LogWarning("[SpyTower] SpyTowerPanelUI.Instance null!");
            return;
        }

        if (SpyTowerPanelUI.Instance.IsOpen)
        {
            Debug.Log("[SpyTower] Panel már nyitva van – kattintás figyelmen kívül hagyva.");
            return;
        }

        if (!IsCharged)
        {
            Debug.Log($"[SpyTower] Még nem töltött fel ({attackTimer:F1}s / {ChargeInterval:F1}s)");
            return;
        }

        // Töltés visszaállítása felhasználás után
        attackTimer = 0f;
        if (chargeBarFill != null)
            chargeBarFill.fillAmount = 0f;

        if (NetworkGameManager.Instance == null || !NetworkGameManager.Instance.IsPvPMode)
        {
            SpyTowerPanelUI.Instance.ShowNoOpponent();
            return;
        }

        ulong localId = Unity.Netcode.NetworkManager.Singleton != null
            ? Unity.Netcode.NetworkManager.Singleton.LocalClientId
            : ulong.MaxValue;

        if (OpponentDataTracker.Instance == null)
        {
            Debug.LogWarning("[SpyTower] OpponentDataTracker null!");
            SpyTowerPanelUI.Instance.ShowNoOpponent();
            return;
        }

        int revealCount = 4;

        // ── Diagnosztikai log ────────────────────────────────────────
        var knownIds = NetworkGameManager.Instance.GetKnownPvpPlayerIds(localId);
        var knownList = new System.Collections.Generic.List<ulong>(knownIds);
        var sb = new System.Text.StringBuilder();
        sb.Append($"[SpyTower] OpenSpyPanel | localId={localId} | revealCount={revealCount} | ismert játékosok ({knownList.Count}): ");
        foreach (var id in knownList)
            sb.Append($"{id}({NetworkGameManager.Instance.GetPlayerName(id)}) ");
        Debug.Log(sb.ToString());
        // ─────────────────────────────────────────────────────────────

        var snapshots = OpponentDataTracker.Instance.GetRandomOpponents(localId, revealCount);

        if (snapshots.Count == 0)
        {
            Debug.Log($"[SpyTower] Nincs megjeleníthető ellenfél | revealCount={revealCount} | pool üres");
            SpyTowerPanelUI.Instance.ShowNoOpponent();
        }
        else
        {
            Debug.Log($"[SpyTower] {snapshots.Count} ellenfél felderítve | revealCount={revealCount}");
            SpyTowerPanelUI.Instance.Show(snapshots);
        }
    }
}
