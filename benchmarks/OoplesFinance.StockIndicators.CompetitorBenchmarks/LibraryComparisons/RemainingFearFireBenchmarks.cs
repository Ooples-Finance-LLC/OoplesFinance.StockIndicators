using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingFearFireBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("FearAndGreedIndicator", "FireflyOscillator")]
    public string Operation { get; set; } = "FearAndGreedIndicator";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "FearAndGreedIndicator" => new FearAndGreedIndicator(),
        "FireflyOscillator" => new FireflyOscillator(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
