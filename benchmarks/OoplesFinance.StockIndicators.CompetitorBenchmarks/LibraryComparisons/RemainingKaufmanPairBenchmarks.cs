using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingKaufmanPairBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("KaufmanAdaptiveBands", "KaufmanBinaryWave")]
    public string Operation { get; set; } = "KaufmanAdaptiveBands";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "KaufmanAdaptiveBands" => new KaufmanAdaptiveBands(),
        "KaufmanBinaryWave" => new KaufmanBinaryWave(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
