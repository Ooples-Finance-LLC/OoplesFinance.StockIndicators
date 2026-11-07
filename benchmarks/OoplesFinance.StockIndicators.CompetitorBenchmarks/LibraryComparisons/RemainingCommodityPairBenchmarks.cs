using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingCommodityPairBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("WoodieCommodityChannelIndex", "EhlersCommodityChannelIndexInverseFisherTransform")]
    public string Operation { get; set; } = "WoodieCommodityChannelIndex";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "WoodieCommodityChannelIndex" => new WoodieCommodityChannelIndex(6, 14),
        "EhlersCommodityChannelIndexInverseFisherTransform" => new EhlersCommodityChannelIndexInverseFisherTransform(20, 9),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
