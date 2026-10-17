using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingWeightedFilterBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("Spencer15PointMovingAverage", "Spencer21PointMovingAverage", "DampedSineWaveWeightedFilter", "HendersonWeightedMovingAverage", "RightSidedRickerMovingAverage")]
    public string Operation { get; set; } = "Spencer15PointMovingAverage";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "Spencer15PointMovingAverage" => new Spencer15PointMovingAverage(20),
        "Spencer21PointMovingAverage" => new Spencer21PointMovingAverage(20),
        "DampedSineWaveWeightedFilter" => new DampedSineWaveWeightedFilter(20),
        "HendersonWeightedMovingAverage" => new HendersonWeightedMovingAverage(20),
        "RightSidedRickerMovingAverage" => new RightSidedRickerMovingAverage(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
