using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingSpectralFiltersBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("EhlersCombFilterSpectralEstimate", "EhlersDominantCycleTunedBypassFilter", "EhlersAutoCorrelationReversals")]
    public string Operation { get; set; } = "EhlersCombFilterSpectralEstimate";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "EhlersCombFilterSpectralEstimate" => new EhlersCombFilterSpectralEstimate(),
        "EhlersDominantCycleTunedBypassFilter" => new EhlersDominantCycleTunedBypassFilter(),
        "EhlersAutoCorrelationReversals" => new EhlersAutoCorrelationReversals(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
