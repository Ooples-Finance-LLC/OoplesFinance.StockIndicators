
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Chande Quick Stick
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateChandeQuickStick(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); var opens = stockData.OpenPrices;
        var lag = 0; var custom = Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType);
        List<double>? customer = null;
        if (custom)
        {
            var changes = input.Select((price, i) => OpenCloseAverageWindow.Difference(i >= lag ? opens[i - lag] : 0, price).Publish()).ToList();
            customer = GetMovingAverageList(stockData, maType, length, changes);
        }
        List<double> line = new(stockData.Count), signal = new(stockData.Count), histogram = new(stockData.Count);
        List<Signal>? signals = CreateSignalsList(stockData);
        using var window = new OpenCloseAverageWindow(maType, length, lag);
        for (var i = 0; i < input.Count; i++)
        {
            var value = window.Next(opens[i], input[i], true, customer?[i]);
            line.Add(value.Line); signal.Add(value.Signal); histogram.Add(value.Histogram);
            signals?.Add(GetCompareSignal(value.Signal, i == 0 ? 0 : signal[i - 1]));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Cqs", signal } });
        stockData.SetSignals(signals); stockData.SetCustomValues(signal); stockData.IndicatorName = IndicatorName.ChandeQuickStick;
        return stockData;
    }


    /// <summary>
    /// Calculates the Chande Momentum Oscillator Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="filter"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateChandeMomentumOscillatorFilter(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length = 9, double filter = 3)
    {
        List<double> cmoList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        using var window = new ChandeMomentumWindow(Math.Max(1, Math.Min(length, stockData.Count)), filter);
        for (var i = 0; i < stockData.Count; i++) cmoList.Add(window.Next(inputList[i], true));
        List<double> cmoSignalList;
        if (maType == MovingAvgType.SimpleMovingAverage)
        {
            cmoSignalList = new(stockData.Count);
            using var mean = new Streaming.RoundedSimpleMovingAverageSmoother(Math.Max(1, length));
            foreach (var value in cmoList) cmoSignalList.Add(mean.Next(value, true));
        }
        else cmoSignalList = GetMovingAverageList(stockData, maType, length, cmoList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var cmo = cmoList[i];
            var cmoSignal = cmoSignalList[i];
            var prevCmo = i >= 1 ? cmoList[i - 1] : 0;
            var prevCmoSignal = i >= 1 ? cmoSignalList[i - 1] : 0;

            var signal = GetRsiSignal(cmo - cmoSignal, prevCmo - prevCmoSignal, cmo, prevCmo, 70, -70);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Cmof", cmoList },
            { "Signal", cmoSignalList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(cmoList);
        stockData.IndicatorName = IndicatorName.ChandeMomentumOscillatorFilter;

        return stockData;
    }


    /// <summary>
    /// Calculates the Chande Momentum Oscillator Absolute
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateChandeMomentumOscillatorAbsolute(this StockData stockData, int length = 9)
    {
        List<double> cmoAbsList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var values = new double[inputList.Count];
        Core.OscillatorCore.ChandeMomentumOscillatorAbsolute(inputList.ToArray(), values, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevCmoAbs1 = i >= 1 ? cmoAbsList[i - 1] : 0;
            var prevCmoAbs2 = i >= 2 ? cmoAbsList[i - 2] : 0;

            var cmoAbs = values[i];
            cmoAbsList.Add(cmoAbs);

            var signal = GetRsiSignal(cmoAbs - prevCmoAbs1, prevCmoAbs1 - prevCmoAbs2, cmoAbs, prevCmoAbs1, 70, 30);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Cmoa", cmoAbsList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(cmoAbsList);
        stockData.IndicatorName = IndicatorName.ChandeMomentumOscillatorAbsolute;

        return stockData;
    }


    /// <summary>
    /// Calculates the Chande Momentum Oscillator Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateChandeMomentumOscillatorAverage(this StockData stockData, int length1 = 5, int length2 = 10, int length3 = 20)
    {
        List<double> cmoAvgList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var values = new double[inputList.Count];
        Core.OscillatorCore.ChandeMomentumOscillatorAverage(inputList.ToArray(), values, length1, length2, length3);

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevCmoAvg1 = i >= 1 ? cmoAvgList[i - 1] : 0;
            var prevCmoAvg2 = i >= 2 ? cmoAvgList[i - 2] : 0;

            var cmoAvg = values[i];
            cmoAvgList.Add(cmoAvg);

            var signal = GetRsiSignal(cmoAvg - prevCmoAvg1, prevCmoAvg1 - prevCmoAvg2, cmoAvg, prevCmoAvg1, 50, -50);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Cmoa", cmoAvgList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(cmoAvgList);
        stockData.IndicatorName = IndicatorName.ChandeMomentumOscillatorAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Chande Momentum Oscillator Absolute Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateChandeMomentumOscillatorAbsoluteAverage(this StockData stockData, int length1 = 5, int length2 = 10, int length3 = 20)
    {
        List<double> cmoAbsAvgList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var values = new double[inputList.Count];
        Core.OscillatorCore.ChandeMomentumOscillatorAbsoluteAverage(inputList.ToArray(), values, length1, length2, length3);

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevCmoAbsAvg1 = i >= 1 ? cmoAbsAvgList[i - 1] : 0;
            var prevCmoAbsAvg2 = i >= 2 ? cmoAbsAvgList[i - 2] : 0;

            var cmoAbsAvg = values[i];
            cmoAbsAvgList.Add(cmoAbsAvg);

            var signal = GetRsiSignal(cmoAbsAvg - prevCmoAbsAvg1, prevCmoAbsAvg1 - prevCmoAbsAvg2, cmoAbsAvg, prevCmoAbsAvg1, 70, 30);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Cmoaa", cmoAbsAvgList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(cmoAbsAvgList);
        stockData.IndicatorName = IndicatorName.ChandeMomentumOscillatorAbsoluteAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Chande Momentum Oscillator Average Disparity Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateChandeMomentumOscillatorAverageDisparityIndex(this StockData stockData,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 200, int length2 = 50, int length3 = 20)
    {
        List<double> avgDisparityIndexList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var firstEmaList = GetMovingAverageList(stockData, maType, length1, inputList);
        var secondEmaList = GetMovingAverageList(stockData, maType, length2, inputList);
        var thirdEmaList = GetMovingAverageList(stockData, maType, length3, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var firstEma = firstEmaList[i];
            var secondEma = secondEmaList[i];
            var thirdEma = thirdEmaList[i];
            var firstDisparityIndex = currentValue != 0 ? (currentValue - firstEma) / currentValue * 100 : 0;
            var secondDisparityIndex = currentValue != 0 ? (currentValue - secondEma) / currentValue * 100 : 0;
            var thirdDisparityIndex = currentValue != 0 ? (currentValue - thirdEma) / currentValue * 100 : 0;

            var prevAvgDisparityIndex = GetLastOrDefault(avgDisparityIndexList);
            var avgDisparityIndex = (firstDisparityIndex + secondDisparityIndex + thirdDisparityIndex) / 3;
            avgDisparityIndexList.Add(avgDisparityIndex);

            var signal = GetCompareSignal(avgDisparityIndex, prevAvgDisparityIndex);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Cmoadi", avgDisparityIndexList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(avgDisparityIndexList);
        stockData.IndicatorName = IndicatorName.ChandeMomentumOscillatorAverageDisparityIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Chande Momentum Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateChandeMomentumOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 14, int signalLength = 3)
    {
        List<double> cmoList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        using var window = new ChandeMomentumWindow(Math.Max(1, Math.Min(length, stockData.Count)));
        for (var i = 0; i < stockData.Count; i++) cmoList.Add(window.Next(inputList[i], true));
        List<double> cmoSignalList;
        if (maType == MovingAvgType.SimpleMovingAverage)
        {
            cmoSignalList = new(stockData.Count);
            using var mean = new Streaming.RoundedSimpleMovingAverageSmoother(Math.Max(1, signalLength));
            foreach (var value in cmoList) cmoSignalList.Add(mean.Next(value, true));
        }
        else cmoSignalList = GetMovingAverageList(stockData, maType, signalLength, cmoList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var cmo = cmoList[i];
            var cmoSignal = cmoSignalList[i];
            var prevCmo = i >= 1 ? cmoList[i - 1] : 0;
            var prevCmoSignal = i >= 1 ? cmoSignalList[i - 1] : 0;

            var signal = GetRsiSignal(cmo - cmoSignal, prevCmo - prevCmoSignal, cmo, prevCmo, 50, -50);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Cmo", cmoList },
            { "Signal", cmoSignalList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(cmoList);
        stockData.IndicatorName = IndicatorName.ChandeMomentumOscillator;

        return stockData;
    }
}

