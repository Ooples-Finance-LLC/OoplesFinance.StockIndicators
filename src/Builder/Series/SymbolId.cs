namespace OoplesFinance.StockIndicators.Builder;

/// <summary>
/// Type-safe symbol identifier.
/// </summary>
public readonly struct SymbolId : IEquatable<SymbolId>
{
    /// <summary>
    /// Creates a new symbol identifier.
    /// </summary>
    /// <param name="value">The symbol string value.</param>
    public SymbolId(string value)
    {
        Value = value ?? string.Empty;
    }

    /// <summary>
    /// Gets the symbol string value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Creates a symbol identifier from a string.
    /// </summary>
    public static SymbolId From(string value)
    {
        return new SymbolId(value);
    }

    /// <summary>
    /// Apple Inc. symbol.
    /// </summary>
    public static SymbolId Aapl => new("AAPL");

    /// <summary>
    /// Microsoft Corp. symbol.
    /// </summary>
    public static SymbolId Msft => new("MSFT");

    /// <summary>
    /// Alphabet Inc. symbol.
    /// </summary>
    public static SymbolId Goog => new("GOOG");

    /// <summary>
    /// Amazon.com Inc. symbol.
    /// </summary>
    public static SymbolId Amzn => new("AMZN");

    /// <summary>
    /// Tesla Inc. symbol.
    /// </summary>
    public static SymbolId Tsla => new("TSLA");

    /// <summary>
    /// NVIDIA Corp. symbol.
    /// </summary>
    public static SymbolId Nvda => new("NVDA");

    /// <summary>
    /// Meta Platforms Inc. symbol.
    /// </summary>
    public static SymbolId Meta => new("META");

    /// <summary>
    /// SPY ETF symbol.
    /// </summary>
    public static SymbolId Spy => new("SPY");

    /// <summary>
    /// QQQ ETF symbol.
    /// </summary>
    public static SymbolId Qqq => new("QQQ");

    /// <summary>
    /// Determines whether this symbol equals another symbol.
    /// </summary>
    public bool Equals(SymbolId other)
    {
        return string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is SymbolId other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return StringComparer.OrdinalIgnoreCase.GetHashCode(Value);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Value;
    }

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(SymbolId left, SymbolId right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(SymbolId left, SymbolId right)
    {
        return !left.Equals(right);
    }

    /// <summary>
    /// Implicit conversion from string to SymbolId.
    /// </summary>
    public static implicit operator SymbolId(string value)
    {
        return new SymbolId(value);
    }

    /// <summary>
    /// Implicit conversion from SymbolId to string.
    /// </summary>
    public static implicit operator string(SymbolId symbol)
    {
        return symbol.Value;
    }
}

/// <summary>
/// Default symbol collections.
/// </summary>
public static class SymbolDefaults
{
    private static readonly SymbolId[] AllUsSymbols = { SymbolId.Aapl, SymbolId.Msft, SymbolId.Goog, SymbolId.Amzn, SymbolId.Tsla, SymbolId.Nvda, SymbolId.Meta };

    /// <summary>
    /// Gets all US market symbols.
    /// </summary>
    public static IReadOnlyList<SymbolId> AllUs => AllUsSymbols;

    /// <summary>
    /// Gets all available symbols.
    /// </summary>
    public static IReadOnlyList<SymbolId> All => AllUsSymbols;
}
