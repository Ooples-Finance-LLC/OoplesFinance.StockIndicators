using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingWalkRainbowBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("RandomWalkIndex", "RSINGIndicator", "SellGravitationIndex", "RainbowOscillator")]
    public string Operation { get; set; } = "RandomWalkIndex";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "RandomWalkIndex" => new RandomWalkIndex(),
"RSINGIndicator" => new RSINGIndicator(),
"SellGravitationIndex" => new SellGravitationIndex(),
"RainbowOscillator" => new RainbowOscillator(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
