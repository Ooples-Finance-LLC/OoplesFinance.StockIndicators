
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
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
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

            var ppo = RoundedPercentageChange.Of(fastEma, slowEma);
            ppoList.Add(ppo);
        }

        var signalCount = ppoList.FindIndex(double.IsInfinity);
        if (signalCount < 0) signalCount = ppoList.Count;
        var ppoSignalList = GetMovingAverageList(stockData, maType, signalLength,
            signalCount == ppoList.Count ? ppoList : ppoList.GetRange(0, signalCount));
        for (var i = signalCount; i < ppoList.Count; i++) ppoSignalList.Add(double.NaN);
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
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculatePercentageVolumeOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int fastLength = 12, int slowLength = 26, int signalLength = 9)
    {
        List<double> pvoList = new(stockData.Count);
        List<double> pvoHistogramList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        // Volume is what this oscillator reads when nothing is passed in, and a chained series
        // replaces it - the same precedence every indicator follows, and what the streaming state's
        // selector already does. It used to read raw volumes whatever was chained in front of it.
        var (inputList, _, _, _, volumes) = GetInputValuesList(stockData);
        var volumeList = stockData.ChainedValues is { Count: > 0 } ? inputList : volumes;

        var fastEmaList = GetMovingAverageList(stockData, maType, fastLength, volumeList);
        var slowEmaList = GetMovingAverageList(stockData, maType, slowLength, volumeList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var fastEma = fastEmaList[i];
            var slowEma = slowEmaList[i];

            var pvo = RoundedPercentageChange.Of(fastEma, slowEma);
            pvoList.Add(pvo);
        }

        var finiteSignalInput = FiniteSignalInput.Create(pvoList, out var finiteCount);
        var pvoSignalList = GetMovingAverageList(stockData, maType, signalLength, finiteSignalInput);
        for (var i = finiteCount; i < pvoSignalList.Count; i++) pvoSignalList[i] = double.NaN;
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
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculatePercentagePriceOscillatorLeader(this StockData stockData,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int fastLength = 12, int slowLength = 26, int signalLength = 9)
    {
        List<double> ppoList = new(stockData.Count);
        List<double> ppoHistogramList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var macdLeaderList = CalculateMovingAverageConvergenceDivergenceLeader(stockData, maType, fastLength, slowLength, signalLength);
        var i1List = macdLeaderList.ChainedOutputs["I1"];
        var i2List = macdLeaderList.ChainedOutputs["I2"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var i1 = i1List[i];
            var i2 = i2List[i];

            var ppo = RoundedFractionalEma.Percentage(i1, i2);
            ppoList.Add(ppo);
        }

        var finite = FiniteSignalInput.Create(ppoList, out var count);
        var ppoSignalLineList = maType == MovingAvgType.SimpleMovingAverage ? BollingerArithmetic.Mean(finite, signalLength)
            : GetMovingAverageList(stockData, maType, signalLength, finite);
        for (var i = count; i < ppoSignalLineList.Count; i++) ppoSignalLineList[i] = double.NaN;
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
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateMirroredPercentagePriceOscillator(this StockData stockData, 
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 20, int signalLength = 9)
    {
        List<double> ppoList = new(stockData.Count);
        List<double> ppoHistogramList = new(stockData.Count);
        List<double> ppoMirrorList = new(stockData.Count);
        List<double> ppoMirrorHistogramList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, openList, _) = GetInputValuesList(stockData);

        var emaOpenList = maType == MovingAvgType.SimpleMovingAverage ? BollingerArithmetic.Mean(openList, length) : GetMovingAverageList(stockData, maType, length, openList);
        var emaCloseList = maType == MovingAvgType.SimpleMovingAverage ? BollingerArithmetic.Mean(inputList, length) : GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var mao = emaOpenList[i];
            var mac = emaCloseList[i];

            var ppo = RoundedPercentageChange.Of(mac, mao);
            ppoList.Add(ppo);

            var ppoMirror = RoundedPercentageChange.Of(mao, mac);
            ppoMirrorList.Add(ppoMirror);
        }

        var ppoInput = FiniteSignalInput.Create(ppoList, out var ppoFiniteCount);
        var ppoSignalLineList = maType == MovingAvgType.SimpleMovingAverage ? BollingerArithmetic.Mean(ppoInput, signalLength) : GetMovingAverageList(stockData, maType, signalLength, ppoInput);
        for (var i = ppoFiniteCount; i < ppoSignalLineList.Count; i++) ppoSignalLineList[i] = double.NaN;
        var ppoMirrorInput = FiniteSignalInput.Create(ppoMirrorList, out var ppoMirrorFiniteCount);
        var ppoMirrorSignalLineList = maType == MovingAvgType.SimpleMovingAverage ? BollingerArithmetic.Mean(ppoMirrorInput, signalLength) : GetMovingAverageList(stockData, maType, signalLength, ppoMirrorInput);
        for (var i = ppoMirrorFiniteCount; i < ppoMirrorSignalLineList.Count; i++) ppoMirrorSignalLineList[i] = double.NaN;
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

