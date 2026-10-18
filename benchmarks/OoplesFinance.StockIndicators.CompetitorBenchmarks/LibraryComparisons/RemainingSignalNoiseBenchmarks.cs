using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingSignalNoiseBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("EhlersSignalToNoiseRatioV1", "EhlersSignalToNoiseRatioV2", "EhlersAlternateSignalToNoiseRatio", "EhlersEnhancedSignalToNoiseRatio")]
    public string Operation { get; set; } = "EhlersSignalToNoiseRatioV1";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "EhlersSignalToNoiseRatioV1" => new EhlersSignalToNoiseRatioV1(),
        "EhlersSignalToNoiseRatioV2" => new EhlersSignalToNoiseRatioV2(6, MovingAvgType.ExponentialMovingAverage),
        "EhlersAlternateSignalToNoiseRatio" => new EhlersAlternateSignalToNoiseRatio(),
        "EhlersEnhancedSignalToNoiseRatio" => new EhlersEnhancedSignalToNoiseRatio(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
