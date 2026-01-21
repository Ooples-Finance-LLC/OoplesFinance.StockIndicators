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
    public SignalWindow(int bars, TimeSpan? duration = null)
    {
        Bars = Math.Max(1, bars);
        Duration = duration;
    }

    /// <summary>
    /// Gets the number of bars.
    /// </summary>
    public int Bars { get; }

    /// <summary>
    /// Gets the duration (optional).
    /// </summary>
    public TimeSpan? Duration { get; }

    /// <summary>
    /// Creates a window from a number of bars.
    /// </summary>
    public static SignalWindow FromBars(int bars)
    {
        return new SignalWindow(bars, null);
    }

    /// <summary>
    /// Creates a window from minutes.
    /// </summary>
    public static SignalWindow Minutes(int minutes)
    {
        return new SignalWindow(1, TimeSpan.FromMinutes(minutes));
    }

    /// <summary>
    /// Creates a window from hours.
    /// </summary>
    public static SignalWindow Hours(int hours)
    {
        return new SignalWindow(1, TimeSpan.FromHours(hours));
    }
}
