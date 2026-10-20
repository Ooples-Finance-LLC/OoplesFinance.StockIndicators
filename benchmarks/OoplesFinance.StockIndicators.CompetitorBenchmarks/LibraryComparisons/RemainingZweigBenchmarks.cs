using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingZweigBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("ZweigMarketBreadthIndicator")]
    public string Operation { get; set; } = "ZweigMarketBreadthIndicator";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "ZweigMarketBreadthIndicator" => new ZweigMarketBreadthIndicator(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
