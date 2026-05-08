using UnityEngine;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// ═══════════════════════════════════════════════════════
///  TORONY ALAP OSZTÁLY (absztrakt)
///  Az ArcherTower, StoneThrower és MagicTower ebből örököl.
///
///  Felelősségek:
///   - Célzás: legközelebb lévő ellenség a kastályhoz
///   - Lövési sebesség kezelése (attackSpeed)
///   - Lövedék spawnolása és inicializálása
///   - Sebzés típus (Physical / Magic) továbbítása
///   - Hatótávolság kör megjelenítése
///   - Izometrikus sorting
///
///  Új torony hozzáadása:
///   1. Hozz létre új osztályt ami Tower-ből örököl
///   2. Override-old a Shoot() metódust ha speciális viselkedés kell
///   3. Állítsd be az Inspector mezőket a prefabon
/// ═══════════════════════════════════════════════════════
/// </summary>
public enum TargetingMode
{
    Alapertelmezett,        // első megtalált élő ellenség a hatótávon belül
    LegkozelabbACastlyhoz,  // legközelebb a kastályhoz
    LegkisebbHP,            // legkevesebb életpontja van
    LegnagyobbHP,           // legtöbb életpontja van
    LegkozelebbiSzorny,     // legközelebb van a toronyhoz
    LegtavolabbiSzorny,     // legtávolabb van a toronyhoz
    Leggyorsabb             // legnagyobb moveSpeed
}

public abstract class Tower : MonoBehaviour
{
    // ══════════════════════════════════════════════════════
    //  INSPECTOR MEZŐK
    // ══════════════════════════════════════════════════════

    [Header("Torony alap értékek")]
    public string towerName = "Torony";
    public int goldCost = 10;

    [Header("Harc értékek")]
    [Tooltip("Lerakáskor mennyivel tolódjon fel (izometrikus igazítás)")]
    public float placementOffsetY = 0.3f;
    [Tooltip("Hatótávolság grid cellában mérve")]
    public float attackRange = 3.5f;
    [Tooltip("Lövés/másodperc")]
    public float attackSpeed = 1f;
    public float damage = 10f;
    [Tooltip("Ha be van kapcsolva, a lövedék becsapódáskor területi sebzést okoz")]
    public bool isAreaDamage = false;
    [Tooltip("AOE sugár – csak területi sebzésnél használt")]
    public float areaRadius = 1.5f;
    [Tooltip("AOE sebzés – a körülötte lévő ellenségek ezt kapják. Ha 0, ugyanannyi mint a Damage.")]
    public float aoeDamage = 0f;
    [Tooltip("Physical → az ellenség Armor értéke csökkenti. Magic → MagicResist csökkenti.")]
    public DamageType damageType = DamageType.Physical;

    [Tooltip("Melyik ellenséget válassza célpontnak a hatótávon belül.")]
    public TargetingMode targetingMode = TargetingMode.Alapertelmezett;

    [Header("Prefab-ok")]
    public GameObject projectilePrefab;
    [Tooltip("A lövedék kiindulási pontja (ha üres, a torony közepéről indul)")]
    public Transform shootPoint;

    [Header("Életerő")]
    [Tooltip("Torony maximális életereje")]
    public float maxHealth = 100f;
    [Tooltip("Fizikai sebzés csökkentője")]
    public float armor = 1f;
    [Tooltip("Mágikus sebzés csökkentője")]
    public float magicResist = 1f;

    [Header("Szint")]
    [Tooltip("Ha be van kapcsolva, ez egy Lvl2-es épület – dekorált cellára is lerakható.\n" +
             "Ha ki van kapcsolva (Lvl1), dekorált cellára nem lehet lerakni.")]
    public bool isLvl2 = false;

    [Header("Skill Tree árhatás")]
    [Tooltip("Ha meg van adva, minden ebbe a fába elköltött skill pont 1%-al emeli a torony árát.")]
    public SkillTreeDefinition priceSkillTree = null;

    [Header("Vizuális")]
    public SpriteRenderer towerRenderer;
    [Tooltip("Hatótávolság jelző kör (opcionális) – kiválasztáskor jelenik meg")]
    public GameObject rangeCircle;
    [Tooltip("Ha be van kapcsolva, a torony sprite-ja tükröződik az ellenség irányába")]
    public bool flipToFaceTarget = false;
    [Tooltip("HP sáv gyökér objektuma – teljes HP-nál rejtve van")]
    public GameObject healthBarRoot;
    [Tooltip("HP sáv kitöltés Image komponense")]
    public UnityEngine.UI.Image healthBarFill;
    [Tooltip("HP szöveg – kiválasztáskor jelenik meg (opcionális)")]
    public TextMeshProUGUI hpText;

