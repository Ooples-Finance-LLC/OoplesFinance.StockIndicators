
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Fisher Transform Stochastic Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="stochLength"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateFisherTransformStochasticOscillator(this StockData stockData,
        MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 2, int stochLength = 30, int smoothLength = 5)
    {
        List<double> rbwList = new(stockData.Count);
        List<double> ftsoList = new(stockData.Count);
        List<double> numList = new(stockData.Count);
        List<double> denomList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum numSumWindow = new();
        RollingSum denomSumWindow = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var wmaList = GetMovingAverageList(stockData, maType, length, inputList);
        var wma2List = GetMovingAverageList(stockData, maType, length, wmaList);
        var wma3List = GetMovingAverageList(stockData, maType, length, wma2List);
        var wma4List = GetMovingAverageList(stockData, maType, length, wma3List);
        var wma5List = GetMovingAverageList(stockData, maType, length, wma4List);
        var wma6List = GetMovingAverageList(stockData, maType, length, wma5List);
        var wma7List = GetMovingAverageList(stockData, maType, length, wma6List);
        var wma8List = GetMovingAverageList(stockData, maType, length, wma7List);
        var wma9List = GetMovingAverageList(stockData, maType, length, wma8List);
        var wma10List = GetMovingAverageList(stockData, maType, length, wma9List);

        for (var i = 0; i < stockData.Count; i++)
        {
            var wma1 = wmaList[i];
            var wma2 = wma2List[i];
            var wma3 = wma3List[i];
            var wma4 = wma4List[i];
            var wma5 = wma5List[i];
            var wma6 = wma6List[i];
            var wma7 = wma7List[i];
            var wma8 = wma8List[i];
            var wma9 = wma9List[i];
            var wma10 = wma10List[i];

            var rbw = ((wma1 * 5) + (wma2 * 4) + (wma3 * 3) + (wma4 * 2) + wma5 + wma6 + wma7 + wma8 + wma9 + wma10) / 20;
            rbwList.Add(rbw);
        }

        var (highestList, lowestList) = GetMaxAndMinValuesList(rbwList, stochLength);
        for (var i = 0; i < stockData.Count; i++)
        {
            var highest = highestList[i];
            var lowest = lowestList[i];
            var rbw = rbwList[i];
            var prevFtso1 = i >= 1 ? ftsoList[i - 1] : 0;
            var prevFtso2 = i >= 2 ? ftsoList[i - 2] : 0;

            var num = rbw - lowest;
            numList.Add(num);
            numSumWindow.Add(num);

            var denom = highest - lowest;
            denomList.Add(denom);
            denomSumWindow.Add(denom);

            var numSum = numSumWindow.Sum(smoothLength);
            var denomSum = denomSumWindow.Sum(smoothLength);
            var rbws = denomSum + 0.0001 != 0 ? MinOrMax(numSum / (denomSum + 0.0001) * 100, 100, 0) : 0;
            var x = 0.1 * (rbws - 50);

            var ftso = MinOrMax((((Exp(2 * x) - 1) / (Exp(2 * x) + 1)) + 1) * 50, 100, 0);
            ftsoList.Add(ftso);

            var signal = GetRsiSignal(ftso - prevFtso1, prevFtso1 - prevFtso2, ftso, prevFtso1, 70, 30);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ftso", ftsoList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(ftsoList);
        stockData.IndicatorName = IndicatorName.FisherTransformStochasticOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Fast and Slow Stochastic Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="length4"></param>
    /// <returns></returns>
    public static StockData CalculateFastandSlowStochasticOscillator(this StockData stockData,
        MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length1 = 3, int length2 = 6, int length3 = 9, int length4 = 9)
    {
        List<double> fsstList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var fskList = CalculateFastandSlowKurtosisOscillator(stockData, maType, length1).CustomValuesList;
        var v4List = GetMovingAverageList(stockData, maType, length2, fskList);
        var fastKList = CalculateStochasticOscillator(stockData, maType, length: length3).CustomValuesList;
        var slowKList = GetMovingAverageList(stockData, maType, length3, fastKList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var v4 = v4List[i];
            var slowK = slowKList[i];

            var fsst = (500 * v4) + slowK;
            fsstList.Add(fsst);
        }

        var wfsstList = GetMovingAverageList(stockData, maType, length4, fsstList);
        for (var i = 0; i < wfsstList.Count; i++)
        {
            var fsst = fsstList[i];
            var wfsst = wfsstList[i];
            var prevFsst = i >= 1 ? fsstList[i - 1] : 0;
            var prevWfsst = i >= 1 ? wfsstList[i - 1] : 0;

            var signal = GetCompareSignal(fsst - wfsst, prevFsst - prevWfsst);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Fsst", fsstList },
            { "Signal", wfsstList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(fsstList);
        stockData.IndicatorName = IndicatorName.FastandSlowStochasticOscillator;

        return stockData;
    }

}

