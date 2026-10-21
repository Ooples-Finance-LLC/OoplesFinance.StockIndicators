using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingRmoQqeBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("RahulMohindarOscillator", "QuantitativeQualitativeEstimation")]
    public string Operation { get; set; } = "RahulMohindarOscillator";
    protected override DateTime GetTime(int index) => new DateTime(2026, 1, 1).AddHours(index);
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "RahulMohindarOscillator" => new RahulMohindarOscillator(10),
        "QuantitativeQualitativeEstimation" => new QuantitativeQualitativeEstimation(14, 5, 2.618, 4.236),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