    [Header("Töltés sáv")]
    [Tooltip("Töltés sáv Image komponense (Filled típus) – mutatja mikor tölt a következő lövésre")]
    public UnityEngine.UI.Image chargeBarFill;

    // ══════════════════════════════════════════════════════
    //  BELSŐ ÁLLAPOT
    // ══════════════════════════════════════════════════════

    protected Enemy      currentTarget;
    protected float      attackTimer = 0f;
    protected float      _idleTimer  = 0f;   // idő az utolsó lövés óta (Charge bónuszhoz)
    protected Vector2Int gridCell;

    private float _currentHealth;
    private bool  _isSelected = false;
    public float CurrentHealth => _currentHealth;
    public float HealthPercent => _currentHealth / maxHealth;
    public bool  IsDead        => _currentHealth <= 0f;

    // ── Statikus lista: összes torony (lekérdezéshez) ─────
    private static List<Tower> allTowers = new List<Tower>();
    public static IReadOnlyList<Tower> AllTowers => allTowers;

    void OnEnable()
    {
        allTowers.Add(this);

        // BuildingHPExtra skill bónusz hozzáadása a max HP-hoz
        var buildingsCfg = BuildingsConfig.Instance ?? FindObjectOfType<BuildingsConfig>();
        if (buildingsCfg != null && buildingsCfg.buildingsSkillTree != null &&
            UserProgressManager.Instance != null)
        {
            float bonus = UserProgressManager.Instance.GetTotalSkillEffect(
                SkillEffectType.BuildingHPExtra, buildingsCfg.buildingsSkillTree);
            maxHealth += bonus;
        }

        _currentHealth = maxHealth;
        if (healthBarRoot != null)
        {
            var canvas = healthBarRoot.GetComponent<Canvas>();
            if (canvas != null)
                canvas.sortingOrder = 100;
        }
        RefreshHpBarVisibility();
    }

    void OnDisable() => allTowers.Remove(this);

    // ══════════════════════════════════════════════════════
    //  LERAKÁS
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// Lerak egy tornyot a megadott grid cellára.
    /// BuildMenuUI hívja lerakáskor.
    /// </summary>
    public void PlaceAt(Vector2Int cell)
    {
        gridCell = cell;
        Vector3 basePos = GridManager.Instance.GridToWorld(cell);
        transform.position = basePos + new Vector3(0f, placementOffsetY, 0f);

        // Izometrikus sorting beállítása
        if (towerRenderer != null)
        {
            int order = GridManager.Instance.GetSortingOrder(cell.x, cell.y);
            towerRenderer.sortingOrder = order;

            // HP sáv Canvas mindig a sprite fölé kerül
            if (healthBarRoot != null)
            {
                var canvas = healthBarRoot.GetComponent<Canvas>();
                if (canvas != null)
                    canvas.sortingOrder = order + 1;
            }

            // Charge sáv Canvas mindig az épületek fölött
            if (chargeBarFill != null)
            {
                var canvas = chargeBarFill.GetComponentInParent<Canvas>();
                if (canvas != null)
                    canvas.sortingOrder = order + 2;
            }
        }

        GridManager.Instance.SetOccupied(cell);

        UpdateRangeCircleSize();
        if (rangeCircle != null)
            rangeCircle.SetActive(false);
    }

    // ══════════════════════════════════════════════════════
    //  ÉLETERŐ
    // ══════════════════════════════════════════════════════

    public void TakeDamage(float amount, DamageType type = DamageType.Physical)
    {
        if (IsDead) return;

        float resistance = type == DamageType.Physical ? armor : magicResist;
        float actual     = Mathf.Max(0f, amount - resistance);

        _currentHealth -= actual;
        _currentHealth  = Mathf.Max(0f, _currentHealth);

        UpdateHealthBar();

        if (_currentHealth <= 0f)
        {
            GridManager.Instance?.PlaceRubble(gridCell);
            Destroy(gameObject);
        }
    }

    public void Heal(float amount)
    {
        if (IsDead) return;
        _currentHealth = Mathf.Min(maxHealth, _currentHealth + amount);
        UpdateHealthBar();
    }

