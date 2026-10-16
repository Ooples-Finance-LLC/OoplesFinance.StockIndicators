using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingRecursiveEnvelopeBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("SmartEnvelope", "DEnvelope", "GChannels")]
    public string Operation { get; set; } = "SmartEnvelope";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "SmartEnvelope" => new SmartEnvelope(),
        "DEnvelope" => new DEnvelope(),
        "GChannels" => new GChannels(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
