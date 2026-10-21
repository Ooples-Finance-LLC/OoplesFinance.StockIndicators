using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingStochasticComponentsBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("StochasticCustomOscillator", "StochasticMomentumIndex")]
    public string Operation { get; set; } = "StochasticCustomOscillator";
    protected override DateTime GetTime(int index) => new DateTime(2026, 1, 1).AddHours(index);
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "StochasticCustomOscillator" => new StochasticCustomOscillator(100),
        "StochasticMomentumIndex" => new StochasticMomentumIndex(100),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
