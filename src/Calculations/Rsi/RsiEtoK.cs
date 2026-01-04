using OoplesFinance.StockIndicators.Compatibility;
using OoplesFinance.StockIndicators.Core;

namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Folded Relative Strength Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateFoldedRelativeStrengthIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 14)
    {
        List<double> absRsiList = new(stockData.Count);
        List<double> frsiList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum absRsiSum = new();

        var rsiList = CalculateRelativeStrengthIndex(stockData, maType, length: length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var rsi = rsiList[i];

            var absRsi = 2 * Math.Abs(rsi - 50);
            absRsiList.Add(absRsi);
            absRsiSum.Add(absRsi);

            var frsi = absRsiSum.Sum(length);
            frsiList.Add(frsi);
        }

        var frsiMaList = GetMovingAverageList(stockData, maType, length, frsiList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var frsi = frsiList[i];
            var frsiMa = frsiMaList[i];
            var prevFrsi = i >= 1 ? frsiList[i - 1] : 0;
            var prevFrsiMa = i >= 1 ? frsiMaList[i - 1] : 0;

            var signal = GetRsiSignal(frsi - frsiMa, prevFrsi - prevFrsiMa, frsi, prevFrsi, 50, 10);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Frsi", frsiList },
            { "Signal", frsiMaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(frsiList);
        stockData.IndicatorName = IndicatorName.FoldedRelativeStrengthIndex;

        return stockData;
    }

}

