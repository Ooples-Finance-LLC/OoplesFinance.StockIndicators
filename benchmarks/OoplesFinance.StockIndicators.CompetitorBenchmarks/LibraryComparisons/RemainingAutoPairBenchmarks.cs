using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingAutoPairBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("AutoFilter", "AutoDispersionBands")]
    public string Operation { get; set; } = "AutoFilter";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "AutoFilter" => new AutoFilter(5),
        "AutoDispersionBands" => new AutoDispersionBands(5),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
