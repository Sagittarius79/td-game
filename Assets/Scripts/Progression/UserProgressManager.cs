using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A játékos haladási adatainak kezelője – singleton, DontDestroyOnLoad.
///
/// Két réteg:
///   - CharacterRosterData  (_roster)          : Google fiók + karakterlista
///   - UserProgressData     (_activeCharacter) : az éppen kiválasztott karakter adatai
///
/// Mentési kulcsok (EncryptedDataStore):
///   "roster"       → _roster
///   "char_{id}"    → karakterenként külön fájl
/// </summary>
public class UserProgressManager : MonoBehaviour
{
    public static UserProgressManager Instance { get; private set; }

    // ── Skill pontok ─────────────────────────────────────────────────
    private const int SKILL_POINTS_PER_LEVEL = 1;

    // ── Szint határok ─────────────────────────────────────────────────
    // Képlet: XPForLevel(N) = (N-1) × 50  →  Lvl2=50, Lvl3=100, Lvl4=150, ...
    // Nincs maximum szint.

    // ── Adatok ───────────────────────────────────────────────────────
    private CharacterRosterData _roster          = new CharacterRosterData();
    private UserProgressData    _activeCharacter = null;

    // Fiók szintű
    public CharacterRosterData Roster           => _roster;
    public string              AccountDisplayName => _roster.displayName;
    public string              AccountEmail       => _roster.email;
    public bool                IsLoggedIn           => !string.IsNullOrEmpty(_roster.userId);
    public int                 AvailableSkillPoints => _activeCharacter?.availableSkillPoints ?? 0;
    public bool                HasAnyCharacter    => _roster.HasCharacters;

    // Aktív karakter szintű (visszafelé kompatibilis)
    public UserProgressData Data        => _activeCharacter ?? new UserProgressData();
    public string           CharacterName => _activeCharacter?.characterName ?? "";
    public bool             HasCharacter  => _activeCharacter != null && _activeCharacter.HasCharacter;
    public long             TotalXP       => _activeCharacter?.totalXP ?? 0;
    public int              Level         => CalculateLevel(_activeCharacter?.totalXP ?? 0);
    public int              TotalWins     => _activeCharacter?.totalWins ?? 0;
    public int              TotalLosses   => _activeCharacter?.totalLosses ?? 0;

    // ── Események ────────────────────────────────────────────────────
    public event Action<long, long> OnXPChanged;
    public event Action<int, int>   OnLevelUp;
    public event Action<string>     OnSkillChanged;   // skill upgrade/downgrade után tüzel (nodeId)
    public event Action<string>     OnTowerUnlocked;
    public event Action<string>     OnEnemyUnlocked;

