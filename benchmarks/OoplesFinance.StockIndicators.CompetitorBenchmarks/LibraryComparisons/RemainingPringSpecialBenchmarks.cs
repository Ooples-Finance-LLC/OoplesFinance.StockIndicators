using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingPringSpecialBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("PringSpecialK")]
    public string Operation { get; set; } = "PringSpecialK";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "PringSpecialK" => new PringSpecialK(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
