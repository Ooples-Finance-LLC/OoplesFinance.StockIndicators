using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedSkenderWilliamsBenchmarks
{
    private const int Period = 20;
    [Params("Skender.GetWilliamsR")] public string Operation { get; set; } = "";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private List<Quote> _quotes = null!;
    private List<Quote>[] _chunks = null!;
    private int[] _skip = null!;
    private int _priorDop;
    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        var input = SkenderOhlcWindowBenchmarkInput.Create(Count, Period - 1);
        var data = input.Data;
        _quotes = data.Quotes; _bars = data.IndicatorBars; _chunks = input.Chunks; _skip = input.Skip;
        var pair = PriceWindowChannelComparison.Pairs.Single(p => p.Id == Operation);
        var native = pair.CompetitorReference!(data, Period).Outputs["Value"];
        var exact = pair.Reference!(data, Period).Outputs["Value"];
        CheckNative(NativeSingle(), native); CheckNative(NativeParallel(), native);
        var expected = exact.Values.ToArray();
        // The builder also publishes partial-window readings during native null startup.
        for (int i = 0; i < Period - 1; i++)
        {
            var high = Units(data.Highs.Take(i + 1).Max());
            var low = Units(data.Lows.Take(i + 1).Min());
            expected[i] = high == low ? -100 : Round(100 * (Units(data.Closes[i]) - high), high - low);
        }
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = new WilliamsR(Period); using var run = Build(indicator, history);
            AsinFeasibilityBenchmarks.RequireSame(expected, run[indicator].ToArray());
        }
    }
    private static void CheckNative(IReadOnlyList<WilliamsResult> actual, ComparisonOutput expected)
    {
        if (expected.FirstValid != Period - 1 || actual.Take(Period - 1).Any(r => r.WilliamsR.HasValue)
            || actual.Skip(Period - 1).Any(r => !r.WilliamsR.HasValue))
            throw new InvalidOperationException("Unexpected Williams startup/presence.");
        AsinFeasibilityBenchmarks.RequireSame(expected.Values.Skip(Period - 1).ToArray(), actual.Skip(Period - 1).Select(r => r.WilliamsR!.Value).ToArray());
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(new WilliamsR(Period), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(new WilliamsR(Period), IndicatorHistoryMode.LatestOnly);
    // Retain the eager result directly. Internal quote-to-double conversion and
    // stochastic calculation remain timed within the public native entry point.
    [Benchmark(Baseline = true)] public IReadOnlyList<WilliamsResult> NativeSingle() => (IReadOnlyList<WilliamsResult>)_quotes.GetWilliamsR(Period);
    [Benchmark] public IReadOnlyList<WilliamsResult> NativeParallel()
    {
        var result = new WilliamsResult[Count];
        CpuParallelSettings.LightweightParallel(_chunks.Length, _chunks.Length, worker =>
        {
            var local = (IReadOnlyList<WilliamsResult>)_chunks[worker].GetWilliamsR(Period);
            int start = Count * worker / _chunks.Length;
            for (int i = _skip[worker]; i < local.Count; i++) result[start + i - _skip[worker]] = local[i];
        });
        return result;
    }
    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history) => SharedBenchmarkSupport.BuildCpu(_bars, indicator, history);
}
