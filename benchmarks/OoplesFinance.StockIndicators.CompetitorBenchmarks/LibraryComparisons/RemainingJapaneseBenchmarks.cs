using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingJapaneseBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("JapaneseCorrelationCoefficient")]
    public string Operation { get; set; } = "JapaneseCorrelationCoefficient";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "JapaneseCorrelationCoefficient" => new JapaneseCorrelationCoefficient(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
