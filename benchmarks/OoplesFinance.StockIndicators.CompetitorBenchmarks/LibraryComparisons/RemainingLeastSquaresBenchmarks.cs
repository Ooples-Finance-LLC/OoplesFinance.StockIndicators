using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingLeastSquaresBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("SimplifiedLeastSquaresMovingAverage", "LinearExtrapolation")]
    public string Operation { get; set; } = "SimplifiedLeastSquaresMovingAverage";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "SimplifiedLeastSquaresMovingAverage" => new SimplifiedLeastSquaresMovingAverage(20),
        "LinearExtrapolation" => new LinearExtrapolation(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
