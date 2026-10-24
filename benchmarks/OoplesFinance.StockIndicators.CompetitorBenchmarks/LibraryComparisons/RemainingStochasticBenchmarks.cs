using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingStochasticBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("Stochastic", "StochasticK", "StochasticD", "PricePosition", "StochasticOscillator", "StochasticFastOscillator", "StochasticRegular")]
    public string Operation { get; set; } = "Stochastic";
    protected override DateTime GetTime(int index) => new DateTime(2026, 1, 1).AddHours(index);
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "Stochastic" => new Stochastic(100, 3),
        "StochasticK" => new StochasticK(100),
        "StochasticD" => new StochasticD(100),
        "PricePosition" => new PricePosition(100),
        "StochasticOscillator" => new StochasticOscillator(100),
        "StochasticFastOscillator" => new StochasticFastOscillator(100),
        "StochasticRegular" => new StochasticRegular(100),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
