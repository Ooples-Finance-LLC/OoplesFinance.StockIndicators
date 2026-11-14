using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingRecursiveExtraBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("McNichollMovingAverage", "WellRoundedMovingAverage", "WellesWilderSummation", "WildersSummationMethod")]
    public string Operation { get; set; } = "McNichollMovingAverage";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "McNichollMovingAverage" => new McNichollMovingAverage(20),
        "WellRoundedMovingAverage" => new WellRoundedMovingAverage(20),
        "WellesWilderSummation" => new WellesWilderSummation(20),
        "WildersSummationMethod" => new WildersSummationMethod(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
