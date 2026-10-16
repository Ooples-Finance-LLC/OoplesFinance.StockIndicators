using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Compare generated CCI/CMO/TRIX against their own legacy route, preserving
// each staged averaging operation. No native startup convention is substituted.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingOscillatorBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("Cci", "Cmo", "Trix")]
    public string Operation { get; set; } = "Cci";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "Cci" => new Cci(20),
        "Cmo" => new Cmo(20),
        "Trix" => new Trix(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
