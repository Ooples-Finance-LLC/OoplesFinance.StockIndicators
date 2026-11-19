using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingDynamicFiltersBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("DynamicSupportAndResistance", "DynamicallyAdjustableFilter", "DynamicallyAdjustableMovingAverage")]
    public string Operation { get; set; } = "DynamicSupportAndResistance";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "DynamicSupportAndResistance" => new DynamicSupportAndResistance(),
        "DynamicallyAdjustableFilter" => new DynamicallyAdjustableFilter(),
        "DynamicallyAdjustableMovingAverage" => new DynamicallyAdjustableMovingAverage(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
