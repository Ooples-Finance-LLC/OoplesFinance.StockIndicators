using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Preserve extended-range lag correction and each published weighted stage.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingChainedAverageBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("ZeroLowLagMovingAverage", "ZeroLagTripleExponentialMovingAverage", "SlowSmoothedMovingAverage")]
    public string Operation { get; set; } = "ZeroLowLagMovingAverage";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "ZeroLowLagMovingAverage" => new ZeroLowLagMovingAverage(20),
        "ZeroLagTripleExponentialMovingAverage" => new ZeroLagTripleExponentialMovingAverage(20),
        "SlowSmoothedMovingAverage" => new SlowSmoothedMovingAverage(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
