using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingStochasticMacdBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("StochasticMacdOscillator")]
    public string Operation { get; set; } = "StochasticMacdOscillator";
    protected override DateTime GetTime(int index) => new DateTime(2026, 1, 1).AddHours(index);
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "StochasticMacdOscillator" => new StochasticMacdOscillator(45),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
