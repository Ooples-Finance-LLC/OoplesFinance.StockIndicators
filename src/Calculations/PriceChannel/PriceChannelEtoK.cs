
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the fractal chaos bands.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateFractalChaosBands(this StockData stockData)
    {
        List<double> upperBandList = new(stockData.Count);
        List<double> lowerBandList = new(stockData.Count);
        List<double> middleBandList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            // A Williams fractal is a five-bar pattern: the centre must beat the TWO bars on each side of
            // it. Comparing it against one neighbour each way makes it an ordinary three-bar pivot, which
            // fires on any single-bar wiggle and re-anchors the bands far more often than a fractal does.
            // The centre is two bars back, so its right-hand neighbours are the previous bar and the
            // current one, and its left-hand neighbours are three and four bars back. See #202.
            // Nothing is judged until five real bars exist. Missing history reads as 0, and 0 is below any
            // positive price, so evaluating earlier would confirm a fractal whose left-hand neighbour never
            // happened - the same fabricated-value mistake fixed for the log-return windows in #205 and
            // #209.
            var complete = i >= 4;
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var prevHigh1 = i >= 1 ? highList[i - 1] : 0;
            var prevHigh2 = i >= 2 ? highList[i - 2] : 0;
            var prevHigh3 = i >= 3 ? highList[i - 3] : 0;
            var prevHigh4 = i >= 4 ? highList[i - 4] : 0;
            var prevLow1 = i >= 1 ? lowList[i - 1] : 0;
            var prevLow2 = i >= 2 ? lowList[i - 2] : 0;
            var prevLow3 = i >= 3 ? lowList[i - 3] : 0;
            var prevLow4 = i >= 4 ? lowList[i - 4] : 0;
            var currentClose = inputList[i];
            var prevClose = i >= 1 ? inputList[i - 1] : 0;
            double oklUpper = complete && prevHigh1 < prevHigh2 && currentHigh < prevHigh2 ? 1 : 0;
            double okrUpper = complete && prevHigh3 < prevHigh2 && prevHigh4 < prevHigh2 ? 1 : 0;
            double oklLower = complete && prevLow1 > prevLow2 && currentLow > prevLow2 ? 1 : 0;
            double okrLower = complete && prevLow3 > prevLow2 && prevLow4 > prevLow2 ? 1 : 0;

            var prevUpperBand = GetLastOrDefault(upperBandList);
            var upperBand = oklUpper == 1 && okrUpper == 1 ? prevHigh2 : prevUpperBand;
            upperBandList.Add(upperBand);

            var prevLowerBand = GetLastOrDefault(lowerBandList);
            var lowerBand = oklLower == 1 && okrLower == 1 ? prevLow2 : prevLowerBand;
            lowerBandList.Add(lowerBand);

            var prevMiddleBand = GetLastOrDefault(middleBandList);
            var midpoint = new ExactMeanAccumulator(); midpoint.Add(upperBand); midpoint.Add(lowerBand);
            var middleBand = midpoint.Mean(2);
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
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateInterquartileRangeBands(this StockData stockData, int length = 14, double mult = 1.5)
    {
        List<double> upperBandList = new(stockData.Count);
        List<double> lowerBandList = new(stockData.Count);
        List<double> middleBandList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var trimeanList = CalculateTrimean(stockData, length);
        var q1List = trimeanList.ChainedOutputs["Q1"];
        var q3List = trimeanList.ChainedOutputs["Q3"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var q1 = q1List[i];
            var q3 = q3List[i];

            var upperBand = RangeBandArithmetic.Band(q3, q3, q1, mult);
            upperBandList.Add(upperBand);

            var lowerBand = RangeBandArithmetic.Band(q1, q3, q1, -mult);
            lowerBandList.Add(lowerBand);

            var prevMiddleBand = GetLastOrDefault(middleBandList);
            var middleBand = RangeBandArithmetic.Midpoint(q1, q3);
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
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateGChannels(this StockData stockData, int length = 100)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); var window = new GChannelWindow(length);
        List<double> upper = new(input.Count), middle = new(input.Count), lower = new(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            var point = window.Next(input[i], true); signals?.Add(GetCompareSignal(input[i] - point.Middle, i > 0 ? input[i - 1] - middle[i - 1] : 0));
            upper.Add(point.Upper); middle.Add(point.Middle); lower.Add(point.Lower);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "UpperBand", upper }, { "MiddleBand", middle }, { "LowerBand", lower } });
        stockData.SetSignals(signals); stockData.SetCustomValues(new List<double>()); stockData.IndicatorName = IndicatorName.GChannels; return stockData;
    }


    /// <summary>
    /// Calculates the High Low Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateHighLowMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length = 14)
    {
        var (input, highs, lows, _, _) = GetInputValuesList(stockData);
        List<double> upper = new(stockData.Count), middle = new(stockData.Count), lower = new(stockData.Count);
        List<Signal>? signals = CreateSignalsList(stockData);
        if (Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType))
        {
            var (highest, lowest) = GetMaxAndMinValuesList(highs, lows, length);
            upper = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(highest), length)?.ToList() ?? GetMovingAverageList(stockData, maType, length, highest);
            lower = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(lowest), length)?.ToList() ?? GetMovingAverageList(stockData, maType, length, lowest);
            middle = upper.Select((value, i) => HighLowAverageWindow.Midpoint(value, lower[i])).ToList();
        }
        else
        {
            using var window = new HighLowAverageWindow(maType, length);
            for (var i = 0; i < input.Count; i++) { var value = window.Next(highs[i], lows[i], true); upper.Add(value.Upper); middle.Add(value.Middle); lower.Add(value.Lower); }
        }
        for (var i = 0; i < input.Count; i++) signals?.Add(GetCompareSignal(input[i] - middle[i], i == 0 ? 0 : input[i - 1] - middle[i - 1]));
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "UpperBand", upper }, { "MiddleBand", middle }, { "LowerBand", lower } });
        stockData.SetSignals(signals); stockData.SetCustomValues(new List<double>()); stockData.IndicatorName = IndicatorName.HighLowMovingAverage;
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
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateHighLowBands(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14,
        double pctShift = 1)
    {
        HighLowBandsWindow.ValidateShift(pctShift);
        List<double> highBandList = new(stockData.Count);
        List<double> lowBandList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        List<double> tmaList2;
        if (Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType))
        {
            var tmaList1 = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(inputList), length)?.ToList() ?? GetMovingAverageList(stockData, maType, length, inputList);
            tmaList2 = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(tmaList1), length)?.ToList() ?? GetMovingAverageList(stockData, maType, length, tmaList1);
        }
        else
        {
            tmaList2 = new(stockData.Count); using var window = new HighLowBandsWindow(maType, length);
            foreach (var price in inputList) tmaList2.Add(window.Next(price, true));
        }

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var tma = tmaList2[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevTma = i >= 1 ? tmaList2[i - 1] : 0;

            var prevHighBand = GetLastOrDefault(highBandList);
            var highBand = HighLowBandsWindow.Shift(tma, pctShift);
            highBandList.Add(highBand);

            var prevLowBand = GetLastOrDefault(lowBandList);
            var lowBand = HighLowBandsWindow.Shift(tma, -pctShift);
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
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateHurstCycleChannel(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod,
        int fastLength = 10, int slowLength = 30, double fastMult = 1, double slowMult = 3)
    {
        HighLowBandsWindow.ValidateShift(fastMult); HighLowBandsWindow.ValidateShift(slowMult); var (input, high, low, _, _) = GetInputValuesList(stockData); var external = Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType);
        var fastCycle = HurstCycleWindow.HalfCycle(fastLength); var slowCycle = HurstCycleWindow.HalfCycle(slowLength); List<double>? fastAtr = null, slowAtr = null, fastMean = null, slowMean = null;
        if (external)
        {
            var ranges = GetTrueRangeList(stockData);
            fastAtr = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(ranges), fastCycle)?.ToList() ?? GetMovingAverageList(stockData, maType, fastCycle, ranges);
            slowAtr = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(ranges), slowCycle)?.ToList() ?? GetMovingAverageList(stockData, maType, slowCycle, ranges);
            fastMean = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(input), fastCycle)?.ToList() ?? GetMovingAverageList(stockData, maType, fastCycle, input);
            slowMean = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(input), slowCycle)?.ToList() ?? GetMovingAverageList(stockData, maType, slowCycle, input);
        }
        using var window = new HurstCycleWindow(maType, fastLength, slowLength, fastMult, slowMult, external, Math.Max(1, input.Count));
        List<double> fu = new(input.Count), fm = new(input.Count), fl = new(input.Count), su = new(input.Count), sm = new(input.Count), sl = new(input.Count), om = new(input.Count), os = new(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            var point = window.Next(high[i], low[i], input[i], true, external ? new RocBankValue(fastAtr![i]) : null, external ? new RocBankValue(slowAtr![i]) : null, external ? new RocBankValue(fastMean![i]) : null, external ? new RocBankValue(slowMean![i]) : null);
            fu.Add(point.FastUpper); fm.Add(point.FastMiddle); fl.Add(point.FastLower); su.Add(point.SlowUpper); sm.Add(point.SlowMiddle); sl.Add(point.SlowLower); om.Add(point.OMed); os.Add(point.OShort);
            signals?.Add(GetBullishBearishSignal(input[i] - fu[i], i > 0 ? input[i - 1] - fu[i - 1] : 0, input[i] - fl[i], i > 0 ? input[i - 1] - fl[i - 1] : 0));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "FastUpperBand", fu }, { "FastMiddleBand", fm }, { "FastLowerBand", fl }, { "SlowUpperBand", su }, { "SlowMiddleBand", sm }, { "SlowLowerBand", sl }, { "OMed", om }, { "OShort", os } }); stockData.SetSignals(signals); stockData.SetCustomValues(new List<double>()); stockData.IndicatorName = IndicatorName.HurstCycleChannel; return stockData;
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
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateHurstBands(this StockData stockData, int length = 10, double innerMult = 1.6, double outerMult = 2.6,
        double extremeMult = 4.2)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); using var window = new HurstBandWindow(length, innerMult, outerMult, extremeMult);
        List<double> middle = new(input.Count), upperInner = new(input.Count), lowerInner = new(input.Count), upperOuter = new(input.Count), lowerOuter = new(input.Count), upperExtreme = new(input.Count), lowerExtreme = new(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            var point = window.Next(input[i], true); signals?.Add(GetCompareSignal(input[i] - point.Middle, i > 0 ? input[i - 1] - middle[i - 1] : 0));
            middle.Add(point.Middle); upperInner.Add(point.UpperInner); lowerInner.Add(point.LowerInner); upperOuter.Add(point.UpperOuter); lowerOuter.Add(point.LowerOuter); upperExtreme.Add(point.UpperExtreme); lowerExtreme.Add(point.LowerExtreme);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "UpperExtremeBand", upperExtreme }, { "UpperOuterBand", upperOuter }, { "UpperInnerBand", upperInner }, { "MiddleBand", middle }, { "LowerExtremeBand", lowerExtreme }, { "LowerOuterBand", lowerOuter }, { "LowerInnerBand", lowerInner } }); stockData.SetSignals(signals); stockData.SetCustomValues(new List<double>()); stockData.IndicatorName = IndicatorName.HurstBands; return stockData;
    }


    /// <summary>
    /// Calculates the Hirashima Sugita RS
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateHirashimaSugitaRS(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length = 1000)
    {
        if (StrengthWindow.Supports(maType))
        {
            var (input, _, _, _, _) = GetInputValuesList(stockData); var result = HirashimaWindow.Calculate(input, maType, length, false);
            var keys = new[] { "UpperBand1", "UpperBand2", "MiddleBand", "LowerBand1", "LowerBand2" };
            stockData.SetOutputValues(() => keys.Select((key, i) => (key, values: result.Bands[i].ToList())).ToDictionary(v => v.key, v => v.values));
            var signals = CreateSignalsList(stockData); if (signals is not null) signals.AddRange(result.Trades); stockData.SetSignals(signals);
            stockData.SetCustomValues(new List<double>()); stockData.IndicatorName = IndicatorName.HirashimaSugitaRS; return stockData;
        }

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
        var s1List = CalculateLinearRegression(stockData, length).ChainedValues;
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
        var s2List = CalculateLinearRegression(stockData, length).ChainedValues;
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

            var upper1 = HirashimaWindow.Band(basis, wma, 1);
            upper1List.Add(upper1);

            var lower1 = HirashimaWindow.Band(basis, wma, -1);
            lower1List.Add(lower1);

            var upper2 = HirashimaWindow.Band(basis, wma, 2);
            upper2List.Add(upper2);

            var lower2 = HirashimaWindow.Band(basis, wma, -2);
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
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateFlaggingBands(this StockData stockData, int length = 14)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); using var window = new FlaggingBandWindow(length);
        List<double> upper = new(input.Count), middle = new(input.Count), lower = new(input.Count), stops = new(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            var point = window.Next(input[i], true); signals?.Add(GetCompareSignal(input[i] - point.Middle, i > 0 ? input[i - 1] - middle[i - 1] : 0));
            upper.Add(point.Upper); middle.Add(point.Middle); lower.Add(point.Lower); stops.Add(point.Stop);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "UpperBand", upper }, { "MiddleBand", middle }, { "LowerBand", lower }, { "TrailingStop", stops } });
        stockData.SetSignals(signals); stockData.SetCustomValues(new List<double>()); stockData.IndicatorName = IndicatorName.FlaggingBands; return stockData;
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
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateKirshenbaumBands(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 30, int length2 = 20, double stdDevFactor = 1)
    {
        HighLowBandsWindow.ValidateShift(stdDevFactor); length1 = Math.Max(1, length1); var (input, _, _, _, _) = GetInputValuesList(stockData); var external = Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType); var mean = external ? Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(input), length1)?.ToList() ?? GetMovingAverageList(stockData, maType, length1, input) : null;
        using var window = new KirshenbaumWindow(maType, length1, length2, stdDevFactor, external, Math.Max(1, input.Count)); List<double> upper = new(input.Count), middle = new(input.Count), lower = new(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            var p = window.Next(input[i], true, mean?[i]); signals?.Add(GetBullishBearishSignal(input[i] - p.Upper, i > 0 ? input[i - 1] - upper[i - 1] : 0, input[i] - p.Lower, i > 0 ? input[i - 1] - lower[i - 1] : 0)); upper.Add(p.Upper); middle.Add(p.Middle); lower.Add(p.Lower);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "UpperBand", upper }, { "MiddleBand", middle }, { "LowerBand", lower } }); stockData.SetSignals(signals); stockData.SetCustomValues(new List<double>()); stockData.IndicatorName = IndicatorName.KirshenbaumBands; return stockData;
    }


    /// <summary>
    /// Calculates the Kaufman Adaptive Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="stdDevFactor"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateKaufmanAdaptiveBands(this StockData stockData, int length = 100, double stdDevFactor = 3)
    {
        var window = new KaufmanAdaptiveBandWindow(length, stdDevFactor);
        var prices = stockData.ChainedValues.Count > 0 ? stockData.ChainedValues : stockData.InputValues;
        foreach (var price in prices) Streaming.StreamingInputValidation.Finite(price, nameof(stockData));
        List<double> upper = new(prices.Count), middle = new(prices.Count), lower = new(prices.Count);
        var signals = CreateSignalsList(stockData);
        foreach (var price in prices)
        {
            var point = window.Next(price, true);
            upper.Add(point.Upper); middle.Add(point.Middle); lower.Add(point.Lower); signals?.Add(point.Trade);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>>
        { { "UpperBand", upper }, { "MiddleBand", middle }, { "LowerBand", lower } });
        stockData.SetSignals(signals); stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.KaufmanAdaptiveBands; return stockData;
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
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateKeltnerChannels(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 20, int length2 = 10, double multFactor = 2,
        MovingAvgType atrMaType = MovingAvgType.WildersSmoothingMethod)
    {
        var (input, high, low, _, _) = GetInputValuesList(stockData);
        List<double>? customAtr = null, customMiddle = null;
        if (Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType) || !StrengthWindow.Supports(atrMaType))
        {
            var ranges = GetTrueRangeList(stockData);
            customAtr = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(ranges), Math.Max(1, length2))?.ToList() ?? GetMovingAverageList(stockData, atrMaType, length2, ranges);
            customMiddle = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(input), Math.Max(1, length1))?.ToList() ?? GetMovingAverageList(stockData, maType, length1, input);
        }
        using var window = customMiddle is null ? new KeltnerWindow(maType, length1, length2, atrMaType, Math.Max(1, input.Count)) : null;
        List<double> upper = new(input.Count), middle = new(input.Count), lower = new(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            var stages = customMiddle is null ? window!.Next(high[i], low[i], input[i], true) : (new RocBankValue(customMiddle[i]), new RocBankValue(customAtr![i]));
            var point = KeltnerWindow.Bands(stages.Item1, stages.Item2, multFactor);
            signals?.Add(GetCompareSignal(input[i] - point.Middle, i == 0 ? 0 : input[i - 1] - middle[i - 1])); upper.Add(point.Upper); middle.Add(point.Middle); lower.Add(point.Lower);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "UpperBand", upper }, { "MiddleBand", middle }, { "LowerBand", lower } });
        stockData.SetSignals(signals); stockData.SetCustomValues(new List<double>()); stockData.IndicatorName = IndicatorName.KeltnerChannels; return stockData;
    }


    /// <summary>
    /// Calculates the Extended Recursive Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateExtendedRecursiveBands(this StockData stockData, int length = 100)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); var window = new ExtendedBandWindow(length);
        List<double> upper = new(input.Count), middle = new(input.Count), lower = new(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            var point = window.Next(input[i], true); signals?.Add(GetCompareSignal(input[i] - point.Middle, i > 0 ? input[i - 1] - middle[i - 1] : 0));
            upper.Add(point.Upper); middle.Add(point.Middle); lower.Add(point.Lower);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "UpperBand", upper }, { "MiddleBand", middle }, { "LowerBand", lower } });
        stockData.SetSignals(signals); stockData.SetCustomValues(new List<double>()); stockData.IndicatorName = IndicatorName.ExtendedRecursiveBands; return stockData;
    }


    /// <summary>
    /// Calculates the Efficient Trend Step Channel
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEfficientTrendStepChannel(this StockData stockData, int length = 100, int fastLength = 50, int slowLength = 200)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); using var window = new EfficientTrendStepWindow(length, fastLength, slowLength);
        List<double> upper = new(input.Count), middle = new(input.Count), lower = new(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            var point = window.Next(input[i], true); signals?.Add(GetBollingerBandsSignal(input[i] - point.Middle, i > 0 ? input[i - 1] - middle[i - 1] : -input[i], input[i], i > 0 ? input[i - 1] : 0, point.Upper, i > 0 ? upper[i - 1] : 0, point.Lower, i > 0 ? lower[i - 1] : 0)); upper.Add(point.Upper); middle.Add(point.Middle); lower.Add(point.Lower);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "UpperBand", upper }, { "MiddleBand", middle }, { "LowerBand", lower } }); stockData.SetSignals(signals); stockData.SetCustomValues(new List<double>()); stockData.IndicatorName = IndicatorName.EfficientTrendStepChannel; return stockData;
    }
}

