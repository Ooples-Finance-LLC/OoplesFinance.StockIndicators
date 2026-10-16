using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingTrendScoreBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("ChandeTrendScore", "TrendScore")]
    public string Operation { get; set; } = "ChandeTrendScore";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "ChandeTrendScore" => new ChandeTrendScore(),
        "TrendScore" => new TrendScore(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
