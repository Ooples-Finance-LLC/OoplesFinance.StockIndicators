using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedSkenderCandleBenchmarks : SharedSkenderBenchmarkBase
{
    [Params("Skender.GetDoji", "Skender.GetMarubozu", "Skender.GetHeikinAshi")]
    public string Operation { get; set; } = "";
    private IEnumerable<Quote>[] _inputs = null!;

    [GlobalSetup]
    public void Setup()
    {
        if (Operation is not ("Skender.GetDoji" or "Skender.GetMarubozu" or "Skender.GetHeikinAshi"))
            throw new ArgumentException("Unknown Skender candle operation.");
        // Doji and Marubozu are pointwise; Heikin-Ashi open is recursive.
        var (pair, data) = PrepareNativeFixture(Operation, Operation == "Skender.GetHeikinAshi" ? null : 0);
        _inputs = _ends.Select((end, i) => _quotes.Skip(_seeds[i]).Take(end - _seeds[i])).ToArray();
        var owner = Create();
        double[][] expected;
        if (owner is PriceCandlePattern pattern)
            expected = SharedPriceCandleBenchmarks.Reference(_bars, pattern);
        else
        {
            var reference = pair.Reference!(data, 20);
            expected = pair.OutputNames!.Select(name => reference.Outputs[name].Values).ToArray();
        }
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            using var run = SharedBenchmarkSupport.BuildCpu(_bars, owner, mode);
            for (int slot = 0; slot < expected.Length; slot++)
                AsinFeasibilityBenchmarks.RequireSame(expected[slot], run[owner.Outputs[slot]].ToArray());
        }
        VerifyNative(pair.CompetitorReference!(data, 20), NativeSingle(), NativeParallel(), Normalize, Operation, pair.ErrorBudget);
    }

    protected override IIndicator Create() => Operation == "Skender.GetHeikinAshi"
        ? new HeikinAshiCandles()
        : new PriceCandlePattern(Operation == "Skender.GetDoji" ? PriceCandleKind.RelativeDoji : PriceCandleKind.Marubozu);

    [Benchmark(Baseline = true)]
    public object NativeSingle() => Operation switch
    {
        "Skender.GetDoji" => _quotes.GetDoji(),
        "Skender.GetMarubozu" => _quotes.GetMarubozu(),
        "Skender.GetHeikinAshi" => _quotes.GetHeikinAshi(),
        _ => throw new InvalidOperationException("Unknown operation.")
    };

    [Benchmark]
    public object NativeParallel() => Operation switch
    {
        "Skender.GetDoji" => Parallel(_inputs, static quotes => quotes.GetDoji()),
        "Skender.GetMarubozu" => Parallel(_inputs, static quotes => quotes.GetMarubozu()),
        "Skender.GetHeikinAshi" => Parallel(_inputs, static quotes => quotes.GetHeikinAshi()),
        _ => throw new InvalidOperationException("Unknown operation.")
    };

    internal static ComparisonSeries Normalize(object result) => result switch
    {
        IReadOnlyList<CandleResult> rows => PriceCandleComparison.Normalize(rows),
        IReadOnlyList<HeikinAshiResult> rows => HeikinAshiComparison.Normalize(rows),
        _ => throw new ArgumentException("Unexpected native result type.", nameof(result))
    };
}
