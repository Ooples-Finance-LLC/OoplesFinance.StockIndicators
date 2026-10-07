
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the adaptive trailing stop.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <param name="factor">The factor.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateAdaptiveTrailingStop(this StockData stockData, int length = 100, double factor = 3)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        using var window = new PoweredKaufmanWindow(length, factor);
        List<double> line = new(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            var value = window.Next(input[i], true).Stop; line.Add(value);
            signals?.Add(GetCompareSignal(input[i] - value, (i > 0 ? input[i - 1] : 0) - (i > 0 ? line[i - 1] : 0)));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Ts", line } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.AdaptiveTrailingStop;
        return stockData;
    }


    /// <summary>
    /// Calculates the adaptive autonomous recursive trailing stop.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <param name="gamma">The gamma.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateAdaptiveAutonomousRecursiveTrailingStop(this StockData stockData, int length = 14, double gamma = 3)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        using var window = new AdaptiveAutonomousWindow(length, gamma);
        List<double> line = new(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            var value = window.Next(input[i], true).Stop; line.Add(value);
            signals?.Add(GetCompareSignal(input[i] - value, (i > 0 ? input[i - 1] : 0) - (i > 0 ? line[i - 1] : 0)));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Ts", line } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.AdaptiveAutonomousRecursiveTrailingStop;
        return stockData;
    }


    /// <summary>
    /// Calculates the Chandelier Exit
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <param name="mult">The mult.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateChandelierExit(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int length = 22, 
        double mult = 3)
    {
        var (input, high, low, _, _) = GetInputValuesList(stockData); var external = Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType);
        using var window = new ChandelierWindow(maType, length, mult, external);
        List<double>? atr = null;
        if (external) { var ranges = GetTrueRangeList(stockData); atr = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(ranges), Math.Max(1, length))?.ToList() ?? GetMovingAverageList(stockData, maType, length, ranges); }
        var longs = new List<double>(input.Count); var shorts = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++) { var point = window.Next(high[i], low[i], input[i], true, external ? atr![i] : null); longs.Add(point.Long); shorts.Add(point.Short); signals?.Add(point.Signal); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "ExitLong", longs }, { "ExitShort", shorts } }); stockData.SetSignals(signals); stockData.SetCustomValues(new List<double>()); stockData.IndicatorName = IndicatorName.ChandelierExit; return stockData;
    }


    /// <summary>
    /// Calculates the Average True Range Trailing Stops
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length1">The length1.</param>
    /// <param name="length2">The length2.</param>
    /// <param name="factor">The factor.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateAverageTrueRangeTrailingStops(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length1 = 63, int length2 = 21, double factor = 3)
    {
        var (input, high, low, _, _) = GetInputValuesList(stockData); var external = Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType);
        List<double>? atr = null, trend = null;
        if (external)
        {
            var ranges = GetTrueRangeList(stockData);
            atr = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(ranges), Math.Max(1, length2))?.ToList() ?? GetMovingAverageList(stockData, maType, length2, ranges);
            trend = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(input), Math.Max(1, length1))?.ToList() ?? GetMovingAverageList(stockData, maType, length1, input);
        }
        using var window = new AtrTrailingWindow(maType, length1, length2, factor, external, Math.Max(1, input.Count));
        List<double> values = new(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            var stop = window.Next(high[i], low[i], input[i], true, external ? new RocBankValue(trend![i]) : null, external ? new RocBankValue(atr![i]) : null);
            signals?.Add(GetCompareSignal(input[i] - stop, (i > 0 ? input[i - 1] : 0) - (i > 0 ? values[i - 1] : input[i]))); values.Add(stop);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Atrts", values } }); stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.AverageTrueRangeTrailingStops; return stockData;
    }

}

