using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingHurstRankBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("EhlersHurstCoefficient", "EhlersNoiseEliminationTechnology", "EhlersSpearmanRankIndicator")]
    public string Operation { get; set; } = "EhlersHurstCoefficient";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "EhlersHurstCoefficient" => new EhlersHurstCoefficient(),
        "EhlersNoiseEliminationTechnology" => new EhlersNoiseEliminationTechnology(14),
        "EhlersSpearmanRankIndicator" => new EhlersSpearmanRankIndicator(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
