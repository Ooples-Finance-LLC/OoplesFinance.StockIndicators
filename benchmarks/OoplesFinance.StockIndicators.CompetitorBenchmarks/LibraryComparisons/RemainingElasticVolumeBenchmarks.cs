using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingElasticVolumeBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("ElasticVolumeWeightedMovingAverageV1", "ElasticVolumeWeightedMovingAverageV2")]
    public string Operation { get; set; } = "ElasticVolumeWeightedMovingAverageV1";
    protected override DateTime GetTime(int index) => new DateTime(2026, 1, 1).AddHours(index);
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "ElasticVolumeWeightedMovingAverageV1" => new ElasticVolumeWeightedMovingAverageV1(14),
        "ElasticVolumeWeightedMovingAverageV2" => new ElasticVolumeWeightedMovingAverageV2(14),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
