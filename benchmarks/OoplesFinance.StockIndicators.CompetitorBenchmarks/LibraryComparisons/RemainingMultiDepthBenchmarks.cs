using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingMultiDepthBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("MultiDepthZeroLagExponentialMovingAverage")]
    public string Operation { get; set; } = "MultiDepthZeroLagExponentialMovingAverage";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "MultiDepthZeroLagExponentialMovingAverage" => new MultiDepthZeroLagExponentialMovingAverage(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
