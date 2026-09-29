
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Half Trend
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="atrLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateHalfTrend(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 2,
        int atrLength = 100)
    {
        var (input, high, low, _, _) = GetInputValuesList(stockData); var external = Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType);
        using var window = new HalfTrendWindow(maType, length, atrLength, external); List<double>? atr = null, highMean = null, lowMean = null;
        if (external)
        {
            var caller = stockData.CaptureInputSeries(); atr = CalculateAverageTrueRange(stockData, maType, Math.Max(1, atrLength)).CustomValuesList.ToList();
            List<double> Average(List<double> values) => Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(values), Math.Max(1, length))?.ToList() ?? GetMovingAverageList(stockData, maType, Math.Max(1, length), values);
            highMean = Average(high); lowMean = Average(low); stockData.RestoreInputSeries(caller);
        }
        var values = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++) { var point = window.Next(high[i], low[i], input[i], true, atr?[i], highMean?[i], lowMean?[i]); values.Add(point.Value); signals?.Add(point.Signal); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Ht", values } }); stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.HalfTrend; return stockData;
    }


    /// <summary>
    /// Calculates the Kase Dev Stop V1
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="length"></param>
    /// <param name="stdDev1"></param>
    /// <param name="stdDev2"></param>
    /// <param name="stdDev3"></param>
    /// <param name="stdDev4"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateKaseDevStopV1(this StockData stockData,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int fastLength = 5, int slowLength = 21, int length = 20, double stdDev1 = 0,
        double stdDev2 = 1, double stdDev3 = 2.2, double stdDev4 = 3.6)
    {
        var (input, high, low, close) = KaseStopV1Window.Inputs(stockData); var external = Builder.Compute.ComponentAverage.HasOverrides; var legacy = !StrengthWindow.Supports(maType); using var window = new KaseStopV1Window(maType, fastLength, slowLength, length, stdDev1, stdDev2, stdDev3, stdDev4, external);
        List<double>? mean = null, slow = null, fast = null;
        if (external || legacy) { var caller = stockData.CaptureInputSeries(); List<double> Average(List<double> values, int period) => Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(values), Math.Max(1, period))?.ToList() ?? GetMovingAverageList(stockData, maType, Math.Max(1, period), values); mean = Average(KaseStopV1Window.PublishedRanges(high, low, close).ToList(), length); if (external) { slow = Average(input, slowLength); fast = Average(input, fastLength); } stockData.RestoreInputSeries(caller); }
        var first = new List<double>(input.Count); var second = new List<double>(input.Count); var third = new List<double>(input.Count); var warning = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++) { var point = window.Next(high[i], low[i], close[i], input[i], true, mean?[i], slow?[i], fast?[i]); first.Add(point.Dev1); second.Add(point.Dev2); third.Add(point.Dev3); warning.Add(point.WarningLine); signals?.Add(point.Signal); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Dev1", first }, { "Dev2", second }, { "Dev3", third }, { "WarningLine", warning } }); stockData.SetSignals(signals); stockData.SetCustomValues(new List<double>()); stockData.IndicatorName = IndicatorName.KaseDevStopV1; return stockData;
    }


    /// <summary>
    /// Calculates the Kase Dev Stop V2
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="length"></param>
    /// <param name="stdDev1"></param>
    /// <param name="stdDev2"></param>
    /// <param name="stdDev3"></param>
    /// <param name="stdDev4"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateKaseDevStopV2(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int fastLength = 10, int slowLength = 21, int length = 20, double stdDev1 = 0, double stdDev2 = 1, double stdDev3 = 2.2,
        double stdDev4 = 3.6)
    {
        var (input, high, low, _, _) = GetInputValuesList(stockData); var external = Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType); using var window = new KaseStopV2Window(maType, fastLength, slowLength, length, stdDev1, stdDev2, stdDev3, stdDev4, external);
        List<double>? fast = null, slow = null, mean = null;
        if (external) { var caller = stockData.CaptureInputSeries(); List<double> Average(List<double> values, int period) => Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(values), Math.Max(1, period))?.ToList() ?? GetMovingAverageList(stockData, maType, Math.Max(1, period), values); fast = Average(input, fastLength); slow = Average(input, slowLength); mean = Average(KaseStopV2Window.PublishedRanges(high, low, input).ToList(), length); stockData.RestoreInputSeries(caller); }
        var first = new List<double>(input.Count); var second = new List<double>(input.Count); var third = new List<double>(input.Count); var fourth = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++) { var point = window.Next(high[i], low[i], input[i], true, external ? fast![i] : null, external ? slow![i] : null, external ? mean![i] : null); first.Add(point.Dev1); second.Add(point.Dev2); third.Add(point.Dev3); fourth.Add(point.Dev4); signals?.Add(point.Signal); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Dev1", first }, { "Dev2", second }, { "Dev3", third }, { "Dev4", fourth } }); stockData.SetSignals(signals); stockData.SetCustomValues(new List<double>()); stockData.IndicatorName = IndicatorName.KaseDevStopV2; return stockData;
    }


    /// <summary>
    /// Calculates the Elder Safe Zone Stops
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="factor"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateElderSafeZoneStops(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 63, int length2 = 22, int length3 = 3, double factor = 2.5)
    {
        var (input, high, low, _, _) = GetInputValuesList(stockData); var external = Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType); using var window = new ElderSafeZoneWindow(maType, length1, length2, length3, factor, external); List<double>? trend = null;
        if (external) { var caller = stockData.CaptureInputSeries(); trend = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(input), Math.Max(1, length1))?.ToList() ?? GetMovingAverageList(stockData, maType, Math.Max(1, length1), input); stockData.RestoreInputSeries(caller); }
        var values = new List<double>(input.Count); var signals = CreateSignalsList(stockData); for (var i = 0; i < input.Count; i++) { var point = window.Next(high[i], low[i], input[i], true, external ? trend![i] : null); values.Add(point.Value); signals?.Add(point.Signal); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Eszs", values } }); stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.ElderSafeZoneStops; return stockData;
    }
}

