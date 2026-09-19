using OoplesFinance.StockIndicators.Builder;
using Skender.Stock.Indicators;
using TALib;
using Trady.Analysis.Extension;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

/// <summary>
/// Cross-checks what the libraries compute before anyone compares how fast they computed it.
///
/// <para>A speed table is worthless if the arms are not doing the same arithmetic, and the usual way that goes
/// wrong is silent: one library seeds an EMA from a simple average and another from the first bar, one smooths
/// an RSI with Wilder's method and another with a plain mean, and the faster one is simply doing less. Running
/// this alongside the timings turns that from an unstated assumption into a printed number.</para>
///
/// <para>It reports rather than asserts. Real divergences here are usually legitimate convention differences,
/// not defects, and the right response is to say so next to the timing - which is why the benchmark README
/// carries this output rather than a claim that everyone agrees.</para>
/// </summary>
internal static class AgreementCheck
{
    private const int Bars = 2_000;
    private const int Length = 20;
    private const int RsiLength = 14;
    private const int AtrLength = 14;

    public static void Run(TextWriter output)
    {
        var data = CompetitorData.Create(Bars);

        output.WriteLine("Final value over " + Bars + " seeded bars, per library:");
        output.WriteLine();

        Report(output, "SMA(20)", Sma(data));
        Report(output, "EMA(20)", Ema(data));
        Report(output, "RSI(14)", Rsi(data));
        Report(output, "ATR(14)", Atr(data));

        ConstantRangeControl(output);
    }

    /// <summary>
    /// A control the answer is known for without running anything: every bar opens, closes and gaps the same,
    /// with a high two above its low, so every true range is exactly 2 and ATR must be 2 at any period and
    /// under any smoothing convention.
    ///
    /// <para>This is here because the random-walk comparison above cannot tell a convention difference from a
    /// defect - both just show up as "these numbers differ". A series whose correct answer is fixed by
    /// arithmetic can: a library that returns something other than 2 is wrong, and no seeding or smoothing
    /// choice rescues it.</para>
    /// </summary>
    private static void ConstantRangeControl(TextWriter output)
    {
        const double trueRange = 2;
        const int bars = 500;

        var opens = new double[bars];
        var highs = new double[bars];
        var lows = new double[bars];
        var closes = new double[bars];
        var volumes = new double[bars];
        var dates = new DateTime[bars];
        var start = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        for (var i = 0; i < bars; i++)
        {
            opens[i] = 100;
            closes[i] = 100;
            highs[i] = 100 + (trueRange / 2);
            lows[i] = 100 - (trueRange / 2);
            volumes[i] = 1;
            dates[i] = start.AddMinutes(i);
        }

        var quotes = new List<Skender.Stock.Indicators.Quote>(bars);
        var candles = new List<Trady.Core.Candle>(bars);
        for (var i = 0; i < bars; i++)
        {
            quotes.Add(new Skender.Stock.Indicators.Quote
            {
                Date = dates[i],
                Open = (decimal)opens[i],
                High = (decimal)highs[i],
                Low = (decimal)lows[i],
                Close = (decimal)closes[i],
                Volume = (decimal)volumes[i]
            });
            candles.Add(new Trady.Core.Candle(dates[i], (decimal)opens[i], (decimal)highs[i], (decimal)lows[i],
                (decimal)closes[i], (decimal)volumes[i]));
        }

        var handle = default(SeriesHandle);
        using var runtime = new StockIndicatorBuilder(IndicatorDataSource.FromBatch(new Models.StockData(
                [.. opens], [.. highs], [.. lows], [.. closes], [.. volumes], [.. dates])))
            .ConfigureIndicators(indicators => handle = indicators.Atr(AtrLength))
            .Build();
        runtime.Start();

        var taLib = new double[bars];
        Functions.Atr<double>(highs, lows, closes, Range.All, taLib, out var taLibRange, AtrLength);

        var quanTAlib = new QuanTAlib.Atr(AtrLength);
        var quanTAlibValue = 0d;
        for (var i = 0; i < bars; i++)
        {
            quanTAlibValue = quanTAlib.Calc(
                new QuanTAlib.TBar(dates[i], opens[i], highs[i], lows[i], closes[i], volumes[i], true)).Value;
        }

        output.WriteLine("Control: ATR(14) where every true range is exactly " + trueRange
            + ", so the answer is " + trueRange);
        output.WriteLine("  OoplesV2    " + runtime.GetSeries(handle).AsSpan()[^1].ToString("R"));
        output.WriteLine("  Skender     " + (quotes.GetAtr(AtrLength).Last().Atr ?? double.NaN).ToString("R"));
        output.WriteLine("  TaLib       " + LastOf(taLib, taLibRange).ToString("R"));
        output.WriteLine("  Trady       " + ((double)(candles.Atr(AtrLength).Last().Tick ?? 0m)).ToString("R"));
        output.WriteLine("  QuanTAlib   " + quanTAlibValue.ToString("R"));
        output.WriteLine();
    }

