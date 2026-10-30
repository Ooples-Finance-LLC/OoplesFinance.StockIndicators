using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingLogisticMotionBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("LogisticCorrelation", "MotionSmoothnessIndex")]
    public string Operation { get; set; } = "LogisticCorrelation";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "LogisticCorrelation" => new LogisticCorrelation(),
        "MotionSmoothnessIndex" => new MotionSmoothnessIndex(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