    void UpdateHealthBar()
    {
        if (healthBarFill != null)
            healthBarFill.fillAmount = Mathf.Clamp01(_currentHealth / maxHealth);
        RefreshHpBarVisibility();
        if (hpText != null && hpText.gameObject.activeSelf)
            hpText.text = $"{Mathf.CeilToInt(_currentHealth)} / {Mathf.CeilToInt(maxHealth)}";
    }

    void UpdateChargeBar()
    {
        if (chargeBarFill == null) return;
        float interval = 1f / GetEffectiveAttackSpeed();
        chargeBarFill.fillAmount = Mathf.Clamp01(attackTimer / interval);
    }

    /// <summary>
    /// HP sáv láthatóságát frissíti a GameSettings beállítás alapján.
    /// Ha "Always Visible" be van kapcsolva → mindig látszik.
    /// Ha ki van kapcsolva → csak kiválasztáskor (ShowRange) látszik.
    /// </summary>
    public void RefreshHpBarVisibility()
    {
        if (healthBarRoot == null) return;
        bool alwaysVisible = GameSettingsUI.TowerHpAlwaysVisible;
        healthBarRoot.SetActive(alwaysVisible || _isSelected);
    }

    /// <summary>
    /// A hatótávolság kör méretét igazítja az attackRange értékéhez.
    /// </summary>
    void UpdateRangeCircleSize()
    {
        if (rangeCircle == null || GridManager.Instance == null) return;

        var sr = rangeCircle.GetComponent<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return;

        // Hatótáv world unitban
        float tileSize   = (GridManager.Instance.tileWidth + GridManager.Instance.tileHeight) * 0.5f;
        float diameter   = GetEffectiveRange() * tileSize * 2f;
        float spriteSize = sr.sprite.bounds.size.x;

        // Szülő scale figyelembe vétele
        Vector3 parentScale = transform.lossyScale;
        rangeCircle.transform.localScale = new Vector3(
            (diameter / spriteSize) / parentScale.x,
            (diameter / spriteSize) / parentScale.y,
            1f);
    }

    // ══════════════════════════════════════════════════════
    //  UPDATE – célzás és lövés
    // ══════════════════════════════════════════════════════

    protected virtual void Update()
    {
        if (GameManager.Instance.IsGameOver) return;

        // Célpont frissítése – ha nincs, meghalt, vagy kiment a hatótávból
        if (currentTarget == null || currentTarget.IsDead || !IsInRange(currentTarget))
            currentTarget = FindBestTarget();
        else if (targetingMode != TargetingMode.Alapertelmezett)
            currentTarget = FindBestTarget();

        // Az időzítő folyamatosan telik, célponttól függetlenül
        attackTimer += Time.deltaTime;
        UpdateChargeBar();

        // Nincs célpont → idle timer nő, lövés nem történik
        if (currentTarget == null)
        {
            _idleTimer += Time.deltaTime;
            return;
        }

        // Van célpont → ha eltelt az idő, azonnal lő és újratölti
        FaceTarget(currentTarget.transform.position);

        if (attackTimer >= 1f / GetEffectiveAttackSpeed())
        {
            attackTimer = 0f;
            Shoot();
            _idleTimer = 0f;
        }
    }

    // ══════════════════════════════════════════════════════
    //  FORGÁS ZÁROLÁS
    //  LateUpdate-ban fut → felülír minden más rotációt
    //  (izometrikus nézetben a torony sosem forog, csak flipX)
    // ══════════════════════════════════════════════════════

    void LateUpdate()
    {
        transform.rotation = Quaternion.identity;
        foreach (Transform child in transform)
            child.localRotation = Quaternion.identity;
    }

    // ══════════════════════════════════════════════════════
    //  CÉLZÁS
    // ══════════════════════════════════════════════════════

    /// <summary>Felülírható: skill bónuszokkal növelt hatótávolság.</summary>
    protected virtual float GetEffectiveRange()       => attackRange;
    protected virtual float GetEffectiveAttackSpeed() => attackSpeed;
    protected virtual float GetEffectiveDamage()      => damage;

    /// <summary>
    /// Felülírható: az idle idő alapján számított extra sebzés.
    /// Alap: 0. Override-old a toronyban ha ChargeDamageBonus skill van.
    /// Képlet: _idleTimer * (skill pontok × effectValuePerLevel)
    /// </summary>
    protected virtual float GetChargeDamageBonus()    => 0f;

    protected bool IsInRange(Enemy enemy)
    {
        float rangeWorld = GetEffectiveRange() * (GridManager.Instance.tileWidth + GridManager.Instance.tileHeight) * 0.5f;
        return Vector3.Distance(transform.position, enemy.transform.position) <= rangeWorld;
    }

