using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingProjectionFamilyBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("ProjectionBands", "ProjectionOscillator", "ProjectionBandwidth")]
    public string Operation { get; set; } = "ProjectionBands";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "ProjectionBands" => new ProjectionBands(),
        "ProjectionOscillator" => new ProjectionOscillator(),
        "ProjectionBandwidth" => new ProjectionBandwidth(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
