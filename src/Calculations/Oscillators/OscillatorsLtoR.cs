using OoplesFinance.StockIndicators.Compatibility;
using OoplesFinance.StockIndicators.Core;

namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Normalized Macd.
    /// </summary>
    /// <remarks>
    /// The gap between a fast and a slow exponential average as a percentage of the slow one, which is what
    /// makes it comparable between instruments where the raw convergence and divergence is not. Both averages
    /// start at the first bar's value rather than warming up, so the reading is meaningful at once, and the
    /// first bar, which has no change behind it, publishes zero.
    /// </remarks>
    /// <param name="stockData"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateNormalizedMacd(this StockData stockData, int fastLength = 12, int slowLength = 26)
    {
        fastLength = Math.Max(fastLength, 1);
        slowLength = Math.Max(slowLength, 1);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var count = inputList.Count;
        List<double> macdList = new(count);
        List<Signal>? signalsList = CreateSignalsList(stockData, count);

        var fastEma = count > 0 ? inputList[0] : 0;
        var slowEma = count > 0 ? inputList[0] : 0;

        for (var i = 0; i < count; i++)
        {
            double macd = 0;
            if (i >= 1)
            {
                fastEma = RoundedSeededEma.Next(inputList[i], fastEma, fastLength);
                slowEma = RoundedSeededEma.Next(inputList[i], slowEma, slowLength);
                macd = RoundedPercentageChange.Of(fastEma, slowEma);
            }

            macdList.Add(macd);

            var prevMacd1 = i >= 1 ? macdList[i - 1] : 0;
            var prevMacd2 = i >= 2 ? macdList[i - 2] : 0;
            var signal = GetCompareSignal(macd - prevMacd1, prevMacd1 - prevMacd2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "NormalizedMacd", macdList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(macdList);
        stockData.IndicatorName = IndicatorName.NormalizedMacd;

        return stockData;
    }

    /// <summary>
    /// Calculates the Relative Volatility Index High.
    /// </summary>
    /// <remarks>
    /// The relative volatility index read from each bar's high: the standard deviation is sorted into the
    /// bars that rose and the bars that fell, and the reading is the share of it that belongs to the risers.
    /// It is the relative strength index with deviation in place of price change, so it measures the
    /// direction of volatility rather than the direction of price. The
    /// <see cref="CalculateRelativeVolatilityIndexLow"/> reads the same measure from each bar's low, and the
    /// two are averaged by the relative volatility index itself.
    /// </remarks>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="stdDevLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateRelativeVolatilityIndexHigh(this StockData stockData, int length = 14, int stdDevLength = 10)
    {
        return CalculateRelativeVolatilityIndexOn(stockData, stockData.HighPrices, length, stdDevLength,
            "RviHigh", IndicatorName.RelativeVolatilityIndexHigh);
    }

    /// <summary>
    /// Calculates the Relative Volatility Index Low.
    /// </summary>
    /// <remarks>
    /// The relative volatility index read from each bar's low, and the mirror of
    /// <see cref="CalculateRelativeVolatilityIndexHigh"/>.
    /// </remarks>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="stdDevLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateRelativeVolatilityIndexLow(this StockData stockData, int length = 14, int stdDevLength = 10)
    {
        return CalculateRelativeVolatilityIndexOn(stockData, stockData.LowPrices, length, stdDevLength,
            "RviLow", IndicatorName.RelativeVolatilityIndexLow);
    }

    /// <summary>
    /// The relative volatility index of one series, which the high and the low readings share.
    /// </summary>
    private static StockData CalculateRelativeVolatilityIndexOn(StockData stockData, List<double> series, int length,
        int stdDevLength, string outputKey, IndicatorName name)
    {
        using var state = new Streaming.RelativeVolatilityIndexCore(length, stdDevLength); List<double> values = new(series.Count); var signals = CreateSignalsList(stockData, series.Count);
        for (var i = 0; i < series.Count; i++) { var value = state.Next(series[i], true); var previous = i > 0 ? values[i - 1] : 0; var older = i > 1 ? values[i - 2] : 0; values.Add(value); signals?.Add(GetCompareSignal(value - previous, previous - older)); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { outputKey, values } }); stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = name; return stockData;
    }

    /// <summary>
    /// Calculates the Log Returns of the input series.
    /// </summary>
    /// <remarks>
    /// The natural logarithm of the ratio of a value to the one <paramref name="length"/> bars before it. A bar
    /// with no value that far back, or with a value of zero or less at either end, has no logarithm to take and
    /// publishes zero.
    /// </remarks>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateLogReturns(this StockData stockData, int length = 1)
    {
        length = Math.Max(length, 1);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var count = inputList.Count;
        List<double> returnsList = new(count);
        List<Signal>? signalsList = CreateSignalsList(stockData, count);

        for (var i = 0; i < count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= length ? inputList[i - length] : 0;
            var returns = i >= length ? StableLogRatio.Of(currentValue, prevValue) : 0;
            returnsList.Add(returns);

            var prevReturns1 = i >= 1 ? returnsList[i - 1] : 0;
            var prevReturns2 = i >= 2 ? returnsList[i - 2] : 0;
            var signal = GetCompareSignal(returns - prevReturns1, prevReturns1 - prevReturns2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Returns", returnsList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(returnsList);
        stockData.IndicatorName = IndicatorName.LogReturns;

        return stockData;
    }

    /// <summary>
    /// Calculates the Median Value of the input series.
    /// </summary>
    /// <remarks>
    /// The middle value of the window once sorted, averaging the middle pair when the length is even. Until
    /// the window fills there is no median to take, and the bar publishes its own value rather than zero, so
    /// the series starts on the scale it will keep.
    /// </remarks>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateMedianValue(this StockData stockData, int length = 14)
    {
        length = Math.Max(length, 1);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var count = inputList.Count;
        List<double> medianList = new(count);
        List<Signal>? signalsList = CreateSignalsList(stockData, count);
        var window = new double[length];

        for (var i = 0; i < count; i++)
        {
            double median;
            if (i < length - 1)
            {
                median = inputList[i];
            }
            else
            {
                for (var j = 0; j < length; j++)
                {
                    window[j] = inputList[i - length + 1 + j];
                }

                Array.Sort(window);
                median = length % 2 == 0 ? PriceMean.Of(window[(length / 2) - 1], window[length / 2]) : window[length / 2];
            }

            medianList.Add(median);

            var prevMedian1 = i >= 1 ? medianList[i - 1] : 0;
            var prevMedian2 = i >= 2 ? medianList[i - 2] : 0;
            var signal = GetCompareSignal(median - prevMedian1, prevMedian1 - prevMedian2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "MedianValue", medianList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(medianList);
        stockData.IndicatorName = IndicatorName.MedianValue;

        return stockData;
    }

    /// <summary>
    /// Calculates the Percent Rank of the input series.
    /// </summary>
    /// <remarks>
    /// The share of the previous <paramref name="length"/> values that the current one stands above, as a
    /// percentage. The current bar is ranked against the bars before it and is not counted among them, so a
    /// value never ranks against itself, and a bar without that many predecessors publishes zero.
    /// </remarks>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculatePercentRank(this StockData stockData, int length = 100)
    {
        length = Math.Max(length, 1);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var count = inputList.Count;
        List<double> percentRankList = new(count);
        List<Signal>? signalsList = CreateSignalsList(stockData, count);

        for (var i = 0; i < count; i++)
        {
            double percentRank = 0;
            if (i >= length)
            {
                var currentValue = inputList[i];
                var below = 0;
                for (var j = i - length; j < i; j++)
                {
                    if (inputList[j] < currentValue)
                    {
                        below++;
                    }
                }

                percentRank = 100d * below / length;
            }

            percentRankList.Add(percentRank);

            var prevRank1 = i >= 1 ? percentRankList[i - 1] : 0;
            var prevRank2 = i >= 2 ? percentRankList[i - 2] : 0;
            var signal = GetCompareSignal(percentRank - prevRank1, prevRank1 - prevRank2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "PercentRank", percentRankList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(percentRankList);
        stockData.IndicatorName = IndicatorName.PercentRank;

        return stockData;
    }

    /// <summary>
    /// Calculates the McClellan Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="signalLength"></param>
    /// <param name="mult"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateMcClellanOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int fastLength = 19, int slowLength = 39, int signalLength = 9, double mult = 1000)
    {
        List<double> advancesSumList = new(stockData.Count);
        List<double> declinesSumList = new(stockData.Count);
        List<double> ranaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var advancesSumWindow = new RollingSum();
        var declinesSumWindow = new RollingSum();

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            double advance = currentValue > prevValue ? 1 : 0;
            advancesSumWindow.Add(advance);

            double decline = currentValue < prevValue ? 1 : 0;
            declinesSumWindow.Add(decline);

            var advanceSum = advancesSumWindow.Sum(fastLength);
            advancesSumList.Add(advanceSum);

            var declineSum = declinesSumWindow.Sum(fastLength);
            declinesSumList.Add(declineSum);

            var rana = advanceSum + declineSum != 0 ? mult * (advanceSum - declineSum) / (advanceSum + declineSum) : 0;
            ranaList.Add(rana);
        }

        stockData.SetCustomValues(ranaList);
        var moList = CalculateMovingAverageConvergenceDivergence(stockData, maType, fastLength, slowLength, signalLength);
        var mcclellanOscillatorList = moList.ChainedOutputs["Macd"];
        var mcclellanSignalLineList = moList.ChainedOutputs["Signal"];
        var mcclellanHistogramList = moList.ChainedOutputs["Histogram"];
        for (var i = 0; i < stockData.Count; i++)
        {
            var mcclellanHistogram = mcclellanHistogramList[i];
            var prevMcclellanHistogram = i >= 1 ? mcclellanHistogramList[i - 1] : 0;

            var signal = GetCompareSignal(mcclellanHistogram, prevMcclellanHistogram);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "AdvSum", advancesSumList },
            { "DecSum", declinesSumList },
            { "Mo", mcclellanOscillatorList },
            { "Signal", mcclellanSignalLineList },
            { "Histogram", mcclellanHistogramList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(mcclellanOscillatorList);
        stockData.IndicatorName = IndicatorName.McClellanOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Quantitative Qualitative Estimation
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <param name="fastFactor"></param>
    /// <param name="slowFactor"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateQuantitativeQualitativeEstimation(this StockData stockData,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14, int smoothLength = 5, double fastFactor = 2.618,
        double slowFactor = 4.236)
    {
        var values = QqeWindow.Calculate(stockData, maType, length, smoothLength, fastFactor, slowFactor, false);
        var fast = values.Fast.ToList(); var slow = values.Slow.ToList();
        List<Signal>? signals = CreateSignalsList(stockData); signals?.AddRange(values.Trades);
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "FastAtrRsi", fast }, { "SlowAtrRsi", slow } });
        stockData.SetSignals(signals); stockData.SetCustomValues(new List<double>()); stockData.IndicatorName = IndicatorName.QuantitativeQualitativeEstimation; return stockData;
    }


    /// <summary>
    /// Calculates the Quasi White Noise
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="noiseLength"></param>
    /// <param name="divisor"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateQuasiWhiteNoise(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod,
        int length = 20, int noiseLength = 500, double divisor = 40)
    {
        List<double> whiteNoiseList = new(stockData.Count);
        List<double> whiteNoiseVarianceList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var connorsRsiList = CalculateConnorsRelativeStrengthIndex(stockData, maType, noiseLength, noiseLength, length).ChainedValues;
        for (var i = 0; i < stockData.Count; i++)
        {
            var connorsRsi = connorsRsiList[i];
            var prevConnorsRsi1 = i >= 1 ? connorsRsiList[i - 1] : 0;
            var prevConnorsRsi2 = i >= 2 ? connorsRsiList[i - 2] : 0;

            var whiteNoise = (connorsRsi - 50) * (1 / divisor);
            whiteNoiseList.Add(whiteNoise);

            var signal = GetRsiSignal(connorsRsi - prevConnorsRsi1, prevConnorsRsi1 - prevConnorsRsi2, connorsRsi, prevConnorsRsi1, 70, 30);
            signalsList?.Add(signal);
        }

        List<double> whiteNoiseSmaList;
        if (StrengthWindow.Supports(maType) && !Builder.Compute.ComponentAverage.HasOverrides)
        {
            using var average = new StrengthAverage(maType, noiseLength, whiteNoiseList.Count);
            whiteNoiseSmaList = whiteNoiseList.Select(v => average.Next(new StrengthValue(v), true).Mantissa).ToList();
        }
        else whiteNoiseSmaList = GetMovingAverageList(stockData, maType, noiseLength, whiteNoiseList);
        // WhiteNoiseVariance below is this squared, so it has to be the deviation of the noise window about
        // its own mean. Squaring CalculateStandardDeviationVolatility recovers the mean squared residual
        // from a moving average, which is not the variance the published output name promises. Both series
        // are published, so the mismatch was visible to callers. See issue #223.
        using var deviation = new ExactPopulationWindow(noiseLength);
        var whiteNoiseStdDevList = whiteNoiseList.Select(v => deviation.Next(v, true)).ToList();
        for (var i = 0; i < stockData.Count; i++)
        {
            var whiteNoiseStdDev = whiteNoiseStdDevList[i];

            var whiteNoiseVariance = Pow(whiteNoiseStdDev, 2);
            whiteNoiseVarianceList.Add(whiteNoiseVariance);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "WhiteNoise", whiteNoiseList },
            { "WhiteNoiseMa", whiteNoiseSmaList },
            { "WhiteNoiseStdDev", whiteNoiseStdDevList },
            { "WhiteNoiseVariance", whiteNoiseVarianceList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(whiteNoiseList);
        stockData.IndicatorName = IndicatorName.QuasiWhiteNoise;

        return stockData;
    }


    /// <summary>
    /// Calculates the LBR Paint Bars
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="lbLength"></param>
    /// <param name="atrMult"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateLBRPaintBars(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 9,
        int lbLength = 16, double atrMult = 2.5)
    {
        var result = LbrPaintWindow.Calculate(stockData, maType, length, lbLength, atrMult, false);
        var upperBandList = result.Upper.ToList(); var lowerBandList = result.Lower.ToList(); var aatrList = result.Width.ToList();
        var signalsList = CreateSignalsList(stockData); signalsList?.AddRange(result.Trades);

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            // Not a MiddleBand: aatr is the width the two bands are pulled in by, about 5 on a market
            // trading near 180, so as a middle band it sat below the lower one on all 251 fixture bars.
            // There is no centre to publish in its place either - these bands are a squeeze, drawn as
            // highest - aatr and lowest + aatr, and they genuinely cross: 171 of the 251 bars have the
            // upper below the lower. Any series put between them would be wrong on those bars.
            { "UpperBand", upperBandList },
            { "LowerBand", lowerBandList },
            { "Aatr", aatrList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.LBRPaintBars;

        return stockData;
    }


    /// <summary>
    /// Calculates the Linear Quadratic Convergence Divergence Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateLinearQuadraticConvergenceDivergenceOscillator(this StockData stockData,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 50, int signalLength = 25)
    {
        List<double> lqcdList = new(stockData.Count);
        List<double> histList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        // Every Calculate method leaves its result on the chained series, so the second component read
        // the first one's output instead of the input both of them measure.
        var callerSeries = stockData.CaptureInputSeries();
        var linregList = CalculateLinearRegression(stockData, length).ChainedValues;
        stockData.RestoreInputSeries(callerSeries);
        var yList = CalculateQuadraticRegression(stockData, maType, length).ChainedValues;
        stockData.RestoreInputSeries(callerSeries);

        for (var i = 0; i < stockData.Count; i++)
        {
            var linreg = linregList[i];
            var y = yList[i];

            var lqcd = y - linreg;
            lqcdList.Add(lqcd);
        }

        var signList = GetMovingAverageList(stockData, maType, signalLength, lqcdList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var sign = signList[i];
            var lqcd = lqcdList[i];
            var osc = lqcd - sign;

            var prevHist = GetLastOrDefault(histList);
            var hist = osc - sign;
            histList.Add(hist);

            var signal = GetCompareSignal(hist, prevHist);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Lqcdo", histList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(histList);
        stockData.IndicatorName = IndicatorName.LinearQuadraticConvergenceDivergenceOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Logistic Correlation
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="k"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateLogisticCorrelation(this StockData stockData, int length = 100, double k = 10)
    {
        List<double> output = new(stockData.Count);
        List<Signal>? signals = CreateSignalsList(stockData);
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        using var window = new LogisticCorrelationWindow(length, k);
        for (var i = 0; i < stockData.Count; i++)
        {
            var value = window.Next(input[i], true);
            var previous = i == 0 ? 0 : output[i - 1];
            var beforePrevious = i < 2 ? 0 : output[i - 2];
            output.Add(value);
            signals?.Add(GetCompareSignal(value - previous, previous - beforePrevious));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "LogCorr", output } });
        stockData.SetSignals(signals); stockData.SetCustomValues(output);
        stockData.IndicatorName = IndicatorName.LogisticCorrelation;
        return stockData;
    }


    /// <summary>
    /// Calculates the Linda Raschke 3/10 Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateLindaRaschke3_10Oscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int fastLength = 3, int slowLength = 10, int smoothLength = 16)
    {
        List<double> macdList = new(stockData.Count);
        List<double> macdHistogramList = new(stockData.Count);
        List<double> ppoList = new(stockData.Count);
        List<double> ppoHistogramList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var fastSmaList = maType == MovingAvgType.SimpleMovingAverage ? BollingerArithmetic.Mean(inputList, fastLength) : GetMovingAverageList(stockData, maType, fastLength, inputList);
        var slowSmaList = maType == MovingAvgType.SimpleMovingAverage ? BollingerArithmetic.Mean(inputList, slowLength) : GetMovingAverageList(stockData, maType, slowLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var sma3 = fastSmaList[i];
            var sma10 = slowSmaList[i];

            var ppo = RoundedPercentageChange.Of(sma3, sma10);
            ppoList.Add(ppo);

            var macd = sma3 - sma10;
            macdList.Add(macd);
        }

        var macdInput = FiniteSignalInput.Create(macdList, out var macdFiniteCount);
        var macdSignalLineList = maType == MovingAvgType.SimpleMovingAverage ? BollingerArithmetic.Mean(macdInput, smoothLength) : GetMovingAverageList(stockData, maType, smoothLength, macdInput);
        for (var i = macdFiniteCount; i < macdSignalLineList.Count; i++) macdSignalLineList[i] = double.NaN;
        var ppoInput = FiniteSignalInput.Create(ppoList, out var ppoFiniteCount);
        var ppoSignalLineList = maType == MovingAvgType.SimpleMovingAverage ? BollingerArithmetic.Mean(ppoInput, smoothLength) : GetMovingAverageList(stockData, maType, smoothLength, ppoInput);
        for (var i = ppoFiniteCount; i < ppoSignalLineList.Count; i++) ppoSignalLineList[i] = double.NaN;
        for (var i = 0; i < stockData.Count; i++)
        {
            var ppo = ppoList[i];
            var ppoSignalLine = ppoSignalLineList[i];
            var macd = macdList[i];
            var macdSignalLine = macdSignalLineList[i];

            var ppoHistogram = ppo - ppoSignalLine;
            ppoHistogramList.Add(ppoHistogram);

            var prevMacdHistogram = GetLastOrDefault(macdHistogramList);
            var macdHistogram = macd - macdSignalLine;
            macdHistogramList.Add(macdHistogram);

            var signal = GetCompareSignal(macdHistogram, prevMacdHistogram);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "LindaMacd", macdList },
            { "LindaMacdSignal", macdSignalLineList },
            { "LindaMacdHistogram", macdHistogramList },
            { "LindaPpo", ppoList },
            { "LindaPpoSignal", ppoSignalLineList },
            { "LindaPpoHistogram", ppoHistogramList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(macdList);
        stockData.IndicatorName = IndicatorName.LindaRaschke3_10Oscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Relative Volatility Index V1
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateRelativeVolatilityIndexV1(this StockData stockData,
        MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int length = 10, int smoothLength = 14)
    {
        smoothLength = Math.Max(1, smoothLength); var (input, _, _, _, _) = GetInputValuesList(stockData); var external = Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType); using var window = new RelativeVolatilityWindow(maType, length, smoothLength, external, input.Count); List<double> values = new(input.Count); var signals = CreateSignalsList(stockData);
        if (external)
        {
            List<double> up = new(input.Count), down = new(input.Count); foreach (var price in input) { var p = window.Generate(price, true); up.Add(p.Up); down.Add(p.Down); }
            var upMean = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(up), smoothLength)?.ToList() ?? GetMovingAverageList(stockData, maType, smoothLength, up); var downMean = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(down), smoothLength)?.ToList() ?? GetMovingAverageList(stockData, maType, smoothLength, down);
            for (var i = 0; i < input.Count; i++) values.Add(RelativeVolatilityWindow.Ratio(new RocBankValue(upMean[i]), new RocBankValue(downMean[i])));
        }
        else foreach (var price in input) values.Add(window.Next(price, true));
        for (var i = 0; i < input.Count; i++) { var previous = i > 0 ? values[i - 1] : 0; var older = i > 1 ? values[i - 2] : 0; signals?.Add(GetRsiSignal(values[i] - previous, previous - older, values[i], previous, 70, 30)); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Rvi", values } }); stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.RelativeVolatilityIndexV1; return stockData;
    }


    /// <summary>
    /// Calculates the Relative Volatility Index V2
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateRelativeVolatilityIndexV2(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod,
        int length = 10, int smoothLength = 14)
    {
        List<double> rviList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (_, highList, lowList, _, _) = GetInputValuesList(stockData);

        stockData.SetCustomValues(highList);
        var rviHighList = CalculateRelativeVolatilityIndexV1(stockData, maType, length, smoothLength).ChainedValues;
        stockData.SetCustomValues(lowList);
        var rviLowList = CalculateRelativeVolatilityIndexV1(stockData, maType, length, smoothLength).ChainedValues;

        for (var i = 0; i < stockData.Count; i++)
        {
            var rviOriginalHigh = rviHighList[i];
            var rviOriginalLow = rviLowList[i];
            var prevRvi1 = i >= 1 ? rviList[i - 1] : 0;
            var prevRvi2 = i >= 2 ? rviList[i - 2] : 0;

            var rvi = RelativeVolatilityWindow.Mean(rviOriginalHigh, rviOriginalLow);
            rviList.Add(rvi);

            var signal = GetRsiSignal(rvi - prevRvi1, prevRvi1 - prevRvi2, rvi, prevRvi1, 70, 30);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Rvi", rviList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(rviList);
        stockData.IndicatorName = IndicatorName.RelativeVolatilityIndexV2;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ocean Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateOceanIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 14)
    {
        List<double> lnList = new(stockData.Count);
        List<double> oiList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevLn = i >= length ? lnList[i - length] : 0;

            var ln = currentValue > 0 ? Math.Log(currentValue) * 1000 : 0;
            lnList.Add(ln);

            var oi = (ln - prevLn) / Sqrt(length) * 100;
            oiList.Add(oi);
        }

        var oiEmaList = GetMovingAverageList(stockData, maType, length, oiList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var oiEma = oiEmaList[i];
            var prevOiEma1 = i >= 1 ? oiEmaList[i - 1] : 0;
            var prevOiEma2 = i >= 2 ? oiEmaList[i - 2] : 0;

            var signal = GetCompareSignal(oiEma - prevOiEma1, prevOiEma1 - prevOiEma2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Oi", oiList },
            { "Signal", oiEmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(oiList);
        stockData.IndicatorName = IndicatorName.OceanIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Oscar Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateOscarIndicator(this StockData stockData, int length = 8)
    {
        var (input, high, low, _, _) = GetInputValuesList(stockData);
        var values = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        using var window = new OscarWindow(length);
        for (var i = 0; i < input.Count; i++)
        { var point = window.Next(input[i], high[i], low[i], true); values.Add(point.Value); signals?.Add(point.Trade); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Oscar", values } });
        stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.OscarIndicator;
        return stockData;
    }


    /// <summary>
    /// Calculates the OC Histogram
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateOCHistogram(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 10)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); var opens = stockData.OpenPrices;
        var custom = Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType);
        List<double>? customerOpen = null, customerClose = null;
        if (custom)
        {
            customerOpen = GetMovingAverageList(stockData, maType, length, opens);
            customerClose = GetMovingAverageList(stockData, maType, length, input);
        }
        List<double> line = new(stockData.Count); List<Signal>? signals = CreateSignalsList(stockData);
        using var window = new OpenCloseHistogramWindow(maType, length);
        for (var i = 0; i < input.Count; i++)
        {
            var value = window.Next(opens[i], input[i], true, customerOpen?[i], customerClose?[i]);
            signals?.Add(GetCompareSignal(value, i == 0 ? 0 : line[i - 1])); line.Add(value);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "OcHistogram", line } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.OCHistogram;
        return stockData;
    }


    /// <summary>
    /// Calculates the Osc Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateOscOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int fastLength = 7, int slowLength = 14)
    {
        List<double> oscList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        fastLength = Math.Max(1, fastLength);
        slowLength = Math.Max(1, slowLength);
        var fastSmaList = StrengthWindow.Supports(maType) ? StrengthWindow.Smooth(inputList, maType, fastLength)
            : GetMovingAverageList(stockData, maType, fastLength, inputList);
        var slowSmaList = StrengthWindow.Supports(maType) ? StrengthWindow.Smooth(inputList, maType, slowLength)
            : GetMovingAverageList(stockData, maType, slowLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var fastSma = fastSmaList[i];
            var slowSma = slowSmaList[i];
            var prevOsc1 = i >= 1 ? oscList[i - 1] : 0;
            var prevOsc2 = i >= 2 ? oscList[i - 2] : 0;

            var osc = slowSma - fastSma;
            oscList.Add(osc);

            var signal = GetCompareSignal(osc - prevOsc1, prevOsc1 - prevOsc2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "OscOscillator", oscList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(oscList);
        stockData.IndicatorName = IndicatorName.OscOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Natural Directional Combo
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateNaturalDirectionalCombo(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 40, int smoothLength = 20)
    {
        var callerSeries = stockData.CaptureInputSeries();
        List<double> nxcList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var ndxList = CalculateNaturalDirectionalIndex(stockData, maType, length, smoothLength).ChainedValues;
        // The next component reads the caller's series, not the previous component's output.
        stockData.RestoreInputSeries(callerSeries);
        var nstList = CalculateNaturalStochasticIndicator(stockData, maType, length, smoothLength).ChainedValues;

        for (var i = 0; i < stockData.Count; i++)
        {
            var ndx = ndxList[i];
            var nst = nstList[i];
            var prevNxc1 = i >= 1 ? nxcList[i - 1] : 0;
            var prevNxc2 = i >= 2 ? nxcList[i - 2] : 0;
            var v3 = Math.Sign(ndx) != Math.Sign(nst) ? ndx * nst : ((Math.Abs(ndx) * nst) + (Math.Abs(nst) * ndx)) / 2;

            var nxc = Math.Sign(v3) * Sqrt(Math.Abs(v3));
            nxcList.Add(nxc);

            var signal = GetCompareSignal(nxc - prevNxc1, prevNxc1 - prevNxc2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Nxc", nxcList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(nxcList);
        stockData.IndicatorName = IndicatorName.NaturalDirectionalCombo;

        return stockData;
    }


    /// <summary>
    /// Calculates the Natural Directional Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateNaturalDirectionalIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 40, int smoothLength = 20)
    {
        List<double> lnList = new(stockData.Count);
        List<double> rawNdxList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];

            var ln = currentValue > 0 ? Math.Log(currentValue) * 1000 : 0;
            lnList.Add(ln);

            double weightSum = 0, denomSum = 0, absSum = 0;
            for (var j = 0; j < length; j++)
            {
                var prevLn = i >= j + 1 ? lnList[i - (j + 1)] : 0;
                var currLn = i >= j ? lnList[i - j] : 0;
                var diff = prevLn - currLn;
                absSum += Math.Abs(diff);
                var frac = absSum != 0 ? (ln - currLn) / absSum : 0;
                var ratio = 1 / Sqrt(j + 1);
                weightSum += frac * ratio;
                denomSum += ratio;
            }

            var rawNdx = denomSum != 0 ? weightSum / denomSum * 100 : 0;
            rawNdxList.Add(rawNdx);
        }

        var ndxList = GetMovingAverageList(stockData, maType, smoothLength, rawNdxList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var ndx = ndxList[i];
            var prevNdx1 = i >= 1 ? ndxList[i - 1] : 0;
            var prevNdx2 = i >= 2 ? ndxList[i - 2] : 0;

            var signal = GetCompareSignal(ndx - prevNdx1, prevNdx1 - prevNdx2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ndx", ndxList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(ndxList);
        stockData.IndicatorName = IndicatorName.NaturalDirectionalIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Natural Market Mirror
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateNaturalMarketMirror(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 40)
    {
        List<double> lnList = new(stockData.Count);
        List<double> oiAvgList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];

            var ln = currentValue > 0 ? Math.Log(currentValue) * 1000 : 0;
            lnList.Add(ln);

            double oiSum = 0;
            for (var j = 1; j <= length; j++)
            {
                var prevLn = i >= j ? lnList[i - j] : 0;
                oiSum += (ln - prevLn) / Sqrt(j) * 100;
            }

            var oiAvg = oiSum / length;
            oiAvgList.Add(oiAvg);
        }

        var nmmList = GetMovingAverageList(stockData, maType, length, oiAvgList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var nmm = nmmList[i];
            var prevNmm1 = i >= 1 ? nmmList[i - 1] : 0;
            var prevNmm2 = i >= 2 ? nmmList[i - 2] : 0;

            var signal = GetCompareSignal(nmm - prevNmm1, prevNmm1 - prevNmm2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Nmm", nmmList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(nmmList);
        stockData.IndicatorName = IndicatorName.NaturalMarketMirror;

        return stockData;
    }


    /// <summary>
    /// Calculates the Natural Market River
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateNaturalMarketRiver(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 40)
    {
        List<double> lnList = new(stockData.Count);
        List<double> oiSumList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];

            var ln = currentValue > 0 ? Math.Log(currentValue) * 1000 : 0;
            lnList.Add(ln);

            double oiSum = 0;
            for (var j = 0; j < length; j++)
            {
                var currentLn = i >= j ? lnList[i - j] : 0;
                var prevLn = i >= j + 1 ? lnList[i - (j + 1)] : 0;

                oiSum += (prevLn - currentLn) * (Sqrt(j) - Sqrt(j + 1));
            }
            oiSumList.Add(oiSum);
        }

        var nmrList = GetMovingAverageList(stockData, maType, length, oiSumList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var nmr = nmrList[i];
            var prevNmr1 = i >= 1 ? nmrList[i - 1] : 0;
            var prevNmr2 = i >= 2 ? nmrList[i - 2] : 0;

            var signal = GetCompareSignal(nmr - prevNmr1, prevNmr1 - prevNmr2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Nmr", nmrList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(nmrList);
        stockData.IndicatorName = IndicatorName.NaturalMarketRiver;

        return stockData;
    }


    /// <summary>
    /// Calculates the Natural Market Combo
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateNaturalMarketCombo(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length = 40, int smoothLength = 20)
    {
        var callerSeries = stockData.CaptureInputSeries();
        List<double> nmcList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var nmrList = CalculateNaturalMarketRiver(stockData, maType, length).ChainedValues;
        // The next component reads the caller's series, not the previous component's output.
        stockData.RestoreInputSeries(callerSeries);
        var nmmList = CalculateNaturalMarketMirror(stockData, maType, length).ChainedValues;

        for (var i = 0; i < stockData.Count; i++)
        {
            var nmr = nmrList[i];
            var nmm = nmmList[i];
            var v3 = Math.Sign(nmm) != Math.Sign(nmr) ? nmm * nmr : ((Math.Abs(nmm) * nmr) + (Math.Abs(nmr) * nmm)) / 2;

            var nmc = Math.Sign(v3) * Sqrt(Math.Abs(v3));
            nmcList.Add(nmc);
        }

        var nmcMaList = GetMovingAverageList(stockData, maType, smoothLength, nmcList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var nmc = nmcMaList[i];
            var prevNmc1 = i >= 1 ? nmcMaList[i - 1] : 0;
            var prevNmc2 = i >= 2 ? nmcMaList[i - 2] : 0;

            var signal = GetCompareSignal(nmc - prevNmc1, prevNmc1 - prevNmc2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Nmc", nmcList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(nmcList);
        stockData.IndicatorName = IndicatorName.NaturalMarketCombo;

        return stockData;
    }


    /// <summary>
    /// Calculates the Natural Market Slope
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateNaturalMarketSlope(this StockData stockData, int length = 40)
    {
        List<double> lnList = new(stockData.Count);
        List<double> nmsList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];

            var ln = currentValue > 0 ? Math.Log(currentValue) * 1000 : 0;
            lnList.Add(ln);
        }

        stockData.SetCustomValues(lnList);
        var linRegList = CalculateLinearRegression(stockData, length).ChainedValues;
        for (var i = 0; i < stockData.Count; i++)
        {
            var linReg = linRegList[i];
            var prevLinReg = i >= 1 ? linRegList[i - 1] : 0;
            var prevNms1 = i >= 1 ? nmsList[i - 1] : 0;
            var prevNms2 = i >= 2 ? nmsList[i - 2] : 0;

            var nms = (linReg - prevLinReg) * Math.Log(length);
            nmsList.Add(nms);

            var signal = GetCompareSignal(nms - prevNms1, prevNms1 - prevNms2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Nms", nmsList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(nmsList);
        stockData.IndicatorName = IndicatorName.NaturalMarketSlope;

        return stockData;
    }


    /// <summary>
    /// Calculates the Narrow Bandpass Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateNarrowBandpassFilter(this StockData stockData, int length = 50)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        using var window = new NarrowBandpassWindow(length, Math.Max(1, input.Count));
        List<double> line = new(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            var value = window.Next(input[i], true); line.Add(value);
            signals?.Add(GetCompareSignal(value - (i > 0 ? line[i - 1] : 0), (i > 0 ? line[i - 1] : 0) - (i > 1 ? line[i - 2] : 0)));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Nbpf", line } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.NarrowBandpassFilter;
        return stockData;
    }


    /// <summary>
    /// Calculates the Nth Order Differencing Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="lbLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateNthOrderDifferencingOscillator(this StockData stockData, int length = 14, int lbLength = 2)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); using var window = new NthDifferenceWindow(length, lbLength, input.Count); List<double> values = new(input.Count); var signals = CreateSignalsList(stockData);
        foreach (var price in input) { var value = window.Next(price, true); var previous = values.Count > 0 ? values[values.Count - 1] : 0; values.Add(value); signals?.Add(GetCompareSignal(value, previous)); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Nodo", values } }); stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.NthOrderDifferencingOscillator; return stockData;
    }


    /// <summary>
    /// Calculates the Normalized Relative Vigor Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateNormalizedRelativeVigorIndex(this StockData stockData,
        MovingAvgType maType = MovingAvgType.SymmetricallyWeightedMovingAverage, int length = 10)
    {
        length=Math.Max(1,length); var input=stockData.ChainedValues.Count>0?stockData.ChainedValues:stockData.InputValues;
        List<double> values=new(input.Count), signal=new(input.Count); var signals=CreateSignalsList(stockData);
        using var window=new NormalizedVigorWindow(maType,length,input.Count);
        if(Builder.Compute.ComponentAverage.HasOverrides || (!StrengthWindow.Supports(maType) && maType!=MovingAvgType.SymmetricallyWeightedMovingAverage))
        {
            var bodies=input.Select((v,i)=>NormalizedVigorWindow.Difference(v,stockData.OpenPrices[i]).Publish()).ToList();
            var ranges=input.Select((v,i)=>NormalizedVigorWindow.Difference(stockData.HighPrices[i],stockData.LowPrices[i]).Publish()).ToList();
            var b=Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(bodies),length)?.ToList()??GetMovingAverageList(stockData,maType,length,bodies);
            var r=Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(ranges),length)?.ToList()??GetMovingAverageList(stockData,maType,length,ranges);
            for(var i=0;i<input.Count;i++) values.Add(window.Ratio(new RocBankValue(b[i]),new RocBankValue(r[i]),true).Publish());
            signal=Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(values),length)?.ToList()??GetMovingAverageList(stockData,maType,length,values);
        }
        else for(var i=0;i<input.Count;i++) {var r=window.Next(input[i],stockData.OpenPrices[i],stockData.HighPrices[i],stockData.LowPrices[i],true);values.Add(r.Value);signal.Add(r.Signal);}
        for(var i=0;i<input.Count;i++) signals?.Add(GetCompareSignal(values[i]-signal[i],i>0?values[i-1]-signal[i-1]:0));
        stockData.SetOutputValues(()=>new Dictionary<string,List<double>>{{"Nrvi",values},{"Signal",signal}});stockData.SetSignals(signals);stockData.SetCustomValues(values);stockData.IndicatorName=IndicatorName.NormalizedRelativeVigorIndex;return stockData;
    }


    /// <summary>
    /// Calculates the Math.PIvot Detector Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculatePivotDetectorOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length1 = 200, int length2 = 14)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        var result = PivotDetectorWindow.Calculate(stockData, input, maType, length1, length2, false);
        var values = result.Values.ToList();
        List<Signal>? signals = CreateSignalsList(stockData);
        if (signals is not null) signals.AddRange(result.Trades);
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Pdo", values } });
        stockData.SetSignals(signals); stockData.SetCustomValues(values);
        stockData.IndicatorName = IndicatorName.PivotDetectorOscillator; return stockData;
    }


    /// <summary>
    /// Calculates the Percent Change Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculatePercentChangeOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length = 14)
    {
        length = Math.Max(1, length); var (input, _, _, _, _) = GetInputValuesList(stockData); var external = Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType); using var window = new OneBarReturnWindow(true, maType, length, external, input.Count); List<double> values = new(input.Count), average = new(input.Count); var signals = CreateSignalsList(stockData);
        foreach (var price in input) { var p = window.Next(price, true); values.Add(p.Value); average.Add(p.Signal); }
        if (external) average = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(values), length)?.ToList() ?? GetMovingAverageList(stockData, maType, length, values);
        for (var i = 0; i < input.Count; i++) signals?.Add(GetCompareSignal(values[i] - average[i], i > 0 ? values[i - 1] - average[i - 1] : 0));
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Pcco", values }, { "Signal", average } }); stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.PercentChangeOscillator; return stockData;
    }


    /// <summary>
    /// Calculates the Prime Number Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculatePrimeNumberOscillator(this StockData stockData, int length = 5)
    {
        List<double> pnoList = new(stockData.Count);
        List<double> pno1List = new(stockData.Count);
        List<double> pno2List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var primes = PrimeNumberSearch.Find(currentValue, length);
            var pno1 = primes.Upper == 0 ? GetLastOrDefault(pno1List) : primes.Upper;
            var pno2 = primes.Lower == 0 ? GetLastOrDefault(pno2List) : primes.Lower;
            pno1List.Add(pno1);
            pno2List.Add(pno2);

            var prevPno = GetLastOrDefault(pnoList);
            var pno = pno1 - currentValue < currentValue - pno2 ? pno1 - currentValue : pno2 - currentValue;
            pno = pno == 0 ? prevPno : pno;
            pnoList.Add(pno);

            var signal = GetCompareSignal(pno, prevPno);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Pno", pnoList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(pnoList);
        stockData.IndicatorName = IndicatorName.PrimeNumberOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Pring Special K
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
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculatePringSpecialK(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 10,
        int length2 = 15, int length3 = 20, int length4 = 30, int length5 = 40, int length6 = 50, int length7 = 65, int length8 = 75, int length9 = 100,
        int length10 = 130, int length11 = 195, int length12 = 265, int length13 = 390, int length14 = 530, int smoothLength = 10)
    {
        List<double> specialKList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        // Each component reads the prices; each Calculate call leaves its own output on CustomValuesList.
        var callerSeries = stockData.CaptureInputSeries();
        List<double> specialKSignalList;
        if (StrengthWindow.Supports(maType) && !Builder.Compute.ComponentAverage.HasOverrides)
        {
            var (prices, _, _, _, _) = GetInputValuesList(stockData);
            specialKSignalList = new(stockData.Count);
            using var bank = new RocBankWindow(maType, new[] { length1, length2, length3, length4, length5, length7, length8, length9, length11, length12, length13, length14 },
                new[] { length1, length1, length1, length2, length6, length7, length8, length9, length10, length10, length10, length11 },
                new[] { 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4 }, smoothLength, stockData.Count);
            foreach (var price in prices)
            {
                var next = bank.Next(price, true);
                specialKList.Add(next.Value); specialKSignalList.Add(next.Signal);
            }
        }
        else
        {
            var rocList = CalculateRateOfChange(stockData, length1).ChainedValues;
            stockData.RestoreInputSeries(callerSeries);
            var roc15List = CalculateRateOfChange(stockData, length2).ChainedValues;
            stockData.RestoreInputSeries(callerSeries);
            var roc20List = CalculateRateOfChange(stockData, length3).ChainedValues;
            stockData.RestoreInputSeries(callerSeries);
            var roc30List = CalculateRateOfChange(stockData, length4).ChainedValues;
            stockData.RestoreInputSeries(callerSeries);
            var roc40List = CalculateRateOfChange(stockData, length5).ChainedValues;
            stockData.RestoreInputSeries(callerSeries);
            var roc65List = CalculateRateOfChange(stockData, length7).ChainedValues;
            stockData.RestoreInputSeries(callerSeries);
            var roc75List = CalculateRateOfChange(stockData, length8).ChainedValues;
            stockData.RestoreInputSeries(callerSeries);
            var roc100List = CalculateRateOfChange(stockData, length9).ChainedValues;
            stockData.RestoreInputSeries(callerSeries);
            var roc195List = CalculateRateOfChange(stockData, length11).ChainedValues;
            stockData.RestoreInputSeries(callerSeries);
            var roc265List = CalculateRateOfChange(stockData, length12).ChainedValues;
            stockData.RestoreInputSeries(callerSeries);
            var roc390List = CalculateRateOfChange(stockData, length13).ChainedValues;
            stockData.RestoreInputSeries(callerSeries);
            var roc530List = CalculateRateOfChange(stockData, length14).ChainedValues;
            var roc10SmaList = GetMovingAverageList(stockData, maType, length1, rocList);
            var roc15SmaList = GetMovingAverageList(stockData, maType, length1, roc15List);
            var roc20SmaList = GetMovingAverageList(stockData, maType, length1, roc20List);
            var roc30SmaList = GetMovingAverageList(stockData, maType, length2, roc30List);
            var roc40SmaList = GetMovingAverageList(stockData, maType, length6, roc40List);
            var roc65SmaList = GetMovingAverageList(stockData, maType, length7, roc65List);
            var roc75SmaList = GetMovingAverageList(stockData, maType, length8, roc75List);
            var roc100SmaList = GetMovingAverageList(stockData, maType, length9, roc100List);
            var roc195SmaList = GetMovingAverageList(stockData, maType, length10, roc195List);
            var roc265SmaList = GetMovingAverageList(stockData, maType, length10, roc265List);
            var roc390SmaList = GetMovingAverageList(stockData, maType, length10, roc390List);
            var roc530SmaList = GetMovingAverageList(stockData, maType, length11, roc530List);

            for (var i = 0; i < stockData.Count; i++)
            {
                var roc10Sma = roc10SmaList[i];
                var roc15Sma = roc15SmaList[i];
                var roc20Sma = roc20SmaList[i];
                var roc30Sma = roc30SmaList[i];
                var roc40Sma = roc40SmaList[i];
                var roc65Sma = roc65SmaList[i];
                var roc75Sma = roc75SmaList[i];
                var roc100Sma = roc100SmaList[i];
                var roc195Sma = roc195SmaList[i];
                var roc265Sma = roc265SmaList[i];
                var roc390Sma = roc390SmaList[i];
                var roc530Sma = roc530SmaList[i];

                var specialK = (roc10Sma * 1) + (roc15Sma * 2) + (roc20Sma * 3) + (roc30Sma * 4) + (roc40Sma * 1) + (roc65Sma * 2) + (roc75Sma * 3) +
                               (roc100Sma * 4) + (roc195Sma * 1) + (roc265Sma * 2) + (roc390Sma * 3) + (roc530Sma * 4);
                specialKList.Add(specialK);
            }

            specialKSignalList = GetMovingAverageList(stockData, maType, smoothLength, specialKList);
        }

        for (var i = 0; i < stockData.Count; i++)
        {
            var specialK = specialKList[i];
            var specialKSignal = specialKSignalList[i];
            var prevSpecialK = i >= 1 ? specialKList[i - 1] : 0;
            var prevSpecialKSignal = i >= 1 ? specialKSignalList[i - 1] : 0;

            var signal = GetCompareSignal(specialK - specialKSignal, prevSpecialK - prevSpecialKSignal);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "PringSpecialK", specialKList },
            { "Signal", specialKSignalList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(specialKList);
        stockData.IndicatorName = IndicatorName.PringSpecialK;

        return stockData;
    }


    /// <summary>
    /// Calculates the Price Zone Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculatePriceZoneOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length = 20)
    {
        List<double> pzoList = new(stockData.Count);
        List<double> dvolList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var emaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var dvol = i == 0 ? 0 : currentValue.CompareTo(prevValue) * currentValue;
            dvolList.Add(dvol);
        }

        var dvmaList = GetMovingAverageList(stockData, maType, length, dvolList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var vma = emaList[i];
            var dvma = dvmaList[i];
            var prevPzo1 = i >= 1 ? pzoList[i - 1] : 0;
            var prevPzo2 = i >= 2 ? pzoList[i - 2] : 0;

            var pzo = PriceZoneWindow.Ratio(dvma, vma);
            pzoList.Add(pzo);

            var signal = GetRsiSignal(pzo - prevPzo1, prevPzo1 - prevPzo2, pzo, prevPzo1, 40, -40);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Pzo", pzoList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(pzoList);
        stockData.IndicatorName = IndicatorName.PriceZoneOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Performance Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculatePerformanceIndex(this StockData stockData, int length = 14)
    {
        length = Math.Max(1, length);
        List<double> kpiList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= length ? inputList[i - length] : 0;

            var prevKpi = GetLastOrDefault(kpiList);
            var kpi = RoundedPercentageChange.Of(currentValue, prevValue);
            kpiList.Add(kpi);

            var signal = GetCompareSignal(kpi, prevKpi);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "PerformanceIndex", kpiList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(kpiList);
        stockData.IndicatorName = IndicatorName.PerformanceIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Polarized Fractal Efficiency
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculatePolarizedFractalEfficiency(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 9, int smoothLength = 5)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        var values = PolarizedEfficiencyWindow.Calculate(stockData, input, maType, length, smoothLength, false).ToList();
        List<Signal>? signals = CreateSignalsList(stockData);
        for (var i = 0; i < values.Count; i++) signals?.Add(GetCompareSignal(values[i], i == 0 ? 0 : values[i - 1]));
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Pfe", values } });
        stockData.SetSignals(signals); stockData.SetCustomValues(values);
        stockData.IndicatorName = IndicatorName.PolarizedFractalEfficiency;
        return stockData;
    }


    /// <summary>
    /// Calculates the Pretty Good Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculatePrettyGoodOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 14)
    {
        length=Math.Max(1,length);var input=stockData.ChainedValues.Count>0?stockData.ChainedValues:stockData.InputValues;using var window=new PrettyGoodWindow(maType,length,input.Count);
        var external=Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType);List<double>? average=null,atr=null;
        if(external)
        {
            average=Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(input),length)?.ToList() ?? GetMovingAverageList(stockData,maType,length,input);
            var ranges=PrettyGoodWindow.TrueRanges(stockData.HighPrices,stockData.LowPrices,input).ToList();
            atr=Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(ranges),length)?.ToList() ?? GetMovingAverageList(stockData,maType,length,ranges);
        }
        List<double> values=new(input.Count);var signals=CreateSignalsList(stockData);
        for(var i=0;i<input.Count;i++){var value=external?PrettyGoodWindow.Finish(input[i],new RocBankValue(average![i]),new RocBankValue(atr![i])):window.Next(stockData.HighPrices[i],stockData.LowPrices[i],input[i],true);signals?.Add(GetCompareSignal(value,i>0?values[i-1]:0));values.Add(value);}
        stockData.SetOutputValues(()=>new Dictionary<string,List<double>>{{"Pgo",values}});stockData.SetSignals(signals);stockData.SetCustomValues(values);stockData.IndicatorName=IndicatorName.PrettyGoodOscillator;return stockData;
    }


    /// <summary>
    /// Calculates the Price Cycle Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculatePriceCycleOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 22)
    {
        length=Math.Max(1,length);var input=stockData.ChainedValues.Count>0?stockData.ChainedValues:stockData.InputValues;using var window=new PriceCycleWindow(maType,length,input.Count);
        var external=Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType);List<double>? atr=null,distance=null;
        if(external)
        {
            var ranges=PrettyGoodWindow.TrueRanges(stockData.HighPrices,stockData.LowPrices,input).ToList();
            atr=Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(ranges),length)?.ToList() ?? GetMovingAverageList(stockData,maType,length,ranges);
            var difference=input.Select((v,i)=>PriceCycleWindow.Distance(v,stockData.LowPrices[i]).Publish()).ToList();
            distance=Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(difference),length)?.ToList() ?? GetMovingAverageList(stockData,maType,length,difference);
        }
        List<double> values=new(input.Count);var signals=CreateSignalsList(stockData);
        for(var i=0;i<input.Count;i++){var value=external?PriceCycleWindow.Finish(new RocBankValue(distance![i]),new RocBankValue(atr![i])):window.Next(stockData.HighPrices[i],stockData.LowPrices[i],input[i],true);var previous=i>0?values[i-1]:0;var older=i>1?values[i-2]:0;signals?.Add(GetCompareSignal(value-previous,previous-older));values.Add(value);}
        stockData.SetOutputValues(()=>new Dictionary<string,List<double>>{{"Pco",values}});stockData.SetSignals(signals);stockData.SetCustomValues(values);stockData.IndicatorName=IndicatorName.PriceCycleOscillator;return stockData;
    }


    /// <summary>
    /// Calculates the Phase Change Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculatePhaseChangeIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 35, int smoothLength = 3)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        var result = PhaseChangeWindow.Calculate(stockData, input, maType, length, smoothLength, callbacks: false);
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Pci", result.Line.ToList() }, { "Signal", result.SignalLine.ToList() } });
        var signals = CreateSignalsList(stockData); signals?.AddRange(result.Trades);
        stockData.SetSignals(signals); stockData.SetCustomValues(result.Line.ToList()); stockData.IndicatorName = IndicatorName.PhaseChangeIndex;
        return stockData;
    }


    /// <summary>
    /// Calculates the Peak Valley Estimation
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculatePeakValleyEstimation(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 500, int smoothLength = 100)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        var result = PeakValleyWindow.Calculate(stockData, input, maType, length, smoothLength, callbacks: false);
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Sign1", result.Sign1.ToList() }, { "Sign2", result.Sign2.ToList() }, { "Sign3", result.Sign3.ToList() } });
        var signals = CreateSignalsList(stockData); signals?.AddRange(result.Sign1.Select(v => GetConditionSignal(v > 0, v < 0)));
        stockData.SetSignals(signals); stockData.SetCustomValues(result.Sign1.ToList()); stockData.IndicatorName = IndicatorName.PeakValleyEstimation;
        return stockData;
    }


    /// <summary>
    /// Calculates the Psychological Line
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculatePsychologicalLine(this StockData stockData, int length = 20)
    {
        List<double> psyList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var condSumWindow = new RollingSum();

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevPsy1 = i >= 1 ? psyList[i - 1] : 0;
            var prevPsy2 = i >= 2 ? psyList[i - 2] : 0;

            double cond = i > 0 && currentValue > prevValue ? 1 : 0;
            condSumWindow.Add(cond);

            var condSum = condSumWindow.Sum(length);
            var psy = length != 0 ? 100d * condSum / length : 0;
            psyList.Add(psy);

            var signal = GetCompareSignal(psy - prevPsy1, prevPsy1 - prevPsy2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Pl", psyList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(psyList);
        stockData.IndicatorName = IndicatorName.PsychologicalLine;

        return stockData;
    }


    /// <summary>
    /// Calculates the Rahul Mohindar Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="length4"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateRahulMohindarOscillator(this StockData stockData, int length1 = 2, int length2 = 10, int length3 = 30, 
        int length4 = 81)
    {
        List<double> swingTrd1List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(inputList, length2);

        var r1List = GetMovingAverageList(stockData, MovingAvgType.SimpleMovingAverage, length1, inputList);
        var r2List = GetMovingAverageList(stockData, MovingAvgType.SimpleMovingAverage, length1, r1List); //-V3056
        var r3List = GetMovingAverageList(stockData, MovingAvgType.SimpleMovingAverage, length1, r2List);
        var r4List = GetMovingAverageList(stockData, MovingAvgType.SimpleMovingAverage, length1, r3List);
        var r5List = GetMovingAverageList(stockData, MovingAvgType.SimpleMovingAverage, length1, r4List);
        var r6List = GetMovingAverageList(stockData, MovingAvgType.SimpleMovingAverage, length1, r5List);
        var r7List = GetMovingAverageList(stockData, MovingAvgType.SimpleMovingAverage, length1, r6List);
        var r8List = GetMovingAverageList(stockData, MovingAvgType.SimpleMovingAverage, length1, r7List);
        var r9List = GetMovingAverageList(stockData, MovingAvgType.SimpleMovingAverage, length1, r8List);
        var r10List = GetMovingAverageList(stockData, MovingAvgType.SimpleMovingAverage, length1, r9List);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var highest = highestList[i];
            var lowest = lowestList[i];
            var r1 = r1List[i];
            var r2 = r2List[i];
            var r3 = r3List[i];
            var r4 = r4List[i];
            var r5 = r5List[i];
            var r6 = r6List[i];
            var r7 = r7List[i];
            var r8 = r8List[i];
            var r9 = r9List[i];
            var r10 = r10List[i];

            var swingTrd1 = highest - lowest != 0 ? 100 * (currentValue - ((r1 + r2 + r3 + r4 + r5 + r6 + r7 + r8 + r9 + r10) / 10)) / 
                                                    (highest - lowest) : 0;
            swingTrd1List.Add(swingTrd1);
        }

        var swingTrd2List = GetMovingAverageList(stockData, MovingAvgType.ExponentialMovingAverage, length3, swingTrd1List);
        var swingTrd3List = GetMovingAverageList(stockData, MovingAvgType.ExponentialMovingAverage, length3, swingTrd2List);
        var rmoList = GetMovingAverageList(stockData, MovingAvgType.ExponentialMovingAverage, length4, swingTrd1List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var rmo = rmoList[i];
            var prevRmo = i >= 1 ? rmoList[i - 1] : 0;

            var signal = GetCompareSignal(rmo, prevRmo);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Rmo", rmoList },
            { "SwingTrade1", swingTrd1List },
            { "SwingTrade2", swingTrd2List },
            { "SwingTrade3", swingTrd3List }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(rmoList);
        stockData.IndicatorName = IndicatorName.RahulMohindarOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Rainbow Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateRainbowOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length1 = 2, int length2 = 10)
    {
        var result = RainbowWindow.Calculate(stockData, maType, length1, length2, false);
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> {
            { "Ro", result.Line.ToList() }, { "UpperBand", result.Upper.ToList() }, { "LowerBand", result.Lower.ToList() } });
        stockData.SetSignals(CreateSignalsList(stockData) is null ? null : result.Trades.ToList());
        stockData.SetCustomValues(result.Line.ToList()); stockData.IndicatorName = IndicatorName.RainbowOscillator;
        return stockData;
    }


    /// <summary>
    /// Calculates the Random Walk Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateRandomWalkIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, 
        int length = 14)
    {
        var result = RandomWalkWindow.Calculate(stockData, maType, length, false);
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> {
            { "RwiHigh", result.High.ToList() }, { "RwiLow", result.Low.ToList() } });
        stockData.SetSignals(CreateSignalsList(stockData) is null ? null : result.Trades.ToList());
        stockData.SetCustomValues(new List<double>()); stockData.IndicatorName = IndicatorName.RandomWalkIndex; return stockData;
    }


    /// <summary>
    /// Calculates the Range Action Verification Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateRangeActionVerificationIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int fastLength = 7, int slowLength = 65)
    {
        fastLength=Math.Max(1,fastLength);slowLength=Math.Max(1,slowLength);var input=stockData.ChainedValues.Count>0?stockData.ChainedValues:stockData.InputValues;List<double> values=new(input.Count);var signals=CreateSignalsList(stockData);
        if(Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType))
        {
            var fast=Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(input),fastLength)?.ToList()??GetMovingAverageList(stockData,maType,fastLength,input);
            var slow=Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(input),slowLength)?.ToList()??GetMovingAverageList(stockData,maType,slowLength,input);
            for(var i=0;i<input.Count;i++)values.Add(AverageGapWindow.Gap(fast[i],slow[i]));
        }
        else {using var window=new AverageGapWindow(maType,fastLength,slowLength,input.Count);foreach(var value in input)values.Add(window.Next(value,true));}
        for(var i=0;i<input.Count;i++)signals?.Add(GetCompareSignal(values[i]-(i>0?values[i-1]:0),(i>0?values[i-1]:0)-(i>1?values[i-2]:0)));
        stockData.SetOutputValues(()=>new Dictionary<string,List<double>>{{"Ravi",values}});stockData.SetSignals(signals);stockData.SetCustomValues(values);stockData.IndicatorName=IndicatorName.RangeActionVerificationIndex;return stockData;
    }


    /// <summary>
    /// Calculates the Really Simple Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateReallySimpleIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length = 21, int smoothLength = 10)
    {
        length = Math.Max(1, length); smoothLength = Math.Max(1, smoothLength);
        List<double> rsiList = new(stockData.Count), rsiMaList = new(stockData.Count); List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        using var window = new ReallySimpleWindow(maType, length, smoothLength);
        var custom = Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType);
        if (custom)
        {
            var maList = Builder.Compute.ComponentAverage.Take(inputList.ToArray(), length)?.ToList() ?? GetMovingAverageList(stockData, maType, length, inputList);
            for (var i = 0; i < stockData.Count; i++) rsiList.Add(window.Line(inputList[i], stockData.LowPrices[i], true, maList[i]).Publish());
            rsiMaList = Builder.Compute.ComponentAverage.Take(rsiList.ToArray(), smoothLength)?.ToList() ?? GetMovingAverageList(stockData, maType, smoothLength, rsiList);
        }
        else for (var i = 0; i < stockData.Count; i++) { var point = window.Next(inputList[i], stockData.LowPrices[i], true); rsiList.Add(point.Line); rsiMaList.Add(point.Signal); }
        for (var i = 0; i < stockData.Count; i++)
        {
            var rsi = rsiList[i];
            var prevRsiMa = i >= 1 ? rsiMaList[i - 1] : 0;
            var prevRsi = i >= 1 ? rsiList[i - 1] : 0;
            var rsiMa = rsiMaList[i];

            var signal = GetCompareSignal(rsi - rsiMa, prevRsi - prevRsiMa);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Rsi", rsiList },
            { "Signal", rsiMaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(rsiList);
        stockData.IndicatorName = IndicatorName.ReallySimpleIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Recursive Differenciator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="alpha"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateRecursiveDifferenciator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length = 14, double alpha = 0.6)
    {
        length = Math.Max(1, length);
        if (StrengthWindow.Supports(maType) && !Builder.Compute.ComponentAverage.HasOverrides)
        {
            var input = stockData.ChainedValues.Count > 0 ? stockData.ChainedValues : stockData.InputValues;
            using var window = new RecursiveDifferenciatorWindow(maType, length, alpha);
            var values = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
            double previous = 0, older = 0;
            for (var i = 0; i < input.Count; i++)
            {
                var point = window.Next(input[i], true); var prior = i == 0 ? 1 : previous;
                signals?.Add(GetCompareSignal(point.Change - prior, prior - older)); values.Add(point.Line);
                older = prior; previous = point.Change;
            }
            stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Rd", values } });
            stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.RecursiveDifferenciator;
            return stockData;
        }
        using var feedback = new RecursiveDifferenciatorWindow(maType, length, alpha);
        List<double> bList = new(stockData.Count);
        List<double> bChgList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var emaList = GetMovingAverageList(stockData, maType, length, inputList);
        stockData.SetCustomValues(emaList);
        var rsiList = CalculateRelativeStrengthIndex(stockData, length: length).ChainedValues;

        for (var i = 0; i < stockData.Count; i++)
        {
            var a = rsiList[i] / 100;
            var prevBChg1 = i >= 1 ? bChgList[i - 1] : a;
            var prevBChg2 = i >= 2 ? bChgList[i - 2] : 0;
            var point = feedback.Finish(rsiList[i], true);
            bList.Add(point.Line); bChgList.Add(point.Change);
            var bChg = point.Change;

            var signal = GetCompareSignal(bChg - prevBChg1, prevBChg1 - prevBChg2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Rd", bList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(bList);
        stockData.IndicatorName = IndicatorName.RecursiveDifferenciator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Regression Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateRegressionOscillator(this StockData stockData, int length = 63)
    {
        List<double> roscList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        using var regression = new ExactLinearFitWindow(length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var fit = regression.Next(currentValue, true);

            var prevRosc = GetLastOrDefault(roscList);
            var rosc = fit.PercentFitResidual(currentValue);
            roscList.Add(rosc);

            var signal = GetCompareSignal(rosc, prevRosc);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Rosc", roscList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(roscList);
        stockData.IndicatorName = IndicatorName.RegressionOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Relative Difference Of Squares Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateRelativeDifferenceOfSquaresOscillator(this StockData stockData, int length = 20)
    {
        List<double> rdosList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var aSumWindow = new RollingSum();
        var dSumWindow = new RollingSum();
        var nSumWindow = new RollingSum();

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            double a = currentValue > prevValue ? 1 : 0;
            aSumWindow.Add(a);

            double d = currentValue < prevValue ? 1 : 0;
            dSumWindow.Add(d);

            double n = currentValue == prevValue ? 1 : 0;
            nSumWindow.Add(n);

            var prevRdos = GetLastOrDefault(rdosList);
            var aSum = aSumWindow.Sum(length);
            var dSum = dSumWindow.Sum(length);
            var nSum = nSumWindow.Sum(length);
            var rdos = aSum > 0 || dSum > 0 || nSum > 0 ? (Pow(aSum, 2) - Pow(dSum, 2)) / Pow(aSum + nSum + dSum, 2) : 0;
            rdosList.Add(rdos);

            var signal = GetCompareSignal(rdos, prevRdos);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Rdos", rdosList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(rdosList);
        stockData.IndicatorName = IndicatorName.RelativeDifferenceOfSquaresOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Relative Spread Strength
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateRelativeSpreadStrength(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int fastLength = 10, int slowLength = 40, int length = 14, int smoothLength = 5)
    {
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        using var kernel = new Streaming.RelativeSpreadKernel(maType, fastLength, slowLength, length, smoothLength);
        var rssList = inputList.Select(value => kernel.Next(value, true)).ToList();
        for (var i = 0; i < stockData.Count; i++)
        {
            var rss = rssList[i];
            var prevRss1 = i >= 1 ? rssList[i - 1] : 0;
            var prevRss2 = i >= 2 ? rssList[i - 2] : 0;

            var signal = GetRsiSignal(rss - prevRss1, prevRss1 - prevRss2, rss, prevRss1, 80, 20);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Rss", rssList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(rssList);
        stockData.IndicatorName = IndicatorName.RelativeSpreadStrength;

        return stockData;
    }


    /// <summary>
    /// Calculates the Relative Strength 3D Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="marketData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="length4"></param>
    /// <param name="length5"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateRelativeStrength3DIndicator(this StockData stockData,
            StockData marketData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 4, int length2 = 7, int length3 = 10,
            int length4 = 15, int length5 = 30)
    {
        List<double> r1List = new(stockData.Count);
        List<double> rs3List = new(stockData.Count);
        List<double> rs2List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var (spInputList, _, _, _, _) = GetInputValuesList(marketData);
        var xSumWindow = new RollingSum();

        if (stockData.Count == marketData.Count)
        {
            for (var i = 0; i < stockData.Count; i++)
            {
                var currentValue = inputList[i];
                var currentSp = spInputList[i];

                var prevR1 = GetLastOrDefault(r1List);
                var r1 = currentSp != 0 ? currentValue / currentSp * 100 : prevR1;
                r1List.Add(r1);
            }

            var fastMaList = GetMovingAverageList(stockData, maType, length3, r1List);
            var medMaList = GetMovingAverageList(stockData, maType, length2, fastMaList);
            var slowMaList = GetMovingAverageList(stockData, maType, length4, fastMaList);
            var vSlowMaList = GetMovingAverageList(stockData, maType, length5, slowMaList);
            for (var i = 0; i < stockData.Count; i++)
            {
                var fastMa = fastMaList[i];
                var medMa = medMaList[i];
                var slowMa = slowMaList[i];
                var vSlowMa = vSlowMaList[i];
                double t1 = TechnicalRatingComparison.Compare(fastMa, medMa) >= 0 && TechnicalRatingComparison.Compare(medMa, slowMa) >= 0 && TechnicalRatingComparison.Compare(slowMa, vSlowMa) >= 0 ? 10 : 0;
                double t2 = TechnicalRatingComparison.Compare(fastMa, medMa) >= 0 && TechnicalRatingComparison.Compare(medMa, slowMa) >= 0 && TechnicalRatingComparison.Compare(slowMa, vSlowMa) < 0 ? 9 : 0;
                double t3 = TechnicalRatingComparison.Compare(fastMa, medMa) < 0 && TechnicalRatingComparison.Compare(medMa, slowMa) >= 0 && TechnicalRatingComparison.Compare(slowMa, vSlowMa) >= 0 ? 9 : 0;
                double t4 = TechnicalRatingComparison.Compare(fastMa, medMa) < 0 && TechnicalRatingComparison.Compare(medMa, slowMa) >= 0 && TechnicalRatingComparison.Compare(slowMa, vSlowMa) < 0 ? 5 : 0;

                var rs2 = t1 + t2 + t3 + t4;
                rs2List.Add(rs2);
            }

            var rs2MaList = GetMovingAverageList(stockData, maType, length1, rs2List);
            for (var i = 0; i < stockData.Count; i++)
            {
                var rs2 = rs2List[i];
                var rs2Ma = rs2MaList[i];
                var prevRs3_1 = i >= 1 ? rs3List[i - 1] : 0;
                var prevRs3_2 = i >= 2 ? rs3List[i - 1] : 0;

                double x = rs2 >= 5 ? 1 : 0;
                xSumWindow.Add(x);

                var rs3 = rs2 >= 5 || TechnicalRatingComparison.Compare(rs2, rs2Ma) > 0 ? xSumWindow.Sum(length4) / length4 * 100 : 0;
                rs3List.Add(rs3);

                var signal = GetCompareSignal(rs3 - prevRs3_1, prevRs3_1 - prevRs3_2);
                signalsList?.Add(signal);
            }
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Rs3d", rs3List }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(rs3List);
        stockData.IndicatorName = IndicatorName.RelativeStrength3DIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Relative Vigor Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateRelativeVigorIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 14)
    {
        length = Math.Max(1, length); var input = stockData.ChainedValues.Count > 0 ? stockData.ChainedValues : stockData.InputValues; var external = Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType); using var window = new RelativeVigorWindow(maType, length, external, input.Count); List<double> values = new(input.Count), signal = new(input.Count); var signals = CreateSignalsList(stockData);
        if (external)
        {
            List<double> numerator = new(input.Count), denominator = new(input.Count); for (var i = 0; i < input.Count; i++) { var legs = window.Generate(input[i], stockData.OpenPrices[i], stockData.HighPrices[i], stockData.LowPrices[i], true); numerator.Add(legs.Numerator.Publish()); denominator.Add(legs.Denominator.Publish()); }
            var n = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(numerator), length)?.ToList() ?? GetMovingAverageList(stockData, maType, length, numerator); var d = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(denominator), length)?.ToList() ?? GetMovingAverageList(stockData, maType, length, denominator);
            for (var i = 0; i < input.Count; i++) { var r = window.Finish(new RocBankValue(n[i]), new RocBankValue(d[i]), true); values.Add(r.Value); signal.Add(r.Signal); }
        }
        else for (var i = 0; i < input.Count; i++) { var r = window.Next(input[i], stockData.OpenPrices[i], stockData.HighPrices[i], stockData.LowPrices[i], true); values.Add(r.Value); signal.Add(r.Signal); }
        for (var i = 0; i < input.Count; i++) signals?.Add(GetCompareSignal(values[i] - signal[i], i > 0 ? values[i - 1] - signal[i - 1] : 0));
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Rvi", values }, { "Signal", signal } }); stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.RelativeVigorIndex; return stockData;
    }


    /// <summary>
    /// Calculates the Repulse
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateRepulse(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 5)
    {
        length = Math.Max(1, length); var powerPeriod = RepulseWindow.PowerPeriod(length);
        var (input, _, _, _, _) = GetInputValuesList(stockData); using var window = new RepulseWindow(maType, length);
        List<double> repulseList = new(stockData.Count), repulseEmaList;
        List<Signal>? signalsList = CreateSignalsList(stockData);
        if (Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType))
        {
            List<double> bullPowerList = new(stockData.Count), bearPowerList = new(stockData.Count);
            for (var i = 0; i < input.Count; i++) { var powers = window.Powers(stockData.OpenPrices[i], stockData.HighPrices[i], stockData.LowPrices[i], input[i], true); bullPowerList.Add(powers.Bull.Publish()); bearPowerList.Add(powers.Bear.Publish()); }
            var bull = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(bullPowerList), powerPeriod)?.ToList() ?? GetMovingAverageList(stockData, maType, powerPeriod, bullPowerList);
            var bear = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(bearPowerList), powerPeriod)?.ToList() ?? GetMovingAverageList(stockData, maType, powerPeriod, bearPowerList);
            for (var i = 0; i < input.Count; i++) repulseList.Add(bull[i] - bear[i]);
            repulseEmaList = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(repulseList), length)?.ToList() ?? GetMovingAverageList(stockData, maType, length, repulseList);
        }
        else
        {
            repulseEmaList = new(stockData.Count);
            for (var i = 0; i < input.Count; i++) { var value = window.Next(stockData.OpenPrices[i], stockData.HighPrices[i], stockData.LowPrices[i], input[i], true); repulseList.Add(value.Line); repulseEmaList.Add(value.Signal); }
        }
        for (var i = 0; i < stockData.Count; i++)
        {
            var repulse = repulseList[i];
            var prevRepulse = i >= 1 ? repulseList[i - 1] : 0;
            var repulseEma = repulseEmaList[i];
            var prevRepulseEma = i >= 1 ? repulseEmaList[i - 1] : 0;

            var signal = GetCompareSignal(repulse - repulseEma, prevRepulse - prevRepulseEma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Repulse", repulseList },
            { "Signal", repulseEmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(repulseList);
        stockData.IndicatorName = IndicatorName.Repulse;

        return stockData;
    }


    /// <summary>
    /// Calculates the Retrospective Candlestick Chart
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateRetrospectiveCandlestickChart(this StockData stockData, int length = 100)
    {
        List<double> values=new(stockData.Count);var signals=CreateSignalsList(stockData);var (input,_,_,_,_)=GetInputValuesList(stockData);
        var window=new RetrospectiveCandleWindow(length);
        for(var i=0;i<stockData.Count;i++)
        {
            var value=window.Next(stockData.OpenPrices[i],stockData.HighPrices[i],stockData.LowPrices[i],input[i],true);
            var previous=i>0?values[i-1]:0;var before=i>1?values[i-2]:0;signals?.Add(GetCompareSignal(value-previous,previous-before));values.Add(value);
        }
        stockData.SetOutputValues(()=>new Dictionary<string,List<double>>{{"Rcc",values}});stockData.SetSignals(signals);stockData.SetCustomValues(values);stockData.IndicatorName=IndicatorName.RetrospectiveCandlestickChart;return stockData;
    }


    /// <summary>
    /// Calculates the Rex Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateRexOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length = 14)
    {
        length = Math.Max(1, length); var input = stockData.ChainedValues.Count > 0 ? stockData.ChainedValues : stockData.InputValues; List<double> values = new(input.Count), signal = new(input.Count); var signals = CreateSignalsList(stockData);
        if (Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType))
        {
            var tvb = input.Select((v,i) => RexWindow.TrueValue(v, stockData.OpenPrices[i], stockData.HighPrices[i], stockData.LowPrices[i]).Publish()).ToList(); values = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(tvb), length)?.ToList() ?? GetMovingAverageList(stockData, maType, length, tvb); signal = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(values), length)?.ToList() ?? GetMovingAverageList(stockData, maType, length, values);
        }
        else { using var window = new RexWindow(maType, length, input.Count); for (var i = 0; i < input.Count; i++) { var r = window.Next(input[i], stockData.OpenPrices[i], stockData.HighPrices[i], stockData.LowPrices[i], true); values.Add(r.Value); signal.Add(r.Signal); } }
        for (var i = 0; i < input.Count; i++) signals?.Add(GetCompareSignal(values[i] - signal[i], i > 0 ? values[i - 1] - signal[i - 1] : 0));
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Ro", values }, { "Signal", signal } }); stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.RexOscillator; return stockData;
    }


    /// <summary>
    /// Calculates the Robust Weighting Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateRobustWeightingOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 200)
    {
        List<double> indexList = new(stockData.Count);
        List<double> corrList = new(stockData.Count);
        List<double> lList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var corrWindow = new RollingCorrelation();

        for (var i = 0; i < stockData.Count; i++)
        {
            double index = i;
            indexList.Add(index);

            var currentValue = inputList[i];
            corrWindow.Add(index, currentValue);
            var corr = corrWindow.R(length);
            corr = IsValueNullOrInfinity(corr) ? 0 : corr;
            corrList.Add((double)corr);
        }

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);
        var indexSmaList = GetMovingAverageList(stockData, maType, length, indexList);
        var stdDevList = GetStandardDeviationList(inputList, length);
        stockData.SetCustomValues(indexList);
        var indexStdDevList = GetStandardDeviationList(indexList, length);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var corr = corrList[i];
            var stdDev = stdDevList[i];
            var indexStdDev = indexStdDevList[i];
            var sma = smaList[i];
            var indexSma = indexSmaList[i];
            var a = indexStdDev != 0 ? corr * (stdDev / indexStdDev) : 0;
            var b = sma - (a * indexSma);

            var l = currentValue - a - (b * currentValue);
            lList.Add(l);
        }

        var lSmaList = GetMovingAverageList(stockData, maType, length, lList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var l = lSmaList[i];
            var prevL1 = i >= 1 ? lSmaList[i - 1] : 0;
            var prevL2 = i >= 2 ? lSmaList[i - 2] : 0;

            var signal = GetCompareSignal(l - prevL1, prevL1 - prevL2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Rwo", lSmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(lSmaList);
        stockData.IndicatorName = IndicatorName.RobustWeightingOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the RSING Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateRSINGIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 20)
    {
        List<double> rsingList = new(stockData.Count);
        List<double> upList = new(stockData.Count);
        List<double> dnList = new(stockData.Count);
        List<double> rangeList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, volumeList) = GetInputValuesList(stockData);

        var maList = GetMovingAverageList(stockData, maType, length, volumeList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var high = highList[i];
            var low = lowList[i];

            var range = high - low;
            rangeList.Add(range);
        }

        stockData.SetCustomValues(rangeList);
        var stdevList = GetStandardDeviationList(rangeList, length);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentVolume = volumeList[i];
            var ma = maList[i];
            var stdev = stdevList[i];
            var range = rangeList[i];
            var currentValue = inputList[i];
            var prevValue = i >= length ? inputList[i - length] : 0;
            var vwr = ma != 0 ? currentVolume / ma : 0;
            var blr = stdev != 0 ? range / stdev : 0;
            var isUp = currentValue > prevValue;
            var isDn = currentValue < prevValue;
            var isEq = currentValue == prevValue;

            var prevUpCount = GetLastOrDefault(upList);
            var upCount = isEq ? 0 : isUp ? (prevUpCount <= 0 ? 1 : prevUpCount + 1) : (prevUpCount >= 0 ? -1 : prevUpCount - 1);
            upList.Add(upCount);

            var prevDnCount = GetLastOrDefault(dnList);
            var dnCount = isEq ? 0 : isDn ? (prevDnCount <= 0 ? 1 : prevDnCount + 1) : (prevDnCount >= 0 ? -1 : prevDnCount - 1);
            dnList.Add(dnCount);

            var pmo = MinPastValues(i, length, currentValue - prevValue);
            var rsing = vwr * blr * pmo;
            rsingList.Add(rsing);
        }

        var rsingMaList = GetMovingAverageList(stockData, maType, length, rsingList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var rsing = rsingMaList[i];
            var prevRsing1 = i >= 1 ? rsingMaList[i - 1] : 0;
            var prevRsing2 = i >= 2 ? rsingMaList[i - 2] : 0;

            var signal = GetCompareSignal(rsing - prevRsing1, prevRsing1 - prevRsing2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Rsing", rsingList },
            { "Signal", rsingMaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(rsingList);
        stockData.IndicatorName = IndicatorName.RSINGIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the RSMK Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="marketData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateRSMKIndicator(this StockData stockData, StockData marketData, 
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 90, int smoothLength = 3)
    {
        if (stockData.Count != marketData.Count)
            throw new ArgumentException("Primary and benchmark batches must have equal counts.", nameof(marketData));
        length = Math.Max(1, length);
        smoothLength = Math.Max(1, smoothLength);
        List<double> rsmkList = new(stockData.Count);
        List<double> logRatioList = new(stockData.Count);
        List<double> logDiffList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var (spInputList, _, _, _, _) = GetInputValuesList(marketData);

        for (var i = 0; i < stockData.Count; i++)
        {
            if (stockData.Dates[i] != marketData.Dates[i] || stockData.Dates[i].Kind != marketData.Dates[i].Kind
                || i > 0 && (stockData.Dates[i] <= stockData.Dates[i - 1] || stockData.Dates[i].Kind != stockData.Dates[i - 1].Kind))
                throw new ArgumentException("RSMK requires aligned, strictly increasing timestamps with a consistent kind.", nameof(marketData));
            if (!(inputList[i] > 0) || double.IsInfinity(inputList[i]) || !(spInputList[i] > 0) || double.IsInfinity(spInputList[i]))
                throw new ArgumentOutOfRangeException(nameof(stockData), "RSMK requires positive finite primary and benchmark inputs.");
        }

        if (stockData.Count == marketData.Count)
        {
            for (var i = 0; i < stockData.Count; i++)
            {
                var currentValue = inputList[i];
                var spValue = spInputList[i];
                var prevLogRatio = i >= length ? logRatioList[i - length] : 0;

                var logRatio = Math.Log(currentValue)-Math.Log(spValue);
                logRatioList.Add(logRatio);

                var logDiff = i < length ? 0 : logRatio - prevLogRatio;
                logDiffList.Add(logDiff);
            }

            var logDiffEmaList = GetMovingAverageList(stockData, maType, smoothLength, logDiffList);
            for (var i = 0; i < stockData.Count; i++)
            {
                var logDiffEma = logDiffEmaList[i];

                var prevRsmk = GetLastOrDefault(rsmkList);
                var rsmk = logDiffEma * 100;
                rsmkList.Add(rsmk);

                var signal = GetCompareSignal(rsmk, prevRsmk);
                signalsList?.Add(signal);
            }
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Rsmk", rsmkList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(rsmkList);
        stockData.IndicatorName = IndicatorName.RSMKIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Running Equity
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateRunningEquity(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 100)
    {
        length = Math.Max(1, length); List<double> reqList = new(stockData.Count); List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData); using var window = new RunningEquityWindow(maType, length);
        if (Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType))
        {
            var smaList = Builder.Compute.ComponentAverage.Take(inputList.ToArray(), length)?.ToList() ?? GetMovingAverageList(stockData, maType, length, inputList);
            for (var i = 0; i < stockData.Count; i++) reqList.Add(window.WithAverage(inputList[i], smaList[i], true));
        }
        else foreach (var price in inputList) reqList.Add(window.Next(price, true));
        for (var i = 0; i < stockData.Count; i++) signalsList?.Add(GetCompareSignal(reqList[i], i == 0 ? 0 : reqList[i - 1]));

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Req", reqList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(reqList);
        stockData.IndicatorName = IndicatorName.RunningEquity;

        return stockData;
    }


    /// <summary>
    /// Calculates the Mass Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateMassIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 21, int length2 = 21, int length3 = 25, int signalLength = 9)
    {
        length1=Math.Max(1,length1);length2=Math.Max(1,length2);length3=Math.Max(1,length3);signalLength=Math.Max(1,signalLength);
        List<double> values=new(stockData.Count),signalValues=new(stockData.Count);var signals=CreateSignalsList(stockData);
        if(Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType))
        {
            var ranges=Enumerable.Range(0,stockData.Count).Select(i=>MassIndexWindow.Range(stockData.HighPrices[i],stockData.LowPrices[i]).Publish()).ToList();
            List<double> Average(List<double> input,int period)=>Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(input),period)?.ToList()??GetMovingAverageList(stockData,maType,period,input);
            var first=Average(ranges,length1);var second=Average(first,length2);var sum=new MassIndexSum(length3);
            for(var i=0;i<stockData.Count;i++)values.Add(sum.Next(new(first[i]),new(second[i]),true).Publish());
            signalValues=Average(values,signalLength);
        }
        else
        {
            using var window=new MassIndexWindow(maType,length1,length2,length3,signalLength,stockData.Count);
            for(var i=0;i<stockData.Count;i++){var result=window.Next(stockData.HighPrices[i],stockData.LowPrices[i],true);values.Add(result.Value);signalValues.Add(result.Signal);}
        }
        for(var i=0;i<stockData.Count;i++)signals?.Add(GetCompareSignal(values[i]-signalValues[i],i>0?values[i-1]-signalValues[i-1]:0));
        stockData.SetOutputValues(()=>new Dictionary<string,List<double>>{{"Mi",values},{"Signal",signalValues}});stockData.SetSignals(signals);stockData.SetCustomValues(values);stockData.IndicatorName=IndicatorName.MassIndex;return stockData;
    }


    /// <summary>
    /// Calculates the Mass Thrust Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateMassThrustOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length = 14)
    {
        var (input, _, _, _, volume) = GetInputValuesList(stockData);
        var result = MassThrustWindow.Calculate(input, volume, true, maType, length);
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Mto", result.Values }, { "Signal", result.SignalLine } });
        stockData.SetSignals(CreateSignalsList(stockData) is null ? null : result.Trades);
        stockData.SetCustomValues(result.Values);
        stockData.IndicatorName = IndicatorName.MassThrustOscillator;
        return stockData;
    }


    /// <summary>
    /// Calculates the Midpoint Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateMidpointOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 26, int signalLength = 9)
    {
        List<double> moList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var hh = highestList[i];
            var ll = lowestList[i];

            var mo = RoundedMidpointOscillator.Of(currentValue, hh, ll);
            moList.Add(mo);
        }

        var moEmaList = GetMovingAverageList(stockData, maType, signalLength, moList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var mo = moList[i];
            var moEma = moEmaList[i];
            var prevMo = i >= 1 ? moList[i - 1] : 0;
            var prevMoEma = i >= 1 ? moEmaList[i - 1] : 0;

            var signal = GetRsiSignal(mo - moEma, prevMo - prevMoEma, mo, prevMo, 70, -70);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Mo", moList },
            { "Signal", moEmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(moList);
        stockData.IndicatorName = IndicatorName.MidpointOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Morphed Sine Wave
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="power"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateMorphedSineWave(this StockData stockData, int length = 14, double power = 100)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        var window = new MorphedSineWindow(length, power); var values = new List<double>(stockData.Count);
        var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++) { var point = window.Next(input[i], true); values.Add(point.Value); signals?.Add(point.Trade); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Msw", values } });
        stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.MorphedSineWave;
        return stockData;
    }


    /// <summary>
    /// Calculates the Move Tracker
    /// </summary>
    /// <param name="stockData"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateMoveTracker(this StockData stockData)
    {
        List<double> mtList = new(stockData.Count);
        List<double> mtSignalList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevMt = GetLastOrDefault(mtList);
            var mt = MinPastValues(i, 1, currentValue - prevValue);
            mtList.Add(mt);

            var prevMtSignal = GetLastOrDefault(mtSignalList);
            var mtSignal = mt - prevMt;
            mtSignalList.Add(mtSignal);

            var signal = GetCompareSignal(mt - mtSignal, prevMt - prevMtSignal);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Mt", mtList },
            { "Signal", mtSignalList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(mtList);
        stockData.IndicatorName = IndicatorName.MoveTracker;

        return stockData;
    }


    /// <summary>
    /// Calculates the Multi Level Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="factor"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateMultiLevelIndicator(this StockData stockData, int length = 14, double factor = 10000)
    {
        List<double> zList = new(stockData.Count); List<Signal>? signalsList = CreateSignalsList(stockData); var window = new MultiLevelWindow(length, factor);
        for (var i = 0; i < stockData.Count; i++)
        {
            var previous = i == 0 ? 0 : zList[i - 1]; var older = i < 2 ? 0 : zList[i - 2];
            var value = window.Next(stockData.OpenPrices[i], true); zList.Add(value);
            signalsList?.Add(GetRsiSignal(value - previous, previous - older, value, previous, 5, -5));
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Mli", zList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(zList);
        stockData.IndicatorName = IndicatorName.MultiLevelIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Modified Gann Hilo Activator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="mult"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateModifiedGannHiloActivator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 50, double mult = 1)
    {
        var (input, high, low, open, _) = GetInputValuesList(stockData);
        var result = ModifiedGannWindow.Calculate(stockData, input, high, low, open, maType, length, mult, false);
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Ghla", result.Values } });
        stockData.SetSignals(CreateSignalsList(stockData) is null ? null : result.Trades);
        stockData.SetCustomValues(result.Values); stockData.IndicatorName = IndicatorName.ModifiedGannHiloActivator;
        return stockData;
    }


    /// <summary>
    /// Calculates the Market Direction Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateMarketDirectionIndicator(this StockData stockData, int fastLength = 13, int slowLength = 55)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        var result = MarketDirectionWindow.Calculate(input, fastLength, slowLength); var line = result.Values.ToList();
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Mdi", line } });
        stockData.SetSignals(result.Trades.ToList()); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.MarketDirectionIndicator;
        return stockData;
    }


    /// <summary>
    /// Calculates the Mobility Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateMobilityOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length1 = 10, int length2 = 14, int signalLength = 7)
    {
        List<double> moList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        length1 = Math.Max(1, length1); length2 = Math.Max(1, length2);
        var masses = new double[length1];
        for (var i = 0; i < stockData.Count; i++)
        {
            var countAvailable = Math.Min(length2, i+1);
            var maximum = highList[i - 0]; var minimum = lowList[i - 0];
            for (var k = 1; k < countAvailable; k++)
            {
                maximum = Math.Max(maximum, highList[i - k]);
                minimum = Math.Min(minimum, lowList[i - k]);
            }
            var width = (maximum-minimum)/length1;
            var rawValue = 0d;
            if (i >= length2 && width > 0)
            {
                var comparison = inputList[i-length2];
                var mode = 0; var largestMass = -1d; var priceMass = 0d;
                for (var bin = 0; bin < length1; bin++)
                {
                    var lower = minimum+bin*width;
                    var upper = bin+1 == length1 ? maximum : minimum+(bin+1)*width;
                    double mass = 0;
                    for (var k = 0; k < countAvailable; k++)
                    {
                        var h = highList[i - k]; var l = lowList[i - k];
                        mass += h == l ? (l >= lower && (l < upper || bin+1 == length1) ? 1 : 0) // NOSONAR: S1244 - Equal candle bounds are a point mass, not a narrow interval.
                            : Math.Max(0, Math.Min(h, upper)-Math.Max(l, lower))/(h-l);
                    }
                    masses[bin] = mass; largestMass = Math.Max(largestMass, mass);
                    if (comparison >= lower && (comparison < upper || bin+1 == length1 && comparison <= upper)) priceMass = mass;
                }
                // Choose the first bin tied with the global maximum.
                while (mode+1 < length1 && largestMass-masses[mode] > 1e-12*countAvailable) mode++;
                largestMass = masses[mode];
                var modePrice = minimum+(mode+0.5)*width;
                if (largestMass > 0)
                    rawValue = (comparison < modePrice ? 1 : -1)*100*Math.Max(0, 1-priceMass/largestMass);
            }
            moList.Add(rawValue);
        }

        var moWmaList = GetMovingAverageList(stockData, maType, signalLength, moList);
        var moSigList = GetMovingAverageList(stockData, maType, signalLength, moWmaList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var mo = moWmaList[i];
            var moSig = moSigList[i];
            var prevMo = i >= 1 ? moWmaList[i - 1] : 0;
            var prevMoSig = i >= 1 ? moSigList[i - 1] : 0;

            var signal = GetCompareSignal(mo - moSig, prevMo - prevMoSig);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Mo", moWmaList },
            { "Signal", moSigList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(moWmaList);
        stockData.IndicatorName = IndicatorName.MobilityOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Mass Thrust Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateMassThrustIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 14)
    {
        var (input, _, _, _, volume) = GetInputValuesList(stockData);
        var result = MassThrustWindow.Calculate(input, volume, false, maType, length);
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Mti", result.Values }, { "Signal", result.SignalLine } });
        stockData.SetSignals(CreateSignalsList(stockData) is null ? null : result.Trades);
        stockData.SetCustomValues(result.Values);
        stockData.IndicatorName = IndicatorName.MassThrustIndicator;
        return stockData;
    }

}

