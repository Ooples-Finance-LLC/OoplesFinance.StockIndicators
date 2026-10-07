
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the bollinger bands.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="stdDevMult">The standard dev mult.</param>
    /// <param name="maType">Average type of the moving.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateBollingerBands(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 20, double stdDevMult = 2)
    {
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var count = inputList.Count;
        var upperBandList = new List<double>(count);
        var lowerBandList = new List<double>(count);
        List<Signal>? signalsList = CreateSignalsList(stockData, count);
        var smaList = maType == MovingAvgType.SimpleMovingAverage ? BollingerArithmetic.Mean(inputList, length) : GetMovingAverageList(stockData, maType, length, inputList);
        // Bollinger's definition: the population standard deviation of the last `length` prices around their
        // own mean. This used to be StandardDeviationVolatility - a different measure - and, through the
        // moving average left on CustomValuesList, of the middle band rather than the prices.
        using var deviation = new ExactPopulationWindow(length);

        double prevUpperBand = 0;
        double prevLowerBand = 0;
        for (var i = 0; i < count; i++)
        {
            var middleBand = smaList[i];
            var currentValue = inputList[i];
            var currentStdDeviation = deviation.Next(currentValue, true);
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevMiddleBand = i >= 1 ? smaList[i - 1] : 0;

            var upperBand = BollingerArithmetic.Band(middleBand, currentStdDeviation, stdDevMult);
            upperBandList.Add(upperBand);

            var lowerBand = BollingerArithmetic.Band(middleBand, currentStdDeviation, -stdDevMult);
            lowerBandList.Add(lowerBand);

            var signal = GetBollingerBandsSignal(currentValue - middleBand, prevValue - prevMiddleBand, currentValue, prevValue, upperBand, prevUpperBand, lowerBand, prevLowerBand);
            signalsList?.Add(signal);

            prevUpperBand = upperBand;
            prevLowerBand = lowerBand;
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", upperBandList },
            { "MiddleBand", smaList },
            { "LowerBand", lowerBandList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.BollingerBands;

        return stockData;
    }


    /// <summary>
    /// Calculates the adaptive price zone indicator.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <param name="pct">The PCT.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateAdaptivePriceZoneIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length = 20, double pct = 2)
    {
        var (input, high, low, _, _) = GetInputValuesList(stockData); var period = AdaptiveZoneWindow.Period(length);
        List<double>? center = null, width = null;
        if (Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType))
        {
            List<double> Mean(List<double> values) => Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(values), period)?.ToList() ?? GetMovingAverageList(stockData, maType, period, values);
            center = Mean(Mean(input)); width = Mean(Mean(high.Select((v, i) => v - low[i]).ToList()));
        }
        using var window = new AdaptiveZoneWindow(maType, length, pct, Math.Max(1, input.Count));
        List<double> upper = new(input.Count), middle = new(input.Count), lower = new(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            var point = center is null ? window.Next(input[i], high[i], low[i], true) : AdaptiveZoneWindow.Bands(new RocBankValue(center[i]), new RocBankValue(width![i]), pct);
            signals?.Add(GetBollingerBandsSignal(input[i] - point.Middle, i == 0 ? 0 : input[i - 1] - middle[i - 1], input[i], i == 0 ? 0 : input[i - 1], point.Upper, i == 0 ? 0 : upper[i - 1], point.Lower, i == 0 ? 0 : lower[i - 1]));
            upper.Add(point.Upper); middle.Add(point.Middle); lower.Add(point.Lower);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "UpperBand", upper }, { "MiddleBand", middle }, { "LowerBand", lower } });
        stockData.SetSignals(signals); stockData.SetCustomValues(new List<double>()); stockData.IndicatorName = IndicatorName.AdaptivePriceZoneIndicator; return stockData;
    }


    /// <summary>
    /// Calculates the Auto Dispersion Bands
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <param name="smoothLength">Length of the smooth.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateAutoDispersionBands(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage, 
        int length = 90, int smoothLength = 140)
    {
        length = Math.Max(1, length); smoothLength = Math.Max(1, smoothLength); var (input, _, _, _, _) = GetInputValuesList(stockData); var external = Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType); using var window = new AutoDispersionWindow(maType, length, smoothLength, external, Math.Max(1, input.Count));
        List<double> upper = new(input.Count), middle = new(input.Count), lower = new(input.Count); var signals = CreateSignalsList(stockData);
        if (external)
        {
            List<double> rawUpper = new(input.Count), rawLower = new(input.Count); foreach (var value in input) { var p = window.Generate(value, true); rawUpper.Add(p.Upper.Publish()); rawLower.Add(p.Lower.Publish()); }
            var firstUpper = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(rawUpper), length)?.ToList() ?? GetMovingAverageList(stockData, maType, length, rawUpper);
            upper = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(firstUpper), smoothLength)?.ToList() ?? GetMovingAverageList(stockData, maType, smoothLength, firstUpper);
            var firstLower = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(rawLower), length)?.ToList() ?? GetMovingAverageList(stockData, maType, length, rawLower);
            lower = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(firstLower), smoothLength)?.ToList() ?? GetMovingAverageList(stockData, maType, smoothLength, firstLower);
            for (var i = 0; i < input.Count; i++) middle.Add(AutoDispersionWindow.Bands(new RocBankValue(upper[i]), new RocBankValue(lower[i])).Middle);
        }
        else foreach (var value in input) { var p = window.Next(value, true); upper.Add(p.Upper); middle.Add(p.Middle); lower.Add(p.Lower); }
        for (var i = 0; i < input.Count; i++) signals?.Add(GetBollingerBandsSignal(input[i] - middle[i], i > 0 ? input[i - 1] - middle[i - 1] : 0, input[i], i > 0 ? input[i - 1] : 0, upper[i], i > 0 ? upper[i - 1] : 0, lower[i], i > 0 ? lower[i - 1] : 0));
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "UpperBand", upper }, { "MiddleBand", middle }, { "LowerBand", lower } }); stockData.SetSignals(signals); stockData.SetCustomValues(new List<double>()); stockData.IndicatorName = IndicatorName.AutoDispersionBands; return stockData;
    }


    /// <summary>
    /// Calculates the Bollinger Bands Fibonacci Ratios
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="fibRatio1"></param>
    /// <param name="fibRatio2"></param>
    /// <param name="fibRatio3"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateBollingerBandsFibonacciRatios(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 20, double fibRatio1 = MathHelper.Phi, double fibRatio2 = MathHelper.Phi + 1, double fibRatio3 = (2 * MathHelper.Phi) + 1)
    {
        // Only the third pair of bands is published. Keep the legacy inner-ratio
        // parameters inert while sharing the verified ATR-band contract.
        stockData.CalculateStollerAverageRangeChannels(maType, length, fibRatio3);
        // Keep publication explicit: generated output metadata reads these keys.
        var bands = stockData.ChainedOutputs;
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> {
            { "UpperBand", bands["UpperBand"] }, { "MiddleBand", bands["MiddleBand"] }, { "LowerBand", bands["LowerBand"] }
        });
        stockData.IndicatorName = IndicatorName.BollingerBandsFibonacciRatios;
        return stockData;
    }


    /// <summary>
    /// Calculates the Bollinger Bands Average True Range
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="atrLength"></param>
    /// <param name="length"></param>
    /// <param name="stdDevMult"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateBollingerBandsAvgTrueRange(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int atrLength = 22, int length = 55, double stdDevMult = 2)
    {
        var (input, high, low, _, _) = GetInputValuesList(stockData); var external = Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType);
        List<double>? atr = null, signalCenter = null;
        if (external)
        {
            // Preserve the component slots: Bollinger basis first, then ATR. The basis
            // cancels from the width and must not affect the ratio through rounding.
            _ = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(input), Math.Max(1, length));
            var ranges = GetTrueRangeList(stockData);
            atr = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(ranges), Math.Max(1, atrLength))?.ToList() ?? GetMovingAverageList(stockData, maType, atrLength, ranges);
            signalCenter = GetMovingAverageList(stockData, maType, atrLength, input);
        }
        using var window = new BollingerAtrWindow(maType, atrLength, length, stdDevMult, external, Math.Max(1, input.Count));
        List<double> values = new(input.Count); var signals = CreateSignalsList(stockData); double previousCenter = 0;
        for (var i = 0; i < input.Count; i++)
        {
            var point = window.Next(high[i], low[i], input[i], true, external ? new RocBankValue(atr![i]) : null, external ? signalCenter![i] : 0);
            signals?.Add(GetVolatilitySignal(input[i] - point.SignalCenter, (i > 0 ? input[i - 1] : 0) - previousCenter, point.Value, i > 0 ? values[i - 1] : 0));
            values.Add(point.Value); previousCenter = point.SignalCenter;
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "AtrDev", values } }); stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.BollingerBandsAverageTrueRange; return stockData;
    }

}

