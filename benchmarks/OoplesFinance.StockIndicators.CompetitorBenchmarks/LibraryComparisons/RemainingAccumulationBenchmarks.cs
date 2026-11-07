using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Verify cumulative flow, smoothing and raw ease against their legacy routes.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingAccumulationBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("AccumulationDistributionLine", "EaseOfMovement", "WilliamsAccumulationDistribution", "SmoothedWilliamsAccumulationDistribution")]
    public string Operation { get; set; } = "AccumulationDistributionLine";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "AccumulationDistributionLine" => new AccumulationDistributionLine(20),
        "EaseOfMovement" => new EaseOfMovement(20),
        "WilliamsAccumulationDistribution" => new WilliamsAccumulationDistribution(),
        "SmoothedWilliamsAccumulationDistribution" => new SmoothedWilliamsAccumulationDistribution(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
