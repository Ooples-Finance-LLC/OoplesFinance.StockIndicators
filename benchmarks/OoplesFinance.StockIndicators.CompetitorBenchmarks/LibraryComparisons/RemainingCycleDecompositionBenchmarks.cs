using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingCycleDecompositionBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("EhlersCycleAmplitude", "EhlersTrendExtraction", "EhlersEmpiricalModeDecomposition")]
    public string Operation { get; set; } = "EhlersCycleAmplitude";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "EhlersCycleAmplitude" => new EhlersCycleAmplitude(),
        "EhlersTrendExtraction" => new EhlersTrendExtraction(),
        "EhlersEmpiricalModeDecomposition" => new EhlersEmpiricalModeDecomposition(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
