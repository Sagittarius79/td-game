using UnityEngine;

/// <summary>
/// Különleges épület – rákattintásra felderíti egy (vagy több) ellenfél adatait.
/// A felderített ellenfelek száma a Spy skill szintjétől függ.
/// </summary>
public class SpyTowerBuilding : Tower
{
    /// <summary>Megnyitja a SpyTowerPanel-t a skill szintnek megfelelő számú ellenfél adataival.</summary>
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

        if (NetworkGameManager.Instance == null || !NetworkGameManager.Instance.IsPvPMode)
            return;

        ulong localId = Unity.Netcode.NetworkManager.Singleton != null
            ? Unity.Netcode.NetworkManager.Singleton.LocalClientId
            : ulong.MaxValue;

        if (OpponentDataTracker.Instance == null)
        {
            Debug.LogWarning("[SpyTower] OpponentDataTracker null!");
            SpyTowerPanelUI.Instance.ShowNoOpponent();
            return;
        }

        int revealCount = BuildingsConfig.Instance != null
            ? BuildingsConfig.Instance.GetSpyRevealCount()
            : 1;

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
