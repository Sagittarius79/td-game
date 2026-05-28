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
///   - Belépéskor: karakterek letöltése szerverről (GET /player/characters)
///     és merge a lokális adatokkal (újabb timestamp nyer).
///   - Karakterek feltöltése a szerverre (POST /player/sync).
///   - Szerver-oldali Elo értékek tárolása (UI megjelenítéshez).
///
/// Hívási lánc:
///   MatchmakingClient.OnSessionReady → LoadThenSync()
///   → GET /player/characters → MergeServerCharacters()
///   → POST /player/sync → kapja vissza az Elo-t
/// </summary>
public class ServerSyncManager : MonoBehaviour
{
    public static ServerSyncManager Instance { get; private set; }

    public static ServerSyncManager GetOrCreate()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("[ServerSyncManager]");
        return go.AddComponent<ServerSyncManager>();
    }

    // ── Szerver-oldali statisztikák ──────────────────────────────────
    public int  ServerElo    { get; private set; } = 0;
    public int  ServerWins   { get; private set; } = 0;
    public int  ServerLosses { get; private set; } = 0;
    public bool IsSynced     { get; private set; } = false;

    public event Action OnSyncComplete;

    // ── Lifecycle ────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        if (MatchmakingClient.Instance != null)
        {
            MatchmakingClient.Instance.OnSessionReady += OnSessionReady;

            if (!string.IsNullOrEmpty(MatchmakingClient.Instance.SessionToken))
                StartCoroutine(LoadThenSync());
        }
    }

    void OnDestroy()
    {
        if (MatchmakingClient.Instance != null)
            MatchmakingClient.Instance.OnSessionReady -= OnSessionReady;
    }

    void OnSessionReady()
    {
        StartCoroutine(LoadThenSync());
    }

    // ── Publikus API ─────────────────────────────────────────────────

    /// <summary>Manuálisan triggereli az upload szinkronizálást (pl. karakter létrehozás után).</summary>
    public void TriggerSync()
    {
        StartCoroutine(SyncWithServer());
    }

    /// <summary>Törli a karaktert a szerveren. Fire-and-forget, nem blokkolja a UI-t.</summary>
    public void DeleteCharacterOnServer(string characterId)
    {
        StartCoroutine(DeleteCharacterCoroutine(characterId));
    }

    IEnumerator DeleteCharacterCoroutine(string characterId)
    {
        var mmc = MatchmakingClient.Instance;
        if (mmc == null || string.IsNullOrEmpty(mmc.SessionToken)) yield break;

        string url = $"{mmc.apiBaseUrl}/player/characters/{characterId}";
        using var req = UnityWebRequest.Delete(url);
        req.SetRequestHeader("Authorization", $"Bearer {mmc.SessionToken}");
        req.downloadHandler = new DownloadHandlerBuffer();
        req.timeout = 10;

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
            Debug.Log($"[SSM] Karakter törölve a szerveren: {characterId}");
        else
            Debug.LogWarning($"[SSM] Szerver törlés sikertelen ({characterId}): {req.responseCode} – {req.error}");
    }

    // ── Fő folyamat ──────────────────────────────────────────────────

    IEnumerator LoadThenSync()
    {
        yield return LoadCharactersFromServer();
        yield return SyncWithServer();
    }

    // ── Letöltés szerverről ──────────────────────────────────────────

    IEnumerator LoadCharactersFromServer()
    {
        var mmc = MatchmakingClient.Instance;
        var upm = UserProgressManager.Instance;

        if (mmc == null || string.IsNullOrEmpty(mmc.SessionToken) || upm == null)
            yield break;

        string url = $"{mmc.apiBaseUrl}/player/characters";
        using var req = UnityWebRequest.Get(url);
        req.SetRequestHeader("Authorization", $"Bearer {mmc.SessionToken}");
        req.timeout = 15;

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[SSM] Karakterek letöltése sikertelen: {req.responseCode} – {req.error}");
            yield break;
        }

        var resp = JsonUtility.FromJson<PlayerLoadResponseJson>(req.downloadHandler.text);
        if (resp == null || resp.characters == null || resp.characters.Length == 0)
        {
            Debug.Log("[SSM] Szerveren nincs karakter – helyi adatok maradnak.");
            yield break;
        }

        // ServerCharacterData → UserProgressData konverzió
        var converted = new List<UserProgressData>();
        foreach (var sc in resp.characters)
        {
            var upd = new UserProgressData
            {
                characterId          = sc.character_id,
                characterName        = sc.character_name,
                gameMode             = sc.game_mode == "SSF" ? CharacterGameMode.SSF : CharacterGameMode.PvP,
                characterClass       = sc.character_class == "StoneThrower" ? CharacterClass.StoneThrower
                                     : sc.character_class == "Mage"         ? CharacterClass.Mage
                                     : CharacterClass.Archer,
                totalXP              = sc.total_xp,
                totalMonstersKilled  = sc.total_monsters_killed,
                crystals             = sc.crystals,
                totalWins            = sc.local_wins,
                totalLosses          = sc.local_losses,
                maxWave              = sc.max_wave,
                totalPlaySeconds     = sc.total_play_seconds,
                availableSkillPoints = sc.available_skill_points,
                unlockedTowers       = new List<string>(sc.unlocked_towers  ?? Array.Empty<string>()),
                unlockedEnemies      = new List<string>(sc.unlocked_enemies ?? Array.Empty<string>()),
                createdAtUtc         = sc.created_at_utc,
                lastSavedUtc         = sc.last_saved_utc,
                saveVersion          = string.IsNullOrEmpty(sc.save_version) ? "5" : sc.save_version,
            };
            if (sc.skill_levels != null)
                foreach (var s in sc.skill_levels)
                    upd.skillLevels.Add(new SkillSaveData { nodeId = s.node_id, currentLevel = s.level });

            converted.Add(upd);
        }

        upm.MergeServerCharacters(converted, resp.active_character_id ?? "");
        Debug.Log($"[SSM] {resp.characters.Length} karakter betöltve szerverről, merge kész.");
    }

    // ── Feltöltés szerverre ──────────────────────────────────────────

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

    // ── JSON builder (feltöltéshez) ──────────────────────────────────

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
            sb.Append($"\"game_mode\":\"{ch.gameMode}\",");
            sb.Append($"\"character_class\":\"{ch.characterClass}\",");
            sb.Append($"\"available_skill_points\":{ch.availableSkillPoints},");
            sb.Append($"\"total_xp\":{ch.totalXP},");
            sb.Append($"\"total_monsters_killed\":{ch.totalMonstersKilled},");
            sb.Append($"\"crystals\":{ch.crystals},");
            sb.Append($"\"local_wins\":{ch.totalWins},");
            sb.Append($"\"local_losses\":{ch.totalLosses},");
            sb.Append($"\"max_wave\":{ch.maxWave},");
            sb.Append($"\"total_play_seconds\":{ch.totalPlaySeconds},");
            sb.Append($"\"created_at_utc\":{ch.createdAtUtc},");
            sb.Append($"\"last_saved_utc\":{ch.lastSavedUtc},");
            sb.Append($"\"save_version\":\"{EscapeJson(ch.saveVersion)}\",");

            // Feloldott tornyok
            sb.Append("\"unlocked_towers\":[");
            for (int k = 0; k < ch.unlockedTowers.Count; k++)
            {
                if (k > 0) sb.Append(",");
                sb.Append($"\"{EscapeJson(ch.unlockedTowers[k])}\"");
            }
            sb.Append("],");

            // Feloldott ellenségek
            sb.Append("\"unlocked_enemies\":[");
            for (int k = 0; k < ch.unlockedEnemies.Count; k++)
            {
                if (k > 0) sb.Append(",");
                sb.Append($"\"{EscapeJson(ch.unlockedEnemies[k])}\"");
            }
            sb.Append("],");

            // Skill szintek
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
    class ServerSkillLevel
    {
        public string node_id;
        public int    level;
    }

    [Serializable]
    class ServerCharacterData
    {
        public string   character_id;
        public string   character_name;
        public string   game_mode;
        public string   character_class;
        public int      available_skill_points;
        public long     total_xp;
        public long     total_monsters_killed;
        public int      crystals;
        public int      local_wins;
        public int      local_losses;
        public int      max_wave;
        public int      total_play_seconds;
        public string[] unlocked_towers;
        public string[] unlocked_enemies;
        public long     created_at_utc;
        public long     last_saved_utc;
        public string   save_version;
        public ServerSkillLevel[] skill_levels;
    }

    [Serializable]
    class PlayerLoadResponseJson
    {
        public ServerCharacterData[] characters;
        public string                active_character_id;
    }

    [Serializable]
    class PlayerSyncResponseJson
    {
        public int elo;
        public int server_wins;
        public int server_losses;
    }
}
