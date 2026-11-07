using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingPriceNormalizersBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("ChartmillValueIndicator", "CenterOfLinearity", "ChopZone")]
    public string Operation { get; set; } = "ChartmillValueIndicator";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "ChartmillValueIndicator" => new ChartmillValueIndicator(),
        "CenterOfLinearity" => new CenterOfLinearity(),
        "ChopZone" => new ChopZone(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