    // ── Életciklus ───────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadAll();
    }

    // ── Fiók (Google) ────────────────────────────────────────────────

    /// <summary>Google bejelentkezés után hívandó.</summary>
    public void SetUserInfo(string userId, string displayName, string email)
    {
        _roster.userId      = userId;
        _roster.displayName = displayName;
        _roster.email       = email;
        SaveRoster();
    }

    // ── Karakter létrehozás / választás ──────────────────────────────

    /// <summary>Új karakter létrehozása és aktiválása.</summary>
    public void CreateCharacter(string name)
    {
        string id = Guid.NewGuid().ToString("N").Substring(0, 8);
        var ch = new UserProgressData
        {
            characterId          = id,
            characterName        = name.Trim(),
            createdAtUtc         = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            availableSkillPoints = 1
        };

        _roster.characterIds.Add(id);
        _roster.activeCharacterId = id;
        _activeCharacter = ch;

        SaveRoster();
        SaveActiveCharacter();
        Debug.Log($"UserProgressManager: új karakter – {name} (id: {id})");
    }

    /// <summary>Meglévő karakter kiválasztása és betöltése.</summary>
    public void SelectCharacter(string characterId)
    {
        if (!_roster.characterIds.Contains(characterId))
        {
            Debug.LogWarning($"UserProgressManager: ismeretlen karakter id – {characterId}");
            return;
        }
        _roster.activeCharacterId = characterId;
        SaveRoster();
        LoadActiveCharacter();
        Debug.Log($"UserProgressManager: aktív karakter – {_activeCharacter?.characterName}");
    }

    /// <summary>Visszaadja az összes karakter adatait (a választó képernyőhöz).</summary>
    public List<UserProgressData> GetAllCharacters()
    {
        var result = new List<UserProgressData>();
        foreach (string id in _roster.characterIds)
        {
            string json = EncryptedDataStore.Load("char_" + id);
            if (string.IsNullOrEmpty(json)) continue;
            try   { result.Add(JsonUtility.FromJson<UserProgressData>(json)); }
            catch { Debug.LogWarning($"UserProgressManager: karakter betöltési hiba – {id}"); }
        }
        return result;
    }

    // ── XP és szint ──────────────────────────────────────────────────

    public void AddXP(long amount)
    {
        if (_activeCharacter == null || amount <= 0) return;

        int oldLevel = Level;
        _activeCharacter.totalXP += amount;
        int newLevel = Level;

        OnXPChanged?.Invoke(amount, _activeCharacter.totalXP);

        if (newLevel > oldLevel)
        {
            int gainedPoints = (newLevel - oldLevel) * SKILL_POINTS_PER_LEVEL;
            _activeCharacter.availableSkillPoints += gainedPoints;
            Debug.Log($"SZINT FEL! {oldLevel} → {newLevel} | +{gainedPoints} skill pont (összesen: {_activeCharacter.availableSkillPoints})");
            OnLevelUp?.Invoke(oldLevel, newLevel);
        }

        SaveActiveCharacter();
    }

    // ── Nyerés / vereség ─────────────────────────────────────────────

    public void RecordWin()
    {
        if (_activeCharacter == null) return;
        _activeCharacter.totalWins++;
        SaveActiveCharacter();
    }

    public void RecordLoss()
    {
        if (_activeCharacter == null) return;
        _activeCharacter.totalLosses++;
        SaveActiveCharacter();
    }

    // ── Feloldás (unlock) ────────────────────────────────────────────

    public bool UnlockTower(string towerName)
    {
        if (_activeCharacter == null) return false;
        if (_activeCharacter.unlockedTowers.Contains(towerName)) return false;
        _activeCharacter.unlockedTowers.Add(towerName);
        SaveActiveCharacter();
        OnTowerUnlocked?.Invoke(towerName);
        return true;
    }

    public bool UnlockEnemy(string enemyName)
    {
        if (_activeCharacter == null) return false;
        if (_activeCharacter.unlockedEnemies.Contains(enemyName)) return false;
        _activeCharacter.unlockedEnemies.Add(enemyName);
        SaveActiveCharacter();
        OnEnemyUnlocked?.Invoke(enemyName);
        return true;
    }

    public bool IsTowerUnlocked(string towerName) =>
        _activeCharacter?.unlockedTowers.Contains(towerName) ?? false;
    public bool IsEnemyUnlocked(string enemyName) =>
        _activeCharacter?.unlockedEnemies.Contains(enemyName) ?? false;

    // ── Mentés / betöltés ────────────────────────────────────────────

    public void Save() => SaveActiveCharacter();

    private void SaveRoster()
    {
        _roster.lastSavedUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        EncryptedDataStore.Save("roster", JsonUtility.ToJson(_roster));
    }

    private void SaveActiveCharacter()
    {
        if (_activeCharacter == null) return;
        _activeCharacter.lastSavedUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        EncryptedDataStore.Save("char_" + _activeCharacter.characterId,
                                JsonUtility.ToJson(_activeCharacter));
    }

    private void LoadAll()
    {
        // Roster betöltése
        string rosterJson = EncryptedDataStore.Load("roster");
        if (!string.IsNullOrEmpty(rosterJson))
        {
            try   { _roster = JsonUtility.FromJson<CharacterRosterData>(rosterJson) ?? new CharacterRosterData(); }
            catch { _roster = new CharacterRosterData(); }
            Debug.Log($"UserProgressManager: roster betöltve – {_roster.displayName}, {_roster.characterIds.Count} karakter");
        }
        else
        {
            _roster = new CharacterRosterData();
            Debug.Log("UserProgressManager: nincs roster, új fiók létrehozva.");
        }

        // Aktív karakter betöltése
        if (!string.IsNullOrEmpty(_roster.activeCharacterId))
            LoadActiveCharacter();
    }

    private void LoadActiveCharacter()
    {
        string json = EncryptedDataStore.Load("char_" + _roster.activeCharacterId);
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                _activeCharacter = JsonUtility.FromJson<UserProgressData>(json);

                // Verzió migráció – csak verziószám frissítés, pont hozzáadás nélkül
                if (_activeCharacter.saveVersion == "2" ||
                    _activeCharacter.saveVersion == "3" ||
                    _activeCharacter.saveVersion == "4")
                {
                    _activeCharacter.saveVersion = "5";
                    SaveActiveCharacter();
                    Debug.Log($"UserProgressManager: migráció – verzió frissítve v5-re ({_activeCharacter.characterName})");
                }

                Debug.Log($"UserProgressManager: karakter betöltve – {_activeCharacter.characterName}, Szint {Level}");
            }
            catch
            {
                _activeCharacter = null;
            }
        }
        else
        {
            _activeCharacter = null;
        }
    }

    /// <summary>Egyetlen karakter törlése. Ha az aktív karaktert töröljük, aktív karakter nullázódik.</summary>
    public void DeleteCharacter(string characterId)
    {
        if (!_roster.characterIds.Contains(characterId)) return;

        _roster.characterIds.Remove(characterId);
        EncryptedDataStore.Delete("char_" + characterId);

        if (_roster.activeCharacterId == characterId)
        {
            _activeCharacter = null;
            _roster.activeCharacterId = _roster.characterIds.Count > 0
                ? _roster.characterIds[0]
                : "";

            if (!string.IsNullOrEmpty(_roster.activeCharacterId))
                LoadActiveCharacter();
        }

        SaveRoster();
        Debug.Log($"UserProgressManager: karakter törölve – {characterId}");
    }

    /// <summary>Törli az összes adatot (kijelentkezéskor).</summary>
    public void ClearData()
    {
        foreach (string id in _roster.characterIds)
            EncryptedDataStore.Delete("char_" + id);
        EncryptedDataStore.Delete("roster");
        _roster          = new CharacterRosterData();
        _activeCharacter = null;
        Debug.Log("UserProgressManager: minden adat törölve.");
    }

    // ── Skill fa metódusok ───────────────────────────────────────────

    /// <summary>Visszaadja egy csomópont aktuális szintjét az aktív karakternél.</summary>
    public int GetSkillLevel(string nodeId)
    {
        var save = _activeCharacter?.skillLevels?.Find(s => s.nodeId == nodeId);
        return save?.currentLevel ?? 0;
    }

    /// <summary>
    /// Az adott skill fában összesen elköltött skill pontok száma.
    /// Minden csomópont minden szintje 1 pontnak számít (GetUpgradeCost alapján).
    /// </summary>
    public int GetTotalSpentPoints(SkillTreeDefinition tree)
    {
        if (_activeCharacter == null || tree == null) return 0;
        int total = 0;
        foreach (var node in tree.nodes)
        {
            int level = GetSkillLevel(node.id);
            for (int i = 0; i < level; i++)
                total += node.GetUpgradeCost(i);
        }
        return total;
    }

    /// <summary>
    /// Az adott effect típushoz tartozó összes skill bónusz összege.
    /// Pl. GetTotalSkillEffect(SkillEffectType.CritHitChance, archerTree) → 3.0 = 3%
    /// </summary>
    public float GetTotalSkillEffect(SkillEffectType effectType, SkillTreeDefinition tree)
    {
        if (_activeCharacter == null || tree == null) return 0f;
        float total = 0f;
        foreach (var node in tree.nodes)
        {
            if (node.effectType != effectType) continue;
            int level = GetSkillLevel(node.id);
            total += node.GetTotalEffect(level);
        }
        return total;
    }

    /// <summary>Igaz ha az összes előfeltétel teljesül (ponttól függetlenül).</summary>
    public bool PrerequisitesMet(string nodeId, SkillTreeDefinition tree)
    {
        var def = tree.GetNode(nodeId);
        if (def == null) return false;
        if (Level < def.minCharacterLevel) return false;
        foreach (string prereq in def.prerequisites)
            if (GetSkillLevel(prereq) < 1) return false;
        return true;
    }

    /// <summary>Igaz ha a csomópontot lehet fejleszteni (van pont, előfeltételek teljesülnek, nem maxolt).</summary>
    public bool CanUpgradeSkill(string nodeId, SkillTreeDefinition tree)
    {
        if (_activeCharacter == null) return false;
        var def = tree.GetNode(nodeId);
        if (def == null) return false;
        if (Level < def.minCharacterLevel) return false;
        int currentLevel = GetSkillLevel(nodeId);
        if (currentLevel >= def.maxLevel) return false;
        if (_activeCharacter.availableSkillPoints < def.GetUpgradeCost(currentLevel)) return false;
        foreach (string prereq in def.prerequisites)
            if (GetSkillLevel(prereq) < 1) return false;
        return true;
    }

    /// <summary>Fejleszt egy csomópontot. Igaz ha sikeres.</summary>
    public bool UpgradeSkill(string nodeId, SkillTreeDefinition tree)
    {
        if (!CanUpgradeSkill(nodeId, tree)) return false;

        var def          = tree.GetNode(nodeId);
        int currentLevel = GetSkillLevel(nodeId);
        int cost         = def.GetUpgradeCost(currentLevel);

        _activeCharacter.availableSkillPoints -= cost;

        var save = _activeCharacter.skillLevels.Find(s => s.nodeId == nodeId);
        if (save == null)
        {
            save = new SkillSaveData { nodeId = nodeId };
            _activeCharacter.skillLevels.Add(save);
        }
        save.currentLevel++;
        SaveActiveCharacter();

        Debug.Log($"Skill fejlesztve: {def?.displayName ?? nodeId} → {save.currentLevel}/{def?.maxLevel} (költség: {cost} pont)");
        OnSkillChanged?.Invoke(nodeId);
        return true;
    }

    /// <summary>Skill pontok hozzáadása (teszteléshez vagy jutalomból).</summary>
    public void AddSkillPoints(int amount)
    {
        if (_activeCharacter == null || amount <= 0) return;
        _activeCharacter.availableSkillPoints += amount;
        SaveActiveCharacter();
        Debug.Log($"Skill pontok hozzáadva: +{amount} (összesen: {_activeCharacter.availableSkillPoints})");
    }

    /// <summary>Skill szint csökkentése 1-gyel, visszaadja a pontot. Igaz ha sikeres.</summary>
    public bool DowngradeSkill(string nodeId, SkillTreeDefinition tree)
    {
        if (_activeCharacter == null) return false;
        var save = _activeCharacter.skillLevels.Find(s => s.nodeId == nodeId);
        if (save == null || save.currentLevel <= 0) return false;

        // Ellenőrzés: más skill támaszkodik-e erre az előfeltételként
        var def = tree.GetNode(nodeId);
        if (save.currentLevel == 1)
        {
            foreach (var node in tree.nodes)
                if (node.prerequisites.Contains(nodeId) && GetSkillLevel(node.id) > 0)
                    return false;   // más skill függ tőle, nem lehet levenni
        }

        int refund = def.GetUpgradeCost(save.currentLevel - 1);
        save.currentLevel--;
        _activeCharacter.availableSkillPoints += refund;
        SaveActiveCharacter();
        Debug.Log($"Skill visszavonva: {def?.displayName ?? nodeId} → {save.currentLevel} (visszakapott: {refund} pont)");
        OnSkillChanged?.Invoke(nodeId);
        return true;
    }

    // ── Statikus segédmetódusok ──────────────────────────────────────

    /// <summary>
    /// Összes XP alapján kiszámolja a szintet.
    /// Képlet: XPForLevel(N) = 25 × N × (N-1)
    ///   → N szintről N+1-re lépés: N × 50 XP (1→2: 50, 2→3: 100, 3→4: 150, ...)
    /// </summary>
    public static int CalculateLevel(long totalXP)
    {
        if (totalXP <= 0) return 1;
        // 25*N*(N-1) <= totalXP  →  N = floor((1 + sqrt(1 + 4*totalXP/25)) / 2)
        return (int)((1 + Math.Sqrt(1 + 4.0 * totalXP / 25)) / 2);
    }

    /// <summary>
    /// Mennyi összesített XP kell az adott szinthez.
    /// Képlet: 25 × szint × (szint - 1)
    ///   Lvl1=0, Lvl2=50, Lvl3=150, Lvl4=300, Lvl5=500, ...
    /// </summary>
    public static long XPForLevel(int level)
    {
        if (level <= 1) return 0;
        return (long)25 * level * (level - 1);
    }
}
