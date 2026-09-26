
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the donchian channels.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateDonchianChannels(this StockData stockData, int length = 20)
    {
        List<double> upperChannelList = new(stockData.Count);
        List<double> lowerChannelList = new(stockData.Count);
        List<double> middleChannelList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var upperChannel = highestList[i];
            upperChannelList.Add(upperChannel);

            var lowerChannel = lowestList[i];
            lowerChannelList.Add(lowerChannel);

            var prevMiddleChannel = GetLastOrDefault(middleChannelList);
            var middleChannel = PriceMean.Of(upperChannel, lowerChannel);
            middleChannelList.Add(middleChannel);

            var signal = GetCompareSignal(currentValue - middleChannel, prevValue - prevMiddleChannel);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperChannel", upperChannelList },
            { "LowerChannel", lowerChannelList },
            { "MiddleChannel", middleChannelList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.DonchianChannels;

        return stockData;
    }


    /// <summary>
    /// Calculates the Average True Range Channel
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <param name="mult">The mult.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateAverageTrueRangeChannel(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 14, double mult = 2.5)
    {
        var (input, high, low, _, _) = GetInputValuesList(stockData);
        var external = Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType);
        List<double>? center = null, atr = null;
        if (external)
        {
            var ranges = GetTrueRangeList(stockData);
            atr = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(ranges), Math.Max(1, length))?.ToList() ?? GetMovingAverageList(stockData, maType, length, ranges);
            center = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(input), Math.Max(1, length))?.ToList() ?? GetMovingAverageList(stockData, maType, length, input);
        }
        using var window = external ? null : new KeltnerWindow(maType, length, length, maType, Math.Max(1, input.Count));
        List<double> upper = new(input.Count), middle = new(input.Count), lower = new(input.Count), average = new(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            var stages = external ? (new RocBankValue(center![i]), new RocBankValue(atr![i])) : window!.Next(high[i], low[i], input[i], true);
            var point = RangeChannelWindow.Output(input[i], stages.Item1, stages.Item2, mult, true);
            signals?.Add(GetBollingerBandsSignal(input[i] - point.Average, (i > 0 ? input[i - 1] : 0) - (i > 0 ? average[i - 1] : 0), input[i], i > 0 ? input[i - 1] : 0, point.Upper, i > 0 ? upper[i - 1] : 0, point.Lower, i > 0 ? lower[i - 1] : 0));
            upper.Add(point.Upper); middle.Add(point.Middle); lower.Add(point.Lower); average.Add(point.Average);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "UpperBand", upper }, { "MiddleBand", middle }, { "LowerBand", lower } , { "Sma", average } });
        stockData.SetSignals(signals); stockData.SetCustomValues(new List<double>()); stockData.IndicatorName = IndicatorName.AverageTrueRangeChannel; return stockData;
    }


    /// <summary>
    /// Calculates the Dema 2 Lines
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateDema2Lines(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int fastLength = 10, int slowLength = 40)
    {
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var ema1List = GetMovingAverageList(stockData, maType, fastLength, inputList);
        var ema2List = GetMovingAverageList(stockData, maType, slowLength, inputList);
        var dema1List = GetMovingAverageList(stockData, maType, fastLength, ema1List);
        var dema2List = GetMovingAverageList(stockData, maType, slowLength, ema2List);

        for (var i = 0; i < stockData.Count; i++)
        {
            var dema1 = dema1List[i];
            var dema2 = dema2List[i];
            var prevDema1 = i >= 1 ? dema1List[i - 1] : 0;
            var prevDema2 = i >= 1 ? dema2List[i - 1] : 0;

            var signal = GetCompareSignal(dema1 - dema2, prevDema1 - prevDema2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Dema1", dema1List },
            { "Dema2", dema2List }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.Dema2Lines;

        return stockData;
    }


    /// <summary>
    /// Calculates the Dynamic Support and Resistance
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateDynamicSupportAndResistance(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, 
        int length = 25)
    {
        var (input, high, low, _, _) = GetInputValuesList(stockData); var external = Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType);
        List<double>? atr = null;
        if (external)
        {
            var ranges = GetTrueRangeList(stockData);
            atr = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(ranges), Math.Max(1, length))?.ToList() ?? GetMovingAverageList(stockData, maType, length, ranges);
        }
        using var window = new DynamicSupportWindow(maType, length, external, Math.Max(1, input.Count));
        List<double> support = new(input.Count), resistance = new(input.Count), middle = new(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            var point = window.Next(high[i], low[i], input[i], true, external ? new RocBankValue(atr![i]) : null);
            signals?.Add(GetCompareSignal(input[i] - point.Middle, i > 0 ? input[i - 1] - middle[i - 1] : 0));
            support.Add(point.Support); resistance.Add(point.Resistance); middle.Add(point.Middle);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Support", support }, { "Resistance", resistance }, { "MiddleBand", middle } });
        stockData.SetSignals(signals); stockData.SetCustomValues(new List<double>()); stockData.IndicatorName = IndicatorName.DynamicSupportAndResistance; return stockData;
    }


    /// <summary>
    /// Calculates the Daily Average Price Delta
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateDailyAveragePriceDelta(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 21)
    {
        List<double> topList = new(stockData.Count);
        List<double> bottomList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (_, highList, lowList, _, _) = GetInputValuesList(stockData);

        var smaHighList = GetMovingAverageList(stockData, maType, length, highList);
        var smaLowList = GetMovingAverageList(stockData, maType, length, lowList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var high = highList[i];
            var low = lowList[i];
            var highSma = smaHighList[i];
            var lowSma = smaLowList[i];
            var dapd = highSma - lowSma;

            var prevTop = GetLastOrDefault(topList);
            var top = high + dapd;
            topList.Add(top);

            var prevBottom = GetLastOrDefault(bottomList);
            var bottom = low - dapd;
            bottomList.Add(bottom);

            var signal = GetConditionSignal(high > prevTop, low < prevBottom);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", topList },
            { "LowerBand", bottomList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.DailyAveragePriceDelta;

        return stockData;
    }


    /// <summary>
    /// Calculates the D Envelope
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="devFactor"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateDEnvelope(this StockData stockData, int length = 20, double devFactor = 2)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); var window = new DEnvelopeWindow(length, devFactor);
        List<double> upper = new(input.Count), middle = new(input.Count), lower = new(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            var point = window.Next(input[i], true); signals?.Add(GetBollingerBandsSignal(input[i] - point.Middle, i > 0 ? input[i - 1] - middle[i - 1] : 0, input[i], i > 0 ? input[i - 1] : 0, point.Upper, i > 0 ? upper[i - 1] : 0, point.Lower, i > 0 ? lower[i - 1] : 0));
            upper.Add(point.Upper); middle.Add(point.Middle); lower.Add(point.Lower);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "UpperBand", upper }, { "MiddleBand", middle }, { "LowerBand", lower } });
        stockData.SetSignals(signals); stockData.SetCustomValues(new List<double>()); stockData.IndicatorName = IndicatorName.DEnvelope; return stockData;
    }

}

