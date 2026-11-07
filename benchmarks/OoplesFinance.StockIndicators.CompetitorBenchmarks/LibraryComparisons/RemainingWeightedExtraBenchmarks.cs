using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingWeightedExtraBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("RepulsionMovingAverage", "CompoundRatioMovingAverage")]
    public string Operation { get; set; } = "RepulsionMovingAverage";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "RepulsionMovingAverage" => new RepulsionMovingAverage(20),
        "CompoundRatioMovingAverage" => new CompoundRatioMovingAverage(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
