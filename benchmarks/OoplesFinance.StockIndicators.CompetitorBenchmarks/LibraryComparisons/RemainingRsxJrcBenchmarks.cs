using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingRsxJrcBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("JmaRsxClone", "JrcFractalDimension")]
    public string Operation { get; set; } = "JmaRsxClone";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "JmaRsxClone" => new JmaRsxClone(),
        "JrcFractalDimension" => new JrcFractalDimension(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
