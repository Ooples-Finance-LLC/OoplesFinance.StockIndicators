using OoplesFinance.StockIndicators.Compatibility;
using OoplesFinance.StockIndicators.Core;

namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Japanese Correlation Coefficient
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateJapaneseCorrelationCoefficient(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 50)
    {
        List<double> joList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        var length1 = MinOrMax((int)Math.Ceiling((double)length / 2));

        var hList = GetMovingAverageList(stockData, maType, length1, highList);
        var lList = GetMovingAverageList(stockData, maType, length1, lowList);
        var cList = GetMovingAverageList(stockData, maType, length1, inputList);
        var highestList = GetMaxAndMinValuesList(hList, length1).Item1;
        var lowestList = GetMaxAndMinValuesList(lList, length1).Item2;

        for (var i = 0; i < stockData.Count; i++)
        {
            var c = cList[i];
            var prevC = i >= length ? cList[i - length] : 0;
            var highest = highestList[i];
            var lowest = lowestList[i];
            var prevJo1 = i >= 1 ? joList[i - 1] : 0;
            var prevJo2 = i >= 2 ? joList[i - 2] : 0;
            var cChg = c - prevC;

            var jo = highest - lowest != 0 ? cChg / (highest - lowest) : 0;
            joList.Add(jo);

            var signal = GetCompareSignal(jo - prevJo1, prevJo1 - prevJo2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Jo", joList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(joList);
        stockData.IndicatorName = IndicatorName.JapaneseCorrelationCoefficient;

        return stockData;
    }


    /// <summary>
    /// Calculates the Jma Rsx Clone
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateJmaRsxClone(this StockData stockData, int length = 14)
    {
        List<double> rsxList = new(stockData.Count);
        List<double> f8List = new(stockData.Count);
        List<double> f28List = new(stockData.Count);
        List<double> f30List = new(stockData.Count);
        List<double> f38List = new(stockData.Count);
        List<double> f40List = new(stockData.Count);
        List<double> f48List = new(stockData.Count);
        List<double> f50List = new(stockData.Count);
        List<double> f58List = new(stockData.Count);
        List<double> f60List = new(stockData.Count);
        List<double> f68List = new(stockData.Count);
        List<double> f70List = new(stockData.Count);
        List<double> f78List = new(stockData.Count);
        List<double> f80List = new(stockData.Count);
        List<double> f88List = new(stockData.Count);
        List<double> f90_List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var f18 = (double)3 / (length + 2);
        var f20 = 1 - f18;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevRsx1 = i >= 1 ? rsxList[i - 1] : 0;
            var prevRsx2 = i >= 2 ? rsxList[i - 2] : 0;

            var prevF8 = GetLastOrDefault(f8List);
            var f8 = 100 * currentValue;
            f8List.Add(f8);

            var f10 = prevF8;
            var v8 = f8 - f10;

            var prevF28 = GetLastOrDefault(f28List);
            var f28 = (f20 * prevF28) + (f18 * v8);
            f28List.Add(f28);

            var prevF30 = GetLastOrDefault(f30List);
            var f30 = (f18 * f28) + (f20 * prevF30);
            f30List.Add(f30);

            var vC = (f28 * 1.5) - (f30 * 0.5);
            var prevF38 = GetLastOrDefault(f38List);
            var f38 = (f20 * prevF38) + (f18 * vC);
            f38List.Add(f38);

            var prevF40 = GetLastOrDefault(f40List);
            var f40 = (f18 * f38) + (f20 * prevF40);
            f40List.Add(f40);

            var v10 = (f38 * 1.5) - (f40 * 0.5);
            var prevF48 = GetLastOrDefault(f48List);
            var f48 = (f20 * prevF48) + (f18 * v10);
            f48List.Add(f48);

            var prevF50 = GetLastOrDefault(f50List);
            var f50 = (f18 * f48) + (f20 * prevF50);
            f50List.Add(f50);

            var v14 = (f48 * 1.5) - (f50 * 0.5);
            var prevF58 = GetLastOrDefault(f58List);
            var f58 = (f20 * prevF58) + (f18 * Math.Abs(v8));
            f58List.Add(f58);

            var prevF60 = GetLastOrDefault(f60List);
            var f60 = (f18 * f58) + (f20 * prevF60);
            f60List.Add(f60);

            var v18 = (f58 * 1.5) - (f60 * 0.5);
            var prevF68 = GetLastOrDefault(f68List);
            var f68 = (f20 * prevF68) + (f18 * v18);
            f68List.Add(f68);

            var prevF70 = GetLastOrDefault(f70List);
            var f70 = (f18 * f68) + (f20 * prevF70);
            f70List.Add(f70);

            var v1C = (f68 * 1.5) - (f70 * 0.5);
            var prevF78 = GetLastOrDefault(f78List);
            var f78 = (f20 * prevF78) + (f18 * v1C);
            f78List.Add(f78);

            var prevF80 = GetLastOrDefault(f80List);
            var f80 = (f18 * f78) + (f20 * prevF80);
            f80List.Add(f80);

            var v20 = (f78 * 1.5) - (f80 * 0.5);
            var prevF88 = GetLastOrDefault(f88List);
            var prevF90_ = GetLastOrDefault(f90_List);
            var f90_ = prevF90_ == 0 ? 1 : prevF88 <= prevF90_ ? prevF88 + 1 : prevF90_ + 1;
            f90_List.Add(f90_);

            double f88 = prevF90_ == 0 && length - 1 >= 5 ? length - 1 : 5;
            double f0 = f88 >= f90_ && f8 != f10 ? 1 : 0;
            var f90 = f88 == f90_ && f0 == 0 ? 0 : f90_;
            var v4_ = f88 < f90 && v20 > 0 ? MinOrMax(((v14 / v20) + 1) * 50, 100, 0) : 50;
            var rsx = v4_ > 100 ? 100 : v4_ < 0 ? 0 : v4_;
            rsxList.Add(rsx);

            var signal = GetRsiSignal(rsx - prevRsx1, prevRsx1 - prevRsx2, rsx, prevRsx1, 70, 30);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Rsx", rsxList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(rsxList);
        stockData.IndicatorName = IndicatorName.JmaRsxClone;

        return stockData;
    }


    /// <summary>
    /// Calculates the Jrc Fractal Dimension
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateJrcFractalDimension(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length1 = 20, int length2 = 5, int smoothLength = 5)
    {
        List<double> smallSumList = new(stockData.Count);
        List<double> smallRangeList = new(stockData.Count);
        List<double> fdList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        var wind1 = MinOrMax((length2 - 1) * length1);
        var wind2 = MinOrMax(length2 * length1);
        var nLog = Math.Log(length2);

        var (highest1List, lowest1List) = GetMaxAndMinValuesList(highList, lowList, length1);
        var (highest2List, lowest2List) = GetMaxAndMinValuesList(highList, lowList, wind2);

        for (var i = 0; i < stockData.Count; i++)
        {
            var highest1 = highest1List[i];
            var lowest1 = lowest1List[i];
            var prevValue1 = i >= length1 ? inputList[i - length1] : 0;
            var highest2 = highest2List[i];
            var lowest2 = lowest2List[i];
            var prevValue2 = i >= wind2 ? inputList[i - wind2] : 0;
            var bigRange = Math.Max(prevValue2, highest2) - Math.Min(prevValue2, lowest2);

            var prevSmallRange = i >= wind1 ? smallRangeList[i - wind1] : 0;
            var smallRange = Math.Max(prevValue1, highest1) - Math.Min(prevValue1, lowest1);
            smallRangeList.Add(smallRange);

            var prevSmallSum = i >= 1 ? GetLastOrDefault(smallSumList) : smallRange;
            var smallSum = prevSmallSum + smallRange - prevSmallRange;
            smallSumList.Add(smallSum);

            var value1 = wind1 != 0 ? smallSum / wind1 : 0;
            var value2 = value1 != 0 ? bigRange / value1 : 0;
            var temp = value2 > 0 ? Math.Log(value2) : 0;

            var fd = nLog != 0 ? 2 - (temp / nLog) : 0;
            fdList.Add(fd);
        }

        var jrcfdList = GetMovingAverageList(stockData, maType, smoothLength, fdList);
        var jrcfdSignalList = GetMovingAverageList(stockData, maType, smoothLength, jrcfdList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var jrcfd = jrcfdList[i];
            var jrcfdSignal = jrcfdSignalList[i];
            var prevJrcfd = i >= 1 ? jrcfdList[i - 1] : 0;
            var prevJrcfdSignal = i >= 1 ? jrcfdSignalList[i - 1] : 0;

            var signal = GetCompareSignal(jrcfd - jrcfdSignal, prevJrcfd - prevJrcfdSignal, true);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Jrcfd", jrcfdList },
            { "Signal", jrcfdSignalList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(jrcfdList);
        stockData.IndicatorName = IndicatorName.JrcFractalDimension;

        return stockData;
    }


    /// <summary>
    /// Calculates the Inertia Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateInertiaIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.LinearRegression,
        int length = 20)
    {
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var rviList = CalculateRelativeVolatilityIndexV2(stockData).CustomValuesList;
        var inertiaList = GetMovingAverageList(stockData, maType, length, rviList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var inertiaIndicator = inertiaList[i];
            var prevInertiaIndicator1 = i >= 1 ? inertiaList[i - 1] : 0;
            var prevInertiaIndicator2 = i >= 2 ? inertiaList[i - 2] : 0;

            var signal = GetCompareSignal(inertiaIndicator - prevInertiaIndicator1, prevInertiaIndicator1 - prevInertiaIndicator2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Inertia", inertiaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(inertiaList);
        stockData.IndicatorName = IndicatorName.InertiaIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Internal Bar Strength Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateInternalBarStrengthIndicator(this StockData stockData, int length = 14, int smoothLength = 3)
    {
        List<double> ibsiList = new(stockData.Count);
        List<double> ibsEmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var ibsSumWindow = new RollingSum();

        for (var i = 0; i < stockData.Count; i++)
        {
            var close = inputList[i];
            var high = highList[i];
            var low = lowList[i];

            var ibs = high - low != 0 ? (close - low) / (high - low) * 100 : 0;
            ibsSumWindow.Add(ibs);

            var prevIbsi = GetLastOrDefault(ibsiList);
            var ibsi = ibsSumWindow.Average(length);
            ibsiList.Add(ibsi);

            var prevIbsiEma = GetLastOrDefault(ibsEmaList);
            var ibsiEma = CalculateEMA(ibsi, prevIbsiEma, smoothLength);
            ibsEmaList.Add(ibsiEma);

            var signal = GetRsiSignal(ibsi - ibsiEma, prevIbsi - prevIbsiEma, ibsi, prevIbsi, 70, 30);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ibs", ibsiList },
            { "Signal", ibsEmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(ibsiList);
        stockData.IndicatorName = IndicatorName.InternalBarStrengthIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Inverse Fisher Fast Z Score
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateInverseFisherFastZScore(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 50)
    {
        List<double> ifzList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var length1 = MinOrMax((int)Math.Ceiling((double)length / 2));

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);
        stockData.SetCustomValues(smaList);
        var stdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        stockData.SetCustomValues(smaList);
        var linreg1List = CalculateLinearRegression(stockData, length).CustomValuesList;
        stockData.SetCustomValues(smaList);
        var linreg2List = CalculateLinearRegression(stockData, length1).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var linreg1 = linreg1List[i];
            var linreg2 = linreg2List[i];
            var stdDev = stdDevList[i];
            var fz = stdDev != 0 ? (linreg2 - linreg1) / stdDev / 2 : 0;
            var prevIfz1 = i >= 1 ? ifzList[i - 1] : 0;
            var prevIfz2 = i >= 2 ? ifzList[i - 2] : 0;

            var ifz = Exp(10 * fz) + 1 != 0 ? (Exp(10 * fz) - 1) / (Exp(10 * fz) + 1) : 0;
            ifzList.Add(ifz);

            var signal = GetCompareSignal(ifz - prevIfz1, prevIfz1 - prevIfz2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Iffzs", ifzList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(ifzList);
        stockData.IndicatorName = IndicatorName.InverseFisherFastZScore;

        return stockData;
    }


    /// <summary>
    /// Calculates the Inverse Fisher Z Score
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateInverseFisherZScore(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 100)
    {
        List<double> fList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);
        var stdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var sma = smaList[i];
            var stdDev = stdDevList[i];
            var prevF1 = i >= 1 ? fList[i - 1] : 0;
            var prevF2 = i >= 2 ? fList[i - 2] : 0;
            var z = stdDev != 0 ? (currentValue - sma) / stdDev : 0;
            var expZ = Exp(2 * z);

            var f = expZ + 1 != 0 ? MinOrMax((((expZ - 1) / (expZ + 1)) + 1) * 50, 100, 0) : 0;
            fList.Add(f);

            var signal = GetRsiSignal(f - prevF1, prevF1 - prevF2, f, prevF1, 80, 20);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ifzs", fList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(fList);
        stockData.IndicatorName = IndicatorName.InverseFisherZScore;

        return stockData;
    }


    /// <summary>
    /// Calculates the Insync Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="signalLength"></param>
    /// <param name="emoLength"></param>
    /// <param name="mfiLength"></param>
    /// <param name="bbLength"></param>
    /// <param name="cciLength"></param>
    /// <param name="dpoLength"></param>
    /// <param name="rocLength"></param>
    /// <param name="rsiLength"></param>
    /// <param name="stochLength"></param>
    /// <param name="stochKLength"></param>
    /// <param name="stochDLength"></param>
    /// <param name="smaLength"></param>
    /// <param name="stdDevMult"></param>
    /// <param name="divisor"></param>
    /// <returns></returns>
    public static StockData CalculateInsyncIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int fastLength = 12, int slowLength = 26, int signalLength = 9, int emoLength = 14, int mfiLength = 20, int bbLength = 20,
        int cciLength = 14, int dpoLength = 18, int rocLength = 10, int rsiLength = 14, int stochLength = 14, int stochKLength = 1,
        int stochDLength = 3, int smaLength = 10, double stdDevMult = 2, double divisor = 10000)
    {
        List<double> iidxList = new(stockData.Count);
        List<double> tempMacdList = new(stockData.Count);
        List<double> tempDpoList = new(stockData.Count);
        List<double> tempRocList = new(stockData.Count);
        List<double> pdoinsbList = new(stockData.Count);
        List<double> pdoinssList = new(stockData.Count);
        List<double> emoList = new(stockData.Count);
        List<double> emoSmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var emoSumWindow = new RollingSum();
        var macdSumWindow = new RollingSum();
        var dpoSumWindow = new RollingSum();
        var rocSumWindow = new RollingSum();

        var rsiList = CalculateRelativeStrengthIndex(stockData, length: rsiLength).CustomValuesList;
        var cciList = CalculateCommodityChannelIndex(stockData, length: cciLength).CustomValuesList;
        var mfiList = CalculateMoneyFlowIndex(stockData, length: mfiLength).CustomValuesList;
        var macdList = CalculateMovingAverageConvergenceDivergence(stockData, fastLength: fastLength, slowLength: slowLength,
            signalLength: signalLength).CustomValuesList;
        var bbIndicatorList = CalculateBollingerBandsPercentB(stockData, stdDevMult: stdDevMult, length: bbLength).CustomValuesList;
        var dpoList = CalculateDetrendedPriceOscillator(stockData, length: dpoLength).CustomValuesList;
        var rocList = CalculateRateOfChange(stockData, length: rocLength).CustomValuesList;
        var stochasticList = CalculateStochasticOscillator(stockData, length: stochLength, smoothLength1: stochKLength, smoothLength2: stochDLength);
        var stochKList = stochasticList.OutputValues["FastD"];
        var stochDList = stochasticList.OutputValues["SlowD"];
        var emvList = CalculateEaseOfMovement(stockData, length: emoLength, divisor: divisor).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var bolins2 = bbIndicatorList[i];
            var prevPdoinss10 = i >= smaLength ? pdoinssList[i - smaLength] : 0;
            var prevPdoinsb10 = i >= smaLength ? pdoinsbList[i - smaLength] : 0;
            var cci = cciList[i];
            var mfi = mfiList[i];
            var rsi = rsiList[i];
            var stochD = stochDList[i];
            var stochK = stochKList[i];
            var prevIidx1 = i >= 1 ? iidxList[i - 1] : 0;
            var prevIidx2 = i >= 2 ? iidxList[i - 2] : 0;
            double bolinsll = bolins2 < 0.05 ? -5 : bolins2 > 0.95 ? 5 : 0;
            double cciins = cci > 100 ? 5 : cci < -100 ? -5 : 0;

            var emo = emvList[i];
            emoList.Add(emo);

            emoSumWindow.Add(emo);
            var emoSma = emoSumWindow.Average(smaLength);
            emoSmaList.Add(emoSma);

            var emvins2 = emo - emoSma;
            double emvinsb = emvins2 < 0 ? emoSma < 0 ? -5 : 0 : emoSma > 0 ? 5 : 0;

            var macd = macdList[i];
            tempMacdList.Add(macd);

            macdSumWindow.Add(macd);
            var macdSma = macdSumWindow.Average(smaLength);
            var macdins2 = macd - macdSma;
            double macdinsb = macdins2 < 0 ? macdSma < 0 ? -5 : 0 : macdSma > 0 ? 5 : 0;
            double mfiins = mfi > 80 ? 5 : mfi < 20 ? -5 : 0;

            var dpo = dpoList[i];
            tempDpoList.Add(dpo);

            dpoSumWindow.Add(dpo);
            var dpoSma = dpoSumWindow.Average(smaLength);
            var pdoins2 = dpo - dpoSma;
            double pdoinsb = pdoins2 < 0 ? dpoSma < 0 ? -5 : 0 : dpoSma > 0 ? 5 : 0;
            pdoinsbList.Add(pdoinsb);

            double pdoinss = pdoins2 > 0 ? dpoSma > 0 ? 5 : 0 : dpoSma < 0 ? -5 : 0;
            pdoinssList.Add(pdoinss);

            var roc = rocList[i];
            tempRocList.Add(roc);

            rocSumWindow.Add(roc);
            var rocSma = rocSumWindow.Average(smaLength);
            var rocins2 = roc - rocSma;
            double rocinsb = rocins2 < 0 ? rocSma < 0 ? -5 : 0 : rocSma > 0 ? 5 : 0;
            double rsiins = rsi > 70 ? 5 : rsi < 30 ? -5 : 0;
            double stopdins = stochD > 80 ? 5 : stochD < 20 ? -5 : 0;
            double stopkins = stochK > 80 ? 5 : stochK < 20 ? -5 : 0;

            var iidx = 50 + cciins + bolinsll + rsiins + stopkins + stopdins + mfiins + emvinsb + rocinsb + prevPdoinss10 + prevPdoinsb10 + macdinsb;
            iidxList.Add(iidx);

            var signal = GetRsiSignal(iidx - prevIidx1, prevIidx1 - prevIidx2, iidx, prevIidx1, 95, 5);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Iidx", iidxList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(iidxList);
        stockData.IndicatorName = IndicatorName.InsyncIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Gann Swing Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateGannSwingOscillator(this StockData stockData, int length = 5)
    {
        List<double> gannSwingOscillatorList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (_, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var highestHigh = highestList[i];
            var lowestLow = lowestList[i];
            var prevHighest1 = i >= 1 ? highestList[i - 1] : 0;
            var prevLowest1 = i >= 1 ? lowestList[i - 1] : 0;
            var prevHighest2 = i >= 2 ? highestList[i - 2] : 0;
            var prevLowest2 = i >= 2 ? lowestList[i - 2] : 0;

            var prevGso = GetLastOrDefault(gannSwingOscillatorList);
            var gso = prevHighest2 > prevHighest1 && highestHigh > prevHighest1 ? 1 :
                prevLowest2 < prevLowest1 && lowestLow < prevLowest1 ? -1 : prevGso;
            gannSwingOscillatorList.Add(gso);

            var signal = GetCompareSignal(gso, prevGso);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Gso", gannSwingOscillatorList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(gannSwingOscillatorList);
        stockData.IndicatorName = IndicatorName.GannSwingOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Gann HiLo Activator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateGannHiLoActivator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 3)
    {
        List<double> ghlaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        var highMaList = GetMovingAverageList(stockData, maType, length, highList);
        var lowMaList = GetMovingAverageList(stockData, maType, length, lowList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var highMa = highMaList[i];
            var lowMa = lowMaList[i];
            var prevHighMa = i >= 1 ? highMaList[i - 1] : 0;
            var prevLowMa = i >= 1 ? lowMaList[i - 1] : 0;
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevGhla = GetLastOrDefault(ghlaList);
            var ghla = currentValue > prevHighMa ? lowMa : currentValue < prevLowMa ? highMa : prevGhla;
            ghlaList.Add(ghla);

            var signal = GetCompareSignal(currentValue - ghla, prevValue - prevGhla);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ghla", ghlaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(ghlaList);
        stockData.IndicatorName = IndicatorName.GannHiLoActivator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Grover Llorens Cycle Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <param name="mult"></param>
    /// <returns></returns>
    public static StockData CalculateGroverLlorensCycleOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod,
        int length = 100, int smoothLength = 20, double mult = 10)
    {
        List<double> tsList = new(stockData.Count);
        List<double> oscList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var atrList = CalculateAverageTrueRange(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var atr = atrList[i];
            var prevTs = i >= 1 ? tsList[i - 1] : currentValue;
            var diff = currentValue - prevTs;

            var ts = diff > 0 ? prevTs - (atr * mult) : diff < 0 ? prevTs + (atr * mult) : prevTs;
            tsList.Add(ts);

            var osc = currentValue - ts;
            oscList.Add(osc);
        }

        var smoList = GetMovingAverageList(stockData, maType, smoothLength, oscList);
        stockData.SetCustomValues(smoList);
        var rsiList = CalculateRelativeStrengthIndex(stockData, maType, length: smoothLength).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var rsi = rsiList[i];
            var prevRsi1 = i >= 1 ? rsiList[i - 1] : 0;
            var prevRsi2 = i >= 2 ? rsiList[i - 2] : 0;

            var signal = GetRsiSignal(rsi - prevRsi1, prevRsi1 - prevRsi2, rsi, prevRsi1, 80, 20);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Glco", rsiList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(rsiList);
        stockData.IndicatorName = IndicatorName.GroverLlorensCycleOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Grover Llorens Activator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="mult"></param>
    /// <returns></returns>
    public static StockData CalculateGroverLlorensActivator(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod,
        int length = 100, double mult = 5)
    {
        List<double> tsList = new(stockData.Count);
        List<double> diffList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var atrList = CalculateAverageTrueRange(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var atr = atrList[i];
            var prevTs = i >= 1 ? tsList[i - 1] : currentValue;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            prevTs = prevTs == 0 ? prevValue : prevTs;

            var prevDiff = GetLastOrDefault(diffList);
            var diff = currentValue - prevTs;
            diffList.Add(diff);

            var ts = diff > 0 ? prevTs - (atr * mult) : diff < 0 ? prevTs + (atr * mult) : prevTs;
            tsList.Add(ts);

            var signal = GetCompareSignal(diff, prevDiff);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Gla", tsList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(tsList);
        stockData.IndicatorName = IndicatorName.GroverLlorensActivator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Guppy Count Back Line
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateGuppyCountBackLine(this StockData stockData, int length = 21)
    {
        List<double> cblList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var hh = highestList[i];
            var ll = lowestList[i];

            var prevCbl = GetLastOrDefault(cblList);
            int hCount = 0, lCount = 0;
            var cbl = currentValue;
            for (var j = 0; j <= length; j++)
            {
                var currentLow = i >= j ? lowList[i - j] : 0;
                var currentHigh = i >= j ? highList[i - j] : 0;

                if (currentLow == ll)
                {
                    for (var k = j + 1; k <= j + length; k++)
                    {
                        var prevHigh = i >= k ? highList[i - k] : 0;
                        lCount += prevHigh > currentHigh ? 1 : 0;
                        if (lCount == 2)
                        {
                            cbl = prevHigh;
                            break;
                        }
                    }
                }

                if (currentHigh == hh)
                {
                    for (var k = j + 1; k <= j + length; k++)
                    {
                        var prevLow = i >= k ? lowList[i - k] : 0;
                        hCount += prevLow > currentLow ? 1 : 0;
                        if (hCount == 2)
                        {
                            cbl = prevLow;
                            break;
                        }
                    }
                }
            }
            cblList.Add(cbl);

            var signal = GetCompareSignal(currentValue - cbl, prevValue - prevCbl);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Cbl", cblList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(cblList);
        stockData.IndicatorName = IndicatorName.GuppyCountBackLine;

        return stockData;
    }


    /// <summary>
    /// Calculates the Guppy Multiple Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="length4"></param>
    /// <param name="length5"></param>
    /// <param name="length6"></param>
    /// <param name="length7"></param>
    /// <param name="length8"></param>
    /// <param name="length9"></param>
    /// <param name="length10"></param>
    /// <param name="length11"></param>
    /// <param name="length12"></param>
    /// <param name="length13"></param>
    /// <param name="length14"></param>
    /// <param name="length15"></param>
    /// <param name="length16"></param>
    /// <param name="length17"></param>
    /// <param name="length18"></param>
    /// <param name="length19"></param>
    /// <param name="length20"></param>
    /// <param name="length21"></param>
    /// <param name="length22"></param>
    /// <param name="length23"></param>
    /// <param name="length24"></param>
    /// <param name="length25"></param>
    /// <param name="length26"></param>
    /// <param name="length27"></param>
    /// <param name="length28"></param>
    /// <param name="length29"></param>
    /// <param name="length30"></param>
    /// <param name="length31"></param>
    /// <param name="length32"></param>
    /// <param name="length33"></param>
    /// <param name="length34"></param>
    /// <param name="length35"></param>
    /// <param name="smoothLength"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculateGuppyMultipleMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 3, int length2 = 5, int length3 = 7, int length4 = 8, int length5 = 9, int length6 = 10, int length7 = 11, int length8 = 12,
        int length9 = 13, int length10 = 15, int length11 = 17, int length12 = 19, int length13 = 21, int length14 = 23, int length15 = 25,
        int length16 = 28, int length17 = 30, int length18 = 31, int length19 = 34, int length20 = 35, int length21 = 37, int length22 = 40,
        int length23 = 43, int length24 = 45, int length25 = 46, int length26 = 49, int length27 = 50, int length28 = 52, int length29 = 55,
        int length30 = 58, int length31 = 60, int length32 = 61, int length33 = 64, int length34 = 67, int length35 = 70, int smoothLength = 1,
        int signalLength = 13)
    {
        List<double> superGmmaFastList = new(stockData.Count);
        List<double> superGmmaSlowList = new(stockData.Count);
        List<double> superGmmaOscRawList = new(stockData.Count);
        List<double> superGmmaOscList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var superGmmaOscRawSumWindow = new RollingSum();

        var ema3List = GetMovingAverageList(stockData, maType, length1, inputList);
        var ema5List = GetMovingAverageList(stockData, maType, length2, inputList);
        var ema7List = GetMovingAverageList(stockData, maType, length3, inputList);
        var ema9List = GetMovingAverageList(stockData, maType, length5, inputList);
        var ema11List = GetMovingAverageList(stockData, maType, length7, inputList);
        var ema13List = GetMovingAverageList(stockData, maType, length9, inputList);
        var ema15List = GetMovingAverageList(stockData, maType, length10, inputList);
        var ema17List = GetMovingAverageList(stockData, maType, length11, inputList);
        var ema19List = GetMovingAverageList(stockData, maType, length12, inputList);
        var ema21List = GetMovingAverageList(stockData, maType, length13, inputList);
        var ema23List = GetMovingAverageList(stockData, maType, length14, inputList);
        var ema25List = GetMovingAverageList(stockData, maType, length15, inputList);
        var ema28List = GetMovingAverageList(stockData, maType, length16, inputList);
        var ema31List = GetMovingAverageList(stockData, maType, length18, inputList);
        var ema34List = GetMovingAverageList(stockData, maType, length19, inputList);
        var ema37List = GetMovingAverageList(stockData, maType, length21, inputList);
        var ema40List = GetMovingAverageList(stockData, maType, length22, inputList);
        var ema43List = GetMovingAverageList(stockData, maType, length23, inputList);
        var ema46List = GetMovingAverageList(stockData, maType, length25, inputList);
        var ema49List = GetMovingAverageList(stockData, maType, length26, inputList);
        var ema52List = GetMovingAverageList(stockData, maType, length28, inputList);
        var ema55List = GetMovingAverageList(stockData, maType, length29, inputList);
        var ema58List = GetMovingAverageList(stockData, maType, length30, inputList);
        var ema61List = GetMovingAverageList(stockData, maType, length32, inputList);
        var ema64List = GetMovingAverageList(stockData, maType, length33, inputList);
        var ema67List = GetMovingAverageList(stockData, maType, length34, inputList);
        var ema70List = GetMovingAverageList(stockData, maType, length35, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var emaF1 = ema3List[i];
            var emaF2 = ema5List[i];
            var emaF3 = ema7List[i];
            var emaF4 = ema9List[i];
            var emaF5 = ema11List[i];
            var emaF6 = ema13List[i];
            var emaF7 = ema15List[i];
            var emaF8 = ema17List[i];
            var emaF9 = ema19List[i];
            var emaF10 = ema21List[i];
            var emaF11 = ema23List[i];
            var emaS1 = ema25List[i];
            var emaS2 = ema28List[i];
            var emaS3 = ema31List[i];
            var emaS4 = ema34List[i];
            var emaS5 = ema37List[i];
            var emaS6 = ema40List[i];
            var emaS7 = ema43List[i];
            var emaS8 = ema46List[i];
            var emaS9 = ema49List[i];
            var emaS10 = ema52List[i];
            var emaS11 = ema55List[i];
            var emaS12 = ema58List[i];
            var emaS13 = ema61List[i];
            var emaS14 = ema64List[i];
            var emaS15 = ema67List[i];
            var emaS16 = ema70List[i];

            var superGmmaFast = (emaF1 + emaF2 + emaF3 + emaF4 + emaF5 + emaF6 + emaF7 + emaF8 + emaF9 + emaF10 + emaF11) / 11;
            superGmmaFastList.Add(superGmmaFast);

            var superGmmaSlow = (emaS1 + emaS2 + emaS3 + emaS4 + emaS5 + emaS6 + emaS7 + emaS8 + emaS9 + emaS10 + emaS11 + emaS12 + emaS13 +
                                 emaS14 + emaS15 + emaS16) / 16;
            superGmmaSlowList.Add(superGmmaSlow);

            var superGmmaOscRaw = superGmmaSlow != 0 ? (superGmmaFast - superGmmaSlow) / superGmmaSlow * 100 : 0;
            superGmmaOscRawList.Add(superGmmaOscRaw);

            superGmmaOscRawSumWindow.Add(superGmmaOscRaw);
            var superGmmaOsc = superGmmaOscRawSumWindow.Average(smoothLength);
            superGmmaOscList.Add(superGmmaOsc);
        }

        var superGmmaSignalList = GetMovingAverageList(stockData, maType, signalLength, superGmmaOscRawList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var superGmmaOsc = superGmmaOscList[i];
            var superGmmaSignal = superGmmaSignalList[i];
            var prevSuperGmmaOsc = i >= 1 ? superGmmaOscList[i - 1] : 0;
            var prevSuperGmmaSignal = i >= 1 ? superGmmaSignalList[i - 1] : 0;

            var signal = GetCompareSignal(superGmmaOsc - superGmmaSignal, prevSuperGmmaOsc - prevSuperGmmaSignal);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "SuperGmmaOsc", superGmmaOscList },
            { "SuperGmmaSignal", superGmmaSignalList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(superGmmaOscList);
        stockData.IndicatorName = IndicatorName.GuppyMultipleMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Guppy Distance Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="length4"></param>
    /// <param name="length5"></param>
    /// <param name="length6"></param>
    /// <param name="length7"></param>
    /// <param name="length8"></param>
    /// <param name="length9"></param>
    /// <param name="length10"></param>
    /// <param name="length11"></param>
    /// <param name="length12"></param>
    /// <returns></returns>
    public static StockData CalculateGuppyDistanceIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 3, int length2 = 5, int length3 = 8, int length4 = 10, int length5 = 12, int length6 = 15, int length7 = 30, int length8 = 35,
        int length9 = 40, int length10 = 45, int length11 = 11, int length12 = 60)
    {
        List<double> fastDistanceList = new(stockData.Count);
        List<double> slowDistanceList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var ema3List = GetMovingAverageList(stockData, maType, length1, inputList);
        var ema5List = GetMovingAverageList(stockData, maType, length2, inputList);
        var ema8List = GetMovingAverageList(stockData, maType, length3, inputList);
        var ema10List = GetMovingAverageList(stockData, maType, length4, inputList);
        var ema12List = GetMovingAverageList(stockData, maType, length5, inputList);
        var ema15List = GetMovingAverageList(stockData, maType, length6, inputList);
        var ema30List = GetMovingAverageList(stockData, maType, length7, inputList);
        var ema35List = GetMovingAverageList(stockData, maType, length8, inputList);
        var ema40List = GetMovingAverageList(stockData, maType, length9, inputList);
        var ema45List = GetMovingAverageList(stockData, maType, length10, inputList);
        var ema50List = GetMovingAverageList(stockData, maType, length11, inputList);
        var ema60List = GetMovingAverageList(stockData, maType, length12, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var ema1 = ema3List[i];
            var ema2 = ema5List[i];
            var ema3 = ema8List[i];
            var ema4 = ema10List[i];
            var ema5 = ema12List[i];
            var ema6 = ema15List[i];
            var ema7 = ema30List[i];
            var ema8 = ema35List[i];
            var ema9 = ema40List[i];
            var ema10 = ema45List[i];
            var ema11 = ema50List[i];
            var ema12 = ema60List[i];
            var diff12 = Math.Abs(ema1 - ema2);
            var diff23 = Math.Abs(ema2 - ema3);
            var diff34 = Math.Abs(ema3 - ema4);
            var diff45 = Math.Abs(ema4 - ema5);
            var diff56 = Math.Abs(ema5 - ema6);
            var diff78 = Math.Abs(ema7 - ema8);
            var diff89 = Math.Abs(ema8 - ema9);
            var diff910 = Math.Abs(ema9 - ema10);
            var diff1011 = Math.Abs(ema10 - ema11);
            var diff1112 = Math.Abs(ema11 - ema12);

            var fastDistance = diff12 + diff23 + diff34 + diff45 + diff56;
            fastDistanceList.Add(fastDistance);

            var slowDistance = diff78 + diff89 + diff910 + diff1011 + diff1112;
            slowDistanceList.Add(slowDistance);

            var colFastL = ema1 > ema2 && ema2 > ema3 && ema3 > ema4 && ema4 > ema5 && ema5 > ema6;
            var colFastS = ema1 < ema2 && ema2 < ema3 && ema3 < ema4 && ema4 < ema5 && ema5 < ema6;
            var colSlowL = ema7 > ema8 && ema8 > ema9 && ema9 > ema10 && ema10 > ema11 && ema11 > ema12;
            var colSlowS = ema7 < ema8 && ema8 < ema9 && ema9 < ema10 && ema10 < ema11 && ema11 < ema12;

            var signal = GetConditionSignal(colSlowL || colFastL, colSlowS || colFastS);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "FastDistance", fastDistanceList },
            { "SlowDistance", slowDistanceList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.GuppyDistanceIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the G Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateGOscillator(this StockData stockData, int length = 14)
    {
        List<double> bSumList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var bSumWindow = new RollingSum();

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevBSum1 = i >= 1 ? bSumList[i - 1] : 0;
            var prevBSum2 = i >= 2 ? bSumList[i - 2] : 0;

            var b = currentValue > prevValue ? (double)100 / length : 0;
            bSumWindow.Add(b);

            var bSum = bSumWindow.Sum(length);
            bSumList.Add(bSum);

            var signal = GetRsiSignal(bSum - prevBSum1, prevBSum1 - prevBSum2, bSum, prevBSum1, 80, 20);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "GOsc", bSumList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(bSumList);
        stockData.IndicatorName = IndicatorName.GOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Gain Loss Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculateGainLossMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod,
        int length = 14, int signalLength = 7)
    {
        List<double> gainLossList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var gainLoss = currentValue + prevValue != 0 ? MinPastValues(i, 1, currentValue - prevValue) / ((currentValue + prevValue) / 2) * 100 : 0;
            gainLossList.Add(gainLoss);
        }

        var gainLossAvgList = GetMovingAverageList(stockData, maType, length, gainLossList);
        var gainLossAvgSignalList = GetMovingAverageList(stockData, maType, signalLength, gainLossAvgList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var gainLossSignal = gainLossAvgSignalList[i];
            var prevGainLossSignal1 = i >= 1 ? gainLossAvgSignalList[i - 1] : 0;
            var prevGainLossSignal2 = i >= 2 ? gainLossAvgSignalList[i - 2] : 0;

            var signal = GetCompareSignal(gainLossSignal - prevGainLossSignal1, prevGainLossSignal1 - prevGainLossSignal2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Glma", gainLossAvgList },
            { "Signal", gainLossAvgSignalList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(gainLossAvgList);
        stockData.IndicatorName = IndicatorName.GainLossMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the High Low Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateHighLowIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 10)
    {
        List<double> advDiffList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (_, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);
        var advSumWindow = new RollingSum();
        var loSumWindow = new RollingSum();

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevHighest = i >= 1 ? highestList[i - 1] : 0;
            var prevLowest = i >= 1 ? lowestList[i - 1] : 0;
            var highest = highestList[i];
            var lowest = lowestList[i];

            double adv = highest > prevHighest ? 1 : 0;
            advSumWindow.Add(adv);

            double lo = lowest < prevLowest ? 1 : 0;
            loSumWindow.Add(lo);

            var advSum = advSumWindow.Sum(length);
            var loSum = loSumWindow.Sum(length);

            var advDiff = advSum + loSum != 0 ? MinOrMax(advSum / (advSum + loSum) * 100, 100, 0) : 0;
            advDiffList.Add(advDiff);
        }

        var zmbtiList = GetMovingAverageList(stockData, maType, length, advDiffList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var zmbti = zmbtiList[i];
            var prevZmbti1 = i >= 1 ? zmbtiList[i - 1] : 0;
            var prevZmbti2 = i >= 2 ? zmbtiList[i - 2] : 0;

            var signal = GetRsiSignal(zmbti - prevZmbti1, prevZmbti1 - prevZmbti2, zmbti, prevZmbti1, 70, 30);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Zmbti", zmbtiList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(zmbtiList);
        stockData.IndicatorName = IndicatorName.HighLowIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Forecast Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateForecastOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 3)
    {
        List<double> pfList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var pf = currentValue != 0 ? 100 * MinPastValues(i, 1, currentValue - prevValue) / currentValue : 0;
            pfList.Add(pf);
        }

        var pfSmaList = GetMovingAverageList(stockData, maType, length, pfList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var pfSma = pfSmaList[i];
            var prevPfSma = i >= 1 ? pfSmaList[i - 1] : 0;

            var signal = GetCompareSignal(pfSma, prevPfSma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Fo", pfList },
            { "Signal", pfSmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(pfList);
        stockData.IndicatorName = IndicatorName.ForecastOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Fast and Slow Kurtosis Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="ratio"></param>
    /// <returns></returns>
    public static StockData CalculateFastandSlowKurtosisOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length = 3, double ratio = 0.03)
    {
        List<double> fskList = new(stockData.Count);
        List<double> momentumList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= length ? inputList[i - length] : 0;

            var prevMomentum = GetLastOrDefault(momentumList);
            var momentum = MinPastValues(i, length, currentValue - prevValue);
            momentumList.Add(momentum);

            var prevFsk = GetLastOrDefault(fskList);
            var fsk = (ratio * (momentum - prevMomentum)) + ((1 - ratio) * prevFsk);
            fskList.Add(fsk);
        }

        var fskSignalList = GetMovingAverageList(stockData, maType, length, fskList);
        for (var i = 0; i < fskSignalList.Count; i++)
        {
            var fsk = fskList[i];
            var fskSignal = fskSignalList[i];
            var prevFsk = i >= 1 ? fskList[i - 1] : 0;
            var prevFskSignal = i >= 1 ? fskSignalList[i - 1] : 0;

            var signal = GetCompareSignal(fsk - fskSignal, prevFsk - prevFskSignal);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Fsk", fskList },
            { "Signal", fskSignalList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(fskList);
        stockData.IndicatorName = IndicatorName.FastandSlowKurtosisOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Fast and Slow Relative Strength Index Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="length4"></param>
    /// <returns></returns>
    public static StockData CalculateFastandSlowRelativeStrengthIndexOscillator(this StockData stockData,
        MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length1 = 3, int length2 = 6, int length3 = 9, int length4 = 6)
    {
        List<double> fsrsiList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var rsiList = CalculateRelativeStrengthIndex(stockData, maType, length: length3).CustomValuesList;
        var fskList = CalculateFastandSlowKurtosisOscillator(stockData, maType, length: length1).CustomValuesList;
        var v4List = GetMovingAverageList(stockData, maType, length2, fskList);

        for (var i = 0; i < v4List.Count; i++)
        {
            var rsi = rsiList[i];
            var v4 = v4List[i];

            var fsrsi = (10000 * v4) + rsi;
            fsrsiList.Add(fsrsi);
        }

        var fsrsiSignalList = GetMovingAverageList(stockData, maType, length4, fsrsiList);
        for (var i = 0; i < fsrsiSignalList.Count; i++)
        {
            var fsrsi = fsrsiList[i];
            var fsrsiSignal = fsrsiSignalList[i];
            var prevFsrsi = i >= 1 ? fsrsiList[i - 1] : 0;
            var prevFsrsiSignal = i >= 1 ? fsrsiSignalList[i - 1] : 0;

            var signal = GetCompareSignal(fsrsi - fsrsiSignal, prevFsrsi - prevFsrsiSignal);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Fsrsi", fsrsiList },
            { "Signal", fsrsiSignalList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(fsrsiList);
        stockData.IndicatorName = IndicatorName.FastandSlowRelativeStrengthIndexOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Fast Slow Degree Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculateFastSlowDegreeOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 100, int fastLength = 3, int slowLength = 2, int signalLength = 14)
    {
        List<double> osList = new(stockData.Count);
        List<double> histList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var fastF1bSumWindow = new RollingSum();
        var fastF2bSumWindow = new RollingSum();
        var fastVWSumWindow = new RollingSum();
        var slowF1bSumWindow = new RollingSum();
        var slowF2bSumWindow = new RollingSum();
        var slowVWSumWindow = new RollingSum();

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var fastF1x = (double)(i + 1) / length;
            var fastF1b = (double)1 / (i + 1) * Math.Sin(fastF1x * (i + 1) * Math.PI);
            fastF1bSumWindow.Add(fastF1b);
            var fastF1bSum = fastF1bSumWindow.Sum(fastLength);
            var fastF1pol = (fastF1x * fastF1x) + fastF1bSum;
            var fastF2x = length != 0 ? (double)i / length : 0;
            var fastF2b = (double)1 / (i + 1) * Math.Sin(fastF2x * (i + 1) * Math.PI);
            fastF2bSumWindow.Add(fastF2b);
            var fastF2bSum = fastF2bSumWindow.Sum(fastLength);
            var fastF2pol = (fastF2x * fastF2x) + fastF2bSum;
            var fastW = fastF1pol - fastF2pol;
            var fastVW = prevValue * fastW;
            fastVWSumWindow.Add(fastVW);
            var fastVWSum = fastVWSumWindow.Sum(length);
            var slowF1x = length != 0 ? (double)(i + 1) / length : 0;
            var slowF1b = (double)1 / (i + 1) * Math.Sin(slowF1x * (i + 1) * Math.PI);
            slowF1bSumWindow.Add(slowF1b);
            var slowF1bSum = slowF1bSumWindow.Sum(slowLength);
            var slowF1pol = (slowF1x * slowF1x) + slowF1bSum;
            var slowF2x = length != 0 ? (double)i / length : 0;
            var slowF2b = (double)1 / (i + 1) * Math.Sin(slowF2x * (i + 1) * Math.PI);
            slowF2bSumWindow.Add(slowF2b);
            var slowF2bSum = slowF2bSumWindow.Sum(slowLength);
            var slowF2pol = (slowF2x * slowF2x) + slowF2bSum;
            var slowW = slowF1pol - slowF2pol;
            var slowVW = prevValue * slowW;
            slowVWSumWindow.Add(slowVW);
            var slowVWSum = slowVWSumWindow.Sum(length);

            var os = fastVWSum - slowVWSum;
            osList.Add(os);
        }

        var osSignalList = GetMovingAverageList(stockData, maType, signalLength, osList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var os = osList[i];
            var osSignal = osSignalList[i];

            var prevHist = GetLastOrDefault(histList);
            var hist = os - osSignal;
            histList.Add(hist);

            var signal = GetCompareSignal(hist, prevHist);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Fsdo", osList },
            { "Signal", osSignalList },
            { "Histogram", histList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(osList);
        stockData.IndicatorName = IndicatorName.FastSlowDegreeOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Fractal Chaos Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <returns></returns>
    public static StockData CalculateFractalChaosOscillator(this StockData stockData)
    {
        List<double> fcoList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var fractalChaosBandsList = CalculateFractalChaosBands(stockData);
        var upperBandList = fractalChaosBandsList.OutputValues["UpperBand"];
        var lowerBandList = fractalChaosBandsList.OutputValues["LowerBand"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var upperBand = upperBandList[i];
            var prevUpperBand = i >= 1 ? upperBandList[i - 1] : 0;
            var lowerBand = lowerBandList[i];
            var prevLowerBand = i >= 1 ? lowerBandList[i - 1] : 0;

            var prevFco = GetLastOrDefault(fcoList);
            double fco = upperBand != prevUpperBand ? 1 : lowerBand != prevLowerBand ? -1 : 0;
            fcoList.Add(fco);

            var signal = GetCompareSignal(fco, prevFco);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Fco", fcoList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(fcoList);
        stockData.IndicatorName = IndicatorName.FractalChaosOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Firefly Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateFireflyOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ZeroLagExponentialMovingAverage,
        int length = 10, int smoothLength = 3)
    {
        List<double> v2List = new(stockData.Count);
        List<double> v5List = new(stockData.Count);
        List<double> wwList = new(stockData.Count);
        List<double> mmList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var wwWindow = new RollingMinMax(smoothLength);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentClose = inputList[i];

            var v2 = (currentHigh + currentLow + (currentClose * 2)) / 4;
            v2List.Add(v2);
        }

        var v3List = GetMovingAverageList(stockData, maType, length, v2List);
        stockData.SetCustomValues(v2List);
        var v4List = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var v2 = v2List[i];
            var v3 = v3List[i];
            var v4 = v4List[i];

            var v5 = v4 == 0 ? (v2 - v3) * 100 : (v2 - v3) * 100 / v4;
            v5List.Add(v5);
        }

        var v6List = GetMovingAverageList(stockData, maType, smoothLength, v5List);
        var v7List = GetMovingAverageList(stockData, maType, smoothLength, v6List);
        var wwZLagEmaList = GetMovingAverageList(stockData, maType, length, v7List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var wwZlagEma = wwZLagEmaList[i];
            var prevWw1 = i >= 1 ? wwList[i - 1] : 0;
            var prevWw2 = i >= 2 ? wwList[i - 2] : 0;

            var ww = ((wwZlagEma + 100) / 2) - 4;
            wwList.Add(ww);

            wwWindow.Add(ww);
            var mm = wwWindow.Max;
            mmList.Add(mm);

            var signal = GetRsiSignal(ww - prevWw1, prevWw1 - prevWw2, ww, prevWw1, 80, 20);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Fo", wwList },
            { "Signal", mmList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(wwList);
        stockData.IndicatorName = IndicatorName.FireflyOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Fibonacci Retrace
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="factor"></param>
    /// <returns></returns>
    public static StockData CalculateFibonacciRetrace(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length1 = 15, int length2 = 50, double factor = 0.382)
    {
        List<double> hretList = new(stockData.Count);
        List<double> lretList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length2);

        var wmaList = GetMovingAverageList(stockData, maType, length1, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var wma = wmaList[i];
            var highest = highestList[i];
            var lowest = lowestList[i];
            var prevWma = i >= 1 ? wmaList[i - 1] : 0;
            var retrace = (highest - lowest) * factor;

            var prevHret = GetLastOrDefault(hretList);
            var hret = highest - retrace;
            hretList.Add(hret);

            var prevLret = GetLastOrDefault(lretList);
            var lret = lowest + retrace;
            lretList.Add(lret);

            var signal = GetBullishBearishSignal(wma - hret, prevWma - prevHret, wma - lret, prevWma - prevLret);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", hretList },
            { "LowerBand", lretList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.FibonacciRetrace;

        return stockData;
    }


    /// <summary>
    /// Calculates the FX Sniper Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="cciLength"></param>
    /// <param name="t3Length"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    public static StockData CalculateFXSniperIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int cciLength = 14, int t3Length = 5, double b = MathHelper.InversePhi)
    {
        List<double> e1List = new(stockData.Count);
        List<double> e2List = new(stockData.Count);
        List<double> e3List = new(stockData.Count);
        List<double> e4List = new(stockData.Count);
        List<double> e5List = new(stockData.Count);
        List<double> e6List = new(stockData.Count);
        List<double> fxSniperList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var b2 = b * b;
        var b3 = b2 * b;
        var c1 = -b3;
        var c2 = 3 * (b2 + b3);
        var c3 = -3 * ((2 * b2) + b + b3);
        var c4 = 1 + (3 * b) + b3 + (3 * b2);
        var nr = 1 + (0.5 * (t3Length - 1));
        var w1 = 2 / (nr + 1);
        var w2 = 1 - w1;

        var cciList = CalculateCommodityChannelIndex(stockData, maType: maType, length: cciLength).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var cci = cciList[i];

            var prevE1 = GetLastOrDefault(e1List);
            var e1 = (w1 * cci) + (w2 * prevE1);
            e1List.Add(e1);

            var prevE2 = GetLastOrDefault(e2List);
            var e2 = (w1 * e1) + (w2 * prevE2);
            e2List.Add(e2);

            var prevE3 = GetLastOrDefault(e3List);
            var e3 = (w1 * e2) + (w2 * prevE3);
            e3List.Add(e3);

            var prevE4 = GetLastOrDefault(e4List);
            var e4 = (w1 * e3) + (w2 * prevE4);
            e4List.Add(e4);

            var prevE5 = GetLastOrDefault(e5List);
            var e5 = (w1 * e4) + (w2 * prevE5);
            e5List.Add(e5);

            var prevE6 = GetLastOrDefault(e6List);
            var e6 = (w1 * e5) + (w2 * prevE6);
            e6List.Add(e6);

            var prevFxSniper = GetLastOrDefault(fxSniperList);
            var fxsniper = (c1 * e6) + (c2 * e5) + (c3 * e4) + (c4 * e3);
            fxSniperList.Add(fxsniper);

            var signal = GetCompareSignal(fxsniper, prevFxSniper);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "FXSniper", fxSniperList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(fxSniperList);
        stockData.IndicatorName = IndicatorName.FXSniperIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Fear and Greed Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateFearAndGreedIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int fastLength = 10, int slowLength = 30, int smoothLength = 2)
    {
        List<double> trUpList = new(stockData.Count);
        List<double> trDnList = new(stockData.Count);
        List<double> fgiList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var tr = CalculateTrueRange(currentHigh, currentLow, prevValue);

            var trUp = currentValue > prevValue ? tr : 0;
            trUpList.Add(trUp);

            var trDn = currentValue < prevValue ? tr : 0;
            trDnList.Add(trDn);
        }

        var fastTrUpList = GetMovingAverageList(stockData, maType, fastLength, trUpList);
        var fastTrDnList = GetMovingAverageList(stockData, maType, fastLength, trDnList);
        var slowTrUpList = GetMovingAverageList(stockData, maType, slowLength, trUpList);
        var slowTrDnList = GetMovingAverageList(stockData, maType, slowLength, trDnList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var fastTrUp = fastTrUpList[i];
            var fastTrDn = fastTrDnList[i];
            var slowTrUp = slowTrUpList[i];
            var slowTrDn = slowTrDnList[i];
            var fastDiff = fastTrUp - fastTrDn;
            var slowDiff = slowTrUp - slowTrDn;

            var fgi = fastDiff - slowDiff;
            fgiList.Add(fgi);
        }

        var fgiEmaList = GetMovingAverageList(stockData, maType, smoothLength, fgiList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var fgiEma = fgiEmaList[i];
            var prevFgiEma = i >= 1 ? fgiEmaList[i - 1] : 0;

            var signal = GetCompareSignal(fgiEma, prevFgiEma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Fgi", fgiList },
            { "Signal", fgiEmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(fgiList);
        stockData.IndicatorName = IndicatorName.FearAndGreedIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Function To Candles
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateFunctionToCandles(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod,
        int length = 14)
    {
        List<double> tpList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, openList, _) = GetInputValuesList(stockData);

        stockData.SetCustomValues(inputList);
        var rsiCList = CalculateRelativeStrengthIndex(stockData, maType, length: length).CustomValuesList;
        stockData.SetCustomValues(openList);
        var rsiOList = CalculateRelativeStrengthIndex(stockData, maType, length: length).CustomValuesList;
        stockData.SetCustomValues(highList);
        var rsiHList = CalculateRelativeStrengthIndex(stockData, maType, length: length).CustomValuesList;
        stockData.SetCustomValues(lowList);
        var rsiLList = CalculateRelativeStrengthIndex(stockData, maType, length: length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var rsiC = rsiCList[i];
            var rsiO = rsiOList[i];
            var rsiH = rsiHList[i];
            var rsiL = rsiLList[i];
            var prevTp1 = i >= 1 ? tpList[i - 1] : 0;
            var prevTp2 = i >= 2 ? tpList[i - 2] : 0;

            var tp = (rsiC + rsiO + rsiH + rsiL) / 4;
            tpList.Add(tp);

            var signal = GetCompareSignal(tp - prevTp1, prevTp1 - prevTp2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Close", rsiCList },
            { "Open", rsiOList },
            { "High", rsiHList },
            { "Low", rsiLList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.FunctionToCandles;

        return stockData;
    }


    /// <summary>
    /// Calculates the Karobein Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateKarobeinOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 50)
    {
        List<double> aList = new(stockData.Count);
        List<double> bList = new(stockData.Count);
        List<double> dList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var emaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var ema = emaList[i];
            var prevEma = i >= 1 ? emaList[i - 1] : 0;

            var a = ema < prevEma && prevEma != 0 ? ema / prevEma : 0;
            aList.Add(a);

            var b = ema > prevEma && prevEma != 0 ? ema / prevEma : 0;
            bList.Add(b);
        }

        var aEmaList = GetMovingAverageList(stockData, maType, length, aList);
        var bEmaList = GetMovingAverageList(stockData, maType, length, bList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var ema = emaList[i];
            var prevEma = i >= 1 ? emaList[i - 1] : 0;
            var a = aEmaList[i];
            var b = bEmaList[i];
            var prevD1 = i >= 1 ? dList[i - 1] : 0;
            var prevD2 = i >= 2 ? dList[i - 2] : 0;
            var c = prevEma != 0 && ema != 0 ? MinOrMax(ema / prevEma / ((ema / prevEma) + b), 1, 0) : 0;

            var d = prevEma != 0 && ema != 0 ? MinOrMax((2 * (ema / prevEma / ((ema / prevEma) + (c * a)))) - 1, 1, 0) : 0;
            dList.Add(d);

            var signal = GetRsiSignal(d - prevD1, prevD1 - prevD2, d, prevD1, 0.8, 0.2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ko", dList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(dList);
        stockData.IndicatorName = IndicatorName.KarobeinOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Kase Peak Oscillator V1
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateKasePeakOscillatorV1(this StockData stockData, int length = 30, int smoothLength = 3)
    {
        List<double> diffList = new(stockData.Count);
        List<double> lnList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (_, highList, lowList, _, _) = GetInputValuesList(stockData);

        var sqrt = Sqrt(length);

        var atrList = CalculateAverageTrueRange(stockData, length: length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentAtr = atrList[i];
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var prevLow = i >= length ? lowList[i - length] : 0;
            var prevHigh = i >= length ? highList[i - length] : 0;
            var rwh = currentAtr != 0 ? (currentHigh - prevLow) / currentAtr * sqrt : 0;
            var rwl = currentAtr != 0 ? (prevHigh - currentLow) / currentAtr * sqrt : 0;

            var diff = rwh - rwl;
            diffList.Add(diff);
        }

        var pkList = GetMovingAverageList(stockData, MovingAvgType.WeightedMovingAverage, smoothLength, diffList);
        var mnList = GetMovingAverageList(stockData, MovingAvgType.SimpleMovingAverage, length, pkList);
        stockData.SetCustomValues(pkList);
        var sdList = CalculateStandardDeviationVolatility(stockData, length: length).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var pk = pkList[i];
            var mn = mnList[i];
            var sd = sdList[i];
            var prevPk = i >= 1 ? pkList[i - 1] : 0;
            var v1 = mn + (1.33 * sd) > 2.08 ? mn + (1.33 * sd) : 2.08;
            var v2 = mn - (1.33 * sd) < -1.92 ? mn - (1.33 * sd) : -1.92;

            var prevLn = GetLastOrDefault(lnList);
            var ln = prevPk >= 0 && pk > 0 ? v1 : prevPk <= 0 && pk < 0 ? v2 : 0;
            lnList.Add(ln);

            var signal = GetCompareSignal(ln, prevLn);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Kpo", lnList },
            { "Pk", pkList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(lnList);
        stockData.IndicatorName = IndicatorName.KasePeakOscillatorV1;

        return stockData;
    }


    /// <summary>
    /// Calculates the Kase Peak Oscillator V2
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="smoothLength"></param>
    /// <param name="devFactor"></param>
    /// <param name="sensitivity"></param>
    /// <returns></returns>
    public static StockData CalculateKasePeakOscillatorV2(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int fastLength = 8, int slowLength = 65, int length1 = 9, int length2 = 30, int length3 = 50, int smoothLength = 3, double devFactor = 2,
        double sensitivity = 40)
    {
        List<double> ccLogList = new(stockData.Count);
        List<double> xpAbsAvgList = new(stockData.Count);
        List<double> kpoBufferList = new(stockData.Count);
        List<double> xpList = new(stockData.Count);
        List<double> xpAbsList = new(stockData.Count);
        List<double> kppBufferList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var x1SumWindow = new RollingSum();
        var x2SumWindow = new RollingSum();
        var xpAbsSumWindow = new RollingSum();

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var temp = prevValue != 0 ? currentValue / prevValue : 0;

            var ccLog = temp > 0 ? Math.Log(temp) : 0;
            ccLogList.Add(ccLog);
        }

        stockData.SetCustomValues(ccLogList);
        var ccDevList = CalculateStandardDeviationVolatility(stockData, maType, length1).CustomValuesList;
        var ccDevAvgList = GetMovingAverageList(stockData, maType, length2, ccDevList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var avg = ccDevAvgList[i];
            var currentLow = lowList[i];
            var currentHigh = highList[i];

            double max1 = 0, max2 = 0;
            for (var j = fastLength; j < slowLength; j++)
            {
                var sqrtK = Sqrt(j);
                var prevLow = i >= j ? lowList[i - j] : 0;
                var prevHigh = i >= j ? highList[i - j] : 0;
                var temp1 = prevLow != 0 ? currentHigh / prevLow : 0;
                var log1 = temp1 > 0 ? Math.Log(temp1) : 0;
                max1 = Math.Max(log1 / sqrtK, max1);
                var temp2 = currentLow != 0 ? prevHigh / currentLow : 0;
                var log2 = temp2 > 0 ? Math.Log(temp2) : 0;
                max2 = Math.Max(log2 / sqrtK, max2);
            }

            var x1 = avg != 0 ? max1 / avg : 0;
            x1SumWindow.Add(x1);

            var x2 = avg != 0 ? max2 / avg : 0;
            x2SumWindow.Add(x2);

            var xp = sensitivity * (x1SumWindow.Average(smoothLength) - x2SumWindow.Average(smoothLength));
            xpList.Add(xp);

            var xpAbs = Math.Abs(xp);
            xpAbsList.Add(xpAbs);

            xpAbsSumWindow.Add(xpAbs);
            var xpAbsAvg = xpAbsSumWindow.Average(length3);
            xpAbsAvgList.Add(xpAbsAvg);
        }

        stockData.SetCustomValues(xpAbsList);
        var xpAbsStdDevList = CalculateStandardDeviationVolatility(stockData, maType, length3).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var xpAbsAvg = xpAbsAvgList[i];
            var xpAbsStdDev = xpAbsStdDevList[i];
            var prevKpoBuffer1 = i >= 1 ? kpoBufferList[i - 1] : 0;
            var prevKpoBuffer2 = i >= 2 ? kpoBufferList[i - 2] : 0;

            var tmpVal = xpAbsAvg + (devFactor * xpAbsStdDev);
            var maxVal = Math.Max(90, tmpVal);

            var prevKpoBuffer = GetLastOrDefault(kpoBufferList);
            var kpoBuffer = xpList[i];
            kpoBufferList.Add(kpoBuffer);

            var kppBuffer = prevKpoBuffer1 > 0 && prevKpoBuffer1 > kpoBuffer && prevKpoBuffer1 >= prevKpoBuffer2 &&
                            prevKpoBuffer1 >= maxVal ? prevKpoBuffer1 : prevKpoBuffer1 < 0 && prevKpoBuffer1 < kpoBuffer &&
                                                                        prevKpoBuffer1 <= prevKpoBuffer2 && prevKpoBuffer1 <= maxVal * -1 ? prevKpoBuffer1 :
                prevKpoBuffer1 > 0 && prevKpoBuffer1 > kpoBuffer && prevKpoBuffer1 >= prevKpoBuffer2 ? prevKpoBuffer1 :
                prevKpoBuffer1 < 0 && prevKpoBuffer1 < kpoBuffer && prevKpoBuffer1 <= prevKpoBuffer2 ? prevKpoBuffer1 : 0;
            kppBufferList.Add(kppBuffer);

            var signal = GetCompareSignal(kpoBuffer, prevKpoBuffer);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Kpo", xpList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(xpList);
        stockData.IndicatorName = IndicatorName.KasePeakOscillatorV2;

        return stockData;
    }


    /// <summary>
    /// Calculates the Kase Serial Dependency Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateKaseSerialDependencyIndex(this StockData stockData, int length = 14)
    {
        List<double> ksdiUpList = new(stockData.Count);
        List<double> ksdiDownList = new(stockData.Count);
        List<double> tempList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var temp = prevValue != 0 ? currentValue / prevValue : 0;

            var tempLog = temp > 0 ? Math.Log(temp) : 0;
            tempList.Add(tempLog);
        }

        stockData.SetCustomValues(tempList);
        var stdDevList = CalculateStandardDeviationVolatility(stockData, length: length).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var volatility = stdDevList[i];
            var prevHigh = i >= length ? highList[i - length] : 0;
            var prevLow = i >= length ? lowList[i - length] : 0;
            var ksdiUpTemp = prevLow != 0 ? currentHigh / prevLow : 0;
            var ksdiDownTemp = prevHigh != 0 ? currentLow / prevHigh : 0;
            var ksdiUpLog = ksdiUpTemp > 0 ? Math.Log(ksdiUpTemp) : 0;
            var ksdiDownLog = ksdiDownTemp > 0 ? Math.Log(ksdiDownTemp) : 0;

            var prevKsdiUp = GetLastOrDefault(ksdiUpList);
            var ksdiUp = volatility != 0 ? ksdiUpLog / volatility : 0;
            ksdiUpList.Add(ksdiUp);

            var prevKsdiDown = GetLastOrDefault(ksdiDownList);
            var ksdiDown = volatility != 0 ? ksdiDownLog / volatility : 0;
            ksdiDownList.Add(ksdiDown);

            var signal = GetCompareSignal(ksdiUp - ksdiDown, prevKsdiUp - prevKsdiDown);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "KsdiUp", ksdiUpList },
            { "KsdiDn", ksdiDownList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.KaseSerialDependencyIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Kaufman Binary Wave
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="fastSc"></param>
    /// <param name="slowSc"></param>
    /// <param name="filterPct"></param>
    /// <returns></returns>
    public static StockData CalculateKaufmanBinaryWave(this StockData stockData, int length = 20, double fastSc = 0.6022, double slowSc = 0.0645,
        double filterPct = 10)
    {
        List<double> amaList = new(stockData.Count);
        List<double> diffList = new(stockData.Count);
        List<double> amaLowList = new(stockData.Count);
        List<double> amaHighList = new(stockData.Count);
        List<double> bwList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var efRatioList = CalculateKaufmanAdaptiveMovingAverage(stockData, length: length).OutputValues["Er"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var efRatio = efRatioList[i];
            var prevAma = i >= 1 ? amaList[i - 1] : currentValue;
            var smooth = Pow((efRatio * fastSc) + slowSc, 2);

            var ama = prevAma + (smooth * (currentValue - prevAma));
            amaList.Add(ama);

            var diff = ama - prevAma;
            diffList.Add(diff);
        }

        stockData.SetCustomValues(diffList);
        var diffStdDevList = CalculateStandardDeviationVolatility(stockData, length: length).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var ama = amaList[i];
            var diffStdDev = diffStdDevList[i];
            var prevAma = i >= 1 ? amaList[i - 1] : currentValue;
            var filter = filterPct / 100 * diffStdDev;

            var prevAmaLow = GetLastOrDefault(amaLowList);
            var amaLow = ama < prevAma ? ama : prevAmaLow;
            amaLowList.Add(amaLow);

            var prevAmaHigh = GetLastOrDefault(amaHighList);
            var amaHigh = ama > prevAma ? ama : prevAmaHigh;
            amaHighList.Add(amaHigh);

            var prevBw = GetLastOrDefault(bwList);
            double bw = ama - amaLow > filter ? 1 : amaHigh - ama > filter ? -1 : 0;
            bwList.Add(bw);

            var signal = GetCompareSignal(bw, prevBw);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Kbw", bwList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(bwList);
        stockData.IndicatorName = IndicatorName.KaufmanBinaryWave;

        return stockData;
    }


    /// <summary>
    /// Calculates the Kurtosis Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <returns></returns>
    public static StockData CalculateKurtosisIndicator(this StockData stockData, int length1 = 3, int length2 = 1, int fastLength = 3,
        int slowLength = 65)
    {
        List<double> diffList = new(stockData.Count);
        List<double> kList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= length1 ? inputList[i - length1] : 0;
            var prevDiff = i >= length2 ? diffList[i - length2] : 0;

            var diff = MinPastValues(i, length1, currentValue - prevValue);
            diffList.Add(diff);

            var k = MinPastValues(i, length2, diff - prevDiff);
            kList.Add(k);
        }

        var fkList = GetMovingAverageList(stockData, MovingAvgType.ExponentialMovingAverage, slowLength, kList);
        var fskList = GetMovingAverageList(stockData, MovingAvgType.WeightedMovingAverage, fastLength, fkList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var fsk = fskList[i];
            var prevFsk = i >= 1 ? fskList[i - 1] : 0;

            var signal = GetCompareSignal(fsk, prevFsk);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Fk", fkList },
            { "Signal", fskList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(fkList);
        stockData.IndicatorName = IndicatorName.KurtosisIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Kaufman Adaptive Correlation Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateKaufmanAdaptiveCorrelationOscillator(this StockData stockData,
        MovingAvgType maType = MovingAvgType.KaufmanAdaptiveMovingAverage, int length = 14)
    {
        List<double> indexList = new(stockData.Count);
        List<double> index2List = new(stockData.Count);
        List<double> src2List = new(stockData.Count);
        List<double> srcStList = new(stockData.Count);
        List<double> indexStList = new(stockData.Count);
        List<double> indexSrcList = new(stockData.Count);
        List<double> rList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var kamaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];

            double index = i;
            indexList.Add(index);

            var indexSrc = i * currentValue;
            indexSrcList.Add(indexSrc);

            var srcSrc = currentValue * currentValue;
            src2List.Add(srcSrc);

            var indexIndex = index * index;
            index2List.Add(indexIndex);
        }

        var indexMaList = GetMovingAverageList(stockData, maType, length, indexList);
        var indexSrcMaList = GetMovingAverageList(stockData, maType, length, indexSrcList);
        var index2MaList = GetMovingAverageList(stockData, maType, length, index2List);
        var src2MaList = GetMovingAverageList(stockData, maType, length, src2List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var srcMa = kamaList[i];
            var indexMa = indexMaList[i];
            var indexSrcMa = indexSrcMaList[i];
            var index2Ma = index2MaList[i];
            var src2Ma = src2MaList[i];
            var prevR1 = i >= 1 ? rList[i - 1] : 0;
            var prevR2 = i >= 2 ? rList[i - 2] : 0;

            var indexSqrt = index2Ma - Pow(indexMa, 2);
            var indexSt = indexSqrt >= 0 ? Sqrt(indexSqrt) : 0;
            indexStList.Add(indexSt);

            var srcSqrt = src2Ma - Pow(srcMa, 2);
            var srcSt = srcSqrt >= 0 ? Sqrt(srcSqrt) : 0;
            srcStList.Add(srcSt);

            var a = indexSrcMa - (indexMa * srcMa);
            var b = indexSt * srcSt;

            var r = b != 0 ? a / b : 0;
            rList.Add(r);

            var signal = GetRsiSignal(r - prevR1, prevR1 - prevR2, r, prevR1, 0.5, -0.5);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "IndexSt", indexStList },
            { "SrcSt", srcStList },
            { "Kaco", rList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(rList);
        stockData.IndicatorName = IndicatorName.KaufmanAdaptiveCorrelationOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Know Sure Thing Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="length4"></param>
    /// <param name="rocLength1"></param>
    /// <param name="rocLength2"></param>
    /// <param name="rocLength3"></param>
    /// <param name="rocLength4"></param>
    /// <param name="signalLength"></param>
    /// <param name="weight1"></param>
    /// <param name="weight2"></param>
    /// <param name="weight3"></param>
    /// <param name="weight4"></param>
    /// <returns></returns>
    public static StockData CalculateKnowSureThing(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 10,
        int length2 = 10, int length3 = 10, int length4 = 15, int rocLength1 = 10, int rocLength2 = 15, int rocLength3 = 20, int rocLength4 = 30,
        int signalLength = 9, double weight1 = 1, double weight2 = 2, double weight3 = 3, double weight4 = 4)
    {
        List<double> kstList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var roc1List = CalculateRateOfChange(stockData, rocLength1).CustomValuesList;
        var roc2List = CalculateRateOfChange(stockData, rocLength2).CustomValuesList;
        var roc3List = CalculateRateOfChange(stockData, rocLength3).CustomValuesList;
        var roc4List = CalculateRateOfChange(stockData, rocLength4).CustomValuesList;
        var roc1SmaList = GetMovingAverageList(stockData, maType, length1, roc1List);
        var roc2SmaList = GetMovingAverageList(stockData, maType, length2, roc2List);
        var roc3SmaList = GetMovingAverageList(stockData, maType, length3, roc3List);
        var roc4SmaList = GetMovingAverageList(stockData, maType, length4, roc4List);

        for (var i = 0; i < stockData.Count; i++)
        {
            var roc1 = roc1SmaList[i];
            var roc2 = roc2SmaList[i];
            var roc3 = roc3SmaList[i];
            var roc4 = roc4SmaList[i];

            var kst = (roc1 * weight1) + (roc2 * weight2) + (roc3 * weight3) + (roc4 * weight4);
            kstList.Add(kst);
        }

        var kstSignalList = GetMovingAverageList(stockData, maType, signalLength, kstList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var kst = kstList[i];
            var kstSignal = kstSignalList[i];
            var prevKst = i >= 1 ? kstList[i - 1] : 0;
            var prevKstSignal = i >= 1 ? kstSignalList[i - 1] : 0;

            var signal = GetCompareSignal(kst - kstSignal, prevKst - prevKstSignal);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Kst", kstList },
            { "Signal", kstSignalList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(kstList);
        stockData.IndicatorName = IndicatorName.KnowSureThing;

        return stockData;
    }


    /// <summary>
    /// Calculates the Kase Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateKaseIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 10)
    {
        List<double> kUpList = new(stockData.Count);
        List<double> kDownList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (_, highList, lowList, _, volumeList) = GetInputValuesList(stockData);

        var sqrtPeriod = Sqrt(length);

        var volumeSmaList = GetMovingAverageList(stockData, maType, length, volumeList);
        var atrList = CalculateAverageTrueRange(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var avgTrueRange = atrList[i];
            var avgVolSma = volumeSmaList[i];
            var prevHigh = i >= 1 ? highList[i - 1] : 0;
            var prevLow = i >= 1 ? lowList[i - 1] : 0;
            var ratio = avgVolSma * sqrtPeriod;

            var prevKUp = GetLastOrDefault(kUpList);
            var kUp = avgTrueRange > 0 && ratio != 0 && currentLow != 0 ? prevHigh / currentLow / ratio : prevKUp;
            kUpList.Add(kUp);

            var prevKDown = GetLastOrDefault(kDownList);
            var kDown = avgTrueRange > 0 && ratio != 0 && prevLow != 0 ? currentHigh / prevLow / ratio : prevKDown;
            kDownList.Add(kDown);

            var signal = GetCompareSignal(kUp - kDown, prevKUp - prevKDown);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "KaseUp", kUpList },
            { "KaseDn", kDownList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.KaseIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Kendall Rank Correlation Coefficient
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateKendallRankCorrelationCoefficient(this StockData stockData, int length = 20)
    {
        List<double> numeratorList = new(stockData.Count);
        List<double> pearsonCorrelationList = new(stockData.Count);
        List<double> kendallCorrelationList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var corrWindow = new RollingCorrelation();

        var linRegList = CalculateLinearRegression(stockData, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevKendall1 = i >= 1 ? kendallCorrelationList[i - 1] : 0;
            var prevKendall2 = i >= 2 ? kendallCorrelationList[i - 2] : 0;

            var currentValue = inputList[i];
            var linReg = linRegList[i];
            corrWindow.Add(linReg, currentValue);
            var pearsonCorrelation = corrWindow.R(length);
            pearsonCorrelation = IsValueNullOrInfinity(pearsonCorrelation) ? 0 : pearsonCorrelation;
            pearsonCorrelationList.Add((double)pearsonCorrelation);

            var totalPairs = length * (double)(length - 1) / 2;
            double numerator = 0;
            for (var j = 0; j <= length - 1; j++)
            {
                for (var k = 0; k <= j; k++)
                {
                    var prevValueJ = i >= j ? inputList[i - j] : 0;
                    var prevValueK = i >= k ? inputList[i - k] : 0;
                    var prevLinRegJ = i >= j ? linRegList[i - j] : 0;
                    var prevLinRegK = i >= k ? linRegList[i - k] : 0;
                    numerator += Math.Sign(prevLinRegJ - prevLinRegK) * Math.Sign(prevValueJ - prevValueK);
                }
            }

            var kendallCorrelation = numerator / totalPairs;
            kendallCorrelationList.Add(kendallCorrelation);

            var signal = GetCompareSignal(kendallCorrelation - prevKendall1, prevKendall1 - prevKendall2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Krcc", kendallCorrelationList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(kendallCorrelationList);
        stockData.IndicatorName = IndicatorName.KendallRankCorrelationCoefficient;

        return stockData;
    }


    /// <summary>
    /// Calculates the Kwan Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateKwanIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int length = 9, 
        int smoothLength = 2)
    {
        List<double> vrList = new(stockData.Count);
        List<double> prevList = new(stockData.Count);
        List<double> knrpList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        double prevSum = 0;
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        var rsiList = CalculateRelativeStrengthIndex(stockData, maType, length: length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentClose = inputList[i];
            var priorClose = i >= length ? inputList[i - length] : 0;
            var mom = priorClose != 0 ? currentClose / priorClose * 100 : 0;
            var rsi = rsiList[i];
            var hh = highestList[i];
            var ll = lowestList[i];
            var sto = hh - ll != 0 ? (currentClose - ll) / (hh - ll) * 100 : 0;
            var prevVr = i >= smoothLength ? vrList[i - smoothLength] : 0;
            var prevKnrp1 = i >= 1 ? knrpList[i - 1] : 0;
            var prevKnrp2 = i >= 2 ? knrpList[i - 2] : 0;

            var vr = mom != 0 ? sto * rsi / mom : 0;
            vrList.Add(vr);

            var prev = prevVr;
            prevList.Add(prev);

            prevSum += prev;
            var knrp = prevSum / smoothLength;
            knrpList.Add(knrp);

            var signal = GetCompareSignal(knrp - prevKnrp1, prevKnrp1 - prevKnrp2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ki", knrpList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(knrpList);
        stockData.IndicatorName = IndicatorName.KwanIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Kaufman Stress Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="marketData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateKaufmanStressIndicator(this StockData stockData, StockData marketData, int length = 60)
    {
        List<double> svList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList1, highList1, lowList1, _, _) = GetInputValuesList(stockData);
        var (inputList2, highList2, lowList2, _, _) = GetInputValuesList(marketData);
        var (highestList1, lowestList1) = GetMaxAndMinValuesList(highList1, lowList1, length);
        var (highestList2, lowestList2) = GetMaxAndMinValuesList(highList2, lowList2, length);
        var dWindow = new RollingMinMax(length);

        if (stockData.Count == marketData.Count)
        {
            for (var i = 0; i < stockData.Count; i++)
            {
                var highestHigh1 = highestList1[i];
                var lowestLow1 = lowestList1[i];
                var highestHigh2 = highestList2[i];
                var lowestLow2 = lowestList2[i];
                var currentValue1 = inputList1[i];
                var currentValue2 = inputList2[i];
                var prevSv1 = i >= 1 ? svList[i - 1] : 0;
                var prevSv2 = i >= 2 ? svList[i - 2] : 0;
                var r1 = highestHigh1 - lowestLow1;
                var r2 = highestHigh2 - lowestLow2;
                var s1 = r1 != 0 ? (currentValue1 - lowestLow1) / r1 : 50;
                var s2 = r2 != 0 ? (currentValue2 - lowestLow2) / r2 : 50;

                var d = s1 - s2;
                dWindow.Add(d);
                var highestD = dWindow.Max;
                var lowestD = dWindow.Min;
                var r11 = highestD - lowestD;

                var sv = r11 != 0 ? MinOrMax(100 * (d - lowestD) / r11, 100, 0) : 50;
                svList.Add(sv);

                var signal = GetRsiSignal(sv - prevSv1, prevSv1 - prevSv2, sv, prevSv1, 90, 10);
                signalsList?.Add(signal);
            }
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ksi", svList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(svList);
        stockData.IndicatorName = IndicatorName.KaufmanStressIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Enhanced Williams R
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculateEnhancedWilliamsR(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14, 
        int signalLength = 5)
    {
        List<double> ewrList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);
        var (highestList1, lowestList1) = GetMaxAndMinValuesList(inputList, length);
        var (highestList2, lowestList2) = GetMaxAndMinValuesList(volumeList, length);

        var af = length < 10 ? 0.25 : ((double)length / 32) - 0.0625;
        var smaLength = MinOrMax((int)Math.Ceiling((double)length / 2));

        var srcSmaList = GetMovingAverageList(stockData, maType, smaLength, inputList);
        var volSmaList = GetMovingAverageList(stockData, maType, smaLength, volumeList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var maxVol = highestList2[i];
            var minVol = lowestList2[i];
            var maxSrc = highestList1[i];
            var minSrc = lowestList1[i];
            var srcSma = srcSmaList[i];
            var volSma = volSmaList[i];
            var volume = volumeList[i];
            var volWr = maxVol - minVol != 0 ? 2 * ((volume - volSma) / (maxVol - minVol)) : 0;
            var srcWr = maxSrc - minSrc != 0 ? 2 * ((currentValue - srcSma) / (maxSrc - minSrc)) : 0;
            var srcSwr = maxSrc - minSrc != 0 ? 2 * (MinPastValues(i, 1, currentValue - prevValue) / (maxSrc - minSrc)) : 0;

            var ewr = ((volWr > 0 && srcWr > 0 && currentValue > prevValue) || (volWr > 0 && srcWr < 0 && currentValue < prevValue)) && srcSwr + af != 0 ?
                ((50 * (srcWr * (srcSwr + af) * volWr)) + srcSwr + af) / (srcSwr + af) : 25 * ((srcWr * (volWr + 1)) + 2);
            ewrList.Add(ewr);
        }

        var ewrSignalList = GetMovingAverageList(stockData, maType, signalLength, ewrList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var ewr = ewrList[i];
            var ewrSignal = ewrSignalList[i];
            var prevEwr = i >= 1 ? ewrList[i - 1] : 0;
            var prevEwrSignal = i >= 1 ? ewrSignalList[i - 1] : 0;

            var signal = GetRsiSignal(ewr - ewrSignal, prevEwr - prevEwrSignal, ewr, prevEwr, 100, -100);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ewr", ewrList },
            { "Signal", ewrSignalList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(ewrList);
        stockData.IndicatorName = IndicatorName.EnhancedWilliamsR;

        return stockData;
    }


    /// <summary>
    /// Calculates the Earning Support Resistance Levels
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="inputName"></param>
    /// <returns></returns>
    public static StockData CalculateEarningSupportResistanceLevels(this StockData stockData, InputName inputName = InputName.MedianPrice)
    {
        List<double> mode1List = new(stockData.Count);
        List<double> mode2List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, closeList, _) = GetInputValuesList(inputName, stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentHigh = highList[i];
            var prevClose = i >= 1 ? closeList[i - 1] : 0;
            var prevLow = i >= 2 ? lowList[i - 2] : 0;
            var prevValue2 = i >= 2 ? inputList[i - 2] : 0;
            var prevValue1 = i >= 1 ? inputList[i - 1] : 0;

            var prevMode1 = GetLastOrDefault(mode1List);
            var mode1 = (prevLow + currentHigh) / 2;
            mode1List.Add(mode1);

            var prevMode2 = GetLastOrDefault(mode2List);
            var mode2 = (prevValue2 + currentValue + prevClose) / 3;
            mode2List.Add(mode2);

            var signal = GetBullishBearishSignal(currentValue - Math.Max(mode1, mode2), prevValue1 - Math.Max(prevMode1, prevMode2),
                currentValue - Math.Min(mode1, mode2), prevValue1 - Math.Min(prevMode1, prevMode2));
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Esr", mode1List }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(mode1List);
        stockData.IndicatorName = IndicatorName.EarningSupportResistanceLevels;

        return stockData;
    }


    /// <summary>
    /// Calculates the Elder Market Thermometer
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateElderMarketThermometer(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 22)
    {
        List<double> emtList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        var emaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var prevHigh = i >= 1 ? highList[i - 1] : 0;
            var prevLow = i >= 1 ? lowList[i - 1] : 0;

            var emt = currentHigh < prevHigh && currentLow > prevLow ? 0 : currentHigh - prevHigh > prevLow - currentLow ? Math.Abs(currentHigh - prevHigh) :
                Math.Abs(prevLow - currentLow);
            emtList.Add(emt);
        }

        var aemtList = GetMovingAverageList(stockData, maType, length, emtList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentEma = emaList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevEma = i >= 1 ? emaList[i - 1] : 0;
            var emt = emtList[i];
            var emtEma = aemtList[i];

            var signal = GetVolatilitySignal(currentValue - currentEma, prevValue - prevEma, emt, emtEma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Emt", emtList },
            { "Signal", aemtList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(emtList);
        stockData.IndicatorName = IndicatorName.ElderMarketThermometer;

        return stockData;
    }


    /// <summary>
    /// Calculates the Elliott Wave Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <returns></returns>
    public static StockData CalculateElliottWaveOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int fastLength = 5,
        int slowLength = 34)
    {
        List<double> ewoList = new(stockData.Count);
        List<double> ewoHistogramList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, fastLength, inputList);
        var sma34List = GetMovingAverageList(stockData, maType, slowLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentSma5 = smaList[i];
            var currentSma34 = sma34List[i];

            var ewo = currentSma5 - currentSma34;
            ewoList.Add(ewo);
        }

        var ewoSignalLineList = GetMovingAverageList(stockData, maType, fastLength, ewoList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var ewo = ewoList[i];
            var ewoSignalLine = ewoSignalLineList[i];

            var prevEwoHistogram = GetLastOrDefault(ewoHistogramList);
            var ewoHistogram = ewo - ewoSignalLine;
            ewoHistogramList.Add(ewoHistogram);

            var signal = GetCompareSignal(ewoHistogram, prevEwoHistogram);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ewo", ewoList },
            { "Signal", ewoSignalLineList },
            { "Histogram", ewoHistogramList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(ewoList);
        stockData.IndicatorName = IndicatorName.ElliottWaveOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ergodic Candlestick Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculateErgodicCandlestickOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length1 = 32, int length2 = 12)
    {
        List<double> xcoList = new(stockData.Count);
        List<double> xhlList = new(stockData.Count);
        List<double> ecoList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, openList, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentOpen = openList[i];
            var currentClose = inputList[i];

            var xco = currentClose - currentOpen;
            xcoList.Add(xco);

            var xhl = currentHigh - currentLow;
            xhlList.Add(xhl);
        }

        var xcoEma1List = GetMovingAverageList(stockData, maType, length1, xcoList);
        var xcoEma2List = GetMovingAverageList(stockData, maType, length2, xcoEma1List);
        var xhlEma1List = GetMovingAverageList(stockData, maType, length1, xhlList);
        var xhlEma2List = GetMovingAverageList(stockData, maType, length2, xhlEma1List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var xhlEma2 = xhlEma2List[i];
            var xcoEma2 = xcoEma2List[i];

            var eco = xhlEma2 != 0 ? 100 * xcoEma2 / xhlEma2 : 0;
            ecoList.Add(eco);
        }

        var ecoSignalList = GetMovingAverageList(stockData, maType, length2, ecoList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var eco = ecoList[i];
            var ecoEma = ecoSignalList[i];
            var prevEco = i >= 1 ? ecoList[i - 1] : 0;
            var prevEcoEma = i >= 1 ? ecoSignalList[i - 1] : 0;

            var signal = GetCompareSignal(eco - ecoEma, prevEco - prevEcoEma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Eco", ecoList },
            { "Signal", ecoSignalList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(ecoList);
        stockData.IndicatorName = IndicatorName.ErgodicCandlestickOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ergodic True Strength Index V1
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculateErgodicTrueStrengthIndexV1(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length1 = 4, int length2 = 8, int length3 = 6, int signalLength = 3)
    {
        List<double> etsiList = new(stockData.Count);
        List<double> priceDiffList = new(stockData.Count);
        List<double> absPriceDiffList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var priceDiff = MinPastValues(i, 1, currentValue - prevValue);
            priceDiffList.Add(priceDiff);

            var absPriceDiff = Math.Abs(priceDiff);
            absPriceDiffList.Add(absPriceDiff);
        }

        var diffEma1List = GetMovingAverageList(stockData, maType, length1, priceDiffList);
        var absDiffEma1List = GetMovingAverageList(stockData, maType, length1, absPriceDiffList);
        var diffEma2List = GetMovingAverageList(stockData, maType, length2, diffEma1List);
        var absDiffEma2List = GetMovingAverageList(stockData, maType, length2, absDiffEma1List);
        var diffEma3List = GetMovingAverageList(stockData, maType, length3, diffEma2List);
        var absDiffEma3List = GetMovingAverageList(stockData, maType, length3, absDiffEma2List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var diffEma3 = diffEma3List[i];
            var absDiffEma3 = absDiffEma3List[i];

            var etsi = absDiffEma3 != 0 ? MinOrMax(100 * diffEma3 / absDiffEma3, 100, -100) : 0;
            etsiList.Add(etsi);
        }

        var etsiSignalList = GetMovingAverageList(stockData, maType, signalLength, etsiList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var etsi = etsiList[i];
            var etsiSignal = etsiSignalList[i];
            var prevEtsi = i >= 1 ? etsiList[i - 1] : 0;
            var prevEtsiSignal = i >= 1 ? etsiSignalList[i - 1] : 0;

            var signal = GetCompareSignal(etsi - etsiSignal, prevEtsi - prevEtsiSignal);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Etsi", etsiList },
            { "Signal", etsiSignalList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(etsiList);
        stockData.IndicatorName = IndicatorName.ErgodicTrueStrengthIndexV1;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ergodic True Strength Index V2
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="length4"></param>
    /// <param name="length5"></param>
    /// <param name="length6"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculateErgodicTrueStrengthIndexV2(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length1 = 21, int length2 = 9, int length3 = 9, int length4 = 17, int length5 = 6, int length6 = 2, int signalLength = 2)
    {
        List<double> etsi2List = new(stockData.Count);
        List<double> etsi1List = new(stockData.Count);
        List<double> priceDiffList = new(stockData.Count);
        List<double> absPriceDiffList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var priceDiff = MinPastValues(i, 1, currentValue - prevValue);
            priceDiffList.Add(priceDiff);

            var absPriceDiff = Math.Abs(priceDiff);
            absPriceDiffList.Add(absPriceDiff);
        }

        var diffEma1List = GetMovingAverageList(stockData, maType, length1, priceDiffList);
        var absDiffEma1List = GetMovingAverageList(stockData, maType, length1, absPriceDiffList);
        var diffEma4List = GetMovingAverageList(stockData, maType, length4, priceDiffList);
        var absDiffEma4List = GetMovingAverageList(stockData, maType, length4, absPriceDiffList);
        var diffEma2List = GetMovingAverageList(stockData, maType, length2, diffEma1List);
        var absDiffEma2List = GetMovingAverageList(stockData, maType, length2, absDiffEma1List);
        var diffEma5List = GetMovingAverageList(stockData, maType, length5, diffEma4List);
        var absDiffEma5List = GetMovingAverageList(stockData, maType, length5, absDiffEma4List);
        var diffEma3List = GetMovingAverageList(stockData, maType, length3, diffEma2List);
        var absDiffEma3List = GetMovingAverageList(stockData, maType, length3, absDiffEma2List);
        var diffEma6List = GetMovingAverageList(stockData, maType, length6, diffEma5List);
        var absDiffEma6List = GetMovingAverageList(stockData, maType, length6, absDiffEma5List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var diffEma6 = diffEma6List[i];
            var absDiffEma6 = absDiffEma6List[i];
            var diffEma3 = diffEma3List[i];
            var absDiffEma3 = absDiffEma3List[i];

            var etsi1 = absDiffEma3 != 0 ? MinOrMax(diffEma3 / absDiffEma3 * 100, 100, -100) : 0;
            etsi1List.Add(etsi1);

            var etsi2 = absDiffEma6 != 0 ? MinOrMax(diffEma6 / absDiffEma6 * 100, 100, -100) : 0;
            etsi2List.Add(etsi2);
        }

        var etsi2SignalList = GetMovingAverageList(stockData, maType, signalLength, etsi2List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var etsi2 = etsi2List[i];
            var etsi2Signal = etsi2SignalList[i];
            var prevEtsi2 = i >= 1 ? etsi2List[i - 1] : 0;
            var prevEtsi2Signal = i >= 1 ? etsi2SignalList[i - 1] : 0;

            var signal = GetCompareSignal(etsi2 - etsi2Signal, prevEtsi2 - prevEtsi2Signal);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Etsi1", etsi1List },
            { "Etsi2", etsi2List },
            { "Signal", etsi2SignalList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(etsi2List);
        stockData.IndicatorName = IndicatorName.ErgodicTrueStrengthIndexV2;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ergodic Commodity Selection Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <param name="pointValue"></param>
    /// <returns></returns>
    public static StockData CalculateErgodicCommoditySelectionIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, 
        int length = 32, int smoothLength = 5, double pointValue = 1)
    {
        List<double> ergodicCsiList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        var k = 100 * (pointValue / Sqrt(length) / (150 + smoothLength));

        var adxList = CalculateAverageDirectionalIndex(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentValue = inputList[i];
            var adx = adxList[i];
            var prevAdx = i >= 1 ? adxList[i - 1] : 0;
            var adxR = (adx + prevAdx) * 0.5;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var tr = CalculateTrueRange(currentHigh, currentLow, prevValue);
            var csi = length + tr > 0 ? k * adxR * tr / length : 0;

            var ergodicCsi = currentValue > 0 ? csi / currentValue : 0;
            ergodicCsiList.Add(ergodicCsi);
        }

        var ergodicCsiSmaList = GetMovingAverageList(stockData, maType, smoothLength, ergodicCsiList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var ergodicCsiSma = ergodicCsiSmaList[i];
            var prevErgodicCsiSma1 = i >= 1 ? ergodicCsiSmaList[i - 1] : 0;
            var prevErgodicCsiSma2 = i >= 2 ? ergodicCsiSmaList[i - 2] : 0;

            var signal = GetCompareSignal(ergodicCsiSma - prevErgodicCsiSma1, prevErgodicCsiSma1 - prevErgodicCsiSma2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ecsi", ergodicCsiList },
            { "Signal", ergodicCsiSmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(ergodicCsiList);
        stockData.IndicatorName = IndicatorName.ErgodicCommoditySelectionIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Enhanced Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculateEnhancedIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14, 
        int signalLength = 8)
    {
        List<double> closewrList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        var smaLength = MinOrMax((int)Math.Ceiling((double)length / 2));

        var smaList = GetMovingAverageList(stockData, maType, smaLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var highest = highestList[i];
            var lowest = lowestList[i];
            var dnm = highest - lowest;
            var sma = smaList[i];

            var closewr = dnm != 0 ? 2 * (currentValue - sma) / dnm : 0;
            closewrList.Add(closewr);
        }

        var closewrSmaList = GetMovingAverageList(stockData, maType, signalLength, closewrList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var closewr = closewrList[i];
            var closewrSma = closewrSmaList[i];
            var prevCloseWr = i >= 1 ? closewrList[i - 1] : 0;
            var prevCloseWrSma = i >= 1 ? closewrSmaList[i - 1] : 0;

            var signal = GetCompareSignal(closewr - closewrSma, prevCloseWr - prevCloseWrSma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ei", closewrList },
            { "Signal", closewrSmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(closewrList);
        stockData.IndicatorName = IndicatorName.EnhancedIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ema Wave Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateEmaWaveIndicator(this StockData stockData, int length1 = 5, int length2 = 25, int length3 = 50, int smoothLength = 4)
    {
        List<double> emaADiffList = new(stockData.Count);
        List<double> emaBDiffList = new(stockData.Count);
        List<double> emaCDiffList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var emaAList = GetMovingAverageList(stockData, MovingAvgType.ExponentialMovingAverage, length1, inputList);
        var emaBList = GetMovingAverageList(stockData, MovingAvgType.ExponentialMovingAverage, length2, inputList);
        var emaCList = GetMovingAverageList(stockData, MovingAvgType.ExponentialMovingAverage, length3, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var emaA = emaAList[i];
            var emaB = emaBList[i];
            var emaC = emaCList[i];

            var emaADiff = currentValue - emaA;
            emaADiffList.Add(emaADiff);

            var emaBDiff = currentValue - emaB;
            emaBDiffList.Add(emaBDiff);

            var emaCDiff = currentValue - emaC;
            emaCDiffList.Add(emaCDiff);
        }

        var waList = GetMovingAverageList(stockData, MovingAvgType.SimpleMovingAverage, smoothLength, emaADiffList);
        var wbList = GetMovingAverageList(stockData, MovingAvgType.SimpleMovingAverage, smoothLength, emaBDiffList);
        var wcList = GetMovingAverageList(stockData, MovingAvgType.SimpleMovingAverage, smoothLength, emaCDiffList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var wa = waList[i];
            var wb = wbList[i];
            var wc = wcList[i];

            var signal = GetConditionSignal(wa > 0 && wb > 0 && wc > 0, wa < 0 && wb < 0 && wc < 0);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Wa", waList },
            { "Wb", wbList },
            { "Wc", wcList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.EmaWaveIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ergodic Mean Deviation Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculateErgodicMeanDeviationIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length1 = 32, int length2 = 5, int length3 = 5, int signalLength = 5)
    {
        List<double> ma1List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var emaList = GetMovingAverageList(stockData, maType, length1, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentEma = emaList[i];

            var ma1 = currentValue - currentEma;
            ma1List.Add(ma1);
        }

        var ma1EmaList = GetMovingAverageList(stockData, maType, length2, ma1List);
        var emdiList = GetMovingAverageList(stockData, maType, length3, ma1EmaList);
        var emdiSignalList = GetMovingAverageList(stockData, maType, signalLength, emdiList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var emdi = emdiList[i];
            var emdiSignal = emdiSignalList[i];
            var prevEmdi = i >= 1 ? emdiList[i - 1] : 0;
            var prevEmdiSignal = i >= 1 ? emdiSignalList[i - 1] : 0;

            var signal = GetCompareSignal(emdi - emdiSignal, prevEmdi - prevEmdiSignal);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Emdi", emdiList },
            { "Signal", emdiSignalList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(emdiList);
        stockData.IndicatorName = IndicatorName.ErgodicMeanDeviationIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Efficient Price
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateEfficientPrice(this StockData stockData, int length = 50)
    {
        List<double> epList = new(stockData.Count);
        List<double> chgErList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        double chgErSum = 0;
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var erList = CalculateKaufmanAdaptiveMovingAverage(stockData, length: length).OutputValues["Er"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var er = erList[i];
            var prevValue = i >= length ? inputList[i - length] : 0;
            var prevEp1 = i >= 1 ? epList[i - 1] : 0;
            var prevEp2 = i >= 2 ? epList[i - 2] : 0;

            var chgEr = MinPastValues(i, length, currentValue - prevValue) * er;
            chgErList.Add(chgEr);
            chgErSum += chgEr;

            var ep = chgErSum;
            epList.Add(ep);

            var signal = GetCompareSignal(ep - prevEp1, prevEp1 - prevEp2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ep", epList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(epList);
        stockData.IndicatorName = IndicatorName.EfficientPrice;

        return stockData;
    }


    /// <summary>
    /// Calculates the Efficient Auto Line
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="fastAlpha"></param>
    /// <param name="slowAlpha"></param>
    /// <returns></returns>
    public static StockData CalculateEfficientAutoLine(this StockData stockData, int length = 19, double fastAlpha = 0.0001, double slowAlpha = 0.005)
    {
        List<double> aList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var erList = CalculateKaufmanAdaptiveMovingAverage(stockData, length: length).OutputValues["Er"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var er = erList[i];
            var dev = (er * fastAlpha) + ((1 - er) * slowAlpha);

            var prevA = GetLastOrDefault(aList);
            var a = i < 9 ? currentValue : currentValue > prevA + dev ? currentValue : currentValue < prevA - dev ? currentValue : prevA;
            aList.Add(a);

            var signal = GetCompareSignal(currentValue - a, prevValue - prevA);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Eal", aList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(aList);
        stockData.IndicatorName = IndicatorName.EfficientAutoLine;

        return stockData;
    }
}

