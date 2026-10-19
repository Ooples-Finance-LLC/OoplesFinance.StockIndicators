using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingRetentionRobustBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("RetentionAccelerationFilter", "RobustWeightingOscillator")]
    public string Operation { get; set; } = "RetentionAccelerationFilter";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "RetentionAccelerationFilter" => new RetentionAccelerationFilter(),
"RobustWeightingOscillator" => new RobustWeightingOscillator(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
