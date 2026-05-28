using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// ═══════════════════════════════════════════════════════
///  ELLENSÉG ALAP OSZTÁLY
///  Minden szörny ebből örököl (Ork, OrkLvl2, OrkLvl3, Farkas stb.)
///
///  Felelősségek:
///   - Waypoint alapú mozgás az úton
///   - Véletlenszerű X/Y eltolás és sebességvariancia (WaveManagerből)
///   - Sebzés fogadása armor/magicResist figyelembevételével
///   - Lebegő sebzés szám megjelenítése találatkor
///   - Kastély elérése → kastély sebzése
///   - Izometrikus mélység sorting
///
///  Bővítés új szörny típussal:
///   1. Hozz létre új prefabot
///   2. Állítsd be: maxHealth, moveSpeed, armor, magicResist
///   3. Kösd be: spriteRenderer, floatingDamageTextPrefab
/// ═══════════════════════════════════════════════════════
/// </summary>
public class Enemy : MonoBehaviour
{
    // ══════════════════════════════════════════════════════
    //  INSPECTOR MEZŐK
    // ══════════════════════════════════════════════════════

    [Header("Alap értékek")]
    public float maxHealth = 30f;
    public float moveSpeed = 0.156f;
    public int damage = 0;

    /// <summary>
    /// Külső rendszerek (pl. RainEventManager) által állítható sebességszorzó.
    /// 1.0 = normál, 1.2 = 20%-kal gyorsabb. Mindig visszaállítandó 1.0-ra az event végén.
    /// </summary>
    [HideInInspector] public float externalSpeedMultiplier = 1f;

    [Header("Immunitás")]
    [Tooltip("Ezekre a lövedék tagekre immunis a szörny (pl. Arrow, Magic, Poison)")]
    public List<string> immuneToProjectileTags = new List<string>();

    [Header("Védelmi értékek")]
    [Tooltip("Fizikai sebzés csökkentése. Pl. 2 armor → 10 fizikai sebzésből 8 lesz. Minimum 1 sebzés mindig átmegy.")]
    public float armor = 0f;
    [Tooltip("Mágikus sebzés csökkentése. Ugyanúgy működik mint az armor, de mágikus támadások ellen.")]
    public float magicResist = 0f;
    [Tooltip("Ha be van pipálva, a lény minden sebzésből csak a felét kapja meg (armor/magicResist után alkalmazva).")]
    public bool halfDamage = false;

    [Header("Sebezhetőség")]
    [Tooltip("Ha egy sebzés típus szerepel itt, az adott típusú sebzés meg lesz szorozva a megadott értékkel. Pl. Fire × 2.0 = kétszeres tűzsebzés.")]
    public List<DamageVulnerability> vulnerabilities = new List<DamageVulnerability>();

    [Header("Vizuális")]
    public SpriteRenderer spriteRenderer;
    public GameObject deathEffect;
    public GameObject healthBarRoot;

    [Header("Halál animáció")]
    [Tooltip("Halál animáció trigger neve az Animator-ban (ha üres, nem vár animációra)")]
    public string deathTrigger = "";
    [Tooltip("Halál animáció state tag-je (Animatorban a state-en kell lennie). Ha üres, fix időt vár.")]
    public string deathStateTag = "death";
    [Tooltip("Maximum várakozási idő a halál animációra (másodperc)")]
    public float deathAnimTimeout = 3f;

    [Header("Stun vizuális")]
    [Tooltip("Szín amire vált stun alatt (pl. kék/lila)")]
    public Color stunColor = new Color(0.4f, 0.6f, 1f);
    [Tooltip("Opcionális: stun effekt prefab (pl. csillagok)")]
    public GameObject stunEffectPrefab;
    [Tooltip("A stun VFX pozíció eltolása a sprite tetejéhez képest")]
    public Vector3 stunVfxOffset = Vector3.zero;
    [Tooltip("A stun VFX méretének szorzója (1 = sprite szélességéhez igazodik)")]
    public float stunVfxScale = 1f;

    [Header("Lebegő sebzés szám")]
    [Tooltip("A FloatingDamageText prefab – ha üres, nem jelenik meg sebzés szám")]
    public GameObject floatingDamageTextPrefab;
    public UnityEngine.UI.Image healthBarFill;
    [Tooltip("TextMeshPro szöveg a HP kijelzéséhez (opcionális)")]
    public TextMeshPro hpText;

