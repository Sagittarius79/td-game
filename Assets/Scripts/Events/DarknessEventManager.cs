using UnityEngine;

/// <summary>
/// PvP receive pass-through – a sorsolást WeatherEventManager végzi.
/// NetworkGameManager hívja BroadcastDarknessEvent után.
/// </summary>
public class DarknessEventManager : MonoBehaviour
{
    public static DarknessEventManager Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>NetworkGameManager hívja PvP módban.</summary>
    public void ReceiveDarknessEvent(float duration) =>
        DarknessOverlay.Instance?.Activate(duration);
}
