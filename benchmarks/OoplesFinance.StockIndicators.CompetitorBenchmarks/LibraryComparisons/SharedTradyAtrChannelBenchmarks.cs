using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedTradyAtrChannelBenchmarks : SharedMappedBenchmarkBase
{
    [Params("ChandelierExit", "KeltnerChannels")]
    public string Operation { get; set; } = "";
    private bool _keltner;
    private (decimal High, decimal Low, decimal Close)[] _prices = null!;
    private IEnumerable<(decimal High, decimal Low, decimal Close)>[] _prefixes = null!;
    private int[] _ends = null!;

    [GlobalSetup]
    public void Setup()
    {
        _keltner = Operation switch
        {
            "ChandelierExit" => false,
            "KeltnerChannels" => true,
            _ => throw new ArgumentException("Unknown Trady ATR channel operation.")
        };
        ArgumentOutOfRangeException.ThrowIfLessThan(Count, 1);
        var (_, data) = PrepareFixture("Trady.Indicator." + Operation);
        _prices = data.Candles.Select(c => (c.High, c.Low, c.Close)).ToArray();
        int workers = Math.Min(Count, Math.Min(8, Environment.ProcessorCount));
        _ends = Enumerable.Range(1, workers).Select(i => (int)((long)Count * i / workers)).ToArray();
        _prefixes = _ends.Select(end => _prices.Take(end)).ToArray();
        var expected = _keltner
            ? AtrEnvelopeComparison.Reference(_bars, 20, 20, 2, 2)
            : ChandelierComparison.Reference(_bars, 20, 3, false, 0);
        // The owned Keltner contract retains a disabled width column and mask.
        if (_keltner) expected[3] = new double?[Count];
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var owner = Create();
            using var run = SharedBenchmarkSupport.BuildCpu(_bars, owner, mode);
            for (int slot = 0; slot < expected.Length; slot++)
            {
                AsinFeasibilityBenchmarks.RequireSame(expected[slot].Select(v => v ?? 0).ToArray(), run[owner.Outputs[slot]].ToArray());
                AsinFeasibilityBenchmarks.RequireSame(expected[slot].Select(v => v.HasValue ? 1d : 0d).ToArray(), run[owner.Outputs[slot + expected.Length]].ToArray());
            }
        }
        // Preserve the public native tuple order: lower/middle/upper for Keltner,
        // long/short for Chandelier. No normalization is inside the timed arms.
        var native = _keltner
            ? Normalize(AtrEnvelopeComparison.TradyReference(_prices, 20, 20, 2))
            : Normalize(ChandelierComparison.TradyReference(_prices, 20, 3));
        RequireSame(native, Normalize(NativeSingle()));
        RequireSame(native, Normalize(NativeParallel()));
    }

    protected override IIndicator Create() => _keltner
        ? new WindowAtrBands(20, 20, 2, AtrBandCenterMode.FirstSeededExponential, false)
        : new WindowChandelierExit(20);

    [Benchmark(Baseline = true)]
    public object NativeSingle() => _keltner
        ? new KeltnerChannelsByTuple(_prices, 20, 2, 20).Compute()
        : new ChandelierExitByTuple(_prices, 20, 3).Compute();

    [Benchmark]
    public object NativeParallel() => _keltner
        ? Parallel<(decimal?, decimal?, decimal?)>(static (prices, first, last) => new KeltnerChannelsByTuple(prices, 20, 2, 20).Compute(startIndex: first, endIndex: last))
        : Parallel<(decimal?, decimal?)>(static (prices, first, last) => new ChandelierExitByTuple(prices, 20, 3).Compute(startIndex: first, endIndex: last));

    private T[] Parallel<T>(Func<IEnumerable<(decimal High, decimal Low, decimal Close)>, int, int, IReadOnlyList<T>> calculate)
    {
        var joined = new T[Count];
        CpuParallelSettings.LightweightParallel(_ends.Length, _ends.Length, worker =>
        {
            int first = worker == 0 ? 0 : _ends[worker - 1];
            // Full prefixes retain Wilder ATR and first-seeded EMA state.
            var rows = (List<T>)calculate(_prefixes[worker], first, _ends[worker] - 1);
            if (rows.Count != _ends[worker] - first) throw new InvalidOperationException("Native ATR channel range length differs.");
            rows.CopyTo(0, joined, first, rows.Count);
        });
        return joined;
    }

    internal static decimal?[][] Normalize(object result) => result switch
    {
        IReadOnlyList<(decimal? Lower, decimal? Middle, decimal? Upper)> rows =>
            [rows.Select(v => v.Lower).ToArray(), rows.Select(v => v.Middle).ToArray(), rows.Select(v => v.Upper).ToArray()],
        IReadOnlyList<(decimal? Long, decimal? Short)> rows =>
            [rows.Select(v => v.Long).ToArray(), rows.Select(v => v.Short).ToArray()],
        _ => throw new ArgumentException("Unexpected native result type.", nameof(result))
    };

    internal static void RequireSame(decimal?[][] expected, decimal?[][] actual)
    {
        if (expected.Length != actual.Length || expected.Where((column, i) => !column.SequenceEqual(actual[i])).Any())
            throw new InvalidOperationException("Native ATR channel decimal values or presence differ.");
    }
}
