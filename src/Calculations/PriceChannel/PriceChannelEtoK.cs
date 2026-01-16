
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the fractal chaos bands.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <returns></returns>
    public static StockData CalculateFractalChaosBands(this StockData stockData)
    {
        List<double> upperBandList = new(stockData.Count);
        List<double> lowerBandList = new(stockData.Count);
        List<double> middleBandList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevHigh1 = i >= 1 ? highList[i - 1] : 0;
            var prevHigh2 = i >= 2 ? highList[i - 2] : 0;
            var prevHigh3 = i >= 3 ? highList[i - 3] : 0;
            var prevLow1 = i >= 1 ? lowList[i - 1] : 0;
            var prevLow2 = i >= 2 ? lowList[i - 2] : 0;
            var prevLow3 = i >= 3 ? lowList[i - 3] : 0;
            var currentClose = inputList[i];
            var prevClose = i >= 1 ? inputList[i - 1] : 0;
            double oklUpper = prevHigh1 < prevHigh2 ? 1 : 0;
            double okrUpper = prevHigh3 < prevHigh2 ? 1 : 0;
            double oklLower = prevLow1 > prevLow2 ? 1 : 0;
            double okrLower = prevLow3 > prevLow2 ? 1 : 0;

            var prevUpperBand = GetLastOrDefault(upperBandList);
            var upperBand = oklUpper == 1 && okrUpper == 1 ? prevHigh2 : prevUpperBand;
            upperBandList.Add(upperBand);

            var prevLowerBand = GetLastOrDefault(lowerBandList);
            var lowerBand = oklLower == 1 && okrLower == 1 ? prevLow2 : prevLowerBand;
            lowerBandList.Add(lowerBand);

            var prevMiddleBand = GetLastOrDefault(middleBandList);
            var middleBand = (upperBand + lowerBand) / 2;
            middleBandList.Add(middleBand);

            var signal = GetBollingerBandsSignal(currentClose - middleBand, prevClose - prevMiddleBand, currentClose, prevClose, upperBand, prevUpperBand, lowerBand, prevLowerBand);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", upperBandList },
            { "MiddleBand", middleBandList },
            { "LowerBand", lowerBandList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.FractalChaosBands;

        return stockData;
    }


    /// <summary>
    /// Calculates the Interquartile Range Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="mult"></param>
    /// <returns></returns>
    public static StockData CalculateInterquartileRangeBands(this StockData stockData, int length = 14, double mult = 1.5)
    {
        List<double> upperBandList = new(stockData.Count);
        List<double> lowerBandList = new(stockData.Count);
        List<double> middleBandList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var trimeanList = CalculateTrimean(stockData, length);
        var q1List = trimeanList.OutputValues["Q1"];
        var q3List = trimeanList.OutputValues["Q3"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var q1 = q1List[i];
            var q3 = q3List[i];
            var iqr = q3 - q1;

            var upperBand = q3 + (mult * iqr);
            upperBandList.Add(upperBand);

            var lowerBand = q1 - (mult * iqr);
            lowerBandList.Add(lowerBand);

            var prevMiddleBand = GetLastOrDefault(middleBandList);
            var middleBand = (upperBand + lowerBand) / 2;
            middleBandList.Add(middleBand);

            var signal = GetCompareSignal(currentValue - middleBand, prevValue - prevMiddleBand);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", upperBandList },
            { "MiddleBand", middleBandList },
            { "LowerBand", lowerBandList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.InterquartileRangeBands;

        return stockData;
    }


    /// <summary>
    /// Calculates the G Channels
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateGChannels(this StockData stockData, int length = 100)
    {
        List<double> aList = new(stockData.Count);
        List<double> bList = new(stockData.Count);
        List<double> midList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevA = GetLastOrDefault(aList);
            var prevB = GetLastOrDefault(bList);
            var factor = length != 0 ? (prevA - prevB) / length : 0;

            var a = Math.Max(currentValue, prevA) - factor;
            aList.Add(a);

            var b = Math.Min(currentValue, prevB) + factor;
            bList.Add(b);

            var prevMid = GetLastOrDefault(midList);
            var mid = (a + b) / 2;
            midList.Add(mid);

            var signal = GetCompareSignal(currentValue - mid, prevValue - prevMid);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", aList },
            { "MiddleBand", midList },
            { "LowerBand", bList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.GChannels;

        return stockData;
    }


    /// <summary>
    /// Calculates the High Low Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateHighLowMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length = 14)
    {
        List<double> middleBandList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        var upperBandList = GetMovingAverageList(stockData, maType, length, highestList);
        var lowerBandList = GetMovingAverageList(stockData, maType, length, lowestList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var upperBand = upperBandList[i];
            var lowerBand = lowerBandList[i];

            var prevMiddleBand = GetLastOrDefault(middleBandList);
            var middleBand = (upperBand + lowerBand) / 2;
            middleBandList.Add(middleBand);

            var signal = GetCompareSignal(currentValue - middleBand, prevValue - prevMiddleBand);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", upperBandList },
            { "MiddleBand", middleBandList },
            { "LowerBand", lowerBandList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.HighLowMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the High Low Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="pctShift"></param>
    /// <returns></returns>
    public static StockData CalculateHighLowBands(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14,
        double pctShift = 1)
    {
        List<double> highBandList = new(stockData.Count);
        List<double> lowBandList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var tmaList1 = GetMovingAverageList(stockData, maType, length, inputList);
        var tmaList2 = GetMovingAverageList(stockData, maType, length, tmaList1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var tma = tmaList2[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevTma = i >= 1 ? tmaList2[i - 1] : 0;

            var prevHighBand = GetLastOrDefault(highBandList);
            var highBand = tma + (tma * pctShift / 100);
            highBandList.Add(highBand);

            var prevLowBand = GetLastOrDefault(lowBandList);
            var lowBand = tma - (tma * pctShift / 100);
            lowBandList.Add(lowBand);

            var signal = GetBollingerBandsSignal(currentValue - tma, prevValue - prevTma, currentValue, prevValue, highBand, prevHighBand,
                lowBand, prevLowBand);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", highBandList },
            { "MiddleBand", tmaList2 },
            { "LowerBand", lowBandList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.HighLowBands;

        return stockData;
    }


    /// <summary>
    /// Calculates the Hurst Cycle Channel
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="fastMult"></param>
    /// <param name="slowMult"></param>
    /// <returns></returns>
    public static StockData CalculateHurstCycleChannel(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod,
        int fastLength = 10, int slowLength = 30, double fastMult = 1, double slowMult = 3)
    {
        List<double> sctList = new(stockData.Count);
        List<double> scbList = new(stockData.Count);
        List<double> mctList = new(stockData.Count);
        List<double> mcbList = new(stockData.Count);
        List<double> scmmList = new(stockData.Count);
        List<double> mcmmList = new(stockData.Count);
        List<double> omedList = new(stockData.Count);
        List<double> oshortList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var scl = MinOrMax((int)Math.Ceiling((double)fastLength / 2));
        var mcl = MinOrMax((int)Math.Ceiling((double)slowLength / 2));
        var scl_2 = MinOrMax((int)Math.Ceiling((double)scl / 2));
        var mcl_2 = MinOrMax((int)Math.Ceiling((double)mcl / 2));

        var sclAtrList = CalculateAverageTrueRange(stockData, maType, scl).CustomValuesList;
        var mclAtrList = CalculateAverageTrueRange(stockData, maType, mcl).CustomValuesList;
        var sclRmaList = GetMovingAverageList(stockData, maType, scl, inputList);
        var mclRmaList = GetMovingAverageList(stockData, maType, mcl, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var sclAtr = sclAtrList[i];
            var mclAtr = mclAtrList[i];
            var prevSclRma = i >= scl_2 ? sclRmaList[i - scl_2] : currentValue;
            var prevMclRma = i >= mcl_2 ? mclRmaList[i - mcl_2] : currentValue;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var scm_off = fastMult * sclAtr;
            var mcm_off = slowMult * mclAtr;

            var prevSct = GetLastOrDefault(sctList);
            var sct = prevSclRma + scm_off;
            sctList.Add(sct);

            var prevScb = GetLastOrDefault(scbList);
            var scb = prevSclRma - scm_off;
            scbList.Add(scb);

            var mct = prevMclRma + mcm_off;
            mctList.Add(mct);

            var mcb = prevMclRma - mcm_off;
            mcbList.Add(mcb);

            var scmm = (sct + scb) / 2;
            scmmList.Add(scmm);

            var mcmm = (mct + mcb) / 2;
            mcmmList.Add(mcmm);

            var omed = mct - mcb != 0 ? (scmm - mcb) / (mct - mcb) : 0;
            omedList.Add(omed);

            var oshort = mct - mcb != 0 ? (currentValue - mcb) / (mct - mcb) : 0;
            oshortList.Add(oshort);

            var signal = GetBullishBearishSignal(currentValue - sct, prevValue - prevSct, currentValue - scb, prevValue - prevScb);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "FastUpperBand", sctList },
            { "SlowUpperBand", mctList },
            { "FastMiddleBand", scmmList },
            { "SlowMiddleBand", mcmmList },
            { "FastLowerBand", scbList },
            { "SlowLowerBand", mcbList },
            { "OMed", omedList },
            { "OShort", oshortList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.HurstCycleChannel;

        return stockData;
    }


    /// <summary>
    /// Calculates the Hurst Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="innerMult"></param>
    /// <param name="outerMult"></param>
    /// <param name="extremeMult"></param>
    /// <returns></returns>
    public static StockData CalculateHurstBands(this StockData stockData, int length = 10, double innerMult = 1.6, double outerMult = 2.6,
        double extremeMult = 4.2)
    {
        List<double> cmaList = new(stockData.Count);
        List<double> upperExtremeBandList = new(stockData.Count);
        List<double> lowerExtremeBandList = new(stockData.Count);
        List<double> upperOuterBandList = new(stockData.Count);
        List<double> lowerOuterBandList = new(stockData.Count);
        List<double> upperInnerBandList = new(stockData.Count);
        List<double> lowerInnerBandList = new(stockData.Count);
        List<double> dPriceList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum dPriceSum = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var displacement = MinOrMax((int)Math.Ceiling((double)length / 2) + 1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevCma1 = i >= 1 ? cmaList[i - 1] : 0;
            var prevCma2 = i >= 2 ? cmaList[i - 2] : 0;

            var dPrice = i >= displacement ? inputList[i - displacement] : 0;
            dPriceList.Add(dPrice);
            dPriceSum.Add(dPrice);

            var cma = dPrice == 0 ? prevCma1 + (prevCma1 - prevCma2) : dPriceSum.Average(length);
            cmaList.Add(cma);

            var extremeBand = cma * extremeMult / 100;
            var outerBand = cma * outerMult / 100;
            var innerBand = cma * innerMult / 100;

            var upperExtremeBand = cma + extremeBand;
            upperExtremeBandList.Add(upperExtremeBand);

            var lowerExtremeBand = cma - extremeBand;
            lowerExtremeBandList.Add(lowerExtremeBand);

            var upperInnerBand = cma + innerBand;
            upperInnerBandList.Add(upperInnerBand);

            var lowerInnerBand = cma - innerBand;
            lowerInnerBandList.Add(lowerInnerBand);

            var upperOuterBand = cma + outerBand;
            upperOuterBandList.Add(upperOuterBand);

            var lowerOuterBand = cma - outerBand;
            lowerOuterBandList.Add(lowerOuterBand);

            var signal = GetCompareSignal(currentValue - cma, prevValue - prevCma1);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperExtremeBand", upperExtremeBandList },
            { "UpperOuterBand", upperOuterBandList },
            { "UpperInnerBand", upperInnerBandList },
            { "MiddleBand", cmaList },
            { "LowerExtremeBand", lowerExtremeBandList },
            { "LowerOuterBand", lowerOuterBandList },
            { "LowerInnerBand", lowerInnerBandList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.HurstBands;

        return stockData;
    }


    /// <summary>
    /// Calculates the Hirashima Sugita RS
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateHirashimaSugitaRS(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length = 1000)
    {
        List<double> d1List = new(stockData.Count);
        List<double> absD1List = new(stockData.Count);
        List<double> d2List = new(stockData.Count);
        List<double> basisList = new(stockData.Count);
        List<double> upper1List = new(stockData.Count);
        List<double> lower1List = new(stockData.Count);
        List<double> upper2List = new(stockData.Count);
        List<double> lower2List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var emaList = GetMovingAverageList(stockData, MovingAvgType.ExponentialMovingAverage, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var ema = emaList[i];

            var d1 = currentValue - ema;
            d1List.Add(d1);

            var absD1 = Math.Abs(d1);
            absD1List.Add(absD1);
        }

        var wmaList = GetMovingAverageList(stockData, maType, length, absD1List);
        stockData.SetCustomValues(d1List);
        var s1List = CalculateLinearRegression(stockData, length).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var ema = emaList[i];
            var s1 = s1List[i];
            var currentValue = inputList[i];
            var x = ema + s1;

            var d2 = currentValue - x;
            d2List.Add(d2);
        }

        stockData.SetCustomValues(d2List);
        var s2List = CalculateLinearRegression(stockData, length).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var ema = emaList[i];
            var s1 = s1List[i];
            var s2 = s2List[i];
            var prevS2 = i >= 1 ? s2List[i - 1] : 0;
            var wma = wmaList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevBasis = GetLastOrDefault(basisList);
            var basis = ema + s1 + (s2 - prevS2);
            basisList.Add(basis);

            var upper1 = basis + wma;
            upper1List.Add(upper1);

            var lower1 = basis - wma;
            lower1List.Add(lower1);

            var upper2 = upper1 + wma;
            upper2List.Add(upper2);

            var lower2 = lower1 - wma;
            lower2List.Add(lower2);

            var signal = GetCompareSignal(currentValue - basis, prevValue - prevBasis);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand1", upper1List },
            { "UpperBand2", upper2List },
            { "MiddleBand", basisList },
            { "LowerBand1", lower1List },
            { "LowerBand2", lower2List }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.HirashimaSugitaRS;

        return stockData;
    }


    /// <summary>
    /// Calculates the Flagging Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateFlaggingBands(this StockData stockData, int length = 14)
    {
        List<double> aList = new(stockData.Count);
        List<double> bList = new(stockData.Count);
        List<double> tavgList = new(stockData.Count);
        List<double> tsList = new(stockData.Count);
        List<double> tosList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var stdDevList = CalculateStandardDeviationVolatility(stockData, length: length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var stdDev = stdDevList[i];
            var prevA1 = i >= 1 ? aList[i - 1] : currentValue;
            var prevB1 = i >= 1 ? bList[i - 1] : currentValue;
            var prevA2 = i >= 2 ? aList[i - 2] : currentValue;
            var prevB2 = i >= 2 ? bList[i - 2] : currentValue;
            var prevA3 = i >= 3 ? aList[i - 3] : currentValue;
            var prevB3 = i >= 3 ? bList[i - 3] : currentValue;
            var l = stdDev != 0 ? (double)1 / length * stdDev : 0;

            var a = currentValue > prevA1 ? prevA1 + (currentValue - prevA1) : prevA2 == prevA3 ? prevA2 - l : prevA2;
            aList.Add(a);

            var b = currentValue < prevB1 ? prevB1 + (currentValue - prevB1) : prevB2 == prevB3 ? prevB2 + l : prevB2;
            bList.Add(b);

            var prevTos = GetLastOrDefault(tosList);
            var tos = currentValue > prevA2 ? 1 : currentValue < prevB2 ? 0 : prevTos;
            tosList.Add(tos);

            var prevTavg = GetLastOrDefault(tavgList);
            var avg = (a + b) / 2;
            var tavg = tos == 1 ? (a + avg) / 2 : (b + avg) / 2;
            tavgList.Add(tavg);

            var ts = (tos * b) + ((1 - tos) * a);
            tsList.Add(ts);

            var signal = GetCompareSignal(currentValue - tavg, prevValue - prevTavg);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", aList },
            { "MiddleBand", tavgList },
            { "LowerBand", bList },
            { "TrailingStop", tsList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.FlaggingBands;

        return stockData;
    }


    /// <summary>
    /// Calculates the Kirshenbaum Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="stdDevFactor"></param>
    /// <returns></returns>
    public static StockData CalculateKirshenbaumBands(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 30, int length2 = 20, double stdDevFactor = 1)
    {
        List<double> topList = new(stockData.Count);
        List<double> bottomList = new(stockData.Count);
        List<double> tempInputList = new(stockData.Count);
        List<double> tempLinRegList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum errorSumWindow = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var emaList = GetMovingAverageList(stockData, maType, length1, inputList);
        var linRegList = CalculateLinearRegression(stockData, length2).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentEma = emaList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var currentValue = inputList[i];
            tempInputList.Add(currentValue);

            var currentLinReg = linRegList[i];
            tempLinRegList.Add(currentLinReg);
            var diff = currentLinReg - currentValue;
            errorSumWindow.Add(diff * diff);

            var sampleCount = Math.Min(length2, errorSumWindow.Count);
            var stdError = sampleCount > 0 ? Sqrt(errorSumWindow.Sum(length2) / sampleCount) : 0;
            stdError = IsValueNullOrInfinity(stdError) ? 0 : stdError;
            var ratio = (double)stdError * stdDevFactor;

            var prevTop = GetLastOrDefault(topList);
            var top = currentEma + ratio;
            topList.Add(top);

            var prevBottom = GetLastOrDefault(bottomList);
            var bottom = currentEma - ratio;
            bottomList.Add(bottom);

            var signal = GetBullishBearishSignal(currentValue - top, prevValue - prevTop, currentValue - bottom, prevValue - prevBottom);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", topList },
            { "MiddleBand", emaList },
            { "LowerBand", bottomList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.KirshenbaumBands;

        return stockData;
    }


    /// <summary>
    /// Calculates the Kaufman Adaptive Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="stdDevFactor"></param>
    /// <returns></returns>
    public static StockData CalculateKaufmanAdaptiveBands(this StockData stockData, int length = 100, double stdDevFactor = 3)
    {
        List<double> upperBandList = new(stockData.Count);
        List<double> lowerBandList = new(stockData.Count);
        List<double> powMaList = new(stockData.Count);
        List<double> middleBandList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var erList = CalculateKaufmanAdaptiveMovingAverage(stockData, length: length).OutputValues["Er"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var er = Pow(erList[i], stdDevFactor);
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevMiddleBand = GetLastOrDefault(middleBandList);
            var middleBand = (currentValue * er) + ((1 - er) * prevMiddleBand);
            middleBandList.Add(middleBand);

            var prevPowMa = GetLastOrDefault(powMaList);
            var powMa = (Pow(currentValue, 2) * er) + ((1 - er) * prevPowMa);
            powMaList.Add(powMa);

            var kaufmanDev = powMa - Pow(middleBand, 2) >= 0 ? Sqrt(powMa - Pow(middleBand, 2)) : 0;
            var prevUpperBand = GetLastOrDefault(upperBandList);
            var upperBand = middleBand + kaufmanDev;
            upperBandList.Add(upperBand);

            var prevLowerBand = GetLastOrDefault(lowerBandList);
            var lowerBand = middleBand - kaufmanDev;
            lowerBandList.Add(lowerBand);

            var signal = GetBollingerBandsSignal(currentValue - middleBand, prevValue - prevMiddleBand, currentValue, prevValue, upperBand,
                prevUpperBand, lowerBand, prevLowerBand);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", upperBandList },
            { "MiddleBand", middleBandList },
            { "LowerBand", lowerBandList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.KaufmanAdaptiveBands;

        return stockData;
    }


    /// <summary>
    /// Calculates the Keltner Channels
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="multFactor"></param>
    /// <returns></returns>
    public static StockData CalculateKeltnerChannels(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 20, int length2 = 10, double multFactor = 2)
    {
        List<double> upperChannelList = new(stockData.Count);
        List<double> lowerChannelList = new(stockData.Count);
        List<double> midChannelList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var emaList = GetMovingAverageList(stockData, maType, length1, inputList);
        var atrList = CalculateAverageTrueRange(stockData, maType, length2).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentEma20Day = emaList[i];
            var currentAtr10Day = atrList[i];

            var upperChannel = currentEma20Day + (multFactor * currentAtr10Day);
            upperChannelList.Add(upperChannel);

            var lowerChannel = currentEma20Day - (multFactor * currentAtr10Day);
            lowerChannelList.Add(lowerChannel);

            var prevMidChannel = GetLastOrDefault(midChannelList);
            var midChannel = (upperChannel + lowerChannel) / 2;
            midChannelList.Add(midChannel);

            var signal = GetCompareSignal(currentValue - midChannel, prevValue - prevMidChannel);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", upperChannelList },
            { "MiddleBand", midChannelList },
            { "LowerBand", lowerChannelList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.KeltnerChannels;

        return stockData;
    }


    /// <summary>
    /// Calculates the Extended Recursive Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateExtendedRecursiveBands(this StockData stockData, int length = 100)
    {
        List<double> aClassicList = new(stockData.Count);
        List<double> bClassicList = new(stockData.Count);
        List<double> cClassicList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var sc = (double)2 / (length + 1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevAClassic = i >= 1 ? aClassicList[i - 1] : currentValue;
            var prevBClassic = i >= 1 ? bClassicList[i - 1] : currentValue;

            var aClassic = Math.Max(prevAClassic, currentValue) - (sc * Math.Abs(currentValue - prevAClassic));
            aClassicList.Add(aClassic);

            var bClassic = Math.Min(prevBClassic, currentValue) + (sc * Math.Abs(currentValue - prevBClassic));
            bClassicList.Add(bClassic);

            var prevCClassic = GetLastOrDefault(cClassicList);
            var cClassic = (aClassic + bClassic) / 2;
            cClassicList.Add(cClassic);

            var signal = GetCompareSignal(currentValue - cClassic, prevValue - prevCClassic);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", aClassicList },
            { "MiddleBand", cClassicList },
            { "LowerBand", bClassicList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.ExtendedRecursiveBands;

        return stockData;
    }


    /// <summary>
    /// Calculates the Efficient Trend Step Channel
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <returns></returns>
    public static StockData CalculateEfficientTrendStepChannel(this StockData stockData, int length = 100, int fastLength = 50, int slowLength = 200)
    {
        List<double> val2List = new(stockData.Count);
        List<double> upperList = new(stockData.Count);
        List<double> lowerList = new(stockData.Count);
        List<double> aList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var erList = CalculateKaufmanAdaptiveMovingAverage(stockData, length).OutputValues["Er"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];

            var val2 = currentValue * 2;
            val2List.Add(val2);
        }

        stockData.SetCustomValues(val2List);
        var stdDevFastList = CalculateStandardDeviationVolatility(stockData, length: fastLength).CustomValuesList;
        stockData.SetCustomValues(val2List);
        var stdDevSlowList = CalculateStandardDeviationVolatility(stockData, length: slowLength).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var er = erList[i];
            var fastStdDev = stdDevFastList[i];
            var slowStdDev = stdDevSlowList[i];
            var prevA = i >= 1 ? aList[i - 1] : currentValue;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var dev = (er * fastStdDev) + ((1 - er) * slowStdDev);

            var a = currentValue > prevA + dev ? currentValue : currentValue < prevA - dev ? currentValue : prevA;
            aList.Add(a);

            var prevUpper = GetLastOrDefault(upperList);
            var upper = a + dev;
            upperList.Add(upper);

            var prevLower = GetLastOrDefault(lowerList);
            var lower = a - dev;
            lowerList.Add(lower);

            var signal = GetBollingerBandsSignal(currentValue - a, prevValue - prevA, currentValue, prevValue, upper, prevUpper, lower, prevLower);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", upperList },
            { "MiddleBand", aList },
            { "LowerBand", lowerList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.EfficientTrendStepChannel;

        return stockData;
    }
}

