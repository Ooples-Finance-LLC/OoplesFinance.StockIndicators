using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingFourierConvolutionBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("EhlersConvolutionIndicator", "EhlersFourierSeriesAnalysis", "EhlersPhaseCalculation")]
    public string Operation { get; set; } = "EhlersConvolutionIndicator";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "EhlersConvolutionIndicator" => new EhlersConvolutionIndicator(),
        "EhlersFourierSeriesAnalysis" => new EhlersFourierSeriesAnalysis(),
        "EhlersPhaseCalculation" => new EhlersPhaseCalculation(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
