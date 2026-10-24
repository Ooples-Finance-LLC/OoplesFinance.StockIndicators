using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingModifiedMotionBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("ModifiedGannHiloActivator", "MorphedSineWave", "MoveTracker")]
    public string Operation { get; set; } = "ModifiedGannHiloActivator";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "ModifiedGannHiloActivator" => new ModifiedGannHiloActivator(3),
"MorphedSineWave" => new MorphedSineWave(),
"MoveTracker" => new MoveTracker(14),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
