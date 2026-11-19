using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingTillsonAliasesBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("T3", "TillsonT3MovingAverage")]
    public string Operation { get; set; } = "T3";
    protected override DateTime GetTime(int index) => new DateTime(2026, 1, 1).AddHours(index);
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "T3" => new T3(100),
        "TillsonT3MovingAverage" => new TillsonT3MovingAverage(100, .7),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
