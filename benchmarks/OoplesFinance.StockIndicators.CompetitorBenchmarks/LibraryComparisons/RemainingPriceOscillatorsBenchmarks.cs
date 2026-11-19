using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingPriceOscillatorsBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("PremierStochasticOscillator", "PrettyGoodOscillator", "PriceZoneOscillator", "PriceVolumeRank", "PsychologicalLine")]
    public string Operation { get; set; } = "PremierStochasticOscillator";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "PremierStochasticOscillator" => new PremierStochasticOscillator(),
"PrettyGoodOscillator" => new PrettyGoodOscillator(),
"PriceZoneOscillator" => new PriceZoneOscillator(),
"PriceVolumeRank" => new PriceVolumeRank(),
"PsychologicalLine" => new PsychologicalLine(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
