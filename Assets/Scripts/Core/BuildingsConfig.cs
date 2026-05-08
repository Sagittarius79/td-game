using UnityEngine;

/// <summary>
/// Globális épületkonfiguráció singleton – a játék scene-ben egy GameObject-re kell rakni.
/// Tartalmazza a Buildings skill tree referenciát, amelyből az árengedmény olvasódik.
/// </summary>
public class BuildingsConfig : MonoBehaviour
{
    public static BuildingsConfig Instance { get; private set; }

    [Header("Skill fa")]
    [Tooltip("A Buildings SkillTreeDefinition ScriptableObject")]
    public SkillTreeDefinition buildingsSkillTree;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>
    /// Visszaadja az összes épületre érvényes %-os árengedményt (0–100).
    /// Pl. 10 → minden épület 10%-kal olcsóbb.
    /// </summary>
    public float GetCostReductionPercent()
    {
        if (buildingsSkillTree == null || UserProgressManager.Instance == null) return 0f;
        return UserProgressManager.Instance.GetTotalSkillEffect(
            SkillEffectType.BuildingCostReduction, buildingsSkillTree);
    }

    /// <summary>
    /// Hány ellenfél adatát mutatja a SpyTower (skill szint = darabszám, min. 1).
    /// </summary>
    public int GetSpyRevealCount()
    {
        if (buildingsSkillTree == null || UserProgressManager.Instance == null) return 1;
        int count = Mathf.RoundToInt(UserProgressManager.Instance.GetTotalSkillEffect(
            SkillEffectType.SpyRevealCount, buildingsSkillTree));
        return Mathf.Max(1, count);
    }
}
