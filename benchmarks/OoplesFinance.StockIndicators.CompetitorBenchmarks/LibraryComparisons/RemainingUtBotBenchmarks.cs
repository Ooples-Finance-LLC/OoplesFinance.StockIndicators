using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingUtBotBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("UtBotAlerts")]
    public string Operation { get; set; } = "UtBotAlerts";
    protected override DateTime GetTime(int index) => new DateTime(2026, 1, 1).AddHours(index);
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "UtBotAlerts" => new UtBotAlerts(10, 1),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
