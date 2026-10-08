using BenchmarkDotNet.Attributes;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser]
[Config(typeof(LibraryTimingConfig))]
public class LibraryPairBenchmarks
{
    public IEnumerable<string> Cases
    {
        get
        {
            var all = ComparisonPairs.All.Select(pair => pair.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray();
            var selected = Environment.GetEnvironmentVariable("COMPARISON_PAIR");
            if (selected is not null) return [ComparisonPairs.Get(selected).Id];
            var shardText = Environment.GetEnvironmentVariable("COMPARISON_SHARD");
            var totalText = Environment.GetEnvironmentVariable("COMPARISON_SHARDS");
            if (shardText is null && totalText is null) return all;
            if (!int.TryParse(shardText, out var shard) || !int.TryParse(totalText, out var total) || total < 1 || shard < 0 || shard >= total)
                throw new InvalidOperationException("Both valid comparison shard variables are required.");
            return all.Where((_, index) => index % total == shard);
        }
    }
    [ParamsSource(nameof(Cases))] public string PairId { get; set; } = "";
    [Params(1_000, 10_000)] public int Bars { get; set; }
    private ComparisonPair _pair = null!;
    private CompetitorData _data = null!;
    private const int Period = 20;

    [GlobalSetup]
    public void Setup()
    {
        _pair = ComparisonPairs.Get(PairId);
        _data = ComparisonVerifier.BenchmarkFixture(_pair, Bars);
        // Correctness shards verify retained buffers and repeated calls. Setup checks
        // both actual timed delegates once; repeating isolation here can dominate slow APIs.
        ComparisonVerifier.Check(_pair, _data, Period, verifyIsolation: false);
    }

    [Benchmark(Baseline = true)] public object Ooples() => _pair.Ooples(_data, Period);
    [Benchmark] public object Competitor() => _pair.Competitor(_data, Period);
}
