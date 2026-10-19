using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingStiffnessBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("StiffnessIndicator")]
    public string Operation { get; set; } = "StiffnessIndicator";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "StiffnessIndicator" => new StiffnessIndicator(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
