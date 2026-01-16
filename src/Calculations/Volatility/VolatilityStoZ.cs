using OoplesFinance.StockIndicators.Compatibility;
using OoplesFinance.StockIndicators.Core;

namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Standard Deviation Volatility
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateStandardDeviationVolatility(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 20)
    {
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var count = inputList.Count;
        List<double> smaList;
        List<double> divisionOfSumList;
        List<double> stdDevVolatilityList;
        List<double> stdDevSmaList;

        if (maType == MovingAvgType.SimpleMovingAverage)
        {
            var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
            var smaBuffer = SpanCompat.CreateOutputBuffer(count);
            MovingAverageCore.SimpleMovingAverage(inputSpan, smaBuffer.Span, length);
            smaList = smaBuffer.ToList();

            var deviationSquared = new double[count];
            for (var i = 0; i < count; i++)
            {
                var currentDeviation = inputList[i] - smaBuffer.Span[i];
                deviationSquared[i] = Pow(currentDeviation, 2);
            }

            var varianceBuffer = SpanCompat.CreateOutputBuffer(count);
            MovingAverageCore.SimpleMovingAverage(deviationSquared, varianceBuffer.Span, length);
            divisionOfSumList = varianceBuffer.ToList();

            var stdDevBuffer = SpanCompat.CreateOutputBuffer(count);
            for (var i = 0; i < count; i++)
            {
                stdDevBuffer.Span[i] = Sqrt(varianceBuffer.Span[i]);
            }

            stdDevVolatilityList = stdDevBuffer.ToList();
            var stdDevSmaBuffer = SpanCompat.CreateOutputBuffer(count);
            MovingAverageCore.SimpleMovingAverage(stdDevBuffer.Span, stdDevSmaBuffer.Span, length);
            stdDevSmaList = stdDevSmaBuffer.ToList();
        }
        else
        {
            var deviationSquaredList = new List<double>(count);
            smaList = GetMovingAverageList(stockData, maType, length, inputList);

            for (var i = 0; i < count; i++)
            {
                var currentValue = inputList[i];
                var currentSma = smaList[i];
                var currentDeviation = currentValue - currentSma;

                var deviationSquared = Pow(currentDeviation, 2);
                deviationSquaredList.Add(deviationSquared);
            }

            divisionOfSumList = GetMovingAverageList(stockData, maType, length, deviationSquaredList);
            stdDevVolatilityList = new List<double>(count);
            for (var i = 0; i < count; i++)
            {
                var divisionOfSum = divisionOfSumList[i];
                var stdDevVolatility = Sqrt(divisionOfSum);
                stdDevVolatilityList.Add(stdDevVolatility);
            }

            stdDevSmaList = GetMovingAverageList(stockData, maType, length, stdDevVolatilityList);
        }

        List<Signal>? signalsList = CreateSignalsList(stockData, count);
        for (var i = 0; i < count; i++)
        {
            var currentValue = inputList[i];
            var currentSma = smaList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevSma = i >= 1 ? smaList[i - 1] : 0;
            var stdDev = stdDevVolatilityList[i];
            var stdDevMa = stdDevSmaList[i];

            var signal = GetVolatilitySignal(currentValue - currentSma, prevValue - prevSma, stdDev, stdDevMa);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "StdDev", stdDevVolatilityList },
            { "Variance", divisionOfSumList },
            { "Signal", stdDevSmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(stdDevVolatilityList);
        stockData.IndicatorName = IndicatorName.StandardDeviationVolatility;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ultimate Volatility Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateUltimateVolatilityIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 14)
    {
        List<double> uviList = new(stockData.Count);
        List<double> absList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum absSumWindow = new();
        var (inputList, _, _, openList, _) = GetInputValuesList(stockData);

        var maList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentOpen = openList[i];
            var currentClose = inputList[i];
            var currentMa = maList[i];
            var prevClose = i >= 1 ? inputList[i - 1] : 0;
            var prevMa = i >= 1 ? maList[i - 1] : 0;

            var abs = Math.Abs(currentClose - currentOpen);
            absList.Add(abs);
            absSumWindow.Add(abs);

            var uvi = (double)1 / length * absSumWindow.Sum(length);
            uviList.Add(uvi);

            var signal = GetVolatilitySignal(currentClose - currentMa, prevClose - prevMa, uvi, 1);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Uvi", uviList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(uviList);
        stockData.IndicatorName = IndicatorName.UltimateVolatilityIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Volatility Switch Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateVolatilitySwitchIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length = 14)
    {
        List<double> drList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var rocSma = (currentValue + prevValue) / 2;
            var dr = rocSma != 0 ? MinPastValues(i, 1, currentValue - prevValue) / rocSma : 0;
            drList.Add(dr);
        }

        stockData.SetCustomValues(drList);
        var volaList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        var vswitchList = GetMovingAverageList(stockData, maType, length, volaList);
        var wmaList = GetMovingAverageList(stockData, maType, length, inputList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentWma = wmaList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevWma = i >= 1 ? wmaList[i - 1] : 0;
            var vswitch14 = vswitchList[i];

            var signal = GetVolatilitySignal(currentValue - currentWma, prevValue - prevWma, vswitch14, 0.5);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Vsi", vswitchList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(vswitchList);
        stockData.IndicatorName = IndicatorName.VolatilitySwitchIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Vertical Horizontal Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculateVerticalHorizontalFilter(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length = 18, int signalLength = 6)
    {
        List<double> vhfList = new(stockData.Count);
        List<double> changeList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum changeSumWindow = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(inputList, length);

        var wmaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentValue = inputList[i];
            var highestPrice = highestList[i];
            var lowestPrice = lowestList[i];
            var numerator = Math.Abs(highestPrice - lowestPrice);

            var priceChange = Math.Abs(MinPastValues(i, 1, currentValue - prevValue));
            changeList.Add(priceChange);
            changeSumWindow.Add(priceChange);

            var denominator = changeSumWindow.Sum(length);
            var vhf = denominator != 0 ? numerator / denominator : 0;
            vhfList.Add(vhf);
        }

        var vhfWmaList = GetMovingAverageList(stockData, maType, signalLength, vhfList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentWma = wmaList[i];
            var prevWma = i >= 1 ? wmaList[i - 1] : 0;
            var vhfWma = vhfWmaList[i];
            var vhf = vhfList[i];

            var signal = GetVolatilitySignal(currentValue - currentWma, prevValue - prevWma, vhf, vhfWma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Vhf", vhfList },
            { "Signal", vhfWmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(vhfList);
        stockData.IndicatorName = IndicatorName.VerticalHorizontalFilter;

        return stockData;
    }


    /// <summary>
    /// Calculates the Statistical Volatility
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculateStatisticalVolatility(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length1 = 30, int length2 = 253)
    {
        List<double> volList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList1, lowestList1) = GetMaxAndMinValuesList(inputList, length1);
        var (highestList2, lowestList2) = GetMaxAndMinValuesList(highList, lowList, length1);

        var annualSqrt = Sqrt((double)length2 / length1);

        var emaList = GetMovingAverageList(stockData, maType, length1, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var maxC = highestList1[i];
            var minC = lowestList1[i];
            var maxH = highestList2[i];
            var minL = lowestList2[i];
            var cLog = minC != 0 ? Math.Log(maxC / minC) : 0;
            var hlLog = minL != 0 ? Math.Log(maxH / minL) : 0;

            var vol = MinOrMax(((0.6 * cLog * annualSqrt) + (0.6 * hlLog * annualSqrt)) * 0.5, 2.99, 0);
            volList.Add(vol);
        }

        var volEmaList = GetMovingAverageList(stockData, maType, length1, volList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentEma = emaList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevEma = i >= 1 ? emaList[i - 1] : 0;
            var vol = volList[i];
            var volEma = volEmaList[i];

            var signal = GetVolatilitySignal(currentValue - currentEma, prevValue - prevEma, vol, volEma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Sv", volList },
            { "Signal", volEmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(volList);
        stockData.IndicatorName = IndicatorName.StatisticalVolatility;

        return stockData;
    }


    /// <summary>
    /// Calculates the Standard Deviation
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateStandardDevation(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14)
    {
        List<double> cList = new(stockData.Count);
        List<double> powList = new(stockData.Count);
        List<double> tempList = new(stockData.Count);
        List<double> sumList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum tempSumWindow = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var emaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            tempList.Add(currentValue);
            tempSumWindow.Add(currentValue);

            var sum = tempSumWindow.Sum(length);
            var sumPow = Pow(sum, 2);
            sumList.Add(sumPow);

            var pow = Pow(currentValue, 2);
            powList.Add(pow);
        }

        var powSmaList = GetMovingAverageList(stockData, maType, length, powList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var a = powSmaList[i];
            var sum = sumList[i];
            var b = sum / Pow(length, 2);

            var c = a - b >= 0 ? Sqrt(a - b) : 0;
            cList.Add(c);
        }

        var cSmaList = GetMovingAverageList(stockData, maType, length, cList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentEma = emaList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevEma = i >= 1 ? emaList[i - 1] : 0;
            var c = cList[i];
            var cSma = cSmaList[i];

            var signal = GetVolatilitySignal(currentValue - currentEma, prevValue - prevEma, c, cSma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Std", cList },
            { "Signal", cSmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(cList);
        stockData.IndicatorName = IndicatorName.StandardDeviation;

        return stockData;
    }


    /// <summary>
    /// Calculates the Volatility Based Momentum
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculateVolatilityBasedMomentum(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod,
        int length1 = 22, int length2 = 65)
    {
        List<double> vbmList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var atrList = CalculateAverageTrueRange(stockData, maType, length2).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentAtr = atrList[i];
            var currentValue = inputList[i];
            var prevValue = i >= length1 ? inputList[i - length1] : 0;
            var rateOfChange = MinPastValues(i, length1, currentValue - prevValue);

            var vbm = currentAtr != 0 ? rateOfChange / currentAtr : 0;
            vbmList.Add(vbm);
        }

        var vbmEmaList = GetMovingAverageList(stockData, maType, length1, vbmList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var vbm = vbmList[i];
            var vbmEma = vbmEmaList[i];
            var prevVbm = i >= 1 ? vbmList[i - 1] : 0;
            var prevVbmEma = i >= 1 ? vbmEmaList[i - 1] : 0;

            var signal = GetCompareSignal(vbm - vbmEma, prevVbm - prevVbmEma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Vbm", vbmList },
            { "Signal", vbmEmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(vbmList);
        stockData.IndicatorName = IndicatorName.VolatilityBasedMomentum;

        return stockData;
    }


    /// <summary>
    /// Calculates the Volatility Quality Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <returns></returns>
    public static StockData CalculateVolatilityQualityIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int fastLength = 9, int slowLength = 200)
    {
        List<double> vqiList = new(stockData.Count);
        List<double> vqiSumList = new(stockData.Count);
        List<double> vqiTList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, openList, _) = GetInputValuesList(stockData);
        double vqiSum = 0;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentClose = inputList[i];
            var currentOpen = openList[i];
            var prevClose = i >= 1 ? inputList[i - 1] : 0;
            var trueRange = CalculateTrueRange(currentHigh, currentLow, prevClose);

            var prevVqiT = GetLastOrDefault(vqiTList);
            var vqiT = trueRange != 0 && currentHigh - currentLow != 0 ?
                (((currentClose - prevClose) / trueRange) + ((currentClose - currentOpen) / (currentHigh - currentLow))) * 0.5 : prevVqiT;
            vqiTList.Add(vqiT);

            var vqi = Math.Abs(vqiT) * ((currentClose - prevClose + (currentClose - currentOpen)) * 0.5);
            vqiList.Add(vqi);

            vqiSum += vqi;
            vqiSumList.Add(vqiSum);
        }

        var vqiSumFastSmaList = GetMovingAverageList(stockData, maType, fastLength, vqiSumList);
        var vqiSumSlowSmaList = GetMovingAverageList(stockData, maType, slowLength, vqiSumList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var vqiSumValue = vqiSumList[i];
            var vqiSumFastSma = vqiSumFastSmaList[i];
            var prevVqiSum = i >= 1 ? vqiSumList[i - 1] : 0;
            var prevVqiSumFastSma = i >= 1 ? vqiSumFastSmaList[i - 1] : 0;      

            var signal = GetCompareSignal(vqiSumValue - vqiSumFastSma, prevVqiSum - prevVqiSumFastSma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Vqi", vqiSumList },
            { "FastSignal", vqiSumFastSmaList },
            { "SlowSignal", vqiSumSlowSmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(vqiSumList);
        stockData.IndicatorName = IndicatorName.VolatilityQualityIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Sigma Spikes
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateSigmaSpikes(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 20)
    {
        List<double> retList = new(stockData.Count);
        List<double> sigmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var ret = prevValue != 0 ? (currentValue / prevValue) - 1 : 0;
            retList.Add(ret);
        }

        stockData.SetCustomValues(retList);
        var stdList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var prevStd = i >= 1 ? stdList[i - 1] : 0;
            var ret = retList[i];

            var sigma = prevStd != 0 ? ret / prevStd : 0;
            sigmaList.Add(sigma);
        }

        var ssList = GetMovingAverageList(stockData, maType, length, sigmaList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var ss = ssList[i];
            var prevSs = i >= 1 ? ssList[i - 1] : 0;

            var signal = GetCompareSignal(ss, prevSs);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ss", sigmaList },
            { "Signal", ssList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(sigmaList);
        stockData.IndicatorName = IndicatorName.SigmaSpikes;

        return stockData;
    }


    /// <summary>
    /// Calculates the Surface Roughness Estimator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateSurfaceRoughnessEstimator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 100)
    {
        List<double> aList = new(stockData.Count);
        List<double> corrList = new(stockData.Count);
        List<double> tempList = new(stockData.Count);
        List<double> prevList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingCorrelation corrWindow = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var emaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            tempList.Add(currentValue);

            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            prevList.Add(prevValue);

            corrWindow.Add(prevValue, currentValue);
            var corr = corrWindow.R(length);
            corr = IsValueNullOrInfinity(corr) ? 0 : corr;
            corrList.Add(corr);
            var a = 1 - (((double)corr + 1) / 2);
            aList.Add(a);
        }

        var aEmaList = GetMovingAverageList(stockData, maType, length, aList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var corr = corrList[i];
            var currentValue = inputList[i];
            var ema = emaList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevEma = i >= 1 ? emaList[i - 1] : 0;
            var a = aList[i];
            var aEma = aEmaList[i];

            var signal = GetVolatilitySignal(currentValue - ema, prevValue - prevEma, a, aEma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Sre", aList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(aList);
        stockData.IndicatorName = IndicatorName.SurfaceRoughnessEstimator;

        return stockData;
    }
}

