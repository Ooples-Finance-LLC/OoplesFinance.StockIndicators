using OoplesFinance.StockIndicators.Compatibility;
using OoplesFinance.StockIndicators.Core;

namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Aroon Up.
    /// </summary>
    /// <remarks>
    /// How recently the window's highest high occurred, as a percentage: a hundred on the bar that sets a new
    /// high, falling towards zero as that high recedes. The window looks back <paramref name="length"/> bars
    /// from the current one, and where the high is equalled more than once the most recent occurrence counts,
    /// since it is the freshness of the high the reading is about.
    /// </remarks>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateAroonUp(this StockData stockData, int length = 25)
    {
        length = Math.Max(length, 1);
        var (_, highList, _, _, _) = GetInputValuesList(stockData);
        var count = highList.Count;
        List<double> aroonUpList = new(count);
        List<Signal>? signalsList = CreateSignalsList(stockData, count);

        for (var i = 0; i < count; i++)
        {
            double aroonUp = 0;
            if (i >= length)
            {
                var highestIndex = i;
                var highestValue = double.MinValue;
                for (var j = i - length; j <= i; j++)
                {
                    if (highList[j] >= highestValue)
                    {
                        highestValue = highList[j];
                        highestIndex = j;
                    }
                }

                aroonUp = 100.0 * (length - (i - highestIndex)) / length;
            }

            aroonUpList.Add(aroonUp);

            var prevAroonUp = i >= 1 ? aroonUpList[i - 1] : 0;
            var signal = GetCompareSignal(aroonUp - prevAroonUp, 0);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "AroonUp", aroonUpList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(aroonUpList);
        stockData.IndicatorName = IndicatorName.AroonUp;

        return stockData;
    }

    /// <summary>
    /// Calculates the Aroon Down.
    /// </summary>
    /// <remarks>
    /// How recently the window's lowest low occurred, as a percentage, and the mirror of
    /// <see cref="CalculateAroonUp"/>: a hundred on the bar that sets a new low, falling towards zero as that
    /// low recedes. Where the low is equalled more than once the most recent occurrence counts.
    /// </remarks>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateAroonDown(this StockData stockData, int length = 25)
    {
        length = Math.Max(length, 1);
        var (_, _, lowList, _, _) = GetInputValuesList(stockData);
        var count = lowList.Count;
        List<double> aroonDownList = new(count);
        List<Signal>? signalsList = CreateSignalsList(stockData, count);

        for (var i = 0; i < count; i++)
        {
            double aroonDown = 0;
            if (i >= length)
            {
                var lowestIndex = i;
                var lowestValue = double.MaxValue;
                for (var j = i - length; j <= i; j++)
                {
                    if (lowList[j] <= lowestValue)
                    {
                        lowestValue = lowList[j];
                        lowestIndex = j;
                    }
                }

                aroonDown = 100.0 * (length - (i - lowestIndex)) / length;
            }

            aroonDownList.Add(aroonDown);

            var prevAroonDown = i >= 1 ? aroonDownList[i - 1] : 0;
            var signal = GetCompareSignal(aroonDown - prevAroonDown, 0);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "AroonDown", aroonDownList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(aroonDownList);
        stockData.IndicatorName = IndicatorName.AroonDown;

        return stockData;
    }

    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateAbsoluteStrengthIndex(this StockData stockData, int length = 10, int maLength = 21, int signalLength = 34)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        if (input.Any(value => !(value > 0) || double.IsInfinity(value)))
            throw new ArgumentOutOfRangeException(nameof(stockData), "Absolute Strength Index requires strictly positive finite effective prices.");
        var window = new AbsoluteStrengthWindow(length, maLength, signalLength);
        List<double> line = new(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            var value = window.Next(input[i], true); line.Add(value);
            signals?.Add(GetCompareSignal(value, i > 0 ? line[i - 1] : 0));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Asi", line } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.AbsoluteStrengthIndex;
        return stockData;
    }


    /// <summary>
    /// Calculates the Accumulative Swing Index
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <param name="limitMove">The market's limit move, Wilder's T; 0 uses each bar's range instead.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateAccumulativeSwingIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14,
        double limitMove = 0)
    {
        length = Math.Max(1, length);
        var (input, high, low, open, _) = GetInputValuesList(stockData);
        using var window = new AccumulatedSwingWindow(maType, length, limitMove, Math.Max(1, input.Count));
        List<double> line = new(input.Count), signal = new(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            var value = window.Next(open[i], high[i], low[i], input[i], true); line.Add(value.Value); signal.Add(value.Signal);
            signals?.Add(GetCompareSignal(value.Value, i > 0 ? line[i - 1] : 0));
        }
        if (Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType))
            signal = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(line), length)?.ToList() ?? GetMovingAverageList(stockData, maType, length, line);
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Asi", line }, { "Signal", signal } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.AccumulativeSwingIndex;
        return stockData;
    }


    /// <summary>
    /// Calculates the Adaptive Ergodic Candlestick Oscillator
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="smoothLength">Length of the smooth.</param>
    /// <param name="stochLength">Length of the stoch.</param>
    /// <param name="signalLength">Length of the signal.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateAdaptiveErgodicCandlestickOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int smoothLength = 5, int stochLength = 14, int signalLength = 9)
    {
        var (input, high, low, open, _) = GetInputValuesList(stockData);
        using var window = new AdaptiveCandleWindow(maType, smoothLength, stochLength, signalLength, Math.Max(1, input.Count));
        List<double> eco = new(input.Count), line = new(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++) { var point = window.Next(open[i], high[i], low[i], input[i], true); eco.Add(point.Eco.Publish()); line.Add(point.Signal); }
        if (Builder.Compute.ComponentAverage.HasOverrides) line = (Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(eco), Math.Max(1, signalLength)) ?? line).ToList();
        for (var i = 0; i < input.Count; i++) signals?.Add(GetCompareSignal(eco[i] - line[i], i == 0 ? 0 : eco[i - 1] - line[i - 1]));
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Eco", eco }, { "Signal", line } });
        stockData.SetSignals(signals); stockData.SetCustomValues(eco); stockData.IndicatorName = IndicatorName.AdaptiveErgodicCandlestickOscillator; return stockData;
    }


    /// <summary>
    /// Calculates the Absolute Strength MTF Indicator
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <param name="smoothLength">Length of the smooth.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateAbsoluteStrengthMTFIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 50, int smoothLength = 25)
    {
        length = Math.Max(1, length); smoothLength = Math.Max(1, smoothLength);
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        List<double> bulls = new(input.Count), bears = new(input.Count); var signals = CreateSignalsList(stockData);
        if (Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType))
        {
            var lagged = input.Select((_, i) => i == 0 ? 0 : input[i - 1]).ToList();
            List<double> Average(List<double> values, int period) => Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(values), period)?.ToList() ?? GetMovingAverageList(stockData, maType, period, values);
            var current = Average(input.ToList(), length); var previous = Average(lagged, length);
            for (var i = 0; i < input.Count; i++)
            {
                var difference = AbsoluteStrengthMtfWindow.Difference(new RocBankValue(current[i]), new RocBankValue(previous[i]));
                bulls.Add(difference.Mantissa > 0 ? difference.Publish() : 0); bears.Add(difference.Mantissa < 0 ? -difference.Publish() : 0);
            }
            bulls = Average(bulls, smoothLength); bears = Average(bears, smoothLength);
        }
        else
        {
            using var window = new AbsoluteStrengthMtfWindow(maType, length, smoothLength, Math.Max(1, input.Count));
            foreach (var price in input) { var value = window.Next(price, true); bulls.Add(value.Bulls); bears.Add(value.Bears); }
        }
        for (var i = 0; i < input.Count; i++) signals?.Add(GetCompareSignal(bulls[i] - bears[i], i > 0 ? bulls[i - 1] - bears[i - 1] : 0));
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Bulls", bulls }, { "Bears", bears } });
        stockData.SetSignals(signals); stockData.SetCustomValues(new List<double>()); stockData.IndicatorName = IndicatorName.AbsoluteStrengthMTFIndicator;
        return stockData;
    }


    /// <summary>
    /// Calculates the Bayesian Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="stdDevMult"></param>
    /// <param name="lowerThreshold"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateBayesianOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 20, double stdDevMult = 2.5, double lowerThreshold = 15)
    {
        List<double> sigmaProbsDownList = new(stockData.Count);
        List<double> sigmaProbsUpList = new(stockData.Count);
        List<double> probPrimeList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var probBbUpperUpSumWindow = new RollingSum();
        var probBbUpperDownSumWindow = new RollingSum();
        var probBbBasisUpSumWindow = new RollingSum();
        var probBbBasisDownSumWindow = new RollingSum();

        var bbList = CalculateBollingerBands(stockData, maType, length, stdDevMult);
        var upperBbList = bbList.ChainedOutputs["UpperBand"];
        var basisList = bbList.ChainedOutputs["MiddleBand"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var upperBb = upperBbList[i];
            var basis = basisList[i];

            double probBbUpperUpSeq = currentValue > upperBb ? 1 : 0;
            probBbUpperUpSumWindow.Add(probBbUpperUpSeq);
            var probBbUpperUp = probBbUpperUpSumWindow.Average(length);

            double probBbUpperDownSeq = currentValue < upperBb ? 1 : 0;
            probBbUpperDownSumWindow.Add(probBbUpperDownSeq);
            var probBbUpperDown = probBbUpperDownSumWindow.Average(length);
            var probUpBbUpper = probBbUpperUp + probBbUpperDown != 0 ? probBbUpperUp / (probBbUpperUp + probBbUpperDown) : 0;
            var probDownBbUpper = probBbUpperUp + probBbUpperDown != 0 ? probBbUpperDown / (probBbUpperUp + probBbUpperDown) : 0;

            double probBbBasisUpSeq = currentValue > basis ? 1 : 0;
            probBbBasisUpSumWindow.Add(probBbBasisUpSeq);
            var probBbBasisUp = probBbBasisUpSumWindow.Average(length);

            double probBbBasisDownSeq = currentValue < basis ? 1 : 0;
            probBbBasisDownSumWindow.Add(probBbBasisDownSeq);
            var probBbBasisDown = probBbBasisDownSumWindow.Average(length);

            var probUpBbBasis = probBbBasisUp + probBbBasisDown != 0 ? probBbBasisUp / (probBbBasisUp + probBbBasisDown) : 0;
            var probDownBbBasis = probBbBasisUp + probBbBasisDown != 0 ? probBbBasisDown / (probBbBasisUp + probBbBasisDown) : 0;

            var prevSigmaProbsDown = GetLastOrDefault(sigmaProbsDownList);
            var sigmaProbsDown = BayesianProbability.Combine(probUpBbUpper, probUpBbBasis);
            sigmaProbsDownList.Add(sigmaProbsDown);

            var prevSigmaProbsUp = GetLastOrDefault(sigmaProbsUpList);
            var sigmaProbsUp = BayesianProbability.Combine(probDownBbUpper, probDownBbBasis);
            sigmaProbsUpList.Add(sigmaProbsUp);

            var prevProbPrime = GetLastOrDefault(probPrimeList);
            var probPrime = BayesianProbability.Combine(sigmaProbsDown, sigmaProbsUp);
            probPrimeList.Add(probPrime);

            var longUsingProbPrime = probPrime > lowerThreshold / 100 && prevProbPrime == 0;
            var longUsingSigmaProbsUp = sigmaProbsUp < 1 && prevSigmaProbsUp == 1;
            var shortUsingProbPrime = probPrime == 0 && prevProbPrime > lowerThreshold / 100;
            var shortUsingSigmaProbsDown = sigmaProbsDown < 1 && prevSigmaProbsDown == 1;

            var signal = GetConditionSignal(longUsingProbPrime || longUsingSigmaProbsUp, shortUsingProbPrime || shortUsingSigmaProbsDown);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "SigmaProbsDown", sigmaProbsDownList },
            { "SigmaProbsUp", sigmaProbsUpList },
            { "ProbPrime", probPrimeList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.BayesianOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Bear Power Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateBearPowerIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14)
    {
        length = Math.Max(1, length); var (input, high, low, open, _) = GetInputValuesList(stockData); var external = Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType);
        using var window = new CandlePowerWindow(false, maType, length, external, input.Count); List<double> values = new(input.Count), average = new(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++) { var p = window.Next(input[i], open[i], high[i], low[i], true); values.Add(p.Value); average.Add(p.Signal); }
        if (external) average = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(values), length)?.ToList() ?? GetMovingAverageList(stockData, maType, length, values);
        for (var i = 0; i < input.Count; i++) signals?.Add(GetCompareSignal(values[i] - average[i], i > 0 ? values[i - 1] - average[i - 1] : 0, true));
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "BearPower", values }, { "Signal", average } }); stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.BearPowerIndicator; return stockData;
    }


    /// <summary>
    /// Calculates the Bull Power Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateBullPowerIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14)
    {
        length = Math.Max(1, length); var (input, high, low, open, _) = GetInputValuesList(stockData); var external = Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType);
        using var window = new CandlePowerWindow(true, maType, length, external, input.Count); List<double> values = new(input.Count), average = new(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++) { var p = window.Next(input[i], open[i], high[i], low[i], true); values.Add(p.Value); average.Add(p.Signal); }
        if (external) average = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(values), length)?.ToList() ?? GetMovingAverageList(stockData, maType, length, values);
        for (var i = 0; i < input.Count; i++) signals?.Add(GetCompareSignal(values[i] - average[i], i > 0 ? values[i - 1] - average[i - 1] : 0, true));
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "BullPower", values }, { "Signal", average } }); stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.BullPowerIndicator; return stockData;
    }


    /// <summary>
    /// Calculates the Belkhayate Timing
    /// </summary>
    /// <param name="stockData"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateBelkhayateTiming(this StockData stockData)
    {
        List<double> bList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var prevHigh1 = i >= 1 ? highList[i - 1] : 0;
            var prevLow1 = i >= 1 ? lowList[i - 1] : 0;
            var prevHigh2 = i >= 2 ? highList[i - 2] : 0;
            var prevLow2 = i >= 2 ? lowList[i - 2] : 0;
            var prevHigh3 = i >= 3 ? highList[i - 3] : 0;
            var prevLow3 = i >= 3 ? lowList[i - 3] : 0;
            var prevHigh4 = i >= 4 ? highList[i - 4] : 0;
            var prevLow4 = i >= 4 ? lowList[i - 4] : 0;
            var prevB1 = i >= 1 ? bList[i - 1] : 0;
            var prevB2 = i >= 2 ? bList[i - 2] : 0;
            var middle = (((currentHigh + currentLow) / 2) + ((prevHigh1 + prevLow1) / 2) + ((prevHigh2 + prevLow2) / 2) +
                          ((prevHigh3 + prevLow3) / 2) + ((prevHigh4 + prevLow4) / 2)) / 5;
            var scale = ((currentHigh - currentLow + (prevHigh1 - prevLow1) + (prevHigh2 - prevLow2) + (prevHigh3 - prevLow3) +
                          (prevHigh4 - prevLow4)) / 5) * 0.2;

            var b = scale != 0 ? (currentValue - middle) / scale : 0;
            bList.Add(b);

            var signal = GetRsiSignal(b - prevB1, prevB1 - prevB2, b, prevB1, 4, -4);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Belkhayate", bList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(bList);
        stockData.IndicatorName = IndicatorName.BelkhayateTiming;

        return stockData;
    }


    /// <summary>
    /// Calculates the Detrended Price Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateDetrendedPriceOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 20)
    {
        List<double> dpoList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var prevPeriods = MinOrMax((int)Math.Ceiling(((double)length / 2) + 1));

        List<double> smaList;
        if (maType == MovingAvgType.SimpleMovingAverage)
        {
            using var mean = new Streaming.RoundedSimpleMovingAverageSmoother(length);
            smaList = inputList.Select(value => mean.Next(value, true)).ToList();
        }
        else smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentSma = smaList[i];
            var prevValue = i >= prevPeriods ? inputList[i - prevPeriods] : 0;

            var prevDpo = GetLastOrDefault(dpoList);
            var dpo = prevValue - currentSma;
            dpoList.Add(dpo);

            var signal = GetCompareSignal(dpo, prevDpo);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Dpo", dpoList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(dpoList);
        stockData.IndicatorName = IndicatorName.DetrendedPriceOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Chartmill Value Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateChartmillValueIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 5)
    {
        length = Math.Max(1, length); var (input, _, _, _, _, _) = GetInputValuesList(InputName.MedianPrice, stockData);
        var close = stockData.ChainedValues.Count > 0 ? stockData.ChainedValues : stockData.ClosePrices;
        List<double>? averages = null;
        if (Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType))
            averages = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(input), length)?.ToList() ?? GetMovingAverageList(stockData, maType, length, input);
        using var window = new ChartmillWindow(maType, length, input.Count);
        var c = new List<double>(input.Count); var o = new List<double>(input.Count); var h = new List<double>(input.Count); var l = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            var value = window.Next(stockData.HighPrices[i], stockData.LowPrices[i], stockData.OpenPrices[i], close[i], input[i], true, averages?[i]);
            var previous = i > 0 ? c[i - 1] : 0; var prior = i > 1 ? c[i - 2] : 0;
            signals?.Add(GetRsiSignal(value.Close - previous, previous - prior, value.Close, previous, .5, -.5));
            c.Add(value.Close); o.Add(value.Open); h.Add(value.High); l.Add(value.Low);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Cmvc", c }, { "Cmvo", o }, { "Cmvh", h }, { "Cmvl", l } });
        stockData.SetSignals(signals); stockData.SetCustomValues(new List<double>()); stockData.IndicatorName = IndicatorName.ChartmillValueIndicator; return stockData;
    }


    /// <summary>
    /// Calculates the Conditional Accumulator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="increment"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateConditionalAccumulator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 14, double increment = 1)
    {
        List<double> valueList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (_, highList, lowList, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var prevHigh = i >= 1 ? highList[i - 1] : 0;
            var prevLow = i >= 1 ? lowList[i - 1] : 0;

            var prevValue = GetLastOrDefault(valueList);
            valueList.Add(prevValue + GapStep(i >= 1, currentHigh, currentLow, prevHigh, prevLow, increment));
        }

        // Strict comparisons: a gap means this bar's low is above the previous high, not level with
        // it. With >= and <=, a market that never moved counted a gap up on every bar - the low
        // equals the previous high - and the accumulator simply returned the bar number.
        // The first bar has no predecessor and therefore cannot gap. prevHigh and prevLow are the 0
        // sentinel there, so currentLow > prevHigh is true for any positively priced instrument: bar
        // 0 was counted as a gap up, and every later value carried that extra increment. The strict
        // comparisons here were already corrected once for a market that never moves; the first-bar
        // case is the same class of defect at the other end of the series.
        static double GapStep(bool hasPrevious, double currentHigh, double currentLow, double prevHigh,
            double prevLow, double increment)
        {
            if (!hasPrevious)
            {
                return 0;
            }

            if (currentLow > prevHigh)
            {
                return increment;
            }

            if (currentHigh < prevLow)
            {
                return -increment;
            }

            return 0;
        }

        var valueEmaList = GetMovingAverageList(stockData, maType, length, valueList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var value = valueList[i];
            var valueEma = valueEmaList[i];
            var prevValue = i >= 1 ? valueList[i - 1] : 0;
            var prevValueEma = i >= 1 ? valueEmaList[i - 1] : 0;

            var signal = GetCompareSignal(value - valueEma, prevValue - prevValueEma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ca", valueList },
            { "Signal", valueEmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(valueList);
        stockData.IndicatorName = IndicatorName.ConditionalAccumulator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Contract High Low Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateContractHighLow(this StockData stockData)
    {
        List<double> conHiList = new(stockData.Count);
        List<double> conLowList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (_, highList, lowList, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];

            var prevConHi = GetLastOrDefault(conHiList);
            var conHi = i >= 1 ? Math.Max(prevConHi, currentHigh) : currentHigh;
            conHiList.Add(conHi);

            var prevConLow = GetLastOrDefault(conLowList);
            var conLow = i >= 1 ? Math.Min(prevConLow, currentLow) : currentLow;
            conLowList.Add(conLow);

            var signal = GetConditionSignal(conHi > prevConHi, conLow < prevConLow);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ch", conHiList },
            { "Cl", conLowList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.ContractHighLow;

        return stockData;
    }


    /// <summary>
    /// Calculates the Chop Zone Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateChopZone(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 30, int length2 = 34)
    {
        List<double> emaAngleList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, closeList, _) = GetInputValuesList(InputName.TypicalPrice, stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length1);

        var emaList = GetMovingAverageList(stockData, maType, length2, closeList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var highest = highestList[i];
            var lowest = lowestList[i];
            var ema = emaList[i];
            var prevEma = i >= 1 ? emaList[i - 1] : 0;
            var range = highest - lowest != 0 ? 25 / (highest - lowest) * lowest : 0;
            var avg = inputList[i];
            var y = avg != 0 && range != 0 ? (prevEma - ema) / avg * range : 0;
            var c = Sqrt(1 + (y * y));
            var emaAngle1 = c != 0 ? Math.Round(Math.Acos(1 / c).ToDegrees()) : 0;

            var prevEmaAngle = GetLastOrDefault(emaAngleList);
            var emaAngle = y > 0 ? -emaAngle1 : emaAngle1;
            emaAngleList.Add(emaAngle);

            var signal = GetCompareSignal(emaAngle, prevEmaAngle);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Cz", emaAngleList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(emaAngleList);
        stockData.IndicatorName = IndicatorName.ChopZone;

        return stockData;
    }


    /// <summary>
    /// Calculates the Center of Linearity
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateCenterOfLinearity(this StockData stockData, int length = 14)
    {
        var (input,_,_,_,_)=GetInputValuesList(stockData);using var window=new CenterLinearityWindow(length);List<double> values=new(input.Count);var signals=CreateSignalsList(stockData);
        foreach(var price in input){var value=window.Next(price,true);signals?.Add(GetCompareSignal(value,values.Count>0?values[values.Count-1]:0));values.Add(value);}
        stockData.SetOutputValues(()=>new Dictionary<string,List<double>>{{"Col",values}});stockData.SetSignals(signals);stockData.SetCustomValues(values);stockData.IndicatorName=IndicatorName.CenterOfLinearity;return stockData;
    }


    /// <summary>
    /// Calculates the Chaikin Volatility
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateChaikinVolatility(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length1 = 10, int length2 = 12)
    {
        length1=Math.Max(1,length1);var high=stockData.HighPrices;var low=stockData.LowPrices;using var window=new ChaikinVolatilityWindow(maType,length1,length2,high.Count);
        var external=Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType);
        List<double>? averaged=null;
        if(external)
        {
            var range=high.Select((v,i)=>ChaikinVolatilityWindow.Range(v,low[i]).Publish()).ToList();
            averaged=Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(range),length1)?.ToList() ?? GetMovingAverageList(stockData,maType,length1,range);
        }
        List<double> values=new(high.Count);var signals=CreateSignalsList(stockData);
        for(var i=0;i<high.Count;i++){var value=external?window.Finish(new RocBankValue(averaged![i]),true):window.Next(high[i],low[i],true);signals?.Add(GetCompareSignal(value,i>0?values[i-1]:0,true));values.Add(value);}
        stockData.SetOutputValues(()=>new Dictionary<string,List<double>>{{"Cv",values}});stockData.SetSignals(signals);stockData.SetCustomValues(values);stockData.IndicatorName=IndicatorName.ChaikinVolatility;return stockData;
    }


    /// <summary>
    /// Calculates the Confluence Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateConfluenceIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 10)
    {
        List<double> value5List = new(stockData.Count);
        List<double> value6List = new(stockData.Count);
        List<double> value7List = new(stockData.Count);
        List<double> momList = new(stockData.Count);
        List<double> sumList = new(stockData.Count);
        List<double> errSumList = new(stockData.Count);
        List<double> value70List = new(stockData.Count);
        List<double> confluenceList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, closeList, _) = GetInputValuesList(InputName.FullTypicalPrice, stockData);
        var errSumWindow = new RollingSum();
        var value70SumWindow = new RollingSum();

        var stl = (int)Math.Ceiling((length * 2) - 1 - 0.5m);
        var itl = (int)Math.Ceiling((stl * 2) - 1 - 0.5m);
        var ltl = (int)Math.Ceiling((itl * 2) - 1 - 0.5m);
        var hoff = (int)Math.Ceiling(((double)length / 2) - 0.5);
        var soff = (int)Math.Ceiling(((double)stl / 2) - 0.5);
        var ioff = (int)Math.Ceiling(((double)itl / 2) - 0.5);
        var hLength = MinOrMax(length - 1);
        var sLength = stl - 1;
        var iLength = itl - 1;
        var lLength = ltl - 1;

        var hAvgList = maType == MovingAvgType.WeightedMovingAverage
            ? Streaming.SpreadAverage.Calculate(closeList, maType, Math.Max(1, length))
            : GetMovingAverageList(stockData, maType, length, closeList);
        var sAvgList = maType == MovingAvgType.WeightedMovingAverage
            ? Streaming.SpreadAverage.Calculate(closeList, maType, Math.Max(1, stl))
            : GetMovingAverageList(stockData, maType, stl, closeList);
        var iAvgList = maType == MovingAvgType.WeightedMovingAverage
            ? Streaming.SpreadAverage.Calculate(closeList, maType, Math.Max(1, itl))
            : GetMovingAverageList(stockData, maType, itl, closeList);
        var lAvgList = maType == MovingAvgType.WeightedMovingAverage
            ? Streaming.SpreadAverage.Calculate(closeList, maType, Math.Max(1, ltl))
            : GetMovingAverageList(stockData, maType, ltl, closeList);
        var h2AvgList = maType == MovingAvgType.WeightedMovingAverage
            ? Streaming.SpreadAverage.Calculate(closeList, maType, Math.Max(1, hLength))
            : GetMovingAverageList(stockData, maType, hLength, closeList);
        var s2AvgList = maType == MovingAvgType.WeightedMovingAverage
            ? Streaming.SpreadAverage.Calculate(closeList, maType, Math.Max(1, sLength))
            : GetMovingAverageList(stockData, maType, Math.Max(1, sLength), closeList);
        var i2AvgList = maType == MovingAvgType.WeightedMovingAverage
            ? Streaming.SpreadAverage.Calculate(closeList, maType, Math.Max(1, iLength))
            : GetMovingAverageList(stockData, maType, Math.Max(1, iLength), closeList);
        var l2AvgList = maType == MovingAvgType.WeightedMovingAverage
            ? Streaming.SpreadAverage.Calculate(closeList, maType, Math.Max(1, lLength))
            : GetMovingAverageList(stockData, maType, Math.Max(1, lLength), closeList);
        var ftpAvgList = maType == MovingAvgType.WeightedMovingAverage
            ? Streaming.SpreadAverage.Calculate(inputList, maType, Math.Max(1, lLength))
            : GetMovingAverageList(stockData, maType, Math.Max(1, lLength), inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var sAvg = sAvgList[i];
            var priorSAvg = i >= soff ? sAvgList[i - soff] : 0;
            var priorHAvg = i >= hoff ? hAvgList[i - hoff] : 0;
            var iAvg = iAvgList[i];
            var priorIAvg = i >= ioff ? iAvgList[i - ioff] : 0;
            var lAvg = lAvgList[i];
            var hAvg = hAvgList[i];
            var prevSAvg = i >= 1 ? sAvgList[i - 1] : 0;
            var prevHAvg = i >= 1 ? hAvgList[i - 1] : 0;
            var prevIAvg = i >= 1 ? iAvgList[i - 1] : 0;
            var prevLAvg = i >= 1 ? lAvgList[i - 1] : 0;
            var h2 = h2AvgList[i];
            var s2 = s2AvgList[i];
            var i2 = i2AvgList[i];
            var l2 = l2AvgList[i];
            var ftpAvg = ftpAvgList[i];
            var priorHAvg2 = i >= soff ? hAvgList[i - soff] : 0;
            var prevErrSum = i >= 1 ? errSumList[i - 1] : 0;
            var prevMom = i >= 1 ? momList[i - 1] : 0;
            var prevValue70 = i >= 1 ? value70List[i - 1] : 0;
            var prevConfluence1 = i >= 1 ? confluenceList[i - 1] : 0;
            var prevConfluence2 = i >= 2 ? confluenceList[i - 2] : 0;
            var value2 = sAvg - priorHAvg;
            var value3 = iAvg - priorSAvg;
            var value12 = lAvg - priorIAvg;
            var momSig = value2 + value3 + value12;
            var derivH = (hAvg * 2) - prevHAvg;
            var derivS = (sAvg * 2) - prevSAvg;
            var derivI = (iAvg * 2) - prevIAvg;
            var derivL = (lAvg * 2) - prevLAvg;
            var value5 = derivH + (length - 1d - hLength) / length * h2;
            value5List.Add(value5);

            var value6 = derivS;
            value6List.Add(value6);

            var value7 = derivI;
            value7List.Add(value7);

            var value13 = derivL + (ltl - 1d) / ltl * (ftpAvg - l2);
            var priorValue5 = i >= hoff ? value5List[i - hoff] : 0;
            var priorValue6 = i >= soff ? value6List[i - soff] : 0;
            var priorValue7 = i >= ioff ? value7List[i - ioff] : 0;
            var value9 = value6 - priorValue5;
            var value10 = value7 - priorValue6;
            var value14 = value13 - priorValue7;

            var mom = value9 + value10 + value14;
            momList.Add(mom);

            var ht = Math.Sin(value5 * 2 * Math.PI / 360) + Math.Cos(value5 * 2 * Math.PI / 360);
            var hta = Math.Sin(hAvg * 2 * Math.PI / 360) + Math.Cos(hAvg * 2 * Math.PI / 360);
            var st = Math.Sin(value6 * 2 * Math.PI / 360) + Math.Cos(value6 * 2 * Math.PI / 360);
            var sta = Math.Sin(sAvg * 2 * Math.PI / 360) + Math.Cos(sAvg * 2 * Math.PI / 360);
            var it = Math.Sin(value7 * 2 * Math.PI / 360) + Math.Cos(value7 * 2 * Math.PI / 360);
            var ita = Math.Sin(iAvg * 2 * Math.PI / 360) + Math.Cos(iAvg * 2 * Math.PI / 360);

            var sum = ht + st + it;
            sumList.Add(sum);
            var priorSum = i >= soff ? sumList[i - soff] : 0;

            var err = hta + sta + ita;
            double cond2 = ConfluenceVotes.Compare(sum, priorSum) * ConfluenceVotes.Compare(hAvg, priorHAvg2) < 0 ? 1 : 0;
            double phase = cond2 == 1 ? -1 : 1;

            var errSum = (sum - err) * phase;
            errSumList.Add(errSum);
            errSumWindow.Add(errSum);

            var value70 = value5 - value13;
            value70List.Add(value70);
            value70SumWindow.Add(value70);

            var errSig = errSumWindow.Average(soff);
            var value71 = value70SumWindow.Average(length);
            double errNum = ConfluenceVotes.Score(errSum, prevErrSum, errSig);
            double momNum = ConfluenceVotes.Score(mom, prevMom, momSig);
            double tcNum = ConfluenceVotes.Score(value70, prevValue70, value71);
            var value42 = errNum + momNum + tcNum;

            var confluence = ConfluenceVotes.Publish(value42, value70);
            confluenceList.Add(confluence);

            var res1 = confluence >= 1 ? confluence : 0;
            var res2 = confluence <= -1 ? confluence : 0;
            var res3 = confluence == 0 ? 0 : confluence > -1 && confluence < 1 ? 10 * confluence : 0;

            var signal = GetCompareSignal(confluence - prevConfluence1, prevConfluence1 - prevConfluence2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ci", confluenceList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(confluenceList);
        stockData.IndicatorName = IndicatorName.ConfluenceIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Coppock Curve
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateCoppockCurve(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 10,
        int fastLength = 11, int slowLength = 14)
    {
        if (StrengthWindow.Supports(maType) && !Builder.Compute.ComponentAverage.HasOverrides)
        {
            var (stableInput, _, _, _, _) = GetInputValuesList(stockData);
            var stableLine = new List<double>(stockData.Count);
            var stableSignals = CreateSignalsList(stockData);
            using var stableWindow = new RocBankWindow(maType, new[] { fastLength, slowLength }, new[] { 1, 1 }, new[] { 1, 1 }, length, stockData.Count);
            double previous = 0;
            foreach (var price in stableInput)
            {
                var next = stableWindow.Next(price, true).Signal;
                stableLine.Add(next); stableSignals?.Add(GetCompareSignal(next, previous)); previous = next;
            }
            stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Cc", stableLine } });
            stockData.SetSignals(stableSignals); stockData.SetCustomValues(stableLine);
            stockData.IndicatorName = IndicatorName.CoppockCurve;
            return stockData;
        }

        List<double> rocTotalList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        // Every Calculate method leaves its result on the chained series, so the second rate of change read
        // the first one's output instead of the price it was meant to measure.
        var callerSeries = stockData.CaptureInputSeries();
        var roc11List = CalculateRateOfChange(stockData, fastLength).ChainedValues;
        stockData.RestoreInputSeries(callerSeries);
        var roc14List = CalculateRateOfChange(stockData, slowLength).ChainedValues;
        stockData.RestoreInputSeries(callerSeries);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentRoc11 = roc11List[i];
            var currentRoc14 = roc14List[i];

            var rocTotal = currentRoc11 + currentRoc14;
            rocTotalList.Add(rocTotal);
        }

        var coppockCurveList = GetMovingAverageList(stockData, maType, length, rocTotalList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var coppockCurve = coppockCurveList[i];
            var prevCoppockCurve = i >= 1 ? coppockCurveList[i - 1] : 0;

            var signal = GetCompareSignal(coppockCurve, prevCoppockCurve);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Cc", coppockCurveList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(coppockCurveList);
        stockData.IndicatorName = IndicatorName.CoppockCurve;

        return stockData;
    }


    /// <summary>
    /// Calculates the Constance Brown Composite Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateConstanceBrownCompositeIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int fastLength = 13, int slowLength = 33, int length1 = 14, int length2 = 9, int smoothLength = 3)
    {
        List<double> sList = new(stockData.Count);
        List<double> bullSlopeList = new(stockData.Count);
        List<double> bearSlopeList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        // Every Calculate method leaves its result on the chained series, so the second component read
        // the first one's output instead of the input both of them measure.
        var callerSeries = stockData.CaptureInputSeries();
        var rsi1List = CalculateRelativeStrengthIndex(stockData, length: length1).ChainedValues;
        stockData.RestoreInputSeries(callerSeries);
        var rsi2List = CalculateRelativeStrengthIndex(stockData, length: smoothLength).ChainedValues;
        stockData.RestoreInputSeries(callerSeries);
        var rsiSmaList = GetMovingAverageList(stockData, maType, smoothLength, rsi2List);

        for (var i = 0; i < stockData.Count; i++)
        {
            var rsiSma = rsiSmaList[i];
            var rsiDelta = i >= length2 ? rsi1List[i] - rsi1List[i - length2] : 0;

            var s = rsiDelta + rsiSma;
            sList.Add(s);
        }

        var sFastSmaList = GetMovingAverageList(stockData, maType, fastLength, sList);
        var sSlowSmaList = GetMovingAverageList(stockData, maType, slowLength, sList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var s = sList[i];
            var sFastSma = sFastSmaList[i];
            var sSlowSma = sSlowSmaList[i];

            var prevBullSlope = GetLastOrDefault(bullSlopeList);
            var bullSlope = s - Math.Max(sFastSma, sSlowSma);
            bullSlopeList.Add(bullSlope);

            var prevBearSlope = GetLastOrDefault(bearSlopeList);
            var bearSlope = s - Math.Min(sFastSma, sSlowSma);
            bearSlopeList.Add(bearSlope);

            var signal = GetBullishBearishSignal(bullSlope, prevBullSlope, bearSlope, prevBearSlope);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Cbci", sList },
            { "FastSignal", sFastSmaList },
            { "SlowSignal", sSlowSmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(sList);
        stockData.IndicatorName = IndicatorName.ConstanceBrownCompositeIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Commodity Selection Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="pointValue"></param>
    /// <param name="margin"></param>
    /// <param name="commission"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateCommoditySelectionIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, 
        int length = 14, double pointValue = 50, double margin = 3000, double commission = 10)
    {
        var callerSeries = stockData.CaptureInputSeries();
        List<double> csiList = new(stockData.Count);
        List<double> csiSmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var csiSumWindow = new RollingSum();

        var k = 100 * (pointValue / Sqrt(margin) / (150 + commission));

        var atrList = CalculateAverageTrueRange(stockData, maType, length).ChainedValues;
        // Clear state to prevent pollution of CalculateAverageDirectionalIndex inputs
        stockData.RestoreInputSeries(callerSeries);
        stockData.SetSignals(null);
        var adxList = CalculateAverageDirectionalIndex(stockData, maType, length).ChainedValues;

        for (var i = 0; i < stockData.Count; i++)
        {
            var atr = atrList[i];
            var adxRating = adxList[i];

            var prevCsi = GetLastOrDefault(csiList);
            var csi = k * atr * adxRating;
            csiList.Add(csi);

            var prevCsiSma = GetLastOrDefault(csiSmaList);
            csiSumWindow.Add(csi);
            var csiSma = csiSumWindow.Average(length);
            csiSmaList.Add(csiSma);

            var signal = GetCompareSignal(csi - csiSma, prevCsi - prevCsiSma, true);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Csi", csiList },
            { "Signal", csiSmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(csiList);
        stockData.IndicatorName = IndicatorName.CommoditySelectionIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Decision Point Breadth Swenlin Trading Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateDecisionPointBreadthSwenlinTradingOscillator(this StockData stockData, 
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 5)
    {
        List<double> iList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            double advance = currentValue > prevValue ? 1 : 0;
            double decline = currentValue < prevValue ? 1 : 0;

            var iVal = advance + decline != 0 ? 1000 * (advance - decline) / (advance + decline) : 0;
            iList.Add(iVal);
        }

        var ivalEmaList = GetMovingAverageList(stockData, maType, length, iList);
        var stoList = GetMovingAverageList(stockData, maType, length, ivalEmaList);
        var stoEmaList = GetMovingAverageList(stockData, maType, length, stoList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var sto = stoList[i];
            var stoEma = stoEmaList[i];
            var prevSto = i >= 1 ? stoList[i - 1] : 0;
            var prevStoEma = i >= 1 ? stoEmaList[i - 1] : 0;

            var signal = GetCompareSignal(sto - stoEma, prevSto - prevStoEma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Dpbsto", stoList },
            { "Signal", stoEmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(stoList);
        stockData.IndicatorName = IndicatorName.DecisionPointBreadthSwenlinTradingOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Delta Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateDeltaMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length1 = 10, int length2 = 5)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); var opens = stockData.OpenPrices;
        var lag = Math.Max(1, length2); var custom = Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType);
        List<double>? customer = null;
        if (custom)
        {
            var changes = input.Select((price, i) => OpenCloseAverageWindow.Difference(i >= lag ? opens[i - lag] : 0, price).Publish()).ToList();
            customer = GetMovingAverageList(stockData, maType, length1, changes);
        }
        List<double> line = new(stockData.Count), signal = new(stockData.Count), histogram = new(stockData.Count);
        List<Signal>? signals = CreateSignalsList(stockData);
        using var window = new OpenCloseAverageWindow(maType, length1, lag);
        for (var i = 0; i < input.Count; i++)
        {
            var value = window.Next(opens[i], input[i], true, customer?[i]);
            line.Add(value.Line); signal.Add(value.Signal); histogram.Add(value.Histogram);
            signals?.Add(GetCompareSignal(value.Histogram, i == 0 ? 0 : histogram[i - 1]));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Delta", line }, { "Signal", signal }, { "Histogram", histogram } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.DeltaMovingAverage;
        return stockData;
    }


    /// <summary>
    /// Calculates the Detrended Synthetic Price
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateDetrendedSyntheticPrice(this StockData stockData, int length = 14)
    {
        List<double> dspList = new(stockData.Count);
        List<double> ema1List = new(stockData.Count);
        List<double> ema2List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (_, highList, lowList, _, _) = GetInputValuesList(stockData);

        var alpha = length > 2 ? (double)2 / (length + 1d) : 0.67;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var prevHigh = i >= 1 ? highList[i - 1] : 0;
            var prevLow = i >= 1 ? lowList[i - 1] : 0;
            var high = Math.Max(currentHigh, prevHigh);
            var low = Math.Min(currentLow, prevLow);
            var price = PriceMean.Of(high, low);
            var prevEma1 = i >= 1 ? ema1List[i - 1] : price;
            var prevEma2 = i >= 1 ? ema2List[i - 1] : price;

            var ema1 = VidyaBlend.Compute(prevEma1, price, alpha);
            ema1List.Add(ema1);

            var ema2 = VidyaBlend.Compute(prevEma2, price, alpha / 2);
            ema2List.Add(ema2);

            var prevDsp = GetLastOrDefault(dspList);
            var dsp = ema1 - ema2;
            dspList.Add(dsp);

            var signal = GetCompareSignal(dsp, prevDsp);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Dsp", dspList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(dspList);
        stockData.IndicatorName = IndicatorName.DetrendedSyntheticPrice;

        return stockData;
    }


    /// <summary>
    /// Calculates the Derivative Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="length4"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateDerivativeOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 14,
        int length2 = 9, int length3 = 5, int length4 = 3)
    {
        if (StrengthWindow.Supports(maType) && !Builder.Compute.ComponentAverage.HasOverrides)
        {
            var (input, _, _, _, _) = GetInputValuesList(stockData); using var window = new DerivativeWindow(maType, length1, length2, length3, length4);
            var line = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
            for (var i = 0; i < input.Count; i++) { var value = window.Next(input[i], true); signals?.Add(GetCompareSignal(value, i > 0 ? line[i - 1] : 0)); line.Add(value); }
            stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Do", line } }); stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.DerivativeOscillator; return stockData;
        }

        List<double> s1List = new(stockData.Count);
        List<double> s2List = new(stockData.Count);
        List<double> s1SmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var s1SumWindow = new RollingSum();

        List<double> rsiList;
        if (StrengthWindow.Supports(maType))
        {
            var (prices, _, _, _, _) = GetInputValuesList(stockData); using var rsiWindow = new PriceRsiWindow(maType, length1, prices.Count);
            rsiList = prices.Select(v => rsiWindow.Next(v, true)).ToList();
        }
        else rsiList = CalculateRelativeStrengthIndex(stockData, maType, length: length1).ChainedValues;
        var rsiEma1List = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(rsiList), length3)?.ToList() ?? GetMovingAverageList(stockData, maType, length3, rsiList);
        var rsiEma2List = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(rsiEma1List), length4)?.ToList() ?? GetMovingAverageList(stockData, maType, length4, rsiEma1List);

        for (var i = 0; i < rsiList.Count; i++)
        {
            var prevS1 = GetLastOrDefault(s1List);
            var s1 = rsiEma2List[i];
            s1List.Add(s1);
            s1SumWindow.Add(s1);

            var prevS1Sma = GetLastOrDefault(s1SmaList);
            var s1Sma = s1SumWindow.Average(length2);
            s1SmaList.Add(s1Sma);

            var s2 = s1 - s1Sma;
            s2List.Add(s2);

            var signal = GetCompareSignal(s1 - s1Sma, prevS1 - prevS1Sma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Do", s2List }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(s2List);
        stockData.IndicatorName = IndicatorName.DerivativeOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Demand Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateDemandOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length1 = 10, int length2 = 2, int length3 = 20)
    {
        List<double> rangeList = new(stockData.Count);
        List<double> doList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length2);

        for (var i = 0; i < stockData.Count; i++)
        {
            var highest = highestList[i];
            var lowest = lowestList[i];

            var range = highest - lowest;
            rangeList.Add(range);
        }

        var vaList = GetMovingAverageList(stockData, maType, length1, rangeList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var va = vaList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var pctChg = prevValue != 0 ? MinPastValues(i, 1, currentValue - prevValue) / Math.Abs(prevValue) * 100 : 0;
            var currentVolume = stockData.Volumes[i];
            var k = va != 0 ? (3 * currentValue) / va : 0;
            var pctK = pctChg * k;
            var volPctK = pctK != 0 ? currentVolume / pctK : 0;
            var bp = currentValue > prevValue ? currentVolume : volPctK;
            var sp = currentValue > prevValue ? volPctK : currentVolume;

            var dosc = bp - sp;
            doList.Add(dosc);
        }

        var doEmaList = GetMovingAverageList(stockData, maType, length3, doList);
        var doSigList = GetMovingAverageList(stockData, maType, length1, doEmaList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var doSig = doSigList[i];
            var prevSig1 = i >= 1 ? doSigList[i - 1] : 0;
            var prevSig2 = i >= 2 ? doSigList[i - 1] : 0;

            var signal = GetCompareSignal(doSig - prevSig1, prevSig1 - prevSig2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Do", doEmaList },
            { "Signal", doSigList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(doEmaList);
        stockData.IndicatorName = IndicatorName.DemandOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Double Smoothed Momenta
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateDoubleSmoothedMomenta(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 2,
        int length2 = 5, int length3 = 25)
    {
        List<double> momList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        List<double> momEmaList;
        if (StrengthWindow.Supports(maType) && !Builder.Compute.ComponentAverage.HasOverrides)
        {
            momEmaList = new(stockData.Count);
            using var window = new MomentaRangeWindow(maType, length1, length2, length3, stockData.Count);
            foreach (var price in inputList)
            {
                var next = window.Next(price, true);
                momList.Add(next.Value); momEmaList.Add(next.Signal);
            }
        }
        else
        {
            List<double> srcLcList = new(stockData.Count);
            List<double> hcLcList = new(stockData.Count);
            var (highestList, lowestList) = GetMaxAndMinValuesList(inputList, Math.Max(2, length1));
            for (var i = 0; i < stockData.Count; i++)
            {
                var currentValue = inputList[i];
                var hc = highestList[i];
                var lc = lowestList[i];

                var srcLc = currentValue - lc;
                srcLcList.Add(srcLc);

                var hcLc = hc - lc;
                hcLcList.Add(hcLc);
            }

            var topEma1List = GetMovingAverageList(stockData, maType, length2, srcLcList);
            var topEma2List = GetMovingAverageList(stockData, maType, length3, topEma1List);
            var botEma1List = GetMovingAverageList(stockData, maType, length2, hcLcList);
            var botEma2List = GetMovingAverageList(stockData, maType, length3, botEma1List);
            for (var i = 0; i < stockData.Count; i++)
            {
                var top = topEma2List[i];
                var bot = botEma2List[i];

                var mom = bot != 0 ? MinOrMax(100 * top / bot, 100, 0) : 0;
                momList.Add(mom);
            }

            momEmaList = GetMovingAverageList(stockData, maType, length3, momList);
        }

        for (var i = 0; i < stockData.Count; i++)
        {
            var mom = momList[i];
            var momEma = momEmaList[i];
            var prevMom = i >= 1 ? momList[i - 1] : 0;
            var prevMomEma = i >= 1 ? momEmaList[i - 1] : 0;

            var signal = GetCompareSignal(mom - momEma, prevMom - prevMomEma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Dsm", momList },
            { "Signal", momEmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(momList);
        stockData.IndicatorName = IndicatorName.DoubleSmoothedMomenta;

        return stockData;
    }


    /// <summary>
    /// Calculates the Didi Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateDidiIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 3, int length2 = 8,
        int length3 = 20)
    {
        List<double> curtaList = new(stockData.Count);
        List<double> mediaList = new(stockData.Count);
        List<double> longaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var mediumSmaList = maType == MovingAvgType.SimpleMovingAverage ? BollingerArithmetic.Mean(inputList, length2) : GetMovingAverageList(stockData, maType, length2, inputList);
        var shortSmaList = maType == MovingAvgType.SimpleMovingAverage ? BollingerArithmetic.Mean(inputList, length1) : GetMovingAverageList(stockData, maType, length1, inputList);
        var longSmaList = maType == MovingAvgType.SimpleMovingAverage ? BollingerArithmetic.Mean(inputList, length3) : GetMovingAverageList(stockData, maType, length3, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var mediumSma = mediumSmaList[i];
            var shortSma = shortSmaList[i];
            var longSma = longSmaList[i];

            var prevCurta = GetLastOrDefault(curtaList);
            var curta = mediumSma != 0 ? shortSma / mediumSma : 0;
            curtaList.Add(curta);
            mediaList.Add(mediumSma == 0 ? 0 : 1);

            var prevLonga = GetLastOrDefault(longaList);
            var longa = mediumSma != 0 ? longSma / mediumSma : 0;
            longaList.Add(longa);

            var signal = GetCompareSignal(curta - longa, prevCurta - prevLonga);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Curta", curtaList },
            { "Media", mediaList },
            { "Longa", longaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.DidiIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Disparity Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateDisparityIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14)
    {
        List<double> disparityIndexList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = maType == MovingAvgType.SimpleMovingAverage ? BollingerArithmetic.Mean(inputList, length) : GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentSma = smaList[i];

            var prevDisparityIndex = GetLastOrDefault(disparityIndexList);
            var disparityIndex = RoundedPercentageChange.Of(currentValue, currentSma);
            disparityIndexList.Add(disparityIndex);

            var signal = GetCompareSignal(disparityIndex, prevDisparityIndex);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Di", disparityIndexList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(disparityIndexList);
        stockData.IndicatorName = IndicatorName.DisparityIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Damping Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="threshold"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateDampingIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 5, 
        double threshold = 1.5)
    {
        List<double> rangeList = new(stockData.Count);
        List<double> diList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            
            var range = currentHigh - currentLow;
            rangeList.Add(range);
        }

        var rangeSmaList = GetMovingAverageList(stockData, maType, length, rangeList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevSma1 = i >= 1 ? rangeSmaList[i - 1] : 0;
            var prevSma6 = i >= 6 ? rangeSmaList[i - 6] : 0;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevSma = i >= 1 ? smaList[i - 1] : 0;
            var currentSma = smaList[i];

            var di = prevSma6 != 0 ? prevSma1 / prevSma6 : 0;
            diList.Add(di);

            var signal = GetVolatilitySignal(currentValue - currentSma, prevValue - prevSma, di, threshold);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Di", diList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(diList);
        stockData.IndicatorName = IndicatorName.DampingIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Directional Trend Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateDirectionalTrendIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 14,
        int length2 = 10, int length3 = 5)
    {
        List<double> dtiList = new(stockData.Count);
        List<double> diffList = new(stockData.Count);
        List<double> absDiffList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (_, highList, lowList, _, _) = GetInputValuesList(stockData);

        if (StrengthWindow.Supports(maType))
        {
            using var window = new DirectionalStrengthWindow(maType, length1, length2, length3, stockData.Count);
            for (var i = 0; i < stockData.Count; i++) dtiList.Add(window.Next(highList[i], lowList[i], true));
        }
        else
        {
            for (var i = 0; i < stockData.Count; i++)
            {
                var currentHigh = highList[i];
                var currentLow = lowList[i];
                var prevHigh = i >= 1 ? highList[i - 1] : 0;
                var prevLow = i >= 1 ? lowList[i - 1] : 0;
                var hmu = currentHigh - prevHigh > 0 ? currentHigh - prevHigh : 0;
                var lmd = currentLow - prevLow < 0 ? (currentLow - prevLow) * -1 : 0;

                var diff = hmu - lmd;
                diffList.Add(diff);

                var absDiff = Math.Abs(diff);
                absDiffList.Add(absDiff);
            }

            var diffEma1List = GetMovingAverageList(stockData, maType, length1, diffList);
            var absDiffEma1List = GetMovingAverageList(stockData, maType, length1, absDiffList);
            var diffEma2List = GetMovingAverageList(stockData, maType, length2, diffEma1List);
            var absDiffEma2List = GetMovingAverageList(stockData, maType, length2, absDiffEma1List);
            var diffEma3List = GetMovingAverageList(stockData, maType, length3, diffEma2List);
            var absDiffEma3List = GetMovingAverageList(stockData, maType, length3, absDiffEma2List);
            for (var i = 0; i < stockData.Count; i++)
            {
                var diffEma3 = diffEma3List[i];
                var absDiffEma3 = absDiffEma3List[i];

                var dti = absDiffEma3 != 0 ? MinOrMax(100 * diffEma3 / absDiffEma3, 100, -100) : 0;
                dtiList.Add(dti);

            }

        }

        for (var i = 0; i < stockData.Count; i++)
        {
            var dti = dtiList[i];
            var prevDti1 = i >= 1 ? dtiList[i - 1] : 0;
            var prevDti2 = i >= 2 ? dtiList[i - 2] : 0;
            signalsList?.Add(GetRsiSignal(dti - prevDti1, prevDti1 - prevDti2, dti, prevDti1, 25, -25));
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Dti", dtiList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(dtiList);
        stockData.IndicatorName = IndicatorName.DirectionalTrendIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Drunkard Walk
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateDrunkardWalk(this StockData stockData, int length1 = 80, int length2 = 14)
    {
        List<double> tempHighList = new(stockData.Count);
        List<double> tempLowList = new(stockData.Count);
        List<double> upAtrList = new(stockData.Count);
        List<double> dnAtrList = new(stockData.Count);
        List<double> upwalkList = new(stockData.Count);
        List<double> dnwalkList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var highestHigh = highestList[i];
            var lowestLow = lowestList[i];
            // For TrueRange on first bar, use current close to avoid inflated TR
            var prevValue = i >= 1 ? inputList[i - 1] : inputList[i];

            var currentHigh = highList[i];
            tempHighList.Add(currentHigh);

            var currentLow = lowList[i];
            tempLowList.Add(currentLow);

            var tr = CalculationsHelper.CalculateTrueRange(currentHigh, currentLow, prevValue);
            var maxIndex = tempHighList.LastIndexOf(highestHigh);
            var minIndex = tempLowList.LastIndexOf(lowestLow);
            var dnRun = i - maxIndex;
            var upRun = i - minIndex;

            var prevAtrUp = GetLastOrDefault(upAtrList);
            var upK = upRun != 0 ? (double)1 / upRun : 0;
            var atrUp = (tr * upK) + (prevAtrUp * (1 - upK));
            upAtrList.Add(atrUp);

            var prevAtrDn = GetLastOrDefault(dnAtrList);
            var dnK = dnRun != 0 ? (double)1 / dnRun : 0;
            var atrDn = (tr * dnK) + (prevAtrDn * (1 - dnK));
            dnAtrList.Add(atrDn);

            var upDen = atrUp > 0 ? atrUp : 1;
            var prevUpWalk = GetLastOrDefault(upwalkList);
            var upWalk = upRun > 0 ? (currentHigh - lowestLow) / (Sqrt(upRun) * upDen) : 0;
            upwalkList.Add(upWalk);

            var dnDen = atrDn > 0 ? atrDn : 1;
            var prevDnWalk = GetLastOrDefault(dnwalkList);
            var dnWalk = dnRun > 0 ? (highestHigh - currentLow) / (Sqrt(dnRun) * dnDen) : 0;
            dnwalkList.Add(dnWalk);

            var signal = GetCompareSignal(upWalk - dnWalk, prevUpWalk - prevDnWalk);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpWalk", upwalkList },
            { "DnWalk", dnwalkList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.DrunkardWalk;

        return stockData;
    }


    /// <summary>
    /// Calculates the DT Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="length4"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateDTOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int length1 = 13, 
        int length2 = 8, int length3 = 5, int length4 = 3)
    {
        length1 = Math.Max(1, length1);
        length2 = Math.Max(1, length2);
        length3 = Math.Max(1, length3);
        length4 = Math.Max(1, length4);
        List<double> stoRsiList = new(stockData.Count);
        List<double> skList = new(stockData.Count);
        List<double> sdList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var stoRsiSumWindow = new RollingSum();
        var skSumWindow = new RollingSum();

        var wilderMovingAvgList = CalculateRelativeStrengthIndex(stockData, maType, length1).ChainedValues;
        var (highestList, lowestList) = GetMaxAndMinValuesList(wilderMovingAvgList, length2);

        for (var i = 0; i < stockData.Count; i++)
        {
            var wima = wilderMovingAvgList[i];
            var highest = highestList[i];
            var lowest = lowestList[i];
            var prevSd1 = i >= 1 ? sdList[i - 1] : 0;
            var prevSd2 = i >= 2 ? sdList[i - 2] : 0;

            var stoRsi = highest - lowest != 0 ? MinOrMax(100 * (wima - lowest) / (highest - lowest), 100, 0) : 0;
            stoRsiList.Add(stoRsi);

            stoRsiSumWindow.Add(stoRsi);
            var sk = stoRsiSumWindow.Average(length3);
            skList.Add(sk);

            skSumWindow.Add(sk);
            var sd = skSumWindow.Average(length4);
            sdList.Add(sd);

            var signal = GetRsiSignal(sd - prevSd1, prevSd1 - prevSd2, sd, prevSd1, 70, 30);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Dto", skList },
            { "Signal", sdList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(skList);
        stockData.IndicatorName = IndicatorName.DTOscillator;

        return stockData;
    }

}

