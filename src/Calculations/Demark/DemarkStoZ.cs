
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Demark Setup Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateDemarkSetupIndicator(this StockData stockData, int length = 4)
    {
        List<double> drpPriceList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];

            double uCount = 0, dCount = 0;
            for (var j = 0; j < length; j++)
            {
                var value = i >= j ? inputList[i - j] : 0;
                var prevValue = i >= j + length ? inputList[i - (j + length)] : 0;

                uCount += value > prevValue ? 1 : 0;
                dCount += value < prevValue ? 1 : 0;
            }

            double drp = dCount == length ? 1 : uCount == length ? -1 : 0;
            var drpPrice = drp != 0 ? currentValue : 0;
            drpPriceList.Add(drpPrice);

            var signal = GetConditionSignal(drp > 0 || uCount > dCount, drp < 0 || dCount > uCount);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Dsi", drpPriceList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(drpPriceList);
        stockData.IndicatorName = IndicatorName.DemarkSetupIndicator;

        return stockData;
    }

}

