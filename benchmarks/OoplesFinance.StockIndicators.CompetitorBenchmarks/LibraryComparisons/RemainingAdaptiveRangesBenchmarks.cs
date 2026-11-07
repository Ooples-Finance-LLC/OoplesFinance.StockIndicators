using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingAdaptiveRangesBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("Ama", "AdaptiveRsi", "AdaptiveRelativeStrengthIndex", "AsymmetricalRsi", "AsymmetricalRelativeStrengthIndex")]
    public string Operation { get; set; } = "Ama";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "Ama" => new Ama(20),
        "AdaptiveRsi" => new AdaptiveRsi(20),
        "AdaptiveRelativeStrengthIndex" => new AdaptiveRelativeStrengthIndex(20),
        "AsymmetricalRsi" => new AsymmetricalRsi(20),
        "AsymmetricalRelativeStrengthIndex" => new AsymmetricalRelativeStrengthIndex(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
