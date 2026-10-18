using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedTradyMacdBenchmarks : SharedMappedBenchmarkBase
{
    [Params("MovingAverageConvergenceDivergence", "MovingAverageConvergenceDivergenceHistogram")]
    public string Operation { get; set; } = "";
    private bool _histogram;
    private decimal[] _prices = null!;
    private IEnumerable<decimal>[] _prefixes = null!;
    private int[] _ends = null!;

    [GlobalSetup]
    public void Setup()
    {
        _histogram = Operation switch
        {
            "MovingAverageConvergenceDivergence" => false,
            "MovingAverageConvergenceDivergenceHistogram" => true,
            _ => throw new ArgumentException("Unknown Trady MACD operation.")
        };
        ArgumentOutOfRangeException.ThrowIfLessThan(Count, 1);
        var (_, data) = PrepareFixture("Trady.Indicator." + Operation);
        _prices = data.Candles.Select(candle => candle.Close).ToArray();
        int workers = Math.Min(Count, Math.Min(8, Environment.ProcessorCount));
        _ends = Enumerable.Range(1, workers).Select(i => (int)((long)Count * i / workers)).ToArray();
        _prefixes = _ends.Select(end => _prices.Take(end)).ToArray();
        var owner = (EmaDifferenceSignal)Create();
        var expected = EmaDifferenceSignalComparison.GridReference(data.Closes, 12, 26, 9, true, false);
        for (int slot = 0; slot < expected.Length; slot++)
            if (((int)owner.Selection & (1 << slot)) == 0) expected[slot] = new double?[Count];
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            using var run = SharedBenchmarkSupport.BuildCpu(_bars, owner, mode);
            for (int slot = 0; slot < expected.Length; slot++)
            {
                AsinFeasibilityBenchmarks.RequireSame(expected[slot].Select(v => v ?? 0).ToArray(), run[owner.Outputs[slot]].ToArray());
                AsinFeasibilityBenchmarks.RequireSame(expected[slot].Select(v => v.HasValue ? 1d : 0d).ToArray(), run[owner.Outputs[slot + expected.Length]].ToArray());
            }
        }
        var native = EmaDifferenceSignalComparison.DecimalReference(_prices, 12, 26, 9);
        RequireSame(_histogram ? [native[2]] : native, Normalize(NativeSingle()));
        RequireSame(_histogram ? [native[2]] : native, Normalize(NativeParallel()));
    }

    protected override IIndicator Create() => new EmaDifferenceSignal(12, 26, 9, true, false, false,
        EmaDifferenceSignalComparison.Selection(_histogram ? 3 : 2));

    [Benchmark(Baseline = true)]
    public object NativeSingle() => _histogram
        ? new MovingAverageConvergenceDivergenceHistogramByTuple(_prices, 12, 26, 9).Compute()
        : new MovingAverageConvergenceDivergenceByTuple(_prices, 12, 26, 9).Compute();

    [Benchmark]
    public object NativeParallel() => _histogram
        ? Parallel<decimal?>(static (prices, first, last) => new MovingAverageConvergenceDivergenceHistogramByTuple(prices, 12, 26, 9).Compute(startIndex: first, endIndex: last))
        : Parallel<(decimal?, decimal?, decimal?)>(static (prices, first, last) => new MovingAverageConvergenceDivergenceByTuple(prices, 12, 26, 9).Compute(startIndex: first, endIndex: last));

    private T[] Parallel<T>(Func<IEnumerable<decimal>, int, int, IReadOnlyList<T>> calculate)
    {
        var joined = new T[Count];
        CpuParallelSettings.LightweightParallel(_ends.Length, _ends.Length, worker =>
        {
            int first = worker == 0 ? 0 : _ends[worker - 1];
            // Full input prefixes preserve all three recursive EMA states;
            // the public range API returns only this worker's eager output.
            var rows = (List<T>)calculate(_prefixes[worker], first, _ends[worker] - 1);
            if (rows.Count != _ends[worker] - first) throw new InvalidOperationException("Native MACD range length differs.");
            rows.CopyTo(0, joined, first, rows.Count);
        });
        return joined;
    }

    internal static decimal?[][] Normalize(object result) => result switch
    {
        IReadOnlyList<(decimal? Macd, decimal? Signal, decimal? Histogram)> rows =>
            [rows.Select(v => v.Macd).ToArray(), rows.Select(v => v.Signal).ToArray(), rows.Select(v => v.Histogram).ToArray()],
        IReadOnlyList<decimal?> rows => [rows.ToArray()],
        _ => throw new ArgumentException("Unexpected native result type.", nameof(result))
    };

    internal static void RequireSame(decimal?[][] expected, decimal?[][] actual)
    {
        if (expected.Length != actual.Length || expected.Where((column, i) => !column.SequenceEqual(actual[i])).Any())
            throw new InvalidOperationException("Native MACD decimal values or presence differ.");
    }
}
