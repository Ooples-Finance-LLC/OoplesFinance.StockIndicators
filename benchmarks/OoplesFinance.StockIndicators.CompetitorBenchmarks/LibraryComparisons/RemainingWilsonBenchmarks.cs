using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingWilsonBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("WilsonRelativePriceChannel")]
    public string Operation { get; set; } = "WilsonRelativePriceChannel";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "WilsonRelativePriceChannel" => new WilsonRelativePriceChannel(34, 1),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
