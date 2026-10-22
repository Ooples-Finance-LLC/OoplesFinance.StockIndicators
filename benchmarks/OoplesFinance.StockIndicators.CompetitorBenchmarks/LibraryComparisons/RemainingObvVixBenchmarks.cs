using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingObvVixBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("OnBalanceVolumeDisparityIndicator", "VixTradingSystem")]
    public string Operation { get; set; } = "OnBalanceVolumeDisparityIndicator";
    protected override DateTime GetTime(int index) => new DateTime(2026, 1, 1).AddHours(index);
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "OnBalanceVolumeDisparityIndicator" => new OnBalanceVolumeDisparityIndicator(33, 4, 1.1, .9),
        "VixTradingSystem" => new VixTradingSystem(50, 11, -11),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
