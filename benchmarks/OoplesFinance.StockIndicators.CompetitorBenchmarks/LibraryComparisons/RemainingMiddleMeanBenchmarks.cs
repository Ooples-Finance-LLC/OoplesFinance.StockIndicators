using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingMiddleMeanBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("MiddleHighLowMovingAverage")]
    public string Operation { get; set; } = "MiddleHighLowMovingAverage";
    protected override DateTime GetTime(int index) => new DateTime(2026, 1, 1).AddHours(index);
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "MiddleHighLowMovingAverage" => new MiddleHighLowMovingAverage(100, 75),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
