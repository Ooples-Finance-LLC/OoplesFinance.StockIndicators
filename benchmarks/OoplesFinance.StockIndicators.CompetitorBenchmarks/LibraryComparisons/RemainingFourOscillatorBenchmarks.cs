using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingFourOscillatorBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("_4MovingAverageConvergenceDivergence", "_4PercentagePriceOscillator")]
    public string Operation { get; set; } = "_4MovingAverageConvergenceDivergence";
    protected override DateTime GetTime(int index) => new DateTime(2026, 1, 1).AddHours(index);
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "_4MovingAverageConvergenceDivergence" => new _4MovingAverageConvergenceDivergence(),
        "_4PercentagePriceOscillator" => new _4PercentagePriceOscillator(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
