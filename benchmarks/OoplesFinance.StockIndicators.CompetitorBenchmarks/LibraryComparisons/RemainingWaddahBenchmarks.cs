using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingWaddahBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("WaddahAttarExplosion")]
    public string Operation { get; set; } = "WaddahAttarExplosion";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "WaddahAttarExplosion" => new WaddahAttarExplosion(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
