using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingVolumeAdjustedBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("VolumeAdjustedMa", "VolumeAdjustedMovingAverage")]
    public string Operation { get; set; } = "VolumeAdjustedMa";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "VolumeAdjustedMa" => new VolumeAdjustedMa(20),
        "VolumeAdjustedMovingAverage" => new VolumeAdjustedMovingAverage(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
