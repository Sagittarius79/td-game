using UnityEngine;

/// <summary>
/// Általános fejlesztések – bármely lövedék prefabra rátehető kiegészítő módosító.
/// Minden egyes képesség külön ki-be kapcsolható.
///
/// Használat:
///   1. Húzd rá bármely lövedék prefabra a Projectile mellé
///   2. Kapcsold be a kívánt képességeket és állítsd be az értékeket
///   3. A Projectile.OnEnable/Start alatt automatikusan inicializálódik
/// </summary>
[RequireComponent(typeof(Projectile))]
public class altalanos_fejlesztesek : MonoBehaviour
{
    // ══════════════════════════════════════════════════════════════
    //  GYÚJTÁS (Ignite)
    // ══════════════════════════════════════════════════════════════

    [Header("── Gyújtás ──────────────────────────────────")]
    [Tooltip("Ha be van kapcsolva, a találat égést okoz a célponton")]
    public bool gyujtas = false;

    [HideInInspector] public float gyujtasEsely = 1f;
    [HideInInspector] public float gyutasTalalatSebzes = 0f;
    [HideInInspector] public float gyutasDoTSebzesMpenkent = 5f;
    [HideInInspector] public float gyutasIdotartam = 3f;

    // ══════════════════════════════════════════════════════════════
    //  STUN
    // ══════════════════════════════════════════════════════════════

    [Header("── Stun ─────────────────────────────────────")]
    [Tooltip("Ha be van kapcsolva, a találat stunolja a célpontot (esély alapján)")]
    public bool stun = false;

    [HideInInspector] public float stunEsely = 0.25f;
    [HideInInspector] public float stunIdotartam = 1.5f;
    [HideInInspector] public GameObject stunEffectPrefab;

    // ══════════════════════════════════════════════════════════════
    //  MULTI SHOT
    // ══════════════════════════════════════════════════════════════

    [Header("── Multi Shot ───────────────────────────────")]
    [Tooltip("Ha be van kapcsolva, egyszerre több lövedék indul")]
    public bool multiShot = false;

    [Tooltip("Extra lövedékek különböző célpontokra menjenek (ha van elég élő ellenség a közelben)")]
    public bool multiShotKulonbozoCelpont = true;

    [Tooltip("Keresési sugár az extra célpontokhoz (tile egységben)")]
    public float multiShotKeresesiSugar = 5f;

    [HideInInspector] public float multiShotEsely = 1f;
    [HideInInspector] public int multiShotMennyiseg = 2;

    // ══════════════════════════════════════════════════════════════
    //  PATTANÁS (Bounce)
    // ══════════════════════════════════════════════════════════════

    [Header("── Pattanás ─────────────────────────────────")]
    [Tooltip("Ha be van kapcsolva, a lövedék találat után pattanhat egy másik ellenségre")]
    public bool pattanas = false;

    [HideInInspector] public float pattanasAlapEsely = 20f;
    [HideInInspector] public float pattanasArmorBonusz = 5f;
    [HideInInspector] public float pattanasMaxEsely = 80f;
    [HideInInspector] public int pattanasMaxSzam = 1;
    [HideInInspector] public float pattanasSugar = 4f;

    // belső: hány pattanás maradt még
    [HideInInspector] public int _pattanasMaradt = -1;

    [Header("── Rúna konfiguráció ────────────────────────")]
    [Tooltip("Egyszer beállított asset – minden prefabra ugyanazt húzd rá")]
    public RuneConfig runeConfig;

    // ── belső hivatkozás ──────────────────────────────────────────
    private Projectile _projectile;
    private bool _isExtraProjectile = false;  // megakadályozza a végtelen rekurziót
    private Vector3 _towerPosition;
    private float   _towerRange = -1f;        // -1 = nincs beállítva
    private Tower   _tower;

    /// <summary>Tower.Shoot() hívja közvetlenül spawnolás után.</summary>
    public void SetTowerData(Vector3 towerPosition, float towerRange, Tower tower = null)
    {
        _towerPosition = towerPosition;
        _towerRange    = towerRange;
        _tower         = tower;
    }

