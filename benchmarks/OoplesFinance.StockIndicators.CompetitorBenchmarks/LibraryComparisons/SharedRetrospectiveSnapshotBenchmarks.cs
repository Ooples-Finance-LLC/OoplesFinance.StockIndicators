using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedRetrospectiveSnapshotBenchmarks
{
    [Params(1000, 10000)] public int Count { get; set; }
    [Params("Skender.GetDpo", "Skender.GetFcb")] public string Operation { get; set; } = "";
    private Bar[] _bars = null!;
    [GlobalSetup]
    public void Setup()
    {
        var pair = ComparisonPairs.Get(Operation);
        var data = ComparisonVerifier.BenchmarkFixture(pair, Count); _bars = data.IndicatorBars;
        // Same period/span20 and generator as the saved competitor measurements.
        // These APIs return retrospective records, not Full/Latest builder runs.
        var expected = pair.Reference!(data, 20);
        double?[][] actual = Snapshot() switch
        {
            IReadOnlyList<DetrendedPriceValue> rows => [rows.Select(r => r.Dpo).ToArray(), rows.Select(r => r.Sma).ToArray()],
            IReadOnlyList<FractalChaosValue> rows => [rows.Select(r => r.Upper).ToArray(), rows.Select(r => r.Lower).ToArray()],
            _ => throw new InvalidOperationException("Unknown snapshot result.")
        };
        for (int slot = 0; slot < 2; slot++)
        {
            var column = expected.Outputs[pair.OutputNames![slot]];
            var present = column.Present!;
            AsinFeasibilityBenchmarks.RequireSame(present.Select(p => p ? 1d : 0d).ToArray(), actual[slot].Select(v => v.HasValue ? 1d : 0d).ToArray());
            AsinFeasibilityBenchmarks.RequireSame(column.Values.Select((v, i) => present[i] ? v : 0).ToArray(), actual[slot].Select(v => v ?? 0).ToArray());
        }
    }
    [Benchmark]
    public object Snapshot() => Operation switch
    {
        "Skender.GetDpo" => DetrendedPriceSnapshot.Calculate(_bars, 20),
        "Skender.GetFcb" => FractalSnapshot.ChaosBands(_bars, 20),
        _ => throw new ArgumentException("Unknown retrospective snapshot.")
    };
}
