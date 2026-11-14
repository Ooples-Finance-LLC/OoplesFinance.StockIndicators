using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingPoweredKaufmanBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("PoweredKaufmanAdaptiveMovingAverage", "AdaptiveTrailingStop")]
    public string Operation { get; set; } = "PoweredKaufmanAdaptiveMovingAverage";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "PoweredKaufmanAdaptiveMovingAverage" => new PoweredKaufmanAdaptiveMovingAverage(100),
        "AdaptiveTrailingStop" => new AdaptiveTrailingStop(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
