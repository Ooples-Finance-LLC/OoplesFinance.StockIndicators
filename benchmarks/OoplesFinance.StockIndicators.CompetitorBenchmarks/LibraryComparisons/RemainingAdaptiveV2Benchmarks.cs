using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingAdaptiveV2Benchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("EhlersAdaptiveRelativeStrengthIndexV2", "EhlersAdaptiveRsiFisherTransformV2", "EhlersAdaptiveStochasticIndicatorV2", "EhlersAdaptiveStochasticInverseFisherTransform", "EhlersAdaptiveCommodityChannelIndexV2")]
    public string Operation { get; set; } = "EhlersAdaptiveRelativeStrengthIndexV2";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "EhlersAdaptiveRelativeStrengthIndexV2" => new EhlersAdaptiveRelativeStrengthIndexV2(),
        "EhlersAdaptiveRsiFisherTransformV2" => new EhlersAdaptiveRsiFisherTransformV2(),
        "EhlersAdaptiveStochasticIndicatorV2" => new EhlersAdaptiveStochasticIndicatorV2(),
        "EhlersAdaptiveStochasticInverseFisherTransform" => new EhlersAdaptiveStochasticInverseFisherTransform(),
        "EhlersAdaptiveCommodityChannelIndexV2" => new EhlersAdaptiveCommodityChannelIndexV2(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
