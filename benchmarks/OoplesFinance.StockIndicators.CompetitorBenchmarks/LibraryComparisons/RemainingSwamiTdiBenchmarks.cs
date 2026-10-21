using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingSwamiTdiBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("SwamiStochastics", "TradersDynamicIndex")]
    public string Operation { get; set; } = "SwamiStochastics";
    protected override DateTime GetTime(int index) => new DateTime(2026, 1, 1).AddHours(index);
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "SwamiStochastics" => new SwamiStochastics(12, 48),
        "TradersDynamicIndex" => new TradersDynamicIndex(13, 34, 2, 7),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
