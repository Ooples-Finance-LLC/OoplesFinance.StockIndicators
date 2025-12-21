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
    /// Calculates the average true range.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateAverageTrueRange(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, 
        int length = 14)
    {
        List<double> trList = new();
        List<Signal> signalsList = new();

        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var maList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var prevClose = i >= 1 ? inputList[i - 1] : 0;

            var currentTrueRange = CalculateTrueRange(currentHigh, currentLow, prevClose);
            trList.Add(currentTrueRange);
        }

        var atrList = GetMovingAverageList(stockData, maType, length, trList);
        var atrMaList = GetMovingAverageList(stockData, maType, length, atrList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var atr = atrList[i];
            var currentMa = maList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevMa = i >= 1 ? maList[i - 1] : 0;
            var atrMa = atrMaList[i];

            var signal = GetVolatilitySignal(currentValue - currentMa, prevValue - prevMa, atr, atrMa);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Atr", atrList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = atrList;
        stockData.IndicatorName = IndicatorName.AverageTrueRange;

        return stockData;
    }

    /// <summary>
    /// Calculates the average index of the directional.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateAverageDirectionalIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int length = 14)
    {
        List<double> dmPlusList = new();
        List<double> dmMinusList = new();
        List<double> diPlusList = new();
        List<double> diMinusList = new();
        List<double> trList = new();
        List<double> diList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var prevHigh = i >= 1 ? highList[i - 1] : 0;
            var prevLow = i >= 1 ? lowList[i - 1] : 0;
            var prevClose = i >= 1 ? inputList[i - 1] : 0;
            var highDiff = currentHigh - prevHigh;
            var lowDiff = prevLow - currentLow;

            var dmPlus = highDiff > lowDiff ? Math.Max(highDiff, 0) : 0;
            dmPlusList.Add(dmPlus);

            var dmMinus = highDiff < lowDiff ? Math.Max(lowDiff, 0) : 0;
            dmMinusList.Add(dmMinus);

            var tr = CalculateTrueRange(currentHigh, currentLow, prevClose);
            trList.Add(tr);
        }

        var dmPlus14List = GetMovingAverageList(stockData, maType, length, dmPlusList);
        var dmMinus14List = GetMovingAverageList(stockData, maType, length, dmMinusList);
        var tr14List = GetMovingAverageList(stockData, maType, length, trList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var dmPlus14 = dmPlus14List[i];
            var dmMinus14 = dmMinus14List[i];
            var trueRange14 = tr14List[i];

            var diPlus = trueRange14 != 0 ? MinOrMax(100 * dmPlus14 / trueRange14, 100, 0) : 0;
            diPlusList.Add(diPlus);

            var diMinus = trueRange14 != 0 ? MinOrMax(100 * dmMinus14 / trueRange14, 100, 0) : 0;
            diMinusList.Add(diMinus);

            var diDiff = Math.Abs(diPlus - diMinus);
            var diSum = diPlus + diMinus;

            var di = diSum != 0 ? MinOrMax(100 * diDiff / diSum, 100, 0) : 0;
            diList.Add(di);
        }

        var adxList = GetMovingAverageList(stockData, maType, length, diList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var diPlus = diPlusList[i];
            var diMinus = diMinusList[i];
            var prevDiPlus = i >= 1 ? diPlusList[i - 1] : 0;
            var prevDiMinus = i >= 1 ? diMinusList[i - 1] : 0;

            var signal = GetCompareSignal(diPlus - diMinus, prevDiPlus - prevDiMinus);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "DiPlus", diPlusList },
            { "DiMinus", diMinusList },
            { "Adx", adxList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = adxList;
        stockData.IndicatorName = IndicatorName.AverageDirectionalIndex;

        return stockData;
    }

    /// <summary>
    /// Calculates the Welles Wilder Volatility System
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="factor"></param>
    /// <returns></returns>
    public static StockData CalculateWellesWilderVolatilitySystem(this StockData stockData,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 63, int length2 = 21, double factor = 3)
    {
        List<double> vstopList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var atrList = CalculateAverageTrueRange(stockData, maType, length2).CustomValuesList;
        var emaList = GetMovingAverageList(stockData, maType, length1, inputList);
        var (highestList, lowestList) = GetMaxAndMinValuesList(inputList, length2);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentAtr = atrList[i];
            var currentEma = emaList[i];
            var highest = highestList[i];
            var lowest = lowestList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevVStop = vstopList.LastOrDefault();
            var sic = currentValue > currentEma ? highest : lowest;
            var vstop = currentValue > currentEma ? sic - (factor * currentAtr) : sic + (factor * currentAtr);
            vstopList.Add(vstop);

            var signal = GetCompareSignal(currentValue - vstop, prevValue - prevVStop);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Wwvs", vstopList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = vstopList;
        stockData.IndicatorName = IndicatorName.WellesWilderVolatilitySystem;

        return stockData;
    }
}
