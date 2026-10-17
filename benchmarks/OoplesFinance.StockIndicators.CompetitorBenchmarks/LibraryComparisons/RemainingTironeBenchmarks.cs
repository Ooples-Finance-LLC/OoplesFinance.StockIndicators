using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingTironeBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("TironeLevels")]
    public string Operation { get; set; } = "TironeLevels";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "TironeLevels" => new TironeLevels(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
