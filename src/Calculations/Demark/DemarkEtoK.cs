
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Demarker
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateDemarker(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 20)
    {
        List<double> demarkerList = new(stockData.Count);
        List<double> dMaxList = new(stockData.Count);
        List<double> dMinList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (_, highList, lowList, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentLow = lowList[i];
            var currentHigh = highList[i];
            var prevHigh = i >= 1 ? highList[i - 1] : 0;
            var prevLow = i >= 1 ? lowList[i - 1] : 0;

            var dMax = currentHigh > prevHigh ? currentHigh - prevHigh : 0;
            dMaxList.Add(dMax);

            var dMin = currentLow < prevLow ? prevLow - currentLow : 0;
            dMinList.Add(dMin);
        }

        var maxMaList = GetMovingAverageList(stockData, maType, length, dMaxList);
        var minMaList = GetMovingAverageList(stockData, maType, length, dMinList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var maxMa = maxMaList[i];
            var minMa = minMaList[i];
            var prevDemarker1 = i >= 1 ? demarkerList[i - 1] : 0;
            var prevDemarker2 = i >= 2 ? demarkerList[i - 2] : 0;

            var demarker = maxMa + minMa != 0 ? MinOrMax(maxMa / (maxMa + minMa) * 100, 100, 0) : 0;
            demarkerList.Add(demarker);

            var signal = GetRsiSignal(demarker - prevDemarker1, prevDemarker1 - prevDemarker2, demarker, prevDemarker1, 70, 30);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Dm", demarkerList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(demarkerList);
        stockData.IndicatorName = IndicatorName.Demarker;

        return stockData;
    }
}

