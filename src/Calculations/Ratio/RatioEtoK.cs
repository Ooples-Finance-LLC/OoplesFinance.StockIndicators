
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Information Ratio
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="bmk"></param>
    /// <returns></returns>
    public static StockData CalculateInformationRatio(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 30, double bmk = 0.05)
    {
        List<double> infoList = new(stockData.Count);
        List<double> retList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        double barMin = 60 * 24;
        double minPerYr = 60 * 24 * 30 * 12;
        var barsPerYr = minPerYr / barMin;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= length ? inputList[i - length] : 0;

            var ret = prevValue != 0 ? (currentValue / prevValue) - 1 : 0;
            retList.Add(ret);
        }

        stockData.SetCustomValues(retList);
        var stdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        var retSmaList = GetMovingAverageList(stockData, maType, length, retList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var stdDeviation = stdDevList[i];
            var retSma = retSmaList[i];
            var bench = Pow(1 + bmk, length / barsPerYr) - 1;

            var prevInfo = GetLastOrDefault(infoList);
            var info = stdDeviation != 0 ? (retSma - bench) / stdDeviation : 0;
            infoList.Add(info);

            var signal = GetCompareSignal(info - 5, prevInfo - 5);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ir", infoList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(infoList);
        stockData.IndicatorName = IndicatorName.InformationRatio;

        return stockData;
    }

}

