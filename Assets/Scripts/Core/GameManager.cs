using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Központi játékmenedzser – arany, játékállapot, singleton.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Kezdő értékek")]
    public int startingGold = 10;
    public int goldPerKill = 1;

    private int currentGold;
    private int currentWave = 0;
    private bool isGameOver = false;
    private bool isVictory = false;
    private float _gameStartTime = 0f;

    // Kill statisztika – szörny típusonként megszámolt ölések + ikon
    public struct KillEntry { public int count; public UnityEngine.Sprite icon; }
    private readonly Dictionary<string, KillEntry> _killCounts = new Dictionary<string, KillEntry>();
    public IReadOnlyDictionary<string, KillEntry> KillCounts => _killCounts;

    // Töredék arany felhalmozó – PvP küldött szörny túlélési jutalom (0.1g/s)
    private float _pendingGold = 0f;

    // Survival-arany – a KÜLDŐ gépén fut, az ő saját élő szörnyeit számolja
    private int   _mySentEnemiesAlive  = 0;
    private float _survivalGoldPending = 0f;
    private float _survivalGoldTick    = 0f;

    /// <summary>Mennyi survival-aranyat szerzett a játékos az előző hullámban.</summary>
    public int LastWaveGoldEarned { get; private set; } = 0;
    /// <summary>Hány élő szörny van az ellenfél mezején amit én küldtem.</summary>
    public int MySentEnemiesAlive => _mySentEnemiesAlive;
    /// <summary>Az aktuális hullámban összegyűlt survival-arany (saját kijelzőhöz).</summary>
    public float CurrentWaveSurvivalGold => _survivalGoldPending;

    public void IncrementSentEnemies() => _mySentEnemiesAlive++;
    public void DecrementSentEnemies() => _mySentEnemiesAlive = Mathf.Max(0, _mySentEnemiesAlive - 1);

    public int CurrentGold => currentGold;
    public int CurrentWave => currentWave;
    public bool IsGameOver => isGameOver;

    // Események – UI és egyéb rendszerek feliratkozhatnak
    public System.Action<int> OnGoldChanged;
    public System.Action<int> OnWaveChanged;
    public System.Action OnGameOver;
    public System.Action OnVictory;
    public System.Action OnEnemyKilled;  // minden megölt szörnynél tűzik

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>A játék kezdete óta eltelt másodpercek (SSF statisztikához).</summary>
    public int ElapsedPlaySeconds => Mathf.FloorToInt(Time.realtimeSinceStartup - _gameStartTime);

    void Start()
    {
        _gameStartTime = Time.realtimeSinceStartup;
        SetGold(startingGold);

        // PvP szint-kompenzáció: ha az ellenfél magasabb szintű,
        // a különbség / 10 arányban extra gold jár
        int lvlBonus = NetworkGameManager.Instance?.LevelGoldBonus ?? 0;
        if (lvlBonus > 0)
        {
            AddGold(lvlBonus);
            Debug.Log($"[GM] Szint-kompenzáció alkalmazva: +{lvlBonus}g");
        }
    }

    // ── Arany kezelés ──────────────────────────────────────────────

    public bool CanAfford(int amount) => currentGold >= amount;

    /// <summary>Arany levonás. Visszatér false-szal ha nincs elég.</summary>
    public bool SpendGold(int amount)
    {
        if (!CanAfford(amount)) return false;
        SetGold(currentGold - amount);
        return true;
    }

    public void AddGold(int amount)
    {
        SetGold(currentGold + amount);
    }

    public void RecordKill(string enemyTypeName, UnityEngine.Sprite icon)
    {
        if (string.IsNullOrEmpty(enemyTypeName)) return;
        _killCounts.TryGetValue(enemyTypeName, out KillEntry entry);
        _killCounts[enemyTypeName] = new KillEntry { count = entry.count + 1, icon = icon ?? entry.icon };
    }

    /// <summary>
    /// PvP-ben a küldött szörny halálakor érkezik hálózaton keresztül.
    /// Azonnal jóváírja az aranyat (nem pending).
    /// </summary>
    public void AddFractionalGold(float amount)
    {
        if (amount <= 0f) return;
        _pendingGold += amount;
        int whole = Mathf.FloorToInt(_pendingGold);
        if (whole > 0)
        {
            _pendingGold -= whole;
            SetGold(currentGold + whole);
        }
    }

    private void SetGold(int value)
    {
        currentGold = Mathf.Max(0, value);
        OnGoldChanged?.Invoke(currentGold);
    }

    /// <summary>
    /// WaveManager hívja minden új hullám elején.
    /// Jóváírja a pending survival gold-ot és nullázza a számlálót.
    /// </summary>
    public void FinalizeWaveGold()
    {
        int award = Mathf.FloorToInt(_survivalGoldPending);
        LastWaveGoldEarned = award;
        _survivalGoldPending = 0f;
        // _mySentEnemiesAlive NEM nullázódik – az elküldött szörnyek
        // tovább élhetnek a következő hullámban is, és továbbra is aranyat termelnek.
        // A számláló IncrementSentEnemies/DecrementSentEnemies hívásokkal automatikusan
        // pontosan tükrözi az élő szörnyek számát.
        if (award > 0) AddGold(award);
    }

    // ── Survival gold – másodpercenkénti tick ─────────────────────

    void Update()
    {
        if (isGameOver || isVictory) return;

        _survivalGoldTick += Time.deltaTime;
        if (_survivalGoldTick < 1f) return;
        _survivalGoldTick -= 1f;

        // SSF módban nem jár survival gold – nincs ellenfél akinek a pályáján élnek a szörnyek
        bool isSsf = UserProgressManager.Instance != null
                     && UserProgressManager.Instance.HasCharacter
                     && UserProgressManager.Instance.Data.IsSSF;
        if (!isSsf && _mySentEnemiesAlive > 0)
            _survivalGoldPending += _mySentEnemiesAlive * 0.1f;
    }

    // ── Hullám kezelés ────────────────────────────────────────────

    public void SetWave(int wave)
    {
        currentWave = wave;
        OnWaveChanged?.Invoke(currentWave);
    }

    // ── Játék vége ────────────────────────────────────────────────

    public void TriggerGameOver()
    {
        if (isGameOver || isVictory) return;
        isGameOver = true;
        OnGameOver?.Invoke();
        Debug.Log("GAME OVER – A szörnyek áttörtek a váron!");
    }

    public void TriggerVictory()
    {
        if (isGameOver || isVictory) return;
        isVictory = true;
        OnVictory?.Invoke();
        Debug.Log("GYŐZELEM!");
    }

    public void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
