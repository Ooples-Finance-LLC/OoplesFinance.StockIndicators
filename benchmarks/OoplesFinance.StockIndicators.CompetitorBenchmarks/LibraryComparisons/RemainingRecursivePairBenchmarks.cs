using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingRecursivePairBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("RecursiveRelativeStrengthIndex", "RecursiveStochastic")]
    public string Operation { get; set; } = "RecursiveRelativeStrengthIndex";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "RecursiveRelativeStrengthIndex" => new RecursiveRelativeStrengthIndex(),
"RecursiveStochastic" => new RecursiveStochastic(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
