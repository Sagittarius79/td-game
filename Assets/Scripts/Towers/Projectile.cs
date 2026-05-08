using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ═══════════════════════════════════════════════════════
///  LÖVEDÉK
///  A célpont felé repül, becsapódáskor sebzést okoz.
///  Területi sebzésnél (AOE) minden közeli ellenséget sebez.
///
///  Inicializálás: Tower.Shoot() hívja az Initialize()-t
///  Az impactEffect mérete automatikusan igazodik az AOE sugárhoz.
/// ═══════════════════════════════════════════════════════
/// </summary>
public class Projectile : MonoBehaviour
{
    // ══════════════════════════════════════════════════════
    //  INSPECTOR MEZŐK
    // ══════════════════════════════════════════════════════

    [Header("Azonosítás")]
    [Tooltip("Lövedék azonosító tag – az ellenség immunitás listájában ez szerepelhet (pl. Arrow, Magic, Poison)")]
    public string projectileTag = "";

    [Header("Áthatolás (Pierce)")]
    [Tooltip("Ha > 0, a lövedék ennyi extra ellenségen hatol át a célpont után")]
    public int pierceCount = 0;

    [Header("Javelin")]
    [Tooltip("Ha be van kapcsolva, ez a lövedék crit-elhet a Javelin Crit Chance skill alapján")]
    public bool javelinCanCrit = false;
    [Tooltip("Javelin crit esetén a sebzés hány %-a legyen (pl. 200 = dupla sebzés)")]
    public float javelinCritDamagePercent = 200f;

    [Header("Prisma")]
    [Tooltip("Ha be van kapcsolva, a PrismaDamageBonus skill érvényes erre a lövedékre")]
    public bool prismaDmgBonus = false;
    [Tooltip("Ha be van kapcsolva, a PrismaRange skill érvényes erre a lövedékre")]
    public bool prismaRangeBonus = false;
    [Tooltip("Ha be van kapcsolva, ez a lövedék részt vesz az Element Changer mechanikában")]
    public bool prismaElementChanger = false;
    [Tooltip("Hány lövésenként aktiválódik az elementális bónusz (skill pont nélkül)")]
    public int prismaElementBaseInterval = 10;
    [Tooltip("Az elementális bónusz sebzés mértéke (a normál damage %-a, pl. 100 = ugyanannyi)")]
    public float prismaElementDamagePercent = 50f;

    [Tooltip("Ha be van kapcsolva, ez a lövedék részt vesz a Bounce mechanikában")]
    public bool prismaBounce = false;
    [Tooltip("Maximum hány alkalommal pattanhat a lövedék (skill növeli)")]
    public int prismaMaxBounces = 1;
    [Tooltip("Alap pattanási esély %-ban, armor nélkül is megvan (pl. 10 = mindig 10% esély)")]
    public float prismaBounceBaseChance = 0f;
    [Tooltip("1 armor pont ennyi %-ot ad a pattanási esélyhez (pl. 10 = 1 armor → +10%)")]
    public float prismaBounceChancePerArmor = 10f;
    [Tooltip("Maximum pattanási esély %-ban (pl. 80 = max 80%)")]
    public float prismaBounceMaxChance = 80f;

    // PrismaTower állítja be lövéskor – ne módosítsd kézzel
    [HideInInspector] public bool  prismaElementActive   = false;
    [HideInInspector] public float prismaBounceSkillBonus = 0f;
    [HideInInspector] public float prismaBounceRange    = 0f;
    [HideInInspector] public int   prismaBouncesLeft    = 0;

    [Header("Célzás")]
    [Tooltip("Ha be van kapcsolva, ez a lövedék el tudja találni a repülő ellenségeket is")]
    public bool canHitFlying = false;

    [Header("Mozgás")]
    [Tooltip("Repülési sebesség")]
    public float moveSpeed = 8f;
    [Tooltip("Ennyi másodperc után megsemmisül célpont nélkül")]
    public float maxLifetime = 5f;

    [Header("Hatás")]
    [Tooltip("Becsapódáskor spawnolt effekt prefab (pl. AOE robbanás animáció)")]
    public GameObject impactEffect;
    [Tooltip("Az effekt pozíció eltolása a találat pontjához képest (Y=-0.3 = lejjebb)")]
    public Vector3 impactOffset = Vector3.zero;
    [Tooltip("Ha be van kapcsolva, becsapódáskor visszateleportálja az ellenséget a kezdőpontra")]
    public bool teleportOnHit = false;