    /// <summary>
    /// A targetingMode alapján választja ki a legjobb célpontot
    /// a hatótávolságon belüli élő ellenségek közül.
    /// </summary>
    protected Enemy FindBestTarget()
    {
        float rangeWorld = GetEffectiveRange() * (GridManager.Instance.tileWidth + GridManager.Instance.tileHeight) * 0.5f;

        // Meghatározzuk, hogy ez a torony tud-e repülőt célozni (projektil alapján)
        bool projCanHitFlying = projectilePrefab != null &&
                                projectilePrefab.GetComponent<Projectile>() is Projectile p &&
                                p.canHitFlying;

        // Alapértelmezett: az első hatótávon belüli élő ellenség
        if (targetingMode == TargetingMode.Alapertelmezett)
        {
            foreach (var enemy in Enemy.AllEnemies)
            {
                if (enemy.IsDead) continue;
                if (enemy.isFlying && !projCanHitFlying) continue;
                if (Vector3.Distance(transform.position, enemy.transform.position) <= rangeWorld)
                    return enemy;
            }
            return null;
        }

        Enemy best      = null;
        float bestValue = 0f;

        foreach (var enemy in Enemy.AllEnemies)
        {
            if (enemy.IsDead) continue;
            if (enemy.isFlying && !projCanHitFlying) continue;
            float distToTower = Vector3.Distance(transform.position, enemy.transform.position);
            if (distToTower > rangeWorld) continue;

            float value = GetTargetingValue(enemy, distToTower);

            if (best == null || IsBetterValue(value, bestValue))
            {
                best      = enemy;
                bestValue = value;
            }
        }
        return best;
    }

    /// <summary>
    /// Az adott célzási mód szerint kiszámít egy értéket az ellenségre.
    /// A kisebb érték = jobb célpont (minden módban).
    /// </summary>
    float GetTargetingValue(Enemy enemy, float distToTower)
    {
        switch (targetingMode)
        {
            case TargetingMode.LegkozelabbACastlyhoz: return  enemy.DistanceToCastle;
            case TargetingMode.LegkisebbHP:           return  enemy.CurrentHealth;
            case TargetingMode.LegnagyobbHP:          return -enemy.CurrentHealth;
            case TargetingMode.LegkozelebbiSzorny:    return  distToTower;
            case TargetingMode.LegtavolabbiSzorny:    return -distToTower;
            case TargetingMode.Leggyorsabb:           return -enemy.moveSpeed;
            default:                                  return  enemy.DistanceToCastle;
        }
    }

    /// <summary>Kisebb érték = jobb célpont.</summary>
    bool IsBetterValue(float newValue, float currentBest) => newValue < currentBest;

    protected void FaceTarget(Vector3 targetPos)
    {
        if (towerRenderer == null || !flipToFaceTarget) return;
        towerRenderer.flipX = targetPos.x < transform.position.x;
    }

    // ══════════════════════════════════════════════════════
    //  LÖVÉS – leszármazottak felülírhatják
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// Lövedéket spawnol és inicializálja a célponttal, sebzéssel,
    /// AOE adatokkal és sebzés típussal.
    /// </summary>
    protected virtual void Shoot()
    {
        if (projectilePrefab == null || currentTarget == null) return;

        Vector3 origin = shootPoint != null ? shootPoint.position : transform.position;
        var proj = Instantiate(projectilePrefab, origin, Quaternion.identity);
        var p = proj.GetComponent<Projectile>();
        if (p != null)
            p.Initialize(currentTarget, GetEffectiveDamage(), isAreaDamage, areaRadius, damageType);
    }

    // ══════════════════════════════════════════════════════
    //  HATÓTÁVOLSÁG MEGJELENÍTÉS
    // ══════════════════════════════════════════════════════

    public void ShowRange(bool show)
    {
        _isSelected = show;
        if (rangeCircle != null) rangeCircle.SetActive(show);

        RefreshHpBarVisibility();

        if (hpText != null)
        {
            hpText.gameObject.SetActive(show);
            if (show) hpText.text = $"{Mathf.CeilToInt(_currentHealth)} / {Mathf.CeilToInt(maxHealth)}";
        }
    }

    // ══════════════════════════════════════════════════════
    //  PUBLIKUS LEKÉRDEZÉSEK
    // ══════════════════════════════════════════════════════

    public Vector2Int GridCell => gridCell;
}
