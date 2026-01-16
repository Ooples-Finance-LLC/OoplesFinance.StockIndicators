
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the TFS Mbo Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculateTFSMboIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int fastLength = 25, int slowLength = 200, int signalLength = 18)
    {
        List<double> tfsMobList = new(stockData.Count);
        List<double> tfsMobHistogramList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var mob1List = GetMovingAverageList(stockData, maType, fastLength, inputList);
        var mob2List = GetMovingAverageList(stockData, maType, slowLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var mob1 = mob1List[i];
            var mob2 = mob2List[i];

            var tfsMob = mob1 - mob2;
            tfsMobList.Add(tfsMob);
        }

        var tfsMobSignalLineList = GetMovingAverageList(stockData, maType, signalLength, tfsMobList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var tfsMob = tfsMobList[i];
            var tfsMobSignalLine = tfsMobSignalLineList[i];

            var prevTfsMobHistogram = i >= 1 ? tfsMobHistogramList[i - 1] : 0;      
            var tfsMobHistogram = tfsMob - tfsMobSignalLine;
            tfsMobHistogramList.Add(tfsMobHistogram);

            var signal = GetCompareSignal(tfsMobHistogram, prevTfsMobHistogram);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "TfsMob", tfsMobList },
            { "Signal", tfsMobSignalLineList },
            { "Histogram", tfsMobHistogramList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(tfsMobList);
        stockData.IndicatorName = IndicatorName.TFSMboIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Stochastic Moving Average Convergence Divergence Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculateStochasticMovingAverageConvergenceDivergenceOscillator(this StockData stockData, 
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 45, int fastLength = 12, int slowLength = 26, int signalLength = 9)
    {
        List<double> macdStochasticHistogramList = new(stockData.Count);
        List<double> fastStochasticList = new(stockData.Count);
        List<double> slowStochasticList = new(stockData.Count);
        List<double> macdStochasticList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        var fastEmaList = GetMovingAverageList(stockData, maType, fastLength, inputList);
        var slowEmaList = GetMovingAverageList(stockData, maType, slowLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var fastEma = fastEmaList[i];
            var slowEma = slowEmaList[i];
            var hh = highestList[i];
            var ll = lowestList[i];
            var range = hh - ll;

            var fastStochastic = range != 0 ? (fastEma - ll) / range : 0;
            fastStochasticList.Add(fastStochastic);

            var slowStochastic = range != 0 ? (slowEma - ll) / range : 0;
            slowStochasticList.Add(slowStochastic);

            var macdStochastic = 10 * (fastStochastic - slowStochastic);
            macdStochasticList.Add(macdStochastic);
        }

        var macdStochasticSignalLineList = GetMovingAverageList(stockData, maType, signalLength, macdStochasticList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var macdStochastic = macdStochasticList[i];
            var macdStochasticSignalLine = macdStochasticSignalLineList[i];

            var prevMacdHistogram = i >= 1 ? macdStochasticHistogramList[i - 1] : 0;
            var macdHistogram = macdStochastic - macdStochasticSignalLine;
            macdStochasticHistogramList.Add(macdHistogram);

            var signal = GetCompareSignal(macdHistogram, prevMacdHistogram);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Macd", macdStochasticList },
            { "Signal", macdStochasticSignalLineList },
            { "Histogram", macdStochasticHistogramList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(macdStochasticList);
        stockData.IndicatorName = IndicatorName.StochasticMovingAverageConvergenceDivergenceOscillator;

        return stockData;
    }

}

