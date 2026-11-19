using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingEhlersSimpleFamilyBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("EhlersSimpleDerivIndicator", "EhlersSimpleClipIndicator", "EhlersSimpleWindowIndicator", "EhlersTripleDelayLineDetrender")]
    public string Operation { get; set; } = "EhlersSimpleDerivIndicator";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "EhlersSimpleDerivIndicator" => new EhlersSimpleDerivIndicator(),
        "EhlersSimpleClipIndicator" => new EhlersSimpleClipIndicator(),
        "EhlersSimpleWindowIndicator" => new EhlersSimpleWindowIndicator(),
        "EhlersTripleDelayLineDetrender" => new EhlersTripleDelayLineDetrender(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
