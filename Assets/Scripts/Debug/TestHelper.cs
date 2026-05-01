using UnityEngine;

/// <summary>Tesztelési segédeszköz – éles build előtt töröld vagy kapcsold ki.</summary>
public class TestHelper : MonoBehaviour
{
    [Header("Skill pontok")]
    public int skillPointsToAdd = 3;

    [ContextMenu("Add Skill Points")]
    public void AddSkillPoints()
    {
        UserProgressManager.Instance?.AddSkillPoints(skillPointsToAdd);
        Debug.Log($"TestHelper: +{skillPointsToAdd} skill pont hozzáadva.");
    }
}
