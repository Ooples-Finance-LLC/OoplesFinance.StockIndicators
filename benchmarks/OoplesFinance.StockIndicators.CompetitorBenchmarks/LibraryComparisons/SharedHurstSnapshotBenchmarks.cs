using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedHurstSnapshotBenchmarks
{
    [Params(1000, 10000)] public int Count { get; set; }
    [Params("Skender.GetHurst")] public string Operation { get; set; } = "Skender.GetHurst";
    private Bar[] _bars = null!;
    [GlobalSetup]
    public void Setup()
    {
        var pair = ComparisonPairs.Get(Operation);
        var data = ComparisonVerifier.BenchmarkFixture(pair, Count);
        _bars = data.IndicatorBars;
        ComparisonVerifier.Compare(pair.Reference!(data, 20), HurstComparison.Series(Snapshot()),
            "Hurst saved-baseline fixture", HurstComparison.Budget);
    }
    // Match saved native period20 inputs; setup and timing never call the native implementation.
    [Benchmark] public IReadOnlyList<HurstValue> Snapshot() => HurstSnapshot.Calculate(_bars, 20);
}
