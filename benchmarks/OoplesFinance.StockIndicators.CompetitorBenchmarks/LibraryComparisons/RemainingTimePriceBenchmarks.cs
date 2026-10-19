using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingTimePriceBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("TimePriceIndicator")]
    public string Operation { get; set; } = "TimePriceIndicator";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "TimePriceIndicator" => new TimePriceIndicator(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
