using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingOtherStopsBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("MotionToAttractionTrailingStop", "ElderSafeZoneStops")]
    public string Operation { get; set; } = "MotionToAttractionTrailingStop";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "MotionToAttractionTrailingStop" => new MotionToAttractionTrailingStop(100),
        "ElderSafeZoneStops" => new ElderSafeZoneStops(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
