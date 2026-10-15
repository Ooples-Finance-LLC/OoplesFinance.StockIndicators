using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedSkenderStochasticBenchmarks : SharedMappedBenchmarkBase
{
    private const int Period = 20;
    [Params("Skender.GetStoch", "Skender.GetStochRsi", "Skender.GetSmi")]
    public string Operation { get; set; } = "";
    private List<Quote> _quotes = null!;
    private IEnumerable<Quote>[] _quotePrefixes = null!;
    private (DateTime Date, double Value)[] _prices = null!;
    private IEnumerable<(DateTime Date, double Value)>[] _prefixes = null!;
    private int[] _ends = null!;
    private int[] _seeds = null!;

    [GlobalSetup]
    public void Setup()
    {
        if (Operation is not ("Skender.GetStoch" or "Skender.GetStochRsi" or "Skender.GetSmi"))
            throw new ArgumentException("Unknown Skender stochastic operation.");
        if (Count < 1) throw new ArgumentOutOfRangeException(nameof(Count));
        var (pair, data) = PrepareFixture(Operation);
        _quotes = data.Quotes;
        int chunks = Math.Min(Count, Math.Min(8, Environment.ProcessorCount));
        _ends = Enumerable.Range(1, chunks).Select(i => (int)((long)Count * i / chunks)).ToArray();
        // Standard Stochastic has a finite 14+3+3-2 bar dependency.
        // RSI and SMI retain their full prefix for recursive smoothing.
        _seeds = Enumerable.Range(0, chunks).Select(i => Operation == "Skender.GetStoch"
            ? Math.Max(0, (i == 0 ? 0 : _ends[i - 1]) - 17) : 0).ToArray();
        _quotePrefixes = _ends.Select((end, i) => _quotes.Skip(_seeds[i]).Take(end - _seeds[i])).ToArray();
        // The tuple overload is public and avoids repeated quote conversion.
        // Preserve the decimal-to-double values used by the quote-based oracle.
        _prices = _quotes.Select(quote => (quote.Date, (double)quote.Close)).ToArray();
        _prefixes = _ends.Select(end => _prices.Take(end)).ToArray();
        var expected = pair.Reference!(data, Period);
        SharedBenchmarkSupport.VerifyMappedOutputs(_bars, Create, expected, pair.OutputNames ?? ["Value"]);
        var nativeExpected = pair.CompetitorReference!(data, Period);
        var serial = NativeSingle();
        var parallel = NativeParallel();
        ComparisonVerifier.Compare(nativeExpected, Normalize(serial), Operation + " direct native serial", pair.ErrorBudget);
        ComparisonVerifier.Compare(nativeExpected, Normalize(parallel), Operation + " direct native parallel", pair.ErrorBudget);
        RequireDates(serial);
        RequireDates(parallel);
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

    private T[] Parallel<TInput, T>(IEnumerable<TInput>[] prefixes, Func<IEnumerable<TInput>, IEnumerable<T>> calculate)
    {
        var result = new T[Count];
        CpuParallelSettings.LightweightParallel(_ends.Length, _ends.Length, chunk =>
        {
            int start = chunk == 0 ? 0 : _ends[chunk - 1], end = _ends[chunk];
            // Skender 2.7.3 eagerly returns List<T>; do not charge an avoidable
            // intermediate array copy to its serial or parallel native path.
            var prefix = (List<T>)calculate(prefixes[chunk]);
            if (prefix.Count != end - _seeds[chunk]) throw new InvalidOperationException("Native input range length differs.");
            prefix.CopyTo(start - _seeds[chunk], result, start, end - start);
        });
        return result;
    }

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

    private void RequireDates(object result)
    {
        var dates = result switch
        {
            IReadOnlyList<StochResult> rows => rows.Select(row => row.Date),
            IReadOnlyList<StochRsiResult> rows => rows.Select(row => row.Date),
            IReadOnlyList<SmiResult> rows => rows.Select(row => row.Date),
            _ => throw new ArgumentException("Unexpected native result type.", nameof(result))
        };
        if (!dates.SequenceEqual(_quotes.Select(quote => quote.Date)))
            throw new InvalidOperationException("Native result dates differ from the fixture.");
    }
}