    [Header("Poison VFX")]
    public GameObject poisonVfxPrefab;
    [Tooltip("A méreg VFX pozíció eltolása a sprite tetejéhez képest")]
    public Vector3 poisonVfxOffset = Vector3.zero;
    [Tooltip("A méreg VFX méretének szorzója (1 = sprite szélességéhez igazodik)")]
    public float poisonVfxScale = 1f;

    [Header("Fire VFX")]
    public GameObject fireVfxPrefab;
    [Tooltip("A tűz VFX pozíció eltolása a sprite tetejéhez képest")]
    public Vector3 fireVfxOffset = Vector3.zero;
    [Tooltip("A tűz VFX méretének szorzója (1 = sprite szélességéhez igazodik)")]
    public float fireVfxScale = 1f;


    [Header("Mozgás típusa")]
    [Tooltip("Ha be van kapcsolva, a lény repül (Air). Ha ki van kapcsolva, a földön jár (Ground).")]
    public bool isFlying = false;

    [Header("Robbanás")]
    [Tooltip("Ha be van kapcsolva, halálkor felrobban és AOE sebzést okoz a közeli tornyoknak")]
    public bool canExplode = false;
    [Tooltip("Robbanás sugara (world egységben)")]
    public float explosionRadius = 2f;
    [Tooltip("Robbanás sebzése a tornyoknak")]
    public float explosionDamage = 30f;
    [Tooltip("Robbanás sebzés típusa")]
    public DamageType explosionDamageType = DamageType.Physical;
    [Tooltip("Robbanás animáció trigger neve az Animator-ban (ha üres, nem vár animációra)")]
    public string explosionTrigger = "explode";
    [Tooltip("Robbanás vizuális effekt prefab (opcionális)")]
    public GameObject explosionEffect;

    // ══════════════════════════════════════════════════════
    //  ESEMÉNYEK – más scriptek feliratkozhatnak rájuk
    // ══════════════════════════════════════════════════════

    public System.Action<Enemy> OnDied;
    public System.Action<Enemy> OnReachedCastle;

    // ══════════════════════════════════════════════════════
    //  STATIKUS LISTA – összes élő ellenség nyomon követése
    //  (tornyok célzáshoz használják)
    // ══════════════════════════════════════════════════════

    private static List<Enemy> allEnemies = new List<Enemy>();
    public static IReadOnlyList<Enemy> AllEnemies => allEnemies;

    // ── Focus target (kattintásra elsődleges célpont) ───────────────
    private static Enemy _focusTarget;
    public  static Enemy FocusTarget => _focusTarget;

    [Header("Focus jelzés")]
    [Tooltip("Opcionális jelző GameObject (pl. keret/nyíl) – fokuszált állapotban aktív")]
    public GameObject focusIndicator;

    /// <summary>Új célpont kijelölése (csak nem-null). TowerSelector hívja.</summary>
    public static void SetFocusTarget(Enemy enemy)
    {
        if (enemy == null) return;
        if (_focusTarget != null && _focusTarget != enemy)
            _focusTarget.SetFocusVisual(false);
        _focusTarget = enemy;
        _focusTarget.SetFocusVisual(true);
    }

    /// <summary>Focus törlése – kastély elérés, vagy Target Tower lerombolása esetén.</summary>
    public static void ClearFocusTarget()
    {
        if (_focusTarget != null)
            _focusTarget.SetFocusVisual(false);
        _focusTarget = null;
    }


    void SetFocusVisual(bool focused)
    {
        if (focusIndicator != null)
            focusIndicator.SetActive(focused);
    }

    void OnEnable()  => allEnemies.Add(this);
    void OnDisable() => allEnemies.Remove(this);

    // ══════════════════════════════════════════════════════
    //  BELSŐ ÁLLAPOT
    // ══════════════════════════════════════════════════════

    private float currentHealth;
    private int currentWaypointIndex = 0;
    public  int CurrentWaypointIndex => currentWaypointIndex;

    /// <summary>SplitOnDeath állítja be – az anya szörny waypoint indexéről indul.</summary>
    [HideInInspector] public int overrideStartWaypointIndex = -1;

