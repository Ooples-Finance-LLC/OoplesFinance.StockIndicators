using OoplesFinance.StockIndicators.Compatibility;
using OoplesFinance.StockIndicators.Core;

namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Welles Wilder Volatility System
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="factor"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateWellesWilderVolatilitySystem(this StockData stockData,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 63, int length2 = 21, double factor = 3)
    {
        List<double> vstopList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var atrList = CalculateAverageTrueRange(stockData, maType, length2).ChainedValues;
        var emaList = GetMovingAverageList(stockData, maType, length1, inputList);
        var (highestList, lowestList) = GetMaxAndMinValuesList(inputList, length2);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentAtr = atrList[i];
            var currentEma = emaList[i];
            var highest = highestList[i];
            var lowest = lowestList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevVStop = GetLastOrDefault(vstopList);
            var sic = currentValue > currentEma ? highest : lowest;
            var vstop = currentValue > currentEma ? sic - (factor * currentAtr) : sic + (factor * currentAtr);
            vstopList.Add(vstop);

            var signal = GetCompareSignal(currentValue - vstop, prevValue - prevVStop);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Wwvs", vstopList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(vstopList);
        stockData.IndicatorName = IndicatorName.WellesWilderVolatilitySystem;

        return stockData;
    }
}

