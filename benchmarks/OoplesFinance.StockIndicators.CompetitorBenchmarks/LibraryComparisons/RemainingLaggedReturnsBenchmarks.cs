using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingLaggedReturnsBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("SimpleReturns", "LogReturns")]
    public string Operation { get; set; } = "SimpleReturns";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "SimpleReturns" => new SimpleReturns(14),
        "LogReturns" => new LogReturns(14),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
