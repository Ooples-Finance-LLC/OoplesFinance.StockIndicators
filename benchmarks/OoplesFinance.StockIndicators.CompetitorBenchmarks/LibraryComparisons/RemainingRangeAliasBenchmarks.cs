using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingRangeAliasBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("AverageTrueRange", "ChandeMomentumOscillator", "RateOfChange", "DetrendedPriceOscillator")]
    public string Operation { get; set; } = "AverageTrueRange";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "AverageTrueRange" => new AverageTrueRange(20),
        "ChandeMomentumOscillator" => new ChandeMomentumOscillator(20),
        "RateOfChange" => new RateOfChange(20),
        "DetrendedPriceOscillator" => new DetrendedPriceOscillator(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
