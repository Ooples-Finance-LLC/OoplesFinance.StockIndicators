
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the TFS Mbo Percentage Price Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculateTFSMboPercentagePriceOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int fastLength = 25, int slowLength = 200, int signalLength = 18)
    {
        List<double> ppoList = new(stockData.Count);
        List<double> ppoHistogramList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var mob1List = GetMovingAverageList(stockData, maType, fastLength, inputList);
        var mob2List = GetMovingAverageList(stockData, maType, slowLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var mob1 = mob1List[i];
            var mob2 = mob2List[i];
            var tfsMob = mob1 - mob2;

            var ppo = mob2 != 0 ? tfsMob / mob2 * 100 : 0;
            ppoList.Add(ppo);
        }

        var ppoSignalLineList = GetMovingAverageList(stockData, maType, signalLength, ppoList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var ppo = ppoList[i];
            var ppoSignalLine = ppoSignalLineList[i];

            var prevPpoHistogram = GetLastOrDefault(ppoHistogramList);
            var ppoHistogram = ppo - ppoSignalLine;
            ppoHistogramList.Add(ppoHistogram);

            var signal = GetCompareSignal(ppoHistogram, prevPpoHistogram);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ppo", ppoList },
            { "Signal", ppoSignalLineList },
            { "Histogram", ppoHistogramList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(ppoList);
        stockData.IndicatorName = IndicatorName.TFSMboPercentagePriceOscillator;

        return stockData;
    }

}

