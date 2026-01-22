using OoplesFinance.StockIndicators.Enums;

namespace OoplesFinance.StockIndicators.Builder;

/// <summary>
/// Named key for looking up an indicator by its type and output.
/// </summary>
public readonly struct IndicatorKey : IEquatable<IndicatorKey>
{
    public IndicatorKey(IndicatorName name, IndicatorOutput output)
    {
        Name = name;
        Output = output;
    }

    public IndicatorName Name { get; }
    public IndicatorOutput Output { get; }

    public static IndicatorKey From(IndicatorName name, IndicatorOutput output = IndicatorOutput.Primary)
    {
        return new IndicatorKey(name, output);
    }

    // Common built-in keys for convenience
    public static IndicatorKey Sma => new(IndicatorName.SimpleMovingAverage, IndicatorOutput.Primary);
    public static IndicatorKey Rsi => new(IndicatorName.RelativeStrengthIndex, IndicatorOutput.Primary);
    public static IndicatorKey Macd => new(IndicatorName.MovingAverageConvergenceDivergence, IndicatorOutput.Primary);
    public static IndicatorKey MacdSignal => new(IndicatorName.MovingAverageConvergenceDivergence, IndicatorOutput.Signal);
    public static IndicatorKey MacdHistogram => new(IndicatorName.MovingAverageConvergenceDivergence, IndicatorOutput.Histogram);
    public static IndicatorKey BollingerUpper => new(IndicatorName.BollingerBands, IndicatorOutput.UpperBand);
    public static IndicatorKey BollingerMiddle => new(IndicatorName.BollingerBands, IndicatorOutput.MiddleBand);
    public static IndicatorKey BollingerLower => new(IndicatorName.BollingerBands, IndicatorOutput.LowerBand);

    public bool Equals(IndicatorKey other)
    {
        return Name == other.Name && Output == other.Output;
    }

    public override bool Equals(object? obj)
    {
        return obj is IndicatorKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + (int)Name;
            hash = hash * 31 + (int)Output;
            return hash;
        }
    }

    public override string ToString()
    {
        return $"{Name}/{Output}";
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
