using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingHampelHarmonicBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("HampelFilter", "HarmonicMeanMovingAverage")]
    public string Operation { get; set; } = "HampelFilter";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "HampelFilter" => new HampelFilter(),
        "HarmonicMeanMovingAverage" => new HarmonicMeanMovingAverage(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
