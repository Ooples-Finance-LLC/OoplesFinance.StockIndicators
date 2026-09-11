
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the full typical price.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateFullTypicalPrice(this StockData stockData)
    {
        // Chained: the same per-bar high and low every other indicator reads for a custom series
        // (GetCustomRangeLists). Unchained: the cached derived series, exactly as before.
        List<double> fullTpList;
        if (stockData.ChainedValues is { Count: > 0 })
        {
            var (seriesList, seriesHighList, seriesLowList, seriesOpenList, _) = GetInputValuesList(stockData);
            fullTpList = new List<double>(seriesList.Count);
            for (var i = 0; i < seriesList.Count; i++)
            {
                fullTpList.Add((seriesOpenList[i] + seriesHighList[i] + seriesLowList[i] + seriesList[i]) / 4);
            }
        }
        else
        {
            fullTpList = GetDerivedSeriesList(stockData, DerivedSeriesKind.Ohlc4);
        }
        var count = fullTpList.Count;
        List<Signal>? signalsList = CreateSignalsList(stockData, count);

        for (var i = 0; i < count; i++)
        {
            var typicalPrice = fullTpList[i];
            var prevTypicalPrice1 = i >= 1 ? fullTpList[i - 1] : 0;
            var prevTypicalPrice2 = i >= 2 ? fullTpList[i - 2] : 0;

            var signal = GetCompareSignal(typicalPrice - prevTypicalPrice1, prevTypicalPrice1 - prevTypicalPrice2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "FullTp", fullTpList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(fullTpList);
        stockData.IndicatorName = IndicatorName.FullTypicalPrice;

        return stockData;
    }

}
