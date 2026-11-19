using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingMomentumCompositesBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("CoppockCurve", "DerivativeOscillator", "DidiIndex")]
    public string Operation { get; set; } = "CoppockCurve";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "CoppockCurve" => new CoppockCurve(),
        "DerivativeOscillator" => new DerivativeOscillator(),
        "DidiIndex" => new DidiIndex(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