    [Header("Pull (összehúzás)")]
    [Tooltip("Ha be van kapcsolva, becsapódáskor a közeli szörnyeket a találat pontja felé húzza")]
    public bool pullOnHit = false;
    [Tooltip("Mekkora sugarú körben húzza össze a szörnyeket (tile egységben)")]
    public float pullRadius = 2f;
    [Tooltip("Milyen erősen húzza (egység/s)")]
    public float pullForce = 3f;
    [Tooltip("Meddig tart a pull hatás (másodperc)")]
    public float pullDuration = 0.4f;

    [Header("Méreg (DoT)")]
    [Tooltip("Ha be van kapcsolva, találatkor mérgezést alkalmaz a célponton")]
    public bool applyPoison = false;
    [Tooltip("Méreg sebzés másodpercenként (stack-enként)")]
    public float poisonDamagePerSecond = 1f;
    [Tooltip("Egy stack időtartama másodpercben")]
    public float poisonDuration = 5f;
    [Tooltip("Maximum hány poison stack lehet egyszerre a szörnyön (1 stack = 1 DMG/sec)")]
    public int maxPoisonStacks = 3;
    [Tooltip("Opcionális méreg VFX prefab – a PoisonEffect spawnolhatja az ellenségen")]
    public GameObject poisonVfxPrefab;

    [Header("Égés (DoT)")]
    [Tooltip("Ha be van kapcsolva, találatkor égést alkalmaz a célponton")]
    public bool applyBurning = false;
    [Tooltip("Tűzsebzés másodpercenként")]
    public float burnDamagePerSecond = 5f;
    [Tooltip("Égés időtartama másodpercben")]
    public float burnDuration = 5f;
    [Tooltip("Ha be van kapcsolva, a becsapódás körüli ellenségekre is kiterjed az égés")]
    public bool burnIsAOE = false;
    [Tooltip("Égés AOE sugara tile egységben (csak ha burnIsAOE be van kapcsolva)")]
    public float burnAOERadius = 1.5f;
    [Tooltip("Opcionális tűz VFX prefab – a BurningEffect spawnolhatja az ellenségen")]
    public GameObject burnVfxPrefab;

    // ─────────────────────────────────────────────────────────────
    //  LVL 1 – Standard_ArcherSkillTree | Standard_StoneSkillTree
    // ─────────────────────────────────────────────────────────────
    [Header("Lvl1 – Standard_ArcherSkillTree")]
    [Tooltip("Ha be van kapcsolva, találat után csapdát hagy maga után (TrapChance skill)")]
    public bool canPlaceTrap = false;
    [Tooltip("A csapda prefab (Trap komponenssel)")]
    public GameObject trapPrefab;
    [Tooltip("A csapda sebzése (0 = ugyanannyi mint a lövedék)")]
    public float trapDamage = 0f;

    [Header("Lvl1 – Standard_StoneSkillTree")]
    [Tooltip("Ha be van kapcsolva, a DamageBonus skill érvényes erre a lövedékre")]
    public bool stoneDmgBonus = false;
    [Tooltip("Ha be van kapcsolva, az AOERadius skill érvényes erre a lövedékre")]
    public bool stoneAOEBonus = false;

    // ─────────────────────────────────────────────────────────────
    //  LVL 2 – ArcherSkillTree | StoneSkillTree_lvl2
    // ─────────────────────────────────────────────────────────────
    [Header("Lvl2 – ArcherSkillTree")]
    [Tooltip("Ha ki van kapcsolva, ez a lövedék soha nem crit-el (CritHitChance skill)")]
    public bool canCrit = true;
    [Tooltip("Crit esetén a sebzés hány %-a legyen (pl. 200 = dupla sebzés)")]
    public float critDamagePercent = 200f;
    [Tooltip("Ha ki van kapcsolva, ez a lövedék nem csökkenti az ellenség armorját (ArmorPierce skill)")]
    public bool canDestroyArmor = true;
    [Tooltip("Ha ki van kapcsolva, ez a lövedék soha nem vesz részt multi-shot lövésben (MultiShotChance skill)")]
    public bool canMultiShot = true;
    [Tooltip("Multi-shot aktiválásakor hány lövedék induljon a toronyból")]
    public int multiShotCount = 3;
    [Tooltip("Ha be van kapcsolva, az ArcherRange skill bónusz érvényes erre a lövedékre")]
    public bool archerRangeBonus = true;
    [Tooltip("Ha be van kapcsolva, ez a lövedék stun-olhatja a szörnyet (StunChance skill)")]
    public bool canStun = true;
    [Tooltip("Stun időtartama másodpercben")]
    public float stunDuration = 1.5f;

