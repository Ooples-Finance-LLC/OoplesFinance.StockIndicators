using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingCoreAliasesBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("AverageDirectionalIndex", "MomentumOscillator", "VolumePriceTrend", "StandardDevation", "Vhf")]
    public string Operation { get; set; } = "AverageDirectionalIndex";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "AverageDirectionalIndex" => new AverageDirectionalIndex(20),
        "MomentumOscillator" => new MomentumOscillator(20, 3),
        "VolumePriceTrend" => new VolumePriceTrend(20),
        "StandardDevation" => new StandardDevation(20, OoplesFinance.StockIndicators.Enums.MovingAvgType.SimpleMovingAverage),
        "Vhf" => new Vhf(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
