
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Money Flow Index
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="inputName">Name of the input.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateMoneyFlowIndex(this StockData stockData, InputName inputName = InputName.TypicalPrice, int length = 14)
    {
        List<double> mfiList = new(stockData.Count);
        List<double> posMoneyFlowList = new(stockData.Count);
        List<double> negMoneyFlowList = new(stockData.Count);
        var posMoneyFlowSumWindow = new RollingSum();
        var negMoneyFlowSumWindow = new RollingSum();
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _, volumeList) = GetInputValuesList(inputName, stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentVolume = volumeList[i];
            var typicalPrice = inputList[i];
            var prevTypicalPrice = i >= 1 ? inputList[i - 1] : 0;
            var prevMfi1 = i >= 1 ? mfiList[i - 1] : 0;
            var prevMfi2 = i >= 2 ? mfiList[i - 2] : 0;
            var rawMoneyFlow = typicalPrice * currentVolume;

            var posMoneyFlow = i >= 1 && typicalPrice > prevTypicalPrice ? rawMoneyFlow : 0;
            posMoneyFlowList.Add(posMoneyFlow);
            posMoneyFlowSumWindow.Add(posMoneyFlow);

            var negMoneyFlow = i >= 1 && typicalPrice < prevTypicalPrice ? rawMoneyFlow : 0;
            negMoneyFlowList.Add(negMoneyFlow);
            negMoneyFlowSumWindow.Add(negMoneyFlow);

            var posMoneyFlowTotal = posMoneyFlowSumWindow.Sum(length);
            var negMoneyFlowTotal = negMoneyFlowSumWindow.Sum(length);
            var mfiRatio = negMoneyFlowTotal != 0 ? posMoneyFlowTotal / negMoneyFlowTotal : 0;

            var mfi = negMoneyFlowTotal == 0 ? 100 : posMoneyFlowTotal == 0 ? 0 : MinOrMax(100 - (100 / (1 + mfiRatio)), 100, 0);
            mfiList.Add(mfi);

            var signal = GetRsiSignal(mfi - prevMfi1, prevMfi1 - prevMfi2, mfi, prevMfi1, 80, 20);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Mfi", mfiList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(mfiList);
        stockData.IndicatorName = IndicatorName.MoneyFlowIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the on balance volume.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateOnBalanceVolume(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 20)  
    {
        var count = stockData.Count;
        List<double> obvList = new(count);
        List<Signal>? signalsList = CreateSignalsList(stockData, count);
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);   

        for (var i = 0; i < count; i++)
        {
            var currentValue = inputList[i];
            var currentVolume = volumeList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevObv = i >= 1 ? obvList[i - 1] : 0;
            var obv = currentValue > prevValue ? prevObv + currentVolume : currentValue < prevValue ? prevObv - currentVolume : prevObv;
            obvList.Add(obv);
        }

        var obvSignalList = GetMovingAverageList(stockData, maType, length, obvList);
        for (var i = 0; i < count; i++)
        {
            var obv = obvList[i];
            var prevObv = i >= 1 ? obvList[i - 1] : 0;
            var obvSig = obvSignalList[i];
            var prevObvSig = i >= 1 ? obvSignalList[i - 1] : 0;

            var signal = GetCompareSignal(obv - obvSig, prevObv - prevObvSig);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Obv", obvList },
            { "ObvSignal", obvSignalList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(obvList);
        stockData.IndicatorName = IndicatorName.OnBalanceVolume;

        return stockData;
    }


    /// <summary>
    /// Calculates the index of the negative volume.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <param name="initialValue">The initial Nvi value</param>
    /// <returns></returns>
    public static StockData CalculateNegativeVolumeIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length = 255, int initialValue = 1000)
    {
        List<double> nviList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentClose = inputList[i];
            var currentVolume = volumeList[i];
            var prevClose = i >= 1 ? inputList[i - 1] : 0;
            var prevVolume = i >= 1 ? volumeList[i - 1] : 0;
            var prevNvi = i >= 1 ? nviList[i - 1] : initialValue;
            var pctChg = CalculatePercentChange(currentClose, prevClose);

            var nvi = currentVolume >= prevVolume ? prevNvi : prevNvi + pctChg;
            nviList.Add(nvi);
        }

        var nviSignalList = GetMovingAverageList(stockData, maType, length, nviList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var nvi = nviList[i];
            var prevNvi = i >= 1 ? nviList[i - 1] : 0;
            var nviSignal = nviSignalList[i];
            var prevNviSignal = i >= 1 ? nviSignalList[i - 1] : 0;

            var signal = GetCompareSignal(nvi - nviSignal, prevNvi - prevNviSignal);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Nvi", nviList },
            { "NviSignal", nviSignalList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(nviList);
        stockData.IndicatorName = IndicatorName.NegativeVolumeIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the index of the positive volume.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <param name="initialValue">The initial Pvi value</param>
    /// <returns></returns>
    public static StockData CalculatePositiveVolumeIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length = 255, int initialValue = 1000)
    {
        List<double> pviList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentClose = inputList[i];
            var currentVolume = volumeList[i];
            var prevClose = i >= 1 ? inputList[i - 1] : 0;
            var prevVolume = i >= 1 ? volumeList[i - 1] : 0;
            var prevPvi = i >= 1 ? pviList[i - 1] : initialValue;
            var pctChg = CalculatePercentChange(currentClose, prevClose);

            var pvi = currentVolume <= prevVolume ? prevPvi : prevPvi + pctChg;
            pviList.Add(pvi);
        }

        var pviSignalList = GetMovingAverageList(stockData, maType, length, pviList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var pvi = pviList[i];
            var prevPvi = i >= 1 ? pviList[i - 1] : 0;
            var pviSignal = pviSignalList[i];
            var prevPviSignal = i >= 1 ? pviSignalList[i - 1] : 0;

            var signal = GetCompareSignal(pvi - pviSignal, prevPvi - prevPviSignal);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Pvi", pviList },
            { "PviSignal", pviSignalList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(pviList);
        stockData.IndicatorName = IndicatorName.PositiveVolumeIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the On Balance Volume Modified
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculateOnBalanceVolumeModified(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 7, int length2 = 10)
    {
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var obvList = CalculateOnBalanceVolume(stockData, maType, length1).CustomValuesList;
        var obvmList = GetMovingAverageList(stockData, maType, length1, obvList);
        var sigList = GetMovingAverageList(stockData, maType, length2, obvmList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var obvm = obvmList[i];
            var sig = sigList[i];
            var prevObvm = i >= 1 ? obvmList[i - 1] : 0;
            var prevSig = i >= 1 ? sigList[i - 1] : 0;

            var signal = GetCompareSignal(obvm - sig, prevObvm - prevSig);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Obvm", obvmList },
            { "Signal", sigList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(obvmList);
        stockData.IndicatorName = IndicatorName.OnBalanceVolumeModified;

        return stockData;
    }


    /// <summary>
    /// Calculates the On Balance Volume Reflex
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculateOnBalanceVolumeReflex(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 4, int signalLength = 14)
    {
        List<double> ovrList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentVolume = volumeList[i];
            var prevValue = i >= length ? inputList[i - length] : 0;

            var prevOvr = i >= 1 ? ovrList[i - 1] : 0;
            var ovr = currentValue > prevValue ? prevOvr + currentVolume : currentValue < prevValue ? prevOvr - currentVolume : prevOvr;
            ovrList.Add(ovr);
        }

        var ovrSmaList = GetMovingAverageList(stockData, maType, signalLength, ovrList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var ovr = ovrList[i];
            var ovrEma = ovrSmaList[i];
            var prevOvr = i >= 1 ? ovrList[i - 1] : 0;
            var prevOvrEma = i >= 1 ? ovrSmaList[i - 1] : 0;

            var signal = GetCompareSignal(ovr - ovrEma, prevOvr - prevOvrEma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Obvr", ovrList },
            { "Signal", ovrSmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(ovrList);
        stockData.IndicatorName = IndicatorName.OnBalanceVolumeReflex;

        return stockData;
    }


    /// <summary>
    /// Calculates the On Balance Volume Disparity Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="signalLength"></param>
    /// <param name="top"></param>
    /// <param name="bottom"></param>
    /// <returns></returns>
    public static StockData CalculateOnBalanceVolumeDisparityIndicator(this StockData stockData,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 33, int signalLength = 4, double top = 1.1, double bottom = 0.9)
    {
        List<double> obvdiList = new(stockData.Count);
        List<double> bscList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var obvList = CalculateOnBalanceVolume(stockData, maType, length).CustomValuesList;
        var obvSmaList = GetMovingAverageList(stockData, maType, length, obvList);
        var smaList = GetMovingAverageList(stockData, maType, length, inputList);
        var stdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        stockData.SetCustomValues(obvList);
        var obvStdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var sma = smaList[i];
            var stdDev = stdDevList[i];
            var obvSma = obvSmaList[i];
            var obvStdDev = obvStdDevList[i];
            var aTop = currentValue - (sma - (2 * stdDev));
            var aBot = currentValue + (2 * stdDev) - (sma - (2 * stdDev));
            var obv = obvList[i];
            var a = aBot != 0 ? aTop / aBot : 0;
            var bTop = obv - (obvSma - (2 * obvStdDev));
            var bBot = obvSma + (2 * obvStdDev) - (obvSma - (2 * obvStdDev));
            var b = bBot != 0 ? bTop / bBot : 0;

            var obvdi = 1 + b != 0 ? (1 + a) / (1 + b) : 0;
            obvdiList.Add(obvdi);
        }

        var obvdiEmaList = GetMovingAverageList(stockData, maType, signalLength, obvdiList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var obvdi = obvdiList[i];
            var obvdiEma = obvdiEmaList[i];
            var prevObvdi = i >= 1 ? obvdiList[i - 1] : 0;

            var prevBsc = i >= 1 ? bscList[i - 1] : 0;
            var bsc = (prevObvdi < bottom && obvdi > bottom) || obvdi > obvdiEma ? 1 : (prevObvdi > top && obvdi < top) ||
                obvdi < bottom ? -1 : prevBsc;
            bscList.Add(bsc);

            var signal = GetCompareSignal(bsc, prevBsc);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Obvdi", obvdiList },
            { "Signal", obvdiEmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(obvdiList);
        stockData.IndicatorName = IndicatorName.OnBalanceVolumeDisparityIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Negative Volume Disparity Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="signalLength"></param>
    /// <param name="top"></param>
    /// <param name="bottom"></param>
    /// <returns></returns>
    public static StockData CalculateNegativeVolumeDisparityIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 33, int signalLength = 4, double top = 1.1, double bottom = 0.9)
    {
        List<double> nvdiList = new(stockData.Count);
        List<double> bscList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var nviList = CalculateNegativeVolumeIndex(stockData, maType, length).CustomValuesList;
        var nviSmaList = GetMovingAverageList(stockData, maType, length, nviList);
        var smaList = GetMovingAverageList(stockData, maType, length, inputList);
        var stdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        stockData.SetCustomValues(nviList);
        var nviStdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var sma = smaList[i];
            var stdDev = stdDevList[i];
            var nviSma = nviSmaList[i];
            var nviStdDev = nviStdDevList[i];
            var aTop = currentValue - (sma - (2 * stdDev));
            var aBot = (currentValue + (2 * stdDev)) - (sma - (2 * stdDev));
            var nvi = nviList[i];
            var a = aBot != 0 ? aTop / aBot : 0;
            var bTop = nvi - (nviSma - (2 * nviStdDev));
            var bBot = (nviSma + (2 * nviStdDev)) - (nviSma - (2 * nviStdDev));
            var b = bBot != 0 ? bTop / bBot : 0;

            var nvdi = 1 + b != 0 ? (1 + a) / (1 + b) : 0;
            nvdiList.Add(nvdi);
        }

        var nvdiEmaList = GetMovingAverageList(stockData, maType, signalLength, nvdiList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var nvdi = nvdiList[i];
            var nvdiEma = nvdiEmaList[i];
            var prevNvdi = i >= 1 ? nvdiList[i - 1] : 0;

            var prevBsc = i >= 1 ? bscList[i - 1] : 0;
            var bsc = (prevNvdi < bottom && nvdi > bottom) || nvdi > nvdiEma ? 1 : (prevNvdi > top && nvdi < top) ||
                nvdi < bottom ? -1 : prevBsc;
            bscList.Add(bsc);

            var signal = GetCompareSignal(bsc, prevBsc);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Nvdi", nvdiList },
            { "Signal", nvdiEmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(nvdiList);
        stockData.IndicatorName = IndicatorName.NegativeVolumeDisparityIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Relative Volume Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateRelativeVolumeIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 60)
    {
        List<double> relVolList = new(stockData.Count);
        List<double> dplList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);

        var smaVolumeList = GetMovingAverageList(stockData, maType, length, volumeList);
        stockData.SetCustomValues(volumeList);
        var stdDevVolumeList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentVolume = volumeList[i];
            var currentValue = inputList[i];
            var av = smaVolumeList[i];
            var sd = stdDevVolumeList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var relVol = sd != 0 ? (currentVolume - av) / sd : 0;
            relVolList.Add(relVol);

            var prevDpl = i >= 1 ? dplList[i - 1] : 0;
            var dpl = relVol >= 2 ? prevValue : i >= 1 ? prevDpl : currentValue;
            dplList.Add(dpl);

            var signal = GetCompareSignal(currentValue - dpl, prevValue - prevDpl);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Rvi", relVolList },
            { "Dpl", dplList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(relVolList);
        stockData.IndicatorName = IndicatorName.RelativeVolumeIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Price Volume Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculatePriceVolumeOscillator(this StockData stockData, int length1 = 50, int length2 = 14)
    {
        List<double> aList = new(stockData.Count);
        List<double> bList = new(stockData.Count);
        List<double> absAList = new(stockData.Count);
        List<double> absBList = new(stockData.Count);
        List<double> oscAList = new(stockData.Count);
        List<double> oscBList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum aSumWindow = new();
        RollingSum bSumWindow = new();
        RollingSum absASumWindow = new();
        RollingSum absBSumWindow = new();
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentVolume = volumeList[i];
            var prevValue = i >= length1 ? inputList[i - length1] : 0;
            var prevVolume = i >= length2 ? volumeList[i - length2] : 0;

            var a = MinPastValues(i, length1, currentValue - prevValue);
            aList.Add(a);
            aSumWindow.Add(a);

            var b = MinPastValues(i, length2, currentVolume - prevVolume);
            bList.Add(b);
            bSumWindow.Add(b);

            var absA = Math.Abs(a);
            absAList.Add(absA);
            absASumWindow.Add(absA);

            var absB = Math.Abs(b);
            absBList.Add(absB);
            absBSumWindow.Add(absB);

            var aSum = aSumWindow.Sum(length1);
            var bSum = bSumWindow.Sum(length2);
            var absASum = absASumWindow.Sum(length1);
            var absBSum = absBSumWindow.Sum(length2);

            var oscA = absASum != 0 ? aSum / absASum : 0;
            oscAList.Add(oscA);

            var oscB = absBSum != 0 ? bSum / absBSum : 0;
            oscBList.Add(oscB);

            var signal = GetConditionSignal(oscA > 0 && oscB > 0, oscA < 0 && oscB > 0);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Po", oscAList },
            { "Vo", oscBList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.PriceVolumeOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Price Volume Rank
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <returns></returns>
    public static StockData CalculatePriceVolumeRank(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int fastLength = 5, int slowLength = 10)
    {
        List<double> pvrList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentVolume = volumeList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevVolume = i >= 1 ? volumeList[i - 1] : 0;

            double pvr = currentValue > prevValue && currentVolume > prevVolume ? 1 : currentValue > prevValue && currentVolume <= prevVolume ? 2 :
                currentValue <= prevValue && currentVolume <= prevVolume ? 3 : 4;
            pvrList.Add(pvr);
        }

        var pvrFastSmaList = GetMovingAverageList(stockData, maType, fastLength, pvrList);
        var pvrSlowSmaList = GetMovingAverageList(stockData, maType, slowLength, pvrList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var fastSma = pvrFastSmaList[i];
            var slowSma = pvrSlowSmaList[i];
            var prevFastSma = i >= 1 ? pvrFastSmaList[i - 1] : 0;
            var prevSlowSma = i >= 1 ? pvrSlowSmaList[i - 1] : 0;

            var signal = GetCompareSignal(fastSma - slowSma, prevFastSma - prevSlowSma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Pvr", pvrList },
            { "SlowSignal", pvrSlowSmaList },
            { "FastSignal", pvrFastSmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(pvrList);
        stockData.IndicatorName = IndicatorName.PriceVolumeRank;

        return stockData;
    }


    /// <summary>
    /// Calculates the Market Facilitation Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <returns></returns>
    public static StockData CalculateMarketFacilitationIndex(this StockData stockData)
    {
        List<double> mfiList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (_, highList, lowList, _, volumeList) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentVolume = volumeList[i];
            var prevVolume = i >= 1 ? volumeList[i - 1] : 0;

            var prevMfi = i >= 1 ? mfiList[i - 1] : 0;
            var mfi = currentVolume != 0 ? (currentHigh - currentLow) / currentVolume : 0;
            mfiList.Add(mfi);

            var mfiDiff = mfi - prevMfi;
            var volDiff = currentVolume - prevVolume;

            var signal = GetConditionSignal(mfiDiff > 0, volDiff > 0);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Mi", mfiList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(mfiList);
        stockData.IndicatorName = IndicatorName.MarketFacilitationIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Multi Vote On Balance Volume
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateMultiVoteOnBalanceVolume(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 14)
    {
        List<double> mvoList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, volumeList) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentClose = inputList[i];
            var currentLow = lowList[i];
            var currentVolume = volumeList[i] / 1000000;
            var prevClose = i >= 1 ? inputList[i - 1] : 0;
            var prevHigh = i >= 1 ? highList[i - 1] : 0;
            var prevLow = i >= 1 ? lowList[i - 1] : 0;
            double highVote = currentHigh > prevHigh ? 1 : currentHigh < prevHigh ? -1 : 0;
            double lowVote = currentLow > prevLow ? 1 : currentLow < prevLow ? -1 : 0;
            double closeVote = currentClose > prevClose ? 1 : currentClose < prevClose ? -1 : 0;
            var totalVotes = highVote + lowVote + closeVote;

            var prevMvo = i >= 1 ? mvoList[i - 1] : 0;
            var mvo = prevMvo + (currentVolume * totalVotes);
            mvoList.Add(mvo);
        }

        var mvoEmaList = GetMovingAverageList(stockData, maType, length, mvoList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var mvo = mvoList[i];
            var mvoEma = mvoEmaList[i];
            var prevMvo = i >= 1 ? mvoList[i - 1] : 0;
            var prevMvoEma = i >= 1 ? mvoEmaList[i - 1] : 0;

            var signal = GetCompareSignal(mvo - mvoEma, prevMvo - prevMvoEma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Mvo", mvoList },
            { "Signal", mvoEmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(mvoList);
        stockData.IndicatorName = IndicatorName.MultiVoteOnBalanceVolume;

        return stockData;
    }
}

