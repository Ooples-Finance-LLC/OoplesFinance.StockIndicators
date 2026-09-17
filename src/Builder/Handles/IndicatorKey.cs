namespace OoplesFinance.StockIndicators.Builder;

/// <summary>
/// Named label for looking one of a builder's indicator handles up again.
/// </summary>
/// <remarks>
/// <para>
/// The label is the caller's, not the resolver's: <c>StockIndicatorBuilder.AddIndicator</c> stores it against
/// the handle and never consults it when computing anything. It still has to tell one handle of an indicator
/// from another, though - a MACD line and its signal line are two handles of one indicator, and a label that
/// could not separate them would have the second quietly overwrite the first.
/// </para>
/// <para>
/// It used to carry an <c>IndicatorOutput</c> slot for that. The slots were assigned positionally and ran out,
/// so they could not name every output; the published key does. See issue #219.
/// </para>
/// </remarks>
public readonly struct IndicatorKey : IEquatable<IndicatorKey>
{
    public IndicatorKey(IndicatorName name, string? outputKey = null)
    {
        Name = name;
        OutputKey = outputKey;
    }

    public IndicatorName Name { get; }

    /// <summary>The published output this label stands for, or null for the indicator's own series.</summary>
    public string? OutputKey { get; }

    public static IndicatorKey From(IndicatorName name, string? outputKey = null)
    {
        return new IndicatorKey(name, outputKey);
    }

    // Common built-in labels for convenience
    public static IndicatorKey Sma => new(IndicatorName.SimpleMovingAverage);
    public static IndicatorKey Rsi => new(IndicatorName.RelativeStrengthIndex);
    public static IndicatorKey Macd => new(IndicatorName.MovingAverageConvergenceDivergence, "Macd");
    public static IndicatorKey MacdSignal => new(IndicatorName.MovingAverageConvergenceDivergence, "Signal");
    public static IndicatorKey MacdHistogram => new(IndicatorName.MovingAverageConvergenceDivergence, "Histogram");
    public static IndicatorKey BollingerUpper => new(IndicatorName.BollingerBands, "UpperBand");
    public static IndicatorKey BollingerMiddle => new(IndicatorName.BollingerBands, "MiddleBand");
    public static IndicatorKey BollingerLower => new(IndicatorName.BollingerBands, "LowerBand");

    public bool Equals(IndicatorKey other)
    {
        return Name == other.Name && string.Equals(OutputKey, other.OutputKey, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj)
    {
        return obj is IndicatorKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = (hash * 31) + (int)Name;
            hash = (hash * 31) + (OutputKey is null ? 0 : StringComparer.Ordinal.GetHashCode(OutputKey));
            return hash;
        }
    }

    public override string ToString()
    {
        return OutputKey is null ? Name.ToString() : $"{Name}/{OutputKey}";
    }

    public static bool operator ==(IndicatorKey left, IndicatorKey right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(IndicatorKey left, IndicatorKey right)
    {
        return !left.Equals(right);
    }
}
