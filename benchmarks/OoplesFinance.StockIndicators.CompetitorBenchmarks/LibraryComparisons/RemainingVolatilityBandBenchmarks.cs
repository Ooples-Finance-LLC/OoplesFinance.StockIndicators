using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingVolatilityBandBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("KirshenbaumBands", "SmoothedVolatilityBands")]
    public string Operation { get; set; } = "KirshenbaumBands";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "KirshenbaumBands" => new KirshenbaumBands(),
        "SmoothedVolatilityBands" => new SmoothedVolatilityBands(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
