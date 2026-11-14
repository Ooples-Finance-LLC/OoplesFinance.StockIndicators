using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingStochCompositesBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("DoubleStochasticOscillator", "DMIStochastic")]
    public string Operation { get; set; } = "DoubleStochasticOscillator";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "DoubleStochasticOscillator" => new DoubleStochasticOscillator(),
        "DMIStochastic" => new DMIStochastic(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
