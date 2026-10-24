using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingNaturalSlopeBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("NaturalMarketSlope")]
    public string Operation { get; set; } = "NaturalMarketSlope";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "NaturalMarketSlope" => new NaturalMarketSlope(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
