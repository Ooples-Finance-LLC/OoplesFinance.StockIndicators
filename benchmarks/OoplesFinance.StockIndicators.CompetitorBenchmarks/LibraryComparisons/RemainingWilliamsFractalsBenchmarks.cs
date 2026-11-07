using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingWilliamsFractalsBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("WilliamsFractalUp", "WilliamsFractalDown", "WilliamsFractalsUp", "WilliamsFractalsDown", "WilliamsFractals")]
    public string Operation { get; set; } = "WilliamsFractalUp";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "WilliamsFractalUp" => new WilliamsFractalUp(),
        "WilliamsFractalDown" => new WilliamsFractalDown(),
        "WilliamsFractalsUp" => new WilliamsFractalsUp(),
        "WilliamsFractalsDown" => new WilliamsFractalsDown(),
        "WilliamsFractals" => new WilliamsFractals(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
