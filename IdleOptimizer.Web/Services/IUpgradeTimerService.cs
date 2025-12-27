using IdleOptimizer.Models;

namespace IdleOptimizer.Services;

public interface IUpgradeTimerService : IDisposable
{
    /// <summary>
    /// Calculates the remaining time (in seconds) until an upgrade can be afforded
    /// </summary>
    /// <param name="item">The upgrade item to check</param>
    /// <returns>Remaining seconds, or int.MaxValue if cannot afford</returns>
    int GetTimeToAfford(UpgradeResult item);

    /// <summary>
    /// Formats a time duration in seconds to a human-readable string
    /// </summary>
    /// <param name="seconds">Time in seconds</param>
    /// <returns>Formatted time string (e.g., "1d 2h 30m 15s")</returns>
    string FormatTime(int seconds);

    /// <summary>
    /// Starts the timer that updates every second
    /// </summary>
    void StartTimer();

    /// <summary>
    /// Stops the timer
    /// </summary>
    void StopTimer();

    /// <summary>
    /// Clears the timer cache to force recalculation
    /// </summary>
    void ClearCache();

    /// <summary>
    /// Event raised when the timer ticks (every second)
    /// </summary>
    event EventHandler? TimerTick;
}