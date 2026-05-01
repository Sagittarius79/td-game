using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Egy szinthez tartozó jutalmak leírása.
/// </summary>
[Serializable]
public class LevelReward
{
    [Header("Szint és XP követelmény")]
    [Tooltip("Ez a szintszám feloldódik ezzel a jutalommal")]
    public int  level      = 2;
    [Tooltip("Összesített XP ami ehhez a szinthez szükséges (egyezzen a UserProgressManager.LEVEL_XP-vel!)")]
    public long requiredXP = 300;

    [Header("Jutalmak (mindkettő hagyható üresen)")]
    [Tooltip("Feloldódó torony neve. Pontosan egyezzen a TowerDefinition.towerName mezővel.")]
    public string unlockedTowerName = "";
    [Tooltip("Feloldódó küldhető szörny neve. Pontosan egyezzen a SendableEnemyDefinition.enemyName mezővel.")]
    public string unlockedEnemyName = "";

    [Header("Leírás")]
    [TextArea(1, 2)]
    [Tooltip("Ez jelenik meg a játékosnak szintlépéskor")]
    public string rewardDescription = "";
}

/// <summary>
/// ScriptableObject – a szintrendszer konfigurációja.
///
/// Létrehozás: jobb klikk a Projectben → Create → TD → Szint Konfiguráció
/// Egy LevelConfig asset-et assign-olj be a UserProgressManager Inspector mezőjébe.
///
/// FONTOS: a requiredXP értékeknek egyezniük kell a UserProgressManager.LEVEL_XP
/// tömbben szereplő értékekkel!
/// </summary>
[CreateAssetMenu(fileName = "LevelConfig", menuName = "TD/Szint Konfiguráció")]
public class LevelConfig : ScriptableObject
{
    [Header("Szint jutalmak – 1-es szinttől kezdve")]
    public List<LevelReward> levels = new List<LevelReward>
    {
        new LevelReward
        {
            level = 2, requiredXP = 300,
            rewardDescription = "Első szintlépés! Haladó harcos."
        },
        new LevelReward
        {
            level = 3, requiredXP = 700,
            unlockedTowerName = "Varázsló",
            rewardDescription = "Varázsló torony feloldva!"
        },
        new LevelReward
        {
            level = 4, requiredXP = 1500,
            rewardDescription = "Harc mestere."
        },
        new LevelReward
        {
            level = 5, requiredXP = 3000,
            unlockedEnemyName = "FarkasSzörny",
            rewardDescription = "Farkas Szörny feloldva! Küldheted ellenfeleidre."
        },
        new LevelReward
        {
            level = 6, requiredXP = 5500,
            rewardDescription = "Veterán katona."
        },
        new LevelReward
        {
            level = 7, requiredXP = 9000,
            unlockedTowerName = "ElektromosTorony",
            rewardDescription = "Elektromos Torony feloldva!"
        },
        new LevelReward
        {
            level = 8, requiredXP = 14000,
            rewardDescription = "Hősies harcos."
        },
        new LevelReward
        {
            level = 9, requiredXP = 21000,
            unlockedEnemyName = "SárkánySzörny",
            rewardDescription = "Sárkány Szörny feloldva! Küldheted ellenfeleidre."
        },
        new LevelReward
        {
            level = 10, requiredXP = 30000,
            rewardDescription = "LEGENDA! Elérted a maximális szintet."
        },
    };

    // ── Segédmetódusok ───────────────────────────────────────────────

    /// <summary>Visszaadja az adott szinthez tartozó jutalom leírást, vagy null-t.</summary>
    public LevelReward GetRewardForLevel(int level)
    {
        foreach (var r in levels)
            if (r.level == level) return r;
        return null;
    }
}
