using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedSkenderStochasticBenchmarks : SharedSkenderBenchmarkBase
{
    private const int Period = 20;
    [Params("Skender.GetStoch", "Skender.GetStochRsi", "Skender.GetSmi")]
    public string Operation { get; set; } = "";
    private IEnumerable<Quote>[] _quotePrefixes = null!;
    private (DateTime Date, double Value)[] _prices = null!;
    private IEnumerable<(DateTime Date, double Value)>[] _prefixes = null!;

    [GlobalSetup]
    public void Setup()
    {
        if (Operation is not ("Skender.GetStoch" or "Skender.GetStochRsi" or "Skender.GetSmi"))
            throw new ArgumentException("Unknown Skender stochastic operation.");
        var (pair, data) = PrepareNativeFixture(Operation, Operation == "Skender.GetStoch" ? 17 : null);
        // Standard Stochastic has a finite 14+3+3-2 bar dependency.
        // RSI and SMI retain their full prefix for recursive smoothing.
        _quotePrefixes = _ends.Select((end, i) => _quotes.Skip(_seeds[i]).Take(end - _seeds[i])).ToArray();
        // The tuple overload is public and avoids repeated quote conversion.
        // Preserve the decimal-to-double values used by the quote-based oracle.
        _prices = _quotes.Select(quote => (quote.Date, (double)quote.Close)).ToArray();
        _prefixes = _ends.Select(end => _prices.Take(end)).ToArray();
        var expected = pair.Reference!(data, Period);
        SharedBenchmarkSupport.VerifyMappedOutputs(_bars, Create, expected, pair.OutputNames ?? ["Value"]);
        var nativeExpected = pair.CompetitorReference!(data, Period);
        VerifyNative(nativeExpected, NativeSingle(), NativeParallel(), Normalize, Operation, pair.ErrorBudget);
    }

    protected override IIndicator Create() => Operation switch
    {
        "Skender.GetStoch" => new WindowStochasticKdj(14, 3, 3),
        "Skender.GetStochRsi" => new SmoothedStochasticRsi(20, 20, 3, 1),
        "Skender.GetSmi" => new DoubleSmoothedStochasticMomentum(20, 25, 2, 3),
        _ => throw new InvalidOperationException("Unknown operation.")
    };

    [Benchmark(Baseline = true)]
    public object NativeSingle() => Operation switch
    {
        "Skender.GetStoch" => _quotes.GetStoch(14, 3, 3),
        "Skender.GetStochRsi" => _prices.GetStochRsi(20, 20, 3, 1),
        "Skender.GetSmi" => _quotes.GetSmi(20, 25, 2, 3),
        _ => throw new InvalidOperationException("Unknown operation.")
    };

    [Benchmark]
    public object NativeParallel() => Operation switch
    {
        "Skender.GetStoch" => Parallel(_quotePrefixes, static quotes => quotes.GetStoch(14, 3, 3)),
        "Skender.GetStochRsi" => Parallel(_prefixes, static prices => prices.GetStochRsi(20, 20, 3, 1)),
        "Skender.GetSmi" => Parallel(_quotePrefixes, static quotes => quotes.GetSmi(20, 25, 2, 3)),
        _ => throw new InvalidOperationException("Unknown operation.")
    };

    internal static ComparisonSeries Normalize(object result) => result switch
    {
        IReadOnlyList<StochResult> rows => WindowStochasticComparison.Series(
            [rows.Select(v => v.Oscillator).ToArray(), rows.Select(v => v.Signal).ToArray(), rows.Select(v => v.PercentJ).ToArray()]),
        IReadOnlyList<StochRsiResult> rows => RetrospectivePriceComparison.Series(StochasticRsiComparison.Names(true),
            [rows.Select(v => v.StochRsi).ToArray(), rows.Select(v => v.Signal).ToArray()]),
        IReadOnlyList<SmiResult> rows => RetrospectivePriceComparison.Series(StochasticMomentumComparison.Names("Skender"),
            [rows.Select(v => v.Smi).ToArray(), rows.Select(v => v.Signal).ToArray()]),
        _ => throw new ArgumentException("Unexpected native result type.", nameof(result))
    };

}
