using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingEhlersModifiedMomentumBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("EhlersModifiedRelativeStrengthIndex", "EhlersModifiedStochasticIndicator", "EhlersRocketRelativeStrengthIndex", "EhlersMovingAverageDifferenceIndicator")]
    public string Operation { get; set; } = "EhlersModifiedRelativeStrengthIndex";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "EhlersModifiedRelativeStrengthIndex" => new EhlersModifiedRelativeStrengthIndex(),
        "EhlersModifiedStochasticIndicator" => new EhlersModifiedStochasticIndicator(),
        "EhlersRocketRelativeStrengthIndex" => new EhlersRocketRelativeStrengthIndex(),
        "EhlersMovingAverageDifferenceIndicator" => new EhlersMovingAverageDifferenceIndicator(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
