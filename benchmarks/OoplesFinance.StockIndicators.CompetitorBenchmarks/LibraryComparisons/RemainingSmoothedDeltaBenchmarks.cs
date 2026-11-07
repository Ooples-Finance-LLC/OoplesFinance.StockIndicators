using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingSmoothedDeltaBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("SmoothedDeltaRatioOscillator")]
    public string Operation { get; set; } = "SmoothedDeltaRatioOscillator";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "SmoothedDeltaRatioOscillator" => new SmoothedDeltaRatioOscillator(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
