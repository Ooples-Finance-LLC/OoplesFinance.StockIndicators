using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingTrueRangeAdjustedBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("TrueRangeAdjustedExponentialMovingAverage")]
    public string Operation { get; set; } = "TrueRangeAdjustedExponentialMovingAverage";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "TrueRangeAdjustedExponentialMovingAverage" => new TrueRangeAdjustedExponentialMovingAverage(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
