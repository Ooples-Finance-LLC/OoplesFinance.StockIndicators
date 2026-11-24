using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingAdaptiveOscillatorsBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("AdaptiveStochastic", "ApirineSlowRelativeStrengthIndex")]
    public string Operation { get; set; } = "AdaptiveStochastic";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "AdaptiveStochastic" => new AdaptiveStochastic(),
        "ApirineSlowRelativeStrengthIndex" => new ApirineSlowRelativeStrengthIndex(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
