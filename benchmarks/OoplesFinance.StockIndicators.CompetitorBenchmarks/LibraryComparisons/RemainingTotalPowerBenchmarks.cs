using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingTotalPowerBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("TotalPowerIndicator")]
    public string Operation { get; set; } = "TotalPowerIndicator";
    protected override DateTime GetTime(int index) => new DateTime(2026, 1, 1).AddHours(index);
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "TotalPowerIndicator" => new TotalPowerIndicator(45, 10),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
