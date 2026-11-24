using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingMacdAliasesBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("MacdLine", "MacdSignal", "MacdHistogram")]
    public string Operation { get; set; } = "MacdLine";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "MacdLine" => new MacdLine(12, 26),
        "MacdSignal" => new MacdSignal(12, 26, 9),
        "MacdHistogram" => new MacdHistogram(12, 26, 9),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
