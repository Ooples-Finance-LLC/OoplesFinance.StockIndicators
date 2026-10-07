using OoplesFinance.StockIndicators.Compatibility;

namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the index of the force.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateForceIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14)
    {
        List<double> output = new(stockData.Count);
        List<Signal>? signals = CreateSignalsList(stockData);
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        using var window = new ForceWindow(maType, length);
        for (var i = 0; i < stockData.Count; i++)
        {
            var value = window.Next(input[i], stockData.Volumes[i], true);
            var previous = i == 0 ? 0 : output[i - 1];
            signals?.Add(GetCompareSignal(value - previous, previous - (i < 2 ? 0 : output[i - 2]))); output.Add(value);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Fi", output } });
        stockData.SetSignals(signals); stockData.SetCustomValues(output);
        stockData.IndicatorName = IndicatorName.ForceIndex;
        return stockData;
    }


    /// <summary>
    /// Calculates the Klinger Volume Oscillator
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="fastLength">Length of the fast.</param>
    /// <param name="slowLength">Length of the slow.</param>
    /// <param name="signalLength">Length of the signal.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateKlingerVolumeOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int fastLength = 34, int slowLength = 55, int signalLength = 13)
    {
        var result = KlingerWindow.Calculate(stockData, maType, fastLength, slowLength, signalLength, true);
        var line = result.Line.ToList();
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Kvo", line },
            { "KvoSignal", result.SignalLine.ToList() }, { "KvoHistogram", result.Histogram.ToList() } });
        stockData.SetSignals(CreateSignalsList(stockData) is null ? null : result.Trades.ToList());
        stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.KlingerVolumeOscillator;
        return stockData;
    }


    /// <summary>
    /// Calculates the Ease Of Movement
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="divisor"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEaseOfMovement(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14,
        double divisor = 1000000)
    {
        List<double> output = new(stockData.Count), signal = new(stockData.Count);
        List<Signal>? signals = CreateSignalsList(stockData);
        var window = new EaseWindow(divisor);
        var standard = StrengthWindow.Supports(maType) && !Builder.Compute.ComponentAverage.HasOverrides;
        using var first = standard ? new RocBankAverage(maType, length, stockData.Count) : null;
        using var second = standard ? new RocBankAverage(maType, length, stockData.Count) : null;
        for (var i = 0; i < stockData.Count; i++)
        {
            var value = window.Next(stockData.HighPrices[i], stockData.LowPrices[i], stockData.Volumes[i], true);
            output.Add(value.Publish());
            if (standard) signal.Add(second!.Next(first!.Next(value, true), true).Publish());
        }
        if (!standard)
        {
            var firstMean = GetMovingAverageList(stockData, maType, length, output);
            signal = GetMovingAverageList(stockData, maType, length, firstMean);
        }
        for (var i = 0; i < stockData.Count; i++) signals?.Add(GetCompareSignal(output[i] - signal[i], i == 0 ? 0 : output[i - 1] - signal[i - 1]));
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Eom", output } });
        stockData.SetSignals(signals); stockData.SetCustomValues(output);
        stockData.IndicatorName = IndicatorName.EaseOfMovement;
        return stockData;
    }


    /// <summary>
    /// Calculates the Hawkeye Volume Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="divisor"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateHawkeyeVolumeIndicator(this StockData stockData, int length = 200,
        double divisor = 3.6)
    {
        var window = new HawkeyeWindow(length, divisor);
        var (input, highs, lows, _, closes, volumes) = GetInputValuesList(InputName.MedianPrice, stockData);
        List<double> up = new(stockData.Count), down = new(stockData.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < stockData.Count; i++)
        {
            var point = window.Next(input[i], highs[i], lows[i], closes[i], volumes[i], true);
            up.Add(point.Up); down.Add(point.Down); signals?.Add(point.Trade);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Up", up }, { "Dn", down } });
        stockData.SetSignals(signals); stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.HawkeyeVolumeIndicator;
        return stockData;
    }


    /// <summary>
    /// Calculates the Herrick Payoff Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="pointValue"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateHerrickPayoffIndex(this StockData stockData, double pointValue = 100)
    {
        var window = new HerrickPayoffWindow(pointValue);
        var (prices, _, _, opens, closes, volumes) = GetInputValuesList(InputName.MedianPrice, stockData);
        List<double> output = new(prices.Count);
        var signals = CreateSignalsList(stockData);
        for (var i = 0; i < prices.Count; i++)
        {
            var point = window.Next(prices[i], opens[i], closes[i], volumes[i], true);
            output.Add(point.Value); signals?.Add(point.Trade);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Hpi", output } });
        stockData.SetSignals(signals); stockData.SetCustomValues(output);
        stockData.IndicatorName = IndicatorName.HerrickPayoffIndex; return stockData;
    }


    /// <summary>
    /// Calculates the Finite Volume Elements
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="factor"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateFiniteVolumeElements(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 22, double factor = 0.3)
    {
        length = Math.Max(1, length); var (input, _, _, _, volumeList) = GetInputValuesList(stockData);
        using var window = new FiniteVolumeWindow(maType, length, factor);
        List<double> fveList = new(stockData.Count), bullList = new(stockData.Count), bearList = new(stockData.Count); List<Signal>? signalsList = CreateSignalsList(stockData);
        var custom = Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType);
        var volumeSmaList = custom ? Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(volumeList), length)?.ToList() ?? GetMovingAverageList(stockData, maType, length, volumeList) : null;
        for (var i = 0; i < stockData.Count; i++)
        {
            var prevFve = i > 0 ? fveList[i - 1] : 0; var prevFve2 = i > 1 ? fveList[i - 2] : 0;
            var fve = window.Next(stockData.HighPrices[i], stockData.LowPrices[i], input[i], volumeList[i], true, volumeSmaList?[i]); fveList.Add(fve);
            var prevBullSlope = i >= 1 ? bullList[i - 1] : 0;
            var bullSlope = fve - Math.Max(prevFve, prevFve2);
            bullList.Add(bullSlope);

            var prevBearSlope = i >= 1 ? bearList[i - 1] : 0;
            var bearSlope = fve - Math.Min(prevFve, prevFve2);
            bearList.Add(bearSlope);

            var signal = GetBullishBearishSignal(bullSlope, prevBullSlope, bearSlope, prevBearSlope);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Fve", fveList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(fveList);
        stockData.IndicatorName = IndicatorName.FiniteVolumeElements;

        return stockData;
    }


    /// <summary>
    /// Calculates the Freedom of Movement
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateFreedomOfMovement(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 60)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); var result = FreedomWindow.Calculate(stockData, input, maType, length, false);
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Fom", result.Score.ToList() }, { "Dpl", result.Demand.ToList() } });
        stockData.SetSignals(result.Trades.ToList()); stockData.SetCustomValues(result.Score.ToList()); stockData.IndicatorName = IndicatorName.FreedomOfMovement; return stockData;
    }

}

