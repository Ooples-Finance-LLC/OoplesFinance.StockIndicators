using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingAdaptiveEmaBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("AdaptiveEma", "AdaptiveExponentialMovingAverage")]
    public string Operation { get; set; } = "AdaptiveEma";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "AdaptiveEma" => new AdaptiveEma(20),
        "AdaptiveExponentialMovingAverage" => new AdaptiveExponentialMovingAverage(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
