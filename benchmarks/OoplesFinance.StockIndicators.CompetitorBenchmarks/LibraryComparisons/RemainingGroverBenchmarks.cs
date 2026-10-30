using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingGroverBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("GroverLlorensActivator", "GroverLlorensCycleOscillator")]
    public string Operation { get; set; } = "GroverLlorensActivator";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "GroverLlorensActivator" => new GroverLlorensActivator(),
        "GroverLlorensCycleOscillator" => new GroverLlorensCycleOscillator(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
