using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingTfsPairBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("TFSMboIndicator", "TFSMboPercentagePriceOscillator")]
    public string Operation { get; set; } = "TFSMboIndicator";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "TFSMboIndicator" => new TFSMboIndicator(),
"TFSMboPercentagePriceOscillator" => new TFSMboPercentagePriceOscillator(25),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
