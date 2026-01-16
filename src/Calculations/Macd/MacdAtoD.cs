
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the 4 Moving Average Convergence Divergence (Macd)
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="length4"></param>
    /// <param name="length5"></param>
    /// <param name="length6"></param>
    /// <param name="blueMult"></param>
    /// <param name="yellowMult"></param>
    /// <returns></returns>
    public static StockData Calculate4MovingAverageConvergenceDivergence(this StockData stockData,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 5, int length2 = 8, int length3 = 10, int length4 = 17,
        int length5 = 14, int length6 = 16, double blueMult = 4.3, double yellowMult = 1.4)
    {
        List<double> macd1List = new(stockData.Count);
        List<double> macd2List = new(stockData.Count);
        List<double> macd3List = new(stockData.Count);
        List<double> macd4List = new(stockData.Count);
        List<double> macd2HistogramList = new(stockData.Count);
        List<double> macd4HistogramList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var ema5List = GetMovingAverageList(stockData, maType, length1, inputList);
        var ema8List = GetMovingAverageList(stockData, maType, length2, inputList);
        var ema10List = GetMovingAverageList(stockData, maType, length3, inputList);
        var ema17List = GetMovingAverageList(stockData, maType, length4, inputList);
        var ema14List = GetMovingAverageList(stockData, maType, length5, inputList);
        var ema16List = GetMovingAverageList(stockData, maType, length6, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var ema5 = ema5List[i];
            var ema8 = ema8List[i];
            var ema10 = ema10List[i];
            var ema14 = ema14List[i];
            var ema16 = ema16List[i];
            var ema17 = ema17List[i];

            var macd1 = ema17 - ema14;
            macd1List.Add(macd1);

            var macd2 = ema17 - ema8;
            macd2List.Add(macd2);

            var macd3 = ema10 - ema16;
            macd3List.Add(macd3);

            var macd4 = ema5 - ema10;
            macd4List.Add(macd4);
        }

        var macd1SignalLineList = GetMovingAverageList(stockData, maType, length1, macd1List);
        var macd2SignalLineList = GetMovingAverageList(stockData, maType, length1, macd2List); //-V3056
        var macd3SignalLineList = GetMovingAverageList(stockData, maType, length1, macd3List);
        var macd4SignalLineList = GetMovingAverageList(stockData, maType, length1, macd4List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var macd1 = macd1List[i];
            var macd1SignalLine = macd1SignalLineList[i];
            var macd2 = macd2List[i];
            var macd2SignalLine = macd2SignalLineList[i];
            var macd3 = macd3List[i];
            var macd3SignalLine = macd3SignalLineList[i];
            var macd4 = macd4List[i];
            var macd4SignalLine = macd4SignalLineList[i];
            var macd1Histogram = macd1 - macd1SignalLine;
            var macdBlue = blueMult * macd1Histogram;

            var prevMacd2Histogram = i >= 1 ? macd2HistogramList[i - 1] : 0;        
            var macd2Histogram = macd2 - macd2SignalLine;
            macd2HistogramList.Add(macd2Histogram);

            var macd3Histogram = macd3 - macd3SignalLine;
            var macdYellow = yellowMult * macd3Histogram;

            var prevMacd4Histogram = i >= 1 ? macd4HistogramList[i - 1] : 0;        
            var macd4Histogram = macd4 - macd4SignalLine;
            macd4HistogramList.Add(macd4Histogram);

            var signal = GetCompareSignal(macd4Histogram - macd2Histogram, prevMacd4Histogram - prevMacd2Histogram);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Macd1", macd4List },
            { "Signal1", macd4SignalLineList },
            { "Histogram1", macd4HistogramList },
            { "Macd2", macd2List },
            { "Signal2", macd2SignalLineList },
            { "Histogram2", macd2HistogramList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName._4MovingAverageConvergenceDivergence;

        return stockData;
    }


    /// <summary>
    /// Calculates the DiNapoli Moving Average Convergence Divergence
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="lc"></param>
    /// <param name="sc"></param>
    /// <param name="sp"></param>
    /// <returns></returns>
    public static StockData CalculateDiNapoliMovingAverageConvergenceDivergence(this StockData stockData, double lc = 17.5185, double sc = 8.3896, 
        double sp = 9.0503)
    {
        List<double> fsList = new(stockData.Count);
        List<double> ssList = new(stockData.Count);
        List<double> rList = new(stockData.Count);
        List<double> sList = new(stockData.Count);
        List<double> hList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var scAlpha = 2 / (1 + sc);
        var lcAlpha = 2 / (1 + lc);
        var spAlpha = 2 / (1 + sp);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];

            var prevFs = i >= 1 ? fsList[i - 1] : 0;
            var fs = prevFs + (scAlpha * (currentValue - prevFs));
            fsList.Add(fs);

            var prevSs = i >= 1 ? ssList[i - 1] : 0;
            var ss = prevSs + (lcAlpha * (currentValue - prevSs));
            ssList.Add(ss);

            var r = fs - ss;
            rList.Add(r);

            var prevS = i >= 1 ? sList[i - 1] : 0;
            var s = prevS + (spAlpha * (r - prevS));
            sList.Add(s);

            var prevH = i >= 1 ? hList[i - 1] : 0;
            var h = r - s;
            hList.Add(h);

            var signal = GetCompareSignal(h, prevH);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "FastS", fsList },
            { "SlowS", ssList },
            { "Macd", rList },
            { "Signal", sList },
            { "Histogram", hList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(rList);
        stockData.IndicatorName = IndicatorName.DiNapoliMovingAverageConvergenceDivergence;

        return stockData;
    }
  
}

