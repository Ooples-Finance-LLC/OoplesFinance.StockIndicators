using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingGainGannBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("GainLossMovingAverage", "GannHiLoActivator", "GannTrendOscillator")]
    public string Operation { get; set; } = "GainLossMovingAverage";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "GainLossMovingAverage" => new GainLossMovingAverage(),
        "GannHiLoActivator" => new GannHiLoActivator(),
        "GannTrendOscillator" => new GannTrendOscillator(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
