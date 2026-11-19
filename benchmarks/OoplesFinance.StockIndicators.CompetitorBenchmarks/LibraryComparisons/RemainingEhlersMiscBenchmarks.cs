using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingEhlersMiscBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("SuperSmoother", "EhlersVariableIndexDynamicAverage", "EhlersSquelchIndicator", "EhlersSwissArmyKnife")]
    public string Operation { get; set; } = "SuperSmoother";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "SuperSmoother" => new SuperSmoother(10),
        "EhlersVariableIndexDynamicAverage" => new EhlersVariableIndexDynamicAverage(9),
        "EhlersSquelchIndicator" => new EhlersSquelchIndicator(),
        "EhlersSwissArmyKnife" => new EhlersSwissArmyKnife(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
