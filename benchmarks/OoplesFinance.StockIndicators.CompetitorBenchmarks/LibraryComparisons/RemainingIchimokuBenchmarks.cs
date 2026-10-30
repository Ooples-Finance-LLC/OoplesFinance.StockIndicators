using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingIchimokuBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("IchimokuTenkanSen", "IchimokuKijunSen", "IchimokuSenkouSpanA", "IchimokuSenkouSpanB", "IchimokuChikouSpan")]
    public string Operation { get; set; } = "IchimokuTenkanSen";
    protected override DateTime GetTime(int index) => new DateTime(2026, 1, 1).AddHours(index);
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "IchimokuTenkanSen" => new IchimokuTenkanSen(100),
        "IchimokuKijunSen" => new IchimokuKijunSen(100),
        "IchimokuSenkouSpanA" => new IchimokuSenkouSpanA(100),
        "IchimokuSenkouSpanB" => new IchimokuSenkouSpanB(100),
        "IchimokuChikouSpan" => new IchimokuChikouSpan(100),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
