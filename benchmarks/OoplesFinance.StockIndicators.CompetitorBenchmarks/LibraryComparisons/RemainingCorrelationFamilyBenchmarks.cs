using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingCorrelationFamilyBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("EhlersCorrelationTrendIndicator", "EhlersCorrelationCycleIndicator", "EhlersCorrelationAngleIndicator", "EhlersMarketStateIndicator")]
    public string Operation { get; set; } = "EhlersCorrelationTrendIndicator";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "EhlersCorrelationTrendIndicator" => new EhlersCorrelationTrendIndicator(),
        "EhlersCorrelationCycleIndicator" => new EhlersCorrelationCycleIndicator(),
        "EhlersCorrelationAngleIndicator" => new EhlersCorrelationAngleIndicator(),
        "EhlersMarketStateIndicator" => new EhlersMarketStateIndicator(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
