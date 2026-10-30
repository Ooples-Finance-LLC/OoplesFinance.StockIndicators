using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Compare generated DEMA/TEMA against their own legacy route, preserving
// each rounded EMA stage. No native startup convention is substituted.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingExtrapolationBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("Dema", "Tema")]
    public string Operation { get; set; } = "Dema";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "Dema" => new Dema(20),
        "Tema" => new Tema(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
