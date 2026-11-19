using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingVolumeReturnFollowupsBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("NormalizedVolume", "OCHistogram", "ModifiedPriceVolumeTrend", "OmegaRatio")]
    public string Operation { get; set; } = "NormalizedVolume";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "NormalizedVolume" => new NormalizedVolume(14),
        "OCHistogram" => new OCHistogram(14),
        "ModifiedPriceVolumeTrend" => new ModifiedPriceVolumeTrend(14),
        "OmegaRatio" => new OmegaRatio(14),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
