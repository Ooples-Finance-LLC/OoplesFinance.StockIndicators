using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingOscillatorAliasBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("AbsolutePriceOscillator", "PriceOscillator", "PriceOscillatorPercent", "PercentagePriceOscillator")]
    public string Operation { get; set; } = "AbsolutePriceOscillator";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "AbsolutePriceOscillator" => new AbsolutePriceOscillator(12, 26),
        "PriceOscillator" => new PriceOscillator(12, 26),
        "PriceOscillatorPercent" => new PriceOscillatorPercent(12, 26, 9),
        "PercentagePriceOscillator" => new PercentagePriceOscillator(12, 26, 9),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
