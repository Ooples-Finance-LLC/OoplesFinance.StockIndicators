using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingAutocorrelationFamilyBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("EhlersAutoCorrelationIndicator", "EhlersAutoCorrelationPeriodogram", "EhlersAdaptiveBandPassFilter", "EhlersZeroCrossingsDominantCycle")]
    public string Operation { get; set; } = "EhlersAutoCorrelationIndicator";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "EhlersAutoCorrelationIndicator" => new EhlersAutoCorrelationIndicator(),
        "EhlersAutoCorrelationPeriodogram" => new EhlersAutoCorrelationPeriodogram(),
        "EhlersAdaptiveBandPassFilter" => new EhlersAdaptiveBandPassFilter(),
        "EhlersZeroCrossingsDominantCycle" => new EhlersZeroCrossingsDominantCycle(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
