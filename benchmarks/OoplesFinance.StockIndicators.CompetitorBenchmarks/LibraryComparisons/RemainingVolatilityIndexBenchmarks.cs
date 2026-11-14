using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingVolatilityIndexBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("VolatilityIndexDynamicAverageIndicator", "ChandeVolatilityIndexDynamicAverageIndicator")]
    public string Operation { get; set; } = "VolatilityIndexDynamicAverageIndicator";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "VolatilityIndexDynamicAverageIndicator" => new VolatilityIndexDynamicAverageIndicator(),
        "ChandeVolatilityIndexDynamicAverageIndicator" => new ChandeVolatilityIndexDynamicAverageIndicator(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
