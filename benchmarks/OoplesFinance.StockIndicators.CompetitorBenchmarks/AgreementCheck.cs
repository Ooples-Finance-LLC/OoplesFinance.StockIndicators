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
    // The row labels are one thing said in many places, and they have to line up in the printed table.
    private const string OoplesV2Row = "  OoplesV2    ";
    private const string OoplesV1Row = "  OoplesV1    ";
    private const string SkenderRow = "  Skender     ";
    private const string TaLibRow = "  TaLib       ";
    private const string TradyRow = "  Trady       ";
    private const string QuanTAlibRow = "  QuanTAlib   ";

    private const int Bars = 2_000;
    private const int Length = 20;
    private const int RsiLength = 14;
    private const int AtrLength = 14;
    private const double BollingerStdDev = 2;
    private const int MacdFast = 12;
    private const int MacdSlow = 26;
    private const int MacdSignal = 9;
    private const int StochasticLength = 14;
    private const int StochasticSmooth = 3;

    public static void Run(TextWriter output)
    {
        var data = CompetitorData.Create(Bars);

        output.WriteLine("Final value over " + Bars + " seeded bars, per library:");
        output.WriteLine();

        Report(output, "SMA(20)", Sma(data));
        Report(output, "EMA(20)", Ema(data));
        Report(output, "RSI(14)", Rsi(data));
        Report(output, "ATR(14)", Atr(data));
        Report(output, "BollingerBands(20,2) upper", BollingerUpper(data));
        Report(output, "MACD(12,26,9) line", MacdLine(data));
        Report(output, "Stochastic(14,3) %K", StochasticK(data));

        ConstantRangeControl(output);
        LinearRampControl(output);
        StochasticConventionControl(output);
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
        output.WriteLine(OoplesV2Row + runtime.GetSeries(handle).AsSpan()[^1].ToString("R"));
        output.WriteLine(SkenderRow + (quotes.GetAtr(AtrLength).Last().Atr ?? double.NaN).ToString("R"));
        output.WriteLine(TaLibRow + LastOf(taLib, taLibRange).ToString("R"));
        output.WriteLine(TradyRow + ((double)(candles.Atr(AtrLength)[^1].Tick ?? 0m)).ToString("R"));
        output.WriteLine(QuanTAlibRow + quanTAlibValue.ToString("R"));
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
            (CompetitorLibrary.Trady, (double)(data.Candles.Sma(Length)[^1].Tick ?? 0m)),
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
            (CompetitorLibrary.Trady, (double)(data.Candles.Ema(Length)[^1].Tick ?? 0m)),
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
            (CompetitorLibrary.Trady, (double)(data.Candles.Rsi(RsiLength)[^1].Tick ?? 0m))
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
            (CompetitorLibrary.Trady, (double)(data.Candles.Atr(AtrLength)[^1].Tick ?? 0m)),
            (CompetitorLibrary.QuanTAlib, quanTAlibValue)
        ];
    }

    private static List<(CompetitorLibrary, double)> BollingerUpper(CompetitorData data)
    {
        var bands = default(Builder.BollingerBandsResult);
        using var runtime = new StockIndicatorBuilder(IndicatorDataSource.FromBatch(data.NewStockData()))
            .ConfigureIndicators(indicators => bands = indicators.BollingerBands(Length, BollingerStdDev))
            .Build();
        runtime.Start();

        var upper = new double[data.Count];
        var middle = new double[data.Count];
        var lower = new double[data.Count];
        Functions.Bbands<double>(data.Closes, Range.All, upper, middle, lower, out var taLibRange,
            Length, BollingerStdDev, BollingerStdDev, TALib.Core.MAType.Sma);

        return
        [
            (CompetitorLibrary.OoplesV2, runtime.GetSeries(bands.Upper).AsSpan()[^1]),
            (CompetitorLibrary.OoplesV1, data.NewStockData()
                .CalculateBollingerBands(stdDevMult: BollingerStdDev, length: Length)
                .OutputValues["UpperBand"][^1]),
            (CompetitorLibrary.Skender,
                data.Quotes.GetBollingerBands(Length, BollingerStdDev).Last().UpperBand ?? double.NaN),
            (CompetitorLibrary.TaLib, LastOf(upper, taLibRange)),
            (CompetitorLibrary.Trady,
                (double)(data.Candles.Bb(Length, (decimal)BollingerStdDev)[^1].Tick.UpperBand ?? 0m))
        ];
    }

    private static List<(CompetitorLibrary, double)> MacdLine(CompetitorData data)
    {
        var macd = default(Builder.MacdResult);
        using var runtime = new StockIndicatorBuilder(IndicatorDataSource.FromBatch(data.NewStockData()))
            .ConfigureIndicators(indicators => macd = indicators.Macd(MacdFast, MacdSlow, MacdSignal))
            .Build();
        runtime.Start();

        var line = new double[data.Count];
        var signal = new double[data.Count];
        var histogram = new double[data.Count];
        Functions.Macd<double>(data.Closes, Range.All, line, signal, histogram, out var taLibRange,
            MacdFast, MacdSlow, MacdSignal);

        return
        [
            (CompetitorLibrary.OoplesV2, runtime.GetSeries(macd.Primary).AsSpan()[^1]),
            (CompetitorLibrary.OoplesV1, data.NewStockData().CalculateMovingAverageConvergenceDivergence(
                fastLength: MacdFast, slowLength: MacdSlow, signalLength: MacdSignal).OutputValues["Macd"][^1]),
            (CompetitorLibrary.Skender,
                data.Quotes.GetMacd(MacdFast, MacdSlow, MacdSignal).Last().Macd ?? double.NaN),
            (CompetitorLibrary.TaLib, LastOf(line, taLibRange)),
            (CompetitorLibrary.Trady,
                (double)(data.Candles.Macd(MacdFast, MacdSlow, MacdSignal)[^1].Tick.MacdLine ?? 0m))
        ];
    }

    private static List<(CompetitorLibrary, double)> StochasticK(CompetitorData data)
    {
        var stochastic = default(StochasticResult);
        using var runtime = new StockIndicatorBuilder(IndicatorDataSource.FromBatch(data.NewStockData()))
            .ConfigureIndicators(indicators => stochastic = indicators.Stochastic(StochasticLength, StochasticSmooth))
            .Build();
        runtime.Start();

        var slowK = new double[data.Count];
        var slowD = new double[data.Count];
        Functions.Stoch<double>(data.Highs, data.Lows, data.Closes, Range.All, slowK, slowD, out var taLibRange,
            StochasticLength, StochasticSmooth, TALib.Core.MAType.Sma, StochasticSmooth, TALib.Core.MAType.Sma);

        return
        [
            (CompetitorLibrary.OoplesV2, runtime.GetSeries(stochastic.K).AsSpan()[^1]),
            (CompetitorLibrary.OoplesV1, data.NewStockData().CalculateStochasticOscillator(
                    length: StochasticLength, smoothLength1: StochasticSmooth, smoothLength2: StochasticSmooth)
                .OutputValues["FastK"][^1]),
            (CompetitorLibrary.Skender,
                data.Quotes.GetStoch(StochasticLength, StochasticSmooth, StochasticSmooth).Last().Oscillator
                ?? double.NaN),
            (CompetitorLibrary.TaLib, LastOf(slowK, taLibRange))
        ];
    }

    /// <summary>
    /// A second control, on a series whose answers are fixed by algebra rather than by convention, covering the
    /// three indicators the random-walk comparison above cannot adjudicate on its own.
    ///
    /// <para>The series is a unit-slope ramp with every bar's high, low and close equal, and it pins all three:</para>
    /// <list type="bullet">
    /// <item>An EMA of period n over x[i] = i settles at i - (n-1)/2, so MACD(12,26,9) settles at
    /// (26-1)/2 - (12-1)/2 = 7 exactly, its signal line at 7, and its histogram at 0, whatever the library seeds
    /// its first EMA from.</item>
    /// <item>On a strictly rising ramp the close is the window's high, so raw %K is 100 on every bar and any
    /// smoothing of a constant 100 is still 100.</item>
    /// <item>A 20-bar window of consecutive integers has population sigma sqrt((20^2-1)/12) and sample sigma
    /// sqrt(20(20^2-1)/(12*19)) = sqrt(35), so the distance from the middle band to the upper band is
    /// 11.5325... under one convention and 11.8321... under the other. That is a convention, not a defect -
    /// which is exactly why it is worth printing the two targets rather than one.</item>
    /// </list>
    /// </summary>
    private static void LinearRampControl(TextWriter output)
    {
        const int bars = 600;

        var opens = new double[bars];
        var highs = new double[bars];
        var lows = new double[bars];
        var closes = new double[bars];
        var volumes = new double[bars];
        var dates = new DateTime[bars];
        var start = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        for (var i = 0; i < bars; i++)
        {
            opens[i] = highs[i] = lows[i] = closes[i] = 100 + i;
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

        // Every v1 Calculate* method mutates the StockData it is handed, so each arm gets its own.
        Models.StockData NewStockData() => new([.. opens], [.. highs], [.. lows], [.. closes], [.. volumes],
            [.. dates]);

        var macd = default(Builder.MacdResult);
        using (var runtime = new StockIndicatorBuilder(IndicatorDataSource.FromBatch(NewStockData()))
            .ConfigureIndicators(indicators => macd = indicators.Macd(MacdFast, MacdSlow, MacdSignal))
            .Build())
        {
            runtime.Start();

            var line = new double[bars];
            var signalLine = new double[bars];
            var histogram = new double[bars];
            Functions.Macd<double>(closes, Range.All, line, signalLine, histogram, out var taLibRange,
                MacdFast, MacdSlow, MacdSignal);

            output.WriteLine("Control: MACD(12,26,9) on a unit-slope ramp, so the answer is "
                + (((MacdSlow - 1) / 2.0) - ((MacdFast - 1) / 2.0)).ToString("R"));
            output.WriteLine(OoplesV2Row + runtime.GetSeries(macd.Primary).AsSpan()[^1].ToString("R"));
            output.WriteLine(OoplesV1Row + NewStockData().CalculateMovingAverageConvergenceDivergence(
                fastLength: MacdFast, slowLength: MacdSlow, signalLength: MacdSignal)
                .OutputValues["Macd"][^1].ToString("R"));
            output.WriteLine(SkenderRow
                + (quotes.GetMacd(MacdFast, MacdSlow, MacdSignal).Last().Macd ?? double.NaN).ToString("R"));
            output.WriteLine(TaLibRow + LastOf(line, taLibRange).ToString("R"));
            output.WriteLine(TradyRow
                + ((double)(candles.Macd(MacdFast, MacdSlow, MacdSignal)[^1].Tick.MacdLine ?? 0m)).ToString("R"));
            output.WriteLine();
        }

        var stochastic = default(StochasticResult);
        using (var runtime = new StockIndicatorBuilder(IndicatorDataSource.FromBatch(NewStockData()))
            .ConfigureIndicators(indicators =>
                stochastic = indicators.Stochastic(StochasticLength, StochasticSmooth))
            .Build())
        {
            runtime.Start();

            var slowK = new double[bars];
            var slowD = new double[bars];
            Functions.Stoch<double>(highs, lows, closes, Range.All, slowK, slowD, out var taLibRange,
                StochasticLength, StochasticSmooth, TALib.Core.MAType.Sma, StochasticSmooth,
                TALib.Core.MAType.Sma);

            output.WriteLine("Control: Stochastic %K on a strictly rising ramp, so the answer is 100");
            output.WriteLine(OoplesV2Row + runtime.GetSeries(stochastic.K).AsSpan()[^1].ToString("R"));
            output.WriteLine(OoplesV1Row + NewStockData().CalculateStochasticOscillator(
                    length: StochasticLength, smoothLength1: StochasticSmooth, smoothLength2: StochasticSmooth)
                .OutputValues["FastK"][^1].ToString("R"));
            output.WriteLine(SkenderRow
                + (quotes.GetStoch(StochasticLength, StochasticSmooth, StochasticSmooth).Last().Oscillator
                    ?? double.NaN).ToString("R"));
            output.WriteLine(TaLibRow + LastOf(slowK, taLibRange).ToString("R"));
            output.WriteLine();
        }

        var bands = default(Builder.BollingerBandsResult);
        using (var runtime = new StockIndicatorBuilder(IndicatorDataSource.FromBatch(NewStockData()))
            .ConfigureIndicators(indicators => bands = indicators.BollingerBands(Length, BollingerStdDev))
            .Build())
        {
            runtime.Start();

            var upper = new double[bars];
            var middle = new double[bars];
            var lower = new double[bars];
            Functions.Bbands<double>(closes, Range.All, upper, middle, lower, out var taLibRange,
                Length, BollingerStdDev, BollingerStdDev, TALib.Core.MAType.Sma);

            var population = BollingerStdDev * Math.Sqrt(((Length * Length) - 1) / 12.0);
            var sample = BollingerStdDev * Math.Sqrt(Length * ((Length * Length) - 1) / (12.0 * (Length - 1)));
            var v1 = NewStockData().CalculateBollingerBands(stdDevMult: BollingerStdDev, length: Length);

            output.WriteLine("Control: BollingerBands(20,2) upper minus middle on a unit-slope ramp, so the "
                + "answer is " + population.ToString("R") + " with a population sigma or " + sample.ToString("R")
                + " with a sample sigma");
            output.WriteLine(OoplesV2Row + (runtime.GetSeries(bands.Upper).AsSpan()[^1]
                - runtime.GetSeries(bands.Middle).AsSpan()[^1]).ToString("R"));
            output.WriteLine(OoplesV1Row
                + (v1.OutputValues["UpperBand"][^1] - v1.OutputValues["MiddleBand"][^1]).ToString("R"));
            var skender = quotes.GetBollingerBands(Length, BollingerStdDev).Last();
            output.WriteLine(SkenderRow
                + ((skender.UpperBand ?? double.NaN) - (skender.Sma ?? double.NaN)).ToString("R"));
            output.WriteLine(TaLibRow
                + (LastOf(upper, taLibRange) - LastOf(middle, taLibRange)).ToString("R"));
            var trady = candles.Bb(Length, (decimal)BollingerStdDev)[^1].Tick;
            output.WriteLine(TradyRow
                + ((double)((trady.UpperBand ?? 0m) - (trady.MiddleBand ?? 0m))).ToString("R"));
            output.WriteLine();
        }
    }


    /// <summary>
    /// A control that separates the two Stochastic conventions, which the ramp above cannot: on a strictly
    /// rising series every raw %K is 100, so smoothing it leaves 100 and both conventions agree.
    ///
    /// <para>Here every bar has the same high and low, so the 14-bar window's range is fixed at 100..110 and
    /// raw %K is exactly ten times the close's distance above 100. The closes hold 105 and then step 110, 105,
    /// 100, making the last three raw %K values 100, 50 and 0. A library reporting the raw fast %K must
    /// therefore end at 0, and one reporting a %K smoothed over three bars must end at (100 + 50 + 0) / 3 = 50.
    /// No seeding or averaging choice produces any other number, so this says which convention each library
    /// follows rather than merely that they differ.</para>
    /// </summary>
    private static void StochasticConventionControl(TextWriter output)
    {
        const int bars = 600;
        const double high = 110;
        const double low = 100;

        var opens = new double[bars];
        var highs = new double[bars];
        var lows = new double[bars];
        var closes = new double[bars];
        var volumes = new double[bars];
        var dates = new DateTime[bars];
        var start = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        for (var i = 0; i < bars; i++)
        {
            highs[i] = high;
            lows[i] = low;
            closes[i] = 105;
            volumes[i] = 1;
            dates[i] = start.AddMinutes(i);
        }

        closes[bars - 3] = 110;
        closes[bars - 2] = 105;
        closes[bars - 1] = 100;
        for (var i = 0; i < bars; i++)
        {
            opens[i] = closes[i];
        }

        var quotes = new List<Skender.Stock.Indicators.Quote>(bars);
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
        }

        Models.StockData NewStockData() => new([.. opens], [.. highs], [.. lows], [.. closes], [.. volumes],
            [.. dates]);

        var stochastic = default(StochasticResult);
        using var runtime = new StockIndicatorBuilder(IndicatorDataSource.FromBatch(NewStockData()))
            .ConfigureIndicators(indicators =>
                stochastic = indicators.Stochastic(StochasticLength, StochasticSmooth))
            .Build();
        runtime.Start();

        var slowK = new double[bars];
        var slowD = new double[bars];
        Functions.Stoch<double>(highs, lows, closes, Range.All, slowK, slowD, out var taLibRange,
            StochasticLength, StochasticSmooth, TALib.Core.MAType.Sma, StochasticSmooth, TALib.Core.MAType.Sma);

        output.WriteLine("Control: Stochastic %K where the last three raw %K values are 100, 50 and 0, so the "
            + "answer is 0 for a raw fast %K or 50 for a %K smoothed over 3");
        output.WriteLine(OoplesV2Row + runtime.GetSeries(stochastic.K).AsSpan()[^1].ToString("R"));
        output.WriteLine(OoplesV1Row + NewStockData().CalculateStochasticOscillator(
                length: StochasticLength, smoothLength1: StochasticSmooth, smoothLength2: StochasticSmooth)
            .OutputValues["FastK"][^1].ToString("R"));
        output.WriteLine(SkenderRow
            + (quotes.GetStoch(StochasticLength, StochasticSmooth, StochasticSmooth).Last().Oscillator
                ?? double.NaN).ToString("R"));
        output.WriteLine(TaLibRow + LastOf(slowK, taLibRange).ToString("R"));
        output.WriteLine();
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
