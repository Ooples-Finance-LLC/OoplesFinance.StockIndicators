using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingDemarkPressureBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("DemarkPressureRatioV1", "DemarkPressureRatioV2", "DemarkRangeExpansionIndex")]
    public string Operation { get; set; } = "DemarkPressureRatioV1";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    // Exercise positive, negative and neutral candle pressure.
    protected override double GetOpen(int index, double price) => price + (index % 3 - 1) * .5;
    protected override IIndicator Create() => Operation switch
    {
        "DemarkPressureRatioV1" => new DemarkPressureRatioV1(13),
        "DemarkPressureRatioV2" => new DemarkPressureRatioV2(10),
        "DemarkRangeExpansionIndex" => new DemarkRangeExpansionIndex(5),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
