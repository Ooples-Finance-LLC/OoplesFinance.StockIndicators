using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedZigZagSnapshotBenchmarks
{
    [Params(1000, 10000)] public int Count { get; set; }
    [Params("Skender.GetZigZag")] public string Operation { get; set; } = "Skender.GetZigZag";
    private Bar[] _bars = null!;
    [GlobalSetup]
    public void Setup()
    {
        var pair = ComparisonPairs.Get(Operation);
        var data = ComparisonVerifier.BenchmarkFixture(pair, Count); _bars = data.IndicatorBars;
        ComparisonVerifier.Compare(pair.Reference!(data, 20), ZigZagComparison.Series(Snapshot()),
            "ZigZag saved-baseline fixture", IndicatorErrorBudget.Exact);
    }
    // Original5percent/close inputs. No native setup or timing calls.
    [Benchmark] public IReadOnlyList<ZigZagValue> Snapshot() => ZigZagSnapshot.Calculate(_bars);
}
