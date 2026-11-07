using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingTrendPersistenceBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("TrendPersistenceRate")]
    public string Operation { get; set; } = "TrendPersistenceRate";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "TrendPersistenceRate" => new TrendPersistenceRate(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
