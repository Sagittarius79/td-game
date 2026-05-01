/// <summary>A skill hatásának típusa – a harc logikában erre hivatkozunk.</summary>
public enum SkillEffectType
{
    None = 0,
    [UnityEngine.InspectorName("Crit Hit Chance (x/lvl)")]
    CritHitChance,
    [UnityEngine.InspectorName("Multi Shot Chance (x/lvl)")]
    MultiShotChance,
    [UnityEngine.InspectorName("Armor Pierce (x/lvl)")]
    ArmorPierce,
    [UnityEngine.InspectorName("Standard Archer Range (x/lvl)")]
    ArcherRange,
    [UnityEngine.InspectorName("Stun Chance (x/lvl)")]
    StunChance,
    [UnityEngine.InspectorName("Standard Archer DMG Bonus (x/lvl)")]
    DamageBonus,        // Archer torony sebzésbónusza
    [UnityEngine.InspectorName("Standard Stone DMG Bonus (x/lvl)")]
    StoneDamageBonus,   // Stone torony sebzésbónusza
    [UnityEngine.InspectorName("Archer Attack Speed (x/lvl)")]
    AttackSpeed,
    [UnityEngine.InspectorName("Stone Attack Speed (x/lvl)")]
    StoneAttackSpeed,   // stone torony támadási sebessége – effectValuePerLevel adja a bónuszt szintenként
    [UnityEngine.InspectorName("Attack Range (x/lvl)")]
    AttackRange,
    [UnityEngine.InspectorName("Archer Trap Chance (x/lvl)")]
    TrapChance,
    [UnityEngine.InspectorName("Standard Castle Health (x/lvl)")]
    CastleHealth,
    [UnityEngine.InspectorName("Standard Stone AOE Radius (x/lvl)")]
    AOERadius,
    [UnityEngine.InspectorName("Standard Stone Tower Armor (x/lvl)")]
    TowerArmor,
    [UnityEngine.InspectorName("Stone AOE Damage Bonus (x/lvl)")]
    AOEDamageBonus,
    [UnityEngine.InspectorName("Stone Focus Attack Speed Lvl2 (x/lvl)")]
    FocusAttackSpeed,   // ugyanarra a célpontra lőve lövésenként növeli az AS-t (reset célpontváltáskor)
    [UnityEngine.InspectorName("Stone Magic Damage Bonus Lvl2 (x/lvl)")]
    MagicDamageBonus,   // extra mágikus sebzés találatonként (külön DamageType.Magic hit)
    [UnityEngine.InspectorName("FireBall Interval (x/lvl)")]
    FireBallInterval,   // alap 40 lövésenként tűzgolyó; pontonként 1-gyel csökken az intervallum
    [UnityEngine.InspectorName("Charge Damage Bonus Lvl2 (x/lvl)")]
    ChargeDamageBonus,  // minél tovább nem lő a torony, a következő lövés ennyivel több sebzést ad másodpercenként
    [UnityEngine.InspectorName("Stone Crit Chance Lvl2 (x/lvl)")]
    StoneCritChance,    // stone torony kritikus találat esélye (külön az Archer CritHitChance-től)
    [UnityEngine.InspectorName("Cheaper Magic Tower (x/lvl)")]
    CheaperMagicTower,  // mágikus torony árának csökkentése szintenként 1 goldal
    [UnityEngine.InspectorName("Magic Attack Speed (x/lvl)")]
    MagicAttackSpeed,   // mágikus torony támadási sebessége szintenként 0.1-gyel nő
    [UnityEngine.InspectorName("Magic Pull Radius (x/lvl)")]
    MagicPullRadius,    // mágikus torony pull sugarának növelése szintenként
    [UnityEngine.InspectorName("Magic DMG Bonus Lvl2 (x/lvl)")]
    MagicDmgBonus,      // mágikus torony Lvl2 lövedék sebzésének növelése szintenként
    [UnityEngine.InspectorName("Building Cost Reduction (x/lvl %)")]
    BuildingCostReduction,  // összes épület költségét csökkenti %-ban szintenként (lefele kerekítve)
    [UnityEngine.InspectorName("Building HP Extra (x/lvl)")]
    BuildingHPExtra,        // kastély maximális HP-ját növeli szintenként
    [UnityEngine.InspectorName("Building HP Regen (x/lvl)")]
    BuildingHPRegen,        // kastély HP-ját gyógyítja 30 másodpercenként szintenként
}
