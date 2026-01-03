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
    /// Calculates the typical price.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <returns></returns>
    public static StockData CalculateTypicalPrice(this StockData stockData)
    {
        List<double> tpList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentClose = inputList[i];
            var prevTypicalPrice1 = i >= 1 ? tpList[i - 1] : 0;
            var prevTypicalPrice2 = i >= 2 ? tpList[i - 2] : 0;

            var typicalPrice = (currentHigh + currentLow + currentClose) / 3;
            tpList.Add(typicalPrice);

            var signal = GetCompareSignal(typicalPrice - prevTypicalPrice1, prevTypicalPrice1 - prevTypicalPrice2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Tp", tpList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = tpList;
        stockData.IndicatorName = IndicatorName.TypicalPrice;

        return stockData;
    }

    /// <summary>
    /// Calculates the median price.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <returns></returns>
    public static StockData CalculateMedianPrice(this StockData stockData)
    {
        List<double> medianPriceList = new();
        List<Signal> signalsList = new();
        var (_, highList, lowList, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentLow = lowList[i];
            var currentHigh = highList[i];
            var prevMedianPrice1 = i >= 1 ? medianPriceList[i - 1] : 0;
            var prevMedianPrice2 = i >= 2 ? medianPriceList[i - 2] : 0;

            var medianPrice = (currentHigh + currentLow) / 2;
            medianPriceList.Add(medianPrice);

            var signal = GetCompareSignal(medianPrice - prevMedianPrice1, prevMedianPrice1 - prevMedianPrice2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "MedianPrice", medianPriceList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = medianPriceList;
        stockData.IndicatorName = IndicatorName.MedianPrice;

        return stockData;
    }

    /// <summary>
    /// Calculates the average price.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <returns></returns>
    public static StockData CalculateAveragePrice(this StockData stockData)
    {
        List<double> avgPriceList = new();
        List<Signal> signalsList = new();
        var (closeList, _, _, openList, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentOpen = openList[i];
            var currentClose = closeList[i];
            var prevAvgPrice1 = i >= 1 ? avgPriceList[i - 1] : 0;
            var prevAvgPrice2 = i >= 2 ? avgPriceList[i - 2] : 0;

            var avgPrice = (currentOpen + currentClose) / 2;
            avgPriceList.Add(avgPrice);

            var signal = GetCompareSignal(avgPrice - prevAvgPrice1, prevAvgPrice1 - prevAvgPrice2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "AveragePrice", avgPriceList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = avgPriceList;
        stockData.IndicatorName = IndicatorName.AveragePrice;

        return stockData;
    }

    /// <summary>
    /// Calculates the full typical price.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <returns></returns>
    public static StockData CalculateFullTypicalPrice(this StockData stockData)
    {
        List<double> fullTpList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, openList, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentClose = inputList[i];
            var currentOpen = openList[i];
            var prevTypicalPrice1 = i >= 1 ? fullTpList[i - 1] : 0;
            var prevTypicalPrice2 = i >= 2 ? fullTpList[i - 2] : 0;

            var typicalPrice = (currentHigh + currentLow + currentClose + currentOpen) / 4;
            fullTpList.Add(typicalPrice);

            var signal = GetCompareSignal(typicalPrice - prevTypicalPrice1, prevTypicalPrice1 - prevTypicalPrice2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "FullTp", fullTpList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = fullTpList;
        stockData.IndicatorName = IndicatorName.FullTypicalPrice;

        return stockData;
    }

    /// <summary>
    /// Calculates the weighted close.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <returns></returns>
    public static StockData CalculateWeightedClose(this StockData stockData)
    {
        List<double> weightedCloseList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentClose = inputList[i];
            var prevClose = i >= 1 ? inputList[i - 1] : 0;

            var prevWeightedClose = weightedCloseList.LastOrDefault();
            var weightedClose = (currentHigh + currentLow + (currentClose * 2)) / 4;
            weightedCloseList.Add(weightedClose);

            var signal = GetCompareSignal(currentClose - weightedClose, prevClose - prevWeightedClose);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "WeightedClose", weightedCloseList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = weightedCloseList;
        stockData.IndicatorName = IndicatorName.WeightedClose;

        return stockData;
    }

    /// <summary>
    /// Calculates the Midpoint
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateMidpoint(this StockData stockData, int length = 14)
    {
        List<double> midpointList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(inputList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var highest = highestList[i];
            var lowest = lowestList[i];

            var prevMidPoint = midpointList.LastOrDefault();
            var midpoint = (highest + lowest) / 2;
            midpointList.Add(midpoint);

            var signal = GetCompareSignal(currentValue - midpoint, prevValue - prevMidPoint);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "HCLC2", midpointList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = midpointList;
        stockData.IndicatorName = IndicatorName.Midpoint;

        return stockData;
    }

    /// <summary>
    /// Calculates the Midprice
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateMidprice(this StockData stockData, int length = 14)
    {
        List<double> midpriceList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var highest = highestList[i];
            var lowest = lowestList[i];

            var prevMidPrice = midpriceList.LastOrDefault();
            var midPrice = (highest + lowest) / 2;
            midpriceList.Add(midPrice);

            var signal = GetCompareSignal(currentValue - midPrice, prevValue - prevMidPrice);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "HHLL2", midpriceList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = midpriceList;
        stockData.IndicatorName = IndicatorName.Midprice;

        return stockData;
    }
}
