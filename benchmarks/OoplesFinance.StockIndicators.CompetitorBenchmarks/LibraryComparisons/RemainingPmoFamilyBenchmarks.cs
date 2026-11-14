using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingPmoFamilyBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("Pmo", "PriceMomentumOscillator", "DecisionPointPriceMomentumOscillator")]
    public string Operation { get; set; } = "Pmo";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "Pmo" => new Pmo(),
        "PriceMomentumOscillator" => new PriceMomentumOscillator(),
        "DecisionPointPriceMomentumOscillator" => new DecisionPointPriceMomentumOscillator(14),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
