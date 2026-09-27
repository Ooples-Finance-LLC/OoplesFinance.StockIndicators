using OoplesFinance.StockIndicators.Compatibility;
using OoplesFinance.StockIndicators.Core;

namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Average Day Range.
    /// </summary>
    /// <remarks>
    /// The simple average of the last <paramref name="length"/> daily ranges, each the bar's high less its low.
    /// It publishes zero until the window fills, as the simple moving average it is built on does.
    /// </remarks>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateAverageDayRange(this StockData stockData, int length = 14)
    {
        length = Math.Max(length, 1);
        var (_, highList, lowList, _, _) = GetInputValuesList(stockData);
        var count = highList.Count;
        List<double> adrList = new(count);
        List<Signal>? signalsList = CreateSignalsList(stockData, count);

        var values = SpanCompat.CreateOutputBuffer(count);
        VolatilityCore.AverageDayRange(SpanCompat.AsReadOnlySpan(highList), SpanCompat.AsReadOnlySpan(lowList), values.Span, length);
        for (var i = 0; i < count; i++)
        {
            var adr = values.Span[i];
            adrList.Add(adr);

            var prevAdr1 = i >= 1 ? adrList[i - 1] : 0;
            var prevAdr2 = i >= 2 ? adrList[i - 2] : 0;
            var signal = GetCompareSignal(adr - prevAdr1, prevAdr1 - prevAdr2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Adr", adrList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(adrList);
        stockData.IndicatorName = IndicatorName.AverageDayRange;

        return stockData;
    }

    /// <summary>
    /// Calculates the Coefficient of Variation.
    /// </summary>
    /// <remarks>
    /// The window's standard deviation as a percentage of its own mean, which is how a spread is compared
    /// between series of different size. A window whose mean is zero has no such percentage, and publishes
    /// zero, as does a window shorter than <paramref name="length"/>.
    /// </remarks>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateCoefficientOfVariation(this StockData stockData, int length = 20)
    {
        length = Math.Max(length, 1);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var count = inputList.Count;
        List<double> cvList = new(count);
        List<Signal>? signalsList = CreateSignalsList(stockData, count);

        using var window = new ExactCoefficientWindow(length);
        for (var i = 0; i < count; i++)
        {
            var cv = window.Next(inputList[i], true);

            cvList.Add(cv);

            var prevCv1 = i >= 1 ? cvList[i - 1] : 0;
            var prevCv2 = i >= 2 ? cvList[i - 2] : 0;
            var signal = GetCompareSignal(cv - prevCv1, prevCv1 - prevCv2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Cv", cvList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(cvList);
        stockData.IndicatorName = IndicatorName.CoefficientOfVariation;

        return stockData;
    }

    /// <summary>
    /// Calculates the Downside Deviation.
    /// </summary>
    /// <remarks>
    /// The deviation of only those returns that fell short of <paramref name="targetReturn"/>, which is what
    /// a downside measure such as the Sortino ratio divides by: returns above the target are not risk. The
    /// root mean square is taken over the shortfalls themselves, not over the whole window, so a window with
    /// no shortfall in it publishes zero rather than a small number.
    /// </remarks>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="targetReturn"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateDownsideDeviation(this StockData stockData, int length = 20, double targetReturn = 0)
    {
        length = Math.Max(length, 1);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var count = inputList.Count;
        List<double> ddList = new(count);
        List<Signal>? signalsList = CreateSignalsList(stockData, count);

        using var window = new ExactDownsideWindow(length, targetReturn);
        for (var i = 0; i < count; i++)
        {
            var downsideDeviation = window.Next(inputList[i], true);

            ddList.Add(downsideDeviation);

            var prevDd1 = i >= 1 ? ddList[i - 1] : 0;
            var prevDd2 = i >= 2 ? ddList[i - 2] : 0;
            var signal = GetCompareSignal(downsideDeviation - prevDd1, prevDd1 - prevDd2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Dd", ddList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(ddList);
        stockData.IndicatorName = IndicatorName.DownsideDeviation;

        return stockData;
    }

    /// <summary>
    /// Calculates the Close to Close Volatility.
    /// </summary>
    /// <remarks>
    /// The annualised deviation of the series' logarithmic returns: the plainest volatility estimate there
    /// is, using only the close of each bar and none of its range. Annualised by the root of 252, the usual
    /// count of trading days in a year, so the reading is comparable with a quoted annual volatility. A
    /// window shorter than <paramref name="length"/> publishes zero.
    /// </remarks>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateCloseToCloseVolatility(this StockData stockData, int length = 20)
    {
        length = Math.Max(length, 1);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var count = inputList.Count;
        List<double> volatilityList = new(count);
        List<Signal>? signalsList = CreateSignalsList(stockData, count);
        var annualisationFactor = Sqrt(252);

        var returns = new double[count];
        for (var i = 1; i < count; i++)
        {
            var prevValue = inputList[i - 1];
            returns[i] = prevValue != 0 ? StableLogRatio.OfSameSign(inputList[i], prevValue) : 0;
        }

        for (var i = 0; i < count; i++)
        {
            double volatility = 0;
            if (i >= length - 1)
            {
                double sum = 0;
                for (var j = i - length + 1; j <= i; j++)
                {
                    sum += returns[j];
                }

                var mean = sum / length;
                double variance = 0;
                for (var j = i - length + 1; j <= i; j++)
                {
                    var diff = returns[j] - mean;
                    variance += diff * diff;
                }

                volatility = Sqrt(variance / length) * annualisationFactor;
            }

            volatilityList.Add(volatility);

            var prevVolatility1 = i >= 1 ? volatilityList[i - 1] : 0;
            var prevVolatility2 = i >= 2 ? volatilityList[i - 2] : 0;
            var signal = GetCompareSignal(volatility - prevVolatility1, prevVolatility1 - prevVolatility2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ctcv", volatilityList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(volatilityList);
        stockData.IndicatorName = IndicatorName.CloseToCloseVolatility;

        return stockData;
    }

    /// <summary>
    /// Calculates the Atr Channel Width.
    /// </summary>
    /// <remarks>
    /// How wide a channel drawn a multiple of the average true range either side of the price would be: twice
    /// the multiple, times the range. It is the width alone, so it says how much room the channel gives
    /// without saying where the channel sits. The average is the one
    /// <see cref="CalculateAverageTrueRange"/> takes, so the two agree bar for bar.
    /// </remarks>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="multiplier"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateAtrChannelWidth(this StockData stockData, int length = 14, double multiplier = 2)
    {
        var (input, high, low, _, _) = GetInputValuesList(stockData);
        using var window = new AtrDerivedWindow(length, Math.Max(1, input.Count));
        List<double> values = new(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            var atr = window.Next(high[i], low[i], input[i], true); var value = AtrDerivedWindow.Width(atr, multiplier);
            signals?.Add(GetCompareSignal(value - (i > 0 ? values[i - 1] : 0), (i > 0 ? values[i - 1] : 0) - (i > 1 ? values[i - 2] : 0))); values.Add(value);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Acw", values } }); stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.AtrChannelWidth; return stockData;
    }

    /// <summary>
    /// Calculates the choppiness index.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateChoppinessIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 14)
    {
        length = Math.Max(2, length); var (input, _, _, _, _) = GetInputValuesList(stockData);
        var emaList = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(input), length)?.ToList() ?? GetMovingAverageList(stockData, maType, length, input);
        var window = new ChoppinessWindow(length); var line = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            var value = window.Next(stockData.HighPrices[i], stockData.LowPrices[i], input[i], true);
            signals?.Add(GetVolatilitySignal(input[i] - emaList[i], (i > 0 ? input[i - 1] : input[i]) - (i > 0 ? emaList[i - 1] : 0), value, 38.2)); line.Add(value);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Ci", line } }); stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.ChoppinessIndex; return stockData;
    }


    /// <summary>
    /// Calculates the Closed Form Distance Volatility
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateClosedFormDistanceVolatility(this StockData stockData,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14)
    {
        List<double> tempHighList = new(stockData.Count);
        List<double> tempLowList = new(stockData.Count);
        List<double> hvList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum highSumWindow = new();
        RollingSum lowSumWindow = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        var emaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var ema = emaList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevEma = i >= 1 ? emaList[i - 1] : 0;

            var currentHigh = highList[i];
            tempHighList.Add(currentHigh);
            highSumWindow.Add(currentHigh);

            var currentLow = lowList[i];
            tempLowList.Add(currentLow);
            lowSumWindow.Add(currentLow);

            var a = highSumWindow.Sum(length);
            var b = lowSumWindow.Sum(length);
            var abAvg = (a + b) / 2;

            var prevHv = GetLastOrDefault(hvList);
            var hv = abAvg != 0 && a != b ? Sqrt(1 - (Pow(a, 0.25) * Pow(b, 0.25) / Pow(abAvg, 0.5))) : 0;
            hvList.Add(hv);

            var signal = GetVolatilitySignal(currentValue - ema, prevValue - prevEma, hv, prevHv);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Cfdv", hvList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(hvList);
        stockData.IndicatorName = IndicatorName.ClosedFormDistanceVolatility;

        return stockData;
    }


    /// <summary>
    /// Calculates the Donchian Channel Width
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateDonchianChannelWidth(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 20,
        int smoothLength = 22)
    {
        var (input, high, low, _, _) = GetInputValuesList(stockData); var external = Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType);
        List<double>? centerOverride = external ? Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(input), Math.Max(1, length))?.ToList() ?? GetMovingAverageList(stockData, maType, length, input) : null;
        using var window = new DonchianWidthWindow(maType, length, smoothLength, external, Math.Max(1, input.Count));
        List<double> width = new(input.Count), signal = new(input.Count), center = new(input.Count);
        for (var i = 0; i < input.Count; i++) { var point = window.Next(high[i], low[i], input[i], true); width.Add(point.Width); signal.Add(point.Signal); center.Add(point.Center); }
        if (external) { center = centerOverride!; signal = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(width), Math.Max(1, smoothLength))?.ToList() ?? GetMovingAverageList(stockData, maType, smoothLength, width); }
        var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++) signals?.Add(GetVolatilitySignal(input[i] - center[i], i > 0 ? input[i - 1] - center[i - 1] : 0, width[i], signal[i]));
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Dcw", width }, { "Signal", signal } });
        stockData.SetSignals(signals); stockData.SetCustomValues(width); stockData.IndicatorName = IndicatorName.DonchianChannelWidth; return stockData;
    }

}

