using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Compare generated CMF/MFI/ForceIndex/OBV against their own legacy route, preserving
// each staged averaging operation. No native startup convention is substituted.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingVolumeBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("Cmf", "Mfi", "ForceIndex", "Obv")]
    public string Operation { get; set; } = "Cmf";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "Cmf" => new Cmf(20),
        "Mfi" => new Mfi(20),
        "ForceIndex" => new ForceIndex(20),
        "Obv" => new Obv(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
