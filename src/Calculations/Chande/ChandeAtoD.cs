
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Chande Composite Momentum Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateChandeCompositeMomentumIndex(this StockData stockData,
        MovingAvgType maType = MovingAvgType.DoubleExponentialMovingAverage, int length1 = 5, int length2 = 10, int length3 = 20, int smoothLength = 3)
    {
        List<double> valueDiff1List = new(stockData.Count);
        List<double> valueDiff2List = new(stockData.Count);
        List<double> dmiList = new(stockData.Count);
        List<double> eList = new(stockData.Count);
        List<double> sList = new(stockData.Count);
        var valueDiff1SumWindow = new RollingSum();
        var valueDiff2SumWindow = new RollingSum();
        var dmiSumWindow = new RollingSum();
        List<double> cmo5RatioList = new(stockData.Count);
        List<double> cmo10RatioList = new(stockData.Count);
        List<double> cmo20RatioList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var stdDevList = CalculateStandardDeviationVolatility(stockData, maType, length1).CustomValuesList;
        var stdDev10List = CalculateStandardDeviationVolatility(stockData, maType, length2).CustomValuesList;
        var stdDev20List = CalculateStandardDeviationVolatility(stockData, maType, length3).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var valueDiff1 = currentValue > prevValue ? MinPastValues(i, 1, currentValue - prevValue) : 0;
            valueDiff1List.Add(valueDiff1);
            valueDiff1SumWindow.Add(valueDiff1);

            var valueDiff2 = currentValue < prevValue ? MinPastValues(i, 1, prevValue - currentValue) : 0;
            valueDiff2List.Add(valueDiff2);
            valueDiff2SumWindow.Add(valueDiff2);

            var cmo51 = valueDiff1SumWindow.Sum(length1);
            var cmo52 = valueDiff2SumWindow.Sum(length1);
            var cmo101 = valueDiff1SumWindow.Sum(length2);
            var cmo102 = valueDiff2SumWindow.Sum(length2);
            var cmo201 = valueDiff1SumWindow.Sum(length3);
            var cmo202 = valueDiff2SumWindow.Sum(length3);

            var cmo5Ratio = cmo51 + cmo52 != 0 ? MinOrMax(100 * (cmo51 - cmo52) / (cmo51 + cmo52), 100, -100) : 0;
            cmo5RatioList.Add(cmo5Ratio);

            var cmo10Ratio = cmo101 + cmo102 != 0 ? MinOrMax(100 * (cmo101 - cmo102) / (cmo101 + cmo102), 100, -100) : 0;
            cmo10RatioList.Add(cmo10Ratio);

            var cmo20Ratio = cmo201 + cmo202 != 0 ? MinOrMax(100 * (cmo201 - cmo202) / (cmo201 + cmo202), 100, -100) : 0;
            cmo20RatioList.Add(cmo20Ratio);
        }

        var cmo5List = GetMovingAverageList(stockData, maType, smoothLength, cmo5RatioList);
        var cmo10List = GetMovingAverageList(stockData, maType, smoothLength, cmo10RatioList);
        var cmo20List = GetMovingAverageList(stockData, maType, smoothLength, cmo20RatioList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var stdDev5 = stdDevList[i];
            var stdDev10 = stdDev10List[i];
            var stdDev20 = stdDev20List[i];
            var cmo5 = cmo5List[i];
            var cmo10 = cmo10List[i];
            var cmo20 = cmo20List[i];

            var dmi = stdDev5 + stdDev10 + stdDev20 != 0 ?
                MinOrMax(((stdDev5 * cmo5) + (stdDev10 * cmo10) + (stdDev20 * cmo20)) / (stdDev5 + stdDev10 + stdDev20), 100, -100) : 0;
            dmiList.Add(dmi);
            dmiSumWindow.Add(dmi);

            var prevS = GetLastOrDefault(sList);
            var s = dmiSumWindow.Average(length1);
            sList.Add(s);

            var prevE = GetLastOrDefault(eList);
            var e = CalculateEMA(dmi, prevE, smoothLength);
            eList.Add(e);

            var signal = GetRsiSignal(e - s, prevE - prevS, e, prevE, 70, -70);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ccmi", eList },
            { "Signal", sList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(eList);
        stockData.IndicatorName = IndicatorName.ChandeCompositeMomentumIndex;

        return stockData;
    }

}

