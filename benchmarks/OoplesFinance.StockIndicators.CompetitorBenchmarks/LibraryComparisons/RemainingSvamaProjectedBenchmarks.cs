using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingSvamaProjectedBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("Svama", "ProjectedSupportAndResistance")]
    public string Operation { get; set; } = "Svama";
    protected override DateTime GetTime(int index) => new DateTime(2026, 1, 1).AddHours(index);
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "Svama" => new Svama(14),
        "ProjectedSupportAndResistance" => new ProjectedSupportAndResistance(25),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
