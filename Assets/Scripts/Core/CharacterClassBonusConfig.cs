using UnityEngine;

/// <summary>
/// Karakter osztály bónuszok hangolható értékei.
/// Létrehozás: Assets → Create → TD → Character Class Bonus Config
/// </summary>
[CreateAssetMenu(fileName = "CharacterClassBonusConfig", menuName = "TD/Character Class Bonus Config")]
public class CharacterClassBonusConfig : ScriptableObject
{
    [Header("Archer – Kettős lövés")]
    [Tooltip("Esély hogy a lövés 0–1 között, pl. 0.1 = 10%")]
    [Range(0f, 1f)]
    public float doubleShotChance = 0.1f;

    [Tooltip("Másodperccel később tüzel újra")]
    [Range(0f, 1f)]
    public float doubleShotDelay = 0.1f;

    [Header("Stone Thrower – AOE szorzó")]
    [Tooltip("Az összes skill bónusz utáni AOE sugár szorzója, pl. 1.2 = 20%-kal nagyobb")]
    [Range(1f, 3f)]
    public float aoeRadiusMultiplier = 1.2f;

    [Header("Mage – Range szorzó")]
    [Tooltip("Az összes skill bónusz utáni hatótávolság szorzója, pl. 1.1 = 10%-kal nagyobb")]
    [Range(1f, 3f)]
    public float rangeMultiplier = 1.1f;
}
