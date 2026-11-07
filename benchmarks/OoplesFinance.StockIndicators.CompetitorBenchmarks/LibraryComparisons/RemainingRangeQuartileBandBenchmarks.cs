using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingRangeQuartileBandBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("InterquartileRangeBands", "RangeBands")]
    public string Operation { get; set; } = "InterquartileRangeBands";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "InterquartileRangeBands" => new InterquartileRangeBands(),
        "RangeBands" => new RangeBands(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
