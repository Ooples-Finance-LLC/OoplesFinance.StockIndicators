using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingSharpRollingBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("SharpModifiedMovingAverage")]
    public string Operation { get; set; } = "SharpModifiedMovingAverage";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "SharpModifiedMovingAverage" => new SharpModifiedMovingAverage(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
