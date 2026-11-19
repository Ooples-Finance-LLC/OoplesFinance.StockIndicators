using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Compare generated variance/coefficient of variation/regression error against their own legacy route, preserving
// each staged averaging operation. No native startup convention is substituted.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingExactStatisticsBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("Variance", "CoefficientOfVariation", "StandardError")]
    public string Operation { get; set; } = "Variance";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "Variance" => new Variance(20),
        "CoefficientOfVariation" => new CoefficientOfVariation(20),
        "StandardError" => new StandardError(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
