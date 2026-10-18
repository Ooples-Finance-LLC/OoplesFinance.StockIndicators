using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingSyntheticPriceBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("DetrendedSyntheticPrice")]
    public string Operation { get; set; } = "DetrendedSyntheticPrice";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "DetrendedSyntheticPrice" => new DetrendedSyntheticPrice(14),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
