using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingVolumeBalanceBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("UpsideDownsideVolume", "TFSVolumeOscillator", "VolumeAccumulationOscillator")]
    public string Operation { get; set; } = "UpsideDownsideVolume";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "UpsideDownsideVolume" => new UpsideDownsideVolume(50),
        "TFSVolumeOscillator" => new TFSVolumeOscillator(7),
        "VolumeAccumulationOscillator" => new VolumeAccumulationOscillator(14),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
