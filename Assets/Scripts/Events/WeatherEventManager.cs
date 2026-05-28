using UnityEngine;

/// <summary>
/// Időjárás event koordinátor – egyetlen dobás hullámonként.
/// Ha kijön, véletlenszerűen választ a három időjárás event közül
/// (Mágikus Sötétség / Felhő / Eső), majd visszaállítja az esélyt.
/// PvP módban csak a szerver sorsol.
/// </summary>
public class WeatherEventManager : MonoBehaviour
{
    public static WeatherEventManager Instance { get; private set; }

    [Header("Esély")]
    [Tooltip("Kiindulási valószínűség hullámonként (0–1). Pl. 0.02 = 2%")]
    public float baseChance = 0.02f;
    [Tooltip("Esély növekedése minden kihagyott hullám után (0.01 = +1%/hullám)")]
    public float chanceIncrement = 0.01f;
    [Tooltip("Maximum elérhető esély (0–1)")]
    public float maxChance = 0.95f;

    [Header("Event hossza (véletlenszerű)")]
    public float minDuration = 15f;
    public float maxDuration = 30f;

    private float _currentChance;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _currentChance = baseChance;
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
        if (wave <= 1) return;
        if (GameManager.Instance == null || GameManager.Instance.IsGameOver) return;
        if (EventCoordinator.IsAnyEventActive) return;

        bool isPvP = NetworkGameManager.Instance != null && NetworkGameManager.Instance.IsPvPMode;
        if (isPvP && !Unity.Netcode.NetworkManager.Singleton.IsServer) return;

        if (Random.value > _currentChance)
        {
            _currentChance = Mathf.Min(maxChance, _currentChance + chanceIncrement);
            Debug.Log($"[WeatherEvent] Kihagyva – következő esély: {_currentChance:P0}");
            return;
        }

        float duration  = Random.Range(minDuration, maxDuration);
        int   eventType = Random.Range(0, 3); // 0=Sötétség, 1=Felhő, 2=Eső

        string[] names = { "Mágikus Sötétség", "Felhő átvonulás", "Eső átvonulás" };
        Debug.Log($"[WeatherEvent] Aktiválódott: {names[eventType]} | Hullám: {wave} | Esély volt: {_currentChance:P0} | Időtartam: {duration:F1}s");

        _currentChance = baseChance;

        if (isPvP)
        {
            switch (eventType)
            {
                case 0: NetworkGameManager.Instance.BroadcastDarknessEvent(duration); break;
                case 1: NetworkGameManager.Instance.BroadcastCloudEvent(duration);    break;
                case 2: NetworkGameManager.Instance.BroadcastRainEvent(duration);     break;
            }
        }
        else
        {
            TriggerWeatherEvent(eventType, duration);
        }
    }

    void TriggerWeatherEvent(int eventType, float duration)
    {
        switch (eventType)
        {
            case 0: DarknessOverlay.Instance?.Activate(duration); break;
            case 1: CloudOverlay.Instance?.Activate(duration);    break;
            case 2: RainOverlay.Instance?.Activate(duration);     break;
        }
    }

    /// <summary>Visszaállítja az esélyt az alap értékre.</summary>
    public void ResetChance() => _currentChance = baseChance;
}
