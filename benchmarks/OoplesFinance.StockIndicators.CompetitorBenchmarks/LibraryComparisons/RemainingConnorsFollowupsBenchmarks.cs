using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingConnorsFollowupsBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("ConnorsRsi", "ConnorsRelativeStrengthIndex", "StochasticConnorsRelativeStrengthIndex")]
    public string Operation { get; set; } = "ConnorsRsi";
    protected override DateTime GetTime(int index) => new DateTime(2026, 1, 1).AddHours(index);
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "ConnorsRsi" => new ConnorsRsi(100),
        "ConnorsRelativeStrengthIndex" => new ConnorsRelativeStrengthIndex(75, 100, 125),
        "StochasticConnorsRelativeStrengthIndex" => new StochasticConnorsRelativeStrengthIndex(75, 100, 125),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
