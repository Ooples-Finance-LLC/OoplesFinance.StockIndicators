using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingHannHammingBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("EhlersHannMovingAverage", "EhlersHammingMovingAverage", "HammingMa", "EhlersHannWindowIndicator", "EhlersHammingWindowIndicator")]
    public string Operation { get; set; } = "EhlersHannMovingAverage";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "EhlersHannMovingAverage" => new EhlersHannMovingAverage(20),
        "EhlersHammingMovingAverage" => new EhlersHammingMovingAverage(20),
        "HammingMa" => new HammingMa(20),
        "EhlersHannWindowIndicator" => new EhlersHannWindowIndicator(20),
        "EhlersHammingWindowIndicator" => new EhlersHammingWindowIndicator(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
