using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingFmDemodulatorBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("EhlersFMDemodulatorIndicator")]
    public string Operation { get; set; } = "EhlersFMDemodulatorIndicator";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "EhlersFMDemodulatorIndicator" => new EhlersFMDemodulatorIndicator(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
