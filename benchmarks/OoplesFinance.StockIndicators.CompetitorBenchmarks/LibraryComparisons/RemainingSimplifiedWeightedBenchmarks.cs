using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingSimplifiedWeightedBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("SimplifiedWeightedMovingAverage")]
    public string Operation { get; set; } = "SimplifiedWeightedMovingAverage";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "SimplifiedWeightedMovingAverage" => new SimplifiedWeightedMovingAverage(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
