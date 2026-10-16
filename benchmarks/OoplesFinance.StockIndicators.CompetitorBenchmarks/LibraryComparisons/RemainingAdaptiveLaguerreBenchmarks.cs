using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingAdaptiveLaguerreBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("EhlersAdaptiveLaguerreFilter", "EhlersLaguerreRelativeStrengthIndexWithSelfAdjustingAlpha")]
    public string Operation { get; set; } = "EhlersAdaptiveLaguerreFilter";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "EhlersAdaptiveLaguerreFilter" => new EhlersAdaptiveLaguerreFilter(20),
        "EhlersLaguerreRelativeStrengthIndexWithSelfAdjustingAlpha" => new EhlersLaguerreRelativeStrengthIndexWithSelfAdjustingAlpha(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
