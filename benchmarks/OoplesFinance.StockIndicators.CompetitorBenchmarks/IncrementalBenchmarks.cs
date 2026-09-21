using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using OoplesFinance.StockIndicators.Streaming;
using Skender.Stock.Indicators;
using TALib;
using Trady.Analysis.Extension;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

/// <summary>
/// The cost of one more bar arriving, with <see cref="History"/> bars already behind it.
///
/// <para>This is the number that decides whether a library can sit in a live feed, and it is the one a
/// whole-series benchmark hides completely. A library with no incremental state has to recompute the entire
/// series to learn the newest value, so its per-bar cost grows with history while a streaming one does not.
/// That is not a slur on the batch libraries - it is what their API offers, and measuring them by calling the
/// API they actually expose is the only honest way to put the two side by side.</para>
///
/// <para>Of the five, this library and QuanTAlib keep state across bars; Skender 2.7.3, TA-Lib and Trady
/// recompute. (Skender gained a streaming API in v3, which is not what a 2.x consumer has.) The recomputing
/// arms are deliberately left in rather than dropped, because "no incremental API" is the finding, and an
/// omitted row would read as an oversight.</para>
///
/// <para>The stateful arms feed the same next bar on every invocation. Their work per call is constant - a
/// rolling window does not grow - so this measures one update, while resetting the state per iteration would
/// measure the reset.</para>
/// </summary>
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class IncrementalBenchmarks
{
    private const int SmaLength = 20;
    private const int RsiLength = 14;
    private const int AtrLength = 14;

    private CompetitorData _history = CompetitorData.Create(1);

    // The same bars plus the one that just arrived. A streaming state is measured advancing over that bar,
    // so a library without one has to recompute the series that includes it - recomputing the history alone
    // measures a smaller problem and flatters the recompute arms by one bar's work.
    private CompetitorData _withNextBar = CompetitorData.Create(1);
    private OhlcvBar _nextBar = new("BENCH", BarTimeframe.Minutes(1), DateTime.UtcNow, DateTime.UtcNow,
        1, 1, 1, 1, 1, true);
    private QuanTAlib.TBar _nextQuanTAlibBar;
    private double _nextClose;

    private SimpleMovingAverageState _sma = new(SmaLength);
    private RelativeStrengthIndexState _rsi = new(RsiLength);
    private AverageTrueRangeState _atr = new(AtrLength);
    private QuanTAlib.Sma _quanTAlibSma = new(SmaLength);
    private QuanTAlib.Atr _quanTAlibAtr = new(AtrLength);

    private double[] _output = [];

    [Params(1_000, 10_000)]
    public int History { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var full = CompetitorData.Create(History + 1);
        _history = full.Take(History);
        _withNextBar = full;
        _output = new double[History + 1];

        var last = History;
        _nextClose = full.Closes[last];
        _nextBar = new OhlcvBar("BENCH", BarTimeframe.Minutes(1), full.Dates[last], full.Dates[last],
            full.Opens[last], full.Highs[last], full.Lows[last], full.Closes[last], full.Volumes[last], true);
        _nextQuanTAlibBar = full.Bars[last];

        _sma = new SimpleMovingAverageState(SmaLength);
        _rsi = new RelativeStrengthIndexState(RsiLength);
        _atr = new AverageTrueRangeState(AtrLength);
        _quanTAlibSma = new QuanTAlib.Sma(SmaLength);
        _quanTAlibAtr = new QuanTAlib.Atr(AtrLength);

        // Warm every stateful arm through the same history the batch arms are handed, so the measured call is
        // the History-th update and not the first.
        for (var i = 0; i < History; i++)
        {
            var bar = new OhlcvBar("BENCH", BarTimeframe.Minutes(1), _history.Dates[i], _history.Dates[i],
                _history.Opens[i], _history.Highs[i], _history.Lows[i], _history.Closes[i],
                _history.Volumes[i], true);
            _sma.Update(bar, true, false);
            _rsi.Update(bar, true, false);
            _atr.Update(bar, true, false);
            _quanTAlibSma.Calc(new QuanTAlib.TValue(_history.Closes[i], true, false));
            _quanTAlibAtr.Calc(_history.Bars[i]);
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _sma.Dispose();
    }

    // ------------------------------------------------------------------ SMA(20), one more bar

    [BenchmarkCategory("Sma"), Benchmark(Baseline = true, Description = "Ooples streaming state")]
    public double SmaOoplesStreaming() => _sma.Update(_nextBar, true, false).Value;

    [BenchmarkCategory("Sma"), Benchmark(Description = "QuanTAlib incremental")]
    public double SmaQuanTAlibIncremental() =>
        _quanTAlibSma.Calc(new QuanTAlib.TValue(_nextClose, true, false)).Value;

    [BenchmarkCategory("Sma"), Benchmark(Description = "Skender (full recompute)")]
    public int SmaSkenderRecompute() => _withNextBar.Quotes.GetSma(SmaLength).ToList().Count;

    [BenchmarkCategory("Sma"), Benchmark(Description = "TA-Lib (full recompute)")]
    public TALib.Core.RetCode SmaTaLibRecompute() =>
        Functions.Sma<double>(_withNextBar.Closes, Range.All, _output, out _, SmaLength);

    [BenchmarkCategory("Sma"), Benchmark(Description = "Trady (full recompute)")]
    public int SmaTradyRecompute() => _withNextBar.Candles.Sma(SmaLength).Count;

    // ------------------------------------------------------------------ RSI(14), one more bar

    [BenchmarkCategory("Rsi"), Benchmark(Baseline = true, Description = "Ooples streaming state")]
    public double RsiOoplesStreaming() => _rsi.Update(_nextBar, true, false).Value;

    [BenchmarkCategory("Rsi"), Benchmark(Description = "Skender (full recompute)")]
    public int RsiSkenderRecompute() => _withNextBar.Quotes.GetRsi(RsiLength).ToList().Count;

    [BenchmarkCategory("Rsi"), Benchmark(Description = "TA-Lib (full recompute)")]
    public TALib.Core.RetCode RsiTaLibRecompute() =>
        Functions.Rsi<double>(_withNextBar.Closes, Range.All, _output, out _, RsiLength);

    [BenchmarkCategory("Rsi"), Benchmark(Description = "Trady (full recompute)")]
    public int RsiTradyRecompute() => _withNextBar.Candles.Rsi(RsiLength).Count;

    // ------------------------------------------------------------------ ATR(14), one more bar

    [BenchmarkCategory("Atr"), Benchmark(Baseline = true, Description = "Ooples streaming state")]
    public double AtrOoplesStreaming() => _atr.Update(_nextBar, true, false).Value;

    [BenchmarkCategory("Atr"), Benchmark(Description = "QuanTAlib incremental")]
    public double AtrQuanTAlibIncremental() => _quanTAlibAtr.Calc(_nextQuanTAlibBar).Value;

    [BenchmarkCategory("Atr"), Benchmark(Description = "Skender (full recompute)")]
    public int AtrSkenderRecompute() => _withNextBar.Quotes.GetAtr(AtrLength).ToList().Count;

    [BenchmarkCategory("Atr"), Benchmark(Description = "TA-Lib (full recompute)")]
    public TALib.Core.RetCode AtrTaLibRecompute() =>
        Functions.Atr<double>(_withNextBar.Highs, _withNextBar.Lows, _withNextBar.Closes, Range.All, _output, out _,
            AtrLength);

    [BenchmarkCategory("Atr"), Benchmark(Description = "Trady (full recompute)")]
    public int AtrTradyRecompute() => _withNextBar.Candles.Atr(AtrLength).Count;
}
