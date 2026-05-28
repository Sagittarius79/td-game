using UnityEngine;

/// <summary>
/// Karakter osztály bónusz kezelő – singleton, scene-ben kell lennie.
///
/// Unity Editor beállítás:
///   1. Hozz létre egy CharacterClassBonusConfig asset-et:
///      Assets → Create → TD → Character Class Bonus Config
///   2. Rakd rá ezt a scriptet egy Manager GameObject-re (pl. GameManager mellé)
///   3. Kösd be a Config mezőbe a létrehozott asset-et
/// </summary>
public class CharacterClassBonus : MonoBehaviour
{
    public static CharacterClassBonus Instance { get; private set; }

    [Tooltip("A hangolható bónusz értékek ScriptableObject-je")]
    public CharacterClassBonusConfig config;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    // ── Statikus segédmetódusok (tornyokban használandó) ─────────────

    /// <summary>Igaz ha az aktív karakter a megadott osztályú.</summary>
    public static bool Is(CharacterClass cls) =>
        UserProgressManager.Instance != null &&
        UserProgressManager.Instance.HasCharacter &&
        UserProgressManager.Instance.Data.characterClass == cls;

    /// <summary>A hangolható config – null ha a Manager nincs a scene-ben.</summary>
    public static CharacterClassBonusConfig Config => Instance?.config;
}
