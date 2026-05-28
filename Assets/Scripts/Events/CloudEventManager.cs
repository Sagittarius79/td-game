using UnityEngine;

/// <summary>
/// PvP receive pass-through – a sorsolást WeatherEventManager végzi.
/// NetworkGameManager hívja BroadcastCloudEvent után.
/// </summary>
public class CloudEventManager : MonoBehaviour
{
    public static CloudEventManager Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>NetworkGameManager hívja PvP módban.</summary>
    public void ReceiveCloudEvent(float duration) =>
        CloudOverlay.Instance?.Activate(duration);
}
