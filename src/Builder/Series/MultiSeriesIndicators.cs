namespace OoplesFinance.StockIndicators.Builder;

/// <summary>
/// MACD multi-output series.
/// </summary>
public readonly struct MacdSeries
{
    /// <summary>
    /// Creates a new MACD series.
    /// </summary>
    public MacdSeries(SeriesHandle primary, SeriesHandle signal, SeriesHandle histogram)
    {
        Primary = primary;
        Signal = signal;
        Histogram = histogram;
    }

    /// <summary>
    /// Gets the primary MACD line.
    /// </summary>
    public SeriesHandle Primary { get; }

    /// <summary>
    /// Gets the signal line.
    /// </summary>
    public SeriesHandle Signal { get; }

    /// <summary>
    /// Gets the histogram.
    /// </summary>
    public SeriesHandle Histogram { get; }
}

/// <summary>
/// Bollinger Bands multi-output series.
/// </summary>
public readonly struct BollingerBandsSeries
{
    /// <summary>
    /// Creates a new Bollinger Bands series.
    /// </summary>
    public BollingerBandsSeries(SeriesHandle upper, SeriesHandle middle, SeriesHandle lower)
    {
        Upper = upper;
        Middle = middle;
        Lower = lower;
    }

    /// <summary>
    /// Gets the upper band.
    /// </summary>
    public SeriesHandle Upper { get; }

    /// <summary>
    /// Gets the middle band (SMA).
    /// </summary>
    public SeriesHandle Middle { get; }

    /// <summary>
    /// Gets the lower band.
    /// </summary>
    public SeriesHandle Lower { get; }
}

/// <summary>
/// Stochastic multi-output series.
/// </summary>
public readonly struct StochasticSeries
{
    /// <summary>
    /// Creates a new Stochastic series.
    /// </summary>
    public StochasticSeries(SeriesHandle k, SeriesHandle d)
    {
        K = k;
        D = d;
    }

    /// <summary>
    /// Gets the %K line.
    /// </summary>
    public SeriesHandle K { get; }

    /// <summary>
    /// Gets the %D line.
    /// </summary>
    public SeriesHandle D { get; }
}

/// <summary>
/// Aroon multi-output series.
/// </summary>
public readonly struct AroonSeries
{
    /// <summary>
    /// Creates a new Aroon series.
    /// </summary>
    public AroonSeries(SeriesHandle up, SeriesHandle down, SeriesHandle oscillator)
    {
        Up = up;
        Down = down;
        Oscillator = oscillator;
    }

    /// <summary>
    /// Gets the Aroon Up.
    /// </summary>
    public SeriesHandle Up { get; }

    /// <summary>
    /// Gets the Aroon Down.
    /// </summary>
    public SeriesHandle Down { get; }

    /// <summary>
    /// Gets the Aroon Oscillator.
    /// </summary>
    public SeriesHandle Oscillator { get; }
}

/// <summary>
/// Ichimoku multi-output series.
/// </summary>
public readonly struct IchimokuSeries
{
    /// <summary>
    /// Creates a new Ichimoku series.
    /// </summary>
    public IchimokuSeries(
        SeriesHandle tenkanSen,
        SeriesHandle kijunSen,
        SeriesHandle senkouSpanA,
        SeriesHandle senkouSpanB,
        SeriesHandle chikouSpan)
    {
        TenkanSen = tenkanSen;
        KijunSen = kijunSen;
        SenkouSpanA = senkouSpanA;
        SenkouSpanB = senkouSpanB;
        ChikouSpan = chikouSpan;
    }

    /// <summary>
    /// Gets the Tenkan-sen (Conversion Line).
    /// </summary>
    public SeriesHandle TenkanSen { get; }

    /// <summary>
    /// Gets the Kijun-sen (Base Line).
    /// </summary>
    public SeriesHandle KijunSen { get; }

    /// <summary>
    /// Gets the Senkou Span A (Leading Span A).
    /// </summary>
    public SeriesHandle SenkouSpanA { get; }

    /// <summary>
    /// Gets the Senkou Span B (Leading Span B).
    /// </summary>
    public SeriesHandle SenkouSpanB { get; }

    /// <summary>
    /// Gets the Chikou Span (Lagging Span).
    /// </summary>
    public SeriesHandle ChikouSpan { get; }
}

/// <summary>
/// Keltner Channel multi-output series.
/// </summary>
public readonly struct KeltnerChannelSeries
{
    /// <summary>
    /// Creates a new Keltner Channel series.
    /// </summary>
    public KeltnerChannelSeries(SeriesHandle upper, SeriesHandle middle, SeriesHandle lower)
    {
        Upper = upper;
        Middle = middle;
        Lower = lower;
    }

    /// <summary>
    /// Gets the upper channel.
    /// </summary>
    public SeriesHandle Upper { get; }

    /// <summary>
    /// Gets the middle line (EMA).
    /// </summary>
    public SeriesHandle Middle { get; }

    /// <summary>
    /// Gets the lower channel.
    /// </summary>
    public SeriesHandle Lower { get; }
}

/// <summary>
/// Donchian Channel multi-output series.
/// </summary>
public readonly struct DonchianChannelSeries
{
    /// <summary>
    /// Creates a new Donchian Channel series.
    /// </summary>
    public DonchianChannelSeries(SeriesHandle upper, SeriesHandle middle, SeriesHandle lower)
    {
        Upper = upper;
        Middle = middle;
        Lower = lower;
    }

    /// <summary>
    /// Gets the upper channel.
    /// </summary>
    public SeriesHandle Upper { get; }

    /// <summary>
    /// Gets the middle line.
    /// </summary>
    public SeriesHandle Middle { get; }

    /// <summary>
    /// Gets the lower channel.
    /// </summary>
    public SeriesHandle Lower { get; }
}