    [Header("Lvl2 – StoneSkillTree_lvl2")]
    [Tooltip("Ha be van kapcsolva, ez a lövedék crit-elhet a Stone Crit Chance Lvl2 skill alapján")]
    public bool stoneCanCrit = false;
    [Tooltip("Ha be van kapcsolva, a FocusAttackSpeed skill érvényes erre a lövedékre")]
    public bool stoneFocusBonus = false;
    [Tooltip("Ha be van kapcsolva, a MagicDamageBonus skill érvényes erre a lövedékre (AOE mágikus sebzés)")]
    public bool stoneMagicAOEDmgBonus = false;
    [Tooltip("Ha be van kapcsolva, a ChargeDamageBonus skill érvényes erre a lövedékre (minél tovább nem lőtt, annál nagyobb az első lövés)")]
    public bool stoneChargeDmgBonus = false;

    // ══════════════════════════════════════════════════════
    //  BELSŐ ÁLLAPOT – Tower.Shoot() tölti fel Initialize()-kor
    // ══════════════════════════════════════════════════════

    private Enemy target;
    private float damage;
    private float aoeDamage;
    private float magicDamage;
    private bool isAreaDamage;
    private float areaRadius;
    public DamageType damageType    = DamageType.Physical;
    private float      critChance          = 0f;
    private float      armorPierce         = 0f;
    private float      stunChance          = 0f;
    private float      trapChance          = 0f;
    private int        _pierceRemaining    = 0;
    private List<Enemy> _pierceHit         = new List<Enemy>();
    public float       javelinActiveCritChance = 0f;

    private bool hasHit = false;
    private float lifetimeTimer = 0f;

    // ══════════════════════════════════════════════════════
    //  INICIALIZÁLÁS
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// Tower.Shoot() hívja közvetlenül spawnolás után.
    /// </summary>
    public void Initialize(Enemy target, float damage, bool isAreaDamage, float areaRadius,
                           DamageType damageType = DamageType.Physical, float critChance = 0f,
                           float armorPierce = 0f, float stunChance = 0f, float trapChance = 0f,
                           float aoeDamage = 0f, float magicDamage = 0f)
    {
        this.target          = target;
        this.damage          = damage;
        this.aoeDamage       = aoeDamage;
        this.magicDamage     = magicDamage;
        this.isAreaDamage    = isAreaDamage;
        this.areaRadius      = areaRadius;
        _pierceRemaining     = pierceCount;
        _pierceHit.Clear();
        this.damageType   = damageType;
        this.critChance   = critChance;
        this.armorPierce  = armorPierce;
        this.stunChance   = stunChance;
        this.trapChance   = trapChance;
    }

    // ══════════════════════════════════════════════════════
    //  MOZGÁS
    // ══════════════════════════════════════════════════════

    void Update()
    {
        if (hasHit) return;

        // Lifetime lejárt → megsemmisülés (elvétett lövés)
        lifetimeTimer += Time.deltaTime;
        if (lifetimeTimer >= maxLifetime)
        {
            Destroy(gameObject);
            return;
        }

        // Ha a célpont meghalt, repüljön tovább előre majd tűnjön el
        if (target == null || target.IsDead)
        {
            transform.position += transform.up * moveSpeed * Time.deltaTime;
            return;
        }

        Vector3 dir  = target.transform.position - transform.position;
        float step   = moveSpeed * Time.deltaTime;

        // Forgás a célpont felé
        if (dir != Vector3.zero)
            transform.up = dir.normalized;

        if (dir.magnitude <= step)
        {
            // Elérte a célt
            transform.position = target.transform.position;
            Impact();
        }
        else
        {
            transform.position += dir.normalized * step;
        }
    }

