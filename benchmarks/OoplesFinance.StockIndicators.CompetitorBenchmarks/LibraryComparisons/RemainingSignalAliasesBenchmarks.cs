using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingSignalAliasesBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("KlingerSignal", "ChandeMomentumOscillatorSignal", "MassIndexCore")]
    public string Operation { get; set; } = "KlingerSignal";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "KlingerSignal" => new KlingerSignal(20),
        "ChandeMomentumOscillatorSignal" => new ChandeMomentumOscillatorSignal(20),
        "MassIndexCore" => new MassIndexCore(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
