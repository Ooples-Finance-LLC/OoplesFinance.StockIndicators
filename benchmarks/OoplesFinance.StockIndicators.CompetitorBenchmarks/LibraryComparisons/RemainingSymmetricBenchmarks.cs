using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingSymmetricBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("SymmetricallyWeightedMovingAverage", "EhlersTriangleMovingAverage", "EhlersTriangleWindowIndicator")]
    public string Operation { get; set; } = "SymmetricallyWeightedMovingAverage";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "SymmetricallyWeightedMovingAverage" => new SymmetricallyWeightedMovingAverage(20),
        "EhlersTriangleMovingAverage" => new EhlersTriangleMovingAverage(20),
        "EhlersTriangleWindowIndicator" => new EhlersTriangleWindowIndicator(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