    // ══════════════════════════════════════════════════════
    //  BECSAPÓDÁS
    // ══════════════════════════════════════════════════════

    bool IsImmune(Enemy enemy)
    {
        if (string.IsNullOrEmpty(projectileTag)) return false;
        bool immune = enemy.immuneToProjectileTags != null &&
                      enemy.immuneToProjectileTags.Contains(projectileTag);
        if (immune) ShowImmuneText(enemy);
        return immune;
    }

    void ShowImmuneText(Enemy enemy)
    {
        if (enemy.floatingDamageTextPrefab == null) return;
        var go = Instantiate(enemy.floatingDamageTextPrefab, enemy.transform.position, Quaternion.identity);
        go.GetComponent<FloatingDamageText>()?.InitializeImmune();
    }

    void Impact()
    {
        if (hasHit) return;
        hasHit = true;

        // Effekt spawnolása és méretezése az AOE sugárhoz
        if (impactEffect != null)
        {
            var effect = Instantiate(impactEffect, transform.position + impactOffset, Quaternion.identity);

            // AOE esetén az effekt mérete igazodik az area radius-hoz
            // → Stone2 torony nagyobb sugara → nagyobb robbanás animáció
            if (isAreaDamage && GridManager.Instance != null)
            {
                float worldRadius = areaRadius * (GridManager.Instance.tileWidth + GridManager.Instance.tileHeight) * 0.5f;
                float scale = worldRadius * 2f; // átmérő = sugár * 2
                effect.transform.localScale = new Vector3(scale, scale, 1f);
            }
        }

        // Sebzés alkalmazása
        if (isAreaDamage && aoeDamage > 0f)
        {
            // Közvetlen találat → damage
            if (target != null && !target.IsDead && !IsImmune(target))
            {
                bool  isCrit      = critChance > 0f && Random.value < critChance;
                float finalDamage = isCrit ? damage * (critDamagePercent / 100f) : damage;
                if (canDestroyArmor && armorPierce > 0f) target.ReduceArmor(armorPierce);
                if (canStun && stunChance > 0f && Random.value < stunChance) { target.ApplyStun(stunDuration); ShowStunText(target); }
                target.TakeDamage(finalDamage, damageType, isCrit);
                if (magicDamage > 0f) target.TakeDamage(magicDamage, DamageType.Magic);
                ApplyBurning(target);
                ApplyPoison(target);
                if (teleportOnHit) target.TeleportToStart();
            }
            // Körülötte lévők → aoeDamage (közvetlen célpont kihagyva)
            ApplyAreaDamage(aoeDamage, target);
        }
        else if (isAreaDamage)
        {
            // Közvetlen találat crit-elhet, a splash ellenségek alap sebzést kapnak
            bool  isCrit      = critChance > 0f && Random.value < critChance;
            float finalDamage = isCrit ? damage * (critDamagePercent / 100f) : damage;

            if (target != null && !target.IsDead && !IsImmune(target))
            {
                if (canDestroyArmor && armorPierce > 0f) target.ReduceArmor(armorPierce);
                if (canStun && stunChance > 0f && Random.value < stunChance) { target.ApplyStun(stunDuration); ShowStunText(target); }
                target.TakeDamage(finalDamage, damageType, isCrit);
                if (magicDamage > 0f) target.TakeDamage(magicDamage, DamageType.Magic);
                ApplyBurning(target);
                ApplyPoison(target);
                if (teleportOnHit) target.TeleportToStart();
            }
            // Splash ellenségek – közvetlen célpont kizárva, alap damage
            ApplyAreaDamage(damage, target);
        }
        else if (target != null && !target.IsDead && !IsImmune(target))
        {
            bool isCrit;
            float finalDamage;
            if (javelinCanCrit && javelinActiveCritChance > 0f)
            {
                isCrit      = Random.value < javelinActiveCritChance;
                finalDamage = isCrit ? damage * (javelinCritDamagePercent / 100f) : damage;
            }
            else
            {
                isCrit      = critChance > 0f && Random.value < critChance;
                finalDamage = isCrit ? damage * (critDamagePercent / 100f) : damage;
            }

            if (canDestroyArmor && armorPierce > 0f)
                target.ReduceArmor(armorPierce);

            if (canStun && stunChance > 0f && Random.value < stunChance) { target.ApplyStun(stunDuration); ShowStunText(target); }

            target.TakeDamage(finalDamage, damageType, isCrit);
            if (magicDamage > 0f) target.TakeDamage(magicDamage, DamageType.Magic);
            ApplyBurning(target);
            ApplyPoison(target);
            ApplyPrismaElement(target);
            ApplyPrismaBounce(target);

            if (teleportOnHit)
                target.TeleportToStart();

            if (canPlaceTrap && trapPrefab != null && trapChance > 0f && Random.value < trapChance)
            {
                var trapGO = Instantiate(trapPrefab, transform.position, Quaternion.identity);
                var trap   = trapGO.GetComponent<Trap>();
                if (trap != null)
                    trap.Initialize(trapDamage > 0f ? trapDamage : damage, damageType, target);
            }
        }

        if (pullOnHit) ApplyPull(target);

        // Pierce: ha van még áthatolás, keresünk új célt
        if (_pierceRemaining > 0 && target != null)
        {
            _pierceHit.Add(target);
            Enemy nextTarget = FindNextPierceTarget();
            if (nextTarget != null)
            {
                _pierceRemaining--;
                target  = nextTarget;
                hasHit  = false;
                return;
            }
        }

        Destroy(gameObject);
    }

