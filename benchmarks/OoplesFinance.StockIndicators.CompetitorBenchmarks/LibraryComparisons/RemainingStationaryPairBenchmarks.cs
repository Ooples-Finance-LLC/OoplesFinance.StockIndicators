using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingStationaryPairBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("StationaryExtrapolatedLevels", "StationaryExtrapolatedLevelsOscillator")]
    public string Operation { get; set; } = "StationaryExtrapolatedLevels";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "StationaryExtrapolatedLevels" => new StationaryExtrapolatedLevels(),
"StationaryExtrapolatedLevelsOscillator" => new StationaryExtrapolatedLevelsOscillator(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
