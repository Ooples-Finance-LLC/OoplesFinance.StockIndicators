using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingMovementStrengthBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("StrengthOfMovement")]
    public string Operation { get; set; } = "StrengthOfMovement";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "StrengthOfMovement" => new StrengthOfMovement(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
