using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingShinoharaBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("ShinoharaIntensityRatioA", "ShinoharaIntensityRatioB")]
    public string Operation { get; set; } = "ShinoharaIntensityRatioA";
    protected override DateTime GetTime(int index) => new DateTime(2026, 1, 1).AddHours(index);
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "ShinoharaIntensityRatioA" => new ShinoharaIntensityRatioA(100),
        "ShinoharaIntensityRatioB" => new ShinoharaIntensityRatioB(100),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
