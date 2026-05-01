using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Szerver szinkronizálás menedzser – singleton, DontDestroyOnLoad.
///
/// Felelős:
///   - Belépéskor: karakter adatok feltöltése a szerverre (/player/sync)
///     és a szerver-oldali Elo letöltése.
///   - Tárolja a szerver-oldali Elo értéket (UI megjelenítéshez).
///
/// A meccs eredmény bejelentése a Unity DEDIKÁLT SZERVEREN fut
/// (NetworkGameManager.ReportMatchResultToApi), nem a kliensen.
///
/// Hívási lánc:
///   MatchmakingClient.OnSessionReady → SyncWithServer() coroutine
///   → POST /player/sync → kapja vissza az Elo-t
/// </summary>
public class ServerSyncManager : MonoBehaviour
{
    public static ServerSyncManager Instance { get; private set; }

    // ── Szerver-oldali statisztikák (UI-ban megjeleníthetők) ──────────
    public int  ServerElo    { get; private set; } = 0;
    public int  ServerWins   { get; private set; } = 0;
    public int  ServerLosses { get; private set; } = 0;
    public bool IsSynced     { get; private set; } = false;

    /// <summary>Szinkronizálás sikeresen befejezve.</summary>
    public event Action OnSyncComplete;

    // ── Lifecycle ────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        // Ha a MatchmakingClient már session-nel rendelkezik (scene váltás után)
        if (MatchmakingClient.Instance != null)
        {
            MatchmakingClient.Instance.OnSessionReady += OnSessionReady;

            if (!string.IsNullOrEmpty(MatchmakingClient.Instance.SessionToken))
                StartCoroutine(SyncWithServer());
        }
    }

    void OnDestroy()
    {
        if (MatchmakingClient.Instance != null)
            MatchmakingClient.Instance.OnSessionReady -= OnSessionReady;
    }

    void OnSessionReady()
    {
        StartCoroutine(SyncWithServer());
    }

    // ── Szinkronizálás ───────────────────────────────────────────────

    /// <summary>
    /// Feltölti az összes karaktert a szerverre, majd letölti az Elo-t.
    /// Manuálisan is meghívható (pl. karakter létrehozás után).
    /// </summary>
    public void TriggerSync()
    {
        StartCoroutine(SyncWithServer());
    }

    IEnumerator SyncWithServer()
    {
        var mmc = MatchmakingClient.Instance;
        var upm = UserProgressManager.Instance;

        if (mmc == null || string.IsNullOrEmpty(mmc.SessionToken))
        {
            Debug.LogWarning("[SSM] Szinkronizálás kihagyva – nincs session token.");
            yield break;
        }

        if (upm == null)
        {
            Debug.LogWarning("[SSM] Szinkronizálás kihagyva – UserProgressManager nincs.");
            yield break;
        }

        // ── JSON összeállítása ────────────────────────────────────────
        string json = BuildSyncJson(upm);
        if (json == null)
        {
            Debug.Log("[SSM] Nincs karakter – szinkronizálás kihagyva.");
            yield break;
        }

        Debug.Log("[SSM] Szinkronizálás indul...");

        string url = $"{mmc.apiBaseUrl}/player/sync";
        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("Authorization", $"Bearer {mmc.SessionToken}");
        req.timeout = 15;

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[SSM] Szinkronizálás hiba: {req.responseCode} – {req.error}");
            yield break;
        }

        // ── Válasz feldolgozása ───────────────────────────────────────
        var resp = JsonUtility.FromJson<PlayerSyncResponseJson>(req.downloadHandler.text);
        if (resp == null)
        {
            Debug.LogWarning("[SSM] Szinkronizálás: érvénytelen válasz.");
            yield break;
        }

        ServerElo    = resp.elo;
        ServerWins   = resp.server_wins;
        ServerLosses = resp.server_losses;
        IsSynced     = true;

        Debug.Log($"[SSM] Szinkronizálás OK – Elo: {ServerElo} | W/L: {ServerWins}/{ServerLosses}");
        OnSyncComplete?.Invoke();
    }

    // ── JSON builder ─────────────────────────────────────────────────

    string BuildSyncJson(UserProgressManager upm)
    {
        var allChars = upm.GetAllCharacters();
        if (allChars == null || allChars.Count == 0)
            return null;

        string charName   = EscapeJson(upm.CharacterName);
        string googleName = EscapeJson(MatchmakingClient.Instance?.DisplayName
                            ?? GoogleAuthManager.Instance?.DisplayName ?? "");

        var sb = new StringBuilder();
        sb.Append("{");
        sb.Append($"\"active_character_id\":\"{upm.Roster.activeCharacterId}\",");
        sb.Append($"\"active_character_name\":\"{charName}\",");
        sb.Append($"\"google_name\":\"{googleName}\",");
        sb.Append("\"characters\":[");

        for (int i = 0; i < allChars.Count; i++)
        {
            var ch = allChars[i];
            if (i > 0) sb.Append(",");

            sb.Append("{");
            sb.Append($"\"character_id\":\"{ch.characterId}\",");
            sb.Append($"\"character_name\":\"{EscapeJson(ch.characterName)}\",");
            sb.Append($"\"available_skill_points\":{ch.availableSkillPoints},");
            sb.Append($"\"total_xp\":{ch.totalXP},");
            sb.Append($"\"local_wins\":{ch.totalWins},");
            sb.Append($"\"local_losses\":{ch.totalLosses},");
            sb.Append("\"skill_levels\":[");

            var skills = ch.skillLevels;
            for (int j = 0; j < skills.Count; j++)
            {
                if (j > 0) sb.Append(",");
                sb.Append($"{{\"node_id\":\"{EscapeJson(skills[j].nodeId)}\",");
                sb.Append($"\"level\":{skills[j].currentLevel}}}");
            }

            sb.Append("]}");
        }

        sb.Append("]}");
        return sb.ToString();
    }

    static string EscapeJson(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    // ── JSON modellek ────────────────────────────────────────────────

    [Serializable]
    class PlayerSyncResponseJson
    {
        public int elo;
        public int server_wins;
        public int server_losses;
    }
}
