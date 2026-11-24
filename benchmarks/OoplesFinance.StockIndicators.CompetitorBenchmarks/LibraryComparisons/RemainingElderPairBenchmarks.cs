using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingElderPairBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("ElderImpulseSystem", "ElderRayIndex")]
    public string Operation { get; set; } = "ElderImpulseSystem";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "ElderImpulseSystem" => new ElderImpulseSystem(13),
        "ElderRayIndex" => new ElderRayIndex(13),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
