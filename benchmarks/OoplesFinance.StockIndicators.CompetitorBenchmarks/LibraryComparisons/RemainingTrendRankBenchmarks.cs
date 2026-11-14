using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingTrendRankBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("TrendStep", "TechnicalRank")]
    public string Operation { get; set; } = "TrendStep";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "TrendStep" => new TrendStep(),
        "TechnicalRank" => new TechnicalRank(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
