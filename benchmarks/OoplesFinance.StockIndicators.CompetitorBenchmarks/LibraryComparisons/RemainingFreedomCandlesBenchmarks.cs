using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingFreedomCandlesBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("FreedomOfMovement", "FunctionToCandles")]
    public string Operation { get; set; } = "FreedomOfMovement";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "FreedomOfMovement" => new FreedomOfMovement(),
        "FunctionToCandles" => new FunctionToCandles(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
