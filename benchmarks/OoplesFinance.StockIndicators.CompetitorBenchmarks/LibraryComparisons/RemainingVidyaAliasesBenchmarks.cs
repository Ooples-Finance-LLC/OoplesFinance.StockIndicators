using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingVidyaAliasesBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("VariableIndexDynamicAverage", "Vidya")]
    public string Operation { get; set; } = "VariableIndexDynamicAverage";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "VariableIndexDynamicAverage" => new VariableIndexDynamicAverage(),
        "Vidya" => new Vidya(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
