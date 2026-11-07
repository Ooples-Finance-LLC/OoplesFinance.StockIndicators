using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingSwenlinBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("DecisionPointBreadthSwenlinTradingOscillator")]
    public string Operation { get; set; } = "DecisionPointBreadthSwenlinTradingOscillator";
    protected override DateTime GetTime(int index) => new DateTime(2026, 1, 1).AddHours(index);
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "DecisionPointBreadthSwenlinTradingOscillator" => new DecisionPointBreadthSwenlinTradingOscillator(5),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