    /// <summary>
    /// Ha true, a szörny nem mozog és nem kerül pályakezdőpontra (pl. PhoenixEgg).
    /// Beállítandó az Instantiate() után, Start() lefutása előtt.
    /// </summary>
    [HideInInspector] public bool isStationary = false;
    protected bool isDead = false;
    private bool reachedCastle = false;
    private Vector3[] worldWaypoints;   // az út waypoint-jai world koordinátában (eltolással)

    private float _stunTimer = 0f;

    // ── Pull (behúzás) ──────────────────────────────────────────
    private Vector3 _pullCenter;
    private float   _pullForce;
    private float   _pullTimer = 0f;

    // ── HP bar méretezés távolság alapján ───────────────────────
    private float     _spawnDistToCastle  = 0f;
    private Transform _hpBarContainer     = null;   // healthBarFill szülője – ezt skálázzuk
    private Vector3   _hpBarOriginalScale = Vector3.one;
    private Color _originalColor;
    private GameObject _stunEffectInstance;
    public bool IsStunned => _stunTimer > 0f;

    /// <summary>
    /// Annak a játékosnak a clientId-ja, aki ezt a szörnyet küldte (PvP).
    /// ulong.MaxValue = nem küldött szörny (normál hullám).
    /// WaveManager állítja be spawnoláskor.
    /// </summary>
    public ulong senderClientId = ulong.MaxValue;

    /// <summary>Szörny típus neve (prefab neve) – WaveManager állítja be spawnoláskor, kill statisztikához.</summary>
    public string enemyTypeName = "";

    /// <summary>Ha false, a halál nem számít bele a kill statisztikába (pl. SplitOnDeath gyerekek).</summary>
    [HideInInspector] public bool countAsKill = true;
    /// <summary>Ha false, beérve a kastélyhoz nem sebzi azt (pl. GoblinEvent).</summary>
    [HideInInspector] public bool dealsCastleDamage = true;
    private float _spawnTime;

    // ══════════════════════════════════════════════════════
    //  INICIALIZÁLÁS
    // ══════════════════════════════════════════════════════

    void Awake()
    {
        currentHealth = maxHealth;
        _spawnTime    = Time.time;
    }

    void Start()
    {
        if (spriteRenderer != null)
            _originalColor = spriteRenderer.color;

        // ── Stacionárius mód (pl. PhoenixEgg) ────────────────────────
        if (isStationary)
        {
            if (healthBarRoot != null)
            {
                healthBarRoot.SetActive(true);
                _spawnDistToCastle = Castle.Instance != null
                    ? Vector3.Distance(transform.position, Castle.Instance.transform.position)
                    : 1f;
                if (healthBarFill != null && healthBarFill.transform.parent != null)
                {
                    _hpBarContainer     = healthBarFill.transform.parent;
                    _hpBarOriginalScale = _hpBarContainer.localScale;
                }
                else
                {
                    _hpBarContainer     = healthBarRoot.transform;
                    _hpBarOriginalScale = healthBarRoot.transform.localScale;
                }
            }
            if (hpText != null) hpText.gameObject.SetActive(false);
            UpdateSortingOrder();
            return;
        }
        // ─────────────────────────────────────────────────────────────

        if (GridManager.Instance == null)
        {
            Debug.LogError("Enemy: GridManager nem található!");
            return;
        }

        var path = GridManager.Instance.FullPath;

        if (path == null || path.Count == 0)
        {
            Debug.LogError("Enemy: Az út üres! Ellenőrizd a GridManager Path Waypoints beállítását.");
            return;
        }

        // ── Véletlenszerű X/Y eltolás ─────────────────────────────
        // WaveManagerből olvasva → egy helyen állítható az egész játékra
        // Minden szörny spawnoláskor kap egy saját eltolást és azt
        // tartja végig az egész úton (párhuzamos mozgás, nem egy vonalban)
        float xRange  = WaveManager.Instance != null ? WaveManager.Instance.enemyPathOffsetX : 0f;
        float yRange  = WaveManager.Instance != null ? WaveManager.Instance.enemyPathOffsetY : 0f;
        float xOffset = Random.Range(-xRange, xRange);
        float yOffset = Random.Range(-yRange, yRange);

        // ── Véletlenszerű sebességvariancia ───────────────────────
        // %-os eltérés az alap moveSpeed-től → természetesebb mozgás
        float variance   = WaveManager.Instance != null ? WaveManager.Instance.enemySpeedVariancePercent : 0f;
        float multiplier = 1f + Random.Range(-variance, variance) / 100f;
        moveSpeed *= multiplier;

        // Waypoint-ok world pozíciókra konvertálva + eltolás alkalmazása
        worldWaypoints = new Vector3[path.Count];
        for (int i = 0; i < path.Count; i++)
        {
            Vector3 wp = GridManager.Instance.GridToWorld(path[i]);
            worldWaypoints[i] = new Vector3(wp.x + xOffset, wp.y + yOffset, wp.z);
        }

        if (overrideStartWaypointIndex > 0 && overrideStartWaypointIndex < worldWaypoints.Length)
        {
            currentWaypointIndex = overrideStartWaypointIndex;
            transform.position   = worldWaypoints[overrideStartWaypointIndex - 1];
        }
        else
        {
            transform.position   = worldWaypoints[0];
            currentWaypointIndex = 1;
        }

        UpdateSortingOrder();

        if (healthBarRoot != null)
        {
            healthBarRoot.SetActive(true);
            _spawnDistToCastle = Castle.Instance != null
                ? Vector3.Distance(worldWaypoints[0], Castle.Instance.transform.position)
                : 0f;

            // A fill szülő-je a tényleges bár konténer – Canvas helyett ezt skálázzuk
            if (healthBarFill != null && healthBarFill.transform.parent != null)
            {
                _hpBarContainer     = healthBarFill.transform.parent;
                _hpBarOriginalScale = _hpBarContainer.localScale;
            }
            else
            {
                _hpBarContainer     = healthBarRoot.transform;
                _hpBarOriginalScale = healthBarRoot.transform.localScale;
            }
        }

        if (hpText != null)
            hpText.gameObject.SetActive(false);
    }

