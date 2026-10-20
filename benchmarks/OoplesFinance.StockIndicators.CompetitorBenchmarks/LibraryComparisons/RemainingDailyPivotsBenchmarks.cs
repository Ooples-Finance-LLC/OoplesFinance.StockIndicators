using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingDailyPivotsBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("StandardPivotPoints", "DynamicPivotPoints")]
    public string Operation { get; set; } = "StandardPivotPoints";
    protected override DateTime GetTime(int index) => new DateTime(2026, 1, 1).AddHours(index);
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "StandardPivotPoints" => new StandardPivotPoints(),
        "DynamicPivotPoints" => new DynamicPivotPoints(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
