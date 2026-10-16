using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingVervoortVolatilityBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("VervoortVolatilityBands")]
    public string Operation { get; set; } = "VervoortVolatilityBands";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "VervoortVolatilityBands" => new VervoortVolatilityBands(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
