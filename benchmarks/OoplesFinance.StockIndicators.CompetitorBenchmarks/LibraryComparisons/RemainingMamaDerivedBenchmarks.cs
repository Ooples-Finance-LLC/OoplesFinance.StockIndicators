using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingMamaDerivedBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("EhlersSineWaveIndicatorV1", "EhlersHilbertOscillator", "EhlersInstantaneousTrendlineV1", "EhlersSineWaveIndicatorV2")]
    public string Operation { get; set; } = "EhlersSineWaveIndicatorV1";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "EhlersSineWaveIndicatorV1" => new EhlersSineWaveIndicatorV1(),
        "EhlersHilbertOscillator" => new EhlersHilbertOscillator(),
        "EhlersInstantaneousTrendlineV1" => new EhlersInstantaneousTrendlineV1(),
        "EhlersSineWaveIndicatorV2" => new EhlersSineWaveIndicatorV2(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
