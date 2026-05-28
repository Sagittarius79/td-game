using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Boss event koordinátor – egyetlen dobás hullámonként.
/// Ha kijön, véletlenszerűen választ a bossPrefabs listából és spawnol egyet.
/// Az event addig aktív amíg a boss él (vagy lejár az időlimit).
///
/// Új boss hozzáadása: húzd be a Boss Prefabs listájába az inspektorban.
/// PvP módban csak a szerver sorsol.
/// </summary>
public class BossEventManager : MonoBehaviour
{
    public static BossEventManager Instance { get; private set; }

    [Header("Boss prefabok")]
    [Tooltip("Húzd be az összes lehetséges boss szörny prefabot")]
    public List<GameObject> bossPrefabs = new();

    [Header("Esély")]
    [Tooltip("Ettől a hullámtól jelenhet meg boss (előtte sosem, hiába dobja ki a véletlen)")]
    [Min(2)]
    public int minWave = 5;

    [Tooltip("Kiindulási valószínűség hullámonként (0–1). Pl. 0.02 = 2%")]
    public float baseChance = 0.02f;
    [Tooltip("Esély növekedése minden kihagyott hullám után (0.01 = +1%/hullám)")]
    public float chanceIncrement = 0.01f;
    [Tooltip("Maximum elérhető esély (0–1)")]
    public float maxChance = 0.95f;

    [Header("HP skálázás")]
    [Tooltip("Alap HP szorzó (1 = prefab HP)")]
    public float hpBaseMultiplier = 2f;
    [Tooltip("HP növekedés hullámonként %-ban (0.1 = +10%/hullám)")]
    public float hpPerWavePercent = 0.1f;

    [Header("Jutalom")]
    [Tooltip("Ennyi goldot kap a játékos, ha lelövi a boss-t (0 = nincs extra jutalom)")]
    public int bossKillGold = 100;

    [Header("Spawn")]
    public float spawnDelay = 1f;

    [Header("Figyelmeztetés")]
    public float warningDelay = 5f;

    [Header("Hang")]
    public AudioClip activateSound;
    public AudioClip deactivateSound;

    [Header("Boss zene")]
    [Tooltip("Ez a zene szól loop-ban amíg a boss él. Üres = nincs boss zene.")]
    public AudioClip bossMusic;
    [Tooltip("Boss zene hangereje (0–1)")]
    [Range(0f, 1f)]
    public float bossMusicVolume = 0.7f;
    [Tooltip("Fade idő a boss zene be- és kihalkulásához (másodperc)")]
    public float bossMusicFade = 1f;

    [Header("Banner (opcionális)")]
    public GameObject bannerPanel;
    public TextMeshProUGUI bannerText;
    public float bannerDuration = 3f;
    public UnityEngine.Color bannerColor = new UnityEngine.Color(0.8f, 0.1f, 0.1f);

    public bool IsActive { get; private set; }

    private float _currentChance;
    private Coroutine _routine;
    private AudioSource _bossSource;
    private Coroutine _musicFadeRoutine;

    /// <summary>
    /// Aktív boss-lánc tagok száma (Phoenix + tojás + újjászületett Phoenix...).
    /// Ha 0-ra csökken, az event véget ér és leáll a zene.
    /// </summary>
    private int _chainCount = 0;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _currentChance = baseChance;