    private static void Report(TextWriter output, string label, List<(CompetitorLibrary Library, double Value)> values)
    {
        output.WriteLine(label);
        foreach (var (library, value) in values)
        {
            output.WriteLine("  " + library.ToString().PadRight(12) + value.ToString("R"));
        }

        var spread = values.Max(v => v.Value) - values.Min(v => v.Value);
        var scale = Math.Max(1e-12, Math.Abs(values[0].Value));
        output.WriteLine("  spread      " + spread.ToString("R") + "  (" + (spread / scale).ToString("P4") + " of "
            + values[0].Library + ")");
        output.WriteLine();
    }

    private static List<(CompetitorLibrary, double)> Sma(CompetitorData data)
    {
        var handle = default(SeriesHandle);
        using var runtime = new StockIndicatorBuilder(IndicatorDataSource.FromBatch(data.NewStockData()))
            .ConfigureIndicators(indicators => handle = indicators.Sma(Length))
            .Build();
        runtime.Start();

        var taLib = new double[data.Count];
        Functions.Sma<double>(data.Closes, Range.All, taLib, out var taLibRange, Length);

        var quanTAlib = new QuanTAlib.Sma(Length);
        var quanTAlibValue = 0d;
        foreach (var close in data.Closes)
        {
            quanTAlibValue = quanTAlib.Calc(new QuanTAlib.TValue(close, true, false)).Value;
        }

        return
        [
            (CompetitorLibrary.OoplesV2, runtime.GetSeries(handle).AsSpan()[^1]),
            (CompetitorLibrary.OoplesV1,
                data.NewStockData().CalculateSimpleMovingAverage(Length).CustomValuesList[^1]),
            (CompetitorLibrary.Skender, data.Quotes.GetSma(Length).Last().Sma ?? double.NaN),
            (CompetitorLibrary.TaLib, LastOf(taLib, taLibRange)),
            (CompetitorLibrary.Trady, (double)(data.Candles.Sma(Length).Last().Tick ?? 0m)),
            (CompetitorLibrary.QuanTAlib, quanTAlibValue)
        ];
    }

    private static List<(CompetitorLibrary, double)> Ema(CompetitorData data)
    {
        var handle = default(SeriesHandle);
        using var runtime = new StockIndicatorBuilder(IndicatorDataSource.FromBatch(data.NewStockData()))
            .ConfigureIndicators(indicators => handle = indicators.Ema(Length))
            .Build();
        runtime.Start();

        var taLib = new double[data.Count];
        Functions.Ema<double>(data.Closes, Range.All, taLib, out var taLibRange, Length);

        var quanTAlib = new QuanTAlib.Ema(Length, true);
        var quanTAlibValue = 0d;
        foreach (var close in data.Closes)
        {
            quanTAlibValue = quanTAlib.Calc(new QuanTAlib.TValue(close, true, false)).Value;
        }

        return
        [
            (CompetitorLibrary.OoplesV2, runtime.GetSeries(handle).AsSpan()[^1]),
            (CompetitorLibrary.OoplesV1,
                data.NewStockData().CalculateExponentialMovingAverage(Length).CustomValuesList[^1]),
            (CompetitorLibrary.Skender, data.Quotes.GetEma(Length).Last().Ema ?? double.NaN),
            (CompetitorLibrary.TaLib, LastOf(taLib, taLibRange)),
            (CompetitorLibrary.Trady, (double)(data.Candles.Ema(Length).Last().Tick ?? 0m)),
            (CompetitorLibrary.QuanTAlib, quanTAlibValue)
        ];
    }

