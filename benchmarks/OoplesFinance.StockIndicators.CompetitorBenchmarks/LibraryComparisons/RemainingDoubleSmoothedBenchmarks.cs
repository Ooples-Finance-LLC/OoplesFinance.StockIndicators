using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingDoubleSmoothedBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("DoubleSmoothedMomenta", "DoubleSmoothedRelativeStrengthIndex")]
    public string Operation { get; set; } = "DoubleSmoothedMomenta";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "DoubleSmoothedMomenta" => new DoubleSmoothedMomenta(),
        "DoubleSmoothedRelativeStrengthIndex" => new DoubleSmoothedRelativeStrengthIndex(14),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
