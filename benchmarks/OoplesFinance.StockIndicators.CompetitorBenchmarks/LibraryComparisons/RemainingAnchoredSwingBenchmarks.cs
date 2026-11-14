using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingAnchoredSwingBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("AnchoredMomentum", "AccumulativeSwingIndex")]
    public string Operation { get; set; } = "AnchoredMomentum";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "AnchoredMomentum" => new AnchoredMomentum(10),
        "AccumulativeSwingIndex" => new AccumulativeSwingIndex(0, 14),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
