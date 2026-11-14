using OoplesFinance.StockIndicators.Enums;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingCctRsiBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("CCTStochRSI", "CCTStochRelativeStrengthIndex")]
    public string Operation { get; set; } = "CCTStochRSI";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "CCTStochRSI" => new CCTStochRSI(5, 8, 13, 14, 21, 3, 8, MovingAvgType.ExponentialMovingAverage),
        "CCTStochRelativeStrengthIndex" => new CCTStochRelativeStrengthIndex(14),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
