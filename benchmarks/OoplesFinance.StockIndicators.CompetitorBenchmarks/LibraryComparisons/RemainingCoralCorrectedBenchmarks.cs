using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingCoralCorrectedBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("CoralTrendIndicator", "CorrectedMovingAverage")]
    public string Operation { get; set; } = "CoralTrendIndicator";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "CoralTrendIndicator" => new CoralTrendIndicator(21),
        "CorrectedMovingAverage" => new CorrectedMovingAverage(35),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
