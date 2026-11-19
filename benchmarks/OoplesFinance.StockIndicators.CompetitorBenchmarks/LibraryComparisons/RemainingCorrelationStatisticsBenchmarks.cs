using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingCorrelationStatisticsBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("KendallRankCorrelationCoefficient", "RSquared")]
    public string Operation { get; set; } = "KendallRankCorrelationCoefficient";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "KendallRankCorrelationCoefficient" => new KendallRankCorrelationCoefficient(20),
        "RSquared" => new RSquared(14),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
