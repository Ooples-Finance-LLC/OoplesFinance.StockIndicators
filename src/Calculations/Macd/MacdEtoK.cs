
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
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
        List<double> macdList = new(stockData.Count);
        List<double> macdSignalLineList = new(stockData.Count);
        List<double> macdHistogramList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum macdSum = new();
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
            macdList.Add(macd);
            macdSum.Add(macd);

            var macdSignalLine = macdSum.Average(signalLength);
            macdSignalLineList.Add(macdSignalLine);

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
        List<double> kcdList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var pkList = CalculateKasePeakOscillatorV1(stockData, length1, length2).OutputValues["Pk"];
        var pkSignalList = GetMovingAverageList(stockData, maType, length3, pkList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var pk = pkList[i];
            var pkSma = pkSignalList[i];

            var prevKcd = i >= 1 ? kcdList[i - 1] : 0;
            var kcd = pk - pkSma;
            kcdList.Add(kcd);

            var signal = GetCompareSignal(kcd, prevKcd);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Kcd", kcdList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(kcdList);
        stockData.IndicatorName = IndicatorName.KaseConvergenceDivergence;

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
        List<double> macdList = new(stockData.Count);
        List<double> macdHistogramList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var period1EmaList = GetMovingAverageList(stockData, maType, length1, inputList);
        var period2EmaList = GetMovingAverageList(stockData, maType, length2, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var ema1 = period1EmaList[i];
            var ema2 = period2EmaList[i];

            var macd = ema1 - ema2;
            macdList.Add(macd);
        }

        var macdSignalLineList = GetMovingAverageList(stockData, maType, length3, macdList);
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
        stockData.IndicatorName = IndicatorName.ErgodicMovingAverageConvergenceDivergence;

        return stockData;
    }
}

