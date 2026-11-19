using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingReverseRsiBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("ReverseEngineeringRsi")]
    public string Operation { get; set; } = "ReverseEngineeringRsi";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "ReverseEngineeringRsi" => new ReverseEngineeringRsi(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
