using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class ValuesBuilderBenchmarks
{
    public static IEnumerable<string> Cases => Environment.GetEnvironmentVariable("COMPARISON_PAIR") is { } id
        ? CpuKernelPilots.Ids.Where(value => value == id) : CpuKernelPilots.Ids;
    [ParamsSource(nameof(Cases))] public string PairId { get; set; } = "";
    private CpuNativeWorkload _work = null!;
    [GlobalSetup]
    public void Setup()
    {
        _work = new CpuNativeWorkload(PairId, 10_000, commonGrid: true);
        CpuBuilderWorkload.Verify(_work);
        var indicator = CpuBuilderWorkload.Create(PairId);
        using var expected = CpuBuilderWorkload.Build(_work, indicator).GetAwaiter().GetResult();
        var actual = Builder(indicator).ConfigureHistory(IndicatorHistoryMode.LatestOnly).BuildAsync().GetAwaiter().GetResult();
        foreach (var output in indicator.Outputs)
            AsinFeasibilityBenchmarks.RequireSame(expected[output].ToArray(), actual[output].ToArray());
    }
    private StockIndicatorBuilder Builder(IIndicator indicator) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(_work.Data.IndicatorBars)).ConfigureIndicators(indicator);
    [Benchmark(Baseline = true)] public int WithSnapshots()
    {
        using var run = Builder(CpuBuilderWorkload.Create(PairId)).BuildAsync().GetAwaiter().GetResult();
        return run.BarCount;
    }
    [Benchmark] public int ValuesOnly() => Builder(CpuBuilderWorkload.Create(PairId))
        .ConfigureHistory(IndicatorHistoryMode.LatestOnly).BuildAsync().GetAwaiter().GetResult().BarCount;
}
