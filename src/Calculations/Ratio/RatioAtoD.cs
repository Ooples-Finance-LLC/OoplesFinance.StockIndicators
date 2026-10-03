
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Calmar Ratio
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateCalmarRatio(this StockData stockData, int length = 30)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); using var window = new CalmarWindow(length); var values = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        foreach (var price in input) { var point = window.Next(price, true); values.Add(point.Value); signals?.Add(point.Signal); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Cr", values } }); stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.CalmarRatio; return stockData;
    }

}