    // ══════════════════════════════════════════════════════
    //  UPDATE
    // ══════════════════════════════════════════════════════

    void Update()
    {
        if (isDead || reachedCastle) return;
        if (isStationary) { UpdateSortingOrder(); return; }
        if (worldWaypoints == null || worldWaypoints.Length == 0) return;

        if (_stunTimer > 0f)
        {
            _stunTimer -= Time.deltaTime;
            UpdateSortingOrder();

            if (_stunTimer <= 0f)
                OnStunEnd();

            return;
        }

        // Pull: a normál mozgás mellé ráteszi a húzóerőt (nem állítja meg a szörnyet)
        if (_pullTimer > 0f)
        {
            _pullTimer -= Time.deltaTime;
            Vector3 dir = _pullCenter - transform.position;
            if (dir.magnitude > 0.05f)
                transform.position += dir.normalized * _pullForce * Time.deltaTime;
        }

        MoveAlongPath();
        UpdateHealthBarScale();
        UpdateHpTextVisibility();
        UpdateSortingOrder();
    }

    /// <summary>
    /// Pull hatás – a szörnyet a megadott pont felé húzza duration másodpercig.
    /// A normál útvonal-követés szünetel a pull ideje alatt.
    /// </summary>
    public void ApplyPull(Vector3 center, float force, float duration)
    {
        if (isDead || reachedCastle) return;
        _pullCenter = center;
        _pullForce  = force;
        _pullTimer  = duration;
    }

    /// <summary>Stun alkalmazása – ha már stun-on van, csak meghosszabbítja.</summary>
    public void ApplyStun(float duration)
    {
        if (isDead || reachedCastle) return;

        bool wasStunned = _stunTimer > 0f;
        _stunTimer = Mathf.Max(_stunTimer, duration);

        if (!wasStunned)
            OnStunStart();
    }

    void OnStunStart()
    {
        if (spriteRenderer != null)
            spriteRenderer.color = stunColor;

        if (stunEffectPrefab != null && _stunEffectInstance == null)
        {
            _stunEffectInstance = Instantiate(stunEffectPrefab, transform.position, Quaternion.identity, transform);

            var sr = spriteRenderer;
            if (sr != null)
            {
                float spriteHeight = sr.bounds.size.y;
                float spriteWidth  = sr.bounds.size.x;

                _stunEffectInstance.transform.localPosition = new Vector3(
                    stunVfxOffset.x,
                    spriteHeight * 0.5f + stunVfxOffset.y,
                    stunVfxOffset.z);

                float finalScale = spriteWidth * stunVfxScale;
                _stunEffectInstance.transform.localScale = new Vector3(finalScale, finalScale, finalScale);
            }
        }
    }

