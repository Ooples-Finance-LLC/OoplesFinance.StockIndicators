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
    /// Calculates the percentage price oscillator.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="fastLength">Length of the fast.</param>
    /// <param name="slowLength">Length of the slow.</param>
    /// <param name="signalLength">Length of the signal.</param>
    /// <returns></returns>
    public static StockData CalculatePercentagePriceOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int fastLength = 12, int slowLength = 26, int signalLength = 9)
    {
        List<double> ppoList = new();
        List<double> ppoHistogramList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var fastEmaList = GetMovingAverageList(stockData, maType, fastLength, inputList);
        var slowEmaList = GetMovingAverageList(stockData, maType, slowLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var fastEma = fastEmaList[i];
            var slowEma = slowEmaList[i];

            var ppo = slowEma != 0 ? 100 * (fastEma - slowEma) / slowEma : 0;
            ppoList.Add(ppo);
        }

        var ppoSignalList = GetMovingAverageList(stockData, maType, signalLength, ppoList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var ppo = ppoList[i];
            var ppoSignalLine = ppoSignalList[i];

            var prevPpoHistogram = ppoHistogramList.LastOrDefault();
            var ppoHistogram = ppo - ppoSignalLine;
            ppoHistogramList.Add(ppoHistogram);

            var signal = GetCompareSignal(ppoHistogram, prevPpoHistogram);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ppo", ppoList },
            { "Signal", ppoSignalList },
            { "Histogram", ppoHistogramList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = ppoList;
        stockData.IndicatorName = IndicatorName.PercentagePriceOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the percentage volume oscillator.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="fastLength">Length of the fast.</param>
    /// <param name="slowLength">Length of the slow.</param>
    /// <param name="signalLength">Length of the signal.</param>
    /// <returns></returns>
    public static StockData CalculatePercentageVolumeOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int fastLength = 12, int slowLength = 26, int signalLength = 9)
    {
        List<double> pvoList = new();
        List<double> pvoHistogramList = new();
        List<Signal> signalsList = new();
        var (_, _, _, _, volumeList) = GetInputValuesList(stockData);

        var fastEmaList = GetMovingAverageList(stockData, maType, fastLength, volumeList);
        var slowEmaList = GetMovingAverageList(stockData, maType, slowLength, volumeList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var fastEma = fastEmaList[i];
            var slowEma = slowEmaList[i];

            var pvo = slowEma != 0 ? 100 * (fastEma - slowEma) / slowEma : 0;
            pvoList.Add(pvo);
        }

        var pvoSignalList = GetMovingAverageList(stockData, maType, signalLength, pvoList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var pvo = pvoList[i];
            var pvoSignalLine = pvoSignalList[i];

            var prevPvoHistogram = pvoHistogramList.LastOrDefault();
            var pvoHistogram = pvo - pvoSignalLine;
            pvoHistogramList.Add(pvoHistogram);

            var signal = GetCompareSignal(pvoHistogram, prevPvoHistogram);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Pvo", pvoList },
            { "Signal", pvoSignalList },
            { "Histogram", pvoHistogramList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = pvoList;
        stockData.IndicatorName = IndicatorName.PercentageVolumeOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the 4 Percentage Price Oscillator
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
    public static StockData Calculate4PercentagePriceOscillator(this StockData stockData,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 5, int length2 = 8, int length3 = 10, int length4 = 17,
        int length5 = 14, int length6 = 16, double blueMult = 4.3, double yellowMult = 1.4)
    {
        List<double> ppo1List = new();
        List<double> ppo2List = new();
        List<double> ppo3List = new();
        List<double> ppo4List = new();
        List<double> ppo2HistogramList = new();
        List<double> ppo4HistogramList = new();
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
            var macd2 = ema17 - ema8;
            var macd3 = ema10 - ema16;
            var macd4 = ema5 - ema10;

            var ppo1 = ema14 != 0 ? macd1 / ema14 * 100 : 0;
            ppo1List.Add(ppo1);

            var ppo2 = ema8 != 0 ? macd2 / ema8 * 100 : 0;
            ppo2List.Add(ppo2);

            var ppo3 = ema16 != 0 ? macd3 / ema16 * 100 : 0;
            ppo3List.Add(ppo3);

            var ppo4 = ema10 != 0 ? macd4 / ema10 * 100 : 0;
            ppo4List.Add(ppo4);
        }

        var ppo1SignalLineList = GetMovingAverageList(stockData, maType, length1, ppo1List);
        var ppo2SignalLineList = GetMovingAverageList(stockData, maType, length1, ppo2List); //-V3056
        var ppo3SignalLineList = GetMovingAverageList(stockData, maType, length1, ppo3List);
        var ppo4SignalLineList = GetMovingAverageList(stockData, maType, length1, ppo4List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var ppo1 = ppo1List[i];
            var ppo1SignalLine = ppo1SignalLineList[i];
            var ppo2 = ppo2List[i];
            var ppo2SignalLine = ppo2SignalLineList[i];
            var ppo3 = ppo3List[i];
            var ppo3SignalLine = ppo3SignalLineList[i];
            var ppo4 = ppo4List[i];
            var ppo4SignalLine = ppo4SignalLineList[i];
            var ppo1Histogram = ppo1 - ppo1SignalLine;
            var ppoBlue = blueMult * ppo1Histogram;

            var prevPpo2Histogram = ppo2HistogramList.LastOrDefault();
            var ppo2Histogram = ppo2 - ppo2SignalLine;
            ppo2HistogramList.Add(ppo2Histogram);

            var ppo3Histogram = ppo3 - ppo3SignalLine;
            var ppoYellow = yellowMult * ppo3Histogram;

            var prevPpo4Histogram = ppo4HistogramList.LastOrDefault();
            var ppo4Histogram = ppo4 - ppo4SignalLine;
            ppo4HistogramList.Add(ppo4Histogram);

            var maxPpo = Math.Max(ppoBlue, Math.Max(ppoYellow, Math.Max(ppo2Histogram, ppo4Histogram)));
            var minPpo = Math.Min(ppoBlue, Math.Min(ppoYellow, Math.Min(ppo2Histogram, ppo4Histogram)));
            var currentPpo = (ppoBlue + ppoYellow + ppo2Histogram + ppo4Histogram) / 4;
            var ppoStochastic = maxPpo - minPpo != 0 ? MinOrMax((currentPpo - minPpo) / (maxPpo - minPpo) * 100, 100, 0) : 0;

            var signal = GetCompareSignal(ppo4Histogram - ppo2Histogram, prevPpo4Histogram - prevPpo2Histogram);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ppo1", ppo4List },
            { "Signal1", ppo4SignalLineList },
            { "Histogram1", ppo4HistogramList },
            { "Ppo2", ppo2List },
            { "Signal2", ppo2SignalLineList },
            { "Histogram2", ppo2HistogramList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName._4PercentagePriceOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Impulse Percentage Price Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="inputName"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculateImpulsePercentagePriceOscillator(this StockData stockData, InputName inputName = InputName.TypicalPrice,
        MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int length = 34, int signalLength = 9)
    {
        List<double> ppoList = new();
        List<double> ppoSignalLineList = new();
        List<double> ppoHistogramList = new();
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

            var ppo = mi > hi && hi != 0 ? macd / hi * 100 : mi < lo && lo != 0 ? macd / lo * 100 : 0;
            ppoList.Add(ppo);

            var ppoSignalLine = ppoList.TakeLastExt(signalLength).Average();
            ppoSignalLineList.Add(ppoSignalLine);

            var prevPpoHistogram = ppoHistogramList.LastOrDefault();
            var ppoHistogram = ppo - ppoSignalLine;
            ppoHistogramList.Add(ppoHistogram);

            var signal = GetCompareSignal(ppoHistogram, prevPpoHistogram);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ppo", ppoList },
            { "Signal", ppoSignalLineList },
            { "Histogram", ppoHistogramList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = ppoList;
        stockData.IndicatorName = IndicatorName.ImpulsePercentagePriceOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Percentage Price Oscillator Leader
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculatePercentagePriceOscillatorLeader(this StockData stockData,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int fastLength = 12, int slowLength = 26, int signalLength = 9)
    {
        List<double> ppoList = new();
        List<double> ppoHistogramList = new();
        List<Signal> signalsList = new();

        var macdLeaderList = CalculateMovingAverageConvergenceDivergenceLeader(stockData, maType, fastLength, slowLength, signalLength);
        var i1List = macdLeaderList.OutputValues["I1"];
        var i2List = macdLeaderList.OutputValues["I2"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var i1 = i1List[i];
            var i2 = i2List[i];
            var macd = i1 - i2;

            var ppo = i2 != 0 ? macd / i2 * 100 : 0;
            ppoList.Add(ppo);
        }

        var ppoSignalLineList = GetMovingAverageList(stockData, maType, signalLength, ppoList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var ppo = ppoList[i];
            var ppoSignalLine = ppoSignalLineList[i];

            var prevPpoHistogram = ppoHistogramList.LastOrDefault();
            var ppoHistogram = ppo - ppoSignalLine;
            ppoHistogramList.Add(ppoHistogram);

            var signal = GetCompareSignal(ppoHistogram, prevPpoHistogram);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ppo", ppoList },
            { "Signal", ppoSignalLineList },
            { "Histogram", ppoHistogramList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = ppoList;
        stockData.IndicatorName = IndicatorName.PercentagePriceOscillatorLeader;

        return stockData;
    }

    /// <summary>
    /// Calculates the TFS Mbo Percentage Price Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculateTFSMboPercentagePriceOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int fastLength = 25, int slowLength = 200, int signalLength = 18)
    {
        List<double> ppoList = new();
        List<double> ppoHistogramList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var mob1List = GetMovingAverageList(stockData, maType, fastLength, inputList);
        var mob2List = GetMovingAverageList(stockData, maType, slowLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var mob1 = mob1List[i];
            var mob2 = mob2List[i];
            var tfsMob = mob1 - mob2;

            var ppo = mob2 != 0 ? tfsMob / mob2 * 100 : 0;
            ppoList.Add(ppo);
        }

        var ppoSignalLineList = GetMovingAverageList(stockData, maType, signalLength, ppoList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var ppo = ppoList[i];
            var ppoSignalLine = ppoSignalLineList[i];

            var prevPpoHistogram = ppoHistogramList.LastOrDefault();
            var ppoHistogram = ppo - ppoSignalLine;
            ppoHistogramList.Add(ppoHistogram);

            var signal = GetCompareSignal(ppoHistogram, prevPpoHistogram);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ppo", ppoList },
            { "Signal", ppoSignalLineList },
            { "Histogram", ppoHistogramList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = ppoList;
        stockData.IndicatorName = IndicatorName.TFSMboPercentagePriceOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Mirrored Percentage Price Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculateMirroredPercentagePriceOscillator(this StockData stockData, 
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 20, int signalLength = 9)
    {
        List<double> ppoList = new();
        List<double> ppoHistogramList = new();
        List<double> ppoMirrorList = new();
        List<double> ppoMirrorHistogramList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, openList, _) = GetInputValuesList(stockData);

        var emaOpenList = GetMovingAverageList(stockData, maType, length, openList);
        var emaCloseList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var mao = emaOpenList[i];
            var mac = emaCloseList[i];
            var macd = mac - mao;
            var macdMirror = mao - mac;

            var ppo = mao != 0 ? macd / mao * 100 : 0;
            ppoList.Add(ppo);

            var ppoMirror = mac != 0 ? macdMirror / mac * 100 : 0;
            ppoMirrorList.Add(ppoMirror);
        }

        var ppoSignalLineList = GetMovingAverageList(stockData, maType, signalLength, ppoList);
        var ppoMirrorSignalLineList = GetMovingAverageList(stockData, maType, signalLength, ppoMirrorList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var ppo = ppoList[i];
            var ppoSignalLine = ppoSignalLineList[i];
            var ppoMirror = ppoMirrorList[i];
            var ppoMirrorSignalLine = ppoMirrorSignalLineList[i];

            var prevPpoHistogram = ppoHistogramList.LastOrDefault();
            var ppoHistogram = ppo - ppoSignalLine;
            ppoHistogramList.Add(ppoHistogram);

            var ppoMirrorHistogram = ppoMirror - ppoMirrorSignalLine;
            ppoMirrorHistogramList.Add(ppoMirrorHistogram);

            var signal = GetCompareSignal(ppoHistogram, prevPpoHistogram);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ppo", ppoList },
            { "Signal", ppoSignalLineList },
            { "Histogram", ppoHistogramList },
            { "MirrorPpo", ppoMirrorList },
            { "MirrorSignal", ppoMirrorSignalLineList },
            { "MirrorHistogram", ppoMirrorHistogramList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = ppoList;
        stockData.IndicatorName = IndicatorName.MirroredPercentagePriceOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the DiNapoli Percentage Price Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="lc"></param>
    /// <param name="sc"></param>
    /// <param name="sp"></param>
    /// <returns></returns>
    public static StockData CalculateDiNapoliPercentagePriceOscillator(this StockData stockData, double lc = 17.5185, double sc = 8.3896, 
        double sp = 9.0503)
    {
        List<double> ppoList = new();
        List<double> sList = new();
        List<double> hList = new();
        List<Signal> signalsList = new();

        var dinapoliMacdList = CalculateDiNapoliMovingAverageConvergenceDivergence(stockData, lc, sc, sp);
        var ssList = dinapoliMacdList.OutputValues["SlowS"];
        var rList = dinapoliMacdList.OutputValues["Macd"];

        var spAlpha = 2 / (1 + sp);

        for (var i = 0; i < stockData.Count; i++)
        {
            var ss = ssList[i];
            var r = rList[i];

            var ppo = ss != 0 ? 100 * r / ss : 0;
            ppoList.Add(ppo);

            var prevS = sList.LastOrDefault();
            var s = prevS + (spAlpha * (ppo - prevS));
            sList.Add(s);

            var prevH = hList.LastOrDefault();
            var h = ppo - s;
            hList.Add(h);

            var signal = GetCompareSignal(h, prevH);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ppo", ppoList },
            { "Signal", sList },
            { "Histogram", hList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = ppoList;
        stockData.IndicatorName = IndicatorName.DiNapoliPercentagePriceOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ergodic Percentage Price Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <returns></returns>
    public static StockData CalculateErgodicPercentagePriceOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 32, int length2 = 5, int length3 = 5)
    {
        List<double> ppoList = new();
        List<double> ppoHistogramList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var period1EmaList = GetMovingAverageList(stockData, maType, length1, inputList);
        var period2EmaList = GetMovingAverageList(stockData, maType, length2, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var ema1 = period1EmaList[i];
            var ema2 = period2EmaList[i];
            var macd = ema1 - ema2;

            var ppo = ema2 != 0 ? macd / ema2 * 100 : 0;
            ppoList.Add(ppo);
        }

        var ppoSignalLineList = GetMovingAverageList(stockData, maType, length3, ppoList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var ppo = ppoList[i];
            var ppoSignalLine = ppoSignalLineList[i];

            var prevPpoHistogram = ppoHistogramList.LastOrDefault();
            var ppoHistogram = ppo - ppoSignalLine;
            ppoHistogramList.Add(ppoHistogram);

            var signal = GetCompareSignal(ppoHistogram, prevPpoHistogram);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ppo", ppoList },
            { "Signal", ppoSignalLineList },
            { "Histogram", ppoHistogramList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = ppoList;
        stockData.IndicatorName = IndicatorName.ErgodicPercentagePriceOscillator;

        return stockData;
    }
}
