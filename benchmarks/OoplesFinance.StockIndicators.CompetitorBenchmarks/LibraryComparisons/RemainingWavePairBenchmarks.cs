using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingWavePairBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("ElliottWaveOscillator", "EmaWaveIndicator")]
    public string Operation { get; set; } = "ElliottWaveOscillator";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "ElliottWaveOscillator" => new ElliottWaveOscillator(),
        "EmaWaveIndicator" => new EmaWaveIndicator(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
