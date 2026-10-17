using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingOneLcBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("NamedSma", "NamedWma", "NamedEma", "NamedWilder", "AliasSma", "AliasWma", "AliasEma", "AliasWilder")]
    public string Operation { get; set; } = "NamedSma";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "NamedSma" => new OneLCLeastSquaresMovingAverage(20, MovingAvgType.SimpleMovingAverage),
        "NamedWma" => new OneLCLeastSquaresMovingAverage(20, MovingAvgType.WeightedMovingAverage),
        "NamedEma" => new OneLCLeastSquaresMovingAverage(20, MovingAvgType.ExponentialMovingAverage),
        "NamedWilder" => new OneLCLeastSquaresMovingAverage(20, MovingAvgType.WildersSmoothingMethod),
        "AliasSma" => new _1LCLeastSquaresMovingAverage(20, MovingAvgType.SimpleMovingAverage),
        "AliasWma" => new _1LCLeastSquaresMovingAverage(20, MovingAvgType.WeightedMovingAverage),
        "AliasEma" => new _1LCLeastSquaresMovingAverage(20, MovingAvgType.ExponentialMovingAverage),
        "AliasWilder" => new _1LCLeastSquaresMovingAverage(20, MovingAvgType.WildersSmoothingMethod),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