    Enemy FindNextPierceTarget()
    {
        Enemy closest = null;
        float closestDist = float.MaxValue;
        foreach (var e in Enemy.AllEnemies)
        {
            if (e == null || e.IsDead) continue;
            if (_pierceHit.Contains(e)) continue;
            float dist = Vector3.Distance(transform.position, e.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest     = e;
            }
        }
        return closest;
    }

    void ApplyPull(Enemy excludeTarget)
    {
        if (GridManager.Instance == null) return;
        float worldRadius = pullRadius *
            (GridManager.Instance.tileWidth + GridManager.Instance.tileHeight) * 0.5f;

        Vector3 center = transform.position;
        foreach (var enemy in Enemy.AllEnemies)
        {
            if (enemy == null || enemy.IsDead) continue;
            if (enemy == excludeTarget) continue;   // közvetlen célpont kihagyása
            if (Vector3.Distance(center, enemy.transform.position) <= worldRadius)
                enemy.ApplyPull(center, pullForce, pullDuration);
        }
    }

    // ══════════════════════════════════════════════════════
    //  TERÜLETI SEBZÉS
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// Becsapódási pont körül areaRadius sugarú körben sebez
    /// minden élő ellenséget.
    /// </summary>
    // ══════════════════════════════════════════════════════
    //  ÉGÉS
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// Ha az applyBurning pipa be van kapcsolva, BurningEffect-et ad az ellenséghez
    /// (vagy frissíti ha már ég).
    /// Ha burnIsAOE be van kapcsolva, a becsapódás körüli összes ellenségre kiterjed.
    /// </summary>
    void ApplyBurning(Enemy directTarget)
    {
        if (!applyBurning) return;

        if (burnIsAOE)
        {
            // AOE égés – minden élő ellenség a sugáron belül
            float worldRadius = burnAOERadius *
                (GridManager.Instance != null
                    ? (GridManager.Instance.tileWidth + GridManager.Instance.tileHeight) * 0.5f
                    : 1f);

            Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
            foreach (var e in enemies)
            {
                if (e == null || e.IsDead) continue;
                if (Vector3.Distance(transform.position, e.transform.position) <= worldRadius)
                    ApplyBurnToEnemy(e);
            }
        }
        else
        {
            // Single-target égés – csak a közvetlen célpont
            if (directTarget == null || directTarget.IsDead) return;
            ApplyBurnToEnemy(directTarget);
        }
    }

    private void ApplyBurnToEnemy(Enemy enemy)
    {
        var existing = enemy.GetComponent<BurningEffect>();
        if (existing != null)
            existing.Refresh(burnDamagePerSecond, burnDuration);
        else
        {
            var burn = enemy.gameObject.AddComponent<BurningEffect>();
            burn.Initialize(burnDamagePerSecond, burnDuration, burnVfxPrefab);
        }
    }

