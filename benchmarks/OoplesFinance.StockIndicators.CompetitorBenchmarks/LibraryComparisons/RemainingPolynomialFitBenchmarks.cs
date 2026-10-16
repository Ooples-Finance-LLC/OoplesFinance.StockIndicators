using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingPolynomialFitBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("PolynomialLeastSquaresMovingAverage", "QuadraticRegression", "QuadraticLeastSquaresMovingAverage")]
    public string Operation { get; set; } = "PolynomialLeastSquaresMovingAverage";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "PolynomialLeastSquaresMovingAverage" => new PolynomialLeastSquaresMovingAverage(20),
        "QuadraticRegression" => new QuadraticRegression(20),
        "QuadraticLeastSquaresMovingAverage" => new QuadraticLeastSquaresMovingAverage(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
