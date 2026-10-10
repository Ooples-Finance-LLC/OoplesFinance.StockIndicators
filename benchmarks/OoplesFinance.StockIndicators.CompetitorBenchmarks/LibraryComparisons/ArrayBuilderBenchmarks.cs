using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Controlled same-process comparison of bulk ingestion and the retained scalar
// source path. Every arm owns its results; both builders include the full lifecycle.
[MemoryDiagnoser, Config(typeof(CpuFeasibilityConfig))]
public class ArrayBuilderBenchmarks
{
    [Params("TaLib.Functions.Asin", "TaLib.Functions.Sma")]
    public string PairId { get; set; } = "";
    [Params(1000, 10000)] public int Count { get; set; }
    private CpuNativeWorkload _work = null!;

    [GlobalSetup]
    public void Setup()
    {
        _work = new CpuNativeWorkload(PairId, Count, commonGrid: true);
        CpuBuilderWorkload.Verify(_work);
        var indicator = CpuBuilderWorkload.Create(PairId);
        using var direct = CpuBuilderWorkload.Build(_work, indicator).GetAwaiter().GetResult();
        using var projected = BuildProjected(indicator).GetAwaiter().GetResult();
        foreach (var output in indicator.Outputs)
            AsinFeasibilityBenchmarks.RequireSame(direct[output].ToArray(), projected[output].ToArray());
    }

    private Task<IIndicatorRun> BuildProjected(IIndicator indicator) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(_work.Data.IndicatorBars, bar => bar))
            .ConfigureIndicators(indicator).BuildAsync();

    [Benchmark] public async Task<int> ArrayBuilder()
    {
        using var run = await CpuBuilderWorkload.Build(_work);
        return run.BarCount;
    }

    [Benchmark] public async Task<int> ProjectedBuilder()
    {
        using var run = await BuildProjected(CpuBuilderWorkload.Create(PairId));
        return run.BarCount;
    }

    [Benchmark(Baseline = true)] public object NativeOwned() => _work.NativeOwned();
}
