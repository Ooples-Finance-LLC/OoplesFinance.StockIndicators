using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingEstimatorBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("TillsonIE2", "IIRLeastSquaresEstimate", "HullEstimate")]
    public string Operation { get; set; } = "TillsonIE2";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "TillsonIE2" => new TillsonIE2(20),
        "IIRLeastSquaresEstimate" => new IIRLeastSquaresEstimate(20),
        "HullEstimate" => new HullEstimate(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
