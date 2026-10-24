using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingVariableAveragesBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("Vma", "VariableMovingAverageBands")]
    public string Operation { get; set; } = "Vma";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "Vma" => new Vma(),
        "VariableMovingAverageBands" => new VariableMovingAverageBands(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
