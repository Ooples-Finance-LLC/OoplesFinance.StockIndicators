using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingAdaptiveFitBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("FisherLeastSquaresMovingAverage", "TStepLeastSquaresMovingAverage")]
    public string Operation { get; set; } = "FisherLeastSquaresMovingAverage";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "FisherLeastSquaresMovingAverage" => new FisherLeastSquaresMovingAverage(20),
        "TStepLeastSquaresMovingAverage" => new TStepLeastSquaresMovingAverage(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
