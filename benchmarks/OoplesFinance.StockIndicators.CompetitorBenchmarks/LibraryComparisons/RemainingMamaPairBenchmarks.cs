using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingMamaPairBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("FollowingAdaptiveMovingAverage", "EhlersMesaAdaptiveMovingAverage")]
    public string Operation { get; set; } = "EhlersMesaAdaptiveMovingAverage";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "FollowingAdaptiveMovingAverage" => new FollowingAdaptiveMovingAverage(20),
        "EhlersMesaAdaptiveMovingAverage" => new EhlersMesaAdaptiveMovingAverage(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
