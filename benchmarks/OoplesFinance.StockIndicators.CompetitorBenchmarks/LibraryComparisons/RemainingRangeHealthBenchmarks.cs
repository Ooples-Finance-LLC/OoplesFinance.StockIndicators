using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Compare generated Demarker/DeMarker/UlcerIndex/ChoppinessIndex against their own legacy route, preserving
// each staged averaging operation. No native startup convention is substituted.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingRangeHealthBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("Demarker", "DeMarker", "UlcerIndex", "ChoppinessIndex")]
    public string Operation { get; set; } = "Demarker";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "Demarker" => new Demarker(20),
        "DeMarker" => new DeMarker(20),
        "UlcerIndex" => new UlcerIndex(20),
        "ChoppinessIndex" => new ChoppinessIndex(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
