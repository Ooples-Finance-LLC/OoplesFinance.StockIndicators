using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Compare generated APO/PPO/MACD against their own legacy route, preserving
// each staged averaging operation. No native startup convention is substituted.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingEmaGapBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("Apo", "Ppo", "Macd")]
    public string Operation { get; set; } = "Apo";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "Apo" => new Apo(12, 26),
        "Ppo" => new Ppo(12, 26, signalLength: 9),
        "Macd" => new Macd(12, 26, 9),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
