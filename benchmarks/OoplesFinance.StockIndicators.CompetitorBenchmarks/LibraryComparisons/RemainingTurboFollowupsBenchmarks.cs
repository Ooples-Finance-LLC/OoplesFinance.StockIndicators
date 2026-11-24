using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingTurboFollowupsBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("TurboTrigger", "TurboStochasticsFast", "TurboStochasticsSlow")]
    public string Operation { get; set; } = "TurboTrigger";
    protected override DateTime GetTime(int index) => new DateTime(2026, 1, 1).AddHours(index);
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "TurboTrigger" => new TurboTrigger(100, 1),
        "TurboStochasticsFast" => new TurboStochasticsFast(100, 75, 2),
        "TurboStochasticsSlow" => new TurboStochasticsSlow(100, 75, 2),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
