using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingCandleKernelBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("Qstick", "DailyAveragePriceDelta", "DrunkardWalk")]
    public string Operation { get; set; } = "Qstick";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override double GetOpen(int index, double price) => price + (index % 3 - 1) * 0.75;

    protected override IIndicator Create() => Operation switch
    {
        "Qstick" => new Qstick(),
        "DailyAveragePriceDelta" => new DailyAveragePriceDelta(),
        "DrunkardWalk" => new DrunkardWalk(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
