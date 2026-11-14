using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingEdgeEfficiencyBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("EdgePreservingFilter", "EfficientAutoLine", "EfficientPrice")]
    public string Operation { get; set; } = "EdgePreservingFilter";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "EdgePreservingFilter" => new EdgePreservingFilter(),
        "EfficientAutoLine" => new EfficientAutoLine(),
        "EfficientPrice" => new EfficientPrice(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
