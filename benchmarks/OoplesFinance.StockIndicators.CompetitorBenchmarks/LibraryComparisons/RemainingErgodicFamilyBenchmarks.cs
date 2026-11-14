using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingErgodicFamilyBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("ErgodicCandlestickOscillator", "ErgodicMovingAverageConvergenceDivergence", "ErgodicPercentagePriceOscillator", "ErgodicTrueStrengthIndexV2")]
    public string Operation { get; set; } = "ErgodicCandlestickOscillator";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "ErgodicCandlestickOscillator" => new ErgodicCandlestickOscillator(),
        "ErgodicMovingAverageConvergenceDivergence" => new ErgodicMovingAverageConvergenceDivergence(),
        "ErgodicPercentagePriceOscillator" => new ErgodicPercentagePriceOscillator(),
        "ErgodicTrueStrengthIndexV2" => new ErgodicTrueStrengthIndexV2(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
