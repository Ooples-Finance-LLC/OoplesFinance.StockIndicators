using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Measures the public dependency graph, including source ownership and output
// publication. The projected arm uses the ordinary graph execution path.
[MemoryDiagnoser, Config(typeof(CpuFeasibilityConfig))]
public class SmaFusionBenchmarks
{
    [Params(1000, 10000)] public int Count { get; set; }
    private Bar[] _bars = null!;

    [GlobalSetup]
    public void Setup()
    {
        _bars = Enumerable.Range(0, Count).Select(i =>
        {
            var value = (i % 127 - 63) / 64d;
            return new Bar(default, value, value, value, value, 1);
        }).ToArray();
        var average = new Sma(20);
        var asin = new PriceCircularTransform(PriceCircularOperation.ArcSine);
        asin.Of(average);
        using var fused = Build(asin, false).GetAwaiter().GetResult();
        using var ordinary = Build(asin, true).GetAwaiter().GetResult();
        foreach (var output in asin.Outputs)
            AsinFeasibilityBenchmarks.RequireSame(ordinary[output].ToArray(), fused[output].ToArray());
    }

    private Task<IIndicatorRun> Build(IIndicator indicator, bool projected) =>
        new StockIndicatorBuilder().ConfigureSource(projected ? Bars.From(_bars, bar => bar) : Bars.From(_bars))
            .ConfigureIndicators(indicator).BuildAsync();

    private async Task<int> Run(bool projected)
    {
        var average = new Sma(20);
        var asin = new PriceCircularTransform(PriceCircularOperation.ArcSine);
        asin.Of(average);
        using var run = await Build(asin, projected);
        return run.BarCount;
    }

    [Benchmark] public Task<int> Fused() => Run(false);
    [Benchmark(Baseline = true)] public Task<int> Ordinary() => Run(true);
}
