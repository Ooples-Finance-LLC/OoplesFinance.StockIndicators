using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TC = Trady.Analysis.Candlestick;
using CandleTuple = (decimal Open, decimal High, decimal Low, decimal Close);
using RangeTuple = (decimal High, decimal Low);

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedTrendCandleBenchmarks
{
    [Params("UpTrend", "DownTrend", "BullishEngulfingPattern", "BearishEngulfingPattern")]
    public string Operation { get; set; } = "";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private const int Period = 14;
    private Bar[] _bars = null!;
    private CandleTuple[] _candles = null!;
    private RangeTuple[] _ranges = null!;
    private CandleTuple[][] _candleSegments = null!;
    private RangeTuple[][] _rangeSegments = null!;
    private int[] _skips = null!;
    private int _priorDop;

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        var fixture = TradyCandleComparison.TrendFixture(Period).IndicatorBars;
        _bars = Enumerable.Range(0, Count).Select(i => fixture[i % fixture.Length]).ToArray();
        _candles = _bars.Select(b => ((decimal)b.Open, (decimal)b.High, (decimal)b.Low, (decimal)b.Close)).ToArray();
        _ranges = _candles.Select(b => (b.High, b.Low)).ToArray();
        int chunks = Math.Min(8, Environment.ProcessorCount);
        _candleSegments = new CandleTuple[chunks][];
        _rangeSegments = new RangeTuple[chunks][];
        _skips = new int[chunks];
        for (int chunk = 0; chunk < chunks; chunk++)
        {
            int start = Count * chunk / chunks, end = Count * (chunk + 1) / chunks;
            // Engulfing needs the previous candle's completed trend: P+1 bars.
            int begin = Math.Max(0, start - (IsEngulfing ? Period + 1 : Period));
            _skips[chunk] = start - begin;
            _candleSegments[chunk] = _candles.AsSpan(begin, end - begin).ToArray();
            _rangeSegments[chunk] = _ranges.AsSpan(begin, end - begin).ToArray();
        }
        var native = NativeSingle().ToArray();
        var parallel = NativeParallel().SelectMany(s => s).ToArray();
        if (!native.SequenceEqual(parallel)) throw new InvalidOperationException("Native trend partition mismatch.");
        var oracle = Enumerable.Range(0, Count).Select(Reference).ToArray();
        if (!native.SequenceEqual(oracle) || !native.Contains(true) || !native.Contains(false))
            throw new InvalidOperationException("Trend fixture must match the oracle and exercise positive/negative cases.");
        var expected = oracle.Select(v => v == true ? 1d : 0d).ToArray();
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = Create();
            using var result = Build(indicator, history);
            AsinFeasibilityBenchmarks.RequireSame(expected, result[indicator].ToArray());
        }
        var ordinary = Create();
        using var reference = Build(ordinary, IndicatorHistoryMode.Full, true);
        AsinFeasibilityBenchmarks.RequireSame(expected, reference[ordinary].ToArray());
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);
    [Benchmark] public IIndicatorRun BuilderReference() => Build(Create(), IndicatorHistoryMode.Full, true);
    [Benchmark(Baseline = true)] public IReadOnlyList<bool?> NativeSingle() => Native(_candles, _ranges);
    [Benchmark] public IReadOnlyList<bool?>[] NativeParallel()
    {
        var output = new IReadOnlyList<bool?>[_skips.Length];
        CpuParallelSettings.LightweightParallel(output.Length, output.Length, chunk =>
            output[chunk] = Native(_candleSegments[chunk], _rangeSegments[chunk], _skips[chunk]));
        return output;
    }
    private bool IsEngulfing => Operation.EndsWith("EngulfingPattern", StringComparison.Ordinal);
    private bool? Reference(int index)
    {
        if (index < (IsEngulfing ? 1 : Period)) return null;
        int end = IsEngulfing ? index - 1 : index;
        if (end < Period) return false;
        bool rising = Operation is "UpTrend" or "BearishEngulfingPattern";
        for (int i = end - Period + 1; i <= end; i++)
        {
            var a = _candles[i - 1]; var b = _candles[i];
            if (rising ? b.High <= a.High || b.Low <= a.Low : b.High >= a.High || b.Low >= a.Low) return false;
        }
        if (!IsEngulfing) return true;
        var previous = _candles[index - 1]; var current = _candles[index];
        bool opposite = rising ? previous.Close > previous.Open && current.Close < current.Open
            : previous.Close < previous.Open && current.Close > current.Open;
        return opposite && Math.Min(current.Open, current.Close) < Math.Min(previous.Open, previous.Close)
            && Math.Max(current.Open, current.Close) > Math.Max(previous.Open, previous.Close);
    }
    // Public start-index selection keeps lookback inputs without constructing
    // unused prefix outputs or copying/normalizing the native Boolean results.
    private IReadOnlyList<bool?> Native(CandleTuple[] candles, RangeTuple[] ranges, int? start = null) => Operation switch
    {
        "UpTrend" => new TC.UpTrendByTuple(ranges, Period).Compute(start, null),
        "DownTrend" => new TC.DownTrendByTuple(ranges, Period).Compute(start, null),
        "BullishEngulfingPattern" => new TC.BullishEngulfingPatternByTuple(candles, Period).Compute(start, null),
        "BearishEngulfingPattern" => new TC.BearishEngulfingPatternByTuple(candles, Period).Compute(start, null),
        _ => throw new InvalidOperationException("Unknown trend candle.")
    };
    private IIndicator Create() => TradyCandleComparison.Create(Operation, Period);
    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history, bool reference = false)
    {
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator).ConfigureHistory(history);
        if (reference) builder.ConfigureBehavior(_ => { });
        return builder.BuildAsync().GetAwaiter().GetResult();
    }
}
