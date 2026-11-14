using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingVigorDifferenceBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("NormalizedRelativeVigorIndex", "NthOrderDifferencingOscillator")]
    public string Operation { get; set; } = "NormalizedRelativeVigorIndex";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "NormalizedRelativeVigorIndex" => new NormalizedRelativeVigorIndex(),
"NthOrderDifferencingOscillator" => new NthOrderDifferencingOscillator(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
