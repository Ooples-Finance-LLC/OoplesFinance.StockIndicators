using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingGatorMeannessBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("GatorOscillator", "MarketMeannessIndex")]
    public string Operation { get; set; } = "GatorOscillator";
    protected override DateTime GetTime(int index) => new DateTime(2026, 1, 1).AddHours(index);
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "GatorOscillator" => new GatorOscillator(100),
        "MarketMeannessIndex" => new MarketMeannessIndex(100),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
