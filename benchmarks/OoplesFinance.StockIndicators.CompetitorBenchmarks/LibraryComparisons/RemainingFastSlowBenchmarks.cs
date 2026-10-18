using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingFastSlowBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("FastSlowKurtosisOscillator", "FastandSlowKurtosisOscillator", "FastSlowRsiOscillator", "FastSlowStochasticOscillator", "FastSlowDegreeOscillator")]
    public string Operation { get; set; } = "FastSlowKurtosisOscillator";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "FastSlowKurtosisOscillator" => new FastSlowKurtosisOscillator(14),
        "FastandSlowKurtosisOscillator" => new FastandSlowKurtosisOscillator(14),
        "FastSlowRsiOscillator" => new FastSlowRsiOscillator(14),
        "FastSlowStochasticOscillator" => new FastSlowStochasticOscillator(14),
        "FastSlowDegreeOscillator" => new FastSlowDegreeOscillator(14),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
