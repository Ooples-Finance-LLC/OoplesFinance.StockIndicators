using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingWoodiePivotBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("WoodiePivotPoint")]
    public string Operation { get; set; } = "WoodiePivotPoint";
    protected override DateTime GetTime(int index) => new DateTime(2026, 1, 1).AddHours(index);
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "WoodiePivotPoint" => new WoodiePivotPoint(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
