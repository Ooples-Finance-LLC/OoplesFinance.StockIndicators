using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingSurfaceRoughnessBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("SurfaceRoughnessEstimator")]
    public string Operation { get; set; } = "SurfaceRoughnessEstimator";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "SurfaceRoughnessEstimator" => new SurfaceRoughnessEstimator(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
