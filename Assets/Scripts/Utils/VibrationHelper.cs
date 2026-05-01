using UnityEngine;

/// <summary>
/// Statikus segédosztály rövid rezgéshez – Android és iOS támogatással.
/// </summary>
public static class VibrationHelper
{
    /// <summary>Rövid rezgés (pl. skill vásárlásnál, gomb megnyomásánál).</summary>
    public static void VibrateShort(long durationMs = 80L)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            int apiLevel = new AndroidJavaClass("android.os.Build$VERSION")
                               .GetStatic<int>("SDK_INT");

            var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            var activity    = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            AndroidJavaObject vibrator;

            if (apiLevel >= 31)
            {
                var vibratorManager = activity.Call<AndroidJavaObject>(
                    "getSystemService", "vibrator_manager");
                vibrator = vibratorManager.Call<AndroidJavaObject>("getDefaultVibrator");
            }
            else
            {
                vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
            }

            var vibrationEffect = new AndroidJavaClass("android.os.VibrationEffect");
            var effect = vibrationEffect.CallStatic<AndroidJavaObject>(
                "createOneShot", durationMs, -1);
            vibrator.Call("vibrate", effect);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Vibration hiba: {e.Message}");
            Handheld.Vibrate();
        }
#elif UNITY_IOS && !UNITY_EDITOR
        Handheld.Vibrate();
#endif
    }
}
