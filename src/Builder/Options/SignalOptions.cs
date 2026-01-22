namespace OoplesFinance.StockIndicators.Builder;

/// <summary>
/// Options for signal configuration.
/// </summary>
public sealed class SignalOptions
{
    /// <summary>
    /// Gets or sets the default signal window.
    /// </summary>
    public SignalWindow? DefaultWindow { get; set; }
}

/// <summary>
/// Represents a window of time or bars for signal evaluation.
/// </summary>
public readonly struct SignalWindow
{
    /// <summary>
    /// Creates a new signal window.
    /// </summary>
    public SignalWindow(int bars, TimeSpan? duration = null, int? validityBars = null, TimeSpan? validityDuration = null)
    {
        Bars = Math.Max(1, bars);
        Duration = duration;
        ValidityBars = validityBars;
        ValidityDuration = validityDuration;
    }

    /// <summary>
    /// Gets the number of bars for condition to be true.
    /// </summary>
    public int Bars { get; }

    /// <summary>
    /// Gets the duration for condition to be true (optional).
    /// </summary>
    public TimeSpan? Duration { get; }

    /// <summary>
    /// Gets the number of bars the signal remains valid after trigger.
    /// </summary>
    public int? ValidityBars { get; }

    /// <summary>
    /// Gets the duration the signal remains valid after trigger.
    /// </summary>
    public TimeSpan? ValidityDuration { get; }

    /// <summary>
    /// Creates a window from a number of bars.
    /// </summary>
    public static SignalWindow FromBars(int bars)
    {
        return new SignalWindow(bars, null, null, null);
    }

    /// <summary>
    /// Creates a window from a duration.
    /// </summary>
    public static SignalWindow FromDuration(TimeSpan duration)
    {
        return new SignalWindow(1, duration, null, null);
    }

    /// <summary>
    /// Creates a window from minutes.
    /// </summary>
    public static SignalWindow Minutes(int minutes)
    {
        return new SignalWindow(1, TimeSpan.FromMinutes(minutes), null, null);
    }

    /// <summary>
    /// Creates a window from hours.
    /// </summary>
    public static SignalWindow Hours(int hours)
    {
        return new SignalWindow(1, TimeSpan.FromHours(hours), null, null);
    }

    /// <summary>
    /// Creates a copy with the specified validity bars.
    /// </summary>
    public SignalWindow WithValidityBars(int bars)
    {
        return new SignalWindow(Bars, Duration, bars, ValidityDuration);
    }

    /// <summary>
    /// Creates a copy with the specified validity duration.
    /// </summary>
    public SignalWindow WithValidityDuration(TimeSpan duration)
    {
        return new SignalWindow(Bars, Duration, ValidityBars, duration);
    }
}
