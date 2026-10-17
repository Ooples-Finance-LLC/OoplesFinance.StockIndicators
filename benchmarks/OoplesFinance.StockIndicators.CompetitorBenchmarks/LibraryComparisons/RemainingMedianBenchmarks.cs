using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingMedianBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("EhlersRecursiveMedianFilter", "EhlersRecursiveMedianOscillator", "EhlersMedianAverageAdaptiveFilter")]
    public string Operation { get; set; } = "EhlersRecursiveMedianFilter";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "EhlersRecursiveMedianFilter" => new EhlersRecursiveMedianFilter(),
        "EhlersRecursiveMedianOscillator" => new EhlersRecursiveMedianOscillator(),
        "EhlersMedianAverageAdaptiveFilter" => new EhlersMedianAverageAdaptiveFilter(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
