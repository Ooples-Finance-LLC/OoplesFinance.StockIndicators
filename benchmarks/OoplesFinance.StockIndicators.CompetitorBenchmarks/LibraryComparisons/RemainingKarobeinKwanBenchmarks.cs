using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingKarobeinKwanBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("KarobeinOscillator", "KwanIndicator")]
    public string Operation { get; set; } = "KarobeinOscillator";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "KarobeinOscillator" => new KarobeinOscillator(),
        "KwanIndicator" => new KwanIndicator(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
