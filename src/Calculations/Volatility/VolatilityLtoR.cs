using OoplesFinance.StockIndicators.Compatibility;
using OoplesFinance.StockIndicators.Core;

namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Moving Average BandWidth
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="mult"></param>
    /// <returns></returns>
    public static StockData CalculateMovingAverageBandWidth(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int fastLength = 10, int slowLength = 50, double mult = 1)
    {
        List<double> mabwList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var mabList = CalculateMovingAverageBands(stockData, maType, fastLength, slowLength, mult);
        var ubList = mabList.OutputValues["UpperBand"];
        var lbList = mabList.OutputValues["LowerBand"];
        var maList = mabList.OutputValues["MiddleBand"];

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
    public static StockData CalculateMovingAverageAdaptiveFilter(this StockData stockData, int length = 10, double filter = 0.15, 
        double fastAlpha = 0.667, double slowAlpha = 0.0645)
    {
        List<double> amaList = new(stockData.Count);
        List<double> amaDiffList = new(stockData.Count);
        List<double> maafList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var erList = CalculateKaufmanAdaptiveMovingAverage(stockData, length: length).OutputValues["Er"];
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
        var stdDevList = CalculateStandardDeviationVolatility(stockData, length: length).CustomValuesList;
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
            var stdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
            var spStdDevList = CalculateStandardDeviationVolatility(marketData, maType, length).CustomValuesList;

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
    public static StockData CalculateMotionSmoothnessIndex(this StockData stockData, int length = 50)
    {
        List<double> bList = new(stockData.Count);
        List<double> chgList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var stdDevList = CalculateStandardDeviationVolatility(stockData, length: length).CustomValuesList;
        var emaList = GetMovingAverageList(stockData, MovingAvgType.ExponentialMovingAverage, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var chg = MinPastValues(i, 1, currentValue - prevValue);
            chgList.Add(chg);
        }

        stockData.SetCustomValues(chgList);
        var aChgStdDevList = CalculateStandardDeviationVolatility(stockData, length: length).CustomValuesList;
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
    public static StockData CalculateQmaSmaDifference(this StockData stockData, int length = 14)
    {
        List<double> cList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var qmaList = CalculateQuadraticMovingAverage(stockData, length).CustomValuesList;
        var smaList = CalculateSimpleMovingAverage(stockData, length).CustomValuesList;
        var emaList = CalculateExponentialMovingAverage(stockData, length).CustomValuesList;

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
    public static StockData CalculateProjectionOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length = 14, int smoothLength = 4)
    {
        List<double> pboList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var projectionBandsList = CalculateProjectionBands(stockData, length);
        var puList = projectionBandsList.OutputValues["UpperBand"];
        var plList = projectionBandsList.OutputValues["LowerBand"];
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
    public static StockData CalculateProjectionBandwidth(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length = 14)
    {
        List<double> pbwList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var projectionBandsList = CalculateProjectionBands(stockData, length);
        var puList = projectionBandsList.OutputValues["UpperBand"];
        var plList = projectionBandsList.OutputValues["LowerBand"];
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

