using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingKasePeakBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("KasePeakOscillatorV1", "KasePeakOscillatorV2")]
    public string Operation { get; set; } = "KasePeakOscillatorV1";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "KasePeakOscillatorV1" => new KasePeakOscillatorV1(),
        "KasePeakOscillatorV2" => new KasePeakOscillatorV2(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
