using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingUhlBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("UhlMaCrossoverSystem")]
    public string Operation { get; set; } = "UhlMaCrossoverSystem";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "UhlMaCrossoverSystem" => new UhlMaCrossoverSystem(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
