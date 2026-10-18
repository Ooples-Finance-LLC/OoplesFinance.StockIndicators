using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingKaseStopsBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("KaseDevStopV1", "KaseDevStopV2")]
    public string Operation { get; set; } = "KaseDevStopV1";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "KaseDevStopV1" => new KaseDevStopV1(),
        "KaseDevStopV2" => new KaseDevStopV2(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
