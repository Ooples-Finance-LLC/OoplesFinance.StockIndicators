using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingTruncatedBandBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("EhlersTruncatedBandPassFilter")]
    public string Operation { get; set; } = "EhlersTruncatedBandPassFilter";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "EhlersTruncatedBandPassFilter" => new EhlersTruncatedBandPassFilter(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
