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
    [UnityEngine.InspectorName("Standard AOE DMG Bonus (x/lvl)")]
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
    [UnityEngine.InspectorName("Spy Reveal Count (x/lvl)")]
    SpyRevealCount,         // SpyTower hány ellenfél adatát mutatja egyszerre
    [UnityEngine.InspectorName("Poison Tower DMG (x/lvl)")]
    PoisonTowerDmg,         // Poison torony méreg sebzésének növelése szintenként
    [UnityEngine.InspectorName("Poison Tower Tick (x/lvl)")]
    PoisonTowerTick,        // Poison torony max stack (tick) számának növelése szintenként
    [UnityEngine.InspectorName("Poison Tower Duration (x/lvl)")]
    PoisonTowerDuration,    // Poison torony stack időtartamának növelése másodpercenként szintenként
    [UnityEngine.InspectorName("Javelin DMG Bonus (x/lvl)")]
    JavelinDamageBonus,     // Javelin torony sebzésének növelése szintenként
    [UnityEngine.InspectorName("Javelin Attack Speed (x/lvl)")]
    JavelinAttackSpeed,     // Javelin torony támadási sebességének növelése szintenként
    [UnityEngine.InspectorName("Javelin Crit Chance (x/lvl)")]
    JavelinCritChance,      // Javelin torony kritikus találat esélye szintenként
    [UnityEngine.InspectorName("Javelin Range (x/lvl)")]
    JavelinRange,           // Javelin torony hatótávolságának növelése szintenként
    [UnityEngine.InspectorName("Javelin Pierce Count (x/lvl)")]
    JavelinPierceCount,     // Javelin lövedék hány ellenségen hatol át
    [UnityEngine.InspectorName("Javelin Charge Speed (x/lvl)")]
    JavelinChargeSpeed,     // Javelin torony attack speed növelése szintenként
    [UnityEngine.InspectorName("Prisma DMG (x/lvl)")]
    PrismaDamageBonus,      // Prisma torony sebzésének növelése szintenként
    [UnityEngine.InspectorName("Prisma Range (x/lvl)")]
    PrismaRange,            // Prisma torony hatótávolságának növelése szintenként
    [UnityEngine.InspectorName("Prisma Element Changer (x/lvl)")]
    PrismaElementChanger,   // Minden n-edik lövésnél random elemtális bónusz sebzés; 1 pont = 1-gyel kevesebb lövés kell
    [UnityEngine.InspectorName("Prisma Tower Bounce (x/lvl)")]
    PrismaBounce,           // Armor-os célpont találatakor pattanás esély; alap 10% + skill pont + célpont armor
    [UnityEngine.InspectorName("Prisma Bounce Count (x/lvl)")]
    PrismaBounceCount,      // Maximum pattanások száma szintenként
    [UnityEngine.InspectorName("Gold Mine Boost (x/lvl)")]
    GoldMineBoost,          // Gold Mine által termelt arany növelése szintenként
    [UnityEngine.InspectorName("Ork Den Boost (x/lvl)")]
    OrkDenBoost,            // Ork Den által termelt bónusz gold növelése killenkénti szintenként
    [UnityEngine.InspectorName("Building HP Over Regen (x/lvl)")]
    BuildingHPOverRegen,    // Ha a kastély teli van, 10 másodpercenként ennyivel nő a max HP
}
