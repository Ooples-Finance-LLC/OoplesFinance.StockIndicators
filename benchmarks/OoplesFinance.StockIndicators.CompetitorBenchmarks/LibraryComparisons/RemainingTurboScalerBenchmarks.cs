using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingTurboScalerBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("TurboScaler")]
    public string Operation { get; set; } = "TurboScaler";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "TurboScaler" => new TurboScaler(50,.5),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
