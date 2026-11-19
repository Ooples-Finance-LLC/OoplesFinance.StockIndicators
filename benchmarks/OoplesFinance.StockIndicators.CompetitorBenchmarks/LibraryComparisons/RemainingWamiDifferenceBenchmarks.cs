using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingWamiDifferenceBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("WamiOscillator", "PriceMomentum", "VolumeMomentum")]
    public string Operation { get; set; } = "WamiOscillator";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "WamiOscillator" => new WamiOscillator(13),
        "PriceMomentum" => new PriceMomentum(10),
        "VolumeMomentum" => new VolumeMomentum(10),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
