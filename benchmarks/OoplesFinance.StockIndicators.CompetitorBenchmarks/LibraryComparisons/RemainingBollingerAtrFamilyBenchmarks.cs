using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingBollingerAtrFamilyBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("BollingerBandsMiddle", "BollingerBandsAtr", "BollingerBandsAvgTrueRange", "BollingerBandsFibonacciRatios", "BollingerBandsWithAtrPct")]
    public string Operation { get; set; } = "BollingerBandsMiddle";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "BollingerBandsMiddle" => new BollingerBandsMiddle(),
        "BollingerBandsAtr" => new BollingerBandsAtr(),
        "BollingerBandsAvgTrueRange" => new BollingerBandsAvgTrueRange(22, 55, 2, Enums.MovingAvgType.SimpleMovingAverage),
        "BollingerBandsFibonacciRatios" => new BollingerBandsFibonacciRatios(),
        "BollingerBandsWithAtrPct" => new BollingerBandsWithAtrPct(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
