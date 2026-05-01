using System;
using System.Collections.Generic;
// SkillSaveData is defined in Assets/Scripts/Skills/SkillSaveData.cs

/// <summary>
/// Egy karakter haladási adatai – titkosítva mentve a telefonra.
/// Fájlnév: char_{characterId}.dat
/// Nem MonoBehaviour, csak adatmodell (JsonUtility-vel szerializálható).
/// </summary>
[Serializable]
public class UserProgressData
{
    public string characterId   = "";           // GUID rövidítve (8 hex karakter)
    public string characterName = "";           // játékoson belüli egyedi név

    public long totalXP      = 0;
    public int  totalWins    = 0;
    public int  totalLosses  = 0;

    public List<string> unlockedTowers  = new List<string>();
    public List<string> unlockedEnemies = new List<string>();

    public int              availableSkillPoints = 0;
    public List<SkillSaveData> skillLevels      = new List<SkillSaveData>();

    public long   createdAtUtc = 0;   // Unix timestamp (UTC), létrehozáskor
    public long   lastSavedUtc = 0;   // Unix timestamp (UTC), utolsó mentéskor
    public string saveVersion  = "3";

    // ── Számított tulajdonságok ──────────────────────────────────────

    public bool HasCharacter => !string.IsNullOrEmpty(characterName);

    public int Level => UserProgressManager.CalculateLevel(totalXP);

    public long XPForNextLevel    => UserProgressManager.XPForLevel(Level + 1);
    public long XPInCurrentLevel  => totalXP - UserProgressManager.XPForLevel(Level);
    public long XPNeededForNextLevel => XPForNextLevel - totalXP;

    public float LevelProgress
    {
        get
        {
            long levelStart = UserProgressManager.XPForLevel(Level);
            long levelEnd   = UserProgressManager.XPForLevel(Level + 1);
            if (levelEnd <= levelStart) return 1f;
            return (float)(totalXP - levelStart) / (levelEnd - levelStart);
        }
    }
}