    private static List<(CompetitorLibrary, double)> Rsi(CompetitorData data)
    {
        var handle = default(SeriesHandle);
        using var runtime = new StockIndicatorBuilder(IndicatorDataSource.FromBatch(data.NewStockData()))
            .ConfigureIndicators(indicators => handle = indicators.Rsi(RsiLength))
            .Build();
        runtime.Start();

        var taLib = new double[data.Count];
        Functions.Rsi<double>(data.Closes, Range.All, taLib, out var taLibRange, RsiLength);

        return
        [
            (CompetitorLibrary.OoplesV2, runtime.GetSeries(handle).AsSpan()[^1]),
            (CompetitorLibrary.OoplesV1,
                data.NewStockData().CalculateRelativeStrengthIndex(length: RsiLength).CustomValuesList[^1]),
            (CompetitorLibrary.Skender, data.Quotes.GetRsi(RsiLength).Last().Rsi ?? double.NaN),
            (CompetitorLibrary.TaLib, LastOf(taLib, taLibRange)),
            (CompetitorLibrary.Trady, (double)(data.Candles.Rsi(RsiLength).Last().Tick ?? 0m))
        ];
    }

    private static List<(CompetitorLibrary, double)> Atr(CompetitorData data)
    {
        var handle = default(SeriesHandle);
        using var runtime = new StockIndicatorBuilder(IndicatorDataSource.FromBatch(data.NewStockData()))
            .ConfigureIndicators(indicators => handle = indicators.Atr(AtrLength))
            .Build();
        runtime.Start();

        var taLib = new double[data.Count];
        Functions.Atr<double>(data.Highs, data.Lows, data.Closes, Range.All, taLib, out var taLibRange,
            AtrLength);

        var quanTAlib = new QuanTAlib.Atr(AtrLength);
        var quanTAlibValue = 0d;
        foreach (var bar in data.Bars)
        {
            quanTAlibValue = quanTAlib.Calc(bar).Value;
        }

        return
        [
            (CompetitorLibrary.OoplesV2, runtime.GetSeries(handle).AsSpan()[^1]),
            (CompetitorLibrary.OoplesV1,
                data.NewStockData().CalculateAverageTrueRange(length: AtrLength).CustomValuesList[^1]),
            (CompetitorLibrary.Skender, data.Quotes.GetAtr(AtrLength).Last().Atr ?? double.NaN),
            (CompetitorLibrary.TaLib, LastOf(taLib, taLibRange)),
            (CompetitorLibrary.Trady, (double)(data.Candles.Atr(AtrLength).Last().Tick ?? 0m)),
            (CompetitorLibrary.QuanTAlib, quanTAlibValue)
        ];
    }

    /// <summary>
    /// TA-Lib writes its results from index 0 of the output span and reports, through <paramref name="range"/>,
    /// which input bars they correspond to. Reading the last element of the buffer instead of the last element
    /// of that range returns an untouched zero for every indicator with a warm-up period - which is exactly
    /// what this check caught on its first run.
    ///
    /// <para>The reported range is half-open: an SMA(20) over 100 bars reports <c>19..100</c> and fills
    /// <c>output[0..80]</c>, so the count is the plain difference and the last value is one below it. Treating
    /// the end as inclusive reads one past the data and yields 0.</para>
    /// </summary>
    private static double LastOf(double[] output, Range range)
    {
        var count = range.End.Value - range.Start.Value;
        return count > 0 && count <= output.Length ? output[count - 1] : double.NaN;
    }
}
