using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingMartinRatioBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("MartinRatio")]
    public string Operation { get; set; } = "MartinRatio";
    protected override DateTime GetTime(int index) => new DateTime(2026, 1, 1).AddHours(index);
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "MartinRatio" => new MartinRatio(100, .02),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
