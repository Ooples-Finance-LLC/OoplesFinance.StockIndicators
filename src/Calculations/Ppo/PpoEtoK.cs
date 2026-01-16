
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
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
        List<double> ppoList = new(stockData.Count);
        List<double> ppoSignalLineList = new(stockData.Count);
        List<double> ppoHistogramList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum ppoSum = new();
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
            ppoSum.Add(ppo);

            var ppoSignalLine = ppoSum.Average(signalLength);
            ppoSignalLineList.Add(ppoSignalLine);

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
        stockData.IndicatorName = IndicatorName.ImpulsePercentagePriceOscillator;

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
        List<double> ppoList = new(stockData.Count);
        List<double> ppoHistogramList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
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
        stockData.IndicatorName = IndicatorName.ErgodicPercentagePriceOscillator;

        return stockData;
    }
}