    void OnStunEnd()
    {
        if (spriteRenderer != null)
            spriteRenderer.color = _originalColor;

        if (_stunEffectInstance != null)
        {
            Destroy(_stunEffectInstance);
            _stunEffectInstance = null;
        }
    }

    // ══════════════════════════════════════════════════════
    //  MOZGÁS
    // ══════════════════════════════════════════════════════

    void MoveAlongPath()
    {
        if (currentWaypointIndex >= worldWaypoints.Length)
        {
            ReachCastle();
            return;
        }

        Vector3 target = worldWaypoints[currentWaypointIndex];
        Vector3 dir    = target - transform.position;
        float step     = moveSpeed * externalSpeedMultiplier * Time.deltaTime;

        // Sprite tükrözés – folyamatosan frissül a jelenlegi célirány alapján
        if (spriteRenderer != null && dir.magnitude > 0.01f)
            spriteRenderer.flipX = dir.x < 0;

        if (dir.magnitude <= step)
        {
            // Elértük a következő waypoint-ot → lépjünk a következőre
            transform.position = target;
            currentWaypointIndex++;
        }
        else
        {
            transform.position += dir.normalized * step;
        }
    }

    // ══════════════════════════════════════════════════════
    //  SEBZÉS FOGADÁSA
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// Sebzést alkalmaz az ellenségre, figyelembe véve az armor/magicResist értékét.
    /// Visszatér true-val ha az ellenség meghalt.
    /// </summary>
    /// <param name="amount">Bejövő nyers sebzés</param>
    /// <param name="damageType">Physical → armor csökkenti, Magic → magicResist csökkenti</param>
    /// <summary>
    /// Armor csökkentése találatonként – 0 alá nem mehet.
    /// A tényleges védelmi érték mindig felfelé kerekített (Ceiling).
    /// Floating text csak akkor jelenik meg, ha a kerekített érték egész számnyit csökkent.
    /// </summary>
    public void ReduceArmor(float amount)
    {
        if (amount <= 0f || armor <= 0f) return;

        int effectiveBefore = Mathf.CeilToInt(armor);

        armor = Mathf.Max(0f, armor - amount);

        int effectiveAfter = Mathf.CeilToInt(armor);
        int intReduction   = effectiveBefore - effectiveAfter;

        if (intReduction > 0 && floatingDamageTextPrefab != null)
        {
            var go = Instantiate(floatingDamageTextPrefab, transform.position, Quaternion.identity);
            var ft = go.GetComponent<FloatingDamageText>();
            ft?.InitializeArmorReduction(intReduction);
        }
    }

    /// <summary>HP skálázás spawn után – maxHealth és currentHealth együtt változik.</summary>
    public void ScaleHealth(float multiplier)
    {
        maxHealth     *= multiplier;
        currentHealth  = maxHealth;
        UpdateHealthBar();
    }

    public bool TakeDamage(float amount, DamageType damageType = DamageType.Physical, bool isCrit = false)
    {
        if (isDead || reachedCastle) return false;

        // Ellenállás levonása a bejövő sebzésből
        // Fizikai: minimum 1 – az armor sosem blokkol teljesen
        // Mágikus / Tűz: teljes blokkolás lehetséges (0 damage)
        float resistance = 0f;
        float actualDamage;

        // Sebezhetőség: ha érzékeny erre a típusra, resistance nem számít – csak a szorzó
        float vulnerabilityMultiplier = 0f;
        if (vulnerabilities != null)
        {
            foreach (var v in vulnerabilities)
            {
                if (v.damageType == damageType && v.multiplier > 0f)
                {
                    vulnerabilityMultiplier = v.multiplier;
                    break;
                }
            }
        }

        if (vulnerabilityMultiplier > 0f)
        {
            actualDamage = amount * vulnerabilityMultiplier;
        }
        else if (damageType == DamageType.Physical)
        {
            resistance   = Mathf.CeilToInt(armor);
            actualDamage = Mathf.Max(1f, amount - resistance);
        }
        else
        {
            resistance   = magicResist;
            actualDamage = Mathf.Max(1f, amount - resistance);
        }

        if (halfDamage)
            actualDamage *= 0.5f;

        if (actualDamage <= 0f) return false;

        currentHealth -= actualDamage;
        UpdateHealthBar();

        // Lebegő sebzés szám megjelenítése a találat helyén
        if (floatingDamageTextPrefab != null)
        {
            var go = Instantiate(floatingDamageTextPrefab, transform.position, Quaternion.identity);
            var ft = go.GetComponent<FloatingDamageText>();
            if (ft != null) ft.Initialize(actualDamage, damageType, isCrit, resistance);
        }

        if (currentHealth <= 0f)
        {
            Die();
            return true;
        }
        return false;
    }

