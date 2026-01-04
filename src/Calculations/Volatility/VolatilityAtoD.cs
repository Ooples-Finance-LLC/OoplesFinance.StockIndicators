using OoplesFinance.StockIndicators.Compatibility;
using OoplesFinance.StockIndicators.Core;

namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the choppiness index.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateChoppinessIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 14)
    {
        List<double> ciList = new(stockData.Count);
        List<double> trList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum trSumWindow = new();

        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestHighList, lowestLowList) = GetMaxAndMinValuesList(highList, lowList, length);
        var emaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentEma = emaList[i];
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevEma = i >= 1 ? emaList[i - 1] : 0;
            var highestHigh = highestHighList[i];
            var lowestLow = lowestLowList[i];
            var range = highestHigh - lowestLow;

            var tr = CalculateTrueRange(currentHigh, currentLow, prevValue);
            trList.Add(tr);
            trSumWindow.Add(tr);

            var trSum = trSumWindow.Sum(length);
            var ci = range > 0 ? 100 * Math.Log10(trSum / range) / Math.Log10(length) : 0;
            ciList.Add(ci);

            var signal = GetVolatilitySignal(currentValue - currentEma, prevValue - prevEma, ci, 38.2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ci", ciList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(ciList);
        stockData.IndicatorName = IndicatorName.ChoppinessIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Closed Form Distance Volatility
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateClosedFormDistanceVolatility(this StockData stockData,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14)
    {
        List<double> tempHighList = new(stockData.Count);
        List<double> tempLowList = new(stockData.Count);
        List<double> hvList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum highSumWindow = new();
        RollingSum lowSumWindow = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        var emaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var ema = emaList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevEma = i >= 1 ? emaList[i - 1] : 0;

            var currentHigh = highList[i];
            tempHighList.Add(currentHigh);
            highSumWindow.Add(currentHigh);

            var currentLow = lowList[i];
            tempLowList.Add(currentLow);
            lowSumWindow.Add(currentLow);

            var a = highSumWindow.Sum(length);
            var b = lowSumWindow.Sum(length);
            var abAvg = (a + b) / 2;

            var prevHv = GetLastOrDefault(hvList);
            var hv = abAvg != 0 && a != b ? Sqrt(1 - (Pow(a, 0.25) * Pow(b, 0.25) / Pow(abAvg, 0.5))) : 0;
            hvList.Add(hv);

            var signal = GetVolatilitySignal(currentValue - ema, prevValue - prevEma, hv, prevHv);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Cfdv", hvList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(hvList);
        stockData.IndicatorName = IndicatorName.ClosedFormDistanceVolatility;

        return stockData;
    }


    /// <summary>
    /// Calculates the Donchian Channel Width
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateDonchianChannelWidth(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 20,
        int smoothLength = 22)
    {
        List<double> donchianWidthList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var upper = highestList[i];
            var lower = lowestList[i];

            var donchianWidth = upper - lower;
            donchianWidthList.Add(donchianWidth);
        }

        var donchianWidthSmaList = GetMovingAverageList(stockData, maType, smoothLength, donchianWidthList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentSma = smaList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevSma = i >= 1 ? smaList[i - 1] : 0;
            var donchianWidth = donchianWidthList[i];
            var donchianWidthSma = donchianWidthSmaList[i];

            var signal = GetVolatilitySignal(currentValue - currentSma, prevValue - prevSma, donchianWidth, donchianWidthSma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Dcw", donchianWidthList },
            { "Signal", donchianWidthSmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(donchianWidthList);
        stockData.IndicatorName = IndicatorName.DonchianChannelWidth;

        return stockData;
    }

}

