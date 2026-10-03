
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the standard deviation channel.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <param name="stdDevMult">The standard dev mult.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateStandardDeviationChannel(this StockData stockData, int length = 40, double stdDevMult = 2)
    {
        List<double> upperBandList = new(stockData.Count);
        List<double> lowerBandList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        List<double> regressionList = new(stockData.Count);
        using var channel = new ExactRegressionChannelWindow(length, stdDevMult);

        for (var i = 0; i < stockData.Count; i++)
        {
            var bands = channel.Next(inputList[i], true);
            var middleBand = bands.Middle;
            regressionList.Add(middleBand);
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevMiddleBand = i >= 1 ? regressionList[i - 1] : 0;

            var prevUpperBand = GetLastOrDefault(upperBandList);
            var upperBand = bands.Upper;
            upperBandList.Add(upperBand);

            var prevLowerBand = GetLastOrDefault(lowerBandList);
            var lowerBand = bands.Lower;
            lowerBandList.Add(lowerBand);

            var signal = GetBollingerBandsSignal(currentValue - middleBand, prevValue - prevMiddleBand, currentValue, prevValue, upperBand, prevUpperBand, lowerBand, prevLowerBand);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", upperBandList },
            { "MiddleBand", regressionList },
            { "LowerBand", lowerBandList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.StandardDeviationChannel;

        return stockData;
    }


    /// <summary>
    /// Calculates the stoller average range channels.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <param name="atrMult">The atr mult.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateStollerAverageRangeChannels(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 14, double atrMult = 2)
    {
        var (input, high, low, _, _) = GetInputValuesList(stockData);
        var external = Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType);
        List<double>? center = null, atr = null;
        if (external)
        {
            var ranges = GetTrueRangeList(stockData);
            center = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(input), Math.Max(1, length))?.ToList() ?? GetMovingAverageList(stockData, maType, length, input);
            atr = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(ranges), Math.Max(1, length))?.ToList() ?? GetMovingAverageList(stockData, maType, length, ranges);
        }
        using var window = external ? null : new KeltnerWindow(maType, length, length, maType, Math.Max(1, input.Count));
        List<double> upper = new(input.Count), middle = new(input.Count), lower = new(input.Count), average = new(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            var stages = external ? (new RocBankValue(center![i]), new RocBankValue(atr![i])) : window!.Next(high[i], low[i], input[i], true);
            var point = RangeChannelWindow.Output(input[i], stages.Item1, stages.Item2, atrMult, false);
            signals?.Add(GetBollingerBandsSignal(input[i] - point.Average, (i > 0 ? input[i - 1] : 0) - (i > 0 ? average[i - 1] : 0), input[i], i > 0 ? input[i - 1] : 0, point.Upper, i > 0 ? upper[i - 1] : 0, point.Lower, i > 0 ? lower[i - 1] : 0));
            upper.Add(point.Upper); middle.Add(point.Middle); lower.Add(point.Lower); average.Add(point.Average);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "UpperBand", upper }, { "MiddleBand", middle }, { "LowerBand", lower }  });
        stockData.SetSignals(signals); stockData.SetCustomValues(new List<double>()); stockData.IndicatorName = IndicatorName.StollerAverageRangeChannels; return stockData;
    }


    /// <summary>
    /// Calculates the Ultimate Moving Average Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="minLength"></param>
    /// <param name="maxLength"></param>
    /// <param name="stdDevMult"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateUltimateMovingAverageBands(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int minLength = 5, int maxLength = 50, double stdDevMult = 2)
    {
        List<double> upperBandList = new(stockData.Count);
        List<double> lowerBandList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var callerSeries = stockData.CaptureInputSeries();
        var umaList = CalculateUltimateMovingAverage(stockData, maType, minLength, maxLength, 1).ChainedValues;
        // The band width is the deviation of the prices, not of the UMA just published onto CustomValuesList.
        stockData.RestoreInputSeries(callerSeries);

        // The deviation of the window about its own mean, not the mean squared residual from a moving average
        // of it. A band at k sigma is the Bollinger construction, and sigma there is the windowed deviation;
        // CalculateStandardDeviationVolatility is a different quantity, about 55% wider on a typical price
        // series, so these bands were about that much too wide - the same defect #186 fixed in the Bollinger
        // bands themselves. Taken over inputList, which is the caller's own series captured above. See #190.
        var stdevList = GetStandardDeviationList(inputList, minLength);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;
            var uma = umaList[i];
            var prevUma = i >= 1 ? umaList[i - 1] : 0;
            var stdev = stdevList[i];

            var prevUpperBand = GetLastOrDefault(upperBandList);
            var upperBand = uma + (stdDevMult * stdev);
            upperBandList.Add(upperBand);

            var prevLowerBand = GetLastOrDefault(lowerBandList);
            var lowerBand = uma - (stdDevMult * stdev);
            lowerBandList.Add(lowerBand);

            var signal = GetBollingerBandsSignal(currentValue - uma, prevVal - prevUma, currentValue, prevVal, upperBand, prevUpperBand,
                lowerBand, prevLowerBand);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", upperBandList },
            { "MiddleBand", umaList },
            { "LowerBand", lowerBandList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.UltimateMovingAverageBands;

        return stockData;
    }


    /// <summary>
    /// Calculates the Uni Channel
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="ubFac"></param>
    /// <param name="lbFac"></param>
    /// <param name="type1"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateUniChannel(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 10, double ubFac = 0.02, double lbFac = 0.02, bool type1 = false)
    {
        UniChannelArithmetic.Validate(ubFac, lbFac); length = Math.Max(1, length); var (input, _, _, _, _) = GetInputValuesList(stockData);
        var middle = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(input), length)?.ToList()
            ?? (maType == MovingAvgType.SimpleMovingAverage ? BollingerArithmetic.Mean(input, length) : GetMovingAverageList(stockData, maType, length, input));
        List<double> upper = new(input.Count), lower = new(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            upper.Add(UniChannelArithmetic.Band(middle[i], ubFac, type1)); lower.Add(UniChannelArithmetic.Band(middle[i], -lbFac, type1));
            signals?.Add(GetBollingerBandsSignal(input[i] - middle[i], i > 0 ? input[i - 1] - middle[i - 1] : 0, input[i], i > 0 ? input[i - 1] : 0, upper[i], i > 0 ? upper[i - 1] : 0, lower[i], i > 0 ? lower[i - 1] : 0));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "UpperBand", upper }, { "MiddleBand", middle }, { "LowerBand", lower } }); stockData.SetSignals(signals); stockData.SetCustomValues(new List<double>()); stockData.IndicatorName = IndicatorName.UniChannel; return stockData;
    }


    /// <summary>
    /// Calculates the Wilson Relative Price Channel
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <param name="overbought"></param>
    /// <param name="oversold"></param>
    /// <param name="upperNeutralZone"></param>
    /// <param name="lowerNeutralZone"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateWilsonRelativePriceChannel(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 34, int smoothLength = 1, double overbought = 70, double oversold = 30, double upperNeutralZone = 55,
        double lowerNeutralZone = 45)
    {
        var result = WilsonWindow.Calculate(stockData, maType, length, smoothLength, new[] { oversold, lowerNeutralZone, overbought, upperNeutralZone });
        var s1List = result.Lines[0].ToList(); var s2List = result.Lines[1].ToList();
        var u1List = result.Lines[2].ToList(); var u2List = result.Lines[3].ToList();
        var signalsList = CreateSignalsList(stockData); signalsList?.AddRange(result.Trades);

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "S1", s1List },
            { "S2", s2List },
            { "U1", u1List },
            { "U2", u2List }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.WilsonRelativePriceChannel;

        return stockData;
    }


    /// <summary>
    /// Calculates the Vortex Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateVortexBands(this StockData stockData, MovingAvgType maType = MovingAvgType.McNichollMovingAverage,
        int length = 20)
    {
        var result = VortexBandWindow.Calculate(stockData, maType, length);
        var upperList = result.Upper.ToList(); var basisList = result.Middle.ToList(); var lowerList = result.Lower.ToList();
        var signalsList = CreateSignalsList(stockData); signalsList?.AddRange(result.Trades);
        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", upperList },
            { "MiddleBand", basisList },
            { "LowerBand", lowerList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.VortexBands;

        return stockData;
    }


    /// <summary>
    /// Calculates the Volume Adaptive Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateVolumeAdaptiveBands(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 100)
    {
        length = Math.Max(1, length); var (input, _, _, _, volume) = GetInputValuesList(stockData); var external = Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType);
        var average = external ? Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(volume), length)?.ToList() ?? GetMovingAverageList(stockData, maType, length, volume) : null;
        using var window = new VolumeAdaptiveBandWindow(maType, length, external, Math.Max(1, input.Count));
        List<double> upper = new(input.Count), middle = new(input.Count), lower = new(input.Count), rawUp = new(input.Count), rawDown = new(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++) { var point = window.Next(input[i], volume[i], true, average?[i]); upper.Add(point.Upper); middle.Add(point.Middle); lower.Add(point.Lower); rawUp.Add(point.RawUp); rawDown.Add(point.RawDown); }
        if (external)
        {
            upper = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(rawUp), length)?.ToList() ?? GetMovingAverageList(stockData, maType, length, rawUp);
            lower = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(rawDown), length)?.ToList() ?? GetMovingAverageList(stockData, maType, length, rawDown);
            for (var i = 0; i < input.Count; i++) middle[i] = VolumeAdaptiveBandWindow.Bands(new RocBankValue(upper[i]), new RocBankValue(lower[i])).Middle;
        }
        for (var i = 0; i < input.Count; i++) signals?.Add(GetCompareSignal(input[i] - middle[i], i > 0 ? input[i - 1] - middle[i - 1] : 0));
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "UpperBand", upper }, { "MiddleBand", middle }, { "LowerBand", lower } }); stockData.SetSignals(signals); stockData.SetCustomValues(new List<double>()); stockData.IndicatorName = IndicatorName.VolumeAdaptiveBands; return stockData;
    }


    /// <summary>
    /// Calculates the Variable Moving Average Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="mult"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateVariableMovingAverageBands(this StockData stockData, MovingAvgType maType = MovingAvgType.VariableMovingAverage,
        int length = 6, double mult = 1.5)
    {
        List<double> ubandList = new(stockData.Count);
        List<double> lbandList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var maList = GetMovingAverageList(stockData, maType, length, inputList);
        var atrList = CalculateAverageTrueRange(stockData, maType, length).ChainedValues;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentAtr = atrList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var vma = maList[i];
            var prevVma = i >= 1 ? maList[i - 1] : 0;
            var o = mult * currentAtr;

            var prevUband = GetLastOrDefault(ubandList);
            var uband = vma + o;
            ubandList.Add(uband);

            var prevLband = GetLastOrDefault(lbandList);
            var lband = vma - o;
            lbandList.Add(lband);

            var signal = GetBollingerBandsSignal(currentValue - vma, prevValue - prevVma, currentValue, prevValue, uband, prevUband, lband, prevLband);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", ubandList },
            { "MiddleBand", maList },
            { "LowerBand", lbandList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.VariableMovingAverageBands;

        return stockData;
    }


    /// <summary>
    /// Calculates the Vervoort Volatility Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="devMult"></param>
    /// <param name="lowBandMult"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateVervoortVolatilityBands(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 8, int length2 = 13, double devMult = 3.55, double lowBandMult = 0.9)
    {
        List<double> typicalList = new(stockData.Count);
        List<double> deviationList = new(stockData.Count);
        List<double> ubList = new(stockData.Count);
        List<double> lbList = new(stockData.Count);
        List<double> tempList = new(stockData.Count);
        List<double> medianAvgSmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum typicalSumWindow = new();
        RollingSum medianAvgSumWindow = new();
        var (inputList, _, lowList, _, _) = GetInputValuesList(stockData);

        var medianAvgList = GetMovingAverageList(stockData, maType, length1, inputList);
        var medianAvgEmaList = GetMovingAverageList(stockData, maType, length1, medianAvgList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var medianAvg = medianAvgList[i];
            tempList.Add(medianAvg);
            medianAvgSumWindow.Add(medianAvg);

            var currentValue = inputList[i];
            var currentLow = lowList[i];
            var prevLow = i >= 1 ? lowList[i - 1] : 0;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var typical = currentValue >= prevValue ? currentValue - prevLow : prevValue - currentLow;
            typicalList.Add(typical);
            typicalSumWindow.Add(typical);

            var typicalSma = typicalSumWindow.Average(length2);
            var deviation = devMult * typicalSma;
            deviationList.Add(deviation);

            var medianAvgSma = medianAvgSumWindow.Average(length1);
            medianAvgSmaList.Add(medianAvgSma);
        }

        var devHighList = GetMovingAverageList(stockData, maType, length1, deviationList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var devHigh = devHighList[i];
            var midline = medianAvgSmaList[i];
            var medianAvgEma = medianAvgEmaList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevMidline = i >= 1 ? medianAvgSmaList[i - 1] : 0;
            var devLow = lowBandMult * devHigh;

            var prevUb = GetLastOrDefault(ubList);
            var ub = medianAvgEma + devHigh;
            ubList.Add(ub);

            var prevLb = GetLastOrDefault(lbList);
            var lb = medianAvgEma - devLow;
            lbList.Add(lb);

            var signal = GetBollingerBandsSignal(currentValue - midline, prevValue - prevMidline, currentValue, prevValue, ub, prevUb, lb, prevLb);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", ubList },
            { "MiddleBand", medianAvgSmaList },
            { "LowerBand", lbList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.VervoortVolatilityBands;

        return stockData;
    }


    /// <summary>
    /// Calculates the Trend Trader Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="mult"></param>
    /// <param name="bandStep"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateTrendTraderBands(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage, 
        int length = 21, double mult = 3, double bandStep = 20)
    {
        TrendTraderWindow.Validate(mult, bandStep); var (input, high, low, _, _) = GetInputValuesList(stockData); var external = Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType);
        List<double>? atr = null;
        if (external) { var ranges = GetTrueRangeList(stockData); atr = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(ranges), Math.Max(1, length))?.ToList() ?? GetMovingAverageList(stockData, maType, Math.Max(1, length), ranges); }
        using var window = new TrendTraderWindow(maType, length, mult, bandStep, external, Math.Max(1, input.Count));
        List<double> upper = new(input.Count), middle = new(input.Count), lower = new(input.Count), raw = new(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++) { var point = window.Next(high[i], low[i], input[i], true, atr is null ? null : new RocBankValue(atr[i])); raw.Add(point.Raw); upper.Add(point.Upper); middle.Add(point.Middle); lower.Add(point.Lower); }
        if (external)
        {
            var average = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(raw), Math.Max(1, length))?.ToList() ?? GetMovingAverageList(stockData, maType, Math.Max(1, length), raw);
            for (var i = 0; i < input.Count; i++) { var point = TrendTraderWindow.Bands(new RocBankValue(average[i]), bandStep); upper[i] = point.Upper; middle[i] = point.Middle; lower[i] = point.Lower; }
        }
        for (var i = 0; i < input.Count; i++) signals?.Add(GetBollingerBandsSignal(input[i] - middle[i], i > 0 ? input[i - 1] - middle[i - 1] : 0, input[i], i > 0 ? input[i - 1] : 0, upper[i], i > 0 ? upper[i - 1] : 0, lower[i], i > 0 ? lower[i - 1] : 0));
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "UpperBand", upper }, { "MiddleBand", middle }, { "LowerBand", lower } }); stockData.SetSignals(signals); stockData.SetCustomValues(new List<double>()); stockData.IndicatorName = IndicatorName.TrendTraderBands; return stockData;
    }


    /// <summary>
    /// Calculates the Time and Money Channel
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateTimeAndMoneyChannel(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length1 = 41, int length2 = 82)
    {
        List<double> yomList = new(stockData.Count);
        List<double> yomSquaredList = new(stockData.Count);
        List<double> varyomList = new(stockData.Count);
        List<double> somList = new(stockData.Count);
        List<double> chPlus1List = new(stockData.Count);
        List<double> chMinus1List = new(stockData.Count);
        List<double> chPlus2List = new(stockData.Count);
        List<double> chMinus2List = new(stockData.Count);
        List<double> chPlus3List = new(stockData.Count);
        List<double> chMinus3List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var halfLength = MinOrMax((int)Math.Ceiling((double)length1 / 2));

        var smaList = GetMovingAverageList(stockData, maType, length1, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevBasis = i >= halfLength ? smaList[i - halfLength] : 0;

            var yom = prevBasis != 0 ? 100 * (currentValue - prevBasis) / prevBasis : 0;
            yomList.Add(yom);

            var yomSquared = Pow(yom, 2);
            yomSquaredList.Add(yomSquared);
        }

        var deviations = maType == MovingAvgType.SimpleMovingAverage ? GetStandardDeviationList(yomList, length2) : null;
        var avyomList = GetMovingAverageList(stockData, maType, length2, yomList);
        var yomSquaredSmaList = GetMovingAverageList(stockData, maType, length2, yomSquaredList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var prevVaryom = i >= halfLength ? varyomList[i - halfLength] : 0;
            var avyom = avyomList[i];
            var yomSquaredSma = yomSquaredSmaList[i];

            var varyom = deviations is not null ? deviations[i] * deviations[i] : yomSquaredSma - (avyom * avyom);
            varyomList.Add(varyom);

            var som = prevVaryom >= 0 ? Sqrt(prevVaryom) : 0;
            somList.Add(som);
        }

        var sigomList = GetMovingAverageList(stockData, maType, length1, somList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var som = somList[i];
            var prevSom = i >= 1 ? somList[i - 1] : 0;
            var sigom = sigomList[i];
            var prevSigom = i >= 1 ? sigomList[i - 1] : 0;
            var basis = smaList[i];

            var chPlus1 = basis * (1 + (0.01 * sigom));
            chPlus1List.Add(chPlus1);

            var chMinus1 = basis * (1 - (0.01 * sigom));
            chMinus1List.Add(chMinus1);

            var chPlus2 = basis * (1 + (0.02 * sigom));
            chPlus2List.Add(chPlus2);

            var chMinus2 = basis * (1 - (0.02 * sigom));
            chMinus2List.Add(chMinus2);

            var chPlus3 = basis * (1 + (0.03 * sigom));
            chPlus3List.Add(chPlus3);

            var chMinus3 = basis * (1 - (0.03 * sigom));
            chMinus3List.Add(chMinus3);

            var signal = GetCompareSignal(som - sigom, prevSom - prevSigom);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ch+1", chPlus1List },
            { "Ch-1", chMinus1List },
            { "Ch+2", chPlus2List },
            { "Ch-2", chMinus2List },
            { "Ch+3", chPlus3List },
            { "Ch-3", chMinus3List },
            { "Median", sigomList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.TimeAndMoneyChannel;

        return stockData;
    }


    /// <summary>
    /// Calculates the Tirone Levels
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateTironeLevels(this StockData stockData, int length = 20)
    {
        List<double> tlhList = new(stockData.Count);
        List<double> clhList = new(stockData.Count);
        List<double> blhList = new(stockData.Count);
        List<double> amList = new(stockData.Count);
        List<double> ehList = new(stockData.Count);
        List<double> elList = new(stockData.Count);
        List<double> rhList = new(stockData.Count);
        List<double> rlList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var inputList=stockData.ChainedValues.Count>0?stockData.ChainedValues:stockData.InputValues;
        using var window=new TironeWindow(length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var levels=window.Next(stockData.HighPrices[i],stockData.LowPrices[i],currentValue,true);

            var tlh = levels[0];
            tlhList.Add(tlh);

            var clh = levels[1];
            clhList.Add(clh);

            var blh = levels[2];
            blhList.Add(blh);

            var prevAm = GetLastOrDefault(amList);
            var am = levels[3];
            amList.Add(am);

            var eh = levels[4];
            ehList.Add(eh);

            var el = levels[5];
            elList.Add(el);

            var rh = levels[6];
            rhList.Add(rh);

            var rl = levels[7];
            rlList.Add(rl);

            var signal = GetCompareSignal(currentValue - am, prevValue - prevAm);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Tlh", tlhList },
            { "Clh", clhList },
            { "Blh", blhList },
            { "Am", amList },
            { "Eh", ehList },
            { "El", elList },
            { "Rh", rhList },
            { "Rl", rlList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.TironeLevels;

        return stockData;
    }


    /// <summary>
    /// Calculates the Time Series Forecast
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateTimeSeriesForecast(this StockData stockData, int length = 500)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); using var window = new TimeSeriesForecastWindow(length); List<double> upper = new(input.Count), middle = new(input.Count), lower = new(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            var p = window.Next(input[i], true); var previous = i > 0 ? input[i - 1] : 0;
            signals?.Add(GetBollingerBandsSignal(input[i] - p.Middle, previous - (i > 0 ? middle[i - 1] : 0), input[i], previous, p.Upper, i > 0 ? upper[i - 1] : 0, p.Lower, i > 0 ? lower[i - 1] : 0)); upper.Add(p.Upper); middle.Add(p.Middle); lower.Add(p.Lower);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "UpperBand", upper }, { "MiddleBand", middle }, { "LowerBand", lower } }); stockData.SetSignals(signals); stockData.SetCustomValues(middle); stockData.IndicatorName = IndicatorName.TimeSeriesForecast; return stockData;
    }


    /// <summary>
    /// Calculates the Smart Envelope
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="factor"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateSmartEnvelope(this StockData stockData, int length = 14, double factor = 1)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); var window = new SmartEnvelopeWindow(length, factor);
        List<double> upper = new(input.Count), middle = new(input.Count), lower = new(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            var point = window.Next(input[i], true); signals?.Add(GetCompareSignal(input[i] - point.Middle, i > 0 ? input[i - 1] - middle[i - 1] : 0));
            upper.Add(point.Upper); middle.Add(point.Middle); lower.Add(point.Lower);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "UpperBand", upper }, { "MiddleBand", middle }, { "LowerBand", lower } });
        stockData.SetSignals(signals); stockData.SetCustomValues(new List<double>()); stockData.IndicatorName = IndicatorName.SmartEnvelope; return stockData;
    }


    /// <summary>
    /// Calculates the Support Resistance
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateSupportResistance(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 20)
    {
        List<double> resList = new(stockData.Count);
        List<double> suppList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var highest = highestList[i];
            var lowest = lowestList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var sma = i >= 1 ? smaList[i - 1] : 0;
            var crossAbove = prevValue < sma && currentValue >= sma;
            var crossBelow = prevValue > sma && currentValue <= sma;

            var prevRes = GetLastOrDefault(resList);
            var res = crossBelow ? highest : i >= 1 ? prevRes : highest;
            resList.Add(res);

            var prevSupp = GetLastOrDefault(suppList);
            var supp = crossAbove ? lowest : i >= 1 ? prevSupp : lowest;
            suppList.Add(supp);

            var signal = GetBullishBearishSignal(currentValue - res, prevValue - prevRes, currentValue - supp, prevValue - prevSupp);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Support", suppList },
            { "Resistance", resList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.SupportResistance;

        return stockData;
    }


    /// <summary>
    /// Calculates the Stationary Extrapolated Levels
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateStationaryExtrapolatedLevels(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 200)
    {
        var values = StationaryLevelsWindow.Calculate(stockData, maType, length, false);
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "UpperBand", values.Upper.ToList() }, { "MiddleBand", values.Middle.ToList() }, { "LowerBand", values.Lower.ToList() }, { "Deviation", values.Deviation.ToList() } });
        var signals = CreateSignalsList(stockData); signals?.AddRange(values.Trades); stockData.SetSignals(signals);
        stockData.SetCustomValues(new List<double>()); stockData.IndicatorName = IndicatorName.StationaryExtrapolatedLevels;
        return stockData;
    }


    /// <summary>
    /// Calculates the Scalper's Channel
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateScalpersChannel(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 15, 
        int length2 = 20)
    {
        var (input, high, low, _, _) = GetInputValuesList(stockData); var external = Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType);
        List<double>? average = null, atr = null;
        if (external)
        {
            var ranges = GetTrueRangeList(stockData);
            average = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(input), Math.Max(1, length2))?.ToList() ?? GetMovingAverageList(stockData, maType, Math.Max(1, length2), input);
            atr = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(ranges), Math.Max(1, length2))?.ToList() ?? GetMovingAverageList(stockData, maType, Math.Max(1, length2), ranges);
        }
        using var window = new ScalperChannelWindow(external ? MovingAvgType.SimpleMovingAverage : maType, length1, length2, Math.Max(1, input.Count));
        List<double> upper = new(input.Count), middle = new(input.Count), lower = new(input.Count), scalper = new(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            var point = window.Next(high[i], low[i], input[i], true, average is null ? null : new RocBankValue(average[i]), atr is null ? null : new RocBankValue(atr[i]));
            signals?.Add(GetCompareSignal(input[i] - point.Scalper, i > 0 ? input[i - 1] - scalper[i - 1] : 0)); upper.Add(point.Upper); middle.Add(point.Middle); lower.Add(point.Lower); scalper.Add(point.Scalper);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "UpperBand", upper }, { "MiddleBand", middle }, { "LowerBand", lower }, { "Scalper", scalper } });
        stockData.SetSignals(signals); stockData.SetCustomValues(new List<double>()); stockData.IndicatorName = IndicatorName.ScalpersChannel; return stockData;
    }


    /// <summary>
    /// Calculates the Smoothed Volatility Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="deviation"></param>
    /// <param name="bandAdjust"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateSmoothedVolatilityBands(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 20, int length2 = 21, double deviation = 2.4, double bandAdjust = 0.9)
    {
        var (input, high, low, _, _) = GetInputValuesList(stockData); var period = SmoothedVolatilityWindow.AtrPeriod(length1);
        var external = Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType); List<double>? atr = null, basis = null, center = null;
        if (external)
        {
            var ranges = GetTrueRangeList(stockData);
            atr = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(ranges), period)?.ToList() ?? GetMovingAverageList(stockData, maType, period, ranges);
            basis = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(input), Math.Max(1, length1))?.ToList() ?? GetMovingAverageList(stockData, maType, length1, input);
            center = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(input), Math.Max(1, length2))?.ToList() ?? GetMovingAverageList(stockData, maType, length2, input);
        }
        using var window = new SmoothedVolatilityWindow(maType, length1, length2, deviation, bandAdjust, external, Math.Max(1, input.Count));
        List<double> upper = new(input.Count), middle = new(input.Count), lower = new(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            var point = window.Next(high[i], low[i], input[i], true, external ? new RocBankValue(atr![i]) : null, external ? new RocBankValue(basis![i]) : null, external ? new RocBankValue(center![i]) : null);
            signals?.Add(GetBollingerBandsSignal(input[i] - point.Middle, i > 0 ? input[i - 1] - middle[i - 1] : 0, input[i], i > 0 ? input[i - 1] : 0, point.Upper, i > 0 ? upper[i - 1] : 0, point.Lower, i > 0 ? lower[i - 1] : 0));
            upper.Add(point.Upper); middle.Add(point.Middle); lower.Add(point.Lower);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "UpperBand", upper }, { "MiddleBand", middle }, { "LowerBand", lower } });
        stockData.SetSignals(signals); stockData.SetCustomValues(new List<double>()); stockData.IndicatorName = IndicatorName.SmoothedVolatilityBands; return stockData;
    }

}

