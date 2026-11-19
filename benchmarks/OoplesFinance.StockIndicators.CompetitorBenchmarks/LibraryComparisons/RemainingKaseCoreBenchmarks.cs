using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingKaseCoreBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("KaseIndicator", "KaseConvergenceDivergence")]
    public string Operation { get; set; } = "KaseIndicator";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "KaseIndicator" => new KaseIndicator(),
        "KaseConvergenceDivergence" => new KaseConvergenceDivergence(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
