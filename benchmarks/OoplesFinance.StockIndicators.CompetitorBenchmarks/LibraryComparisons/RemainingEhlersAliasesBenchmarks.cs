using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingEhlersAliasesBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("ButterworthFilter", "InverseFisherTransformCore", "EhlersReflex", "EhlersTrendflex")]
    public string Operation { get; set; } = "ButterworthFilter";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "ButterworthFilter" => new ButterworthFilter(20),
        "InverseFisherTransformCore" => new InverseFisherTransformCore(20),
        "EhlersReflex" => new EhlersReflex(20),
        "EhlersTrendflex" => new EhlersTrendflex(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
