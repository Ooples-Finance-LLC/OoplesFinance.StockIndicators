
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Calmar Ratio
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateCalmarRatio(this StockData stockData, int length = 30)
    {
        List<double> calmarList = new(stockData.Count);
        List<double> ddList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingMinMax ddWindow = new(length);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var (highestList, _) = GetMaxAndMinValuesList(inputList, length);

        double barMin = 60 * 24;
        double minPerYr = 60 * 24 * 30 * 12;
        var barsPerYr = minPerYr / barMin;
        var power = barsPerYr / (length * 15);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= length ? inputList[i - length] : 0;
            var maxDn = highestList[i];

            var dd = maxDn != 0 ? (currentValue - maxDn) / maxDn : 0;
            ddList.Add(dd);
            ddWindow.Add(dd);

            var ret = prevValue != 0 ? (currentValue / prevValue) - 1 : 0;
            var annualReturn = 1 + ret >= 0 ? Pow(1 + ret, power) - 1 : 0;
            var maxDd = ddWindow.Min;

            var prevCalmar = GetLastOrDefault(calmarList);
            var calmar = maxDd != 0 ? annualReturn / Math.Abs(maxDd) : 0;
            calmarList.Add(calmar);

            var signal = GetCompareSignal(calmar - 2, prevCalmar - 2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Cr", calmarList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(calmarList);
        stockData.IndicatorName = IndicatorName.CalmarRatio;

        return stockData;
    }

}

