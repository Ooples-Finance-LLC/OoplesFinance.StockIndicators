using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingFisherFramaBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("Frama", "EhlersFrama", "FisherTransform", "EhlersFisherTransform", "EhlersInverseFisherTransform", "EhlersRelativeStrengthIndexInverseFisherTransform")]
    public string Operation { get; set; } = "Frama";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "Frama" => new Frama(),
        "EhlersFrama" => new EhlersFrama(),
        "FisherTransform" => new FisherTransform(),
        "EhlersFisherTransform" => new EhlersFisherTransform(),
        "EhlersInverseFisherTransform" => new EhlersInverseFisherTransform(),
        "EhlersRelativeStrengthIndexInverseFisherTransform" => new EhlersRelativeStrengthIndexInverseFisherTransform(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
