using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Reuse the composed-average states and the extended-range Ehlers lag correction.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingExpandedAverageBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("HullMovingAverage", "TriangularMovingAverage", "EhlersZeroLagEma", "EhlersZeroLagExponentialMovingAverage")]
    public string Operation { get; set; } = "HullMovingAverage";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "HullMovingAverage" => new HullMovingAverage(20),
        "TriangularMovingAverage" => new TriangularMovingAverage(20),
        "EhlersZeroLagEma" => new EhlersZeroLagEma(20),
        "EhlersZeroLagExponentialMovingAverage" => new EhlersZeroLagExponentialMovingAverage(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
