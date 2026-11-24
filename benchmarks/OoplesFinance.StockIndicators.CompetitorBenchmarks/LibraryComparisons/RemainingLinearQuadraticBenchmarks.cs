using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingLinearQuadraticBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("LinearQuadraticConvergenceDivergenceOscillator")]
    public string Operation { get; set; } = "LinearQuadraticConvergenceDivergenceOscillator";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "LinearQuadraticConvergenceDivergenceOscillator" => new LinearQuadraticConvergenceDivergenceOscillator(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
