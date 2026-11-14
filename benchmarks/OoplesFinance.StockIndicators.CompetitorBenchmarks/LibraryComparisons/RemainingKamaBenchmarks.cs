using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingKamaBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("Kama")]
    public string Operation { get; set; } = "Kama";
    protected override DateTime GetTime(int index) => new DateTime(2026, 1, 1).AddHours(index);
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "Kama" => new Kama(100),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
