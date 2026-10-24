using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingWindowMeansBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("NaturalMa", "FareySequenceWeightedMovingAverage", "OptimalWeightedMovingAverage")]
    public string Operation { get; set; } = "NaturalMa";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "NaturalMa" => new NaturalMa(14),
        "FareySequenceWeightedMovingAverage" => new FareySequenceWeightedMovingAverage(14),
        "OptimalWeightedMovingAverage" => new OptimalWeightedMovingAverage(14),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
