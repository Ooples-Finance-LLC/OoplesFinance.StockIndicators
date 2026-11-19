using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Share signed/absolute change smoothing and the published signal stage.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingTrueStrengthBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("Tsi", "TrueStrengthIndex", "ErgodicTrueStrengthIndexV1", "SMIErgodicIndicator")]
    public string Operation { get; set; } = "Tsi";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "Tsi" => new Tsi(25, 13),
        "TrueStrengthIndex" => new TrueStrengthIndex(25, 13, 7),
        "ErgodicTrueStrengthIndexV1" => new ErgodicTrueStrengthIndexV1(4, 8, 6, 3),
        "SMIErgodicIndicator" => new SMIErgodicIndicator(5, 20, 5),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
