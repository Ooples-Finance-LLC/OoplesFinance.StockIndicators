using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingAhrensAlphaBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("AhrensMovingAverage", "AlphaDecreasingEma")]
    public string Operation { get; set; } = "AhrensMovingAverage";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "AhrensMovingAverage" => new AhrensMovingAverage(20),
        "AlphaDecreasingEma" => new AlphaDecreasingEma(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
