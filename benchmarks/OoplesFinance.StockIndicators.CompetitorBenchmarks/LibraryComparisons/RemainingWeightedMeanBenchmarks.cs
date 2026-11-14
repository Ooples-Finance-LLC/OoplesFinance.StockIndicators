using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingWeightedMeanBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("FibonacciWeightedMovingAverage", "SquareRootWeightedMovingAverage", "SineWma")]
    public string Operation { get; set; } = "FibonacciWeightedMovingAverage";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "FibonacciWeightedMovingAverage" => new FibonacciWeightedMovingAverage(20),
        "SquareRootWeightedMovingAverage" => new SquareRootWeightedMovingAverage(20),
        "SineWma" => new SineWma(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
