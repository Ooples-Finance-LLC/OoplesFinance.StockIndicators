using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Preserve volume inputs, SMA/EMA conventions and independent price/volume lags.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingVolumeOscillatorBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("Pvo", "PercentageVolumeOscillator", "VolumeOscillator", "PriceVolumeOscillator")]
    public string Operation { get; set; } = "Pvo";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "Pvo" => new Pvo(20),
        "PercentageVolumeOscillator" => new PercentageVolumeOscillator(20),
        "VolumeOscillator" => new VolumeOscillator(20),
        "PriceVolumeOscillator" => new PriceVolumeOscillator(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
