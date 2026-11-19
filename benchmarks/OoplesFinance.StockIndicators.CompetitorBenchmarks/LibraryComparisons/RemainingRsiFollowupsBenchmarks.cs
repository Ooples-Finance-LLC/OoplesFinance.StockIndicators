using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingRsiFollowupsBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("RapidRelativeStrengthIndex", "StochRsi", "StochasticRsiOscillator", "StochasticRelativeStrengthIndex")]
    public string Operation { get; set; } = "RapidRelativeStrengthIndex";
    protected override DateTime GetTime(int index) => new DateTime(2026, 1, 1).AddHours(index);
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "RapidRelativeStrengthIndex" => new RapidRelativeStrengthIndex(100),
        "StochRsi" => new StochRsi(100, 75),
        "StochasticRsiOscillator" => new StochasticRsiOscillator(100, 75),
        "StochasticRelativeStrengthIndex" => new StochasticRelativeStrengthIndex(100),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
