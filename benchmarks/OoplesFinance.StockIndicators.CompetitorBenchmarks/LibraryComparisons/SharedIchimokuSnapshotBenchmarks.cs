using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedIchimokuSnapshotBenchmarks
{
    [Params(1000, 10000)] public int Count { get; set; }
    [Params("Skender.GetIchimoku", "Trady.Indicator.IchimokuCloud")] public string Operation { get; set; } = "";
    private Bar[] _bars = null!;
    [GlobalSetup]
    public void Setup()
    {
        var pair = ComparisonPairs.Get(Operation);
        var data = ComparisonVerifier.BenchmarkFixture(pair, Count); _bars = data.IndicatorBars;
        var expected = pair.Reference!(data, 20);
        var snapshot = Snapshot();
        IchimokuCloudValue[] rows;
        if (snapshot is IndexedIchimokuCloudValue[] indexed)
        {
            if (indexed.Length != Count + 51 || indexed.Where((r, i) => r.Index != i - 25L).Any())
                throw new InvalidOperationException("Extended coordinates differ.");
            rows = indexed.Select(r => r.Value).ToArray();
        }
        else rows = ((IReadOnlyList<IchimokuCloudValue>)snapshot).ToArray();
        double?[][] actual = [rows.Select(r => r.Conversion).ToArray(), rows.Select(r => r.Base).ToArray(), rows.Select(r => r.LeadingA).ToArray(), rows.Select(r => r.LeadingB).ToArray(), rows.Select(r => r.Lagging).ToArray()];
        for (int slot = 0; slot < 5; slot++)
        {
            var column = expected.Outputs[pair.OutputNames![slot]]; var present = column.Present!;
            AsinFeasibilityBenchmarks.RequireSame(present.Select(p => p ? 1d : 0d).ToArray(), actual[slot].Select(v => v.HasValue ? 1d : 0d).ToArray());
            AsinFeasibilityBenchmarks.RequireSame(column.Values.Select((v, i) => present[i] ? v : 0).ToArray(), actual[slot].Select(v => v ?? 0).ToArray());
        }
    }
    // Original default9/26/52 inputs, saved native timing reused. Materialize
    // every extended row inside the timed call; never time deferred enumeration.
    [Benchmark]
    public object Snapshot() => Operation switch
    {
        "Skender.GetIchimoku" => IchimokuCloudSnapshot.Calculate(_bars),
        "Trady.Indicator.IchimokuCloud" => IchimokuCloudSnapshot.Extended(_bars).ToArray(),
        _ => throw new ArgumentException("Unknown Ichimoku snapshot.")
    };
}
