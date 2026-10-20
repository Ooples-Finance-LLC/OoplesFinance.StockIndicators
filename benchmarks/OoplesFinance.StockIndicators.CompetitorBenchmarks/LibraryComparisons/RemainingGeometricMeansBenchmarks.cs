using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingGeometricMeansBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("GeoMa", "GeometricMeanMovingAverage")]
    public string Operation { get; set; } = "GeoMa";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "GeoMa" => new GeoMa(14),
        "GeometricMeanMovingAverage" => new GeometricMeanMovingAverage(14),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
