using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Validation;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedSkenderRocBenchmarks : SharedSkenderBenchmarkBase
{
    private const int Period = 20;
    [Params("Skender.GetRoc", "Skender.GetRocWb")]
    public string Operation { get; set; } = "";
    private (DateTime Date, double Value)[] _prices = null!;
    private IEnumerable<(DateTime Date, double Value)>[] _prefixes = null!;

    [GlobalSetup]
    public void Setup()
    {
        if (Operation is not ("Skender.GetRoc" or "Skender.GetRocWb"))
            throw new ArgumentException("Unknown Skender ROC operation.");
        var (_, data) = PrepareNativeFixture(Operation, Operation == "Skender.GetRoc" ? 39 : null);
        // ROC20 with SMA20 needs 39 preceding bars. ROC bands retain the
        // full prefix because their EMA is recursive.
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
        VerifyNative(nativeExpected, NativeSingle(), NativeParallel(), Normalize, Operation, IndicatorErrorBudget.Exact);
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

}
