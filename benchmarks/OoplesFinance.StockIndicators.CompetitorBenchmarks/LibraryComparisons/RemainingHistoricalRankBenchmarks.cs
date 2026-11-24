using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingHistoricalRankBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("HistoricalVolatilityPercentile")]
    public string Operation { get; set; } = "HistoricalVolatilityPercentile";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "HistoricalVolatilityPercentile" => new HistoricalVolatilityPercentile(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
