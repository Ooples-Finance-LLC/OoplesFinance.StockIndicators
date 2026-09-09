using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Builder;

/// <summary>
/// Uniquely identifies a data series by symbol and timeframe.
/// </summary>
public readonly struct SeriesKey : IEquatable<SeriesKey>
{
    /// <summary>
    /// Creates a new series key.
    /// </summary>
    /// <param name="symbol">The symbol identifier.</param>
    /// <param name="timeframe">The bar timeframe.</param>
    public SeriesKey(SymbolId symbol, BarTimeframe timeframe)
    {
        Symbol = symbol;
        Timeframe = timeframe;
    }

    /// <summary>
    /// Gets the symbol identifier.
    /// </summary>
    public SymbolId Symbol { get; }

    /// <summary>
    /// Gets the bar timeframe.
    /// </summary>
    public BarTimeframe Timeframe { get; }

    /// <summary>
    /// Determines whether this key equals another key.
    /// </summary>
    public bool Equals(SeriesKey other)
    {
        return Symbol.Equals(other.Symbol) && Equals(Timeframe, other.Timeframe);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is SeriesKey other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            var hash = Symbol.GetHashCode();
            hash = (hash * 397) ^ (Timeframe?.GetHashCode() ?? 0);
            return hash;
        }
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"{Symbol}:{Timeframe}";
    }

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(SeriesKey left, SeriesKey right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(SeriesKey left, SeriesKey right)
    {
        return !left.Equals(right);
    }
}