    void UpdateHealthBar()
    {
        if (healthBarFill != null)
            healthBarFill.fillAmount = Mathf.Clamp01(currentHealth / maxHealth);
        if (healthBarRoot != null)
            healthBarRoot.SetActive(true);
        UpdateHealthBarScale();
    }

    void UpdateHealthBarScale()
    {
        if (_hpBarContainer == null)  { Debug.LogWarning($"[HPScale] {name}: _hpBarContainer NULL"); return; }
        if (Castle.Instance == null)  { Debug.LogWarning($"[HPScale] {name}: Castle.Instance NULL"); return; }
        if (_spawnDistToCastle <= 0f) { Debug.LogWarning($"[HPScale] {name}: _spawnDistToCastle=0, originalScale={_hpBarOriginalScale}"); return; }

        float currentDist = Vector3.Distance(transform.position, Castle.Instance.transform.position);
        float progress = 1f - Mathf.Clamp01(currentDist / _spawnDistToCastle);
        float t        = Mathf.Clamp01((progress - 0.8f) / 0.2f);
        float scale    = Mathf.Lerp(1f, 10f, t);
        _hpBarContainer.localScale = _hpBarOriginalScale * scale;

    }

    void UpdateHpTextVisibility()
    {
        if (hpText == null) return;
        if (Castle.Instance == null || _spawnDistToCastle <= 0f)
        {
            bool focusOnly = _focusTarget == this;
            if (hpText.gameObject.activeSelf != focusOnly)
                hpText.gameObject.SetActive(focusOnly);
            if (focusOnly)
                hpText.text = Mathf.CeilToInt(currentHealth).ToString();
            return;
        }
        float dist     = Vector3.Distance(transform.position, Castle.Instance.transform.position);
        float progress = 1f - Mathf.Clamp01(dist / _spawnDistToCastle);
        bool show      = progress >= 0.9f || _focusTarget == this;
        if (hpText.gameObject.activeSelf != show)
            hpText.gameObject.SetActive(show);
        if (show)
            hpText.text = Mathf.CeilToInt(currentHealth).ToString();
    }

    // ══════════════════════════════════════════════════════
    //  VÉG ÁLLAPOTOK
    // ══════════════════════════════════════════════════════

    protected virtual void Die()
    {
        if (isDead) return;
        isDead = true;

        if (_focusTarget == this)
            ClearFocusTarget();

        OnDied?.Invoke(this);

        if (canExplode)
        {
            StartCoroutine(ExplodeAndDestroy());
        }
        else
        {
            StartCoroutine(DieWithAnimation());
        }
    }

    IEnumerator DieWithAnimation()
    {
        if (healthBarRoot != null) healthBarRoot.SetActive(false);

        var animator = GetComponent<Animator>();
        if (animator != null && !string.IsNullOrEmpty(deathTrigger))
        {
            animator.SetTrigger(deathTrigger);

            float elapsed = 0f;
            bool started = false;
            while (elapsed < deathAnimTimeout)
            {
                yield return null;
                elapsed += Time.deltaTime;
                var info = animator.GetCurrentAnimatorStateInfo(0);

                if (!string.IsNullOrEmpty(deathStateTag))
                {
                    if (!started && info.IsTag(deathStateTag)) started = true;
                    if (started && info.normalizedTime >= 1f) break;
                }
            }
        }

        if (deathEffect != null)
            Instantiate(deathEffect, transform.position, Quaternion.identity);

        Destroy(gameObject);
    }

