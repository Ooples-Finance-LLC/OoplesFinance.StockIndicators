
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the index of the force.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateForceIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14)
    {
        List<double> rawForceList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentVolume = volumeList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var rawForce = MinPastValues(i, 1, currentValue - prevValue) * currentVolume;
            rawForceList.Add(rawForce);
        }

        var forceList = GetMovingAverageList(stockData, maType, length, rawForceList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var force = forceList[i];
            var prevForce1 = i >= 1 ? forceList[i - 1] : 0;
            var prevForce2 = i >= 2 ? forceList[i - 2] : 0;

            var signal = GetCompareSignal(force - prevForce1, prevForce1 - prevForce2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Fi", forceList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(forceList);
        stockData.IndicatorName = IndicatorName.ForceIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Klinger Volume Oscillator
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="fastLength">Length of the fast.</param>
    /// <param name="slowLength">Length of the slow.</param>
    /// <param name="signalLength">Length of the signal.</param>
    /// <returns></returns>
    public static StockData CalculateKlingerVolumeOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int fastLength = 34, int slowLength = 55, int signalLength = 13)
    {
        var count = stockData.Count;
        List<double> kvoList = new(count);
        List<double> trendList = new(count);
        List<double> dmList = new(count);
        List<double> cmList = new(count);
        List<double> vfList = new(count);
        List<double> kvoHistoList = new(count);
        List<Signal>? signalsList = CreateSignalsList(stockData, count);
        var (inputList, highList, lowList, _, volumeList) = GetInputValuesList(stockData);

        for (var i = 0; i < count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentValue = inputList[i];
            var currentVolume = volumeList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var mom = MinPastValues(i, 1, currentValue - prevValue);

            var prevTrend = i >= 1 ? trendList[i - 1] : 0;
            var trend = mom > 0 ? 1 : mom < 0 ? -1 : prevTrend;
            trendList.Add(trend);

            var prevDm = i >= 1 ? dmList[i - 1] : 0;
            var dm = currentHigh - currentLow;
            dmList.Add(dm);

            var prevCm = i >= 1 ? cmList[i - 1] : 0;
            var cm = trend == prevTrend ? prevCm + dm : prevDm + dm;
            cmList.Add(cm);

            var temp = cm != 0 ? Math.Abs((2 * (dm / cm)) - 1) : -1;
            var vf = currentVolume * temp * trend * 100;
            vfList.Add(vf);
        }

        var ema34List = GetMovingAverageList(stockData, maType, fastLength, vfList);
        var ema55List = GetMovingAverageList(stockData, maType, slowLength, vfList);
        for (var i = 0; i < count; i++)
        {
            var ema34 = ema34List[i];
            var ema55 = ema55List[i];

            var klingerOscillator = ema34 - ema55;
            kvoList.Add(klingerOscillator);
        }

        var kvoSignalList = GetMovingAverageList(stockData, maType, signalLength, kvoList);
        for (var i = 0; i < count; i++)
        {
            var klingerOscillator = kvoList[i];
            var koSignalLine = kvoSignalList[i];

            var prevKlingerOscillatorHistogram = i >= 1 ? kvoHistoList[i - 1] : 0;  
            var klingerOscillatorHistogram = klingerOscillator - koSignalLine;  
            kvoHistoList.Add(klingerOscillatorHistogram);

            var signal = GetCompareSignal(klingerOscillatorHistogram, prevKlingerOscillatorHistogram);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Kvo", kvoList },
            { "KvoSignal", kvoSignalList },
            { "KvoHistogram", kvoHistoList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(kvoList);
        stockData.IndicatorName = IndicatorName.KlingerVolumeOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ease Of Movement
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="divisor"></param>
    /// <returns></returns>
    public static StockData CalculateEaseOfMovement(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14,
        double divisor = 1000000)
    {
        List<double> halfRangeList = new(stockData.Count);
        List<double> midpointMoveList = new(stockData.Count);
        List<double> emvList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (_, highList, lowList, _, volumeList) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentVolume = volumeList[i];
            var prevHalfRange = i >= 1 ? halfRangeList[i - 1] : 0;
            var halfRange = (currentHigh - currentLow) * 0.5;
            halfRangeList.Add(halfRange);
            var boxRatio = currentHigh - currentLow != 0 ? currentVolume / (currentHigh - currentLow) : 0;

            var prevMidpointMove = i >= 1 ? midpointMoveList[i - 1] : 0;
            var midpointMove = halfRange - prevHalfRange;
            midpointMoveList.Add(midpointMove);

            var emv = boxRatio != 0 ? divisor * ((midpointMove - prevMidpointMove) / boxRatio) : 0;
            emvList.Add(emv);
        }

        var emvSmaList = GetMovingAverageList(stockData, maType, length, emvList);
        var emvSignalList = GetMovingAverageList(stockData, maType, length, emvSmaList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var emv = emvList[i];
            var emvSignal = emvSignalList[i];
            var prevEmv = i >= 1 ? emvList[i - 1] : 0;
            var prevEmvSignal = i >= 1 ? emvSignalList[i - 1] : 0;

            var signal = GetCompareSignal(emv - emvSignal, prevEmv - prevEmvSignal);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Eom", emvList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(emvList);
        stockData.IndicatorName = IndicatorName.EaseOfMovement;

        return stockData;
    }


    /// <summary>
    /// Calculates the Hawkeye Volume Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="inputName"></param>
    /// <param name="length"></param>
    /// <param name="divisor"></param>
    /// <returns></returns>
    public static StockData CalculateHawkeyeVolumeIndicator(this StockData stockData, InputName inputName = InputName.MedianPrice, int length = 200,
        double divisor = 3.6)
    {
        List<double> tempRangeList = new(stockData.Count);
        List<double> tempVolumeList = new(stockData.Count);
        List<double> u1List = new(stockData.Count);
        List<double> d1List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum volumeSum = new();
        RollingSum rangeSum = new();
        var (inputList, highList, lowList, _, closeList, volumeList) = GetInputValuesList(inputName, stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentValue = closeList[i];

            var currentVolume = volumeList[i];
            tempVolumeList.Add(currentVolume);
            volumeSum.Add(currentVolume);

            var range = currentHigh - currentLow;
            tempRangeList.Add(range);
            rangeSum.Add(range);

            var volumeSma = volumeSum.Average(length);
            var rangeSma = rangeSum.Average(length);
            var prevHigh = i >= 1 ? highList[i - 1] : 0;
            var prevLow = i >= 1 ? lowList[i - 1] : 0;
            var prevMidpoint = i >= 1 ? inputList[i - 1] : 0;
            var prevVolume = i >= 1 ? volumeList[i - 1] : 0;

            var u1 = divisor != 0 ? prevMidpoint + ((prevHigh - prevLow) / divisor) : prevMidpoint;
            u1List.Add(u1);

            var d1 = divisor != 0 ? prevMidpoint - ((prevHigh - prevLow) / divisor) : prevMidpoint;
            d1List.Add(d1);

            var rEnabled1 = range > rangeSma && currentValue < d1 && currentVolume > volumeSma;
            var rEnabled2 = currentValue < prevMidpoint;
            var rEnabled = rEnabled1 || rEnabled2;

            var gEnabled1 = currentValue > prevMidpoint;
            var gEnabled2 = range > rangeSma && currentValue > u1 && currentVolume > volumeSma;
            var gEnabled3 = currentHigh > prevHigh && range < rangeSma / 1.5 && currentVolume < volumeSma;
            var gEnabled4 = currentLow < prevLow && range < rangeSma / 1.5 && currentVolume > volumeSma;
            var gEnabled = gEnabled1 || gEnabled2 || gEnabled3 || gEnabled4;

            var grEnabled1 = range > rangeSma && currentValue > d1 && currentValue < u1 && currentVolume > volumeSma && currentVolume < volumeSma * 1.5 && currentVolume > prevVolume;
            var grEnabled2 = range < rangeSma / 1.5 && currentVolume < volumeSma / 1.5;
            var grEnabled3 = currentValue > d1 && currentValue < u1;
            var grEnabled = grEnabled1 || grEnabled2 || grEnabled3;

            var signal = GetConditionSignal(gEnabled, rEnabled);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Up", u1List },
            { "Dn", d1List }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.HawkeyeVolumeIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Herrick Payoff Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="inputName"></param>
    /// <param name="pointValue"></param>
    /// <returns></returns>
    public static StockData CalculateHerrickPayoffIndex(this StockData stockData, InputName inputName = InputName.MedianPrice, double pointValue = 100)
    {
        List<double> kList = new(stockData.Count);
        List<double> hpicList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, openList, closeList, volumeList) = GetInputValuesList(inputName, stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentClose = closeList[i];
            var currentOpen = openList[i];
            var currentValue = inputList[i];
            var currentVolume = volumeList[i];
            var prevClose = i >= 1 ? closeList[i - 1] : 0;
            var prevOpen = i >= 1 ? openList[i - 1] : 0;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevK = i >= 1 ? kList[i - 1] : 0;
            var absDiff = Math.Abs(currentClose - prevClose);
            var g = Math.Min(currentOpen, prevOpen);
            var k = MinPastValues(i, 1, currentValue - prevValue) * pointValue * currentVolume;
            var temp = g != 0 ? currentValue < prevValue ? 1 - (absDiff / 2 / g) : 1 + (absDiff / 2 / g) : 1;

            k *= temp;
            kList.Add(k);

            var prevHpic = i >= 1 ? hpicList[i - 1] : 0;
            var hpic = prevK + (k - prevK);
            hpicList.Add(hpic);

            var signal = GetCompareSignal(hpic, prevHpic);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Hpi", hpicList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(hpicList);
        stockData.IndicatorName = IndicatorName.HerrickPayoffIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Finite Volume Elements
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="factor"></param>
    /// <returns></returns>
    public static StockData CalculateFiniteVolumeElements(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 22, double factor = 0.3)
    {
        List<double> fveList = new(stockData.Count);
        List<double> bullList = new(stockData.Count);
        List<double> bearList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);

        var medianPriceList = CalculateMedianPrice(stockData).CustomValuesList;
        var typicalPriceList = CalculateTypicalPrice(stockData).CustomValuesList;
        var volumeSmaList = GetMovingAverageList(stockData, maType, length, volumeList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var medianPrice = medianPriceList[i];
            var typicalPrice = typicalPriceList[i];
            var prevTypicalPrice = i >= 1 ? typicalPriceList[i - 1] : 0;
            var volumeSma = volumeSmaList[i];
            var volume = volumeList[i];
            var close = inputList[i];
            var nmf = close - medianPrice + typicalPrice - prevTypicalPrice;
            var nvlm = nmf > factor * close / 100 ? volume : nmf < -factor * close / 100 ? -volume : 0;

            var prevFve = i >= 1 ? fveList[i - 1] : 0;
            var prevFve2 = i >= 2 ? fveList[i - 2] : 0;
            var fve = volumeSma != 0 && length != 0 ? prevFve + (nvlm / volumeSma / length * 100) : prevFve;
            fveList.Add(fve);

            var prevBullSlope = i >= 1 ? bullList[i - 1] : 0;
            var bullSlope = fve - Math.Max(prevFve, prevFve2);
            bullList.Add(bullSlope);

            var prevBearSlope = i >= 1 ? bearList[i - 1] : 0;
            var bearSlope = fve - Math.Min(prevFve, prevFve2);
            bearList.Add(bearSlope);

            var signal = GetBullishBearishSignal(bullSlope, prevBullSlope, bearSlope, prevBearSlope);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Fve", fveList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(fveList);
        stockData.IndicatorName = IndicatorName.FiniteVolumeElements;

        return stockData;
    }


    /// <summary>
    /// Calculates the Freedom of Movement
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateFreedomOfMovement(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 60)
    {
        List<double> aMoveList = new(stockData.Count);
        List<double> vBymList = new(stockData.Count);
        List<double> theFomList = new(stockData.Count);
        List<double> avfList = new(stockData.Count);
        List<double> dplList = new(stockData.Count);
        List<double> tempList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingMinMax aMoveWindow = new(length);
        RollingMinMax relVolWindow = new(length);
        RollingSum vBymSum = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var relVolList = CalculateRelativeVolumeIndicator(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var currentRelVol = relVolList[i];
            tempList.Add(currentRelVol);
            relVolWindow.Add(currentRelVol);

            var aMove = prevValue != 0 ? Math.Abs(MinPastValues(i, 1, currentValue - prevValue) / prevValue) : 0;
            aMoveList.Add(aMove);
            aMoveWindow.Add(aMove);

            var aMoveMax = aMoveWindow.Max;
            var aMoveMin = aMoveWindow.Min;
            var theMove = aMoveMax - aMoveMin != 0 ? (1 + ((aMove - aMoveMin) * (10 - 1))) / (aMoveMax - aMoveMin) : 0;
            var relVolMax = relVolWindow.Max;
            var relVolMin = relVolWindow.Min;
            var theVol = relVolMax - relVolMin != 0 ? (1 + ((currentRelVol - relVolMin) * (10 - 1))) / (relVolMax - relVolMin) : 0;

            var vBym = theMove != 0 ? theVol / theMove : 0;
            vBymList.Add(vBym);
            vBymSum.Add(vBym);

            var avf = vBymSum.Average(length);
            avfList.Add(avf);
        }

        stockData.SetCustomValues(vBymList);
        var sdfList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var vBym = vBymList[i];
            var avf = avfList[i];
            var sdf = sdfList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var theFom = sdf != 0 ? (vBym - avf) / sdf : 0;
            theFomList.Add(theFom);

            var prevDpl = i >= 1 ? dplList[i - 1] : 0;
            var dpl = theFom >= 2 ? prevValue : i >= 1 ? prevDpl : currentValue;
            dplList.Add(dpl);

            var signal = GetCompareSignal(currentValue - dpl, prevValue - prevDpl);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Fom", theFomList },
            { "Dpl", dplList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(theFomList);
        stockData.IndicatorName = IndicatorName.FreedomOfMovement;

        return stockData;
    }

}

