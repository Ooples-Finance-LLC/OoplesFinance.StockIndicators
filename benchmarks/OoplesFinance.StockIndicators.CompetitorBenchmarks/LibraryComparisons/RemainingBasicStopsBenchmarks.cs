using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingBasicStopsBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("LinearTrailingStop", "PercentageTrailingStops")]
    public string Operation { get; set; } = "LinearTrailingStop";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "LinearTrailingStop" => new LinearTrailingStop(100),
        "PercentageTrailingStops" => new PercentageTrailingStops(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
