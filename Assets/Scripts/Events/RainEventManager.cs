using UnityEngine;

/// <summary>
/// PvP receive pass-through – a sorsolást WeatherEventManager végzi.
/// NetworkGameManager hívja BroadcastRainEvent után.
/// </summary>
public class RainEventManager : MonoBehaviour
{
    public static RainEventManager Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>NetworkGameManager hívja PvP módban.</summary>
    public void ReceiveRainEvent(float duration) =>
        RainOverlay.Instance?.Activate(duration);
}
