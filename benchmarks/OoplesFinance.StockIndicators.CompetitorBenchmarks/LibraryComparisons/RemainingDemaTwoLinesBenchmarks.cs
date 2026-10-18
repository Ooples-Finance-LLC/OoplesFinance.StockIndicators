using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingDemaTwoLinesBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("Dema2Lines")]
    public string Operation { get; set; } = "Dema2Lines";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "Dema2Lines" => new Dema2Lines(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