        _bossSource             = gameObject.AddComponent<AudioSource>();
        _bossSource.loop        = true;
        _bossSource.playOnAwake = false;
        _bossSource.volume      = 0f;
    }

    void Start()
    {
        if (WaveManager.Instance != null)
            WaveManager.Instance.OnWaveStarted += OnWaveStarted;
    }

    void OnDestroy()
    {
        if (WaveManager.Instance != null)
            WaveManager.Instance.OnWaveStarted -= OnWaveStarted;
    }

    void OnWaveStarted(int wave)
    {
        if (wave < minWave) return;
        if (bossPrefabs == null || bossPrefabs.Count == 0) return;
        if (GameManager.Instance == null || GameManager.Instance.IsGameOver) return;
        if (EventCoordinator.IsAnyEventActive) return;

        bool isPvP = NetworkGameManager.Instance != null && NetworkGameManager.Instance.IsPvPMode;
        if (isPvP && !Unity.Netcode.NetworkManager.Singleton.IsServer) return;

        if (Random.value > _currentChance)
        {
            _currentChance = Mathf.Min(maxChance, _currentChance + chanceIncrement);
            Debug.Log($"[BossEvent] Kihagyva – következő esély: {_currentChance:P0}");
            return;
        }

        int prefabIndex = Random.Range(0, bossPrefabs.Count);

        Debug.Log($"[BossEvent] Aktiválódott: {bossPrefabs[prefabIndex]?.name} | Hullám: {wave} | Esély volt: {_currentChance:P0}");

        _currentChance = baseChance;

        if (isPvP)
            NetworkGameManager.Instance.BroadcastBossEvent(prefabIndex, 0f);
        else
            Trigger(prefabIndex);
    }

    /// <summary>NetworkGameManager hívja PvP módban.</summary>
    public void ReceiveBossEvent(int prefabIndex, float duration) => Trigger(prefabIndex);

    void Trigger(int prefabIndex)
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(BossRoutine(prefabIndex));
    }

    IEnumerator BossRoutine(int prefabIndex)
    {
        IsActive = true;

        // Figyelmeztetés
        AudioManager.Instance?.PlaySFX(activateSound);
        yield return new WaitForSeconds(warningDelay);

        // Boss zene indítása – háttérzene elnémítása
        StartBossMusic();

        ShowBanner();

        if (spawnDelay > 0f)
            yield return new WaitForSeconds(spawnDelay);

        if (GameManager.Instance == null || GameManager.Instance.IsGameOver) { StopBossMusic(); IsActive = false; yield break; }
        if (GridManager.Instance == null) { StopBossMusic(); IsActive = false; yield break; }

        // Prefab érvényességi ellenőrzés
        if (prefabIndex < 0 || prefabIndex >= bossPrefabs.Count || bossPrefabs[prefabIndex] == null)
        {
            Debug.LogWarning("[BossEvent] Érvénytelen boss prefab index!");
            StopBossMusic(); IsActive = false;
            yield break;
        }

        // Spawn
        Vector3 spawnPos = GridManager.Instance.GridToWorld(GridManager.Instance.SpawnCell);
        GameObject bossGO = Instantiate(bossPrefabs[prefabIndex], spawnPos, Quaternion.identity);

        var enemy = bossGO.GetComponent<Enemy>();
        if (enemy != null)
        {
            enemy.enemyTypeName     = "Boss";
            enemy.countAsKill       = true;
            enemy.dealsCastleDamage = true;

            int wave = WaveManager.Instance != null ? WaveManager.Instance.CurrentWave : 1;
            float waveScale = Mathf.Pow(1f + hpPerWavePercent, wave - 1);
            enemy.ScaleHealth(hpBaseMultiplier * waveScale);

            // Jutalom a boss leöléséért
            if (bossKillGold > 0)
                enemy.OnDied += _ => GameManager.Instance?.AddGold(bossKillGold);

            // Lánc-követő: a boss az első tag
            RegisterChainEntity(enemy);
        }
        else
        {
            // Nincs Enemy komponens – nincs kit követni
            AudioManager.Instance?.PlaySFX(deactivateSound);
            StopBossMusic(); IsActive = false; yield break;
        }

        // Várakozás amíg a teljes lánc él (Phoenix → Tojás → Phoenix → ...)
        while (_chainCount > 0)
            yield return null;

        AudioManager.Instance?.PlaySFX(deactivateSound);

        // Boss zene leállítása – háttérzene visszaállítása
        StopBossMusic();

        IsActive = false;
    }

    void StartBossMusic()
    {
        if (bossMusic == null || _bossSource == null) return;

        // Háttérzene elnémítása
        AudioManager.Instance?.SetMusicMute(true);

        _bossSource.clip   = bossMusic;
        _bossSource.volume = 0f;
        _bossSource.Play();

        if (_musicFadeRoutine != null) StopCoroutine(_musicFadeRoutine);
        _musicFadeRoutine = StartCoroutine(FadeBossMusic(0f, bossMusicVolume, bossMusicFade));
    }

    void StopBossMusic()
    {
        if (_bossSource == null) return;
        if (_musicFadeRoutine != null) StopCoroutine(_musicFadeRoutine);
        _musicFadeRoutine = StartCoroutine(StopBossMusicRoutine());
    }

    IEnumerator StopBossMusicRoutine()
    {
        // Boss zene kifade-elése
        yield return StartCoroutine(FadeBossMusic(_bossSource.volume, 0f, bossMusicFade));
        _bossSource.Stop();

        // Háttérzene visszakapcsolása
        AudioManager.Instance?.SetMusicMute(false);
    }

    IEnumerator FadeBossMusic(float from, float to, float duration)
    {
        if (_bossSource == null) yield break;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed           += Time.deltaTime;
            _bossSource.volume = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        _bossSource.volume = to;
    }

    void ShowBanner()
    {
        if (bannerPanel != null && bannerText != null)
        {
            bannerText.text = "BOSS MEGJELENT!";
            StartCoroutine(BannerRoutine());
        }
        else
        {
            UIManager.Instance?.ShowNotification("BOSS MEGJELENT!", bannerColor);
        }
    }

    IEnumerator BannerRoutine()
    {
        bannerPanel.SetActive(true);
        yield return new WaitForSeconds(bannerDuration);
        bannerPanel.SetActive(false);
    }

    // ── Boss lánc-követő API ──────────────────────────────────────

    /// <summary>
    /// Hozzáad egy entitást a boss-lánchoz (Phoenix, tojás, újjászületett Phoenix...).
    /// Amíg legalább egy tag él, a boss event és a zene aktív marad.
    /// PhoenixRebirth és PhoenixEgg hívja.
    /// </summary>
    public void RegisterChainEntity(Enemy enemy)
    {
        if (enemy == null) return;
        _chainCount++;
        enemy.OnDied          += OnChainEntityEnded;
        enemy.OnReachedCastle += OnChainEntityEnded;
    }

    /// <summary>
    /// Eltávolít egy entitást a láncból anélkül, hogy megölné
    /// (pl. PhoenixEgg kikeléskor – a tojás nem hal meg, csak átalakul).
    /// </summary>
    public void UnregisterChainEntity(Enemy enemy)
    {
        if (enemy == null) return;
        enemy.OnDied          -= OnChainEntityEnded;
        enemy.OnReachedCastle -= OnChainEntityEnded;
        _chainCount = Mathf.Max(0, _chainCount - 1);
    }

    void OnChainEntityEnded(Enemy e)
    {
        e.OnDied          -= OnChainEntityEnded;
        e.OnReachedCastle -= OnChainEntityEnded;
        _chainCount = Mathf.Max(0, _chainCount - 1);
        // Ha a lánc teljesen üres és az event még aktív → deactivate hang
        // (a while loop majd kilép és a BossRoutine folytatja a leállítást)
    }

    /// <summary>Visszaállítja az esélyt az alap értékre.</summary>
    public void ResetChance() => _currentChance = baseChance;
}
