using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingMacdLeaderBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("MovingAverageConvergenceDivergenceLeader")]
    public string Operation { get; set; } = "MovingAverageConvergenceDivergenceLeader";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "MovingAverageConvergenceDivergenceLeader" => new MovingAverageConvergenceDivergenceLeader(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
