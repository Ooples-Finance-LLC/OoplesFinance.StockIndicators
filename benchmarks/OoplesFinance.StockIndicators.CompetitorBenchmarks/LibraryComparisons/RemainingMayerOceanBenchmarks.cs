using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingMayerOceanBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("MayerMultiple", "OceanIndicator")]
    public string Operation { get; set; } = "MayerMultiple";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "MayerMultiple" => new MayerMultiple(),
        "OceanIndicator" => new OceanIndicator(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
