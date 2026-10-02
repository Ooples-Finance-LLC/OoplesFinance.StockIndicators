
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Stochastic Oscillator
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <param name="smoothLength1">Length of the K signal.</param>
    /// <param name="smoothLength2">Length of the D signal.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateStochasticOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 14, int smoothLength1 = 3, int smoothLength2 = 3)
    {
        List<double> fastKList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var highestHigh = highestList[i];
            var lowestLow = lowestList[i];

            var fastK = ClampedRangePosition.Percent(currentValue, lowestLow, highestHigh);
            fastKList.Add(fastK);
        }

        List<double> fastDList;
        List<double> slowDList;
        if (maType == MovingAvgType.SimpleMovingAverage)
        {
            using var first = new Streaming.RoundedSimpleMovingAverageSmoother(Math.Max(1, smoothLength1));
            using var second = new Streaming.RoundedSimpleMovingAverageSmoother(Math.Max(1, smoothLength2));
            fastDList = fastKList.Select(value => first.Next(value, true)).ToList();
            slowDList = fastDList.Select(value => second.Next(value, true)).ToList();
        }
        else
        {
            // Keep both stages explicit: generated component constructors derive their arity here.
            fastDList = GetMovingAverageList(stockData, maType, smoothLength1, fastKList);
            slowDList = GetMovingAverageList(stockData, maType, smoothLength2, fastDList);
        }
        for (var i = 0; i < stockData.Count; i++)
        {
            var slowK = fastDList[i];
            var slowD = slowDList[i];
            var prevSlowk = i >= 1 ? fastDList[i - 1] : 0;
            var prevSlowd = i >= 1 ? slowDList[i - 1] : 0;

            var signal = GetRsiSignal(slowK - slowD, prevSlowk - prevSlowd, slowK, prevSlowk, 80, 20);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "FastK", fastKList },
            { "FastD", fastDList },
            { "SlowD", slowDList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(fastKList);
        stockData.IndicatorName = IndicatorName.StochasticOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Fast Turbo Stochastics
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="turboLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateTurboStochasticsFast(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length1 = 20, int length2 = 10, int turboLength = 2)
    {
        var values = TurboStochasticsWindow.Calculate(stockData, maType, length1, length2, turboLength, false);
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Tsf", values.Line.ToList() }, { "Signal", values.SignalLine.ToList() } });
        stockData.SetSignals(values.Trades.ToList()); stockData.SetCustomValues(values.Line.ToList());
        stockData.IndicatorName = IndicatorName.TurboStochasticsFast; return stockData;
    }


    /// <summary>
    /// Calculates the Slow Turbo Stochastics
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="turboLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateTurboStochasticsSlow(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length1 = 20, int length2 = 10, int turboLength = 2)
    {
        var values = TurboStochasticsWindow.Calculate(stockData, maType, length1, length2, turboLength, true);
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Tsf", values.Line.ToList() }, { "Signal", values.SignalLine.ToList() } });
        stockData.SetSignals(values.Trades.ToList()); stockData.SetCustomValues(values.Line.ToList());
        stockData.IndicatorName = IndicatorName.TurboStochasticsSlow; return stockData;
    }


    /// <summary>
    /// Calculates the Stochastic Momentum Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="smoothLength1"></param>
    /// <param name="smoothLength2"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateStochasticMomentumIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length1 = 2, int length2 = 8, int smoothLength1 = 5, int smoothLength2 = 5)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        List<double> line = new(stockData.Count), signal = new(stockData.Count);
        if (StrengthWindow.Supports(maType) && !Builder.Compute.ComponentAverage.HasOverrides)
        {
            using var window = new StochasticMomentumWindow(maType, length1, length2, smoothLength1, smoothLength2);
            for (var i = 0; i < input.Count; i++)
            {
                var value = window.Next(input[i], stockData.HighPrices[i], stockData.LowPrices[i], true);
                line.Add(value.Line); signal.Add(value.Signal);
            }
        }
        else
        {
            List<double> distance = new(stockData.Count), range = new(stockData.Count);
            using var high = new Streaming.RollingWindowMax(Math.Max(1, length1)); using var low = new Streaming.RollingWindowMin(Math.Max(1, length1));
            for (var i = 0; i < input.Count; i++)
            {
                var value = StochasticMomentumWindow.Components(input[i], high.Add(stockData.HighPrices[i], out _), low.Add(stockData.LowPrices[i], out _));
                distance.Add(value.Distance.Publish()); range.Add(value.Range.Publish());
            }
            var firstDistance = GetMovingAverageList(stockData, maType, length2, distance);
            var firstRange = GetMovingAverageList(stockData, maType, length2, range);
            var secondDistance = GetMovingAverageList(stockData, maType, smoothLength1, firstDistance);
            var secondRange = GetMovingAverageList(stockData, maType, smoothLength1, firstRange);
            for (var i = 0; i < input.Count; i++) line.Add(StochasticMomentumWindow.Ratio(new(secondDistance[i]), new(secondRange[i])));
            signal = GetMovingAverageList(stockData, maType, smoothLength2, line);
        }
        List<Signal>? signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++) signals?.Add(GetCompareSignal(line[i] - signal[i], i == 0 ? 0 : line[i - 1] - signal[i - 1]));
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Smi", line }, { "Signal", signal } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.StochasticMomentumIndex;
        return stockData;
    }


    /// <summary>
    /// Calculates the Stochastic Fast Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength1"></param>
    /// <param name="smoothLength2"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateStochasticFastOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length = 14, int smoothLength1 = 3, int smoothLength2 = 2)
    {
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var fastKList = CalculateStochasticOscillator(stockData, maType, length, smoothLength1, smoothLength2);
        var pkList = fastKList.ChainedOutputs["FastD"];
        var pdList = fastKList.ChainedOutputs["SlowD"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var pkEma = pkList[i];
            var pdEma = pdList[i];
            var prevPkema = i >= 1 ? pkList[i - 1] : 0;
            var prevPdema = i >= 1 ? pdList[i - 1] : 0;

            var signal = GetRsiSignal(pkEma - pdEma, prevPkema - prevPdema, pkEma, prevPkema, 80, 20);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Sfo", pkList },
            { "Signal", pdList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(pkList);
        stockData.IndicatorName = IndicatorName.StochasticFastOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Stochastic Custom Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateStochasticCustomOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 7,
        int length2 = 3, int length3 = 12)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        List<double> line = new(stockData.Count), signal = new(stockData.Count);
        if (StrengthWindow.Supports(maType) && !Builder.Compute.ComponentAverage.HasOverrides)
        {
            using var window = new StochasticCustomWindow(maType, length1, length2, length3);
            for (var i = 0; i < input.Count; i++)
            {
                var value = window.Next(input[i], stockData.HighPrices[i], stockData.LowPrices[i], true);
                line.Add(value.Line); signal.Add(value.Signal);
            }
        }
        else
        {
            List<double> distance = new(stockData.Count), range = new(stockData.Count);
            using var high = new Streaming.RollingWindowMax(Math.Max(1, length1)); using var low = new Streaming.RollingWindowMin(Math.Max(1, length1));
            for (var i = 0; i < input.Count; i++)
            {
                var value = StochasticCustomWindow.Components(input[i], high.Add(stockData.HighPrices[i], out _), low.Add(stockData.LowPrices[i], out _));
                distance.Add(value.Distance.Publish()); range.Add(value.Range.Publish());
            }
            var firstDistance = GetMovingAverageList(stockData, maType, length2, distance);
            var firstRange = GetMovingAverageList(stockData, maType, length2, range);
            for (var i = 0; i < input.Count; i++) line.Add(StochasticCustomWindow.Ratio(new(firstDistance[i]), new(firstRange[i])));
            signal = GetMovingAverageList(stockData, maType, length3, line);
        }
        List<Signal>? signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++) signals?.Add(GetRsiSignal(line[i] - signal[i], i == 0 ? 0 : line[i - 1] - signal[i - 1], line[i], i == 0 ? 0 : line[i - 1], 70, 30));
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Sco", line }, { "Signal", signal } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.StochasticCustomOscillator;
        return stockData;
    }


    /// <summary>
    /// Calculates the Stochastic Regular
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateStochasticRegular(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 5, 
        int length2 = 3)
    {
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var stoList = CalculateStochasticOscillator(stockData, maType, length1, length2, length2);
        var fastKList = stoList.ChainedValues;
        var skList = stoList.ChainedOutputs["FastD"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var fk = fastKList[i];
            var sk = skList[i];
            var prevFk = i >= 1 ? fastKList[i - 1] : 0;
            var prevSk = i >= 1 ? skList[i - 1] : 0;

            var signal = GetRsiSignal(fk - sk, prevFk - prevSk, fk, prevFk, 70, 30);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Sco", fastKList },
            { "Signal", skList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(fastKList);
        stockData.IndicatorName = IndicatorName.StochasticRegular;

        return stockData;
    }


    /// <summary>
    /// Calculates the Swami Stochastics
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateSwamiStochastics(this StockData stockData, int fastLength = 12, int slowLength = 48)
    {
        var values = SwamiWindow.Calculate(stockData, fastLength, slowLength);
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Ss", values.Line.ToList() } });
        var signals = CreateSignalsList(stockData); signals?.AddRange(values.Trades); stockData.SetSignals(signals);
        stockData.SetCustomValues(values.Line.ToList()); stockData.IndicatorName = IndicatorName.SwamiStochastics;
        return stockData;
    }
}

