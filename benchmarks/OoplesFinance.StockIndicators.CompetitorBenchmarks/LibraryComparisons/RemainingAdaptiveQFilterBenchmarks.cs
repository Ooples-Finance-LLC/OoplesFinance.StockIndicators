using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingAdaptiveQFilterBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("MovingAverageAdaptiveQ", "MovingAverageAdaptiveFilter")]
    public string Operation { get; set; } = "MovingAverageAdaptiveQ";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "MovingAverageAdaptiveQ" => new MovingAverageAdaptiveQ(),
"MovingAverageAdaptiveFilter" => new MovingAverageAdaptiveFilter(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