    // ══════════════════════════════════════════════════════
    //  PRISMA ELEMENTÁLIS SEBZÉS
    // ══════════════════════════════════════════════════════

    void ApplyPrismaBounce(Enemy hitEnemy)
    {
        if (!prismaBounce || prismaBounceSkillBonus < 1f) return;
        if (hitEnemy == null || hitEnemy.IsDead) return;
        if (prismaBouncesLeft <= 0) return;

        // Esély: alap + armor × chancePerArmor%, maximum maxChance%
        float bounceChance = Mathf.Min(prismaBounceMaxChance, prismaBounceBaseChance + hitEnemy.armor * prismaBounceChancePerArmor) / 100f;
        if (Random.value > bounceChance) return;

        // Véletlenszerű élő ellenség a találat pontjától range-en belül
        var candidates = new List<Enemy>();
        foreach (var e in Enemy.AllEnemies)
        {
            if (e == null || e.IsDead || e == hitEnemy) continue;
            float dist = Vector3.Distance(hitEnemy.transform.position, e.transform.position);
            if (dist <= prismaBounceRange) candidates.Add(e);
        }

        if (candidates.Count == 0) return;
        Enemy bounceTarget = candidates[Random.Range(0, candidates.Count)];

        if (bounceTarget == null) return;

        // Új lövedék spawnolása – átadjuk a maradék bounce számot
        var go = Instantiate(gameObject, hitEnemy.transform.position, Quaternion.identity);
        var p  = go.GetComponent<Projectile>();
        if (p != null)
        {
            p.prismaBouncesLeft   = prismaBouncesLeft - 1;
            p.prismaBounceSkillBonus = prismaBounceSkillBonus;
            p.prismaBounceRange   = prismaBounceRange;
            p.Initialize(bounceTarget, damage, false, 0f, damageType);
        }
        else
        {
            bounceTarget.TakeDamage(damage, damageType);
        }
    }

    void ApplyPrismaElement(Enemy enemy)
    {
        if (!prismaElementChanger || !prismaElementActive) return;
        if (enemy == null || enemy.IsDead) return;

        float elementDmg = damage * (prismaElementDamagePercent / 100f);

        // Véletlenszerű sebzés típus kiválasztása – csak a típus változik, nincs DoT
        int roll = Random.Range(0, 3);
        DamageType elementType = roll == 0 ? DamageType.Fire
                               : roll == 1 ? DamageType.Poison
                               : DamageType.Physical;

        enemy.TakeDamage(elementDmg, elementType);
    }

    // ══════════════════════════════════════════════════════
    //  STUN SZÖVEG
    // ══════════════════════════════════════════════════════

    void ShowStunText(Enemy enemy)
    {
        if (enemy.floatingDamageTextPrefab == null) return;
        var go = Instantiate(enemy.floatingDamageTextPrefab, enemy.transform.position, Quaternion.identity);
        go.GetComponent<FloatingDamageText>()?.InitializeStun();
    }

    // ══════════════════════════════════════════════════════
    //  MÉREG
    // ══════════════════════════════════════════════════════

    void ApplyPoison(Enemy directTarget)
    {
        if (!applyPoison) return;
        if (directTarget == null || directTarget.IsDead) return;

        var existing = directTarget.GetComponent<PoisonEffect>();
        if (existing != null)
            existing.AddStack(poisonDamagePerSecond, poisonDuration, maxPoisonStacks);
        else
        {
            var poison = directTarget.gameObject.AddComponent<PoisonEffect>();
            poison.Initialize(poisonDamagePerSecond, poisonDuration, maxPoisonStacks, poisonVfxPrefab);
        }
    }

    void ApplyAreaDamage(float dmgAmount, Enemy excludeTarget)
    {
        float worldRadius = areaRadius * (GridManager.Instance.tileWidth + GridManager.Instance.tileHeight) * 0.5f;

        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);

        foreach (var enemy in enemies)
        {
            if (enemy.IsDead) continue;
            if (enemy == excludeTarget) continue;
            if (IsImmune(enemy)) continue;
            float dist = Vector3.Distance(transform.position, enemy.transform.position);
            if (dist <= worldRadius)
            {
                enemy.TakeDamage(dmgAmount, damageType);
                if (teleportOnHit)
                    enemy.TeleportToStart();
            }
        }
    }

}
