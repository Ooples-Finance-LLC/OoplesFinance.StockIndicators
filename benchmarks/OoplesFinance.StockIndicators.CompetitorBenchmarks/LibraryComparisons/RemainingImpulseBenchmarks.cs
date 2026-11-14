using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingImpulseBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("EhlersEarlyOnsetTrendIndicator", "EhlersImpulseReaction", "EhlersImpulseResponse")]
    public string Operation { get; set; } = "EhlersEarlyOnsetTrendIndicator";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "EhlersEarlyOnsetTrendIndicator" => new EhlersEarlyOnsetTrendIndicator(30, 100, .85),
        "EhlersImpulseReaction" => new EhlersImpulseReaction(2, 20, .9),
        "EhlersImpulseResponse" => new EhlersImpulseResponse(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
