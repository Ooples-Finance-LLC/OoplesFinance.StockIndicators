using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// These aliases use a zero-seeded rounded recurrence. Existing native SMMA
// fixtures use other startup contracts and are not interchangeable baselines.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingWilderBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("Wwma", "Smma", "ModifiedMa")]
    public string Operation { get; set; } = "Wwma";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "Wwma" => new Wwma(20),
        "Smma" => new Smma(20),
        "ModifiedMa" => new ModifiedMa(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
