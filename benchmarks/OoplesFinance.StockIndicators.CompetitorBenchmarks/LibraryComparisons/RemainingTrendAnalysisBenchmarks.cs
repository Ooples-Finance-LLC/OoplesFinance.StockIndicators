using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingTrendAnalysisBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("TrendAnalysisIndex", "TrendAnalysisIndicator")]
    public string Operation { get; set; } = "TrendAnalysisIndex";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "TrendAnalysisIndex" => new TrendAnalysisIndex(),
"TrendAnalysisIndicator" => new TrendAnalysisIndicator(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
