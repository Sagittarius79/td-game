using UnityEngine;

/// <summary>
/// Arány termelő épület – nem lő, hanem rendszeres időközönként gold-ot termel.
///
/// Dinamikus árhoz add hozzá a DynamicPriceBuilding scriptet is a prefabhoz,
/// és a TowerShopItem-en kapcsold be a "Dynamic Price" jelölőt.
///
/// Unity beállítás:
///   1. Húzd rá ezt a scriptet az épület prefabjára
///   2. Add hozzá a DynamicPriceBuilding scriptet is
///   3. Állítsd be az Inspector mezőket:
///      - Gold Per Tick: mennyi gold-ot termel egy ciklusban (alapból 1)
///      - Tick Interval: milyen gyakran termeljen másodpercben (alapból 1)
/// </summary>
public class GoldMine : MonoBehaviour
{
    [Header("Termelés")]
    [Tooltip("Mennyi gold-ot termel egy ciklusban")]
    public int goldPerTick = 1;

    [Tooltip("Milyen gyakran termeljen (másodperc)")]
    public float tickInterval = 1f;

    [Header("Skill fa")]
    [Tooltip("A Buildings skill tree ScriptableObject")]
    public SkillTreeDefinition buildingsSkillTree;

    private float _timer    = 0f;
    private int   _effectiveGoldPerTick;

    void Start()
    {
        _effectiveGoldPerTick = goldPerTick;

        if (buildingsSkillTree != null && UserProgressManager.Instance != null)
        {
            int bonus = Mathf.RoundToInt(UserProgressManager.Instance.GetTotalSkillEffect(
                SkillEffectType.GoldMineBoost, buildingsSkillTree));
            _effectiveGoldPerTick += bonus;
        }
    }

    void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.IsGameOver) return;

        _timer += Time.deltaTime;

        if (_timer >= tickInterval)
        {
            _timer -= tickInterval;
            GameManager.Instance.AddGold(_effectiveGoldPerTick);
        }
    }
}
