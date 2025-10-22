//     Ooples Finance Stock Indicator Library
//     https://ooples.github.io/OoplesFinance.StockIndicators/
//
//     Copyright © Franklin Moormann, 2020-2022
//     cheatcountry@gmail.com
//
//     This library is free software and it uses the Apache 2.0 license
//     so if you are going to re-use or modify my code then I just ask
//     that you include my copyright info and my contact info in a comment

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
        List<double> macdList = new();
        List<double> macdHistogramList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var fastEmaList = GetMovingAverageList(stockData, movingAvgType, fastLength, inputList);
        var slowEmaList = GetMovingAverageList(stockData, movingAvgType, slowLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var fastEma = fastEmaList[i];
            var slowEma = slowEmaList[i];

            var macd = fastEma - slowEma;
            macdList.AddRounded(macd);
        }

        var macdSignalLineList = GetMovingAverageList(stockData, movingAvgType, signalLength, macdList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var macd = macdList[i];
            var macdSignalLine = macdSignalLineList[i];

            var prevMacdHistogram = macdHistogramList.LastOrDefault();
            var macdHistogram = macd - macdSignalLine;
            macdHistogramList.AddRounded(macdHistogram);

            var signal = GetCompareSignal(macdHistogram, prevMacdHistogram);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Macd", macdList },
            { "Signal", macdSignalLineList },
            { "Histogram", macdHistogramList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = macdList;
        stockData.IndicatorName = IndicatorName.MovingAverageConvergenceDivergence;

        return stockData;
    }

    /// <summary>
    /// Calculates the 4 Moving Average Convergence Divergence (Macd)
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="length4"></param>
    /// <param name="length5"></param>
    /// <param name="length6"></param>
    /// <param name="blueMult"></param>
    /// <param name="yellowMult"></param>
    /// <returns></returns>
    public static StockData Calculate4MovingAverageConvergenceDivergence(this StockData stockData,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 5, int length2 = 8, int length3 = 10, int length4 = 17,
        int length5 = 14, int length6 = 16, double blueMult = 4.3, double yellowMult = 1.4)
    {
        List<double> macd1List = new();
        List<double> macd2List = new();
        List<double> macd3List = new();
        List<double> macd4List = new();
        List<double> macd2HistogramList = new();
        List<double> macd4HistogramList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var ema5List = GetMovingAverageList(stockData, maType, length1, inputList);
        var ema8List = GetMovingAverageList(stockData, maType, length2, inputList);
        var ema10List = GetMovingAverageList(stockData, maType, length3, inputList);
        var ema17List = GetMovingAverageList(stockData, maType, length4, inputList);
        var ema14List = GetMovingAverageList(stockData, maType, length5, inputList);
        var ema16List = GetMovingAverageList(stockData, maType, length6, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var ema5 = ema5List[i];
            var ema8 = ema8List[i];
            var ema10 = ema10List[i];
            var ema14 = ema14List[i];
            var ema16 = ema16List[i];
            var ema17 = ema17List[i];

            var macd1 = ema17 - ema14;
            macd1List.AddRounded(macd1);

            var macd2 = ema17 - ema8;
            macd2List.AddRounded(macd2);

            var macd3 = ema10 - ema16;
            macd3List.AddRounded(macd3);

            var macd4 = ema5 - ema10;
            macd4List.AddRounded(macd4);
        }

        var macd1SignalLineList = GetMovingAverageList(stockData, maType, length1, macd1List);
        var macd2SignalLineList = GetMovingAverageList(stockData, maType, length1, macd2List); //-V3056
        var macd3SignalLineList = GetMovingAverageList(stockData, maType, length1, macd3List);
        var macd4SignalLineList = GetMovingAverageList(stockData, maType, length1, macd4List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var macd1 = macd1List[i];
            var macd1SignalLine = macd1SignalLineList[i];
            var macd2 = macd2List[i];
            var macd2SignalLine = macd2SignalLineList[i];
            var macd3 = macd3List[i];
            var macd3SignalLine = macd3SignalLineList[i];
            var macd4 = macd4List[i];
            var macd4SignalLine = macd4SignalLineList[i];
            var macd1Histogram = macd1 - macd1SignalLine;
            var macdBlue = blueMult * macd1Histogram;

            var prevMacd2Histogram = macd2HistogramList.LastOrDefault();
            var macd2Histogram = macd2 - macd2SignalLine;
            macd2HistogramList.AddRounded(macd2Histogram);

            var macd3Histogram = macd3 - macd3SignalLine;
            var macdYellow = yellowMult * macd3Histogram;

            var prevMacd4Histogram = macd4HistogramList.LastOrDefault();
            var macd4Histogram = macd4 - macd4SignalLine;
            macd4HistogramList.AddRounded(macd4Histogram);

            var signal = GetCompareSignal(macd4Histogram - macd2Histogram, prevMacd4Histogram - prevMacd2Histogram);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Macd1", macd4List },
            { "Signal1", macd4SignalLineList },
            { "Histogram1", macd4HistogramList },
            { "Macd2", macd2List },
            { "Signal2", macd2SignalLineList },
            { "Histogram2", macd2HistogramList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName._4MovingAverageConvergenceDivergence;

        return stockData;
    }

    /// <summary>
    /// Calculates the Impulse Moving Average Convergence Divergence
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="inputName"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculateImpulseMovingAverageConvergenceDivergence(this StockData stockData, InputName inputName = InputName.TypicalPrice,
        MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int length = 34, int signalLength = 9)
    {
        List<double> macdList = new();
        List<double> macdSignalLineList = new();
        List<double> macdHistogramList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _, _) = GetInputValuesList(inputName, stockData);

        var typicalPriceZeroLagEmaList = GetMovingAverageList(stockData, MovingAvgType.ZeroLagExponentialMovingAverage, length, inputList);
        var wellesWilderHighMovingAvgList = GetMovingAverageList(stockData, maType, length, highList);
        var wellesWilderLowMovingAvgList = GetMovingAverageList(stockData, maType, length, lowList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var hi = wellesWilderHighMovingAvgList[i];
            var lo = wellesWilderLowMovingAvgList[i];
            var mi = typicalPriceZeroLagEmaList[i];

            var macd = mi > hi ? mi - hi : mi < lo ? mi - lo : 0;
            macdList.AddRounded(macd);

            var macdSignalLine = macdList.TakeLastExt(signalLength).Average();
            macdSignalLineList.AddRounded(macdSignalLine);

            var prevMacdHistogram = macdHistogramList.LastOrDefault();
            var macdHistogram = macd - macdSignalLine;
            macdHistogramList.AddRounded(macdHistogram);

            var signal = GetCompareSignal(macdHistogram, prevMacdHistogram);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Macd", macdList },
            { "Signal", macdSignalLineList },
            { "Histogram", macdHistogramList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = macdList;
        stockData.IndicatorName = IndicatorName.ImpulseMovingAverageConvergenceDivergence;

        return stockData;
    }

    /// <summary>
    /// Calculates the Kase Convergence Divergence
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <returns></returns>
    public static StockData CalculateKaseConvergenceDivergence(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length1 = 30, int length2 = 3, int length3 = 8)
    {
        List<double> kcdList = new();
        List<Signal> signalsList = new();

        var pkList = CalculateKasePeakOscillatorV1(stockData, length1, length2).OutputValues["Pk"];
        var pkSignalList = GetMovingAverageList(stockData, maType, length3, pkList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var pk = pkList[i];
            var pkSma = pkSignalList[i];

            var prevKcd = kcdList.LastOrDefault();
            var kcd = pk - pkSma;
            kcdList.AddRounded(kcd);

            var signal = GetCompareSignal(kcd, prevKcd);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Kcd", kcdList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = kcdList;
        stockData.IndicatorName = IndicatorName.KaseConvergenceDivergence;

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
        List<double> macdList = new();
        List<double> macdHistogramList = new();
        List<double> diff12List = new();
        List<double> diff26List = new();
        List<double> i1List = new();
        List<double> i2List = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var emaList = GetMovingAverageList(stockData, maType, fastLength, inputList);
        var ema26List = GetMovingAverageList(stockData, maType, slowLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var ema12 = emaList[i];
            var ema26 = ema26List[i];

            var diff12 = currentValue - ema12;
            diff12List.AddRounded(diff12);

            var diff26 = currentValue - ema26;
            diff26List.AddRounded(diff26);
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
            i1List.AddRounded(i1);

            var i2 = ema26 + diff26Ema;
            i2List.AddRounded(i2);

            var macd = i1 - i2;
            macdList.AddRounded(macd);
        }

        var macdSignalLineList = GetMovingAverageList(stockData, maType, signalLength, macdList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var macd = macdList[i];
            var macdSignalLine = macdSignalLineList[i];

            var prevMacdHistogram = macdHistogramList.LastOrDefault();
            var macdHistogram = macd - macdSignalLine;
            macdHistogramList.AddRounded(macdHistogram);

            var signal = GetCompareSignal(macdHistogram, prevMacdHistogram);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Macd", macdList },
            { "I1", i1List },
            { "I2", i2List }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = macdList;
        stockData.IndicatorName = IndicatorName.MovingAverageConvergenceDivergenceLeader;

        return stockData;
    }

    /// <summary>
    /// Calculates the TFS Mbo Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculateTFSMboIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int fastLength = 25, int slowLength = 200, int signalLength = 18)
    {
        List<double> tfsMobList = new();
        List<double> tfsMobHistogramList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var mob1List = GetMovingAverageList(stockData, maType, fastLength, inputList);
        var mob2List = GetMovingAverageList(stockData, maType, slowLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var mob1 = mob1List[i];
            var mob2 = mob2List[i];

            var tfsMob = mob1 - mob2;
            tfsMobList.AddRounded(tfsMob);
        }

        var tfsMobSignalLineList = GetMovingAverageList(stockData, maType, signalLength, tfsMobList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var tfsMob = tfsMobList[i];
            var tfsMobSignalLine = tfsMobSignalLineList[i];

            var prevTfsMobHistogram = tfsMobHistogramList.LastOrDefault();
            var tfsMobHistogram = tfsMob - tfsMobSignalLine;
            tfsMobHistogramList.AddRounded(tfsMobHistogram);

            var signal = GetCompareSignal(tfsMobHistogram, prevTfsMobHistogram);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "TfsMob", tfsMobList },
            { "Signal", tfsMobSignalLineList },
            { "Histogram", tfsMobHistogramList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = tfsMobList;
        stockData.IndicatorName = IndicatorName.TFSMboIndicator;

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
        List<double> macztList = new();
        List<double> l0List = new();
        List<double> l1List = new();
        List<double> l2List = new();
        List<double> l3List = new();
        List<double> maczList = new();
        List<double> histList = new();
        List<Signal> signalsList = new();
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
            macztList.AddRounded(maczt);

            var prevL0 = i >= 1 ? l0List.LastOrDefault() : maczt;
            var l0 = ((1 - gamma) * maczt) + (gamma * prevL0);
            l0List.AddRounded(l0);

            var prevL1 = i >= 1 ? l1List.LastOrDefault() : maczt;
            var l1 = (-1 * gamma * l0) + prevL0 + (gamma * prevL1);
            l1List.AddRounded(l1);

            var prevL2 = i >= 1 ? l2List.LastOrDefault() : maczt;
            var l2 = (-1 * gamma * l1) + prevL1 + (gamma * prevL2);
            l2List.AddRounded(l2);

            var prevL3 = i >= 1 ? l3List.LastOrDefault() : maczt;
            var l3 = (-1 * gamma * l2) + prevL2 + (gamma * prevL3);
            l3List.AddRounded(l3);

            var macz = (l0 + (2 * l1) + (2 * l2) + l3) / 6;
            maczList.AddRounded(macz);
        }

        var maczSignalList = GetMovingAverageList(stockData, maType, signalLength, maczList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var macz = maczList[i];
            var maczSignal = maczSignalList[i];

            var prevHist = histList.LastOrDefault();
            var hist = macz - maczSignal;
            histList.AddRounded(hist);

            var signal = GetCompareSignal(hist, prevHist);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Macz", maczList },
            { "Signal", maczSignalList },
            { "Histogram", histList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = maczList;
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
        List<double> maczList = new();
        List<double> histList = new();
        List<Signal> signalsList = new();
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
            maczList.AddRounded(macz);
        }

        var maczSignalList = GetMovingAverageList(stockData, maType, signalLength, maczList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var macz = maczList[i];
            var maczSignal = maczSignalList[i];

            var prevHist = histList.LastOrDefault();
            var hist = macz - maczSignal;
            histList.AddRounded(hist);

            var signal = GetCompareSignal(hist, prevHist);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Macz", maczList },
            { "Signal", maczSignalList },
            { "Histogram", histList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = maczList;
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
        List<double> macdList = new();
        List<double> macdHistogramList = new();
        List<double> macdMirrorList = new();
        List<double> macdMirrorHistogramList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, openList, _) = GetInputValuesList(stockData);

        var emaOpenList = GetMovingAverageList(stockData, maType, length, openList);
        var emaCloseList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var mao = emaOpenList[i];
            var mac = emaCloseList[i];

            var macd = mac - mao;
            macdList.AddRounded(macd);

            var macdMirror = mao - mac;
            macdMirrorList.AddRounded(macdMirror);
        }

        var macdSignalLineList = GetMovingAverageList(stockData, maType, signalLength, macdList);
        var macdMirrorSignalLineList = GetMovingAverageList(stockData, maType, signalLength, macdMirrorList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var macd = macdList[i];
            var macdMirror = macdMirrorList[i];
            var macdSignalLine = macdSignalLineList[i];
            var macdMirrorSignalLine = macdMirrorSignalLineList[i];

            var prevMacdHistogram = macdHistogramList.LastOrDefault();
            var macdHistogram = macd - macdSignalLine;
            macdHistogramList.AddRounded(macdHistogram);

            var macdMirrorHistogram = macdMirror - macdMirrorSignalLine;
            macdMirrorHistogramList.AddRounded(macdMirrorHistogram);

            var signal = GetCompareSignal(macdHistogram, prevMacdHistogram);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Macd", macdList },
            { "Signal", macdSignalLineList },
            { "Histogram", macdHistogramList },
            { "MirrorMacd", macdMirrorList },
            { "MirrorSignal", macdMirrorSignalLineList },
            { "MirrorHistogram", macdMirrorHistogramList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = macdList;
        stockData.IndicatorName = IndicatorName.MirroredMovingAverageConvergenceDivergence;

        return stockData;
    }

    /// <summary>
    /// Calculates the DiNapoli Moving Average Convergence Divergence
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="lc"></param>
    /// <param name="sc"></param>
    /// <param name="sp"></param>
    /// <returns></returns>
    public static StockData CalculateDiNapoliMovingAverageConvergenceDivergence(this StockData stockData, double lc = 17.5185, double sc = 8.3896, 
        double sp = 9.0503)
    {
        List<double> fsList = new();
        List<double> ssList = new();
        List<double> rList = new();
        List<double> sList = new();
        List<double> hList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var scAlpha = 2 / (1 + sc);
        var lcAlpha = 2 / (1 + lc);
        var spAlpha = 2 / (1 + sp);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];

            var prevFs = fsList.LastOrDefault();
            var fs = prevFs + (scAlpha * (currentValue - prevFs));
            fsList.AddRounded(fs);

            var prevSs = ssList.LastOrDefault();
            var ss = prevSs + (lcAlpha * (currentValue - prevSs));
            ssList.AddRounded(ss);

            var r = fs - ss;
            rList.AddRounded(r);

            var prevS = sList.LastOrDefault();
            var s = prevS + (spAlpha * (r - prevS));
            sList.AddRounded(s);

            var prevH = hList.LastOrDefault();
            var h = r - s;
            hList.AddRounded(h);

            var signal = GetCompareSignal(h, prevH);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "FastS", fsList },
            { "SlowS", ssList },
            { "Macd", rList },
            { "Signal", sList },
            { "Histogram", hList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = rList;
        stockData.IndicatorName = IndicatorName.DiNapoliMovingAverageConvergenceDivergence;

        return stockData;
    }
  
    /// <summary>
    /// Calculates the Stochastic Moving Average Convergence Divergence Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculateStochasticMovingAverageConvergenceDivergenceOscillator(this StockData stockData, 
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 45, int fastLength = 12, int slowLength = 26, int signalLength = 9)
    {
        List<double> macdStochasticHistogramList = new();
        List<double> fastStochasticList = new();
        List<double> slowStochasticList = new();
        List<double> macdStochasticList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        var fastEmaList = GetMovingAverageList(stockData, maType, fastLength, inputList);
        var slowEmaList = GetMovingAverageList(stockData, maType, slowLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var fastEma = fastEmaList[i];
            var slowEma = slowEmaList[i];
            var hh = highestList[i];
            var ll = lowestList[i];
            var range = hh - ll;

            var fastStochastic = range != 0 ? (fastEma - ll) / range : 0;
            fastStochasticList.AddRounded(fastStochastic);

            var slowStochastic = range != 0 ? (slowEma - ll) / range : 0;
            slowStochasticList.AddRounded(slowStochastic);

            var macdStochastic = 10 * (fastStochastic - slowStochastic);
            macdStochasticList.AddRounded(macdStochastic);
        }

        var macdStochasticSignalLineList = GetMovingAverageList(stockData, maType, signalLength, macdStochasticList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var macdStochastic = macdStochasticList[i];
            var macdStochasticSignalLine = macdStochasticSignalLineList[i];

            var prevMacdHistogram = macdStochasticHistogramList.LastOrDefault();
            var macdHistogram = macdStochastic - macdStochasticSignalLine;
            macdStochasticHistogramList.AddRounded(macdHistogram);

            var signal = GetCompareSignal(macdHistogram, prevMacdHistogram);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Macd", macdStochasticList },
            { "Signal", macdStochasticSignalLineList },
            { "Histogram", macdStochasticHistogramList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = macdStochasticList;
        stockData.IndicatorName = IndicatorName.StochasticMovingAverageConvergenceDivergenceOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ergodic Moving Average Convergence Divergence
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <returns></returns>
    public static StockData CalculateErgodicMovingAverageConvergenceDivergence(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 32, int length2 = 5, int length3 = 5)
    {
        List<double> macdList = new();
        List<double> macdHistogramList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var period1EmaList = GetMovingAverageList(stockData, maType, length1, inputList);
        var period2EmaList = GetMovingAverageList(stockData, maType, length2, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var ema1 = period1EmaList[i];
            var ema2 = period2EmaList[i];

            var macd = ema1 - ema2;
            macdList.AddRounded(macd);
        }

        var macdSignalLineList = GetMovingAverageList(stockData, maType, length3, macdList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var macd = macdList[i];
            var macdSignalLine = macdSignalLineList[i];

            var prevMacdHistogram = macdHistogramList.LastOrDefault();
            var macdHistogram = macd - macdSignalLine;
            macdHistogramList.AddRounded(macdHistogram);

            var signal = GetCompareSignal(macdHistogram, prevMacdHistogram);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Macd", macdList },
            { "Signal", macdSignalLineList },
            { "Histogram", macdHistogramList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = macdList;
        stockData.IndicatorName = IndicatorName.ErgodicMovingAverageConvergenceDivergence;

        return stockData;
    }
}
