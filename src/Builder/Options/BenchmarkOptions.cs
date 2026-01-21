namespace OoplesFinance.StockIndicators.Builder;

/// <summary>
/// Options for benchmark comparison.
/// </summary>
public sealed class BenchmarkOptions
{
    /// <summary>
    /// Gets or sets the benchmark kind.
    /// </summary>
    public BenchmarkKind? Benchmark { get; set; }

    /// <summary>
    /// Gets or sets a custom benchmark symbol (when using Custom benchmark kind).
    /// </summary>
    public SymbolId? CustomSymbol { get; set; }

    /// <summary>
    /// Creates benchmark options for SPY.
    /// </summary>
    public static BenchmarkOptions Spy()
    {
        return new BenchmarkOptions { Benchmark = BenchmarkKind.Spy };
    }

    /// <summary>
    /// Creates benchmark options for QQQ.
    /// </summary>
    public static BenchmarkOptions Qqq()
    {
        return new BenchmarkOptions { Benchmark = BenchmarkKind.Qqq };
    }

    /// <summary>
    /// Creates benchmark options for a custom symbol.
    /// </summary>
    public static BenchmarkOptions Custom(SymbolId symbol)
    {
        return new BenchmarkOptions
        {
            Benchmark = BenchmarkKind.Custom,
            CustomSymbol = symbol
        };
    }
}
