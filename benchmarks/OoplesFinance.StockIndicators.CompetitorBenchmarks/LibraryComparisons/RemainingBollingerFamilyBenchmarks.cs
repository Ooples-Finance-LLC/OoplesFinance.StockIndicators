using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingBollingerFamilyBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("BollingerBands", "BollingerBandsWidth", "BollingerBandsPercentB", "NarrowSidewaysChannel")]
    public string Operation { get; set; } = "BollingerBands";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "BollingerBands" => new BollingerBands(),
        "BollingerBandsWidth" => new BollingerBandsWidth(),
        "BollingerBandsPercentB" => new BollingerBandsPercentB(),
        "NarrowSidewaysChannel" => new NarrowSidewaysChannel(14, 3),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
