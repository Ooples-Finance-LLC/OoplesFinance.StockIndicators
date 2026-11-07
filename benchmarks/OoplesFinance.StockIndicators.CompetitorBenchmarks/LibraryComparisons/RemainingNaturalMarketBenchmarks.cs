using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingNaturalMarketBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("NaturalMarketMirror", "NaturalMarketRiver", "NaturalMarketCombo")]
    public string Operation { get; set; } = "NaturalMarketMirror";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "NaturalMarketMirror" => new NaturalMarketMirror(),
"NaturalMarketRiver" => new NaturalMarketRiver(),
"NaturalMarketCombo" => new NaturalMarketCombo(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
