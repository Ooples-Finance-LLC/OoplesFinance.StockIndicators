using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingResidualFitBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("R2AdaptiveRegression", "KaufmanAdaptiveLeastSquaresMovingAverage", "RegressionOscillator", "KaufmanAdaptiveCorrelationOscillator")]
    public string Operation { get; set; } = "R2AdaptiveRegression";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "R2AdaptiveRegression" => new R2AdaptiveRegression(20),
        "KaufmanAdaptiveLeastSquaresMovingAverage" => new KaufmanAdaptiveLeastSquaresMovingAverage(20),
        "RegressionOscillator" => new RegressionOscillator(20),
        "KaufmanAdaptiveCorrelationOscillator" => new KaufmanAdaptiveCorrelationOscillator(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
