using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingPercentPerformanceBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("PercentChangeOscillator", "PerformanceIndex")]
    public string Operation { get; set; } = "PercentChangeOscillator";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "PercentChangeOscillator" => new PercentChangeOscillator(),
"PerformanceIndex" => new PerformanceIndex(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
