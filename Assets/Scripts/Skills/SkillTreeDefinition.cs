using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Skill fa definíció – ScriptableObject, az Editorban szerkeszthető.
///
/// Létrehozás: Assets → jobb klikk → Create → TD → Skill Tree Definition
/// </summary>
[CreateAssetMenu(menuName = "TD/Skill Tree Definition")]
public class SkillTreeDefinition : ScriptableObject
{
    [Tooltip("A fa neve – pl. Archer, Stone, Magic")]
    public string treeName = "Archer";

    [Tooltip("Szintlépésenként kapott skill pontok")]
    public int skillPointsPerLevel = 3;

    public List<SkillNodeDefinition> nodes = new List<SkillNodeDefinition>();

    /// <summary>Id alapján visszaadja a csomópont definícióját.</summary>
    public SkillNodeDefinition GetNode(string id) =>
        nodes.Find(n => n.id == id);

    /// <summary>Megadja hány oszlop van a fában (layout számításhoz).</summary>
    public int MaxColumn()
    {
        int max = 0;
        foreach (var n in nodes)
            if (n.column > max) max = n.column;
        return max;
    }

    /// <summary>Megadja hány sor van a fában (Content méret számításhoz).</summary>
    public int MaxRow()
    {
        int max = 0;
        foreach (var n in nodes)
            if (n.row > max) max = n.row;
        return max;
    }
}
