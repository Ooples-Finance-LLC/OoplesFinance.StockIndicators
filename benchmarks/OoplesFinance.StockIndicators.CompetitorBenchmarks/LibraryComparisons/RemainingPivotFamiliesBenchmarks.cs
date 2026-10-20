using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingPivotFamiliesBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("CamarillaPivotPoint", "FibonacciPivotPoint", "DemarkPivotPoint", "FloorPivotPoint")]
    public string Operation { get; set; } = "CamarillaPivotPoint";
    protected override DateTime GetTime(int index) => new DateTime(2026, 1, 1).AddHours(index);
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "CamarillaPivotPoint" => new CamarillaPivotPoint(),
        "FibonacciPivotPoint" => new FibonacciPivotPoint(),
        "DemarkPivotPoint" => new DemarkPivotPoint(),
        "FloorPivotPoint" => new FloorPivotPoint(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
