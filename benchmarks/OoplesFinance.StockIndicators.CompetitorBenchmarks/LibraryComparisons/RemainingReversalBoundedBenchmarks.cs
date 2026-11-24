using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingReversalBoundedBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("ReversalPoints")]
    public string Operation { get; set; } = "ReversalPoints";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "ReversalPoints" => new ReversalPoints(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
