using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Share Klinger force/EMA stages and exact ultimate-oscillator pressure windows.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingPressureFlowBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("Kvo", "KlingerVolumeOscillator", "UltimateOscillator")]
    public string Operation { get; set; } = "Kvo";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "Kvo" => new Kvo(20),
        "KlingerVolumeOscillator" => new KlingerVolumeOscillator(20, 55, 13),
        "UltimateOscillator" => new UltimateOscillator(7, 14, 28),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
