using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedTradyNativeStochasticBenchmarks : SharedMappedBenchmarkBase
{
    [Params("Raw", "Fast", "Slow", "Full", "FastDifference", "SlowDifference", "FullDifference")]
    public string Operation { get; set; } = "";
    private bool _scalar;
    private (decimal High, decimal Low, decimal Close)[] _prices = null!;
    private IEnumerable<(decimal High, decimal Low, decimal Close)>[] _windows = null!;
    private int[] _ends = null!, _seeds = null!;

    [GlobalSetup]
    public void Setup()
    {
        if (!StochasticSmaComparison.Variants.Contains(Operation)) throw new ArgumentException("Unknown Trady stochastic operation.");
        ArgumentOutOfRangeException.ThrowIfLessThan(Count, 1);
        _scalar = Operation == "Raw" || StochasticSmaComparison.Difference(Operation);
        var (pair, data) = PrepareFixture(StochasticSmaComparison.Id(Operation));
        _prices = data.Candles.Select(c => (c.High, c.Low, c.Close)).ToArray();
        int workers = Math.Min(Count, Math.Min(8, Environment.ProcessorCount));
        _ends = Enumerable.Range(1, workers).Select(i => (int)((long)Count * i / workers)).ToArray();
        // Retain the raw 20-bar window plus each SMA's preceding inputs.
        // Prefixes near startup preserve the native fifty seed and null masks.
        int overlap = 19 + (Operation == "Raw" ? 0 : StochasticSmaComparison.KPeriod(Operation, 3) - 1 + 2);
        _seeds = Enumerable.Range(0, workers).Select(i => Math.Max(0, (i == 0 ? 0 : _ends[i - 1]) - overlap)).ToArray();
        _windows = _ends.Select((end, i) => _prices.Skip(_seeds[i]).Take(end - _seeds[i])).ToArray();
        SharedBenchmarkSupport.VerifyMappedOutputs(_bars, Create, pair.Reference!(data, 20), StochasticSmaComparison.Names(Operation), includeScalarStartup: true);
        var expected = StochasticSmaComparison.NativeReference(_prices, 20, Operation, 3, 3);
        RequireSame(expected, Normalize(NativeSingle()));
        RequireSame(expected, Normalize(NativeParallel()));
    }

    protected override IIndicator Create() => StochasticSmaComparison.Indicator(20, Operation, 3, 3);

    [Benchmark(Baseline = true)]
    public object NativeSingle() => _scalar
        ? StochasticSmaComparison.ScalarTuple(_prices, 20, Operation, 3, 3).Compute()
        : StochasticSmaComparison.Tuple(_prices, 20, Operation, 3, 3).Compute();

    [Benchmark]
    public object NativeParallel() => _scalar
        ? Parallel<decimal?>((prices, first, last) => StochasticSmaComparison.ScalarTuple(prices, 20, Operation, 3, 3).Compute(startIndex: first, endIndex: last))
        : Parallel<(decimal?, decimal?, decimal?)>((prices, first, last) => StochasticSmaComparison.Tuple(prices, 20, Operation, 3, 3).Compute(startIndex: first, endIndex: last));

    private T[] Parallel<T>(Func<IEnumerable<(decimal High, decimal Low, decimal Close)>, int, int, IReadOnlyList<T>> calculate)
    {
        var joined = new T[Count];
        CpuParallelSettings.LightweightParallel(_ends.Length, _ends.Length, worker =>
        {
            int first = worker == 0 ? 0 : _ends[worker - 1];
            var rows = (List<T>)calculate(_windows[worker], first - _seeds[worker], _ends[worker] - _seeds[worker] - 1);
            if (rows.Count != _ends[worker] - first) throw new InvalidOperationException("Native stochastic range length differs.");
            rows.CopyTo(0, joined, first, rows.Count);
        });
        return joined;
    }

    internal static decimal?[][] Normalize(object result) => result switch
    {
        IReadOnlyList<(decimal? K, decimal? D, decimal? J)> rows =>
            [rows.Select(v => v.K).ToArray(), rows.Select(v => v.D).ToArray(), rows.Select(v => v.J).ToArray()],
        IReadOnlyList<decimal?> rows => [rows.ToArray()],
        _ => throw new ArgumentException("Unexpected native result type.", nameof(result))
    };

    internal static void RequireSame(decimal?[][] expected, decimal?[][] actual)
    {
        if (expected.Length != actual.Length || expected.Where((column, i) => !column.SequenceEqual(actual[i])).Any())
            throw new InvalidOperationException("Native stochastic decimal values or presence differ.");
    }
}