    // ══════════════════════════════════════════════════════════════

    void Awake()
    {
        _projectile = GetComponent<Projectile>();
    }

    void Start()
    {
        // Első spawnolásnál inicializáljuk a maradék pattanás számlálót
        if (_pattanasMaradt < 0)
            _pattanasMaradt = pattanasMaxSzam;

        // Ha SetTowerData nem lett meghívva, keressük meg a legközelebbi tornyot
        if (_towerRange < 0f)
        {
            Tower closest = null;
            float minDist = float.MaxValue;
            foreach (var t in Tower.AllTowers)
            {
                if (t == null) continue;
                float d = Vector3.Distance(transform.position, t.transform.position);
                if (d < minDist) { minDist = d; closest = t; }
            }
            if (closest != null)
            {
                _towerPosition = closest.transform.position;
                _towerRange    = closest.attackRange;
                _tower         = closest;
            }
        }

        ApplyRuneBuffs();

        if (multiShot && !_isExtraProjectile && (multiShotEsely >= 1f || Random.value <= multiShotEsely))
            SpawnExtraProjectiles();
    }

    void SpawnExtraProjectiles()
    {
        if (_projectile == null) return;

        // Torony sugara tile → world; ha nincs beállítva, multiShotKeresesiSugar a fallback
        float tileSize = GridManager.Instance != null
            ? (GridManager.Instance.tileWidth + GridManager.Instance.tileHeight) * 0.5f
            : 1f;

        float worldRadius = _towerRange >= 0f
            ? _towerRange * tileSize
            : multiShotKeresesiSugar * tileSize;

        Vector3 origin = _towerRange >= 0f ? _towerPosition : transform.position;

        var candidates = new System.Collections.Generic.List<Enemy>();
        foreach (var e in Enemy.AllEnemies)
        {
            if (e == null || e.IsDead) continue;
            if (Vector3.Distance(origin, e.transform.position) <= worldRadius)
                candidates.Add(e);
        }

        // Az eredeti célpontot kivesszük a listából (ő már megkapja az eredeti lövedéket)
        Enemy originalTarget = _projectile.GetTarget();
        candidates.Remove(originalTarget);

        for (int i = 1; i < multiShotMennyiseg; i++)
        {
            Enemy extraTarget = originalTarget;  // alapból ugyanarra megy

            if (multiShotKulonbozoCelpont && candidates.Count > 0)
            {
                int idx = Random.Range(0, candidates.Count);
                extraTarget = candidates[idx];
                candidates.RemoveAt(idx);  // ne ugyanazt válassza kétszer
            }

            if (extraTarget == null || extraTarget.IsDead) continue;

            // Másolat spawning
            var extra = Instantiate(gameObject, transform.position, transform.rotation);

            // Multi shot kikapcsolása az extra példányon (rekurzió elkerülése)
            var af = extra.GetComponent<altalanos_fejlesztesek>();
            if (af != null) af._isExtraProjectile = true;

            // Ugyanolyan paraméterek, új célpont
            var extraProj = extra.GetComponent<Projectile>();
            if (extraProj != null)
                extraProj.Initialize(
                    extraTarget,
                    _projectile.damage,
                    _projectile.isAreaDamage,
                    _projectile.areaRadius,
                    _projectile.damageType,
                    _projectile.critChance,
                    _projectile.armorPierce,
                    _projectile.stunChance,
                    _projectile.trapChance,
                    _projectile.aoeDamage,
                    _projectile.magicDamage);
        }
    }

    /// <summary>
    /// A Projectile.cs Impact() metódusa hívja ezt, miután saját logikáját lefuttatta.
    /// </summary>
    public void OnHit(Enemy target)
    {
        if (target == null || target.IsDead) return;

        ApplyGyujtas(target);
        ApplyStun(target);
        ApplyMereg(target);
        ApplyPattanas(target);
    }

    // ── Gyújtás ───────────────────────────────────────────────────

