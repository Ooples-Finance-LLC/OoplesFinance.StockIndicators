using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingAtrFilterBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("AtrFilteredEma", "AtrFilteredExponentialMovingAverage", "AdaptiveLeastSquares")]
    public string Operation { get; set; } = "AtrFilteredEma";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "AtrFilteredEma" => new AtrFilteredEma(20),
        "AtrFilteredExponentialMovingAverage" => new AtrFilteredExponentialMovingAverage(20),
        "AdaptiveLeastSquares" => new AdaptiveLeastSquares(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
