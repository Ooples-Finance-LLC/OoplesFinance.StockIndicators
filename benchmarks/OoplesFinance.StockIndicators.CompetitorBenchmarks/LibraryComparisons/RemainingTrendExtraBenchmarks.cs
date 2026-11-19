using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingTrendExtraBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("LightLeastSquaresMovingAverage", "LinearExtrapolation", "RecursiveMovingTrendAverage")]
    public string Operation { get; set; } = "LightLeastSquaresMovingAverage";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "LightLeastSquaresMovingAverage" => new LightLeastSquaresMovingAverage(20),
        "LinearExtrapolation" => new LinearExtrapolation(20),
        "RecursiveMovingTrendAverage" => new RecursiveMovingTrendAverage(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
