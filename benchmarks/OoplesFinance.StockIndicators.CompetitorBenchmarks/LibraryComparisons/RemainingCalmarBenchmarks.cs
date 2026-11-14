using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingCalmarBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("CalmarRatio")]
    public string Operation { get; set; } = "CalmarRatio";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "CalmarRatio" => new CalmarRatio(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
