using OoplesFinance.StockIndicators.Compatibility;
using OoplesFinance.StockIndicators.Core;

namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the historical volatility.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateHistoricalVolatility(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 20)
    {
        List<double> hvList = new(stockData.Count);
        List<double> tempLogList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var annualSqrt = Sqrt(365);

        var emaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var temp = prevValue != 0 ? currentValue / prevValue : 0;

            var tempLog = temp > 0 ? Math.Log(temp) : 0;
            tempLogList.Add(tempLog);
        }

        stockData.SetCustomValues(tempLogList);
        var stdDevLogList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var stdDevLog = stdDevLogList[i];
            var currentEma = emaList[i];
            var prevEma = i >= 1 ? emaList[i - 1] : 0;
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevHv = GetLastOrDefault(hvList);
            var hv = 100 * stdDevLog * annualSqrt;
            hvList.Add(hv);

            var signal = GetVolatilitySignal(currentValue - currentEma, prevValue - prevEma, hv, prevHv);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Hv", hvList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(hvList);
        stockData.IndicatorName = IndicatorName.HistoricalVolatility;

        return stockData;
    }


    /// <summary>
    /// Calculates the Garman Klass Volatility
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculateGarmanKlassVolatility(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length = 14, int signalLength = 7)
    {
        List<double> gcvList = new(stockData.Count);
        List<double> logList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum logSumWindow = new();
        var (inputList, highList, lowList, openList, _) = GetInputValuesList(stockData);

        var wmaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentOpen = openList[i];
            var currentClose = inputList[i];
            var logHl = currentLow != 0 ? Math.Log(currentHigh / currentLow) : 0;
            var logCo = currentOpen != 0 ? Math.Log(currentClose / currentOpen) : 0;

            var log = (0.5 * Pow(logHl, 2)) - (((2 * Math.Log(2)) - 1) * Pow(logCo, 2));
            logList.Add(log);
            logSumWindow.Add(log);

            var logSum = logSumWindow.Sum(length);
            var gcv = length != 0 && logSum != 0 ? Sqrt((double)i / length * logSum) : 0;
            gcvList.Add(gcv);
        }

        var gcvWmaList = GetMovingAverageList(stockData, maType, signalLength, gcvList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentClose = inputList[i];
            var wma = wmaList[i];
            var prevClose = i >= 1 ? inputList[i - 1] : 0;
            var prevWma = i >= 1 ? wmaList[i - 1] : 0;
            var gcv = gcvList[i];
            var gcvWma = i >= 1 ? gcvWmaList[i - 1] : 0;

            var signal = GetVolatilitySignal(currentClose - wma, prevClose - prevWma, gcv, gcvWma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Gcv", gcvList },
            { "Signal", gcvWmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(gcvList);
        stockData.IndicatorName = IndicatorName.GarmanKlassVolatility;

        return stockData;
    }


    /// <summary>
    /// Calculates the Gopalakrishnan Range Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateGopalakrishnanRangeIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length = 5)
    {
        List<double> gapoList = new(stockData.Count);
        List<double> gapoEmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        var wmaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var highestHigh = highestList[i];
            var lowestLow = lowestList[i];
            var range = highestHigh - lowestLow;
            var rangeLog = range > 0 ? Math.Log(range) : 0;

            var gapo = rangeLog / Math.Log(length);
            gapoList.Add(gapo);
        }

        var gapoWmaList = GetMovingAverageList(stockData, maType, length, gapoList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var gapoWma = gapoWmaList[i];
            var prevGapoWma = i >= 1 ? gapoWmaList[i - 1] : 0;
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentWma = wmaList[i];
            var prevWma = i >= 1 ? wmaList[i - 1] : 0;

            var signal = GetVolatilitySignal(currentValue - currentWma, prevValue - prevWma, gapoWma, prevGapoWma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Gapo", gapoList },
            { "Signal", gapoEmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(gapoList);
        stockData.IndicatorName = IndicatorName.GopalakrishnanRangeIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Historical Volatility Percentile
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="annualLength"></param>
    /// <returns></returns>
    public static StockData CalculateHistoricalVolatilityPercentile(this StockData stockData,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 21, int annualLength = 252)
    {
        List<double> devLogSqList = new(stockData.Count);
        List<double> devLogSqAvgList = new(stockData.Count);
        List<double> hvList = new(stockData.Count);
        List<double> hvpList = new(stockData.Count);
        List<double> tempLogList = new(stockData.Count);
        List<double> stdDevLogList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum tempLogSumWindow = new();
        RollingSum devLogSqSumWindow = new();
        using var hvOrder = new RollingOrderStatistic(annualLength);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var emaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentEma = emaList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var temp = prevValue != 0 ? currentValue / prevValue : 0;
            var prevEma = i >= 1 ? emaList[i - 1] : 0;

            var tempLog = temp > 0 ? Math.Log(temp) : 0;
            tempLogList.Add(tempLog);
            tempLogSumWindow.Add(tempLog);

            var avgLog = tempLogSumWindow.Average(length);
            var devLogSq = Pow(tempLog - avgLog, 2);
            devLogSqList.Add(devLogSq);
            devLogSqSumWindow.Add(devLogSq);

            var devLogSqAvg = devLogSqSumWindow.Sum(length) / (length - 1);
            var stdDevLog = devLogSqAvg >= 0 ? Sqrt(devLogSqAvg) : 0;

            var hv = stdDevLog * Sqrt(annualLength);
            hvList.Add(hv);
            hvOrder.Add(hv);

            double count = hvOrder.CountLessThan(hv);
            var hvp = count / annualLength * 100;
            hvpList.Add(hvp);
        }

        var hvpEmaList = GetMovingAverageList(stockData, maType, length, hvpList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentEma = emaList[i];
            var prevEma = i >= 1 ? emaList[i - 1] : 0;
            var hvp = hvpList[i];
            var hvpEma = hvpEmaList[i];

            var signal = GetVolatilitySignal(currentValue - currentEma, prevValue - prevEma, hvp, hvpEma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Hvp", hvpList },
            { "Signal", hvpEmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(hvpList);
        stockData.IndicatorName = IndicatorName.HistoricalVolatilityPercentile;

        return stockData;
    }


    /// <summary>
    /// Calculates the Fast Z Score
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateFastZScore(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 200)
    {
        List<double> gsList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var length2 = MinOrMax((int)Math.Ceiling((double)length / 2));

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);
        stockData.SetCustomValues(smaList);
        var smaLinregList = CalculateLinearRegression(stockData, length).CustomValuesList;
        stockData.SetCustomValues(smaList);
        var linreg2List = CalculateLinearRegression(stockData, length2).CustomValuesList;
        stockData.SetCustomValues(smaList);
        var smaStdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var sma = smaList[i];
            var stdDev = smaStdDevList[i];
            var linreg = smaLinregList[i];
            var linreg2 = linreg2List[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevSma = i >= 1 ? smaList[i - 1] : 0;

            var gs = stdDev != 0 ? (linreg2 - linreg) / stdDev / 2 : 0;
            gsList.Add(gs);

            var signal = GetVolatilitySignal(currentValue - sma, prevValue - prevSma, gs, 0);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Fzs", gsList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(gsList);
        stockData.IndicatorName = IndicatorName.FastZScore;

        return stockData;
    }

}

