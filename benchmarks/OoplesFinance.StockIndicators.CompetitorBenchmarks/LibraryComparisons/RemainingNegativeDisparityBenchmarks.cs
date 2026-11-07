using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingNegativeDisparityBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("NegativeVolumeDisparityIndicator")]
    public string Operation { get; set; } = "NegativeVolumeDisparityIndicator";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "NegativeVolumeDisparityIndicator" => new NegativeVolumeDisparityIndicator(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
