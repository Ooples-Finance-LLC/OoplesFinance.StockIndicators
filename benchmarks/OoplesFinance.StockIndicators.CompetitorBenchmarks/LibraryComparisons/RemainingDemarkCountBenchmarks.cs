using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingDemarkCountBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("DemarkSetupIndicator", "DemarkReversalPoints")]
    public string Operation { get; set; } = "DemarkSetupIndicator";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "DemarkSetupIndicator" => new DemarkSetupIndicator(),
        "DemarkReversalPoints" => new DemarkReversalPoints(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
