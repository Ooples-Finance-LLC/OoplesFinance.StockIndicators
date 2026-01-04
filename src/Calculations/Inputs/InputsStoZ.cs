
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
        var tpList = GetDerivedSeriesList(stockData, DerivedSeriesKind.Hlc3);
        var count = tpList.Count;
        List<Signal>? signalsList = CreateSignalsList(stockData, count);

        for (var i = 0; i < count; i++)
        {
            var typicalPrice = tpList[i];
            var prevTypicalPrice1 = i >= 1 ? tpList[i - 1] : 0;
            var prevTypicalPrice2 = i >= 2 ? tpList[i - 2] : 0;

            var signal = GetCompareSignal(typicalPrice - prevTypicalPrice1, prevTypicalPrice1 - prevTypicalPrice2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Tp", tpList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(tpList);
        stockData.IndicatorName = IndicatorName.TypicalPrice;

        return stockData;
    }


    /// <summary>
    /// Calculates the weighted close.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <returns></returns>
    public static StockData CalculateWeightedClose(this StockData stockData)    
    {
        var weightedCloseList = GetDerivedSeriesList(stockData, DerivedSeriesKind.WeightedClose);
        var count = weightedCloseList.Count;
        List<Signal>? signalsList = CreateSignalsList(stockData, count);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < count; i++)
        {
            var currentClose = inputList[i];
            var prevClose = i >= 1 ? inputList[i - 1] : 0;
            var weightedClose = weightedCloseList[i];
            var prevWeightedClose = i >= 1 ? weightedCloseList[i - 1] : 0;

            var signal = GetCompareSignal(currentClose - weightedClose, prevClose - prevWeightedClose);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "WeightedClose", weightedCloseList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(weightedCloseList);
        stockData.IndicatorName = IndicatorName.WeightedClose;

        return stockData;
    }

}
