using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingAdaptiveBoundaryBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("FlaggingBands", "ExtendedRecursiveBands", "MotionToAttractionChannels", "VolumeAdaptiveBands")]
    public string Operation { get; set; } = "FlaggingBands";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "FlaggingBands" => new FlaggingBands(),
        "ExtendedRecursiveBands" => new ExtendedRecursiveBands(),
        "MotionToAttractionChannels" => new MotionToAttractionChannels(),
        "VolumeAdaptiveBands" => new VolumeAdaptiveBands(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
