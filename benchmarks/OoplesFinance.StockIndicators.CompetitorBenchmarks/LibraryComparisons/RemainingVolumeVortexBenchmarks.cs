using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingVolumeVortexBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("Vpci", "VolumeWeightedRsi", "VortexBands")]
    public string Operation { get; set; } = "Vpci";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "Vpci" => new Vpci(8),
        "VolumeWeightedRsi" => new VolumeWeightedRsi(10),
        "VortexBands" => new VortexBands(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
