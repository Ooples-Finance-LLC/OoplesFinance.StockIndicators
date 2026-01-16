
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the median price.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <returns></returns>
    public static StockData CalculateMedianPrice(this StockData stockData)
    {
        var medianPriceList = GetDerivedSeriesList(stockData, DerivedSeriesKind.Hl2);
        var count = medianPriceList.Count;
        List<Signal>? signalsList = CreateSignalsList(stockData, count);

        for (var i = 0; i < count; i++)
        {
            var medianPrice = medianPriceList[i];
            var prevMedianPrice1 = i >= 1 ? medianPriceList[i - 1] : 0;
            var prevMedianPrice2 = i >= 2 ? medianPriceList[i - 2] : 0;

            var signal = GetCompareSignal(medianPrice - prevMedianPrice1, prevMedianPrice1 - prevMedianPrice2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "MedianPrice", medianPriceList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(medianPriceList);
        stockData.IndicatorName = IndicatorName.MedianPrice;

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
        List<double> midpointList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(inputList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var highest = highestList[i];
            var lowest = lowestList[i];

            var prevMidPoint = GetLastOrDefault(midpointList);
            var midpoint = (highest + lowest) / 2;
            midpointList.Add(midpoint);

            var signal = GetCompareSignal(currentValue - midpoint, prevValue - prevMidPoint);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "HCLC2", midpointList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(midpointList);
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
        List<double> midpriceList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var highest = highestList[i];
            var lowest = lowestList[i];

            var prevMidPrice = GetLastOrDefault(midpriceList);
            var midPrice = (highest + lowest) / 2;
            midpriceList.Add(midPrice);

            var signal = GetCompareSignal(currentValue - midPrice, prevValue - prevMidPrice);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "HHLL2", midpriceList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(midpriceList);
        stockData.IndicatorName = IndicatorName.Midprice;

        return stockData;
    }
}

