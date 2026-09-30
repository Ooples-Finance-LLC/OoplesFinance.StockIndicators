
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Demand Index
    /// </summary>
    /// <remarks>
    /// How much of the bar's volume the buyers took against how much the sellers took, less one, so that a
    /// bar split evenly reads zero. The split comes from where the close sits in the bar's range: a close at
    /// the high gives the whole volume to the buyers, a close at the low gives it to the sellers. It looks at
    /// one bar at a time and the first bar, having no predecessor, reads zero.
    /// </remarks>
    /// <param name="stockData"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateDemandIndex(this StockData stockData)
    {
        var (inputList, highList, lowList, _, volumeList) = GetInputValuesList(stockData);
        var count = inputList.Count;
        List<double> demandIndexList = new(count);
        List<Signal>? signalsList = CreateSignalsList(stockData, count);

        var window = new DemandIndexWindow();
        for (var i = 0; i < count; i++)
        {
            var point = window.Next(highList[i], lowList[i], inputList[i], volumeList[i], true);
            demandIndexList.Add(point.Value);
            signalsList?.Add(point.Signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Di", demandIndexList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(demandIndexList);
        stockData.IndicatorName = IndicatorName.DemandIndex;

        return stockData;
    }

    /// <summary>
    /// Calculates the Cumulative Volume Index
    /// </summary>
    /// <remarks>
    /// A running total that adds the bar's volume when it closed up and subtracts it when it closed down,
    /// leaving the total alone on a bar that closed unchanged. The first bar has nothing to compare with, so
    /// it starts the total at zero. Because it accumulates a change rather than a level, it settles on a
    /// market that stops moving instead of drifting on for ever.
    /// </remarks>
    /// <param name="stockData"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateCumulativeVolumeIndex(this StockData stockData)
    {
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);
        var count = inputList.Count;
        List<double> cviList = new(count);
        List<Signal>? signalsList = CreateSignalsList(stockData, count);

        var total = new ExactMeanAccumulator();
        for (var i = 0; i < count; i++)
        {
            if (i >= 1)
            {
                var currentValue = inputList[i];
                var prevValue = inputList[i - 1];
                if (currentValue > prevValue)
                {
                    total.Add(volumeList[i]);
                }
                else if (currentValue < prevValue)
                {
                    total.Add(volumeList[i], -1);
                }
            }

            var cvi = total.Mean(1);
            cviList.Add(cvi);

            var prevCvi1 = i >= 1 ? cviList[i - 1] : 0;
            var prevCvi2 = i >= 2 ? cviList[i - 2] : 0;
            var signal = GetCompareSignal(cvi - prevCvi1, prevCvi1 - prevCvi2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Cvi", cviList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(cviList);
        stockData.IndicatorName = IndicatorName.CumulativeVolumeIndex;

        return stockData;
    }

    /// <summary>
    /// Calculates the Chaikin Money Flow
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateChaikinMoneyFlow(this StockData stockData, int length = 20)
    {
        var input=stockData.ChainedValues.Count>0?stockData.ChainedValues:stockData.InputValues;var high=stockData.HighPrices;var low=stockData.LowPrices;var volume=stockData.Volumes;using var window=new ChaikinFlowWindow(length);
        List<double> values=new(input.Count);var signals=CreateSignalsList(stockData);
        for(var i=0;i<input.Count;i++)
        {
            var value=window.Next(high[i],low[i],input[i],volume[i],true);
            var previous=i>0?values[i-1]:0;var older=i>1?values[i-2]:0;
            signals?.Add(GetCompareSignal(value-previous,previous-older));values.Add(value);
        }
        stockData.SetOutputValues(()=>new Dictionary<string,List<double>>{{"Cmf",values}});stockData.SetSignals(signals);stockData.SetCustomValues(values);stockData.IndicatorName=IndicatorName.ChaikinMoneyFlow;return stockData;
    }


    /// <summary>
    /// Calculates the accumulation distribution line.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateAccumulationDistributionLine(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length = 14)
    {
        List<double> line = new(stockData.Count), first = new(stockData.Count);
        List<Signal>? signals = CreateSignalsList(stockData);
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        var cumulative = new MoneyFlowAccumulationWindow();
        var standard = StrengthWindow.Supports(maType) && !Builder.Compute.ComponentAverage.HasOverrides;
        using var firstAverage = standard ? new RocBankAverage(maType, length, stockData.Count) : null;
        for (var i = 0; i < stockData.Count; i++)
        {
            var value = cumulative.Next(stockData.HighPrices[i], stockData.LowPrices[i], input[i], stockData.Volumes[i], true);
            line.Add(value.Publish());
            if (standard)
            {
                var mean = firstAverage!.Next(value, true);
                first.Add(mean.Publish());
            }
        }
        if (!standard)
        {
            first = GetMovingAverageList(stockData, maType, length, line);
        }
        for (var i = 0; i < stockData.Count; i++) signals?.Add(GetCompareSignal(line[i] - first[i], i == 0 ? 0 : line[i - 1] - first[i - 1]));
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Adl", line }, { "AdlSignal", first } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line);
        stockData.IndicatorName = IndicatorName.AccumulationDistributionLine;
        return stockData;
    }


    /// <summary>
    /// Calculates the average money flow oscillator.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <param name="smoothLength">Length of the smooth.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateAverageMoneyFlowOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage, 
        int length = 5, int smoothLength = 3)
    {
        var (input, _, _, _, volumes) = GetInputValuesList(stockData); var external = Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType);
        var components = external ? AverageMoneyFlowWindow.Components(stockData, input, volumes, maType, length, smoothLength) : null; using var window = new AverageMoneyFlowWindow(maType, length, smoothLength, external);
        var values = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++) { var point = window.Next(input[i], volumes[i], true, components?[0][i], components?[1][i], components?[2][i]); values.Add(point.Value); signals?.Add(point.Signal); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Amfo", values } }); stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.AverageMoneyFlowOscillator; return stockData;
    }


    /// <summary>
    /// Calculates the Better Volume Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="lbLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateBetterVolumeIndicator(this StockData stockData, int length = 8, int lbLength = 2)
    {
        var (input, high, low, open, volumes) = GetInputValuesList(stockData); var window = new BetterVolumeWindow(length, lbLength);
        var values = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++) { var point = window.Next(open[i], high[i], low[i], input[i], volumes[i], true); values.Add(point.Value); signals?.Add(point.Signal); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Bvi", values } }); stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.BetterVolumeIndicator; return stockData;
    }


    /// <summary>
    /// Calculates the Buff Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateBuffAverage(this StockData stockData, int fastLength = 5, int slowLength = 20)
    {
        List<double> fastValues = new(stockData.Count), slowValues = new(stockData.Count);
        var signals = CreateSignalsList(stockData);
        var (input, _, _, _, volumes) = GetInputValuesList(stockData);
        var fast = new BuffWindow(fastLength); var slow = new BuffWindow(slowLength);
        for (var i = 0; i < stockData.Count; i++)
        {
            var a = fast.Next(input[i], volumes[i], true); var b = slow.Next(input[i], volumes[i], true);
            var previous = i == 0 ? 0 : fastValues[i - 1] - slowValues[i - 1];
            signals?.Add(GetCompareSignal(a - b, previous)); fastValues.Add(a); slowValues.Add(b);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "FastBuff", fastValues }, { "SlowBuff", slowValues } });
        stockData.SetSignals(signals); stockData.SetCustomValues(new List<double>()); stockData.IndicatorName = IndicatorName.BuffAverage;
        return stockData;
    }

}