    // ══════════════════════════════════════════════════════════════
    //  MÉREG (Poison)
    // ══════════════════════════════════════════════════════════════

    [Header("── Méreg ────────────────────────────────────")]
    [Tooltip("Ha be van kapcsolva, a találat mérgezi a célpontot")]
    public bool mereg = false;

    [HideInInspector] public float meregEsely = 1f;
    [HideInInspector] public float meregSebzesMpenkent = 3f;
    [HideInInspector] public float meregIdotartam = 4f;
    [HideInInspector] public int meregMaxStack = 5;

    // ── Méreg ─────────────────────────────────────────────────────

    void ApplyMereg(Enemy target)
    {
        if (!mereg) return;
        if (meregEsely < 1f && Random.value > meregEsely) return;

        var existing = target.GetComponent<PoisonEffect>();
        if (existing != null)
            existing.AddStack(meregSebzesMpenkent, meregIdotartam, meregMaxStack);
        else
        {
            var poison = target.gameObject.AddComponent<PoisonEffect>();
            poison.Initialize(meregSebzesMpenkent, meregIdotartam, meregMaxStack, target.poisonVfxPrefab);
        }
    }

    // ── Stun ──────────────────────────────────────────────────────

    void ApplyStun(Enemy target)
    {
        if (!stun) return;
        if (Random.value > stunEsely) return;

        if (stunEffectPrefab != null)
            target.stunEffectPrefab = stunEffectPrefab;

        target.ApplyStun(stunIdotartam);

        // Floating "STUN" szöveg megjelenítése
        if (target.floatingDamageTextPrefab != null)
        {
            var go = Instantiate(target.floatingDamageTextPrefab, target.transform.position, Quaternion.identity);
            go.GetComponent<FloatingDamageText>()?.InitializeStun();
        }
    }

    // ── Pattanás ──────────────────────────────────────────────────

    void ApplyPattanas(Enemy hitEnemy)
    {
        if (!pattanas) return;
        if (_pattanasMaradt <= 0) return;
        if (_projectile == null) return;

        // Esély: alap + armor × bonusz%, max cap
        float esely = Mathf.Min(pattanasMaxEsely,
            pattanasAlapEsely + hitEnemy.armor * pattanasArmorBonusz) / 100f;
        if (Random.value > esely) return;

        // Keresési sugár tile → world
        float worldRadius = pattanasSugar *
            (GridManager.Instance != null
                ? (GridManager.Instance.tileWidth + GridManager.Instance.tileHeight) * 0.5f
                : 1f);

        // Véletlenszerű célpont a sugáron belül (nem az épp eltalált)
        var candidates = new System.Collections.Generic.List<Enemy>();
        foreach (var e in Enemy.AllEnemies)
        {
            if (e == null || e.IsDead || e == hitEnemy) continue;
            if (Vector3.Distance(hitEnemy.transform.position, e.transform.position) <= worldRadius)
                candidates.Add(e);
        }

        if (candidates.Count == 0) return;
        Enemy bounceTarget = candidates[Random.Range(0, candidates.Count)];

        // Új lövedék spawning a találat pontjából
        var go = Instantiate(gameObject, hitEnemy.transform.position, Quaternion.identity);

        // Pattanás számláló csökkentése az új példányon
        var af = go.GetComponent<altalanos_fejlesztesek>();
        if (af != null)
        {
            af._pattanasMaradt    = _pattanasMaradt - 1;
            af._isExtraProjectile = true;   // ne indítson multi shot-ot
        }

        var p = go.GetComponent<Projectile>();
        if (p != null)
            p.Initialize(bounceTarget, _projectile.damage, false, 0f, _projectile.damageType);
        else
            bounceTarget.TakeDamage(_projectile.damage, _projectile.damageType);
    }

    // ── Rúna buffok alkalmazása ───────────────────────────────────

