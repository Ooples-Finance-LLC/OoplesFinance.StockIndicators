using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingVolumeWeightedBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("Vwma", "VolumeWeightedMovingAverage", "WindowedVolumeWeightedMovingAverage")]
    public string Operation { get; set; } = "Vwma";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "Vwma" => new Vwma(20),
        "VolumeWeightedMovingAverage" => new VolumeWeightedMovingAverage(20),
        "WindowedVolumeWeightedMovingAverage" => new WindowedVolumeWeightedMovingAverage(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
