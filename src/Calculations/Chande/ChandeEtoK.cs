
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Chande Kroll Rsquared Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateChandeKrollRSquaredIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 14, int smoothLength = 3)
    {
        List<double> r2RawList = new(stockData.Count);
        List<double> tempValueList = new(stockData.Count);
        List<double> indexList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingCorrelation corrWindow = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            double index = i;
            indexList.Add(index);

            var currentValue = inputList[i];
            tempValueList.Add(currentValue);

            corrWindow.Add(index, currentValue);
            var r2 = corrWindow.RSquared(length);
            r2 = IsValueNullOrInfinity(r2) ? 0 : r2;
            r2RawList.Add((double)r2);
        }

        var r2SmoothedList = GetMovingAverageList(stockData, maType, smoothLength, r2RawList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var r2Sma = r2SmoothedList[i];
            var prevR2Sma1 = i >= 1 ? r2SmoothedList[i - 1] : 0;
            var prevR2Sma2 = i >= 2 ? r2SmoothedList[i - 2] : 0;

            var signal = GetCompareSignal(r2Sma - prevR2Sma1, prevR2Sma1 - prevR2Sma2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ckrsi", r2SmoothedList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(r2SmoothedList);
        stockData.IndicatorName = IndicatorName.ChandeKrollRSquaredIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Chande Forecast Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateChandeForecastOscillator(this StockData stockData, int length = 14)
    {
        List<double> pfList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var linRegList = CalculateLinearRegression(stockData, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentLinReg = linRegList[i];

            var prevPf = GetLastOrDefault(pfList);
            var pf = currentValue != 0 ? (currentValue - currentLinReg) * 100 / currentValue : 0;
            pfList.Add(pf);

            var signal = GetCompareSignal(pf, prevPf);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Cfo", pfList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(pfList);
        stockData.IndicatorName = IndicatorName.ChandeForecastOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Chande Intraday Momentum Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateChandeIntradayMomentumIndex(this StockData stockData, int length = 14)
    {
        List<double> imiUnfilteredList = new(stockData.Count);
        List<double> gainsList = new(stockData.Count);
        List<double> lossesList = new(stockData.Count);
        var gainsSumWindow = new RollingSum();
        var lossesSumWindow = new RollingSum();
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, openList, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentClose = inputList[i];
            var currentOpen = openList[i];
            var prevImi1 = i >= 1 ? imiUnfilteredList[i - 1] : 0;
            var prevImi2 = i >= 2 ? imiUnfilteredList[i - 2] : 0;

            var prevGains = GetLastOrDefault(gainsList);
            var gains = currentClose > currentOpen ? prevGains + (currentClose - currentOpen) : 0;
            gainsList.Add(gains);
            gainsSumWindow.Add(gains);

            var prevLosses = GetLastOrDefault(lossesList);
            var losses = currentClose < currentOpen ? prevLosses + (currentOpen - currentClose) : 0;
            lossesList.Add(losses);
            lossesSumWindow.Add(losses);

            var upt = gainsSumWindow.Sum(length);
            var dnt = lossesSumWindow.Sum(length);

            var imiUnfiltered = upt + dnt != 0 ? MinOrMax(100 * upt / (upt + dnt), 100, 0) : 0;
            imiUnfilteredList.Add(imiUnfiltered);

            var signal = GetRsiSignal(imiUnfiltered - prevImi1, prevImi1 - prevImi2, imiUnfiltered, prevImi1, 70, 30);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Cimi", imiUnfilteredList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(imiUnfilteredList);
        stockData.IndicatorName = IndicatorName.ChandeIntradayMomentumIndex;

        return stockData;
    }

}

