using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingDiNapoliBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("DiNapoliMovingAverageConvergenceDivergence", "DiNapoliPercentagePriceOscillator", "DiNapoliPreferredStochasticOscillator")]
    public string Operation { get; set; } = "DiNapoliMovingAverageConvergenceDivergence";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "DiNapoliMovingAverageConvergenceDivergence" => new DiNapoliMovingAverageConvergenceDivergence(),
        "DiNapoliPercentagePriceOscillator" => new DiNapoliPercentagePriceOscillator(14),
        "DiNapoliPreferredStochasticOscillator" => new DiNapoliPreferredStochasticOscillator(8),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
