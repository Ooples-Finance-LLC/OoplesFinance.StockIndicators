using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingDominantCycleBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("EhlersDualDifferentiatorDominantCycle", "EhlersHomodyneDominantCycle", "EhlersPhaseAccumulationDominantCycle")]
    public string Operation { get; set; } = "EhlersHomodyneDominantCycle";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "EhlersDualDifferentiatorDominantCycle" => new EhlersDualDifferentiatorDominantCycle(),
        "EhlersHomodyneDominantCycle" => new EhlersHomodyneDominantCycle(),
        "EhlersPhaseAccumulationDominantCycle" => new EhlersPhaseAccumulationDominantCycle(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
