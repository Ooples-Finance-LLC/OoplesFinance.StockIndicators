using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Validation;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedSkenderRocBenchmarks : SharedMappedBenchmarkBase
{
    private const int Period = 20;
    [Params("Skender.GetRoc", "Skender.GetRocWb")]
    public string Operation { get; set; } = "";
    private List<Quote> _quotes = null!;
    private (DateTime Date, double Value)[] _prices = null!;
    private IEnumerable<(DateTime Date, double Value)>[] _prefixes = null!;
    private int[] _ends = null!;
    private int[] _seeds = null!;

    [GlobalSetup]
    public void Setup()
    {
        if (Operation is not ("Skender.GetRoc" or "Skender.GetRocWb"))
            throw new ArgumentException("Unknown Skender ROC operation.");
        if (Count < 1) throw new ArgumentOutOfRangeException(nameof(Count));
        var (_, data) = PrepareFixture(Operation);
        _quotes = data.Quotes;
        int chunks = Math.Min(Count, Math.Min(8, Environment.ProcessorCount));
        _ends = Enumerable.Range(1, chunks).Select(i => (int)((long)Count * i / chunks)).ToArray();
        // ROC20 with SMA20 needs 39 preceding bars. ROC bands retain the
        // full prefix because their EMA is recursive.
        _seeds = Enumerable.Range(0, chunks).Select(i => Operation == "Skender.GetRoc"
            ? Math.Max(0, (i == 0 ? 0 : _ends[i - 1]) - 39) : 0).ToArray();
        _prices = _quotes.Select(quote => (quote.Date, (double)quote.Close)).ToArray();
        _prefixes = _ends.Select((end, i) => _prices.Skip(_seeds[i]).Take(end - _seeds[i])).ToArray();
        var owner = Create();
        var expected = SharedRocBenchmarks.Reference(_bars, Operation == "Skender.GetRocWb");
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            using var run = SharedBenchmarkSupport.BuildCpu(_bars, owner, mode);
            for (int slot = 0; slot < expected.Length; slot++)
                AsinFeasibilityBenchmarks.RequireSame(expected[slot], run[owner.Outputs[slot]].ToArray());
        }
        var nativeExpected = NativeReference(_prices.Select(v => v.Value).ToArray(), Operation == "Skender.GetRocWb");
        var serial = NativeSingle();
        var parallel = NativeParallel();
        ComparisonVerifier.Compare(nativeExpected, Normalize(serial), Operation + " direct native serial", IndicatorErrorBudget.Exact);
        ComparisonVerifier.Compare(nativeExpected, Normalize(parallel), Operation + " direct native parallel", IndicatorErrorBudget.Exact);
        RequireDates(serial);
        RequireDates(parallel);
    }

    protected override IIndicator Create() => Operation switch
    {
        "Skender.GetRoc" => new RateOfChangeWithAverage(Period, Period),
        "Skender.GetRocWb" => new RateOfChangeRmsBands(Period, 3, 3),
        _ => throw new InvalidOperationException("Unknown operation.")
    };

    [Benchmark(Baseline = true)]
    public object NativeSingle() => Operation switch
    {
        "Skender.GetRoc" => _prices.GetRoc(Period, Period),
        "Skender.GetRocWb" => _prices.GetRocWb(Period, 3, 3),
        _ => throw new InvalidOperationException("Unknown operation.")
    };

    [Benchmark]
    public object NativeParallel() => Operation switch
    {
        "Skender.GetRoc" => Parallel(_prefixes, static prices => prices.GetRoc(Period, Period)),
        "Skender.GetRocWb" => Parallel(_prefixes, static prices => prices.GetRocWb(Period, 3, 3)),
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
        IReadOnlyList<RocResult> rows => RetrospectivePriceComparison.Series(["Momentum", "Roc", "RocSma"],
            [rows.Select(v => v.Momentum).ToArray(), rows.Select(v => v.Roc).ToArray(), rows.Select(v => v.RocSma).ToArray()]),
        IReadOnlyList<RocWbResult> rows => RetrospectivePriceComparison.Series(["Roc", "RocEma", "UpperBand", "LowerBand"],
            [rows.Select(v => v.Roc).ToArray(), rows.Select(v => v.RocEma).ToArray(), rows.Select(v => v.UpperBand).ToArray(), rows.Select(v => v.LowerBand).ToArray()]),
        _ => throw new ArgumentException("Unexpected native result type.", nameof(result))
    };

    // Integer-grid operations independently round each native floating step.
    // Owned output references round their aggregate expressions instead.
    internal static ComparisonSeries NativeReference(double[] prices, bool bands)
    {
        var momentum = new double?[prices.Length];
        var roc = new double?[prices.Length];
        var average = new double?[prices.Length];
        var upper = new double?[prices.Length];
        var lower = new double?[prices.Length];
        for (int i = Period; i < prices.Length; i++)
        {
            momentum[i] = Subtract(prices[i], prices[i - Period]);
            if (!Units(prices[i - Period]).IsZero)
                roc[i] = Divide(Multiply(100, momentum[i]!.Value), prices[i - Period]);
        }
        int window = bands ? 3 : Period;
        for (int i = Period + window - 1; i < prices.Length; i++)
        {
            var values = roc.Skip(i - window + 1).Take(window).ToArray();
            if (bands && i > Period + window - 1)
            {
                if (average[i - 1].HasValue && roc[i].HasValue)
                    average[i] = Add(average[i - 1]!.Value,
                        Multiply(0.5, Subtract(roc[i]!.Value, average[i - 1]!.Value)));
            }
            else if (values.All(v => v.HasValue))
                average[i] = Divide(values.Aggregate(0d, (sum, v) => Add(sum, v!.Value)), window);
            if (!bands || values.Any(v => !v.HasValue)) continue;
            double squares = values.Aggregate(0d, (sum, v) => Add(sum, Multiply(v!.Value, v.Value)));
            upper[i] = Math.Sqrt(Divide(squares, window));
            lower[i] = -upper[i];
        }
        return bands
            ? RetrospectivePriceComparison.Series(["Roc", "RocEma", "UpperBand", "LowerBand"], [roc, average, upper, lower])
            : RetrospectivePriceComparison.Series(["Momentum", "Roc", "RocSma"], [momentum, roc, average]);
    }

    private void RequireDates(object result)
    {
        var dates = result switch
        {
            IReadOnlyList<RocResult> rows => rows.Select(row => row.Date),
            IReadOnlyList<RocWbResult> rows => rows.Select(row => row.Date),
            _ => throw new ArgumentException("Unexpected native result type.", nameof(result))
        };
        if (!dates.SequenceEqual(_quotes.Select(quote => quote.Date)))
            throw new InvalidOperationException("Native result dates differ from the fixture.");
    }
}
