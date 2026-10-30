using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingSmoothedReturnBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("SmoothedRateOfChange")]
    public string Operation { get; set; } = "SmoothedRateOfChange";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "SmoothedRateOfChange" => new SmoothedRateOfChange(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
