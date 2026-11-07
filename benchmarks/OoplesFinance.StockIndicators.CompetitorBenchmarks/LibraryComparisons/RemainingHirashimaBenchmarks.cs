using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingHirashimaBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("HirashimaSugitaRS")]
    public string Operation { get; set; } = "HirashimaSugitaRS";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "HirashimaSugitaRS" => new HirashimaSugitaRS(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
