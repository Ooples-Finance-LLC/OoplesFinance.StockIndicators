
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the donchian channels.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateDonchianChannels(this StockData stockData, int length = 20)
    {
        List<double> upperChannelList = new(stockData.Count);
        List<double> lowerChannelList = new(stockData.Count);
        List<double> middleChannelList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var upperChannel = highestList[i];
            upperChannelList.Add(upperChannel);

            var lowerChannel = lowestList[i];
            lowerChannelList.Add(lowerChannel);

            var prevMiddleChannel = GetLastOrDefault(middleChannelList);
            var middleChannel = (upperChannel + lowerChannel) / 2;
            middleChannelList.Add(middleChannel);

            var signal = GetCompareSignal(currentValue - middleChannel, prevValue - prevMiddleChannel);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperChannel", upperChannelList },
            { "LowerChannel", lowerChannelList },
            { "MiddleChannel", middleChannelList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.DonchianChannels;

        return stockData;
    }


    /// <summary>
    /// Calculates the Average True Range Channel
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <param name="mult">The mult.</param>
    /// <returns></returns>
    public static StockData CalculateAverageTrueRangeChannel(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 14, double mult = 2.5)
    {
        List<double> innerTopAtrChannelList = new(stockData.Count);
        List<double> innerBottomAtrChannelList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var atrList = CalculateAverageTrueRange(stockData, maType, length).CustomValuesList;
        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var atr = atrList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var sma = smaList[i];
            var prevSma = i >= 1 ? smaList[i - 1] : 0;

            var prevTopInner = GetLastOrDefault(innerTopAtrChannelList);
            var topInner = Math.Round(currentValue + (atr * mult));
            innerTopAtrChannelList.Add(topInner);

            var prevBottomInner = GetLastOrDefault(innerBottomAtrChannelList);
            var bottomInner = Math.Round(currentValue - (atr * mult));
            innerBottomAtrChannelList.Add(bottomInner);

            var signal = GetBollingerBandsSignal(currentValue - sma, prevValue - prevSma, currentValue, prevValue, topInner,
                prevTopInner, bottomInner, prevBottomInner);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", innerTopAtrChannelList },
            { "MiddleBand", smaList },
            { "LowerBand", innerBottomAtrChannelList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.AverageTrueRangeChannel;

        return stockData;
    }


    /// <summary>
    /// Calculates the Dema 2 Lines
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <returns></returns>
    public static StockData CalculateDema2Lines(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int fastLength = 10, int slowLength = 40)
    {
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var ema1List = GetMovingAverageList(stockData, maType, fastLength, inputList);
        var ema2List = GetMovingAverageList(stockData, maType, slowLength, inputList);
        var dema1List = GetMovingAverageList(stockData, maType, fastLength, ema1List);
        var dema2List = GetMovingAverageList(stockData, maType, slowLength, ema2List);

        for (var i = 0; i < stockData.Count; i++)
        {
            var dema1 = dema1List[i];
            var dema2 = dema2List[i];
            var prevDema1 = i >= 1 ? dema1List[i - 1] : 0;
            var prevDema2 = i >= 1 ? dema2List[i - 1] : 0;

            var signal = GetCompareSignal(dema1 - dema2, prevDema1 - prevDema2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Dema1", dema1List },
            { "Dema2", dema2List }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.Dema2Lines;

        return stockData;
    }


    /// <summary>
    /// Calculates the Dynamic Support and Resistance
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateDynamicSupportAndResistance(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, 
        int length = 25)
    {
        List<double> supportList = new(stockData.Count);
        List<double> resistanceList = new(stockData.Count);
        List<double> middleList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        var mult = Sqrt(length);

        var atrList = CalculateAverageTrueRange(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentAvgTrueRange = atrList[i];
            var highestHigh = highestList[i];
            var lowestLow = lowestList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var support = highestHigh - (currentAvgTrueRange * mult);
            supportList.Add(support);

            var resistance = lowestLow + (currentAvgTrueRange * mult);
            resistanceList.Add(resistance);

            var prevMiddle = GetLastOrDefault(middleList);
            var middle = (support + resistance) / 2;
            middleList.Add(middle);

            var signal = GetCompareSignal(currentValue - middle, prevValue - prevMiddle);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Support", supportList },
            { "Resistance", resistanceList },
            { "MiddleBand", middleList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.DynamicSupportAndResistance;

        return stockData;
    }


    /// <summary>
    /// Calculates the Daily Average Price Delta
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateDailyAveragePriceDelta(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 21)
    {
        List<double> topList = new(stockData.Count);
        List<double> bottomList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (_, highList, lowList, _, _) = GetInputValuesList(stockData);

        var smaHighList = GetMovingAverageList(stockData, maType, length, highList);
        var smaLowList = GetMovingAverageList(stockData, maType, length, lowList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var high = highList[i];
            var low = lowList[i];
            var highSma = smaHighList[i];
            var lowSma = smaLowList[i];
            var dapd = highSma - lowSma;

            var prevTop = GetLastOrDefault(topList);
            var top = high + dapd;
            topList.Add(top);

            var prevBottom = GetLastOrDefault(bottomList);
            var bottom = low - dapd;
            bottomList.Add(bottom);

            var signal = GetConditionSignal(high > prevTop, low < prevBottom);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", topList },
            { "LowerBand", bottomList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.DailyAveragePriceDelta;

        return stockData;
    }


    /// <summary>
    /// Calculates the D Envelope
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="devFactor"></param>
    /// <returns></returns>
    public static StockData CalculateDEnvelope(this StockData stockData, int length = 20, double devFactor = 2)
    {
        List<double> mtList = new(stockData.Count);
        List<double> utList = new(stockData.Count);
        List<double> dtList = new(stockData.Count);
        List<double> mt2List = new(stockData.Count);
        List<double> ut2List = new(stockData.Count);
        List<double> butList = new(stockData.Count);
        List<double> bltList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var alp = (double)2 / (length + 1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevMt = GetLastOrDefault(mtList);
            var mt = (alp * currentValue) + ((1 - alp) * prevMt);
            mtList.Add(mt);

            var prevUt = GetLastOrDefault(utList);
            var ut = (alp * mt) + ((1 - alp) * prevUt);
            utList.Add(ut);

            var prevDt = GetLastOrDefault(dtList);
            var dt = (2 - alp) * (mt - ut) / (1 - alp);
            dtList.Add(dt);

            var prevMt2 = GetLastOrDefault(mt2List);
            var mt2 = (alp * Math.Abs(currentValue - dt)) + ((1 - alp) * prevMt2);
            mt2List.Add(mt2);

            var prevUt2 = GetLastOrDefault(ut2List);
            var ut2 = (alp * mt2) + ((1 - alp) * prevUt2);
            ut2List.Add(ut2);

            var dt2 = (2 - alp) * (mt2 - ut2) / (1 - alp);
            var prevBut = GetLastOrDefault(butList);
            var but = dt + (devFactor * dt2);
            butList.Add(but);

            var prevBlt = GetLastOrDefault(bltList);
            var blt = dt - (devFactor * dt2);
            bltList.Add(blt);

            var signal = GetBollingerBandsSignal(currentValue - dt, prevValue - prevDt, currentValue, prevValue, but, prevBut, blt, prevBlt);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", butList },
            { "MiddleBand", dtList },
            { "LowerBand", bltList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.DEnvelope;

        return stockData;
    }

}

