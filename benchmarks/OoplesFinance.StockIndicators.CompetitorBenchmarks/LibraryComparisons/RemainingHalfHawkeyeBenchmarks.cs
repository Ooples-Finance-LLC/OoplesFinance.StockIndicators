using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingHalfHawkeyeBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("HalfTrend", "HawkeyeVolumeIndicator")]
    public string Operation { get; set; } = "HalfTrend";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "HalfTrend" => new HalfTrend(),
        "HawkeyeVolumeIndicator" => new HawkeyeVolumeIndicator(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
