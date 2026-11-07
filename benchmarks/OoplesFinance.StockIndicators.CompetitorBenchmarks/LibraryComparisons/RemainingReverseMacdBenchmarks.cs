using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingReverseMacdBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("ReverseMovingAverageConvergenceDivergence")]
    public string Operation { get; set; } = "ReverseMovingAverageConvergenceDivergence";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "ReverseMovingAverageConvergenceDivergence" => new ReverseMovingAverageConvergenceDivergence(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
