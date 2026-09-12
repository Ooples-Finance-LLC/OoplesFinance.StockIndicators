using OoplesFinance.StockIndicators.Compatibility;
using OoplesFinance.StockIndicators.Core;

namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Parkinson Volatility.
    /// </summary>
    /// <remarks>
    /// Parkinson's estimator, which reads volatility from the range each bar travelled rather than from
    /// close to close: the mean squared logarithm of high over low, scaled by four times the logarithm of
    /// two, and annualised by the root of 252. Because it uses the whole bar it is the more efficient
    /// estimate of the two, though it cannot see a gap between one bar's close and the next bar's open.
    /// </remarks>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateParkinsonVolatility(this StockData stockData, int length = 20)
    {
        length = Math.Max(length, 1);
        var (_, highList, lowList, _, _) = GetInputValuesList(stockData);
        var count = highList.Count;
        List<double> volatilityList = new(count);
        List<Signal>? signalsList = CreateSignalsList(stockData, count);
        var factor = 1 / (4 * length * Log(2));
        var annualisationFactor = Sqrt(252);

        for (var i = 0; i < count; i++)
        {
            double volatility = 0;
            if (i >= length - 1)
            {
                double sum = 0;
                for (var j = i - length + 1; j <= i; j++)
                {
                    var logRatio = lowList[j] != 0 ? Log(highList[j] / lowList[j]) : 0;
                    sum += logRatio * logRatio;
                }

                volatility = Sqrt(factor * sum) * annualisationFactor;
            }

            volatilityList.Add(volatility);

            var prevVolatility1 = i >= 1 ? volatilityList[i - 1] : 0;
            var prevVolatility2 = i >= 2 ? volatilityList[i - 2] : 0;
            var signal = GetCompareSignal(volatility - prevVolatility1, prevVolatility1 - prevVolatility2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Pv", volatilityList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(volatilityList);
        stockData.IndicatorName = IndicatorName.ParkinsonVolatility;

        return stockData;
    }

    /// <summary>
    /// Calculates the Rogers Satchell Volatility.
    /// </summary>
    /// <remarks>
    /// The Rogers-Satchell estimator, which reads each bar's high and low against both its open and its
    /// close. Unlike Parkinson's, it stays unbiased when the price drifts, because the drift cancels between
    /// the two products it sums. Annualised by the root of 252.
    /// </remarks>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateRogersSatchellVolatility(this StockData stockData, int length = 20)
    {
        length = Math.Max(length, 1);
        var (inputList, highList, lowList, openList, _) = GetInputValuesList(stockData);
        var count = inputList.Count;
        List<double> volatilityList = new(count);
        List<Signal>? signalsList = CreateSignalsList(stockData, count);
        var annualisationFactor = Sqrt(252);

        for (var i = 0; i < count; i++)
        {
            double volatility = 0;
            if (i >= length - 1)
            {
                double sum = 0;
                for (var j = i - length + 1; j <= i; j++)
                {
                    var currentClose = inputList[j];
                    var currentOpen = openList[j];
                    var logHc = currentClose != 0 ? Log(highList[j] / currentClose) : 0;
                    var logHo = currentOpen != 0 ? Log(highList[j] / currentOpen) : 0;
                    var logLc = currentClose != 0 ? Log(lowList[j] / currentClose) : 0;
                    var logLo = currentOpen != 0 ? Log(lowList[j] / currentOpen) : 0;
                    sum += (logHc * logHo) + (logLc * logLo);
                }

                volatility = Sqrt(sum / length) * annualisationFactor;
            }

            volatilityList.Add(volatility);

            var prevVolatility1 = i >= 1 ? volatilityList[i - 1] : 0;
            var prevVolatility2 = i >= 2 ? volatilityList[i - 2] : 0;
            var signal = GetCompareSignal(volatility - prevVolatility1, prevVolatility1 - prevVolatility2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Rsv", volatilityList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(volatilityList);
        stockData.IndicatorName = IndicatorName.RogersSatchellVolatility;

        return stockData;
    }

    /// <summary>
    /// Calculates the Normalized Average True Range.
    /// </summary>
    /// <remarks>
    /// The average true range as a percentage of the price, so that one instrument's volatility can be
    /// compared with another's whatever they cost. The average is the one
    /// <see cref="CalculateAverageTrueRange"/> takes, smoothed Wilder's way, so the two agree bar for bar;
    /// a bar with no price to divide by publishes zero.
    /// </remarks>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateNormalizedAverageTrueRange(this StockData stockData, int length = 14)
    {
        length = Math.Max(length, 1);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var count = inputList.Count;
        List<double> natrList = new(count);
        List<Signal>? signalsList = CreateSignalsList(stockData, count);

        var trList = GetTrueRangeList(stockData);
        var trSpan = SpanCompat.AsReadOnlySpan(trList);
        var atrBuffer = SpanCompat.CreateOutputBuffer(count);
        MovingAverageCore.WellesWilderMovingAverage(trSpan, atrBuffer.Span, length);

        for (var i = 0; i < count; i++)
        {
            var currentValue = inputList[i];
            var natr = currentValue != 0 ? atrBuffer.Span[i] / currentValue * 100 : 0;
            natrList.Add(natr);

            var prevNatr1 = i >= 1 ? natrList[i - 1] : 0;
            var prevNatr2 = i >= 2 ? natrList[i - 2] : 0;
            var signal = GetCompareSignal(natr - prevNatr1, prevNatr1 - prevNatr2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Natr", natrList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(natrList);
        stockData.IndicatorName = IndicatorName.NormalizedAverageTrueRange;

        return stockData;
    }

    /// <summary>
    /// Calculates the Moving Average BandWidth
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="mult"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateMovingAverageBandWidth(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int fastLength = 10, int slowLength = 50, double mult = 1)
    {
        List<double> mabwList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var mabList = CalculateMovingAverageBands(stockData, maType, fastLength, slowLength, mult);
        var ubList = mabList.ChainedOutputs["UpperBand"];
        var lbList = mabList.ChainedOutputs["LowerBand"];
        var maList = mabList.ChainedOutputs["MiddleBand"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var mb = maList[i];
            var ub = ubList[i];
            var lb = lbList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevMb = i >= 1 ? maList[i - 1] : 0;
            var prevUb = i >= 1 ? ubList[i - 1] : 0;
            var prevLb = i >= 1 ? lbList[i - 1] : 0;

            var mabw = mb != 0 ? (ub - lb) / mb * 100 : 0;
            mabwList.Add(mabw);

            var signal = GetBollingerBandsSignal(currentValue - mb, prevValue - prevMb, currentValue, prevValue, ub, prevUb, lb, prevLb);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Mabw", mabwList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(mabwList);
        stockData.IndicatorName = IndicatorName.MovingAverageBandWidth;

        return stockData;
    }


    /// <summary>
    /// Calculates the Moving Average Adaptive Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="filter"></param>
    /// <param name="fastAlpha"></param>
    /// <param name="slowAlpha"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateMovingAverageAdaptiveFilter(this StockData stockData, int length = 10, double filter = 0.15, 
        double fastAlpha = 0.667, double slowAlpha = 0.0645)
    {
        List<double> amaList = new(stockData.Count);
        List<double> amaDiffList = new(stockData.Count);
        List<double> maafList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var erList = CalculateKaufmanAdaptiveMovingAverage(stockData, length: length).ChainedOutputs["Er"];
        var emaList = GetMovingAverageList(stockData, MovingAvgType.ExponentialMovingAverage, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevAma = i >= 1 ? amaList[i - 1] : currentValue;
            var er = erList[i];
            var sm = Pow((er * (fastAlpha - slowAlpha)) + slowAlpha, 2);

            var ama = prevAma + (sm * (currentValue - prevAma));
            amaList.Add(ama);

            var amaDiff = ama - prevAma;
            amaDiffList.Add(amaDiff);
        }

        stockData.SetCustomValues(amaDiffList);
        var stdDevList = CalculateStandardDeviationVolatility(stockData, length: length).ChainedValues;
        for (var i = 0; i < stockData.Count; i++)
        {
            var stdDev = stdDevList[i];
            var currentValue = inputList[i];
            var ema = emaList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevEma = i >= 1 ? emaList[i - 1] : 0;

            var prevMaaf = GetLastOrDefault(maafList);
            var maaf = stdDev * filter;
            maafList.Add(maaf);

            var signal = GetVolatilitySignal(currentValue - ema, prevValue - prevEma, maaf, prevMaaf);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Maaf", maafList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(maafList);
        stockData.IndicatorName = IndicatorName.MovingAverageAdaptiveFilter;

        return stockData;
    }


    /// <summary>
    /// Calculates the Relative Normalized Volatility
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="marketData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateRelativeNormalizedVolatility(this StockData stockData, StockData marketData,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14)
    {
        List<double> absZsrcList = new(stockData.Count);
        List<double> absZspList = new(stockData.Count);
        List<double> rList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var (spInputList, _, _, _, _) = GetInputValuesList(marketData);

        if (stockData.Count == marketData.Count)
        {
            var emaList = GetMovingAverageList(stockData, maType, length, inputList);
            var stdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).ChainedValues;
            var spStdDevList = CalculateStandardDeviationVolatility(marketData, maType, length).ChainedValues;

            for (var i = 0; i < stockData.Count; i++)
            {
                var currentValue = inputList[i];
                var prevValue = i >= 1 ? inputList[i - 1] : 0;
                var spValue = spInputList[i];
                var prevSpValue = i >= 1 ? spInputList[i - 1] : 0;
                var stdDev = stdDevList[i];
                var spStdDev = spStdDevList[i];
                var d = MinPastValues(i, 1, currentValue - prevValue);
                var sp = spValue - prevSpValue;
                var zsrc = stdDev != 0 ? d / stdDev : 0;
                var zsp = spStdDev != 0 ? sp / spStdDev : 0;

                var absZsrc = Math.Abs(zsrc);
                absZsrcList.Add(absZsrc);

                var absZsp = Math.Abs(zsp);
                absZspList.Add(absZsp);
            }

            var absZsrcSmaList = GetMovingAverageList(stockData, maType, length, absZsrcList);
            var absZspSmaList = GetMovingAverageList(marketData, maType, length, absZspList);
            for (var i = 0; i < stockData.Count; i++)
            {
                var currentValue = inputList[i];
                var currentEma = emaList[i];
                var absZsrcSma = absZsrcSmaList[i];
                var absZspSma = absZspSmaList[i];
                var prevValue = i >= 1 ? inputList[i - 1] : 0;
                var prevEma = i >= 1 ? emaList[i - 1] : 0;

                var r = absZspSma != 0 ? absZsrcSma / absZspSma : 0;
                rList.Add(r);

                var signal = GetVolatilitySignal(currentValue - currentEma, prevValue - prevEma, r, 1);
                signalsList?.Add(signal);
            }
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Rnv", rList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(rList);
        stockData.IndicatorName = IndicatorName.RelativeNormalizedVolatility;

        return stockData;
    }


    /// <summary>
    /// Calculates the Reversal Points
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateReversalPoints(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 100)
    {
        List<double> aList = new(stockData.Count);
        List<double> bList = new(stockData.Count);
        List<double> bSumList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum bSumWindow = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var c = length + (length / Sqrt(length) / 2);
        var length1 = MinOrMax((int)Math.Ceiling((double)length / 2));

        var emaList = GetMovingAverageList(stockData, maType, length1, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var max = Math.Max(currentValue, prevValue);
            var min = Math.Min(currentValue, prevValue);

            var a = max - min;
            aList.Add(a);
        }

        var aEma1List = GetMovingAverageList(stockData, maType, length1, aList);
        var aEma2List = GetMovingAverageList(stockData, maType, length1, aEma1List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var aEma1 = aEma1List[i];
            var aEma2 = aEma2List[i];
            var currentValue = inputList[i];
            var ema = emaList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevEma = i >= 1 ? emaList[i - 1] : 0;

            var b = aEma2 != 0 ? aEma1 / aEma2 : 0;
            bList.Add(b);
            bSumWindow.Add(b);

            var bSum = bSumWindow.Sum(length);
            bSumList.Add(bSum);

            var signal = GetVolatilitySignal(currentValue - ema, prevValue - prevEma, bSum, c);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Rp", bSumList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(bSumList);
        stockData.IndicatorName = IndicatorName.ReversalPoints;

        return stockData;
    }


    /// <summary>
    /// Calculates the Mayer Multiple
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="threshold"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateMayerMultiple(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 200,
        double threshold = 2.4)
    {
        List<double> mmList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentSma = smaList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevSma = i >= 1 ? smaList[i - 1] : 0;

            var mm = currentSma != 0 ? currentValue / currentSma : 0;
            mmList.Add(mm);

            var signal = GetVolatilitySignal(currentValue - currentSma, prevValue - prevSma, mm, threshold);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Mm", mmList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(mmList);
        stockData.IndicatorName = IndicatorName.MayerMultiple;

        return stockData;
    }


    /// <summary>
    /// Calculates the Motion Smoothness Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateMotionSmoothnessIndex(this StockData stockData, int length = 50)
    {
        List<double> bList = new(stockData.Count);
        List<double> chgList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var stdDevList = CalculateStandardDeviationVolatility(stockData, length: length).ChainedValues;
        var emaList = GetMovingAverageList(stockData, MovingAvgType.ExponentialMovingAverage, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var chg = MinPastValues(i, 1, currentValue - prevValue);
            chgList.Add(chg);
        }

        stockData.SetCustomValues(chgList);
        var aChgStdDevList = CalculateStandardDeviationVolatility(stockData, length: length).ChainedValues;
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentEma = emaList[i];
            var aChgStdDev = aChgStdDevList[i];
            var stdDev = stdDevList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevEma = i >= 1 ? emaList[i - 1] : 0;

            var b = stdDev != 0 ? aChgStdDev / stdDev : 0;
            bList.Add(b);

            var signal = GetVolatilitySignal(currentValue - currentEma, prevValue - prevEma, b, 0.5);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Msi", bList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(bList);
        stockData.IndicatorName = IndicatorName.MotionSmoothnessIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Market Meanness Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateMarketMeannessIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.EhlersNoiseEliminationTechnology, int length = 100)
    {
        List<double> mmiList = new(stockData.Count);
        List<double> tempList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        using var medianWindow = new RollingMedian(length);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var maList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            tempList.Add(currentValue);
            medianWindow.Add(currentValue);

            var median = medianWindow.Median;
            int nl = 0, nh = 0;
            for (var j = 1; j < length; j++)
            {
                var value1 = i >= j - 1 ? tempList[i - (j - 1)] : 0;
                var value2 = i >= j ? tempList[i - j] : 0;

                if (value1 > median && value1 > value2)
                {
                    nl++;
                }
                else if (value1 < median && value1 < value2)
                {
                    nh++;
                }
            }

            double mmi = length != 1 ? 100 * (nl + nh) / (length - 1) : 0;
            mmiList.Add(mmi);
        }

        var mmiFilterList = GetMovingAverageList(stockData, maType, length, mmiList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var mmiFilt = mmiFilterList[i];
            var prevMmiFilt1 = i >= 1 ? mmiFilterList[i - 1] : 0;
            var prevMmiFilt2 = i >= 2 ? mmiFilterList[i - 2] : 0;
            var currentValue = inputList[i];
            var currentMa = maList[i];

            var signal = GetConditionSignal(currentValue < currentMa && ((mmiFilt > prevMmiFilt1 && prevMmiFilt1 < prevMmiFilt2) || (mmiFilt < prevMmiFilt1 && prevMmiFilt1 > prevMmiFilt2)), currentValue < currentMa && ((mmiFilt > prevMmiFilt1 && prevMmiFilt1 < prevMmiFilt2) || (mmiFilt < prevMmiFilt1 && prevMmiFilt1 > prevMmiFilt2)));
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Mmi", mmiList },
            { "MmiSmoothed", mmiFilterList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(mmiList);
        stockData.IndicatorName = IndicatorName.MarketMeannessIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Qma Sma Difference
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateQmaSmaDifference(this StockData stockData, int length = 14)
    {
        List<double> cList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        // All three averages are of the prices; each Calculate call leaves its own output on CustomValuesList.
        var callerSeries = stockData.CaptureInputSeries();
        var qmaList = CalculateQuadraticMovingAverage(stockData, length).ChainedValues;
        stockData.RestoreInputSeries(callerSeries);
        var smaList = CalculateSimpleMovingAverage(stockData, length).ChainedValues;
        stockData.RestoreInputSeries(callerSeries);
        var emaList = CalculateExponentialMovingAverage(stockData, length).ChainedValues;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentEma = emaList[i];
            var sma = smaList[i];
            var qma = qmaList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevEma = i >= 1 ? emaList[i - 1] : 0;

            var prevC = GetLastOrDefault(cList);
            var c = qma - sma;
            cList.Add(c);

            var signal = GetVolatilitySignal(currentValue - currentEma, prevValue - prevEma, c, prevC);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "QmaSmaDiff", cList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(cList);
        stockData.IndicatorName = IndicatorName.QmaSmaDifference;

        return stockData;
    }


    /// <summary>
    /// Calculates the Projection Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateProjectionOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length = 14, int smoothLength = 4)
    {
        List<double> pboList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var projectionBandsList = CalculateProjectionBands(stockData, length);
        var puList = projectionBandsList.ChainedOutputs["UpperBand"];
        var plList = projectionBandsList.ChainedOutputs["LowerBand"];
        var wmaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var pl = plList[i];
            var pu = puList[i];

            var pbo = pu - pl != 0 ? 100 * (currentValue - pl) / (pu - pl) : 0;
            pboList.Add(pbo);
        }

        var pboSignalList = GetMovingAverageList(stockData, maType, smoothLength, pboList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var pbo = pboSignalList[i];
            var prevPbo = i >= 1 ? pboSignalList[i - 1] : 0;
            var wma = wmaList[i];
            var currentValue = inputList[i];
            var prevWma = i >= 1 ? wmaList[i - 1] : 0;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var signal = GetVolatilitySignal(currentValue - wma, prevValue - prevWma, pbo, prevPbo);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Pbo", pboList },
            { "Signal", pboSignalList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(pboList);
        stockData.IndicatorName = IndicatorName.ProjectionOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Projection Bandwidth
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateProjectionBandwidth(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length = 14)
    {
        List<double> pbwList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var projectionBandsList = CalculateProjectionBands(stockData, length);
        var puList = projectionBandsList.ChainedOutputs["UpperBand"];
        var plList = projectionBandsList.ChainedOutputs["LowerBand"];
        var wmaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var pu = puList[i];
            var pl = plList[i];

            var pbw = pu + pl != 0 ? 200 * (pu - pl) / (pu + pl) : 0;
            pbwList.Add(pbw);
        }

        var pbwSignalList = GetMovingAverageList(stockData, maType, length, pbwList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var pbw = pbwList[i];
            var pbwSignal = pbwSignalList[i];
            var wma = wmaList[i];
            var currentValue = inputList[i];
            var prevWma = i >= 1 ? wmaList[i - 1] : 0;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var signal = GetVolatilitySignal(currentValue - wma, prevValue - prevWma, pbw, pbwSignal);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Pbw", pbwList },
            { "Signal", pbwSignalList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(pbwList);
        stockData.IndicatorName = IndicatorName.ProjectionBandwidth;

        return stockData;
    }

}

