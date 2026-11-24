using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingWilliamsPairBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("SmoothedWilliamsR", "EnhancedWilliamsR")]
    public string Operation { get; set; } = "EnhancedWilliamsR";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "SmoothedWilliamsR" => new SmoothedWilliamsR(),
        "EnhancedWilliamsR" => new EnhancedWilliamsR(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
