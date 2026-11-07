using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingVolatilityStopBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("VolatilityStop", "WellesWilderVolatilitySystem")]
    public string Operation { get; set; } = "VolatilityStop";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "VolatilityStop" => new VolatilityStop(),
        "WellesWilderVolatilitySystem" => new WellesWilderVolatilitySystem(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
