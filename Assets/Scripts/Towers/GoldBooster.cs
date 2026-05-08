using UnityEngine;

/// <summary>
/// Gold Booster épület – minden N-edik kill után 1 gold bónuszt ad.
/// Lerakáskor azonnal aktív, elbontáskor leiratkozik.
///
/// Unity beállítás:
///   1. Húzd rá ezt a scriptet az épület prefabjára
///   2. Állítsd be az Inspector mezőket:
///      - Kills Per Bonus: hány kill után jár 1 gold (alapból 5)
///      - Bonus Gold:      mennyi gold jár (alapból 1)
/// </summary>
public class GoldBooster : MonoBehaviour
{
    [Header("Bónusz")]
    [Tooltip("Hány kill után jár bónusz gold")]
    public int killsPerBonus = 5;

    [Tooltip("Mennyi gold jár minden N-edik kill után")]
    public int bonusGold = 1;

    [Header("Skill fa")]
    [Tooltip("A Buildings skill tree ScriptableObject")]
    public SkillTreeDefinition buildingsSkillTree;

    private int _killCounter      = 0;
    private int _effectiveBonusGold = 1;

    void Start()
    {
        _effectiveBonusGold = bonusGold;

        if (buildingsSkillTree != null && UserProgressManager.Instance != null)
        {
            int bonus = Mathf.RoundToInt(UserProgressManager.Instance.GetTotalSkillEffect(
                SkillEffectType.OrkDenBoost, buildingsSkillTree));
            _effectiveBonusGold += bonus;
        }
    }

    void OnEnable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnEnemyKilled += OnKill;
    }

    void OnDisable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnEnemyKilled -= OnKill;
    }

    void OnKill()
    {
        _killCounter++;
        if (_killCounter >= killsPerBonus)
        {
            _killCounter = 0;
            GameManager.Instance.AddGold(_effectiveBonusGold);
        }
    }
}
