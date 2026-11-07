using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingHilbertFamilyBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("EhlersClassicHilbertTransformer", "EhlersHilbertTransformer", "EhlersHilbertTransformerIndicator", "EhlersHilbertTransformIndicator", "EhlersInstantaneousPhaseIndicator")]
    public string Operation { get; set; } = "EhlersClassicHilbertTransformer";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "EhlersClassicHilbertTransformer" => new EhlersClassicHilbertTransformer(),
        "EhlersHilbertTransformer" => new EhlersHilbertTransformer(),
        "EhlersHilbertTransformerIndicator" => new EhlersHilbertTransformerIndicator(),
        "EhlersHilbertTransformIndicator" => new EhlersHilbertTransformIndicator(),
        "EhlersInstantaneousPhaseIndicator" => new EhlersInstantaneousPhaseIndicator(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
