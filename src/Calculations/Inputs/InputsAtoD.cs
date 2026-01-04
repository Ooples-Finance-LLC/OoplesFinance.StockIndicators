
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the average price.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <returns></returns>
    public static StockData CalculateAveragePrice(this StockData stockData)
    {
        var avgPriceList = GetDerivedSeriesList(stockData, DerivedSeriesKind.AveragePrice);
        var count = avgPriceList.Count;
        List<Signal>? signalsList = CreateSignalsList(stockData, count);

        for (var i = 0; i < count; i++)
        {
            var avgPrice = avgPriceList[i];
            var prevAvgPrice1 = i >= 1 ? avgPriceList[i - 1] : 0;
            var prevAvgPrice2 = i >= 2 ? avgPriceList[i - 2] : 0;

            var signal = GetCompareSignal(avgPrice - prevAvgPrice1, prevAvgPrice1 - prevAvgPrice2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "AveragePrice", avgPriceList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(avgPriceList);
        stockData.IndicatorName = IndicatorName.AveragePrice;

        return stockData;
    }

}
