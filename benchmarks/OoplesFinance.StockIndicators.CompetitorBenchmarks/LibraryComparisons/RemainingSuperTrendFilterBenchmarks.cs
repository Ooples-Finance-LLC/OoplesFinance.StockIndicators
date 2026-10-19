using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingSuperTrendFilterBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("SuperTrendFilter")]
    public string Operation { get; set; } = "SuperTrendFilter";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "SuperTrendFilter" => new SuperTrendFilter(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