    void ApplyRuneBuffs()
    {
        if (runeConfig == null) return;

        // ── Alap értékek felülírása RuneConfig-ból ────────────────
        gyujtasEsely            = runeConfig.gyujtasEsely;
        gyutasTalalatSebzes     = runeConfig.gyujtasTalalatSebzes;
        gyutasDoTSebzesMpenkent = runeConfig.gyujtasDoTAlap;
        gyutasIdotartam         = runeConfig.gyujtasIdotartamAlap;

        stunEsely               = runeConfig.stunEselyAlap;
        stunIdotartam           = runeConfig.stunIdotartamAlap;
        if (runeConfig.stunVfxPrefab != null)
            stunEffectPrefab    = runeConfig.stunVfxPrefab;

        multiShotEsely          = runeConfig.multiShotEsely;
        multiShotMennyiseg      = runeConfig.multiShotMennyisegAlap;

        pattanasAlapEsely       = runeConfig.pattanasEselyAlap;
        pattanasMaxEsely        = runeConfig.pattanasMaxEsely;
        pattanasMaxSzam         = runeConfig.pattanasSzamAlap;
        pattanasArmorBonusz     = runeConfig.pattanasArmorBonusz;
        pattanasSugar           = runeConfig.pattanasSugar;

        meregEsely              = runeConfig.meregEsely;
        meregSebzesMpenkent     = runeConfig.meregDoTAlap;
        meregIdotartam          = runeConfig.meregIdotartamAlap;
        meregMaxStack           = runeConfig.meregMaxStackAlap;

        if (_tower == null) return;

        // ── Stack bónuszok hozzáadása (csak a torony saját stackjei) ──
        int s;

        s = _tower.GetRuneStack(runeConfig.gyujtasRunePrefab);
        if (s > 0)
        {
            gyujtas                  = true;
            gyutasDoTSebzesMpenkent += runeConfig.gyujtasDoTBonusz * s;
            gyutasIdotartam         += runeConfig.gyujtasIdotartamBonusz * s;
        }

        s = _tower.GetRuneStack(runeConfig.stunRunePrefab);
        if (s > 0)
        {
            stun          = true;
            stunEsely     = Mathf.Min(1f, stunEsely + runeConfig.stunEselyBonusz * s);
            stunIdotartam += runeConfig.stunIdotartamBonusz * s;
        }

        s = _tower.GetRuneStack(runeConfig.multiShotRunePrefab);
        if (s > 0)
        {
            multiShot          = true;
            multiShotMennyiseg += runeConfig.multiShotMennyisegBonusz * s;
        }

        s = _tower.GetRuneStack(runeConfig.pattanasRunePrefab);
        if (s > 0)
        {
            pattanas          = true;
            pattanasMaxSzam   += runeConfig.pattanasSzamBonusz * s;
            pattanasAlapEsely  = Mathf.Min(pattanasMaxEsely, pattanasAlapEsely + runeConfig.pattanasEselyBonusz * s);
        }

        s = _tower.GetRuneStack(runeConfig.meregRunePrefab);
        if (s > 0)
        {
            mereg                = true;
            meregSebzesMpenkent += runeConfig.meregDoTBonusz * s;
            meregMaxStack       += runeConfig.meregStackBonusz * s;
        }
    }

    // ── Gyújtás ───────────────────────────────────────────────────

    void ApplyGyujtas(Enemy target)
    {
        if (!gyujtas) return;
        if (gyujtasEsely < 1f && Random.value > gyujtasEsely) return;

        // Közvetlen találati tűzsebzés
        if (gyutasTalalatSebzes > 0f)
            target.TakeDamage(gyutasTalalatSebzes, DamageType.Fire);

        // DoT: BurningEffect hozzáadása vagy frissítése
        if (gyutasDoTSebzesMpenkent > 0f)
        {
            var existing = target.GetComponent<BurningEffect>();
            if (existing != null)
                existing.Refresh(gyutasDoTSebzesMpenkent, gyutasIdotartam);
            else
            {
                var burn = target.gameObject.AddComponent<BurningEffect>();
                burn.Initialize(gyutasDoTSebzesMpenkent, gyutasIdotartam, target.fireVfxPrefab);
            }
        }
    }
}
