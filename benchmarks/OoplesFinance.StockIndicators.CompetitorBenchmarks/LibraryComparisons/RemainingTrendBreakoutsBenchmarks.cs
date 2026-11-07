using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingTrendBreakoutsBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("TrendForceHistogram", "TrendImpulseFilter")]
    public string Operation { get; set; } = "TrendForceHistogram";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "TrendForceHistogram" => new TrendForceHistogram(),
"TrendImpulseFilter" => new TrendImpulseFilter(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
