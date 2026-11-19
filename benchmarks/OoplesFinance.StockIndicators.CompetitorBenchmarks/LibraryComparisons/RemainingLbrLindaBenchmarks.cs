using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingLbrLindaBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("LBRPaintBars", "LindaRaschke310Oscillator")]
    public string Operation { get; set; } = "LBRPaintBars";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "LBRPaintBars" => new LBRPaintBars(),
        "LindaRaschke310Oscillator" => new LindaRaschke310Oscillator(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
