using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingRangeStopsBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("AtrTrailingStops", "AverageTrueRangeTrailingStops", "ChandelierExit", "ChandelierExitLong", "ChandelierExitShort")]
    public string Operation { get; set; } = "AtrTrailingStops";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "AtrTrailingStops" => new AtrTrailingStops(20),
        "AverageTrueRangeTrailingStops" => new AverageTrueRangeTrailingStops(20),
        "ChandelierExit" => new ChandelierExit(20),
        "ChandelierExitLong" => new ChandelierExitLong(20),
        "ChandelierExitShort" => new ChandelierExitShort(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
