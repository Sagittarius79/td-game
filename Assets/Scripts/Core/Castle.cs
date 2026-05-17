using UnityEngine;

/// <summary>
/// Kastély – életerő kezelés. Ha 0-ra esik, vége a játéknak.
/// </summary>
public class Castle : MonoBehaviour
{
    public static Castle Instance { get; private set; }

    [Header("Kastély értékei")]
    public int maxHealth = 30;

    [Header("Pozíció finomhangolás (pixel)")]
    public Vector2 positionOffset = new Vector2(0f, 100f);

    [Header("Skill fa")]
    [Tooltip("A skill fa amelyből a CastleHealth bónusz olvasódik")]
    public SkillTreeDefinition skillTree;

    [Header("Hangok")]
    public AudioClip hitSound;

    private int currentHealth;
    public int CurrentHealth => currentHealth;
    public float HealthPercent => (float)currentHealth / maxHealth;

    public System.Action<int, int> OnHealthChanged;
    public System.Action OnDestroyed;

    // ── HP Regen ─────────────────────────────────────────────────────
    private const float REGEN_INTERVAL = 30f;
    private float _regenTimer    = 0f;
    public  float RegenTimer     => _regenTimer;
    private int   _regenAmount   = 0;
    private int   _overRegenAmount = 0;
    public  int   RegenAmount => _regenAmount;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        currentHealth = maxHealth;
    }

    void Start()
    {
        // Skill bónuszok kiszámítása (Start-ban, mert Awake-ben a singletonok még null-ok lehetnek)
        if (skillTree != null && UserProgressManager.Instance != null)
        {
            int bonus = (int)UserProgressManager.Instance.GetTotalSkillEffect(SkillEffectType.CastleHealth, skillTree);
            maxHealth += bonus;
        }

        var buildingsCfg = BuildingsConfig.Instance ?? FindObjectOfType<BuildingsConfig>();
        if (buildingsCfg != null && buildingsCfg.buildingsSkillTree != null &&
            UserProgressManager.Instance != null)
        {
            int buildingBonus = (int)UserProgressManager.Instance.GetTotalSkillEffect(
                SkillEffectType.BuildingHPExtra, buildingsCfg.buildingsSkillTree);
            maxHealth += buildingBonus;

            _regenAmount = (int)UserProgressManager.Instance.GetTotalSkillEffect(
                SkillEffectType.BuildingHPRegen, buildingsCfg.buildingsSkillTree);

            _overRegenAmount = (int)UserProgressManager.Instance.GetTotalSkillEffect(
                SkillEffectType.BuildingHPOverRegen, buildingsCfg.buildingsSkillTree);
        }

        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        NetworkGameManager.Instance?.BroadcastCastleHp(currentHealth);
        Invoke(nameof(BroadcastHpDelayed), 10f);

        // Csak akkor pozicionálja magát, ha a GridManager létezik
        if (GridManager.Instance != null)
        {
            Vector2Int castleCell = GridManager.Instance.CastleCell;
            Vector3 basePos = GridManager.Instance.GridToWorld(castleCell);
            transform.position = basePos + new Vector3(positionOffset.x, positionOffset.y, 0f);

            var sr = GetComponent<SpriteRenderer>();
            if (sr != null)
                sr.sortingOrder = GridManager.Instance.GetSortingOrder(castleCell.x, castleCell.y);
        }
        else
        {
            Debug.LogWarning("Castle: GridManager nem található! " +
                "A kastély a saját pozícióján marad.");
        }
    }

    void BroadcastHpDelayed() => NetworkGameManager.Instance?.BroadcastCastleHp(currentHealth);

    void Update()
    {
        if (_regenAmount <= 0 && _overRegenAmount <= 0) return;

        _regenTimer += Time.deltaTime;
        if (_regenTimer >= REGEN_INTERVAL)
        {
            _regenTimer = 0f;

            if (_regenAmount > 0)
            {
                // Pool-alapú gyógyítás: először kastély, maradékból tornyok sorban
                int pool = _regenAmount;

                int castleHeal = 0;
                if (currentHealth > 0 && currentHealth < maxHealth)
                {
                    castleHeal = Mathf.Min(pool, maxHealth - currentHealth);
                    pool -= castleHeal;
                }

                var towerHeals = new System.Collections.Generic.List<(Tower t, int amount)>();
                foreach (var tower in Tower.AllTowers)
                {
                    if (pool <= 0) break;
                    if (tower == null || tower.IsDead || tower.HealthPercent >= 1f) continue;
                    int heal = Mathf.Min(pool, Mathf.RoundToInt(tower.maxHealth - tower.CurrentHealth));
                    if (heal <= 0) continue;
                    towerHeals.Add((tower, heal));
                    pool -= heal;
                }

                int totalHeal = _regenAmount - pool;

                // Csak akkor gyógyítunk ha van elég gold
                if (totalHeal > 0 && GameManager.Instance != null &&
                    GameManager.Instance.CanAfford(totalHeal))
                {
                    GameManager.Instance.SpendGold(totalHeal);

                    // Kastély gyógyítás
                    if (castleHeal > 0)
                    {
                        currentHealth = Mathf.Min(maxHealth, currentHealth + castleHeal);
                        OnHealthChanged?.Invoke(currentHealth, maxHealth);
                    }

                    // Torony gyógyítás sorban
                    foreach (var (tower, amount) in towerHeals)
                        tower.Heal(amount);
                }
            }

            // Over regen: ha teli van a kastély, nő a max HP
            if (_overRegenAmount > 0 && currentHealth > 0 && currentHealth >= maxHealth)
            {
                maxHealth     += _overRegenAmount;
                currentHealth  = maxHealth;
                OnHealthChanged?.Invoke(currentHealth, maxHealth);
            }

        }
    }

    public void TakeDamage(int damage)
    {
        if (currentHealth <= 0) return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(0, currentHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        NetworkGameManager.Instance?.BroadcastCastleHp(currentHealth);

        AudioManager.Instance?.PlaySFX(hitSound);

        Debug.Log($"Kastély sebzett! HP: {currentHealth}/{maxHealth}");

        if (currentHealth <= 0)
        {
            OnDestroyed?.Invoke();

            // PvP módban a NetworkGameManager értesítése (ő küldi a vereség üzenetet a szervernek)
            // Solo módban a szokásos Game Over triggerelése
            if (NetworkGameManager.Instance != null && NetworkGameManager.Instance.IsPvPMode)
                NetworkGameManager.Instance.OnLocalCastleDestroyed();
            else if (GameManager.Instance != null)
                GameManager.Instance.TriggerGameOver();
            else
                Debug.LogError("Castle: GameManager.Instance NULL – game over nem triggerelhető!");
        }
    }
}
