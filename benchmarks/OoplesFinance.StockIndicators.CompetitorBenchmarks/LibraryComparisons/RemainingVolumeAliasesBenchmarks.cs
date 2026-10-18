using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingVolumeAliasesBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("ElderForceIndex", "MfiCore", "WilliamsAD")]
    public string Operation { get; set; } = "ElderForceIndex";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "ElderForceIndex" => new ElderForceIndex(20),
        "MfiCore" => new MfiCore(20),
        "WilliamsAD" => new WilliamsAD(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
