using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Compare each generated price-average alias with its own legacy route.
// Distinct OHLC fields prevent an accidental field substitution from passing setup.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingPriceAverageBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("AveragePrice", "MidRange", "HlcAverage", "OhlcAverage")]
    public string Operation { get; set; } = "AveragePrice";
    protected override double OpenOffset => -.75;

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "AveragePrice" => new AveragePrice(20),
        "MidRange" => new MidRange(),
        "HlcAverage" => new HlcAverage(),
        "OhlcAverage" => new OhlcAverage(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
