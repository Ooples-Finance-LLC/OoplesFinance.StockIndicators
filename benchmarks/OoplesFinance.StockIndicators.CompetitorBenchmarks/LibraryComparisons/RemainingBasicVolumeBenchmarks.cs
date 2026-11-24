using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingBasicVolumeBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("NetVolume", "Range", "MarketFacilitationIndex")]
    public string Operation { get; set; } = "NetVolume";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "NetVolume" => new NetVolume(14),
        "Range" => new Indicators.Range(14),
        "MarketFacilitationIndex" => new MarketFacilitationIndex(14),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
