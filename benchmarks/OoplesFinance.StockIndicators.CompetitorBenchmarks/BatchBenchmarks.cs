using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using OoplesFinance.StockIndicators.Builder;
using Skender.Stock.Indicators;
using TALib;
using Trady.Analysis.Extension;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

/// <summary>
/// One-shot cost of computing an indicator over a whole series, this library against Skender.Stock.Indicators,
/// TA-Lib (TALib.NETCore), Trady and QuanTAlib.
///
/// <para>Each indicator is its own category, so BenchmarkDotNet prints the libraries side by side per indicator
/// rather than one ranking over unlike work. The v2 builder arm is the baseline in each category because it is
/// the path this library recommends; the v1 batch arm is measured too, since that is what existing users are
/// on and the difference between the two is the point of v2.</para>
///
/// <para><b>Every arm materialises its result.</b> Skender returns lazy enumerables and this library's builder
/// does no work until <c>Start()</c>, so an arm that returns the un-enumerated result measures the construction
/// of an iterator and reports it as the indicator. Where a library produces several series at once (bands, MACD
/// lines, %K/%D) every one of them is requested, because asking one library for three series and another for
/// one is not a comparison.</para>
///
/// <para>Libraries differ in what they even implement - QuanTAlib 1.0.0 ships moving averages, statistics and
/// ATR but no RSI, Bollinger Bands, MACD or stochastic, and Trady computes in <c>decimal</c> rather than
/// <c>double</c>. <see cref="CompetitorCoverage"/> states that per indicator, and an absent arm below always
/// means the library has no such indicator, never that it was left out for being slow.</para>
/// </summary>
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class BatchBenchmarks
{
    private const int SmaLength = 20;
    private const int EmaLength = 20;
    private const int RsiLength = 14;
    private const int AtrLength = 14;
    private const int BollingerLength = 20;
    private const double BollingerStdDev = 2;
    private const int MacdFast = 12;
    private const int MacdSlow = 26;
    private const int MacdSignal = 9;
    private const int StochasticK = 14;
    private const int StochasticD = 3;

    private CompetitorData _data = CompetitorData.Create(1);
    private double[] _output = [];
    private double[] _output2 = [];
    private double[] _output3 = [];

    [Params(1_000, 10_000)]
    public int Bars { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _data = CompetitorData.Create(Bars);
        _output = new double[Bars];
        _output2 = new double[Bars];
        _output3 = new double[Bars];
    }

    // ------------------------------------------------------------------ SMA(20)

    [BenchmarkCategory("Sma"), Benchmark(Baseline = true, Description = "Ooples v2 builder")]
    public int SmaOoplesBuilder()
    {
        var handle = default(SeriesHandle);
        using var runtime = new StockIndicatorBuilder(IndicatorDataSource.FromBatch(_data.NewStockData()))
            .ConfigureIndicators(indicators => handle = indicators.Sma(SmaLength))
            .Build();
        runtime.Start();
        return runtime.GetSeries(handle).AsSpan().Length;
    }

    [BenchmarkCategory("Sma"), Benchmark(Description = "Ooples v1 batch")]
    public int SmaOoplesBatch() =>
        _data.NewStockData().CalculateSimpleMovingAverage(SmaLength).CustomValuesList.Count;

    [BenchmarkCategory("Sma"), Benchmark(Description = "Skender")]
    public int SmaSkender() => _data.Quotes.GetSma(SmaLength).ToList().Count;

    [BenchmarkCategory("Sma"), Benchmark(Description = "TA-Lib")]
    public TALib.Core.RetCode SmaTaLib() =>
        Functions.Sma<double>(_data.Closes, Range.All, _output, out _, SmaLength);

    [BenchmarkCategory("Sma"), Benchmark(Description = "Trady")]
    public int SmaTrady() => _data.Candles.Sma(SmaLength).Count;

    [BenchmarkCategory("Sma"), Benchmark(Description = "QuanTAlib")]
    public double SmaQuanTAlib()
    {
        var sma = new QuanTAlib.Sma(SmaLength);
        var last = 0d;
        foreach (var close in _data.Closes)
        {
            last = sma.Calc(new QuanTAlib.TValue(close, true, false)).Value;
        }

        return last;
    }

    // ------------------------------------------------------------------ EMA(20)

    [BenchmarkCategory("Ema"), Benchmark(Baseline = true, Description = "Ooples v2 builder")]
    public int EmaOoplesBuilder()
    {
        var handle = default(SeriesHandle);
        using var runtime = new StockIndicatorBuilder(IndicatorDataSource.FromBatch(_data.NewStockData()))
            .ConfigureIndicators(indicators => handle = indicators.Ema(EmaLength))
            .Build();
        runtime.Start();
        return runtime.GetSeries(handle).AsSpan().Length;
    }

    [BenchmarkCategory("Ema"), Benchmark(Description = "Ooples v1 batch")]
    public int EmaOoplesBatch() =>
        _data.NewStockData().CalculateExponentialMovingAverage(EmaLength).CustomValuesList.Count;

    [BenchmarkCategory("Ema"), Benchmark(Description = "Skender")]
    public int EmaSkender() => _data.Quotes.GetEma(EmaLength).ToList().Count;

    [BenchmarkCategory("Ema"), Benchmark(Description = "TA-Lib")]
    public TALib.Core.RetCode EmaTaLib() =>
        Functions.Ema<double>(_data.Closes, Range.All, _output, out _, EmaLength);

    [BenchmarkCategory("Ema"), Benchmark(Description = "Trady")]
    public int EmaTrady() => _data.Candles.Ema(EmaLength).Count;

    [BenchmarkCategory("Ema"), Benchmark(Description = "QuanTAlib")]
    public double EmaQuanTAlib()
    {
        var ema = new QuanTAlib.Ema(EmaLength, true);
        var last = 0d;
        foreach (var close in _data.Closes)
        {
            last = ema.Calc(new QuanTAlib.TValue(close, true, false)).Value;
        }

        return last;
    }

    // ------------------------------------------------------------------ RSI(14)

    [BenchmarkCategory("Rsi"), Benchmark(Baseline = true, Description = "Ooples v2 builder")]
    public int RsiOoplesBuilder()
    {
        var handle = default(SeriesHandle);
        using var runtime = new StockIndicatorBuilder(IndicatorDataSource.FromBatch(_data.NewStockData()))
            .ConfigureIndicators(indicators => handle = indicators.Rsi(RsiLength))
            .Build();
        runtime.Start();
        return runtime.GetSeries(handle).AsSpan().Length;
    }

    [BenchmarkCategory("Rsi"), Benchmark(Description = "Ooples v1 batch")]
    public int RsiOoplesBatch() =>
        _data.NewStockData().CalculateRelativeStrengthIndex(length: RsiLength).CustomValuesList.Count;

    [BenchmarkCategory("Rsi"), Benchmark(Description = "Skender")]
    public int RsiSkender() => _data.Quotes.GetRsi(RsiLength).ToList().Count;

    [BenchmarkCategory("Rsi"), Benchmark(Description = "TA-Lib")]
    public TALib.Core.RetCode RsiTaLib() =>
        Functions.Rsi<double>(_data.Closes, Range.All, _output, out _, RsiLength);

    [BenchmarkCategory("Rsi"), Benchmark(Description = "Trady")]
    public int RsiTrady() => _data.Candles.Rsi(RsiLength).Count;

    // ------------------------------------------------------------------ ATR(14)

    [BenchmarkCategory("Atr"), Benchmark(Baseline = true, Description = "Ooples v2 builder")]
    public int AtrOoplesBuilder()
    {
        var handle = default(SeriesHandle);
        using var runtime = new StockIndicatorBuilder(IndicatorDataSource.FromBatch(_data.NewStockData()))
            .ConfigureIndicators(indicators => handle = indicators.Atr(AtrLength))
            .Build();
        runtime.Start();
        return runtime.GetSeries(handle).AsSpan().Length;
    }

    [BenchmarkCategory("Atr"), Benchmark(Description = "Ooples v1 batch")]
    public int AtrOoplesBatch() =>
        _data.NewStockData().CalculateAverageTrueRange(length: AtrLength).CustomValuesList.Count;

    [BenchmarkCategory("Atr"), Benchmark(Description = "Skender")]
    public int AtrSkender() => _data.Quotes.GetAtr(AtrLength).ToList().Count;

    [BenchmarkCategory("Atr"), Benchmark(Description = "TA-Lib")]
    public TALib.Core.RetCode AtrTaLib() =>
        Functions.Atr<double>(_data.Highs, _data.Lows, _data.Closes, Range.All, _output, out _, AtrLength);

    [BenchmarkCategory("Atr"), Benchmark(Description = "Trady")]
    public int AtrTrady() => _data.Candles.Atr(AtrLength).Count;

    [BenchmarkCategory("Atr"), Benchmark(Description = "QuanTAlib")]
    public double AtrQuanTAlib()
    {
        var atr = new QuanTAlib.Atr(AtrLength);
        var last = 0d;
        foreach (var bar in _data.Bars)
        {
            last = atr.Calc(bar).Value;
        }

        return last;
    }

    // ------------------------------------------------------------------ Bollinger Bands(20, 2)

    [BenchmarkCategory("BollingerBands"), Benchmark(Baseline = true, Description = "Ooples v2 builder")]
    public int BollingerOoplesBuilder()
    {
        var bands = default(Builder.BollingerBandsResult);
        using var runtime = new StockIndicatorBuilder(IndicatorDataSource.FromBatch(_data.NewStockData()))
            .ConfigureIndicators(indicators =>
                bands = indicators.BollingerBands(BollingerLength, BollingerStdDev))
            .Build();
        runtime.Start();
        return runtime.GetSeries(bands.Upper).AsSpan().Length
            + runtime.GetSeries(bands.Middle).AsSpan().Length
            + runtime.GetSeries(bands.Lower).AsSpan().Length;
    }

    [BenchmarkCategory("BollingerBands"), Benchmark(Description = "Ooples v1 batch")]
    public int BollingerOoplesBatch() =>
        _data.NewStockData().CalculateBollingerBands(stdDevMult: BollingerStdDev, length: BollingerLength)
            .OutputValues.Count;

    [BenchmarkCategory("BollingerBands"), Benchmark(Description = "Skender")]
    public int BollingerSkender() =>
        _data.Quotes.GetBollingerBands(BollingerLength, BollingerStdDev).ToList().Count;

    [BenchmarkCategory("BollingerBands"), Benchmark(Description = "TA-Lib")]
    public TALib.Core.RetCode BollingerTaLib() =>
        Functions.Bbands<double>(_data.Closes, Range.All, _output, _output2, _output3, out _,
            BollingerLength, BollingerStdDev, BollingerStdDev, TALib.Core.MAType.Sma);

    [BenchmarkCategory("BollingerBands"), Benchmark(Description = "Trady")]
    public int BollingerTrady() => _data.Candles.Bb(BollingerLength, (decimal)BollingerStdDev).Count;

    // ------------------------------------------------------------------ MACD(12, 26, 9)

    [BenchmarkCategory("Macd"), Benchmark(Baseline = true, Description = "Ooples v2 builder")]
    public int MacdOoplesBuilder()
    {
        var macd = default(Builder.MacdResult);
        using var runtime = new StockIndicatorBuilder(IndicatorDataSource.FromBatch(_data.NewStockData()))
            .ConfigureIndicators(indicators => macd = indicators.Macd(MacdFast, MacdSlow, MacdSignal))
            .Build();
        runtime.Start();
        return runtime.GetSeries(macd.Primary).AsSpan().Length
            + runtime.GetSeries(macd.Signal).AsSpan().Length
            + runtime.GetSeries(macd.Histogram).AsSpan().Length;
    }

    [BenchmarkCategory("Macd"), Benchmark(Description = "Ooples v1 batch")]
    public int MacdOoplesBatch() =>
        _data.NewStockData().CalculateMovingAverageConvergenceDivergence(
            fastLength: MacdFast, slowLength: MacdSlow, signalLength: MacdSignal).OutputValues.Count;

    [BenchmarkCategory("Macd"), Benchmark(Description = "Skender")]
    public int MacdSkender() => _data.Quotes.GetMacd(MacdFast, MacdSlow, MacdSignal).ToList().Count;

    [BenchmarkCategory("Macd"), Benchmark(Description = "TA-Lib")]
    public TALib.Core.RetCode MacdTaLib() =>
        Functions.Macd<double>(_data.Closes, Range.All, _output, _output2, _output3, out _,
            MacdFast, MacdSlow, MacdSignal);

    [BenchmarkCategory("Macd"), Benchmark(Description = "Trady")]
    public int MacdTrady() => _data.Candles.Macd(MacdFast, MacdSlow, MacdSignal).Count;

    // ------------------------------------------------------------------ Stochastic(14, 3)

    [BenchmarkCategory("Stochastic"), Benchmark(Baseline = true, Description = "Ooples v2 builder")]
    public int StochasticOoplesBuilder()
    {
        var stochastic = default(StochasticResult);
        using var runtime = new StockIndicatorBuilder(IndicatorDataSource.FromBatch(_data.NewStockData()))
            .ConfigureIndicators(indicators =>
                stochastic = indicators.Stochastic(StochasticK, StochasticD))
            .Build();
        runtime.Start();
        return runtime.GetSeries(stochastic.K).AsSpan().Length
            + runtime.GetSeries(stochastic.D).AsSpan().Length;
    }

    [BenchmarkCategory("Stochastic"), Benchmark(Description = "Ooples v1 batch")]
    public int StochasticOoplesBatch() =>
        _data.NewStockData().CalculateStochasticOscillator(
            length: StochasticK, smoothLength1: StochasticD, smoothLength2: StochasticD).OutputValues.Count;

    [BenchmarkCategory("Stochastic"), Benchmark(Description = "Skender")]
    public int StochasticSkender() =>
        _data.Quotes.GetStoch(StochasticK, StochasticD, StochasticD).ToList().Count;

    [BenchmarkCategory("Stochastic"), Benchmark(Description = "TA-Lib")]
    public TALib.Core.RetCode StochasticTaLib() =>
        Functions.Stoch<double>(_data.Highs, _data.Lows, _data.Closes, Range.All, _output, _output2, out _,
            StochasticK, StochasticD, TALib.Core.MAType.Sma, StochasticD, TALib.Core.MAType.Sma);

    // The same builder, handed the columns the caller already holds instead of a StockData it has to copy
    // them into. Every competitor receives its input pre-built in the GlobalSetup above; this is the arm that
    // measures the same courtesy, and the difference between it and "Ooples v2 builder" is the adapter.
    private IndicatorDataSource Columns() => IndicatorDataSource.FromColumns(
        _data.Opens, _data.Highs, _data.Lows, _data.Closes, _data.Volumes, _data.Dates);

    [BenchmarkCategory("Sma"), Benchmark(Description = "Ooples v2 columns")]
    public int SmaOoplesColumns()
    {
        var handle = default(SeriesHandle);
        using var runtime = new StockIndicatorBuilder(Columns())
            .ConfigureIndicators(indicators => handle = indicators.Sma(SmaLength))
            .Build();
        runtime.Start();
        return runtime.GetSeries(handle).AsSpan().Length;
    }

    [BenchmarkCategory("Ema"), Benchmark(Description = "Ooples v2 columns")]
    public int EmaOoplesColumns()
    {
        var handle = default(SeriesHandle);
        using var runtime = new StockIndicatorBuilder(Columns())
            .ConfigureIndicators(indicators => handle = indicators.Ema(EmaLength))
            .Build();
        runtime.Start();
        return runtime.GetSeries(handle).AsSpan().Length;
    }

    [BenchmarkCategory("Rsi"), Benchmark(Description = "Ooples v2 columns")]
    public int RsiOoplesColumns()
    {
        var handle = default(SeriesHandle);
        using var runtime = new StockIndicatorBuilder(Columns())
            .ConfigureIndicators(indicators => handle = indicators.Rsi(RsiLength))
            .Build();
        runtime.Start();
        return runtime.GetSeries(handle).AsSpan().Length;
    }

    [BenchmarkCategory("Atr"), Benchmark(Description = "Ooples v2 columns")]
    public int AtrOoplesColumns()
    {
        var handle = default(SeriesHandle);
        using var runtime = new StockIndicatorBuilder(Columns())
            .ConfigureIndicators(indicators => handle = indicators.Atr(AtrLength))
            .Build();
        runtime.Start();
        return runtime.GetSeries(handle).AsSpan().Length;
    }
    [BenchmarkCategory("BollingerBands"), Benchmark(Description = "Ooples v2 columns")]
    public int BollingerOoplesColumns()
    {
        var bands = default(Builder.BollingerBandsResult);
        using var runtime = new StockIndicatorBuilder(Columns())
            .ConfigureIndicators(indicators =>
                bands = indicators.BollingerBands(BollingerLength, BollingerStdDev))
            .Build();
        runtime.Start();
        return runtime.GetSeries(bands.Upper).AsSpan().Length
            + runtime.GetSeries(bands.Middle).AsSpan().Length
            + runtime.GetSeries(bands.Lower).AsSpan().Length;
    }

    [BenchmarkCategory("Macd"), Benchmark(Description = "Ooples v2 columns")]
    public int MacdOoplesColumns()
    {
        var macd = default(Builder.MacdResult);
        using var runtime = new StockIndicatorBuilder(Columns())
            .ConfigureIndicators(indicators => macd = indicators.Macd(MacdFast, MacdSlow, MacdSignal))
            .Build();
        runtime.Start();
        return runtime.GetSeries(macd.Primary).AsSpan().Length
            + runtime.GetSeries(macd.Signal).AsSpan().Length
            + runtime.GetSeries(macd.Histogram).AsSpan().Length;
    }

    [BenchmarkCategory("Stochastic"), Benchmark(Description = "Ooples v2 columns")]
    public int StochasticOoplesColumns()
    {
        var stochastic = default(StochasticResult);
        using var runtime = new StockIndicatorBuilder(Columns())
            .ConfigureIndicators(indicators =>
                stochastic = indicators.Stochastic(StochasticK, StochasticD))
            .Build();
        runtime.Start();
        return runtime.GetSeries(stochastic.K).AsSpan().Length
            + runtime.GetSeries(stochastic.D).AsSpan().Length;
    }
}
