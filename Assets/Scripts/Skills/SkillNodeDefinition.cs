using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Egy skill fa csomópont definíciója – a SkillTreeDefinition ScriptableObject-ben tárolva.
/// </summary>
[Serializable]
public class SkillNodeDefinition
{
    [Tooltip("Egyedi azonosító – a mentésben ez alapján tárolódik")]
    public string id          = "";

    [Tooltip("Megjelenített név a kockán")]
    public string displayName = "";

    [TextArea(2, 4)]
    [Tooltip("Leírás (tooltip vagy részletnézet)")]
    public string description = "";

    [Tooltip("Kocka ikonja")]
    public Sprite icon;

    [Tooltip("Hányszor lehet fejleszteni (max szint)")]
    public int maxLevel = 10;

    [Tooltip("Hány skill pontba kerül egy szint (alapból 1)")]
    [HideInInspector] public int costPerLevel = 1;   // régi mező – már nem használt

    [Tooltip("Sor a fában – 0 = legfelső")]
    public int row    = 0;

    [Tooltip("Oszlop a fában – 0 = bal széle")]
    public int column = 0;

    [Tooltip("Ezeknek a node-oknak legalább 1-es szintűnek kell lenniük az upgrade-hez")]
    public List<string> prerequisites = new List<string>();

    [Header("Hatás")]
    [Tooltip("Milyen gameplay értéket módosít ez a skill")]
    public SkillEffectType effectType = SkillEffectType.None;

    [Tooltip("Mennyit ad hozzá szintenként (pl. 1 = 1% / szint)")]
    public float effectValuePerLevel = 0f;

    /// <summary>Teljes hatás az adott szinten (szint × érték/szint).</summary>
    public float GetTotalEffect(int level) => level * effectValuePerLevel;

    /// <summary>
    /// Mennyibe kerül a következő szint elérése az aktuális szintről.
    /// Képlet: floor((currentLevel + 1) / 2), minimum 1.
    /// Példák: 0→1 = 1, 4→5 = 2, 5→6 = 3, 7→8 = 4, 8→9 = 4
    /// </summary>
    public static int GetUpgradeCost(int currentLevel) =>
        Mathf.Max(1, Mathf.FloorToInt((currentLevel + 1) / 2f));
}
