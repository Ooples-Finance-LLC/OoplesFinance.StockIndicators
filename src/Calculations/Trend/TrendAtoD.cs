
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Coral Trend Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="cd"></param>
    /// <returns></returns>
    public static StockData CalculateCoralTrendIndicator(this StockData stockData, int length = 21, double cd = 0.4)
    {
        List<double> i1List = new(stockData.Count);
        List<double> i2List = new(stockData.Count);
        List<double> i3List = new(stockData.Count);
        List<double> i4List = new(stockData.Count);
        List<double> i5List = new(stockData.Count);
        List<double> i6List = new(stockData.Count);
        List<double> bfrList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var di = ((double)(length - 1) / 2) + 1;
        var c1 = 2 / (di + 1);
        var c2 = 1 - c1;
        var c3 = 3 * ((cd * cd) + (cd * cd * cd));
        var c4 = -3 * ((2 * cd * cd) + cd + (cd * cd * cd));
        var c5 = (3 * cd) + 1 + (cd * cd * cd) + (3 * cd * cd);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevI1 = i >= 1 ? i1List[i - 1] : 0;
            var i1 = (c1 * currentValue) + (c2 * prevI1);
            i1List.Add(i1);

            var prevI2 = i >= 1 ? i2List[i - 1] : 0;
            var i2 = (c1 * i1) + (c2 * prevI2);
            i2List.Add(i2);

            var prevI3 = i >= 1 ? i3List[i - 1] : 0;
            var i3 = (c1 * i2) + (c2 * prevI3);
            i3List.Add(i3);

            var prevI4 = i >= 1 ? i4List[i - 1] : 0;
            var i4 = (c1 * i3) + (c2 * prevI4);
            i4List.Add(i4);

            var prevI5 = i >= 1 ? i5List[i - 1] : 0;
            var i5 = (c1 * i4) + (c2 * prevI5);
            i5List.Add(i5);

            var prevI6 = i >= 1 ? i6List[i - 1] : 0;
            var i6 = (c1 * i5) + (c2 * prevI6);
            i6List.Add(i6);

            var prevBfr = i >= 1 ? bfrList[i - 1] : 0;
            var bfr = (-1 * cd * cd * cd * i6) + (c3 * i5) + (c4 * i4) + (c5 * i3);
            bfrList.Add(bfr);

            var signal = GetCompareSignal(currentValue - bfr, prevValue - prevBfr);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Cti", bfrList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(bfrList);
        stockData.IndicatorName = IndicatorName.CoralTrendIndicator;

        return stockData;
    }

}

