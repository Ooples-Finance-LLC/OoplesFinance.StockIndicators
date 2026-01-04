
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the moving average convergence divergence.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="movingAvgType">Average type of the moving.</param>
    /// <param name="fastLength">Length of the fast.</param>
    /// <param name="slowLength">Length of the slow.</param>
    /// <param name="signalLength">Length of the signal.</param>
    /// <returns></returns>
    public static StockData CalculateMovingAverageConvergenceDivergence(this StockData stockData,
        MovingAvgType movingAvgType = MovingAvgType.ExponentialMovingAverage, int fastLength = 12, int slowLength = 26, int signalLength = 9)
    {
        List<double> macdList = new(stockData.Count);
        List<double> macdHistogramList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var fastEmaList = GetMovingAverageList(stockData, movingAvgType, fastLength, inputList);
        var slowEmaList = GetMovingAverageList(stockData, movingAvgType, slowLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var fastEma = fastEmaList[i];
            var slowEma = slowEmaList[i];

            var macd = fastEma - slowEma;
            macdList.Add(macd);
        }

        var macdSignalLineList = GetMovingAverageList(stockData, movingAvgType, signalLength, macdList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var macd = macdList[i];
            var macdSignalLine = macdSignalLineList[i];

            var prevMacdHistogram = i >= 1 ? macdHistogramList[i - 1] : 0;
            var macdHistogram = macd - macdSignalLine;
            macdHistogramList.Add(macdHistogram);

            var signal = GetCompareSignal(macdHistogram, prevMacdHistogram);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Macd", macdList },
            { "Signal", macdSignalLineList },
            { "Histogram", macdHistogramList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(macdList);
        stockData.IndicatorName = IndicatorName.MovingAverageConvergenceDivergence;

        return stockData;
    }


    /// <summary>
    /// Calculates the Moving Average Convergence Divergence Leader
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculateMovingAverageConvergenceDivergenceLeader(this StockData stockData, 
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int fastLength = 12, int slowLength = 26, int signalLength = 9)
    {
        List<double> macdList = new(stockData.Count);
        List<double> macdHistogramList = new(stockData.Count);
        List<double> diff12List = new(stockData.Count);
        List<double> diff26List = new(stockData.Count);
        List<double> i1List = new(stockData.Count);
        List<double> i2List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var emaList = GetMovingAverageList(stockData, maType, fastLength, inputList);
        var ema26List = GetMovingAverageList(stockData, maType, slowLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var ema12 = emaList[i];
            var ema26 = ema26List[i];

            var diff12 = currentValue - ema12;
            diff12List.Add(diff12);

            var diff26 = currentValue - ema26;
            diff26List.Add(diff26);
        }

        var diff12EmaList = GetMovingAverageList(stockData, maType, fastLength, diff12List);
        var diff26EmaList = GetMovingAverageList(stockData, maType, slowLength, diff26List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var ema12 = emaList[i];
            var ema26 = ema26List[i];
            var diff12Ema = diff12EmaList[i];
            var diff26Ema = diff26EmaList[i];

            var i1 = ema12 + diff12Ema;
            i1List.Add(i1);

            var i2 = ema26 + diff26Ema;
            i2List.Add(i2);

            var macd = i1 - i2;
            macdList.Add(macd);
        }

        var macdSignalLineList = GetMovingAverageList(stockData, maType, signalLength, macdList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var macd = macdList[i];
            var macdSignalLine = macdSignalLineList[i];

            var prevMacdHistogram = i >= 1 ? macdHistogramList[i - 1] : 0;
            var macdHistogram = macd - macdSignalLine;
            macdHistogramList.Add(macdHistogram);

            var signal = GetCompareSignal(macdHistogram, prevMacdHistogram);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Macd", macdList },
            { "I1", i1List },
            { "I2", i2List }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(macdList);
        stockData.IndicatorName = IndicatorName.MovingAverageConvergenceDivergenceLeader;

        return stockData;
    }


    /// <summary>
    /// Calculates the MacZ Vwap Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="signalLength"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="gamma"></param>
    /// <returns></returns>
    public static StockData CalculateMacZVwapIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int fastLength = 12, int slowLength = 25, int signalLength = 9, int length1 = 20, int length2 = 25, double gamma = 0.02)
    {
        List<double> macztList = new(stockData.Count);
        List<double> l0List = new(stockData.Count);
        List<double> l1List = new(stockData.Count);
        List<double> l2List = new(stockData.Count);
        List<double> l3List = new(stockData.Count);
        List<double> maczList = new(stockData.Count);
        List<double> histList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var stdDevList = CalculateStandardDeviationVolatility(stockData, maType, length2).CustomValuesList;
        var fastSmaList = GetMovingAverageList(stockData, maType, fastLength, inputList);
        var slowSmaList = GetMovingAverageList(stockData, maType, slowLength, inputList);
        var zScoreList = CalculateZDistanceFromVwapIndicator(stockData, length: length1).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var stdev = stdDevList[i];
            var fastMa = fastSmaList[i];
            var slowMa = slowSmaList[i];
            var zscore = zScoreList[i];

            var macd = fastMa - slowMa;
            var maczt = stdev != 0 ? zscore + (macd / stdev) : zscore;
            macztList.Add(maczt);

            var prevL0 = i >= 1 ? l0List[i - 1] : maczt;
            var l0 = ((1 - gamma) * maczt) + (gamma * prevL0);
            l0List.Add(l0);

            var prevL1 = i >= 1 ? l1List[i - 1] : maczt;
            var l1 = (-1 * gamma * l0) + prevL0 + (gamma * prevL1);
            l1List.Add(l1);

            var prevL2 = i >= 1 ? l2List[i - 1] : maczt;
            var l2 = (-1 * gamma * l1) + prevL1 + (gamma * prevL2);
            l2List.Add(l2);

            var prevL3 = i >= 1 ? l3List[i - 1] : maczt;
            var l3 = (-1 * gamma * l2) + prevL2 + (gamma * prevL3);
            l3List.Add(l3);

            var macz = (l0 + (2 * l1) + (2 * l2) + l3) / 6;
            maczList.Add(macz);
        }

        var maczSignalList = GetMovingAverageList(stockData, maType, signalLength, maczList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var macz = maczList[i];
            var maczSignal = maczSignalList[i];

            var prevHist = i >= 1 ? histList[i - 1] : 0;
            var hist = macz - maczSignal;
            histList.Add(hist);

            var signal = GetCompareSignal(hist, prevHist);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Macz", maczList },
            { "Signal", maczSignalList },
            { "Histogram", histList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(maczList);
        stockData.IndicatorName = IndicatorName.MacZVwapIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the MacZ Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="signalLength"></param>
    /// <param name="length"></param>
    /// <param name="gamma"></param>
    /// <param name="mult"></param>
    /// <returns></returns>
    public static StockData CalculateMacZIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int fastLength = 12, int slowLength = 25, int signalLength = 9, int length = 25, double gamma = 0.02, double mult = 1)
    {
        List<double> maczList = new(stockData.Count);
        List<double> histList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var stdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        var fastSmaList = GetMovingAverageList(stockData, maType, fastLength, inputList);
        var slowSmaList = GetMovingAverageList(stockData, maType, slowLength, inputList);
        var wilderMovingAvgList = GetMovingAverageList(stockData, MovingAvgType.WildersSmoothingMethod, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var stdev = stdDevList[i];
            var wima = wilderMovingAvgList[i];
            var fastMa = fastSmaList[i];
            var slowMa = slowSmaList[i];
            var zscore = stdev != 0 ? (currentValue - wima) / stdev : 0;

            var macd = fastMa - slowMa;
            var macz = stdev != 0 ? (zscore * mult) + (mult * macd / stdev) : zscore;
            maczList.Add(macz);
        }

        var maczSignalList = GetMovingAverageList(stockData, maType, signalLength, maczList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var macz = maczList[i];
            var maczSignal = maczSignalList[i];

            var prevHist = i >= 1 ? histList[i - 1] : 0;
            var hist = macz - maczSignal;
            histList.Add(hist);

            var signal = GetCompareSignal(hist, prevHist);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Macz", maczList },
            { "Signal", maczSignalList },
            { "Histogram", histList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(maczList);
        stockData.IndicatorName = IndicatorName.MacZIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Mirrored Moving Average Convergence Divergence
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculateMirroredMovingAverageConvergenceDivergence(this StockData stockData,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 20, int signalLength = 9)
    {
        List<double> macdList = new(stockData.Count);
        List<double> macdHistogramList = new(stockData.Count);
        List<double> macdMirrorList = new(stockData.Count);
        List<double> macdMirrorHistogramList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, openList, _) = GetInputValuesList(stockData);

        var emaOpenList = GetMovingAverageList(stockData, maType, length, openList);
        var emaCloseList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var mao = emaOpenList[i];
            var mac = emaCloseList[i];

            var macd = mac - mao;
            macdList.Add(macd);

            var macdMirror = mao - mac;
            macdMirrorList.Add(macdMirror);
        }

        var macdSignalLineList = GetMovingAverageList(stockData, maType, signalLength, macdList);
        var macdMirrorSignalLineList = GetMovingAverageList(stockData, maType, signalLength, macdMirrorList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var macd = macdList[i];
            var macdMirror = macdMirrorList[i];
            var macdSignalLine = macdSignalLineList[i];
            var macdMirrorSignalLine = macdMirrorSignalLineList[i];

            var prevMacdHistogram = i >= 1 ? macdHistogramList[i - 1] : 0;
            var macdHistogram = macd - macdSignalLine;
            macdHistogramList.Add(macdHistogram);

            var macdMirrorHistogram = macdMirror - macdMirrorSignalLine;
            macdMirrorHistogramList.Add(macdMirrorHistogram);

            var signal = GetCompareSignal(macdHistogram, prevMacdHistogram);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Macd", macdList },
            { "Signal", macdSignalLineList },
            { "Histogram", macdHistogramList },
            { "MirrorMacd", macdMirrorList },
            { "MirrorSignal", macdMirrorSignalLineList },
            { "MirrorHistogram", macdMirrorHistogramList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(macdList);
        stockData.IndicatorName = IndicatorName.MirroredMovingAverageConvergenceDivergence;

        return stockData;
    }

}

