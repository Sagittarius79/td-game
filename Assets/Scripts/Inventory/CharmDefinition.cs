using System;
using UnityEngine;

[Serializable]
public class CharmDefinition
{
    [Tooltip("Egyedi string ID – ez kerül a mentésbe. Ne változtasd meg utólag!")]
    public string id = "";

    public string displayName = "";

    [TextArea(2, 4)]
    public string description = "";

    public Sprite icon;

    [Tooltip("Hang ami lejátszódik amikor a charm hatása aktiválódik a játékban (opcionális)")]
    public AudioClip effectSound;

    public CharmRarity     rarity      = CharmRarity.Common;
    public CharmEffectType effectType  = CharmEffectType.None;
    public float           effectValue = 0f;

    [Tooltip("Charm szintje: 1, 2 vagy 3. Két azonos típusú ÉS szintű charm egyesíthető eggyel magasabbra.")]
    public int             level       = 1;
}

public enum CharmRarity
{
    Common,
    Rare,
    Epic,
    Legendary
}

// ════════════════════════════════════════════════════════════════════
//  FONTOS: Az enum értékeket a Unity EGÉSZ SZÁM szerint szerializálja!
//  - SOHA ne szúrj be új tagot a meglévők KÖZÉ, és ne változtasd meg a
//    meglévő = N értékeket → a charmokhoz kiosztott hatások elcsúsznának.
//  - Új hatást MINDIG a végére tegyél, a következő szabad számmal.
// ════════════════════════════════════════════════════════════════════
public enum CharmEffectType
{
    None             = 0,
    // Archer
    ArrowDamageBonus = 1,
    CritChanceStoneBonus = 2, // X% crit esély a Stone direkt találatra
    MultiShotBonus   = 3,
    // Stone
    AOERadiusBonus   = 4,
    StoneDamageBonus = 5,
    // Mage
    MageRangeBonus   = 6,
    MageDamageBonus  = 7,
    // Általános
    AttackSpeedBonus = 8,
    GoldBonus        = 9,
    TowerArmorBonus  = 10,

    // ── Új hatások – csak a végére, növekvő számmal! ──────────────────
    InstantKillChanceMagic = 11, // X% esély, hogy a Magic találat azonnal megöli az ellenfelet
    AOECritChanceBonus    = 12, // X% esély, hogy az AOE splash sebzés kritikus
    LaserArmorStripChance = 13, // X% esély, hogy egy páncél lekerül (Laser találat)
    StoneStunChance       = 14, // X% esély, hogy a Stone találat elkábítja az eltalált ellenfelet
    JavelinBurnChance     = 15, // X% esély, hogy a Javelin találat burn effektet rak
    PoisonBounceChance    = 16, // X% esély, hogy a méreg átpattan a legközelebbi ellenfélre (Poison találat)
    TeleportHalveHpChance = 17, // X% esély, hogy a teleport találat megfelezi a szörny HP-ját
    TurulExtraBirdChance  = 18, // X% esély, hogy a Turul torony +1 madarat indít
}
