using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingImpulsePairBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("ImpulseMovingAverageConvergenceDivergence", "ImpulsePercentagePriceOscillator")]
    public string Operation { get; set; } = "ImpulseMovingAverageConvergenceDivergence";
    protected override DateTime GetTime(int index) => new DateTime(2026, 1, 1).AddHours(index);
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "ImpulseMovingAverageConvergenceDivergence" => new ImpulseMovingAverageConvergenceDivergence(34, 9),
        "ImpulsePercentagePriceOscillator" => new ImpulsePercentagePriceOscillator(34),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
