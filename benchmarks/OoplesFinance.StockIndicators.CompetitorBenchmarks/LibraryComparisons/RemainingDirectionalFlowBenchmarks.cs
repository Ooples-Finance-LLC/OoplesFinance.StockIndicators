using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Compare generated ADX/MassIndex/ChaikinOscillator against their own legacy route, preserving
// each staged averaging operation. No native startup convention is substituted.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingDirectionalFlowBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("Adx", "MassIndex", "ChaikinOscillator")]
    public string Operation { get; set; } = "Adx";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "Adx" => new Adx(20),
        "MassIndex" => new MassIndex(9, 25),
        "ChaikinOscillator" => new ChaikinOscillator(3, 10),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
