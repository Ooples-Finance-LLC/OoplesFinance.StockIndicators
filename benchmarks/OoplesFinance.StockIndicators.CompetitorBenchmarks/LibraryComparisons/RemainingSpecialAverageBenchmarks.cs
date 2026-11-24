using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingSpecialAverageBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("EhlersBetterExponentialMovingAverage", "HoltExponentialMovingAverage", "RegularizedEma", "QuickMovingAverage")]
    public string Operation { get; set; } = "EhlersBetterExponentialMovingAverage";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "EhlersBetterExponentialMovingAverage" => new EhlersBetterExponentialMovingAverage(20),
        "HoltExponentialMovingAverage" => new HoltExponentialMovingAverage(20),
        "RegularizedEma" => new RegularizedEma(20),
        "QuickMovingAverage" => new QuickMovingAverage(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
