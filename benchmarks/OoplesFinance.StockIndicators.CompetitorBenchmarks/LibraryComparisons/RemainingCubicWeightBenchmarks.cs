using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingCubicWeightBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("CubedWeightedMovingAverage")]
    public string Operation { get; set; } = "CubedWeightedMovingAverage";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "CubedWeightedMovingAverage" => new CubedWeightedMovingAverage(14),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
