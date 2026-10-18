using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingDiscreteFourierBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("EhlersDiscreteFourierTransform", "EhlersDiscreteFourierTransformSpectralEstimate")]
    public string Operation { get; set; } = "EhlersDiscreteFourierTransform";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "EhlersDiscreteFourierTransform" => new EhlersDiscreteFourierTransform(),
        "EhlersDiscreteFourierTransformSpectralEstimate" => new EhlersDiscreteFourierTransformSpectralEstimate(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
