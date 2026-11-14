using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingStatisticalQualityBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("StatisticalVolatility", "VolatilityQualityIndex")]
    public string Operation { get; set; } = "StatisticalVolatility";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "StatisticalVolatility" => new StatisticalVolatility(),
        "VolatilityQualityIndex" => new VolatilityQualityIndex(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
