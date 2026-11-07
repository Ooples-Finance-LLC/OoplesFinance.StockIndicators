using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingSuperTrendBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("SuperTrend")]
    public string Operation { get; set; } = "SuperTrend";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "SuperTrend" => new SuperTrend(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
