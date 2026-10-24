using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingSquareWeightsBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("ParabolicWma", "QuadraticWma")]
    public string Operation { get; set; } = "ParabolicWma";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "ParabolicWma" => new ParabolicWma(),
"QuadraticWma" => new QuadraticWma(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
