/// <summary>
/// Globális event állapot ellenőrző és koordinátor.
///
/// IsAnyEventActive – egyszerre max. 1 aktív event (warning delay alatt is).
/// ResetAllChances  – ha bármelyik event létrejön, az összes manager visszaáll
///                    a kiindulási esélyére.
/// </summary>
public static class EventCoordinator
{
    public static bool IsAnyEventActive =>
        (DarknessOverlay.Instance?.IsActive             ?? false) ||
        (CloudOverlay.Instance?.IsActive                ?? false) ||
        (RainOverlay.Instance?.IsActive                 ?? false) ||
        (BossEventManager.Instance?.IsActive              ?? false);

    /// <summary>
    /// Hívja meg az éppen triggerelt EventManager, mielőtt elindítja az eventet.
    /// Visszaállítja az összes event esélyét az alap értékre.
    /// </summary>
    public static void ResetAllChances()
    {
        WeatherEventManager.Instance?.ResetChance();
        BossEventManager.Instance?.ResetChance();
    }
}
