
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the adaptive stochastic.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <param name="fastLength">Length of the fast.</param>
    /// <param name="slowLength">Length of the slow.</param>
    /// <returns></returns>
    public static StockData CalculateAdaptiveStochastic(this StockData stockData, int length = 50, int fastLength = 50, int slowLength = 200)
    {
        List<double> stcList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var srcList = CalculateLinearRegression(stockData, Math.Abs(slowLength - fastLength)).CustomValuesList;
        var erList = CalculateKaufmanAdaptiveMovingAverage(stockData, length: length).OutputValues["Er"];
        var (highest1List, lowest1List) = GetMaxAndMinValuesList(srcList, fastLength);
        var (highest2List, lowest2List) = GetMaxAndMinValuesList(srcList, slowLength);

        for (var i = 0; i < stockData.Count; i++)
        {
            var er = erList[i];
            var src = srcList[i];
            var highest1 = highest1List[i];
            var lowest1 = lowest1List[i];
            var highest2 = highest2List[i];
            var lowest2 = lowest2List[i];
            var prevStc1 = i >= 1 ? stcList[i - 1] : 0;
            var prevStc2 = i >= 2 ? stcList[i - 2] : 0;
            var a = (er * highest1) + ((1 - er) * highest2);
            var b = (er * lowest1) + ((1 - er) * lowest2);

            var stc = a - b != 0 ? MinOrMax((src - b) / (a - b), 1, 0) : 0;
            stcList.Add(stc);

            var signal = GetRsiSignal(stc - prevStc1, prevStc1 - prevStc2, stc, prevStc1, 0.8, 0.2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ast", stcList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(stcList);
        stockData.IndicatorName = IndicatorName.AdaptiveStochastic;

        return stockData;
    }


    /// <summary>
    /// Calculates the Bilateral Stochastic Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculateBilateralStochasticOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 100, int signalLength = 20)
    {
        List<double> bullList = new(stockData.Count);
        List<double> bearList = new(stockData.Count);
        List<double> rangeList = new(stockData.Count);
        List<double> maxList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);
        var (highestList, lowestList) = GetMaxAndMinValuesList(smaList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var highest = highestList[i];
            var lowest = lowestList[i];

            var range = highest - lowest;
            rangeList.Add(range);
        }

        var rangeSmaList = GetMovingAverageList(stockData, maType, length, rangeList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var sma = smaList[i];
            var highest = highestList[i];
            var lowest = lowestList[i];
            var rangeSma = rangeSmaList[i];

            var bull = rangeSma != 0 ? (sma / rangeSma) - (lowest / rangeSma) : 0;
            bullList.Add(bull);

            var bear = rangeSma != 0 ? Math.Abs((sma / rangeSma) - (highest / rangeSma)) : 0;
            bearList.Add(bear);

            var max = Math.Max(bull, bear);
            maxList.Add(max);
        }

        var signalList = GetMovingAverageList(stockData, maType, signalLength, maxList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var bull = bullList[i];
            var bear = bearList[i];
            var sig = signalList[i];

            var signal = GetConditionSignal(bull > bear || bull > sig, bear > bull || bull < sig);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Bull", bullList },
            { "Bear", bearList },
            { "Bso", maxList },
            { "Signal", signalList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(maxList);
        stockData.IndicatorName = IndicatorName.BilateralStochasticOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the DiNapoli Preferred Stochastic Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <returns></returns>
    public static StockData CalculateDiNapoliPreferredStochasticOscillator(this StockData stockData, int length1 = 8, int length2 = 3, int length3 = 3)
    {
        List<double> rList = new(stockData.Count);
        List<double> sList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var max = highestList[i];
            var min = lowestList[i];
            var fast = max - min != 0 ? MinOrMax((currentValue - min) / (max - min) * 100, 100, 0) : 0;

            var prevR = GetLastOrDefault(rList);
            var r = prevR + ((fast - prevR) / length2);
            rList.Add(r);

            var prevS = GetLastOrDefault(sList);
            var s = prevS + ((r - prevS) / length3);
            sList.Add(s);

            var signal = GetRsiSignal(r - s, prevR - prevS, r, prevR, 70, 30);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Dpso", rList },
            { "Signal", sList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(rList);
        stockData.IndicatorName = IndicatorName.DiNapoliPreferredStochasticOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Double Smoothed Stochastic
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="length4"></param>
    /// <returns></returns>
    public static StockData CalculateDoubleSmoothedStochastic(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 2,
        int length2 = 3, int length3 = 15, int length4 = 3)
    {
        List<double> dssList = new(stockData.Count);
        List<double> numList = new(stockData.Count);
        List<double> denomList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var highestHigh = highestList[i];
            var lowestLow = lowestList[i];

            var num = currentValue - lowestLow;
            numList.Add(num);

            var denom = highestHigh - lowestLow;
            denomList.Add(denom);
        }

        var ssNumList = GetMovingAverageList(stockData, maType, length2, numList);
        var ssDenomList = GetMovingAverageList(stockData, maType, length2, denomList);
        var dsNumList = GetMovingAverageList(stockData, maType, length3, ssNumList);
        var dsDenomList = GetMovingAverageList(stockData, maType, length3, ssDenomList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var dsNum = dsNumList[i];
            var dsDenom = dsDenomList[i];

            var dss = dsDenom != 0 ? MinOrMax(100 * dsNum / dsDenom, 100, 0) : 0;
            dssList.Add(dss);
        }

        var sdssList = GetMovingAverageList(stockData, maType, length4, dssList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var dss = dssList[i];
            var sdss = sdssList[i];
            var prevDss = i >= 1 ? dssList[i - 1] : 0;
            var prevSdss = i >= 1 ? sdssList[i - 1] : 0;

            var signal = GetRsiSignal(dss - sdss, prevDss - prevSdss, dss, prevDss, 70, 30);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Dss", dssList },
            { "Signal", sdssList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(dssList);
        stockData.IndicatorName = IndicatorName.DoubleSmoothedStochastic;

        return stockData;
    }


    /// <summary>
    /// Calculates the Double Stochastic Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateDoubleStochasticOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14,
        int smoothLength = 3)
    {
        List<double> doubleKList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var stochasticList = CalculateStochasticOscillator(stockData, maType, length: length).CustomValuesList;
        var (highestList, lowestList) = GetMaxAndMinValuesList(stochasticList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var slowK = stochasticList[i];
            var highestSlowK = highestList[i];
            var lowestSlowK = lowestList[i];

            var doubleK = highestSlowK - lowestSlowK != 0 ? MinOrMax((slowK - lowestSlowK) / (highestSlowK - lowestSlowK) * 100, 100, 0) : 0;
            doubleKList.Add(doubleK);
        }

        var doubleSlowKList = GetMovingAverageList(stockData, maType, smoothLength, doubleKList);
        var doubleKSignalList = GetMovingAverageList(stockData, maType, smoothLength, doubleSlowKList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var doubleSlowK = doubleSlowKList[i];
            var doubleKSignal = doubleKSignalList[i];
            var prevDoubleslowk = i >= 1 ? doubleSlowKList[i - 1] : 0;
            var prevDoubleKSignal = i >= 1 ? doubleKSignalList[i - 1] : 0;

            var signal = GetRsiSignal(doubleSlowK - doubleKSignal, prevDoubleslowk - prevDoubleKSignal, doubleSlowK, prevDoubleslowk, 80, 20);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Dso", doubleSlowKList },
            { "Signal", doubleKSignalList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(doubleSlowKList);
        stockData.IndicatorName = IndicatorName.DoubleStochasticOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the DMI Stochastic
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="length4"></param>
    /// <returns></returns>
    public static StockData CalculateDMIStochastic(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 10,
        int length2 = 10, int length3 = 3, int length4 = 3)
    {
        List<double> dmiOscillatorList = new(stockData.Count);
        List<double> fastKList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var adxList = CalculateAverageDirectionalIndex(stockData, maType, length1);
        var diPlusList = adxList.OutputValues["DiPlus"];
        var diMinusList = adxList.OutputValues["DiMinus"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var pdi = diPlusList[i];
            var ndi = diMinusList[i];

            var dmiOscillator = ndi - pdi;
            dmiOscillatorList.Add(dmiOscillator);
        }

        var (highestList, lowestList) = GetMaxAndMinValuesList(dmiOscillatorList, length2);
        for (var i = 0; i < stockData.Count; i++)
        {
            var highest = highestList[i];
            var lowest = lowestList[i];
            var dmiOscillator = dmiOscillatorList[i];

            var fastK = highest - lowest != 0 ? MinOrMax((dmiOscillator - lowest) / (highest - lowest) * 100, 100, 0) : 0;
            fastKList.Add(fastK);
        }

        var slowKList = GetMovingAverageList(stockData, maType, length3, fastKList);
        var dmiStochList = GetMovingAverageList(stockData, maType, length4, slowKList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var dmiStoch = dmiStochList[i];
            var prevDmiStoch1 = i >= 1 ? dmiStochList[i - 1] : 0;
            var prevDmiStoch2 = i >= 2 ? dmiStochList[i - 2] : 0;

            var signal = GetRsiSignal(dmiStoch - prevDmiStoch1, prevDmiStoch1 - prevDmiStoch2, dmiStoch, prevDmiStoch1, 90, 10);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "DmiStochastic", dmiStochList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(dmiStochList);
        stockData.IndicatorName = IndicatorName.DMIStochastic;

        return stockData;
    }

}

