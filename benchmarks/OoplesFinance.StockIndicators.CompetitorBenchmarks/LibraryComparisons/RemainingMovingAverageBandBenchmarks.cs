using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingMovingAverageBandBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("MovingAverageBands", "MovingAverageBandWidth")]
    public string Operation { get; set; } = "MovingAverageBands";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "MovingAverageBands" => new MovingAverageBands(),
        "MovingAverageBandWidth" => new MovingAverageBandWidth(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
