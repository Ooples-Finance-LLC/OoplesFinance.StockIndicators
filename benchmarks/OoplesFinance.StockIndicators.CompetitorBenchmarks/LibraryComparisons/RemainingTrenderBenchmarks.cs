using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingTrenderBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("Trender")]
    public string Operation { get; set; } = "Trender";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "Trender" => new Trender(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
