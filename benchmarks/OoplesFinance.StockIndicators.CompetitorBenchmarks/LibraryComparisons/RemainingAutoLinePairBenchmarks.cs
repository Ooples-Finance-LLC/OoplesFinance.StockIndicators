using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingAutoLinePairBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("AutoLine", "AutoLineWithDrift")]
    public string Operation { get; set; } = "AutoLine";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "AutoLine" => new AutoLine(5),
        "AutoLineWithDrift" => new AutoLineWithDrift(5),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
