using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingSwingIndexBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("SwingIndex")]
    public string Operation { get; set; } = "SwingIndex";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "SwingIndex" => new SwingIndex(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
