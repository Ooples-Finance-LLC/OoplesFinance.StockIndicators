namespace OoplesFinance.StockIndicators.Builder;

/// <summary>
/// Options for symbol configuration.
/// </summary>
public sealed class SymbolOptions
{
    /// <summary>
    /// Gets or sets the specific symbols to use.
    /// </summary>
    public IReadOnlyList<SymbolId>? Symbols { get; set; }

    /// <summary>
    /// Gets or sets the symbol universe.
    /// </summary>
    public SymbolUniverse? Universe { get; set; }

    /// <summary>
    /// Creates options for all US symbols.
    /// </summary>
    public static SymbolOptions AllUs()
    {
        return new SymbolOptions { Universe = SymbolUniverse.AllUs };
    }

    /// <summary>
    /// Creates options for specific symbols.
    /// </summary>
    public static SymbolOptions For(params SymbolId[] symbols)
    {
        return new SymbolOptions { Symbols = symbols };
    }

    /// <summary>
    /// Creates options for specific symbols by string.
    /// </summary>
    public static SymbolOptions For(params string[] symbols)
    {
        var symbolIds = new SymbolId[symbols.Length];
        for (var i = 0; i < symbols.Length; i++)
        {
            symbolIds[i] = SymbolId.From(symbols[i]);
        }
        return new SymbolOptions { Symbols = symbolIds };
    }
}