    IEnumerator ExplodeAndDestroy()
    {
        var animator = GetComponent<Animator>();

        if (animator != null && !string.IsNullOrEmpty(explosionTrigger))
        {
            animator.SetTrigger(explosionTrigger);

            // Várjuk meg amíg az explode state ténylegesen elindul
            float timeout = 5f;
            float elapsed = 0f;
            bool explodeStarted = false;

            while (elapsed < timeout)
            {
                yield return null;
                elapsed += Time.deltaTime;
                var info = animator.GetCurrentAnimatorStateInfo(0);

                // Megvárjuk amíg az "explode" tagű state elindul
                if (!explodeStarted && info.IsTag("explode"))
                    explodeStarted = true;

                // Ha elindult és végigfutott → kilépünk
                if (explodeStarted && info.normalizedTime >= 1f)
                    break;
            }
        }

        // Robbanás effekt – méret az explosionRadius-hoz igazítva
        if (explosionEffect != null)
        {
            var fx = Instantiate(explosionEffect, transform.position, Quaternion.identity);
            fx.transform.localScale = explosionEffect.transform.localScale * explosionRadius;
        }

        // AOE sebzés – másolaton iterálunk, mert a TakeDamage eltávolíthat tornyot a listából
        var towersSnapshot = new List<Tower>(Tower.AllTowers);
        foreach (var tower in towersSnapshot)
        {
            if (tower == null) continue;
            float dist = Vector3.Distance(transform.position, tower.transform.position);
            if (dist <= explosionRadius)
                tower.TakeDamage(explosionDamage, explosionDamageType);
        }

        Destroy(gameObject);
    }

    void ReachCastle()
    {
        if (reachedCastle) return;
        reachedCastle = true;

        if (_focusTarget == this)
            ClearFocusTarget();

        if (dealsCastleDamage)
        {
            int dmg = Mathf.CeilToInt(currentHealth);
            if (Castle.Instance != null)
                Castle.Instance.TakeDamage(dmg);
        }

        OnReachedCastle?.Invoke(this);
        Destroy(gameObject);
    }

    // ══════════════════════════════════════════════════════
    //  IZOMETRIKUS MÉLYSÉG SORTING
    //  Y pozíció alapján dönti el melyik sprite kerül előre
    // ══════════════════════════════════════════════════════

    void UpdateSortingOrder()
    {
        if (GridManager.Instance == null || spriteRenderer == null) return;
        var cell = GridManager.Instance.WorldToGrid(transform.position);
        spriteRenderer.sortingOrder = GridManager.Instance.GetSortingOrder(cell.x, cell.y) + 1;
    }

    // ══════════════════════════════════════════════════════
    //  PUBLIKUS LEKÉRDEZÉSEK
    // ══════════════════════════════════════════════════════

    public float HealthPercent => currentHealth / maxHealth;
    public float CurrentHealth => currentHealth;
    public bool IsDead => isDead || reachedCastle;

    /// <summary>
    /// Visszateleportálja az ellenséget a kezdőpontra.
    /// A TeleportTower hívja találatkor.
    /// </summary>
    public void TeleportToStart()
    {
        if (isDead || reachedCastle || worldWaypoints == null || worldWaypoints.Length == 0) return;

        transform.position   = worldWaypoints[0];
        currentWaypointIndex = 1;
    }

    /// <summary>
    /// Megfelezi az ellenfél aktuális HP-ját (charm effekt – nem öli meg, minimum 1 marad).
    /// </summary>
    public void HalveHealth()
    {
        if (isDead || reachedCastle) return;
        currentHealth = Mathf.Max(1f, currentHealth * 0.5f);
        UpdateHealthBar();
    }

    /// <summary>Azonnal megöli az ellenfelet (charm effekt).</summary>
    public void InstantKill()
    {
        if (isDead || reachedCastle) return;
        currentHealth = 0f;
        UpdateHealthBar();
        Die();
    }

    /// <summary>
    /// A kastályig hátralévő út hossza world unitban.
    /// Tornyok ezzel döntik el melyik ellenség a legveszélyesebb célpont.
    /// </summary>
    public float DistanceToCastle
    {
        get
        {
            if (isStationary)
                return Castle.Instance != null
                    ? Vector3.Distance(transform.position, Castle.Instance.transform.position)
                    : float.MaxValue;
            if (worldWaypoints == null) return 0f;
            float dist = Vector3.Distance(transform.position,
                currentWaypointIndex < worldWaypoints.Length
                    ? worldWaypoints[currentWaypointIndex]
                    : transform.position);
            for (int i = currentWaypointIndex; i < worldWaypoints.Length - 1; i++)
                dist += Vector3.Distance(worldWaypoints[i], worldWaypoints[i + 1]);
            return dist;
        }
    }
}
