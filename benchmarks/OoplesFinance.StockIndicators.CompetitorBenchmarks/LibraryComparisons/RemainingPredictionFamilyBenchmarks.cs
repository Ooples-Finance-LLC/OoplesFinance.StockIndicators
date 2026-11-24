using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingPredictionFamilyBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("EhlersMesaPredictIndicatorV1", "EhlersMesaPredictIndicatorV2", "EhlersAnticipateIndicator")]
    public string Operation { get; set; } = "EhlersMesaPredictIndicatorV1";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "EhlersMesaPredictIndicatorV1" => new EhlersMesaPredictIndicatorV1(),
        "EhlersMesaPredictIndicatorV2" => new EhlersMesaPredictIndicatorV2(),
        "EhlersAnticipateIndicator" => new EhlersAnticipateIndicator(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
