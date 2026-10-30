using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingDynamicMomentumBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("DynamicMomentumIndex", "DynamicMomentumOscillator")]
    public string Operation { get; set; } = "DynamicMomentumIndex";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "DynamicMomentumIndex" => new DynamicMomentumIndex(14),
        "DynamicMomentumOscillator" => new DynamicMomentumOscillator(10),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
