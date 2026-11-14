using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingFloorAliasesBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("FloorPivotPointS1", "FloorPivotPointR1", "PivotPoint")]
    public string Operation { get; set; } = "FloorPivotPointS1";
    protected override DateTime GetTime(int index) => new DateTime(2026, 1, 1).AddHours(index);
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "FloorPivotPointS1" => new FloorPivotPointS1(),
        "FloorPivotPointR1" => new FloorPivotPointR1(),
        "PivotPoint" => new PivotPoint(14),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
