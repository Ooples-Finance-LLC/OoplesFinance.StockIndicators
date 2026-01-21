using OoplesFinance.StockIndicators.Enums;

namespace OoplesFinance.StockIndicators.Builder;

/// <summary>
/// Named key for looking up indicators by name and output.
/// </summary>
public readonly struct IndicatorKey : IEquatable<IndicatorKey>
{
    /// <summary>
    /// Creates a new indicator key.
    /// </summary>
    /// <param name="name">The indicator name.</param>
    /// <param name="output">The output type.</param>
    public IndicatorKey(IndicatorName name, IndicatorOutput output = IndicatorOutput.Primary)
    {
        Name = name;
        Output = output;
    }

    /// <summary>
    /// Gets the indicator name.
    /// </summary>
    public IndicatorName Name { get; }

    /// <summary>
    /// Gets the output type.
    /// </summary>
    public IndicatorOutput Output { get; }

    /// <summary>
    /// Creates an indicator key from a name and output.
    /// </summary>
    public static IndicatorKey From(IndicatorName name, IndicatorOutput output = IndicatorOutput.Primary)
    {
        return new IndicatorKey(name, output);
    }

    /// <summary>
    /// Gets the SMA indicator key.
    /// </summary>
    public static IndicatorKey Sma => new(IndicatorName.SimpleMovingAverage, IndicatorOutput.Primary);

    /// <summary>
    /// Gets the RSI indicator key.
    /// </summary>
    public static IndicatorKey Rsi => new(IndicatorName.RelativeStrengthIndex, IndicatorOutput.Primary);

    /// <summary>
    /// Gets the MACD primary indicator key.
    /// </summary>
    public static IndicatorKey Macd => new(IndicatorName.MovingAverageConvergenceDivergence, IndicatorOutput.Primary);

    /// <summary>
    /// Gets the MACD signal line indicator key.
    /// </summary>
    public static IndicatorKey MacdSignal => new(IndicatorName.MovingAverageConvergenceDivergence, IndicatorOutput.Signal);

    /// <summary>
    /// Gets the MACD histogram indicator key.
    /// </summary>
    public static IndicatorKey MacdHistogram => new(IndicatorName.MovingAverageConvergenceDivergence, IndicatorOutput.Histogram);

    /// <summary>
    /// Gets the Bollinger Bands upper band indicator key.
    /// </summary>
    public static IndicatorKey BollingerUpper => new(IndicatorName.BollingerBands, IndicatorOutput.UpperBand);

    /// <summary>
    /// Gets the Bollinger Bands middle band indicator key.
    /// </summary>
    public static IndicatorKey BollingerMiddle => new(IndicatorName.BollingerBands, IndicatorOutput.MiddleBand);

    /// <summary>
    /// Gets the Bollinger Bands lower band indicator key.
    /// </summary>
    public static IndicatorKey BollingerLower => new(IndicatorName.BollingerBands, IndicatorOutput.LowerBand);

    /// <summary>
    /// Determines whether this key equals another key.
    /// </summary>
    public bool Equals(IndicatorKey other)
    {
        return Name == other.Name && Output == other.Output;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is IndicatorKey other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            return ((int)Name * 397) ^ (int)Output;
        }
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"{Name}/{Output}";
    }

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(IndicatorKey left, IndicatorKey right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(IndicatorKey left, IndicatorKey right)
    {
        return !left.Equals(right);
    }
}
