using OoplesFinance.StockIndicators.Enums;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingThreeHullBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("ThreeHMA", "ThreeHma", "TripleHullMovingAverage", "_3HMA")]
    public string Operation { get; set; } = "ThreeHMA";
    protected override DateTime GetTime(int index) => new DateTime(2026, 1, 1).AddHours(index);
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "ThreeHMA" => new ThreeHMA(100),
        "ThreeHma" => new ThreeHma(100, MovingAvgType.WeightedMovingAverage),
        "TripleHullMovingAverage" => new TripleHullMovingAverage(100),
        "_3HMA" => new _3HMA(100, MovingAvgType.WeightedMovingAverage),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
