
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the 4 Percentage Price Oscillator
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
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData Calculate4PercentagePriceOscillator(this StockData stockData,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 5, int length2 = 8, int length3 = 10, int length4 = 17,
        int length5 = 14, int length6 = 16, double blueMult = 4.3, double yellowMult = 1.4)
    {
        List<double> ppo1List = new(stockData.Count);
        List<double> ppo2List = new(stockData.Count);
        List<double> ppo3List = new(stockData.Count);
        List<double> ppo4List = new(stockData.Count);
        List<double> ppo2HistogramList = new(stockData.Count);
        List<double> ppo4HistogramList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var ema5List = maType == MovingAvgType.SimpleMovingAverage ? BollingerArithmetic.Mean(inputList, length1) : GetMovingAverageList(stockData, maType, length1, inputList);
        var ema8List = maType == MovingAvgType.SimpleMovingAverage ? BollingerArithmetic.Mean(inputList, length2) : GetMovingAverageList(stockData, maType, length2, inputList);
        var ema10List = maType == MovingAvgType.SimpleMovingAverage ? BollingerArithmetic.Mean(inputList, length3) : GetMovingAverageList(stockData, maType, length3, inputList);
        var ema17List = maType == MovingAvgType.SimpleMovingAverage ? BollingerArithmetic.Mean(inputList, length4) : GetMovingAverageList(stockData, maType, length4, inputList);
        var ema14List = maType == MovingAvgType.SimpleMovingAverage ? BollingerArithmetic.Mean(inputList, length5) : GetMovingAverageList(stockData, maType, length5, inputList);
        var ema16List = maType == MovingAvgType.SimpleMovingAverage ? BollingerArithmetic.Mean(inputList, length6) : GetMovingAverageList(stockData, maType, length6, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var ema5 = ema5List[i];
            var ema8 = ema8List[i];
            var ema10 = ema10List[i];
            var ema14 = ema14List[i];
            var ema16 = ema16List[i];
            var ema17 = ema17List[i];

            var ppo1 = RoundedPercentageChange.Of(ema17, ema14);
            ppo1List.Add(ppo1);

            var ppo2 = RoundedPercentageChange.Of(ema17, ema8);
            ppo2List.Add(ppo2);

            var ppo3 = RoundedPercentageChange.Of(ema10, ema16);
            ppo3List.Add(ppo3);

            var ppo4 = RoundedPercentageChange.Of(ema5, ema10);
            ppo4List.Add(ppo4);
        }

        var ppo1Input = FiniteSignalInput.Create(ppo1List, out var ppo1Count);
        var ppo1SignalLineList = maType == MovingAvgType.SimpleMovingAverage ? BollingerArithmetic.Mean(ppo1Input, length1) : GetMovingAverageList(stockData, maType, length1, ppo1Input);
        for (var i = ppo1Count; i < ppo1SignalLineList.Count; i++) ppo1SignalLineList[i] = double.NaN;
        var ppo2Input = FiniteSignalInput.Create(ppo2List, out var ppo2Count);
        var ppo2SignalLineList = maType == MovingAvgType.SimpleMovingAverage ? BollingerArithmetic.Mean(ppo2Input, length1) : GetMovingAverageList(stockData, maType, length1, ppo2Input);
        for (var i = ppo2Count; i < ppo2SignalLineList.Count; i++) ppo2SignalLineList[i] = double.NaN; //-V3056
        var ppo3Input = FiniteSignalInput.Create(ppo3List, out var ppo3Count);
        var ppo3SignalLineList = maType == MovingAvgType.SimpleMovingAverage ? BollingerArithmetic.Mean(ppo3Input, length1) : GetMovingAverageList(stockData, maType, length1, ppo3Input);
        for (var i = ppo3Count; i < ppo3SignalLineList.Count; i++) ppo3SignalLineList[i] = double.NaN;
        var ppo4Input = FiniteSignalInput.Create(ppo4List, out var ppo4Count);
        var ppo4SignalLineList = maType == MovingAvgType.SimpleMovingAverage ? BollingerArithmetic.Mean(ppo4Input, length1) : GetMovingAverageList(stockData, maType, length1, ppo4Input);
        for (var i = ppo4Count; i < ppo4SignalLineList.Count; i++) ppo4SignalLineList[i] = double.NaN;
        for (var i = 0; i < stockData.Count; i++)
        {
            var ppo1 = ppo1List[i];
            var ppo1SignalLine = ppo1SignalLineList[i];
            var ppo2 = ppo2List[i];
            var ppo2SignalLine = ppo2SignalLineList[i];
            var ppo3 = ppo3List[i];
            var ppo3SignalLine = ppo3SignalLineList[i];
            var ppo4 = ppo4List[i];
            var ppo4SignalLine = ppo4SignalLineList[i];
            var ppo1Histogram = ppo1 - ppo1SignalLine;
            var ppoBlue = blueMult * ppo1Histogram;

            var prevPpo2Histogram = GetLastOrDefault(ppo2HistogramList);
            var ppo2Histogram = ppo2 - ppo2SignalLine;
            ppo2HistogramList.Add(ppo2Histogram);

            var ppo3Histogram = ppo3 - ppo3SignalLine;
            var ppoYellow = yellowMult * ppo3Histogram;

            var prevPpo4Histogram = GetLastOrDefault(ppo4HistogramList);
            var ppo4Histogram = ppo4 - ppo4SignalLine;
            ppo4HistogramList.Add(ppo4Histogram);

            var maxPpo = Math.Max(ppoBlue, Math.Max(ppoYellow, Math.Max(ppo2Histogram, ppo4Histogram)));
            var minPpo = Math.Min(ppoBlue, Math.Min(ppoYellow, Math.Min(ppo2Histogram, ppo4Histogram)));
            var currentPpo = (ppoBlue + ppoYellow + ppo2Histogram + ppo4Histogram) / 4;
            var ppoStochastic = maxPpo - minPpo != 0 ? MinOrMax((currentPpo - minPpo) / (maxPpo - minPpo) * 100, 100, 0) : 0;

            var signal = GetCompareSignal(ppo4Histogram - ppo2Histogram, prevPpo4Histogram - prevPpo2Histogram);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ppo1", ppo4List },
            { "Signal1", ppo4SignalLineList },
            { "Histogram1", ppo4HistogramList },
            { "Ppo2", ppo2List },
            { "Signal2", ppo2SignalLineList },
            { "Histogram2", ppo2HistogramList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName._4PercentagePriceOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the DiNapoli Percentage Price Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="lc"></param>
    /// <param name="sc"></param>
    /// <param name="sp"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateDiNapoliPercentagePriceOscillator(this StockData stockData, double lc = 17.5185, double sc = 8.3896, 
        double sp = 9.0503)
    {
        List<double> ppoList = new(stockData.Count);
        List<double> sList = new(stockData.Count);
        List<double> hList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var dinapoliMacdList = CalculateDiNapoliMovingAverageConvergenceDivergence(stockData, lc, sc, sp);
        var ssList = dinapoliMacdList.ChainedOutputs["SlowS"];
        var rList = dinapoliMacdList.ChainedOutputs["Macd"];

        var spAlpha = 2 / (1 + sp);

        for (var i = 0; i < stockData.Count; i++)
        {
            var ss = ssList[i];
            var r = rList[i];

            var ppo = ss != 0 ? 100 * r / ss : 0;
            ppoList.Add(ppo);

            var prevS = GetLastOrDefault(sList);
            var s = prevS + (spAlpha * (ppo - prevS));
            sList.Add(s);

            var prevH = GetLastOrDefault(hList);
            var h = ppo - s;
            hList.Add(h);

            var signal = GetCompareSignal(h, prevH);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ppo", ppoList },
            { "Signal", sList },
            { "Histogram", hList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(ppoList);
        stockData.IndicatorName = IndicatorName.DiNapoliPercentagePriceOscillator;

        return stockData;
    }

}

