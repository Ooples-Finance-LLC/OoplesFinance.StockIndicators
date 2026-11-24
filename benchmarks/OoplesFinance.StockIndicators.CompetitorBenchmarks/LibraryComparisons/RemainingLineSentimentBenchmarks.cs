using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingLineSentimentBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("SimpleLines", "SentimentZoneOscillator")]
    public string Operation { get; set; } = "SimpleLines";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "SimpleLines" => new SimpleLines(),
"SentimentZoneOscillator" => new SentimentZoneOscillator(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
