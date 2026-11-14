using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingV3ModularBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("MovingAverageV3", "ModularFilter")]
    public string Operation { get; set; } = "MovingAverageV3";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "MovingAverageV3" => new MovingAverageV3(),
"ModularFilter" => new ModularFilter(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
