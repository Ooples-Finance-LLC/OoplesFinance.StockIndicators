using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingOptimizedTrendBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("OptimizedTrendTracker")]
    public string Operation { get; set; } = "OptimizedTrendTracker";
    protected override DateTime GetTime(int index) => new DateTime(2026, 1, 1).AddHours(index);
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "OptimizedTrendTracker" => new OptimizedTrendTracker(30, 1.4),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
