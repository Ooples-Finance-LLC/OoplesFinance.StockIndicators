using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingAverageCompositionsBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("LinearWeightedMovingAverageCore", "LeoMovingAverage", "McGinleyDynamic")]
    public string Operation { get; set; } = "LinearWeightedMovingAverageCore";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "LinearWeightedMovingAverageCore" => new LinearWeightedMovingAverageCore(14),
        "LeoMovingAverage" => new LeoMovingAverage(14),
        "McGinleyDynamic" => new McGinleyDynamic(14),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
