using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingBandPassBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("EhlersBandPassFilterV1", "EhlersBandPassFilterV2", "EhlersCycleBandPassFilter", "EhlersSuperPassbandFilter")]
    public string Operation { get; set; } = "EhlersBandPassFilterV1";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "EhlersBandPassFilterV1" => new EhlersBandPassFilterV1(20),
        "EhlersBandPassFilterV2" => new EhlersBandPassFilterV2(20),
        "EhlersCycleBandPassFilter" => new EhlersCycleBandPassFilter(20),
        "EhlersSuperPassbandFilter" => new EhlersSuperPassbandFilter(40, 60, 5, 50),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
