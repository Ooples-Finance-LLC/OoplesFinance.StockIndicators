
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
        List<double> ppoList = new(stockData.Count);
        List<double> ppoHistogramList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
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

            var prevPpoHistogram = GetLastOrDefault(ppoHistogramList);
            var ppoHistogram = ppo - ppoSignalLine;
            ppoHistogramList.Add(ppoHistogram);

            var signal = GetCompareSignal(ppoHistogram, prevPpoHistogram);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ppo", ppoList },
            { "Signal", ppoSignalList },
            { "Histogram", ppoHistogramList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(ppoList);
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
        List<double> pvoList = new(stockData.Count);
        List<double> pvoHistogramList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
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

            var prevPvoHistogram = GetLastOrDefault(pvoHistogramList);
            var pvoHistogram = pvo - pvoSignalLine;
            pvoHistogramList.Add(pvoHistogram);

            var signal = GetCompareSignal(pvoHistogram, prevPvoHistogram);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Pvo", pvoList },
            { "Signal", pvoSignalList },
            { "Histogram", pvoHistogramList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(pvoList);
        stockData.IndicatorName = IndicatorName.PercentageVolumeOscillator;

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
        List<double> ppoList = new(stockData.Count);
        List<double> ppoHistogramList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

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

            var prevPpoHistogram = GetLastOrDefault(ppoHistogramList);
            var ppoHistogram = ppo - ppoSignalLine;
            ppoHistogramList.Add(ppoHistogram);

            var signal = GetCompareSignal(ppoHistogram, prevPpoHistogram);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ppo", ppoList },
            { "Signal", ppoSignalLineList },
            { "Histogram", ppoHistogramList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(ppoList);
        stockData.IndicatorName = IndicatorName.PercentagePriceOscillatorLeader;

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
        List<double> ppoList = new(stockData.Count);
        List<double> ppoHistogramList = new(stockData.Count);
        List<double> ppoMirrorList = new(stockData.Count);
        List<double> ppoMirrorHistogramList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
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

            var prevPpoHistogram = GetLastOrDefault(ppoHistogramList);
            var ppoHistogram = ppo - ppoSignalLine;
            ppoHistogramList.Add(ppoHistogram);

            var ppoMirrorHistogram = ppoMirror - ppoMirrorSignalLine;
            ppoMirrorHistogramList.Add(ppoMirrorHistogram);

            var signal = GetCompareSignal(ppoHistogram, prevPpoHistogram);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ppo", ppoList },
            { "Signal", ppoSignalLineList },
            { "Histogram", ppoHistogramList },
            { "MirrorPpo", ppoMirrorList },
            { "MirrorSignal", ppoMirrorSignalLineList },
            { "MirrorHistogram", ppoMirrorHistogramList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(ppoList);
        stockData.IndicatorName = IndicatorName.MirroredPercentagePriceOscillator;

        return stockData;
    }

}

