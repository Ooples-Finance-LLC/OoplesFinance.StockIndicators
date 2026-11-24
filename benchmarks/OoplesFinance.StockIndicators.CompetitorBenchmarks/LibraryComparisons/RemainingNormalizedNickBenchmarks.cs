using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingNormalizedNickBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("NormalizedMacd", "NickRypockTrailingReverse")]
    public string Operation { get; set; } = "NormalizedMacd";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "NormalizedMacd" => new NormalizedMacd(),
"NickRypockTrailingReverse" => new NickRypockTrailingReverse(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
