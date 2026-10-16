using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingAdaptivePairBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("BryantAdaptiveMovingAverage", "VariableAdaptiveMovingAverage")]
    public string Operation { get; set; } = "BryantAdaptiveMovingAverage";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "BryantAdaptiveMovingAverage" => new BryantAdaptiveMovingAverage(20),
        "VariableAdaptiveMovingAverage" => new VariableAdaptiveMovingAverage(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
