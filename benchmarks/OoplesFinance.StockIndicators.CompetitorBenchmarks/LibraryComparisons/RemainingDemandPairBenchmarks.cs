using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingDemandPairBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("DemandIndex", "DemandOscillator")]
    public string Operation { get; set; } = "DemandIndex";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "DemandIndex" => new DemandIndex(14),
        "DemandOscillator" => new DemandOscillator(14),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
