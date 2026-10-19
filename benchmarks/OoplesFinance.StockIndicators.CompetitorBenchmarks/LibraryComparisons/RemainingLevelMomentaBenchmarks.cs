using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingLevelMomentaBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("MultiLevelIndicator", "MomentaRelativeStrengthIndex")]
    public string Operation { get; set; } = "MultiLevelIndicator";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "MultiLevelIndicator" => new MultiLevelIndicator(),
"MomentaRelativeStrengthIndex" => new MomentaRelativeStrengthIndex(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
