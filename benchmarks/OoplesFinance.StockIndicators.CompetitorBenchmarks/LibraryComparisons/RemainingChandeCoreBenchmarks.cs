using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingChandeCoreBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("ChandeCompositeMomentumIndex", "ChandeForecastOscillator", "ChandeKrollRSquaredIndex")]
    public string Operation { get; set; } = "ChandeCompositeMomentumIndex";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "ChandeCompositeMomentumIndex" => new ChandeCompositeMomentumIndex(),
        "ChandeForecastOscillator" => new ChandeForecastOscillator(),
        "ChandeKrollRSquaredIndex" => new ChandeKrollRSquaredIndex(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
