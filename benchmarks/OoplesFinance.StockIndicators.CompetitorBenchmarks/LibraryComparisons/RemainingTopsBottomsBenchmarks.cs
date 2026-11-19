using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingTopsBottomsBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("TopsAndBottomsFinder")]
    public string Operation { get; set; } = "TopsAndBottomsFinder";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "TopsAndBottomsFinder" => new TopsAndBottomsFinder(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
