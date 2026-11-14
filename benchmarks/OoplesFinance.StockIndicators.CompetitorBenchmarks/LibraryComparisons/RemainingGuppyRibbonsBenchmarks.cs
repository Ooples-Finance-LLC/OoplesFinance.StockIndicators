using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingGuppyRibbonsBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("GuppyDistanceIndicator", "GuppyMultipleMovingAverage")]
    public string Operation { get; set; } = "GuppyDistanceIndicator";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "GuppyDistanceIndicator" => new GuppyDistanceIndicator(),
        "GuppyMultipleMovingAverage" => new GuppyMultipleMovingAverage(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
