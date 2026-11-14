using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingVolumeOscillatorsBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("VolumeZoneOscillator", "VolumeMomentumOscillator")]
    public string Operation { get; set; } = "VolumeZoneOscillator";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "VolumeZoneOscillator" => new VolumeZoneOscillator(14),
        "VolumeMomentumOscillator" => new VolumeMomentumOscillator(5, 20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
