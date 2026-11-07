using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingStepAndSqueezeBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("SettingLessTrendStepFiltering", "SqueezeMomentumIndicator")]
    public string Operation { get; set; } = "SettingLessTrendStepFiltering";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "SettingLessTrendStepFiltering" => new SettingLessTrendStepFiltering(14),
"SqueezeMomentumIndicator" => new SqueezeMomentumIndicator(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
