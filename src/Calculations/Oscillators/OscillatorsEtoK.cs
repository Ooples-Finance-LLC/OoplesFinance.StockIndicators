using OoplesFinance.StockIndicators.Compatibility;
using OoplesFinance.StockIndicators.Core;

namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Ichimoku Chikou Span.
    /// </summary>
    /// <remarks>
    /// The lagging span of the Ichimoku cloud: the close itself, which a chart then draws shifted back behind
    /// the price so that the eye compares today's close with the price of some bars ago. The shift belongs to
    /// the drawing rather than the series, as it does for the cloud's own spans, which
    /// <see cref="CalculateIchimokuCloud"/> likewise publishes at the bar that computes them. So every bar
    /// here carries its own close and none is left empty.
    /// </remarks>
    /// <param name="stockData"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateIchimokuChikouSpan(this StockData stockData)
    {
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var count = inputList.Count;
        List<double> chikouSpanList = new(count);
        List<Signal>? signalsList = CreateSignalsList(stockData, count);

        for (var i = 0; i < count; i++)
        {
            var chikouSpan = inputList[i];
            chikouSpanList.Add(chikouSpan);

            var prevChikou1 = i >= 1 ? chikouSpanList[i - 1] : 0;
            var prevChikou2 = i >= 2 ? chikouSpanList[i - 2] : 0;
            var signal = GetCompareSignal(chikouSpan - prevChikou1, prevChikou1 - prevChikou2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "ChikouSpan", chikouSpanList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(chikouSpanList);
        stockData.IndicatorName = IndicatorName.IchimokuChikouSpan;

        return stockData;
    }

    /// <summary>
    /// Calculates the Japanese Correlation Coefficient
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateJapaneseCorrelationCoefficient(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 50)
    {
        List<double> joList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        var length1 = MinOrMax((int)Math.Ceiling((double)length / 2));

        var hList = GetMovingAverageList(stockData, maType, length1, highList);
        var lList = GetMovingAverageList(stockData, maType, length1, lowList);
        var cList = GetMovingAverageList(stockData, maType, length1, inputList);
        var highestList = GetMaxAndMinValuesList(hList, length1).Item1;
        var lowestList = GetMaxAndMinValuesList(lList, length1).Item2;

        for (var i = 0; i < stockData.Count; i++)
        {
            var c = cList[i];
            var prevC = i >= length ? cList[i - length] : 0;
            var highest = highestList[i];
            var lowest = lowestList[i];
            var prevJo1 = i >= 1 ? joList[i - 1] : 0;
            var prevJo2 = i >= 2 ? joList[i - 2] : 0;
            var jo = ExactDifferenceRatio.Of(c, prevC, highest, lowest);
            joList.Add(jo);

            var signal = GetCompareSignal(jo - prevJo1, prevJo1 - prevJo2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Jo", joList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(joList);
        stockData.IndicatorName = IndicatorName.JapaneseCorrelationCoefficient;

        return stockData;
    }


    /// <summary>
    /// Calculates the Jma Rsx Clone
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateJmaRsxClone(this StockData stockData, int length = 14)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); var window = new RsxWindow(length);
        var values = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        foreach (var price in input) { var point = window.Next(price, true); values.Add(point.Value); signals?.Add(point.Trade); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Rsx", values } });
        stockData.SetSignals(signals); stockData.SetCustomValues(values);
        stockData.IndicatorName = IndicatorName.JmaRsxClone; return stockData;
    }


    /// <summary>
    /// Calculates the Jrc Fractal Dimension
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateJrcFractalDimension(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length1 = 20, int length2 = 5, int smoothLength = 5)
    {
        var (input, high, low, _, _) = GetInputValuesList(stockData);
        var values = new List<double>(input.Count); var signals = new List<double>(input.Count); var trades = CreateSignalsList(stockData);
        var external = !StrengthWindow.Supports(maType);
        var components = external ? JrcWindow.Components(stockData, input, high, low, maType, length1, length2, smoothLength, false, true) : default;
        using var window = new JrcWindow(maType, length1, length2, smoothLength, external);
        for (var i = 0; i < input.Count; i++)
        {
            var point = window.Next(high[i], low[i], input[i], true, external ? components.Line[i] : null, external ? components.Signal[i] : null);
            values.Add(point.Line); signals.Add(point.SignalLine); trades?.Add(point.Trade);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Jrcfd", values }, { "Signal", signals } });
        stockData.SetSignals(trades); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.JrcFractalDimension; return stockData;
    }


    /// <summary>
    /// Calculates the Inertia Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="rviLength">The smoothing length of the Relative Volatility Index that Dorsey's Inertia regresses.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateInertiaIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.LinearRegression,
        int length = 20, int rviLength = 14)
    {
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var rviList = CalculateRelativeVolatilityIndexV2(stockData, smoothLength: Math.Max(1, rviLength)).ChainedValues;
        var inertiaList = InertiaSmoother.Supports(maType) ? InertiaSmoother.Compute(rviList, maType, length).ToList() : GetMovingAverageList(stockData, maType, length, rviList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var inertiaIndicator = inertiaList[i];
            var prevInertiaIndicator1 = i >= 1 ? inertiaList[i - 1] : 0;
            var prevInertiaIndicator2 = i >= 2 ? inertiaList[i - 2] : 0;

            var signal = InertiaSmoother.Trade(inertiaIndicator, prevInertiaIndicator1, prevInertiaIndicator2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Inertia", inertiaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(inertiaList);
        stockData.IndicatorName = IndicatorName.InertiaIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Internal Bar Strength Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateInternalBarStrengthIndicator(this StockData stockData, int length = 14, int smoothLength = 3)
    {
        var close=stockData.ChainedValues.Count>0?stockData.ChainedValues:stockData.InputValues;
        using var window=new InternalBarStrengthWindow(length,smoothLength);List<double> values=new(stockData.Count),signalValues=new(stockData.Count);var signals=CreateSignalsList(stockData);
        for(var i=0;i<stockData.Count;i++)
        {
            var result=window.Next(stockData.HighPrices[i],stockData.LowPrices[i],close[i],true);var previous=i>0?values[i-1]:0;var previousSignal=i>0?signalValues[i-1]:0;
            signals?.Add(GetRsiSignal(result.Value-result.Signal,previous-previousSignal,result.Value,previous,70,30));values.Add(result.Value);signalValues.Add(result.Signal);
        }
        stockData.SetOutputValues(()=>new Dictionary<string,List<double>>{{"Ibs",values},{"Signal",signalValues}});stockData.SetSignals(signals);stockData.SetCustomValues(values);stockData.IndicatorName=IndicatorName.InternalBarStrengthIndicator;return stockData;
    }


    /// <summary>
    /// Calculates the Inverse Fisher Fast Z Score
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateInverseFisherFastZScore(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 50)
    {
        length = Math.Max(1, length);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var exact = StrengthWindow.Supports(maType);
        var means = exact ? StrengthWindow.Smooth(inputList, maType, length) : GetMovingAverageList(stockData, maType, length, inputList);
        using var state = new StandardizedScoreWindow(length, true);
        var values = new List<double>(stockData.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < stockData.Count; i++)
        {
            var score = state.Next(inputList[i], means[i], exact && maType == MovingAvgType.SimpleMovingAverage, true);
            score = StandardizedScoreWindow.Inverse(score, true);
            var previous = i > 0 ? values[i - 1] : 0;
            var older = i > 1 ? values[i - 2] : 0;
            signals?.Add(GetCompareSignal(score - previous, previous - older)); values.Add(score);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Iffzs", values } });
        stockData.SetSignals(signals); stockData.SetCustomValues(values);
        stockData.IndicatorName = IndicatorName.InverseFisherFastZScore; return stockData;
    }


    /// <summary>
    /// Calculates the Inverse Fisher Z Score
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateInverseFisherZScore(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 100)
    {
        length = Math.Max(1, length);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var exact = StrengthWindow.Supports(maType);
        var means = exact ? StrengthWindow.Smooth(inputList, maType, length) : GetMovingAverageList(stockData, maType, length, inputList);
        using var state = new StandardizedScoreWindow(length, false);
        var values = new List<double>(stockData.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < stockData.Count; i++)
        {
            var score = state.Next(inputList[i], means[i], exact && maType == MovingAvgType.SimpleMovingAverage, true);
            score = StandardizedScoreWindow.Inverse(score, false);
            var previous = i > 0 ? values[i - 1] : 0;
            var older = i > 1 ? values[i - 2] : 0;
            signals?.Add(GetRsiSignal(score - previous, previous - older, score, previous, 80, 20)); values.Add(score);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Ifzs", values } });
        stockData.SetSignals(signals); stockData.SetCustomValues(values);
        stockData.IndicatorName = IndicatorName.InverseFisherZScore; return stockData;
    }


    /// <summary>
    /// Calculates the Insync Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="signalLength"></param>
    /// <param name="emoLength"></param>
    /// <param name="mfiLength"></param>
    /// <param name="bbLength"></param>
    /// <param name="cciLength"></param>
    /// <param name="dpoLength"></param>
    /// <param name="rocLength"></param>
    /// <param name="rsiLength"></param>
    /// <param name="stochLength"></param>
    /// <param name="stochKLength"></param>
    /// <param name="stochDLength"></param>
    /// <param name="smaLength"></param>
    /// <param name="stdDevMult"></param>
    /// <param name="divisor"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateInsyncIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int fastLength = 12, int slowLength = 26, int signalLength = 9, int emoLength = 14, int mfiLength = 20, int bbLength = 20,
        int cciLength = 14, int dpoLength = 18, int rocLength = 10, int rsiLength = 14, int stochLength = 14, int stochKLength = 1,
        int stochDLength = 3, int smaLength = 10, double stdDevMult = 2, double divisor = 10000)
    {
        // maType, signalLength and emoLength only described unused component signal lines.
        using var window = new InsyncWindow(fastLength, slowLength, mfiLength, bbLength, cciLength, dpoLength, rocLength, rsiLength, stochLength, stochKLength, stochDLength, smaLength, stdDevMult, divisor);
        var values = new double[stockData.Count]; window.Compute(stockData, values);
        var line = values.ToList(); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < values.Length; i++)
        {
            var previous = i > 0 ? values[i - 1] : 0;
            var before = i > 1 ? values[i - 2] : 0;
            signals?.Add(GetRsiSignal(values[i] - previous, previous - before, values[i], previous, 95, 5));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Iidx", line } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line);
        stockData.IndicatorName = IndicatorName.InsyncIndex; return stockData;
    }


    /// <summary>
    /// Calculates the Gann Swing Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateGannSwingOscillator(this StockData stockData, int length = 5)
    {
        List<double> gannSwingOscillatorList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (_, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var highestHigh = highestList[i];
            var lowestLow = lowestList[i];
            var prevHighest1 = i >= 1 ? highestList[i - 1] : 0;
            var prevLowest1 = i >= 1 ? lowestList[i - 1] : 0;
            var prevHighest2 = i >= 2 ? highestList[i - 2] : 0;
            var prevLowest2 = i >= 2 ? lowestList[i - 2] : 0;

            var prevGso = GetLastOrDefault(gannSwingOscillatorList);
            var gso = prevHighest2 > prevHighest1 && highestHigh > prevHighest1 ? 1 :
                prevLowest2 < prevLowest1 && lowestLow < prevLowest1 ? -1 : prevGso;
            gannSwingOscillatorList.Add(gso);

            var signal = GetCompareSignal(gso, prevGso);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Gso", gannSwingOscillatorList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(gannSwingOscillatorList);
        stockData.IndicatorName = IndicatorName.GannSwingOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Gann HiLo Activator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateGannHiLoActivator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 3)
    {
        List<double> ghlaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        var highMaList = GetMovingAverageList(stockData, maType, length, highList);
        var lowMaList = GetMovingAverageList(stockData, maType, length, lowList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var highMa = highMaList[i];
            var lowMa = lowMaList[i];
            var prevHighMa = i >= 1 ? highMaList[i - 1] : 0;
            var prevLowMa = i >= 1 ? lowMaList[i - 1] : 0;
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevGhla = GetLastOrDefault(ghlaList);
            var ghla = currentValue > prevHighMa ? lowMa : currentValue < prevLowMa ? highMa : prevGhla;
            ghlaList.Add(ghla);

            var signal = GetCompareSignal(currentValue - ghla, prevValue - prevGhla);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ghla", ghlaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(ghlaList);
        stockData.IndicatorName = IndicatorName.GannHiLoActivator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Grover Llorens Cycle Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <param name="mult"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateGroverLlorensCycleOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod,
        int length = 100, int smoothLength = 20, double mult = 10)
    {
        if (double.IsNaN(mult) || double.IsInfinity(mult)) throw new ArgumentOutOfRangeException(nameof(mult));
        if (StrengthWindow.Supports(maType))
        {
            var (prices, highs, lows, _, _) = GetInputValuesList(stockData);
            var points = GroverWindow.Calculate(prices, highs, lows, maType, length, smoothLength, mult, true, false); var line = points.Line.ToList();
            var signals = CreateSignalsList(stockData); signals?.AddRange(points.Trades);
            stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Glco", line } }); stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.GroverLlorensCycleOscillator; return stockData;
        }

        List<double> tsList = new(stockData.Count);
        List<double> oscList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var atrList = CalculateAverageTrueRange(stockData, maType, length).ChainedValues;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var atr = atrList[i];
            var prevTs = i >= 1 ? tsList[i - 1] : currentValue;
            var diff = currentValue - prevTs;

            var ts = diff > 0 ? prevTs - (atr * mult) : diff < 0 ? prevTs + (atr * mult) : prevTs;
            tsList.Add(ts);

            var osc = currentValue - ts;
            oscList.Add(osc);
        }

        var smoList = GetMovingAverageList(stockData, maType, smoothLength, oscList);
        stockData.SetCustomValues(smoList);
        var rsiList = CalculateRelativeStrengthIndex(stockData, maType, length: smoothLength).ChainedValues;
        for (var i = 0; i < stockData.Count; i++)
        {
            var rsi = rsiList[i];
            var prevRsi1 = i >= 1 ? rsiList[i - 1] : 0;
            var prevRsi2 = i >= 2 ? rsiList[i - 2] : 0;

            var signal = GetRsiSignal(rsi - prevRsi1, prevRsi1 - prevRsi2, rsi, prevRsi1, 80, 20);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Glco", rsiList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(rsiList);
        stockData.IndicatorName = IndicatorName.GroverLlorensCycleOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Grover Llorens Activator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="mult"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateGroverLlorensActivator(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod,
        int length = 100, double mult = 5)
    {
        if (double.IsNaN(mult) || double.IsInfinity(mult)) throw new ArgumentOutOfRangeException(nameof(mult));
        if (StrengthWindow.Supports(maType))
        {
            var (prices, highs, lows, _, _) = GetInputValuesList(stockData);
            var points = GroverWindow.Calculate(prices, highs, lows, maType, length, 1, mult, false, false); var line = points.Line.ToList();
            var signals = CreateSignalsList(stockData); signals?.AddRange(points.Trades);
            stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Gla", line } }); stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.GroverLlorensActivator; return stockData;
        }

        List<double> tsList = new(stockData.Count);
        List<double> diffList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var atrList = CalculateAverageTrueRange(stockData, maType, length).ChainedValues;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var atr = atrList[i];
            var prevTs = i >= 1 ? tsList[i - 1] : currentValue;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            prevTs = prevTs == 0 ? prevValue : prevTs;

            var prevDiff = GetLastOrDefault(diffList);
            var diff = currentValue - prevTs;
            diffList.Add(diff);

            var ts = diff > 0 ? prevTs - (atr * mult) : diff < 0 ? prevTs + (atr * mult) : prevTs;
            tsList.Add(ts);

            var signal = GetCompareSignal(diff, prevDiff);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Gla", tsList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(tsList);
        stockData.IndicatorName = IndicatorName.GroverLlorensActivator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Guppy Count Back Line
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateGuppyCountBackLine(this StockData stockData, int length = 21)
    {
        List<double> cblList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        length = Math.Max(1, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevCbl = GetLastOrDefault(cblList);
            var cbl = currentValue;
            int highPivot = 0, lowPivot = 0;
            var highest = highList[i - 0];
            var lowest = lowList[i - 0];
            for (var offset = 1; offset < length && offset <= i; offset++)
            {
                var h = highList[i - offset]; var l = lowList[i - offset];
                if (h > highest) { highest = h; highPivot = offset; }
                if (l < lowest) { lowest = l; lowPivot = offset; }
            }
            // The latest extreme determines direction; an outside-bar tie uses its high.
            var rising = highPivot <= lowPivot;
            var pivot = rising ? highPivot : lowPivot;
            var level = rising ? lowList[i - pivot] : highList[i - pivot];
            var count = 0;
            for (var offset = pivot + 1; offset <= pivot + length && offset <= i; offset++)
            {
                var candidate = rising ? lowList[i - offset] : highList[i - offset];
                if (rising ? candidate < level : candidate > level)
                {
                    level = candidate;
                    if (++count == 2) { cbl = level; break; }
                }
            }
            cblList.Add(cbl);

            var signal = GetCompareSignal(currentValue - cbl, prevValue - prevCbl);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Cbl", cblList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(cblList);
        stockData.IndicatorName = IndicatorName.GuppyCountBackLine;

        return stockData;
    }


    /// <summary>
    /// Calculates the Guppy Multiple Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="length4"></param>
    /// <param name="length5"></param>
    /// <param name="length6"></param>
    /// <param name="length7"></param>
    /// <param name="length8"></param>
    /// <param name="length9"></param>
    /// <param name="length10"></param>
    /// <param name="length11"></param>
    /// <param name="length12"></param>
    /// <param name="length13"></param>
    /// <param name="length14"></param>
    /// <param name="length15"></param>
    /// <param name="length16"></param>
    /// <param name="length17"></param>
    /// <param name="length18"></param>
    /// <param name="length19"></param>
    /// <param name="length20"></param>
    /// <param name="length21"></param>
    /// <param name="length22"></param>
    /// <param name="length23"></param>
    /// <param name="length24"></param>
    /// <param name="length25"></param>
    /// <param name="length26"></param>
    /// <param name="length27"></param>
    /// <param name="length28"></param>
    /// <param name="length29"></param>
    /// <param name="length30"></param>
    /// <param name="length31"></param>
    /// <param name="length32"></param>
    /// <param name="length33"></param>
    /// <param name="length34"></param>
    /// <param name="length35"></param>
    /// <param name="smoothLength"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateGuppyMultipleMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 3, int length2 = 5, int length3 = 7, int length4 = 8, int length5 = 9, int length6 = 10, int length7 = 11, int length8 = 12,
        int length9 = 13, int length10 = 15, int length11 = 17, int length12 = 19, int length13 = 21, int length14 = 23, int length15 = 25,
        int length16 = 28, int length17 = 30, int length18 = 31, int length19 = 34, int length20 = 35, int length21 = 37, int length22 = 40,
        int length23 = 43, int length24 = 45, int length25 = 46, int length26 = 49, int length27 = 50, int length28 = 52, int length29 = 55,
        int length30 = 58, int length31 = 60, int length32 = 61, int length33 = 64, int length34 = 67, int length35 = 70, int smoothLength = 1,
        int signalLength = 13)
    {
        List<double> superGmmaFastList = new(stockData.Count);
        List<double> superGmmaSlowList = new(stockData.Count);
        List<double> superGmmaOscRawList = new(stockData.Count);
        List<double> superGmmaOscList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        using var superGmmaOscRawSumWindow = new ExactPartialMeanWindow(smoothLength);

        var ema3List = GetMovingAverageList(stockData, maType, length1, inputList);
        var ema5List = GetMovingAverageList(stockData, maType, length2, inputList);
        var ema7List = GetMovingAverageList(stockData, maType, length3, inputList);
        var ema9List = GetMovingAverageList(stockData, maType, length5, inputList);
        var ema11List = GetMovingAverageList(stockData, maType, length7, inputList);
        var ema13List = GetMovingAverageList(stockData, maType, length9, inputList);
        var ema15List = GetMovingAverageList(stockData, maType, length10, inputList);
        var ema17List = GetMovingAverageList(stockData, maType, length11, inputList);
        var ema19List = GetMovingAverageList(stockData, maType, length12, inputList);
        var ema21List = GetMovingAverageList(stockData, maType, length13, inputList);
        var ema23List = GetMovingAverageList(stockData, maType, length14, inputList);
        var ema25List = GetMovingAverageList(stockData, maType, length15, inputList);
        var ema28List = GetMovingAverageList(stockData, maType, length16, inputList);
        var ema31List = GetMovingAverageList(stockData, maType, length18, inputList);
        var ema34List = GetMovingAverageList(stockData, maType, length19, inputList);
        var ema37List = GetMovingAverageList(stockData, maType, length21, inputList);
        var ema40List = GetMovingAverageList(stockData, maType, length22, inputList);
        var ema43List = GetMovingAverageList(stockData, maType, length23, inputList);
        var ema46List = GetMovingAverageList(stockData, maType, length25, inputList);
        var ema49List = GetMovingAverageList(stockData, maType, length26, inputList);
        var ema52List = GetMovingAverageList(stockData, maType, length28, inputList);
        var ema55List = GetMovingAverageList(stockData, maType, length29, inputList);
        var ema58List = GetMovingAverageList(stockData, maType, length30, inputList);
        var ema61List = GetMovingAverageList(stockData, maType, length32, inputList);
        var ema64List = GetMovingAverageList(stockData, maType, length33, inputList);
        var ema67List = GetMovingAverageList(stockData, maType, length34, inputList);
        var ema70List = GetMovingAverageList(stockData, maType, length35, inputList);

        Span<double> fastRibbon = stackalloc double[11];
        Span<double> slowRibbon = stackalloc double[16];
        for (var i = 0; i < stockData.Count; i++)
        {
            var emaF1 = ema3List[i];
            var emaF2 = ema5List[i];
            var emaF3 = ema7List[i];
            var emaF4 = ema9List[i];
            var emaF5 = ema11List[i];
            var emaF6 = ema13List[i];
            var emaF7 = ema15List[i];
            var emaF8 = ema17List[i];
            var emaF9 = ema19List[i];
            var emaF10 = ema21List[i];
            var emaF11 = ema23List[i];
            var emaS1 = ema25List[i];
            var emaS2 = ema28List[i];
            var emaS3 = ema31List[i];
            var emaS4 = ema34List[i];
            var emaS5 = ema37List[i];
            var emaS6 = ema40List[i];
            var emaS7 = ema43List[i];
            var emaS8 = ema46List[i];
            var emaS9 = ema49List[i];
            var emaS10 = ema52List[i];
            var emaS11 = ema55List[i];
            var emaS12 = ema58List[i];
            var emaS13 = ema61List[i];
            var emaS14 = ema64List[i];
            var emaS15 = ema67List[i];
            var emaS16 = ema70List[i];

            fastRibbon[0] = emaF1;
            fastRibbon[1] = emaF2;
            fastRibbon[2] = emaF3;
            fastRibbon[3] = emaF4;
            fastRibbon[4] = emaF5;
            fastRibbon[5] = emaF6;
            fastRibbon[6] = emaF7;
            fastRibbon[7] = emaF8;
            fastRibbon[8] = emaF9;
            fastRibbon[9] = emaF10;
            fastRibbon[10] = emaF11;
            var superGmmaFast = GuppyRibbonArithmetic.Mean(fastRibbon);
            superGmmaFastList.Add(superGmmaFast);

            slowRibbon[0] = emaS1;
            slowRibbon[1] = emaS2;
            slowRibbon[2] = emaS3;
            slowRibbon[3] = emaS4;
            slowRibbon[4] = emaS5;
            slowRibbon[5] = emaS6;
            slowRibbon[6] = emaS7;
            slowRibbon[7] = emaS8;
            slowRibbon[8] = emaS9;
            slowRibbon[9] = emaS10;
            slowRibbon[10] = emaS11;
            slowRibbon[11] = emaS12;
            slowRibbon[12] = emaS13;
            slowRibbon[13] = emaS14;
            slowRibbon[14] = emaS15;
            slowRibbon[15] = emaS16;
            var superGmmaSlow = GuppyRibbonArithmetic.Mean(slowRibbon);
            superGmmaSlowList.Add(superGmmaSlow);

            var superGmmaOscRaw = GuppyRibbonArithmetic.Percent(superGmmaFast, superGmmaSlow);
            superGmmaOscRawList.Add(superGmmaOscRaw);

            var superGmmaOsc = superGmmaOscRawSumWindow.Next(superGmmaOscRaw, true);
            superGmmaOscList.Add(superGmmaOsc);
        }

        var superGmmaSignalList = GetMovingAverageList(stockData, maType, signalLength, superGmmaOscRawList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var superGmmaOsc = superGmmaOscList[i];
            var superGmmaSignal = superGmmaSignalList[i];
            var prevSuperGmmaOsc = i >= 1 ? superGmmaOscList[i - 1] : 0;
            var prevSuperGmmaSignal = i >= 1 ? superGmmaSignalList[i - 1] : 0;

            var signal = GetCompareSignal(superGmmaOsc - superGmmaSignal, prevSuperGmmaOsc - prevSuperGmmaSignal);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "SuperGmmaOsc", superGmmaOscList },
            { "SuperGmmaSignal", superGmmaSignalList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(superGmmaOscList);
        stockData.IndicatorName = IndicatorName.GuppyMultipleMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Guppy Distance Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="length4"></param>
    /// <param name="length5"></param>
    /// <param name="length6"></param>
    /// <param name="length7"></param>
    /// <param name="length8"></param>
    /// <param name="length9"></param>
    /// <param name="length10"></param>
    /// <param name="length11"></param>
    /// <param name="length12"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateGuppyDistanceIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 3, int length2 = 5, int length3 = 8, int length4 = 10, int length5 = 12, int length6 = 15, int length7 = 30, int length8 = 35,
        int length9 = 40, int length10 = 45, int length11 = 11, int length12 = 60)
    {
        List<double> fastDistanceList = new(stockData.Count);
        List<double> slowDistanceList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var ema3List = GetMovingAverageList(stockData, maType, length1, inputList);
        var ema5List = GetMovingAverageList(stockData, maType, length2, inputList);
        var ema8List = GetMovingAverageList(stockData, maType, length3, inputList);
        var ema10List = GetMovingAverageList(stockData, maType, length4, inputList);
        var ema12List = GetMovingAverageList(stockData, maType, length5, inputList);
        var ema15List = GetMovingAverageList(stockData, maType, length6, inputList);
        var ema30List = GetMovingAverageList(stockData, maType, length7, inputList);
        var ema35List = GetMovingAverageList(stockData, maType, length8, inputList);
        var ema40List = GetMovingAverageList(stockData, maType, length9, inputList);
        var ema45List = GetMovingAverageList(stockData, maType, length10, inputList);
        var ema50List = GetMovingAverageList(stockData, maType, length11, inputList);
        var ema60List = GetMovingAverageList(stockData, maType, length12, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var ema1 = ema3List[i];
            var ema2 = ema5List[i];
            var ema3 = ema8List[i];
            var ema4 = ema10List[i];
            var ema5 = ema12List[i];
            var ema6 = ema15List[i];
            var ema7 = ema30List[i];
            var ema8 = ema35List[i];
            var ema9 = ema40List[i];
            var ema10 = ema45List[i];
            var ema11 = ema50List[i];
            var ema12 = ema60List[i];

            var fastDistance = GuppyRibbonArithmetic.Distance(ema1, ema2, ema3, ema4, ema5, ema6);
            fastDistanceList.Add(fastDistance);

            var slowDistance = GuppyRibbonArithmetic.Distance(ema7, ema8, ema9, ema10, ema11, ema12);
            slowDistanceList.Add(slowDistance);

            var colFastL = ema1 > ema2 && ema2 > ema3 && ema3 > ema4 && ema4 > ema5 && ema5 > ema6;
            var colFastS = ema1 < ema2 && ema2 < ema3 && ema3 < ema4 && ema4 < ema5 && ema5 < ema6;
            var colSlowL = ema7 > ema8 && ema8 > ema9 && ema9 > ema10 && ema10 > ema11 && ema11 > ema12;
            var colSlowS = ema7 < ema8 && ema8 < ema9 && ema9 < ema10 && ema10 < ema11 && ema11 < ema12;

            var signal = GetConditionSignal(colSlowL || colFastL, colSlowS || colFastS);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "FastDistance", fastDistanceList },
            { "SlowDistance", slowDistanceList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.GuppyDistanceIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the G Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateGOscillator(this StockData stockData, int length = 14)
    {
        List<double> bSumList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var bSumWindow = new RollingSum();

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevBSum1 = i >= 1 ? bSumList[i - 1] : 0;
            var prevBSum2 = i >= 2 ? bSumList[i - 2] : 0;

            var b = currentValue > prevValue ? (double)100 / length : 0;
            bSumWindow.Add(b);

            var bSum = bSumWindow.Sum(length);
            bSumList.Add(bSum);

            var signal = GetRsiSignal(bSum - prevBSum1, prevBSum1 - prevBSum2, bSum, prevBSum1, 80, 20);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "GOsc", bSumList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(bSumList);
        stockData.IndicatorName = IndicatorName.GOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Gain Loss Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateGainLossMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod,
        int length = 14, int signalLength = 7)
    {
        length = Math.Max(1, length); signalLength = Math.Max(1, signalLength);
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        List<double> line = new(input.Count), signal = new(input.Count); var signals = CreateSignalsList(stockData);
        if (Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType))
        {
            var changes = input.Select((price, i) => GainLossAverageWindow.Change(price, i == 0 ? 0 : input[i - 1], i > 0)).ToList();
            line = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(changes), length)?.ToList() ?? GetMovingAverageList(stockData, maType, length, changes);
            signal = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(line), signalLength)?.ToList() ?? GetMovingAverageList(stockData, maType, signalLength, line);
        }
        else
        {
            using var window = new GainLossAverageWindow(maType, length, signalLength, Math.Max(1, input.Count));
            foreach (var price in input) { var value = window.Next(price, true); line.Add(value.Value); signal.Add(value.Signal); }
        }
        for (var i = 0; i < input.Count; i++) signals?.Add(GetCompareSignal(signal[i] - (i > 0 ? signal[i - 1] : 0), (i > 0 ? signal[i - 1] : 0) - (i > 1 ? signal[i - 2] : 0)));
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Glma", line }, { "Signal", signal } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.GainLossMovingAverage;
        return stockData;
    }


    /// <summary>
    /// Calculates the High Low Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateHighLowIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 10)
    {
        List<double> advDiffList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (_, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);
        var advSumWindow = new RollingSum();
        var loSumWindow = new RollingSum();

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevHighest = i >= 1 ? highestList[i - 1] : 0;
            var prevLowest = i >= 1 ? lowestList[i - 1] : 0;
            var highest = highestList[i];
            var lowest = lowestList[i];

            double adv = highest > prevHighest ? 1 : 0;
            advSumWindow.Add(adv);

            double lo = lowest < prevLowest ? 1 : 0;
            loSumWindow.Add(lo);

            var advSum = advSumWindow.Sum(length);
            var loSum = loSumWindow.Sum(length);

            var advDiff = advSum + loSum != 0 ? 100 * advSum / (advSum + loSum) : 0;
            advDiffList.Add(advDiff);
        }

        var zmbtiList = GetMovingAverageList(stockData, maType, length, advDiffList);
        if (maType == MovingAvgType.SimpleMovingAverage)
        {
            using var mean = new OoplesFinance.StockIndicators.Streaming.RoundedSimpleMovingAverageSmoother(Math.Max(1, length));
            for (var i = 0; i < zmbtiList.Count; i++) zmbtiList[i] = mean.Next(advDiffList[i], true);
        }
        for (var i = 0; i < stockData.Count; i++)
        {
            var zmbti = zmbtiList[i];
            var prevZmbti1 = i >= 1 ? zmbtiList[i - 1] : 0;
            var prevZmbti2 = i >= 2 ? zmbtiList[i - 2] : 0;

            var signal = GetRsiSignal(zmbti - prevZmbti1, prevZmbti1 - prevZmbti2, zmbti, prevZmbti1, 70, 30);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Zmbti", zmbtiList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(zmbtiList);
        stockData.IndicatorName = IndicatorName.HighLowIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Forecast Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateForecastOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 3)
    {
        length = Math.Max(1, length); var (input, _, _, _, _) = GetInputValuesList(stockData); var external = Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType); using var window = new OneBarReturnWindow(false, maType, length, external, input.Count); List<double> values = new(input.Count), average = new(input.Count); var signals = CreateSignalsList(stockData);
        foreach (var price in input) { var p = window.Next(price, true); values.Add(p.Value); average.Add(p.Signal); }
        if (external) average = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(values), length)?.ToList() ?? GetMovingAverageList(stockData, maType, length, values);
        for (var i = 0; i < input.Count; i++) signals?.Add(GetCompareSignal(average[i], i > 0 ? average[i - 1] : 0));
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Fo", values }, { "Signal", average } }); stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.ForecastOscillator; return stockData;
    }


    /// <summary>
    /// Calculates the Fast and Slow Kurtosis Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="ratio"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateFastandSlowKurtosisOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length = 3, double ratio = 0.03)
    {
        length = Math.Max(1, length);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        using var window = new FastSlowKurtosisWindow(length, ratio, maType);
        List<double> fskList = new(stockData.Count), fskSignalList;
        List<Signal>? signalsList = CreateSignalsList(stockData);
        if (Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType))
        {
            foreach (var price in inputList) fskList.Add(window.Line(price, true).Publish());
            fskSignalList = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(fskList), length)?.ToList() ?? GetMovingAverageList(stockData, maType, length, fskList);
        }
        else
        {
            fskSignalList = new(stockData.Count);
            foreach (var price in inputList) { var value = window.Next(price, true); fskList.Add(value.Line); fskSignalList.Add(value.Signal); }
        }
        for (var i = 0; i < fskSignalList.Count; i++)
        {
            var fsk = fskList[i];
            var fskSignal = fskSignalList[i];
            var prevFsk = i >= 1 ? fskList[i - 1] : 0;
            var prevFskSignal = i >= 1 ? fskSignalList[i - 1] : 0;

            var signal = GetCompareSignal(fsk - fskSignal, prevFsk - prevFskSignal);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Fsk", fskList },
            { "Signal", fskSignalList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(fskList);
        stockData.IndicatorName = IndicatorName.FastandSlowKurtosisOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Fast and Slow Relative Strength Index Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="length4"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateFastandSlowRelativeStrengthIndexOscillator(this StockData stockData,
        MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length1 = 3, int length2 = 6, int length3 = 9, int length4 = 6)
    {
        var (input, high, low, _, _) = GetInputValuesList(stockData); var external = Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType);
        var components = external ? FastSlowCompositeWindow.Components(stockData, input, high, low, true, maType, length1, length2, length3, length4) : null; using var window = new FastSlowCompositeWindow(true, maType, length1, length2, length3, length4, external);
        var line = new List<double>(input.Count); var signalLine = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++) { var point = window.Next(high[i], low[i], input[i], true, components?[0][i], components?[1][i], components?[2][i]); line.Add(point.Line); signalLine.Add(point.SignalLine); signals?.Add(point.Signal); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Fsrsi", line }, { "Signal", signalLine } }); stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.FastandSlowRelativeStrengthIndexOscillator; return stockData;
    }


    /// <summary>
    /// Calculates the Fast Slow Degree Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateFastSlowDegreeOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 100, int fastLength = 3, int slowLength = 2, int signalLength = 14)
    {
        var (prices, _, _, _, _) = GetInputValuesList(stockData); var external = !StrengthWindow.Supports(maType);
        using var window = new FastSlowDegreeWindow(maType, length, fastLength, slowLength, signalLength, external);
        var component = external ? FastSlowDegreeWindow.ComponentSignal(stockData, prices, maType, length, fastLength, slowLength, signalLength, false) : null;
        var line = new List<double>(prices.Count); var signal = new List<double>(prices.Count); var histogram = new List<double>(prices.Count); var trades = CreateSignalsList(stockData);
        for (var i = 0; i < prices.Count; i++) { var point = window.Next(prices[i], true, component?[i]); line.Add(point.Line); signal.Add(point.SignalLine); histogram.Add(point.Histogram); trades?.Add(point.Trade); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Fsdo", line }, { "Signal", signal }, { "Histogram", histogram } });
        stockData.SetSignals(trades); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.FastSlowDegreeOscillator; return stockData;
    }


    /// <summary>
    /// Calculates the Fractal Chaos Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateFractalChaosOscillator(this StockData stockData)
    {
        List<double> fcoList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var fractalChaosBandsList = CalculateFractalChaosBands(stockData);
        var upperBandList = fractalChaosBandsList.ChainedOutputs["UpperBand"];
        var lowerBandList = fractalChaosBandsList.ChainedOutputs["LowerBand"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var upperBand = upperBandList[i];
            var prevUpperBand = i >= 1 ? upperBandList[i - 1] : 0;
            var lowerBand = lowerBandList[i];
            var prevLowerBand = i >= 1 ? lowerBandList[i - 1] : 0;

            var prevFco = GetLastOrDefault(fcoList);
            double fco = upperBand != prevUpperBand ? 1 : lowerBand != prevLowerBand ? -1 : 0;
            fcoList.Add(fco);

            var signal = GetCompareSignal(fco, prevFco);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Fco", fcoList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(fcoList);
        stockData.IndicatorName = IndicatorName.FractalChaosOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Firefly Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateFireflyOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ZeroLagExponentialMovingAverage,
        int length = 10, int smoothLength = 3)
    {
        var (input, highs, lows, _, _) = GetInputValuesList(stockData);
        var result = FireflyWindow.Calculate(stockData, input, highs, lows, maType, length, smoothLength, false);
        var line = result.Line.ToList(); var signal = result.SignalLine.ToList(); var trades = CreateSignalsList(stockData, input.Count); trades?.AddRange(result.Trades);
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Fo", line }, { "Signal", signal } });
        stockData.SetSignals(trades); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.FireflyOscillator;
        return stockData;
    }


    /// <summary>
    /// Calculates the Fibonacci Retrace
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="factor"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateFibonacciRetrace(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length1 = 15, int length2 = 50, double factor = 0.382)
    {
        var (input, highs, lows, _, _) = GetInputValuesList(stockData);
        using var window = new FibonacciRetraceWindow(maType, length1, length2, factor);
        var upper = new List<double>(input.Count); var lower = new List<double>(input.Count); var trades = CreateSignalsList(stockData, input.Count);
        var external = !StrengthWindow.Supports(maType) ? GetMovingAverageList(stockData, maType, Math.Max(1, length1), input) : null;
        for (var i = 0; i < input.Count; i++)
        {
            var point = window.Next(highs[i], lows[i], input[i], true, external?[i]);
            upper.Add(point.Upper); lower.Add(point.Lower); trades?.Add(point.Trade);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "UpperBand", upper }, { "LowerBand", lower } });
        stockData.SetSignals(trades); stockData.SetCustomValues(new List<double>()); stockData.IndicatorName = IndicatorName.FibonacciRetrace;
        return stockData;
    }


    /// <summary>
    /// Calculates the FX Sniper Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="cciLength"></param>
    /// <param name="t3Length"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateFXSniperIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int cciLength = 14, int t3Length = 5, double b = MathHelper.InversePhi)
    {
        var prices = CommodityIndexWindow.Prices(stockData); var external = !StrengthWindow.Supports(maType);
        using var window = new FxSniperWindow(maType, cciLength, t3Length, b, external);
        var components = external ? FxSniperWindow.Components(stockData, prices, maType, cciLength, false) : default;
        var values = new List<double>(prices.Count); var trades = CreateSignalsList(stockData);
        for (var i = 0; i < prices.Count; i++) { var point = window.Next(prices[i], true, external ? components.Mean[i] : null, external ? components.Deviation[i] : null); values.Add(point.Line); trades?.Add(point.Trade); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "FXSniper", values } });
        stockData.SetSignals(trades); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.FXSniperIndicator; return stockData;
    }


    /// <summary>
    /// Calculates the Fear and Greed Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateFearAndGreedIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int fastLength = 10, int slowLength = 30, int smoothLength = 2)
    {
        var (input, high, low, _, _) = GetInputValuesList(stockData);
        var result = FearGreedWindow.Calculate(stockData, input, high, low, maType, fastLength, slowLength, smoothLength, false, true);
        var line = result.Line.ToList(); var signal = result.SignalLine.ToList(); var trades = CreateSignalsList(stockData, input.Count);
        trades?.AddRange(result.Trades);
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Fgi", line }, { "Signal", signal } });
        stockData.SetSignals(trades); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.FearAndGreedIndicator;
        return stockData;
    }


    /// <summary>
    /// Calculates the Function To Candles
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateFunctionToCandles(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod,
        int length = 14)
    {
        if (StrengthWindow.Supports(maType))
        {
            var (close, high, low, open, _) = GetInputValuesList(stockData);
            var c = FunctionCandleRsi.Calculate(close.ToArray(), maType, length, false).ToList();
            var o = FunctionCandleRsi.Calculate(open.ToArray(), maType, length, false).ToList();
            var h = FunctionCandleRsi.Calculate(high.ToArray(), maType, length, false).ToList();
            var l = FunctionCandleRsi.Calculate(low.ToArray(), maType, length, false).ToList();
            var events = CreateSignalsList(stockData); double previous = 0, previousTwo = 0;
            for (var i = 0; i < c.Count; i++)
            { var sum = new ExactMeanAccumulator(); sum.Add(c[i]); sum.Add(o[i]); sum.Add(h[i]); sum.Add(l[i]); var mean = sum.Mean(4);
              events?.Add(GetCompareSignal(mean - previous, previous - previousTwo)); previousTwo = previous; previous = mean; }
            stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Close", c }, { "Open", o }, { "High", h }, { "Low", l } });
            stockData.SetSignals(events); stockData.SetCustomValues(new List<double>()); stockData.IndicatorName = IndicatorName.FunctionToCandles; return stockData;
        }

        List<double> tpList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, openList, _) = GetInputValuesList(stockData);

        stockData.SetCustomValues(inputList);
        var rsiCList = CalculateRelativeStrengthIndex(stockData, maType, length: length).ChainedValues;
        stockData.SetCustomValues(openList);
        var rsiOList = CalculateRelativeStrengthIndex(stockData, maType, length: length).ChainedValues;
        stockData.SetCustomValues(highList);
        var rsiHList = CalculateRelativeStrengthIndex(stockData, maType, length: length).ChainedValues;
        stockData.SetCustomValues(lowList);
        var rsiLList = CalculateRelativeStrengthIndex(stockData, maType, length: length).ChainedValues;

        for (var i = 0; i < stockData.Count; i++)
        {
            var rsiC = rsiCList[i];
            var rsiO = rsiOList[i];
            var rsiH = rsiHList[i];
            var rsiL = rsiLList[i];
            var prevTp1 = i >= 1 ? tpList[i - 1] : 0;
            var prevTp2 = i >= 2 ? tpList[i - 2] : 0;

            var tp = (rsiC + rsiO + rsiH + rsiL) / 4;
            tpList.Add(tp);

            var signal = GetCompareSignal(tp - prevTp1, prevTp1 - prevTp2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Close", rsiCList },
            { "Open", rsiOList },
            { "High", rsiHList },
            { "Low", rsiLList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.FunctionToCandles;

        return stockData;
    }


    /// <summary>
    /// Calculates the Karobein Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateKarobeinOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 50)
    {
        List<double> aList = new(stockData.Count);
        List<double> bList = new(stockData.Count);
        List<double> dList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var emaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var ema = emaList[i];
            var prevEma = i >= 1 ? emaList[i - 1] : 0;

            var a = ema < prevEma && prevEma != 0 ? ema / prevEma : 0;
            aList.Add(a);

            var b = ema > prevEma && prevEma != 0 ? ema / prevEma : 0;
            bList.Add(b);
        }

        var aEmaList = GetMovingAverageList(stockData, maType, length, aList);
        var bEmaList = GetMovingAverageList(stockData, maType, length, bList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var ema = emaList[i];
            var prevEma = i >= 1 ? emaList[i - 1] : 0;
            var a = aEmaList[i];
            var b = bEmaList[i];
            var prevD1 = i >= 1 ? dList[i - 1] : 0;
            var prevD2 = i >= 2 ? dList[i - 2] : 0;
            var c = prevEma != 0 && ema != 0 ? MinOrMax(ema / prevEma / ((ema / prevEma) + b), 1, 0) : 0;

            var d = prevEma != 0 && ema != 0 ? MinOrMax((2 * (ema / prevEma / ((ema / prevEma) + (c * a)))) - 1, 1, 0) : 0;
            dList.Add(d);

            var signal = GetRsiSignal(d - prevD1, prevD1 - prevD2, d, prevD1, 0.8, 0.2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ko", dList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(dList);
        stockData.IndicatorName = IndicatorName.KarobeinOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Kase Peak Oscillator V1
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateKasePeakOscillatorV1(this StockData stockData, int length = 30, int smoothLength = 3)
    {
        List<double> diffList = new(stockData.Count);
        List<double> lnList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (_, highList, lowList, _, _) = GetInputValuesList(stockData);

        var sqrt = Sqrt(length);

        var atrList = CalculateAverageTrueRange(stockData, length: length).ChainedValues;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentAtr = atrList[i];
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var prevLow = i >= length ? lowList[i - length] : 0;
            var prevHigh = i >= length ? highList[i - length] : 0;
            var rwh = currentAtr != 0 ? (currentHigh - prevLow) / currentAtr * sqrt : 0;
            var rwl = currentAtr != 0 ? (prevHigh - currentLow) / currentAtr * sqrt : 0;

            var diff = rwh - rwl;
            diffList.Add(diff);
        }

        var pkList = GetMovingAverageList(stockData, MovingAvgType.WeightedMovingAverage, smoothLength, diffList);
        var mnList = GetMovingAverageList(stockData, MovingAvgType.SimpleMovingAverage, length, pkList);
        // The deviation of the peak-oscillator window about its own mean; the levels below are mn + 1.33 * sd,
        // a band at a multiple of sigma. See #190.
        var sdList = GetStandardDeviationList(pkList, length);
        for (var i = 0; i < stockData.Count; i++)
        {
            var pk = pkList[i];
            var mn = mnList[i];
            var sd = sdList[i];
            var prevPk = i >= 1 ? pkList[i - 1] : 0;
            var v1 = mn + (1.33 * sd) > 2.08 ? mn + (1.33 * sd) : 2.08;
            var v2 = mn - (1.33 * sd) < -1.92 ? mn - (1.33 * sd) : -1.92;

            var prevLn = GetLastOrDefault(lnList);
            var ln = prevPk >= 0 && pk > 0 ? v1 : prevPk <= 0 && pk < 0 ? v2 : 0;
            lnList.Add(ln);

            var signal = GetCompareSignal(ln, prevLn);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Kpo", lnList },
            { "Pk", pkList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(lnList);
        stockData.IndicatorName = IndicatorName.KasePeakOscillatorV1;

        return stockData;
    }


    /// <summary>
    /// Calculates the Kase Peak Oscillator V2
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="smoothLength"></param>
    /// <param name="devFactor"></param>
    /// <param name="sensitivity"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateKasePeakOscillatorV2(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int fastLength = 8, int slowLength = 65, int length1 = 9, int length2 = 30, int length3 = 50, int smoothLength = 3, double devFactor = 2, double sensitivity = 40)
    {
        var result = KasePeakV2Window.Compute(stockData, maType, fastLength, slowLength, length1, length2, smoothLength, sensitivity, false);
        var values = result.Values.ToList(); var trades = CreateSignalsList(stockData); trades?.AddRange(result.Trades);
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Kpo", values } }); stockData.SetSignals(trades);
        stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.KasePeakOscillatorV2; return stockData;
    }


    /// <summary>
    /// Calculates the Kase Serial Dependency Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateKaseSerialDependencyIndex(this StockData stockData, int length = 14)
    {
        List<double> ksdiUpList = new(stockData.Count);
        List<double> ksdiDownList = new(stockData.Count);
        List<double> tempList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var tempLog = StableLogRatio.OfSameSign(currentValue, prevValue);
            tempList.Add(tempLog);
        }

        stockData.SetCustomValues(tempList);
        var stdDevList = GetStandardDeviationList(tempList, length);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var volatility = stdDevList[i];
            var prevHigh = i >= length ? highList[i - length] : 0;
            var prevLow = i >= length ? lowList[i - length] : 0;
            var ksdiUpLog = StableLogRatio.OfSameSign(currentHigh, prevLow);
            var ksdiDownLog = StableLogRatio.OfSameSign(currentLow, prevHigh);

            var prevKsdiUp = GetLastOrDefault(ksdiUpList);
            var ksdiUp = volatility != 0 ? ksdiUpLog / volatility : 0;
            ksdiUpList.Add(ksdiUp);

            var prevKsdiDown = GetLastOrDefault(ksdiDownList);
            var ksdiDown = volatility != 0 ? ksdiDownLog / volatility : 0;
            ksdiDownList.Add(ksdiDown);

            var signal = GetCompareSignal(ksdiUp - ksdiDown, prevKsdiUp - prevKsdiDown);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "KsdiUp", ksdiUpList },
            { "KsdiDn", ksdiDownList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.KaseSerialDependencyIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Kaufman Binary Wave
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="fastSc"></param>
    /// <param name="slowSc"></param>
    /// <param name="filterPct"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateKaufmanBinaryWave(this StockData stockData, int length = 20, double fastSc = 0.6022, double slowSc = 0.0645,
        double filterPct = 10)
    {
        List<double> amaList = new(stockData.Count);
        List<double> diffList = new(stockData.Count);
        List<double> amaLowList = new(stockData.Count);
        List<double> amaHighList = new(stockData.Count);
        List<double> bwList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var efRatioList = CalculateKaufmanAdaptiveMovingAverage(stockData, length: length).ChainedOutputs["Er"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var efRatio = efRatioList[i];
            var prevAma = i >= 1 ? amaList[i - 1] : currentValue;
            var smooth = Pow((efRatio * fastSc) + slowSc, 2);

            var ama = prevAma + (smooth * (currentValue - prevAma));
            amaList.Add(ama);

            var diff = ama - prevAma;
            diffList.Add(diff);
        }

        stockData.SetCustomValues(diffList);

        // The deviation of the window about its own mean, not the mean squared residual from a moving average
        // of it. The filter is a percentage of a deviation of the adaptive average's own changes, and a move
        // has to clear it before the wave turns; the quantity this replaces is about 55% wider on a typical
        // price series, so the filter sat too high and the wave turned less often than it should. Taken over
        // diffList by name, which is the series this measures. See #190.
        var diffStdDevList = GetStandardDeviationList(diffList, length);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var ama = amaList[i];
            var diffStdDev = diffStdDevList[i];
            var prevAma = i >= 1 ? amaList[i - 1] : currentValue;
            var filter = filterPct / 100 * diffStdDev;

            var prevAmaLow = GetLastOrDefault(amaLowList);
            var amaLow = ama < prevAma ? ama : prevAmaLow;
            amaLowList.Add(amaLow);

            var prevAmaHigh = GetLastOrDefault(amaHighList);
            var amaHigh = ama > prevAma ? ama : prevAmaHigh;
            amaHighList.Add(amaHigh);

            var prevBw = GetLastOrDefault(bwList);
            double bw = ama - amaLow > filter ? 1 : amaHigh - ama > filter ? -1 : 0;
            bwList.Add(bw);

            var signal = GetCompareSignal(bw, prevBw);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Kbw", bwList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(bwList);
        stockData.IndicatorName = IndicatorName.KaufmanBinaryWave;

        return stockData;
    }


    /// <summary>
    /// Calculates the Kurtosis Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateKurtosisIndicator(this StockData stockData, int length1 = 3, int length2 = 1, int fastLength = 3,
        int slowLength = 65)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); using var window = new KurtosisWindow(length1, length2, fastLength, slowLength, input.Count);
        List<double> line, signal;
        if (Builder.Compute.ComponentAverage.HasOverrides)
        {
            var kList = input.Select(price => window.Difference(price, true).Publish()).ToList();
            line = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(kList), Math.Max(1, slowLength))?.ToList() ?? GetMovingAverageList(stockData, MovingAvgType.ExponentialMovingAverage, slowLength, kList);
            signal = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(line), Math.Max(1, fastLength))?.ToList() ?? GetMovingAverageList(stockData, MovingAvgType.WeightedMovingAverage, fastLength, line);
        }
        else
        {
            line = new(input.Count); signal = new(input.Count);
            foreach (var price in input) { var value = window.Next(price, true); line.Add(value.Line); signal.Add(value.Signal); }
        }
        var signals = CreateSignalsList(stockData); for (var i = 0; i < input.Count; i++) signals?.Add(GetCompareSignal(signal[i], i > 0 ? signal[i - 1] : 0));
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Fk", line }, { "Signal", signal } }); stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.KurtosisIndicator; return stockData;
    }


    /// <summary>
    /// Calculates the Kaufman Adaptive Correlation Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateKaufmanAdaptiveCorrelationOscillator(this StockData stockData,
        MovingAvgType maType = MovingAvgType.KaufmanAdaptiveMovingAverage, int length = 14)
    {
        List<double> indexList = new(stockData.Count);
        List<double> index2List = new(stockData.Count);
        List<double> src2List = new(stockData.Count);
        List<double> srcStList = new(stockData.Count);
        List<double> indexStList = new(stockData.Count);
        List<double> indexSrcList = new(stockData.Count);
        List<double> rList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var kamaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];

            double index = i;
            indexList.Add(index);

            var indexSrc = i * currentValue;
            indexSrcList.Add(indexSrc);

            var srcSrc = currentValue * currentValue;
            src2List.Add(srcSrc);

            var indexIndex = index * index;
            index2List.Add(indexIndex);
        }

        var indexMaList = GetMovingAverageList(stockData, maType, length, indexList);
        var indexSrcMaList = GetMovingAverageList(stockData, maType, length, indexSrcList);
        var index2MaList = GetMovingAverageList(stockData, maType, length, index2List);
        var src2MaList = GetMovingAverageList(stockData, maType, length, src2List);
        using var moments = maType == MovingAvgType.KaufmanAdaptiveMovingAverage && !Builder.Compute.ComponentAverage.HasOverrides
            ? new Streaming.KaufmanRegressionMoments(length) : null;
        for (var i = 0; i < stockData.Count; i++)
        {
            var srcMa = kamaList[i];
            var indexMa = indexMaList[i];
            var indexSrcMa = indexSrcMaList[i];
            var index2Ma = index2MaList[i];
            var src2Ma = src2MaList[i];
            var prevR1 = i >= 1 ? rList[i - 1] : 0;
            var prevR2 = i >= 2 ? rList[i - 2] : 0;

            var indexSqrt = index2Ma - Pow(indexMa, 2);
            var indexSt = indexSqrt >= 0 ? Sqrt(indexSqrt) : 0;


            var srcSqrt = src2Ma - Pow(srcMa, 2);
            var srcSt = srcSqrt >= 0 ? Sqrt(srcSqrt) : 0;


            var a = indexSrcMa - (indexMa * srcMa);
            var b = indexSt * srcSt;

            var r = b != 0 ? a / b : 0;
            if (moments is not null)
                moments.Next(inputList[i], true, out indexSt, out srcSt, out r);
            indexStList.Add(indexSt);
            srcStList.Add(srcSt);
            rList.Add(r);

            var signal = GetRsiSignal(r - prevR1, prevR1 - prevR2, r, prevR1, 0.5, -0.5);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "IndexSt", indexStList },
            { "SrcSt", srcStList },
            { "Kaco", rList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(rList);
        stockData.IndicatorName = IndicatorName.KaufmanAdaptiveCorrelationOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Know Sure Thing Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="length4"></param>
    /// <param name="rocLength1"></param>
    /// <param name="rocLength2"></param>
    /// <param name="rocLength3"></param>
    /// <param name="rocLength4"></param>
    /// <param name="signalLength"></param>
    /// <param name="weight1"></param>
    /// <param name="weight2"></param>
    /// <param name="weight3"></param>
    /// <param name="weight4"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateKnowSureThing(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 10,
        int length2 = 10, int length3 = 10, int length4 = 15, int rocLength1 = 10, int rocLength2 = 15, int rocLength3 = 20, int rocLength4 = 30,
        int signalLength = 9, double weight1 = 1, double weight2 = 2, double weight3 = 3, double weight4 = 4)
    {
        var callerSeries = stockData.CaptureInputSeries();
        List<double> kstList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        List<double> kstSignalList;
        if (StrengthWindow.Supports(maType) && !Builder.Compute.ComponentAverage.HasOverrides
#pragma warning disable S1244 // The specialized formula requires these exact integer coefficients.
            && weight1 == 1 && weight2 == 2 && weight3 == 3 && weight4 == 4)
#pragma warning restore S1244
        {
            var (prices, _, _, _, _) = GetInputValuesList(stockData);
            kstSignalList = new(stockData.Count);
            using var bank = new RocBankWindow(maType, new[] { rocLength1, rocLength2, rocLength3, rocLength4 }, new[] { length1, length2, length3, length4 }, new[] { 1, 2, 3, 4 }, signalLength, stockData.Count);
            foreach (var price in prices)
            {
                var next = bank.Next(price, true);
                kstList.Add(next.Value); kstSignalList.Add(next.Signal);
            }
        }
        else
        {
            var roc1List = CalculateRateOfChange(stockData, rocLength1).ChainedValues;
            stockData.RestoreInputSeries(callerSeries);
            var roc2List = CalculateRateOfChange(stockData, rocLength2).ChainedValues;
            stockData.RestoreInputSeries(callerSeries);
            var roc3List = CalculateRateOfChange(stockData, rocLength3).ChainedValues;
            stockData.RestoreInputSeries(callerSeries);
            var roc4List = CalculateRateOfChange(stockData, rocLength4).ChainedValues;
            var roc1SmaList = GetMovingAverageList(stockData, maType, length1, roc1List);
            var roc2SmaList = GetMovingAverageList(stockData, maType, length2, roc2List);
            var roc3SmaList = GetMovingAverageList(stockData, maType, length3, roc3List);
            var roc4SmaList = GetMovingAverageList(stockData, maType, length4, roc4List);

            for (var i = 0; i < stockData.Count; i++)
            {
                var roc1 = roc1SmaList[i];
                var roc2 = roc2SmaList[i];
                var roc3 = roc3SmaList[i];
                var roc4 = roc4SmaList[i];

                var kst = (roc1 * weight1) + (roc2 * weight2) + (roc3 * weight3) + (roc4 * weight4);
                kstList.Add(kst);
            }

            kstSignalList = GetMovingAverageList(stockData, maType, signalLength, kstList);
        }

        for (var i = 0; i < stockData.Count; i++)
        {
            var kst = kstList[i];
            var kstSignal = kstSignalList[i];
            var prevKst = i >= 1 ? kstList[i - 1] : 0;
            var prevKstSignal = i >= 1 ? kstSignalList[i - 1] : 0;

            var signal = GetCompareSignal(kst - kstSignal, prevKst - prevKstSignal);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Kst", kstList },
            { "Signal", kstSignalList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(kstList);
        stockData.IndicatorName = IndicatorName.KnowSureThing;

        return stockData;
    }


    /// <summary>
    /// Calculates the Kase Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateKaseIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 10)
    {
        var result = KaseRatioWindow.Compute(stockData, maType, length, false); var up = result.Up.ToList(); var down = result.Down.ToList();
        var trades = CreateSignalsList(stockData); trades?.AddRange(result.Trades);
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "KaseUp", up }, { "KaseDn", down } });
        stockData.SetSignals(trades); stockData.SetCustomValues(new List<double>()); stockData.IndicatorName = IndicatorName.KaseIndicator; return stockData;
    }


    /// <summary>
    /// Calculates the Kendall Rank Correlation Coefficient
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateKendallRankCorrelationCoefficient(this StockData stockData, int length = 20)
    {
        List<double> output = new(stockData.Count);
        List<Signal>? signals = CreateSignalsList(stockData);
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        using var window = new KendallCorrelationWindow(length);
        for (var i = 0; i < stockData.Count; i++)
        {
            var value = window.Next(input[i], true);
            var previous = i == 0 ? 0 : output[i - 1];
            var beforePrevious = i < 2 ? 0 : output[i - 2];
            output.Add(value);
            signals?.Add(GetCompareSignal(value - previous, previous - beforePrevious));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Krcc", output } });
        stockData.SetSignals(signals); stockData.SetCustomValues(output);
        stockData.IndicatorName = IndicatorName.KendallRankCorrelationCoefficient;
        return stockData;
    }


    /// <summary>
    /// Calculates the Kwan Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateKwanIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int length = 9, 
        int smoothLength = 2)
    {
        List<double> vrList = new(stockData.Count);
        List<double> prevList = new(stockData.Count);
        List<double> knrpList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        double prevSum = 0;
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        var rsiList = CalculateRelativeStrengthIndex(stockData, maType, length: length).ChainedValues;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentClose = inputList[i];
            var priorClose = i >= length ? inputList[i - length] : 0;
            var mom = priorClose != 0 ? currentClose / priorClose * 100 : 0;
            var rsi = rsiList[i];
            var hh = highestList[i];
            var ll = lowestList[i];
            var sto = hh - ll != 0 ? (currentClose - ll) / (hh - ll) * 100 : 0;
            var prevVr = i >= smoothLength ? vrList[i - smoothLength] : 0;
            var prevKnrp1 = i >= 1 ? knrpList[i - 1] : 0;
            var prevKnrp2 = i >= 2 ? knrpList[i - 2] : 0;

            var vr = mom != 0 ? sto * rsi / mom : 0;
            vrList.Add(vr);

            var prev = prevVr;
            prevList.Add(prev);

            prevSum += prev;
            var knrp = prevSum / smoothLength;
            knrpList.Add(knrp);

            var signal = GetCompareSignal(knrp - prevKnrp1, prevKnrp1 - prevKnrp2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ki", knrpList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(knrpList);
        stockData.IndicatorName = IndicatorName.KwanIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Kaufman Stress Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="marketData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateKaufmanStressIndicator(this StockData stockData, StockData marketData, int length = 60)
    {
        List<double> svList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList1, highList1, lowList1, _, _) = GetInputValuesList(stockData);
        var (inputList2, highList2, lowList2, _, _) = GetInputValuesList(marketData);
        var (highestList1, lowestList1) = GetMaxAndMinValuesList(highList1, lowList1, length);
        var (highestList2, lowestList2) = GetMaxAndMinValuesList(highList2, lowList2, length);
        var dWindow = new RollingMinMax(length);

        if (stockData.Count == marketData.Count)
        {
            for (var i = 0; i < stockData.Count; i++)
            {
                var highestHigh1 = highestList1[i];
                var lowestLow1 = lowestList1[i];
                var highestHigh2 = highestList2[i];
                var lowestLow2 = lowestList2[i];
                var currentValue1 = inputList1[i];
                var currentValue2 = inputList2[i];
                var prevSv1 = i >= 1 ? svList[i - 1] : 0;
                var prevSv2 = i >= 2 ? svList[i - 2] : 0;
                var r1 = highestHigh1 - lowestLow1;
                var r2 = highestHigh2 - lowestLow2;
                var s1 = r1 != 0 ? (currentValue1 - lowestLow1) / r1 : 0.5;
                var s2 = r2 != 0 ? (currentValue2 - lowestLow2) / r2 : 0.5;

                var d = Math.Abs(s1-s2) <= 1.4210854715202004e-14 ? 0 : s1-s2;
                dWindow.Add(d);
                var highestD = dWindow.Max;
                var lowestD = dWindow.Min;
                var r11 = highestD - lowestD;

                var sv = r11 != 0 ? MinOrMax(100 * (d - lowestD) / r11, 100, 0) : 50;
                svList.Add(sv);

                var signal = GetRsiSignal(sv - prevSv1, prevSv1 - prevSv2, sv, prevSv1, 90, 10);
                signalsList?.Add(signal);
            }
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ksi", svList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(svList);
        stockData.IndicatorName = IndicatorName.KaufmanStressIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Enhanced Williams R
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEnhancedWilliamsR(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14, 
        int signalLength = 5)
    {
        var (input, _, _, _, volume) = GetInputValuesList(stockData);
        var values = new List<double>(input.Count); var signals = new List<double>(input.Count); var trades = CreateSignalsList(stockData);
        var external = !StrengthWindow.Supports(maType);
        var components = external ? EnhancedWilliamsWindow.Components(stockData, input, volume, maType, length, signalLength, false, true) : default;
        using var window = new EnhancedWilliamsWindow(maType, length, signalLength, external);
        for (var i = 0; i < input.Count; i++)
        {
            var point = window.Next(input[i], volume[i], true, external ? components.PriceMean[i] : null, external ? components.VolumeMean[i] : null, external ? components.Signal[i] : null);
            values.Add(point.Line); signals.Add(point.SignalLine); trades?.Add(point.Trade);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Ewr", values }, { "Signal", signals } });
        stockData.SetSignals(trades); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.EnhancedWilliamsR; return stockData;
    }


    /// <summary>
    /// Calculates the Earning Support Resistance Levels
    /// </summary>
    /// <param name="stockData"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEarningSupportResistanceLevels(this StockData stockData)
    {
        List<double> mode1List = new(stockData.Count);
        List<double> mode2List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, closeList, _) = GetInputValuesList(InputName.MedianPrice, stockData);
        var highList = stockData.HighPrices;
        var lowList = stockData.LowPrices;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentHigh = highList[i];
            var prevClose = i >= 1 ? closeList[i - 1] : 0;
            var prevLow = i >= 2 ? lowList[i - 2] : 0;
            var prevValue2 = i >= 2 ? inputList[i - 2] : 0;
            var prevValue1 = i >= 1 ? inputList[i - 1] : 0;

            var prevMode1 = GetLastOrDefault(mode1List);
            var level = new ExactMeanAccumulator(); level.Add(prevLow); level.Add(currentHigh);
            var mode1 = level.Mean(2);
            mode1List.Add(mode1);

            var prevMode2 = GetLastOrDefault(mode2List);
            var signalMean = new ExactMeanAccumulator(); signalMean.Add(prevValue2); signalMean.Add(currentValue); signalMean.Add(prevClose);
            var mode2 = signalMean.Mean(3);
            mode2List.Add(mode2);

            var signal = GetBullishBearishSignal(currentValue - Math.Max(mode1, mode2), prevValue1 - Math.Max(prevMode1, prevMode2),
                currentValue - Math.Min(mode1, mode2), prevValue1 - Math.Min(prevMode1, prevMode2));
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Esr", mode1List }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(mode1List);
        stockData.IndicatorName = IndicatorName.EarningSupportResistanceLevels;

        return stockData;
    }


    /// <summary>
    /// Calculates the Elder Market Thermometer
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateElderMarketThermometer(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 22)
    {
        List<double> emtList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var highList = stockData.HighPrices; var lowList = stockData.LowPrices;

        var emaList = GetMovingAverageList(stockData, maType, length, inputList);
        List<double> wideSignal = new(stockData.Count);
        using var thermometer = new ElderThermometerWindow(maType, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var (emt, signal) = thermometer.Next(currentHigh, currentLow, true);
            emtList.Add(emt); wideSignal.Add(signal);
        }

        var aemtList = StrengthWindow.Supports(maType) ? wideSignal : GetMovingAverageList(stockData, maType, length, emtList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentEma = emaList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevEma = i >= 1 ? emaList[i - 1] : 0;
            var emt = emtList[i];
            var emtEma = aemtList[i];

            var signal = GetVolatilitySignal(currentValue - currentEma, prevValue - prevEma, emt, emtEma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Emt", emtList },
            { "Signal", aemtList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(emtList);
        stockData.IndicatorName = IndicatorName.ElderMarketThermometer;

        return stockData;
    }


    /// <summary>
    /// Calculates the Elliott Wave Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateElliottWaveOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int fastLength = 5,
        int slowLength = 34)
    {
        List<double> ewoList = new(stockData.Count);
        List<double> ewoHistogramList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = maType == MovingAvgType.SimpleMovingAverage ? BollingerArithmetic.Mean(inputList, fastLength) : GetMovingAverageList(stockData, maType, fastLength, inputList);
        var sma34List = maType == MovingAvgType.SimpleMovingAverage ? BollingerArithmetic.Mean(inputList, slowLength) : GetMovingAverageList(stockData, maType, slowLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentSma5 = smaList[i];
            var currentSma34 = sma34List[i];

            var ewo = currentSma5 - currentSma34;
            ewoList.Add(ewo);
        }

        var finiteInput = FiniteSignalInput.Create(ewoList, out var finiteCount);
        var ewoSignalLineList = maType == MovingAvgType.SimpleMovingAverage ? BollingerArithmetic.Mean(finiteInput, fastLength) : GetMovingAverageList(stockData, maType, fastLength, finiteInput);
        for (var i = finiteCount; i < ewoSignalLineList.Count; i++) ewoSignalLineList[i] = double.NaN;
        for (var i = 0; i < stockData.Count; i++)
        {
            var ewo = ewoList[i];
            var ewoSignalLine = ewoSignalLineList[i];

            var prevEwoHistogram = GetLastOrDefault(ewoHistogramList);
            var ewoHistogram = ewo - ewoSignalLine;
            ewoHistogramList.Add(ewoHistogram);

            var signal = GetCompareSignal(ewoHistogram, prevEwoHistogram);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ewo", ewoList },
            { "Signal", ewoSignalLineList },
            { "Histogram", ewoHistogramList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(ewoList);
        stockData.IndicatorName = IndicatorName.ElliottWaveOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ergodic Candlestick Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateErgodicCandlestickOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length1 = 32, int length2 = 12)
    {
        if (StrengthWindow.Supports(maType) && !Builder.Compute.ComponentAverage.HasOverrides)
        {
            var (close, _, _, open, _) = GetInputValuesList(stockData); var high = stockData.HighPrices; var low = stockData.LowPrices;
            using var window = new ErgodicCandleWindow(maType, length1, length2, close.Count);
            var line = new List<double>(close.Count); var signal = new List<double>(close.Count); var signals = CreateSignalsList(stockData);
            for (var i = 0; i < close.Count; i++)
            {
                var value = window.Next(close[i], open[i], high[i], low[i], true);
                signals?.Add(GetCompareSignal(value.Eco - value.Signal, i > 0 ? line[i - 1] - signal[i - 1] : 0));
                line.Add(value.Eco); signal.Add(value.Signal);
            }
            stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Eco", line }, { "Signal", signal } });
            stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.ErgodicCandlestickOscillator; return stockData;
        }

        List<double> xcoList = new(stockData.Count);
        List<double> xhlList = new(stockData.Count);
        List<double> ecoList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, openList, _) = GetInputValuesList(stockData); var highList = stockData.HighPrices; var lowList = stockData.LowPrices;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentOpen = openList[i];
            var currentClose = inputList[i];

            var xco = currentClose - currentOpen;
            xcoList.Add(xco);

            var xhl = currentHigh - currentLow;
            xhlList.Add(xhl);
        }

        var xcoEma1List = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(xcoList), length1)?.ToList() ?? GetMovingAverageList(stockData, maType, length1, xcoList);
        var xcoEma2List = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(xcoEma1List), length2)?.ToList() ?? GetMovingAverageList(stockData, maType, length2, xcoEma1List);
        var xhlEma1List = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(xhlList), length1)?.ToList() ?? GetMovingAverageList(stockData, maType, length1, xhlList);
        var xhlEma2List = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(xhlEma1List), length2)?.ToList() ?? GetMovingAverageList(stockData, maType, length2, xhlEma1List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var xhlEma2 = xhlEma2List[i];
            var xcoEma2 = xcoEma2List[i];

            var eco = xhlEma2 != 0 ? 100 * xcoEma2 / xhlEma2 : 0;
            ecoList.Add(eco);
        }

        var ecoSignalList = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(ecoList), length2)?.ToList() ?? GetMovingAverageList(stockData, maType, length2, ecoList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var eco = ecoList[i];
            var ecoEma = ecoSignalList[i];
            var prevEco = i >= 1 ? ecoList[i - 1] : 0;
            var prevEcoEma = i >= 1 ? ecoSignalList[i - 1] : 0;

            var signal = GetCompareSignal(eco - ecoEma, prevEco - prevEcoEma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Eco", ecoList },
            { "Signal", ecoSignalList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(ecoList);
        stockData.IndicatorName = IndicatorName.ErgodicCandlestickOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ergodic True Strength Index V1
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateErgodicTrueStrengthIndexV1(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length1 = 4, int length2 = 8, int length3 = 6, int signalLength = 3)
    {
        List<double> etsiList = new(stockData.Count);
        List<double> priceDiffList = new(stockData.Count);
        List<double> absPriceDiffList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        if (StrengthWindow.Supports(maType))
            etsiList.AddRange(StrengthWindow.Compute(inputList, maType, length1, length2, length3));
        else
        {
            for (var i = 0; i < stockData.Count; i++)
            {
                var currentValue = inputList[i];
                var prevValue = i >= 1 ? inputList[i - 1] : 0;

                var priceDiff = MinPastValues(i, 1, currentValue - prevValue);
                priceDiffList.Add(priceDiff);

                var absPriceDiff = Math.Abs(priceDiff);
                absPriceDiffList.Add(absPriceDiff);
            }

            var diffEma1List = GetMovingAverageList(stockData, maType, length1, priceDiffList);
            var absDiffEma1List = GetMovingAverageList(stockData, maType, length1, absPriceDiffList);
            var diffEma2List = GetMovingAverageList(stockData, maType, length2, diffEma1List);
            var absDiffEma2List = GetMovingAverageList(stockData, maType, length2, absDiffEma1List);
            var diffEma3List = GetMovingAverageList(stockData, maType, length3, diffEma2List);
            var absDiffEma3List = GetMovingAverageList(stockData, maType, length3, absDiffEma2List);
            for (var i = 0; i < stockData.Count; i++)
            {
                var diffEma3 = diffEma3List[i];
                var absDiffEma3 = absDiffEma3List[i];

                var etsi = absDiffEma3 != 0 ? MinOrMax(100 * diffEma3 / absDiffEma3, 100, -100) : 0;
                etsiList.Add(etsi);
            }

        }

        var etsiSignalList = StrengthWindow.Supports(maType) ? StrengthWindow.Smooth(etsiList, maType, signalLength)
            : GetMovingAverageList(stockData, maType, signalLength, etsiList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var etsi = etsiList[i];
            var etsiSignal = etsiSignalList[i];
            var prevEtsi = i >= 1 ? etsiList[i - 1] : 0;
            var prevEtsiSignal = i >= 1 ? etsiSignalList[i - 1] : 0;

            var signal = GetCompareSignal(etsi - etsiSignal, prevEtsi - prevEtsiSignal);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Etsi", etsiList },
            { "Signal", etsiSignalList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(etsiList);
        stockData.IndicatorName = IndicatorName.ErgodicTrueStrengthIndexV1;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ergodic True Strength Index V2
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="length4"></param>
    /// <param name="length5"></param>
    /// <param name="length6"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateErgodicTrueStrengthIndexV2(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length1 = 21, int length2 = 9, int length3 = 9, int length4 = 17, int length5 = 6, int length6 = 2, int signalLength = 2)
    {
        List<double> etsi2List = new(stockData.Count);
        List<double> etsi1List = new(stockData.Count);
        List<double> priceDiffList = new(stockData.Count);
        List<double> absPriceDiffList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        if (StrengthWindow.Supports(maType))
        {
            etsi1List.AddRange(StrengthWindow.Compute(inputList, maType, length1, length2, length3));
            etsi2List.AddRange(StrengthWindow.Compute(inputList, maType, length4, length5, length6));
        }
        else
        {
            for (var i = 0; i < stockData.Count; i++)
            {
                var currentValue = inputList[i];
                var prevValue = i >= 1 ? inputList[i - 1] : 0;

                var priceDiff = MinPastValues(i, 1, currentValue - prevValue);
                priceDiffList.Add(priceDiff);

                var absPriceDiff = Math.Abs(priceDiff);
                absPriceDiffList.Add(absPriceDiff);
            }

            var diffEma1List = GetMovingAverageList(stockData, maType, length1, priceDiffList);
            var absDiffEma1List = GetMovingAverageList(stockData, maType, length1, absPriceDiffList);
            var diffEma4List = GetMovingAverageList(stockData, maType, length4, priceDiffList);
            var absDiffEma4List = GetMovingAverageList(stockData, maType, length4, absPriceDiffList);
            var diffEma2List = GetMovingAverageList(stockData, maType, length2, diffEma1List);
            var absDiffEma2List = GetMovingAverageList(stockData, maType, length2, absDiffEma1List);
            var diffEma5List = GetMovingAverageList(stockData, maType, length5, diffEma4List);
            var absDiffEma5List = GetMovingAverageList(stockData, maType, length5, absDiffEma4List);
            var diffEma3List = GetMovingAverageList(stockData, maType, length3, diffEma2List);
            var absDiffEma3List = GetMovingAverageList(stockData, maType, length3, absDiffEma2List);
            var diffEma6List = GetMovingAverageList(stockData, maType, length6, diffEma5List);
            var absDiffEma6List = GetMovingAverageList(stockData, maType, length6, absDiffEma5List);
            for (var i = 0; i < stockData.Count; i++)
            {
                var diffEma6 = diffEma6List[i];
                var absDiffEma6 = absDiffEma6List[i];
                var diffEma3 = diffEma3List[i];
                var absDiffEma3 = absDiffEma3List[i];

                var etsi1 = absDiffEma3 != 0 ? MinOrMax(diffEma3 / absDiffEma3 * 100, 100, -100) : 0;
                etsi1List.Add(etsi1);

                var etsi2 = absDiffEma6 != 0 ? MinOrMax(diffEma6 / absDiffEma6 * 100, 100, -100) : 0;
                etsi2List.Add(etsi2);
            }

        }

        var etsi2SignalList = StrengthWindow.Supports(maType) ? StrengthWindow.Smooth(etsi2List, maType, signalLength)
            : GetMovingAverageList(stockData, maType, signalLength, etsi2List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var etsi2 = etsi2List[i];
            var etsi2Signal = etsi2SignalList[i];
            var prevEtsi2 = i >= 1 ? etsi2List[i - 1] : 0;
            var prevEtsi2Signal = i >= 1 ? etsi2SignalList[i - 1] : 0;

            var signal = GetCompareSignal(etsi2 - etsi2Signal, prevEtsi2 - prevEtsi2Signal);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Etsi1", etsi1List },
            { "Etsi2", etsi2List },
            { "Signal", etsi2SignalList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(etsi2List);
        stockData.IndicatorName = IndicatorName.ErgodicTrueStrengthIndexV2;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ergodic Commodity Selection Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <param name="pointValue"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateErgodicCommoditySelectionIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, 
        int length = 32, int smoothLength = 5, double pointValue = 1)
    {
        var (input, high, low, _, _) = GetInputValuesList(stockData);
        var values = new List<double>(input.Count); var signals = new List<double>(input.Count); var trades = CreateSignalsList(stockData);
        var external = !StrengthWindow.Supports(maType) || Builder.Compute.ComponentAverage.HasOverrides;
        var legacySignal = !StrengthWindow.Supports(maType);
        using var window = new ErgodicSelectionWindow(maType, length, smoothLength, pointValue, external);
        var components = external ? ErgodicSelectionWindow.Components(stockData, input, high, low, maType, length, smoothLength, pointValue, true, false) : default;
        for (var i = 0; i < input.Count; i++)
        {
            var point = window.Next(high[i], low[i], input[i], true, external ? components.Adx[i] : null, includeSignal: !legacySignal);
            values.Add(point.Line); if (!legacySignal) { signals.Add(point.SignalLine); trades?.Add(point.Trade); }
        }
        if (legacySignal)
        {
            // The legacy batch path consumes the four ADX overrides, but not a fifth signal override.
            signals = GetMovingAverageList(stockData, maType, Math.Max(1, smoothLength), values);
            var previous = new System.Numerics.BigInteger(); var previousSlope = new System.Numerics.BigInteger();
            foreach (var value in signals) { var current = ExactVarianceWindow.Units(value); var slope = current - previous; trades?.Add(slope.Sign > 0 && slope > previousSlope ? Signal.StrongBuy : slope.Sign < 0 && slope < previousSlope ? Signal.StrongSell : slope.Sign > 0 ? Signal.Buy : slope.Sign < 0 ? Signal.Sell : Signal.None); previous = current; previousSlope = slope; }
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Ecsi", values }, { "Signal", signals } });
        stockData.SetSignals(trades); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.ErgodicCommoditySelectionIndex; return stockData;
    }


    /// <summary>
    /// Calculates the Enhanced Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEnhancedIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14, 
        int signalLength = 8)
    {
        var (input, high, low, _, _) = GetInputValuesList(stockData);
        var values = new List<double>(input.Count); var signals = new List<double>(input.Count); var trades = CreateSignalsList(stockData);
        var external = !StrengthWindow.Supports(maType);
        var components = external ? EnhancedIndexWindow.Components(stockData, input, high, low, maType, length, signalLength, false, true) : default;
        using var window = new EnhancedIndexWindow(maType, length, signalLength, external);
        for (var i = 0; i < input.Count; i++)
        {
            var point = window.Next(high[i], low[i], input[i], true, external ? components.Mean[i] : null, external ? components.Signal[i] : null);
            values.Add(point.Line); signals.Add(point.SignalLine); trades?.Add(point.Trade);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Ei", values }, { "Signal", signals } });
        stockData.SetSignals(trades); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.EnhancedIndex; return stockData;
    }


    /// <summary>
    /// Calculates the Ema Wave Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEmaWaveIndicator(this StockData stockData, int length1 = 5, int length2 = 25, int length3 = 50, int smoothLength = 4)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        List<double> Wave(int length)
        {
            using var window = new ResidualAverageWindow(MovingAvgType.ExponentialMovingAverage, length, MovingAvgType.SimpleMovingAverage, smoothLength);
            return input.Select(value => window.Next(value, true).Value).ToList();
        }
        var a = Wave(length1); var b = Wave(length2); var c = Wave(length3); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++) signals?.Add(GetConditionSignal(a[i] > 0 && b[i] > 0 && c[i] > 0, a[i] < 0 && b[i] < 0 && c[i] < 0));
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Wa", a }, { "Wb", b }, { "Wc", c } });
        stockData.SetSignals(signals); stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.EmaWaveIndicator; return stockData;
    }


    /// <summary>
    /// Calculates the Ergodic Mean Deviation Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateErgodicMeanDeviationIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length1 = 32, int length2 = 5, int length3 = 5, int signalLength = 5)
    {
        if (StrengthWindow.Supports(maType))
        {
            var (prices, _, _, _, _) = GetInputValuesList(stockData);
            using var window = new ResidualAverageWindow(maType, length1, maType, length2, length3, signalLength);
            var line = new List<double>(stockData.Count); var signalLine = new List<double>(stockData.Count); var signals = CreateSignalsList(stockData);
            for (var i = 0; i < prices.Count; i++)
            {
                var value = window.Next(prices[i], true);
                signals?.Add(GetCompareSignal(value.Value - value.Signal, i > 0 ? line[i - 1] - signalLine[i - 1] : 0));
                line.Add(value.Value); signalLine.Add(value.Signal);
            }
            stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Emdi", line }, { "Signal", signalLine } });
            stockData.SetSignals(signals); stockData.SetCustomValues(line);
            stockData.IndicatorName = IndicatorName.ErgodicMeanDeviationIndicator; return stockData;
        }
        List<double> ma1List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var emaList = GetMovingAverageList(stockData, maType, length1, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentEma = emaList[i];

            var ma1 = currentValue - currentEma;
            ma1List.Add(ma1);
        }

        var ma1EmaList = GetMovingAverageList(stockData, maType, length2, ma1List);
        var emdiList = GetMovingAverageList(stockData, maType, length3, ma1EmaList);
        var emdiSignalList = GetMovingAverageList(stockData, maType, signalLength, emdiList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var emdi = emdiList[i];
            var emdiSignal = emdiSignalList[i];
            var prevEmdi = i >= 1 ? emdiList[i - 1] : 0;
            var prevEmdiSignal = i >= 1 ? emdiSignalList[i - 1] : 0;

            var signal = GetCompareSignal(emdi - emdiSignal, prevEmdi - prevEmdiSignal);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Emdi", emdiList },
            { "Signal", emdiSignalList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(emdiList);
        stockData.IndicatorName = IndicatorName.ErgodicMeanDeviationIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Efficient Price
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEfficientPrice(this StockData stockData, int length = 50)
    {
        List<double> line = new(stockData.Count); List<Signal>? signals = CreateSignalsList(stockData);
        var (input, _, _, _, _) = GetInputValuesList(stockData); var window = new EfficiencyOutputWindow(length);
        for (var i = 0; i < input.Count; i++)
        {
            var value = window.Next(input[i], true);
            signals?.Add(GetCompareSignal(value - (i == 0 ? 0 : line[i - 1]), i == 0 ? 0 : line[i - 1] - (i < 2 ? 0 : line[i - 2]))); line.Add(value);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Ep", line } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.EfficientPrice;
        return stockData;
    }


    /// <summary>
    /// Calculates the Efficient Auto Line
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="fastAlpha"></param>
    /// <param name="slowAlpha"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEfficientAutoLine(this StockData stockData, int length = 19, double fastAlpha = 0.0001, double slowAlpha = 0.005)
    {
        List<double> line = new(stockData.Count); List<Signal>? signals = CreateSignalsList(stockData);
        var (input, _, _, _, _) = GetInputValuesList(stockData); var window = new EfficiencyOutputWindow(length, true, fastAlpha, slowAlpha);
        for (var i = 0; i < input.Count; i++)
        {
            var value = window.Next(input[i], true);
            signals?.Add(GetCompareSignal(input[i] - value, i == 0 ? 0 : input[i - 1] - line[i - 1])); line.Add(value);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Eal", line } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.EfficientAutoLine;
        return stockData;
    }
}

