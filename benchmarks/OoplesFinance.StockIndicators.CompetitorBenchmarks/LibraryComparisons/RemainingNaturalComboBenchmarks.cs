using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingNaturalComboBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("NaturalDirectionalCombo")]
    public string Operation { get; set; } = "NaturalDirectionalCombo";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "NaturalDirectionalCombo" => new NaturalDirectionalCombo(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
