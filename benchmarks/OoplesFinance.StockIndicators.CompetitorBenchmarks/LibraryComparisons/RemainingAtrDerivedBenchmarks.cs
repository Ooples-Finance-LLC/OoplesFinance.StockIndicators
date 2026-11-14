using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Compare generated NATR/ATR percent/channel width/Chaikin volatility against their own legacy route, preserving
// each staged averaging operation. No native startup convention is substituted.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingAtrDerivedBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("Natr", "AtrPercent", "AtrChannelWidth", "ChaikinVolatility")]
    public string Operation { get; set; } = "Natr";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "Natr" => new Natr(20),
        "AtrPercent" => new AtrPercent(20),
        "AtrChannelWidth" => new AtrChannelWidth(20),
        "ChaikinVolatility" => new ChaikinVolatility(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
