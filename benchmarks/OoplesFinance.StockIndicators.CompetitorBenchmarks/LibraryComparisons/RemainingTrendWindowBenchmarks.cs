using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingTrendWindowBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("TrendContinuationFactor", "TrendTriggerFactor", "TrendIntensityIndex")]
    public string Operation { get; set; } = "TrendIntensityIndex";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "TrendContinuationFactor" => new TrendContinuationFactor(),
        "TrendTriggerFactor" => new TrendTriggerFactor(),
        "TrendIntensityIndex" => new TrendIntensityIndex(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
