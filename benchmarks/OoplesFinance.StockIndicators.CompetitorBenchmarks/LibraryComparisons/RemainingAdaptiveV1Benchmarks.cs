using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingAdaptiveV1Benchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("EhlersAdaptiveRelativeStrengthIndexV1", "EhlersAdaptiveRsiFisherTransformV1", "EhlersAdaptiveStochasticIndicatorV1", "EhlersAdaptiveCommodityChannelIndexV1")]
    public string Operation { get; set; } = "EhlersAdaptiveRelativeStrengthIndexV1";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "EhlersAdaptiveRelativeStrengthIndexV1" => new EhlersAdaptiveRelativeStrengthIndexV1(),
        "EhlersAdaptiveRsiFisherTransformV1" => new EhlersAdaptiveRsiFisherTransformV1(),
        "EhlersAdaptiveStochasticIndicatorV1" => new EhlersAdaptiveStochasticIndicatorV1(),
        "EhlersAdaptiveCommodityChannelIndexV1" => new EhlersAdaptiveCommodityChannelIndexV1(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
