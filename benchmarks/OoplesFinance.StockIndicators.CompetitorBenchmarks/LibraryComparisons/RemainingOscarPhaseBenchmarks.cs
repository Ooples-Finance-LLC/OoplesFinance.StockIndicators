using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingOscarPhaseBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("OscarIndicator", "PhaseChangeIndex")]
    public string Operation { get; set; } = "OscarIndicator";
    protected override DateTime GetTime(int index) => new DateTime(2026, 1, 1).AddHours(index);
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "OscarIndicator" => new OscarIndicator(100),
        "PhaseChangeIndex" => new PhaseChangeIndex(100, 5),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
