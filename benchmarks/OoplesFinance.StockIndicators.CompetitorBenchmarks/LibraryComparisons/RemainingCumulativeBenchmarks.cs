using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingCumulativeBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("CumulativeSum", "CumulativeVolumeIndex")]
    public string Operation { get; set; } = "CumulativeSum";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "CumulativeSum" => new CumulativeSum(),
        "CumulativeVolumeIndex" => new CumulativeVolumeIndex(14),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
