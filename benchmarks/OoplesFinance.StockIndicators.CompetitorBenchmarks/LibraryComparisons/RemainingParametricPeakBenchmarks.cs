using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingParametricPeakBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("ParametricCorrectiveLinearMovingAverage", "ParametricKalmanFilter", "PeakValleyEstimation")]
    public string Operation { get; set; } = "ParametricCorrectiveLinearMovingAverage";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "ParametricCorrectiveLinearMovingAverage" => new ParametricCorrectiveLinearMovingAverage(),
"ParametricKalmanFilter" => new ParametricKalmanFilter(),
"PeakValleyEstimation" => new PeakValleyEstimation(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
