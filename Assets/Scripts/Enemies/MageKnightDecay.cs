using UnityEngine;

/// <summary>
/// Mage Knight különleges képessége:
/// Spawnnál 200 Armor és 200 Magic Resist értékkel indul,
/// majd másodpercenként csökken mindkettő a megadott értékkel,
/// amíg 0-ra nem esik.
///
/// Unity beállítás:
///   1. Húzd rá a Mage Knight prefabre
///   2. Az Enemy komponensen állítsd be: armor = 200, magicResist = 200
///   3. Ezen a scripten állítsd be az armorDecayPerSecond és magicResistDecayPerSecond értékeket
/// </summary>
[RequireComponent(typeof(Enemy))]
public class MageKnightDecay : MonoBehaviour
{
    [Header("Romlás értékek")]
    [Tooltip("Ennyivel csökken az Armor másodpercenként")]
    public float armorDecayPerSecond = 5f;

    [Tooltip("Ennyivel csökken a Magic Resist másodpercenként")]
    public float magicResistDecayPerSecond = 5f;

    private Enemy _enemy;
    private float _decayTimer = 0f;

    void Awake()
    {
        _enemy = GetComponent<Enemy>();
    }

    void Update()
    {
        if (_enemy == null || _enemy.IsDead) return;
        if (_enemy.armor <= 0f && _enemy.magicResist <= 0f) return;

        _decayTimer += Time.deltaTime;

        if (_decayTimer >= 1f)
        {
            _decayTimer -= 1f;

            if (_enemy.armor > 0f)
                _enemy.armor = Mathf.Max(0f, _enemy.armor - armorDecayPerSecond);

            if (_enemy.magicResist > 0f)
                _enemy.magicResist = Mathf.Max(0f, _enemy.magicResist - magicResistDecayPerSecond);
        }
    }
}
