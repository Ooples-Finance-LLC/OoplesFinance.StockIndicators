using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingStandardizedScoreBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("ZScore", "FastZScore", "InverseFisherZScore", "InverseFisherFastZScore")]
    public string Operation { get; set; } = "ZScore";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "ZScore" => new ZScore(14),
        "FastZScore" => new FastZScore(200),
        "InverseFisherZScore" => new InverseFisherZScore(100),
        "InverseFisherFastZScore" => new InverseFisherFastZScore(50),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
