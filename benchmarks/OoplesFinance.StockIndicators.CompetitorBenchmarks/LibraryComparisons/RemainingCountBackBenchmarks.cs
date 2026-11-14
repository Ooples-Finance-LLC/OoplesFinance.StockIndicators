using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingCountBackBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("GuppyCountBackLine")]
    public string Operation { get; set; } = "GuppyCountBackLine";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "GuppyCountBackLine" => new GuppyCountBackLine(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
