using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Preserve chronological deviation rounding and historical-volatility startup.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingOrderedVolatilityBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("StdDev", "HistoricalVolatility")]
    public string Operation { get; set; } = "StdDev";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "StdDev" => new StdDev(20),
        "HistoricalVolatility" => new HistoricalVolatility(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
