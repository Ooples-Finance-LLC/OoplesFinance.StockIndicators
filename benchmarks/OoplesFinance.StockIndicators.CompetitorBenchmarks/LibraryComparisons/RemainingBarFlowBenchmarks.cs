using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Compare generated EMV/VWAP/AroonUp/AroonDown against their own legacy route, preserving
// each staged averaging operation. No native startup convention is substituted.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingBarFlowBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("Emv", "Vwap", "AroonUp", "AroonDown")]
    public string Operation { get; set; } = "Emv";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "Emv" => new Emv(20),
        "Vwap" => new Vwap(20),
        "AroonUp" => new AroonUp(20),
        "AroonDown" => new AroonDown(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
