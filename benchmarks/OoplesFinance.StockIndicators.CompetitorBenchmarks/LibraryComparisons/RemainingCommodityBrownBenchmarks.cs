using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingCommodityBrownBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("CommoditySelectionIndex", "ConstanceBrownCompositeIndex")]
    public string Operation { get; set; } = "CommoditySelectionIndex";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "CommoditySelectionIndex" => new CommoditySelectionIndex(),
        "ConstanceBrownCompositeIndex" => new ConstanceBrownCompositeIndex(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
