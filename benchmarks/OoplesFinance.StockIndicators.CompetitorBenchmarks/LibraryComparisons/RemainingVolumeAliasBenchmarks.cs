using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Verify the full-name volume facades against their own legacy route,
// including the signal output and default index seeds.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingVolumeAliasBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("NegativeVolumeIndex", "PositiveVolumeIndex", "OnBalanceVolume", "PriceVolumeTrend")]
    public string Operation { get; set; } = "NegativeVolumeIndex";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "NegativeVolumeIndex" => new NegativeVolumeIndex(20),
        "PositiveVolumeIndex" => new PositiveVolumeIndex(20),
        "OnBalanceVolume" => new OnBalanceVolume(20),
        "PriceVolumeTrend" => new PriceVolumeTrend(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
