using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingUltimateAveragesBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("UltimateMovingAverage", "UltimateMovingAverageBands")]
    public string Operation { get; set; } = "UltimateMovingAverage";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "UltimateMovingAverage" => new UltimateMovingAverage(14),
        "UltimateMovingAverageBands" => new UltimateMovingAverageBands(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
