using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingFoldedFisherBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("FoldedRelativeStrengthIndex", "FisherTransformStochasticOscillator")]
    public string Operation { get; set; } = "FoldedRelativeStrengthIndex";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "FoldedRelativeStrengthIndex" => new FoldedRelativeStrengthIndex(),
        "FisherTransformStochasticOscillator" => new FisherTransformStochasticOscillator(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
