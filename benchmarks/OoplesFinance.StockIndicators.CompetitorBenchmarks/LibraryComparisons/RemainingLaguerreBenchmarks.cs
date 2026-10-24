using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingLaguerreBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("EhlersLaguerreFilter", "EhlersLaguerreRsi")]
    public string Operation { get; set; } = "EhlersLaguerreFilter";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "EhlersLaguerreFilter" => new EhlersLaguerreFilter(20),
        "EhlersLaguerreRsi" => new EhlersLaguerreRsi(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
