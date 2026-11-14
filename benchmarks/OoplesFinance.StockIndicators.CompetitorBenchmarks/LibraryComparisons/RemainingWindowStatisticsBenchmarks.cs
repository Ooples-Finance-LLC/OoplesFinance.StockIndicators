using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingWindowStatisticsBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("Skewness", "PercentRank", "MedianValue", "MedianMa")]
    public string Operation { get; set; } = "Skewness";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "Skewness" => new Skewness(14),
        "PercentRank" => new PercentRank(100),
        "MedianValue" => new MedianValue(14),
        "MedianMa" => new MedianMa(14),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
