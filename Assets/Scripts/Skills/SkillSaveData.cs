using System;

/// <summary>
/// Egy skill csomópont mentett szintje – karakterenként tárolva a UserProgressData-ban.
/// </summary>
[Serializable]
public class SkillSaveData
{
    public string nodeId       = "";
    public int    currentLevel = 0;
}
