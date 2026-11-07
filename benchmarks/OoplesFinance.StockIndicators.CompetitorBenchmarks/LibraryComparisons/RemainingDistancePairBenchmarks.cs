using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingDistancePairBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("DistanceWeightedMovingAverage", "InverseDistanceWeightedMovingAverage")]
    public string Operation { get; set; } = "DistanceWeightedMovingAverage";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "DistanceWeightedMovingAverage" => new DistanceWeightedMovingAverage(14),
        "InverseDistanceWeightedMovingAverage" => new InverseDistanceWeightedMovingAverage(14),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
