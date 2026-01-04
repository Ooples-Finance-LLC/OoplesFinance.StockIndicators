
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Pivot Point Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="inputLength"></param>
    /// <returns></returns>
    public static StockData CalculatePivotPointAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 3, InputLength inputLength = InputLength.Day)
    {
        List<double> pp1List = new(stockData.Count);
        List<double> pp2List = new(stockData.Count);
        List<double> pp3List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, openList, _) = GetInputValuesList(stockData, inputLength);

        for (var i = 0; i < inputList.Count; i++)
        {
            var currentOpen = openList[i];
            var prevHigh = i >= 1 ? highList[i - 1] : 0;
            var prevLow = i >= 1 ? lowList[i - 1] : 0;
            var prevClose = i >= 1 ? inputList[i - 1] : 0;

            var pp1 = (prevHigh + prevLow + prevClose) / 3;
            pp1List.Add(pp1);

            var pp2 = (prevHigh + prevLow + prevClose + currentOpen) / 4;
            pp2List.Add(pp2);

            var pp3 = (prevHigh + prevLow + currentOpen) / 3;
            pp3List.Add(pp3);
        }

        var ppav1List = GetMovingAverageList(stockData, maType, length, pp1List);
        var ppav2List = GetMovingAverageList(stockData, maType, length, pp2List);
        var ppav3List = GetMovingAverageList(stockData, maType, length, pp3List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var pp1 = pp1List[i];
            var ppav1 = ppav1List[i];
            var prevPp1 = i >= 1 ? pp1List[i - 1] : 0;
            var prevPpav1 = i >= 1 ? ppav1List[i - 1] : 0;

            var signal = GetCompareSignal(pp1 - ppav1, prevPp1 - prevPpav1);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Pivot1", pp1List },
            { "Signal1", ppav1List },
            { "Pivot2", pp2List },
            { "Signal2", ppav2List },
            { "Pivot3", pp3List },
            { "Signal3", ppav3List }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(pp1List);
        stockData.IndicatorName = IndicatorName.PivotPointAverage;

        return stockData;
    }

}

