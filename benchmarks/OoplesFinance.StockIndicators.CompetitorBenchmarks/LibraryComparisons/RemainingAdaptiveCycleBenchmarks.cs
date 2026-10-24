using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Enums;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingAdaptiveCycleBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("EhlersAdaptiveCyberCycle", "EhlersAdaptiveCenterOfGravityOscillator", "EhlersSmoothedAdaptiveMomentum")]
    public string Operation { get; set; } = "EhlersAdaptiveCyberCycle";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "EhlersAdaptiveCyberCycle" => new EhlersAdaptiveCyberCycle(),
        "EhlersAdaptiveCenterOfGravityOscillator" => new EhlersAdaptiveCenterOfGravityOscillator(),
        "EhlersSmoothedAdaptiveMomentum" => new EhlersSmoothedAdaptiveMomentum(5, 8, MovingAvgType.ExponentialMovingAverage),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
