using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingTrimeanBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("Trimean")]
    public string Operation { get; set; } = "Trimean";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "Trimean" => new Trimean(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
