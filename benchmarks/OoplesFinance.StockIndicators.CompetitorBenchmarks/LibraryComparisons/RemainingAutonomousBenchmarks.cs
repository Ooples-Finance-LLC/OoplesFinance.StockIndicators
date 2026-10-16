using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingAutonomousBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("AutonomousRecursiveMa", "AdaptiveAutonomousRecursiveMovingAverage", "AdaptiveAutonomousRecursiveTrailingStop")]
    public string Operation { get; set; } = "AutonomousRecursiveMa";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "AutonomousRecursiveMa" => new AutonomousRecursiveMa(20),
        "AdaptiveAutonomousRecursiveMovingAverage" => new AdaptiveAutonomousRecursiveMovingAverage(20),
        "AdaptiveAutonomousRecursiveTrailingStop" => new AdaptiveAutonomousRecursiveTrailingStop(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
