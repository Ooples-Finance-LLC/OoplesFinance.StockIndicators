using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingMirroredPairBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("MirroredMovingAverageConvergenceDivergence", "MirroredPercentagePriceOscillator")]
    public string Operation { get; set; } = "MirroredMovingAverageConvergenceDivergence";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "MirroredMovingAverageConvergenceDivergence" => new MirroredMovingAverageConvergenceDivergence(),
"MirroredPercentagePriceOscillator" => new MirroredPercentagePriceOscillator(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
