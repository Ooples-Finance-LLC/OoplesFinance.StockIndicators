using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingRollingExtremaBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("RollingMax", "RollingMin")]
    public string Operation { get; set; } = "RollingMax";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "RollingMax" => new RollingMax(),
        "RollingMin" => new RollingMin(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
