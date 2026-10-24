using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingGarmanKlassBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("GarmanKlassVolatility")]
    public string Operation { get; set; } = "GarmanKlassVolatility";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "GarmanKlassVolatility" => new GarmanKlassVolatility(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
