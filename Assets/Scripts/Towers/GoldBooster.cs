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
    [Tooltip("Hány kill után jár 1 bónusz gold")]
    public int killsPerBonus = 5;

    [Tooltip("Mennyi gold jár minden N-edik kill után")]
    public int bonusGold = 1;

    private int _killCounter = 0;

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
            GameManager.Instance.AddGold(bonusGold);
        }
    }
}
