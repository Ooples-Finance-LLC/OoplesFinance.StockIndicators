using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingGopalGrandBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("GopalakrishnanRangeIndex", "GrandTrendForecasting")]
    public string Operation { get; set; } = "GopalakrishnanRangeIndex";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "GopalakrishnanRangeIndex" => new GopalakrishnanRangeIndex(),
        "GrandTrendForecasting" => new GrandTrendForecasting(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
