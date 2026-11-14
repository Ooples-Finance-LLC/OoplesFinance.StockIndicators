using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingErgodicAdaptiveSelectionBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("AdaptiveErgodicCandlestickOscillator", "ErgodicCommoditySelectionIndex")]
    public string Operation { get; set; } = "AdaptiveErgodicCandlestickOscillator";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "AdaptiveErgodicCandlestickOscillator" => new AdaptiveErgodicCandlestickOscillator(),
        "ErgodicCommoditySelectionIndex" => new ErgodicCommoditySelectionIndex(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
