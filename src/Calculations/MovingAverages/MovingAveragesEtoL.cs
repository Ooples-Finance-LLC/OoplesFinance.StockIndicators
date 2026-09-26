using OoplesFinance.StockIndicators.Compatibility;
using OoplesFinance.StockIndicators.Core;

namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Geometric Moving Average.
    /// </summary>
    /// <remarks>
    /// Each value is floored at a millionth. The exact product is normalized with one final root rounding.
    /// This preserves representable means without intermediate overflow. Distinct from
    /// <see cref="CalculateGeometricMeanMovingAverage"/>, which omits nonpositive values.
    /// </remarks>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateGeometricMovingAverage(this StockData stockData, int length = 14)
    {
        length = Math.Max(length, 1);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var count = inputList.Count;
        List<double> gmaList = new(count);
        List<Signal>? signalsList = CreateSignalsList(stockData, count);

        using var mean = new RollingGeometricMean(length);

        for (var i = 0; i < count; i++)
        {
            var gma = mean.Next(inputList[i], true);

            gmaList.Add(gma);

            var prevGma1 = i >= 1 ? gmaList[i - 1] : 0;
            var prevGma2 = i >= 2 ? gmaList[i - 2] : 0;
            var signal = GetCompareSignal(gma - prevGma1, prevGma1 - prevGma2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Gma", gmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(gmaList);
        stockData.IndicatorName = IndicatorName.GeometricMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Geometric Mean Moving Average.
    /// </summary>
    /// <remarks>
    /// The geometric mean of the window's positive values: their product raised to one over how many there
    /// were. A value of zero or less has no place in a product of that kind and is left out, so the root
    /// taken is the root of the count actually used. Until the window fills, the bar publishes its own value.
    /// </remarks>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateGeometricMeanMovingAverage(this StockData stockData, int length = 14)
    {
        length = Math.Max(length, 1);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var count = inputList.Count;
        List<double> gmmaList = new(count);
        List<Signal>? signalsList = CreateSignalsList(stockData, count);

        using var mean = new RollingGeometricMean(length, positiveOnly: true);

        for (var i = 0; i < count; i++)
        {
            var gmma = mean.Next(inputList[i], true);

            gmmaList.Add(gmma);

            var prevGmma1 = i >= 1 ? gmmaList[i - 1] : 0;
            var prevGmma2 = i >= 2 ? gmmaList[i - 2] : 0;
            var signal = GetCompareSignal(gmma - prevGmma1, prevGmma1 - prevGmma2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Gmma", gmmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(gmmaList);
        stockData.IndicatorName = IndicatorName.GeometricMeanMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Harmonic Mean Moving Average.
    /// </summary>
    /// <remarks>
    /// The harmonic mean of the window: how many values there were, over the sum of their reciprocals. It
    /// leans towards the smaller values of the window, which is what makes it the right mean for a rate. A
    /// zero has no reciprocal and is left out. Until the window fills, the bar publishes its own value.
    /// </remarks>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateHarmonicMeanMovingAverage(this StockData stockData, int length = 14)
    {
        length = Math.Max(length, 1);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var count = inputList.Count;
        List<double> hmmaList = new(count);
        List<Signal>? signalsList = CreateSignalsList(stockData, count);

        for (var i = 0; i < count; i++)
        {
            double hmma;
            if (i < length - 1)
            {
                hmma = inputList[i];
            }
            else
            {
                var sum = new ExactReciprocalSum();
                for (var j = 0; j < length; j++) sum.Add(inputList[i - j]);
                hmma = sum.Mean;
            }

            hmmaList.Add(hmma);

            var prevHmma1 = i >= 1 ? hmmaList[i - 1] : 0;
            var prevHmma2 = i >= 2 ? hmmaList[i - 2] : 0;
            var signal = GetCompareSignal(hmma - prevHmma1, prevHmma1 - prevHmma2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Hmma", hmmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(hmmaList);
        stockData.IndicatorName = IndicatorName.HarmonicMeanMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the exponential moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateExponentialMovingAverage(this StockData stockData, int length = 14)
    {
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var count = inputList.Count;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var outputBuffer = SpanCompat.CreateOutputBuffer(count);
        var emaSpan = outputBuffer.Span;
        MovingAverageCore.ExponentialMovingAverage(inputSpan, emaSpan, length);
        var emaList = outputBuffer.ToList();

        List<Signal>? signalsList = CreateSignalsList(stockData, count);
        for (var i = 0; i < count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var ema = emaList[i];
            var prevEma = i >= 1 ? emaList[i - 1] : 0;
            var signal = GetCompareSignal(currentValue - ema, prevValue - prevEma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ema", emaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(emaList);
        stockData.IndicatorName = IndicatorName.ExponentialMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the hull moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateHullMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length = 20)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        List<double> line = new(stockData.Count); List<Signal>? signals = CreateSignalsList(stockData);
        if (Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType))
        {
            var full = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(input), length)?.ToList() ?? GetMovingAverageList(stockData, maType, length, input);
            var half = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(input), HullWindow.Half(length))?.ToList() ?? GetMovingAverageList(stockData, maType, HullWindow.Half(length), input);
            var adjusted = full.Select((v, i) => HullWindow.Combine(v, half[i])).ToList();
            line = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(adjusted), HullWindow.Root(length))?.ToList() ?? GetMovingAverageList(stockData, maType, HullWindow.Root(length), adjusted);
        }
        else
        {
            using var window = new HullWindow(maType, length);
            foreach (var price in input) line.Add(window.Next(price, true));
        }
        for (var i = 0; i < input.Count; i++) signals?.Add(GetCompareSignal(input[i] - line[i], i == 0 ? 0 : input[i - 1] - line[i - 1]));
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Hma", line } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.HullMovingAverage;
        return stockData;
    }


    /// <summary>
    /// Calculates the kaufman adaptive moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <param name="fastLength">Length of the fast.</param>
    /// <param name="slowLength">Length of the slow.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateKaufmanAdaptiveMovingAverage(this StockData stockData, int length = 10, int fastLength = 2, int slowLength = 30)
    {
        List<double> erList = new(stockData.Count);
        List<double> kamaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        using var mean = new RoundedKaufmanWindow(length, fastLength, slowLength);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var previous = GetLastOrDefault(kamaList);
            var result = mean.Next(currentValue, true);
            kamaList.Add(result.Average);
            erList.Add(result.Efficiency);
            signalsList?.Add(GetCompareSignal(currentValue - result.Average, prevValue - previous));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Er", erList }, { "Kama", kamaList } });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(kamaList);
        stockData.IndicatorName = IndicatorName.KaufmanAdaptiveMovingAverage;
        return stockData;
    }


    /// <summary>
    /// Calculates the end point moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <param name="offset">The offset.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEndPointMovingAverage(this StockData stockData, int length = 11, int offset = 4)
    {
        List<double> epmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        using var window = new AffineAverageWindow(length, offset, capacityHint: stockData.Count);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            var prevEpma = GetLastOrDefault(epmaList);
            var epma = window.Next(currentValue);
            epmaList.Add(epma);

            var signal = GetCompareSignal(currentValue - epma, prevVal - prevEpma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Epma", epmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(epmaList);
        stockData.IndicatorName = IndicatorName.EndPointMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the least squares moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateLeastSquaresMovingAverage(this StockData stockData, int length = 25)
    {
        List<double> lsmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        // Every Calculate method leaves its result on the chained series, so the second component read
        // the first one's output instead of the input both of them measure.
        var callerSeries = stockData.CaptureInputSeries();
        var wmaList = CalculateWeightedMovingAverage(stockData, length).ChainedValues;
        stockData.RestoreInputSeries(callerSeries);
        using var simple = new Streaming.RoundedSimpleMovingAverageSmoother(length);
        var smaList = inputList.Select(value => simple.Next(value, true)).ToList();
        stockData.RestoreInputSeries(callerSeries);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentWma = wmaList[i];
            var currentSma = smaList[i];

            var prevLsma = GetLastOrDefault(lsmaList);
            var lsma = LeastSquaresAverage.Combine(currentWma, currentSma);
            lsmaList.Add(lsma);

            var signal = GetCompareSignal(currentValue - lsma, prevValue - prevLsma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Lsma", lsmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(lsmaList);
        stockData.IndicatorName = IndicatorName.LeastSquaresMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Jsa Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateJsaMovingAverage(this StockData stockData, int length = 14)
    {
        List<double> jmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var priorValue = i >= length ? inputList[i - length] : 0;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevJma = GetLastOrDefault(jmaList);
            var jma = PriceMean.Of(currentValue, priorValue);
            jmaList.Add(jma);

            var signal = GetCompareSignal(currentValue - jma, prevValue - prevJma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Jma", jmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(jmaList);
        stockData.IndicatorName = IndicatorName.JsaMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Jurik Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="phase"></param>
    /// <param name="power"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateJurikMovingAverage(this StockData stockData, int length = 7, double phase = 50, double power = 2)
    {
        List<double> e0List = new(stockData.Count);
        List<double> e1List = new(stockData.Count);
        List<double> e2List = new(stockData.Count);
        List<double> jmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var phaseRatio = phase < -100 ? 0.5 : phase > 100 ? 2.5 : ((double)phase / 100) + 1.5;
        var ratio = 0.45 * (length - 1);
        var beta = ratio / (ratio + 2);
        var alpha = Pow(beta, power);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevJma = GetLastOrDefault(jmaList);

            var prevE0 = GetLastOrDefault(e0List);
            var e0 = ((1 - alpha) * currentValue) + (alpha * prevE0);
            e0List.Add(e0);

            var prevE1 = GetLastOrDefault(e1List);
            var e1 = ((currentValue - e0) * (1 - beta)) + (beta * prevE1);
            e1List.Add(e1);

            var prevE2 = GetLastOrDefault(e2List);
            var e2 = ((e0 + (phaseRatio * e1) - prevJma) * Pow(1 - alpha, 2)) + (Pow(alpha, 2) * prevE2);
            e2List.Add(e2);

            var jma = e2 + prevJma;
            jmaList.Add(jma);

            var signal = GetCompareSignal(currentValue - jma, prevValue - prevJma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Jma", jmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(jmaList);
        stockData.IndicatorName = IndicatorName.JurikMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Linear Weighted Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateLinearWeightedMovingAverage(this StockData stockData, int length = 14)
    {
        List<double> lwmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        using var average = new Streaming.WmaState(length);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            var prevLwma = GetLastOrDefault(lwmaList);
            var lwma = average.GetNext(currentValue, commit: true);
            lwmaList.Add(lwma);

            var signal = GetCompareSignal(currentValue - lwma, prevVal - prevLwma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Lwma", lwmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(lwmaList);
        stockData.IndicatorName = IndicatorName.LinearWeightedMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Leo Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateLeoMovingAverage(this StockData stockData, int length = 14)
    {
        List<double> lmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        // Every Calculate method leaves its result on the chained series, so the second component read
        // the first one's output instead of the input both of them measure.
        var callerSeries = stockData.CaptureInputSeries();
        var wmaList = CalculateWeightedMovingAverage(stockData, length).ChainedValues;
        stockData.RestoreInputSeries(callerSeries);
        using var simple = new Streaming.RoundedSimpleMovingAverageSmoother(length);
        var smaList = inputList.Select(value => simple.Next(value, true)).ToArray();
        stockData.RestoreInputSeries(callerSeries);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentWma = wmaList[i];
            var currentSma = smaList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevLma = GetLastOrDefault(lmaList);
            var lma = LeoAverage.Combine(currentWma, currentSma);
            lmaList.Add(lma);

            var signal = GetCompareSignal(currentValue - lma, prevValue - prevLma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Lma", lmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(lmaList);
        stockData.IndicatorName = IndicatorName.LeoMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Light Least Squares Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateLightLeastSquaresMovingAverage(this StockData stockData,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 250)
    {
        List<double> yList = new(stockData.Count);
        List<double> indexList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var length1 = MinOrMax((int)Math.Ceiling((double)length / 2));

        for (var i = 0; i < stockData.Count; i++)
        {
            double index = i;
            indexList.Add(index);
        }

        var sma1List = GetMovingAverageList(stockData, maType, length, inputList);
        var sma2List = GetMovingAverageList(stockData, maType, length1, inputList);
        var stdDevList = GetStandardDeviationList(inputList, length);
        stockData.SetCustomValues(indexList);
        var indexStdDevList = GetStandardDeviationList(indexList, length);
        var indexSmaList = GetMovingAverageList(stockData, maType, length, indexList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var sma1 = sma1List[i];
            var sma2 = sma2List[i];
            var stdDev = stdDevList[i];
            var indexStdDev = indexStdDevList[i];
            var indexSma = indexSmaList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var c = stdDev != 0 ? (sma2 - sma1) / stdDev : 0;
            var z = indexStdDev != 0 && c != 0 ? (i - indexSma) / indexStdDev * c : 0;

            var prevY = GetLastOrDefault(yList);
            var y = sma1 + (z * stdDev);
            yList.Add(y);

            var signal = GetCompareSignal(currentValue - y, prevValue - prevY);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Llsma", yList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(yList);
        stockData.IndicatorName = IndicatorName.LightLeastSquaresMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Linear Extrapolation
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateLinearExtrapolation(this StockData stockData, int length = 500)
    {
        List<double> extList = new(stockData.Count);
        List<double> xList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevY = i >= 1 ? inputList[i - 1] : 0;
            var priorY = i >= length ? inputList[i - length] : 0;
            var priorY2 = i >= length * 2 ? inputList[i - (length * 2)] : 0;
            var priorX = i >= length ? xList[i - length] : 0;
            var priorX2 = i >= length * 2 ? xList[i - (length * 2)] : 0;

            double x = i;
            xList.Add(i);

            var prevExt = GetLastOrDefault(extList);
            var ext = priorX2 - priorX != 0 && priorY2 - priorY != 0 ? priorY + ((x - priorX) / (priorX2 - priorX) * (priorY2 - priorY)) : priorY;
            extList.Add(ext);

            var signal = GetCompareSignal(currentValue - ext, prevY - prevExt);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "LinExt", extList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(extList);
        stockData.IndicatorName = IndicatorName.LinearExtrapolation;

        return stockData;
    }


    /// <summary>
    /// Calculates the Linear Regression Line
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateLinearRegressionLine(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 14)
    {
        length = Math.Max(1, length);
        List<double> output = new(stockData.Count);
        List<Signal>? signals = CreateSignalsList(stockData);
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        var meanPrice = GetMovingAverageList(stockData, maType, length, input);
        var times = Enumerable.Range(0, stockData.Count).Select(i => (double)i).ToList();
        var meanTime = GetMovingAverageList(stockData, maType, length, times);
        using var regression = new ExactLinearFitWindow(length);
        for (var i = 0; i < stockData.Count; i++)
        {
            var fit = regression.Next(input[i], true);
            var value = fit.Count < length ? meanPrice[i] : maType == MovingAvgType.SimpleMovingAverage ? fit.Last : fit.CenteredLine(meanPrice[i], meanTime[i]);
            var previous = i == 0 ? 0 : output[i - 1];
            output.Add(value);
            signals?.Add(GetCompareSignal(input[i] - value, (i == 0 ? 0 : input[i - 1]) - previous));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "LinReg", output } });
        stockData.SetSignals(signals); stockData.SetCustomValues(output);
        stockData.IndicatorName = IndicatorName.LinearRegressionLine;
        return stockData;
    }


    /// <summary>
    /// Calculates the IIR Least Squares Estimate
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateIIRLeastSquaresEstimate(this StockData stockData, int length = 100)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        var window = new IirLeastSquaresWindow(length);
        List<double> line = new(stockData.Count); List<Signal>? signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            var value = window.Next(input[i], true);
            signals?.Add(GetCompareSignal(input[i] - value, i == 0 ? -input[i] : input[i - 1] - line[i - 1])); line.Add(value);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "IIRLse", line } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.IIRLeastSquaresEstimate;
        return stockData;
    }


    /// <summary>
    /// Calculates the Inverse Distance Weighted Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateInverseDistanceWeightedMovingAverage(this StockData stockData, int length = 14)
    {
        List<double> idwmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        using var mean = new DistanceMassWindowMean(length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            var prevIdwma = GetLastOrDefault(idwmaList);
            var idwma = mean.Next(currentValue, true);
            idwmaList.Add(idwma);

            var signal = GetCompareSignal(currentValue - idwma, prevVal - prevIdwma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Idwma", idwmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(idwmaList);
        stockData.IndicatorName = IndicatorName.InverseDistanceWeightedMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Generalized Double Exponential Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="factor"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateGeneralizedDoubleExponentialMovingAverage(this StockData stockData,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 5, double factor = 0.7)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        var custom = Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType);
        List<double>? first = null, second = null;
        using var window = new GeneralizedDoubleWindow(maType, length, factor, initializeFallback: !custom);
        if (custom)
        {
            first = GetMovingAverageList(stockData, maType, length, input);
            second = GetMovingAverageList(stockData, maType, length, first);
        }
        List<double> line = new(stockData.Count); List<Signal>? signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            var value = window.Next(input[i], true, first?[i], second?[i]);
            signals?.Add(GetCompareSignal(input[i] - value, i == 0 ? 0 : input[i - 1] - line[i - 1])); line.Add(value);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Gdema", line } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.GeneralizedDoubleExponentialMovingAverage;
        return stockData;
    }


    /// <summary>
    /// Calculates the General Filter Estimator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="beta"></param>
    /// <param name="gamma"></param>
    /// <param name="zeta"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateGeneralFilterEstimator(this StockData stockData, int length = 100, double beta = 5.25, double gamma = 1,
        double zeta = 1)
    {
        List<double> dList = new(stockData.Count);
        List<double> bList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var p = beta != 0 ? (int)Math.Ceiling(length / beta) : 0;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var priorB = i >= p ? bList[i - p] : currentValue;
            var a = currentValue - priorB;

            var prevB = i >= 1 ? bList[i - 1] : currentValue;
            var b = prevB + (a / p * gamma);
            bList.Add(b);

            var priorD = i >= p ? dList[i - p] : b;
            var c = b - priorD;

            var prevD = i >= 1 ? dList[i - 1] : currentValue;
            var d = prevD + (((zeta * a) + ((1 - zeta) * c)) / p * gamma);
            dList.Add(d);

            var signal = GetCompareSignal(currentValue - d, prevValue - prevD);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Gfe", dList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(dList);
        stockData.IndicatorName = IndicatorName.GeneralFilterEstimator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Henderson Weighted Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateHendersonWeightedMovingAverage(this StockData stockData, int length = 7)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        using var window = new HendersonWindow(length);
        List<double> line = new(stockData.Count); List<Signal>? signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            var value = window.Next(input[i], true);
            signals?.Add(GetCompareSignal(input[i] - value, i == 0 ? 0 : input[i - 1] - line[i - 1])); line.Add(value);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Hwma", line } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.HendersonWeightedMovingAverage;
        return stockData;
    }


    /// <summary>
    /// Calculates the Holt Exponential Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="alphaLength"></param>
    /// <param name="gammaLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateHoltExponentialMovingAverage(this StockData stockData, int alphaLength = 20, int gammaLength = 20)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        var window = new HoltWindow(alphaLength, gammaLength);
        List<double> line = new(stockData.Count); List<Signal>? signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            var value = window.Next(input[i], true);
            signals?.Add(GetCompareSignal(input[i] - value, i == 0 ? 0 : input[i - 1] - line[i - 1])); line.Add(value);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Hema", line } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.HoltExponentialMovingAverage;
        return stockData;
    }


    /// <summary>
    /// Calculates the Hull Estimate
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateHullEstimate(this StockData stockData, int length = 50)
    {
        List<double> hemaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var maLength = MinOrMax((int)Math.Ceiling((double)length / 2));

        var wmaList = GetMovingAverageList(stockData, MovingAvgType.WeightedMovingAverage, maLength, inputList);
        var emaList = GetMovingAverageList(stockData, MovingAvgType.ExponentialMovingAverage, maLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentWma = wmaList[i];
            var currentEma = emaList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevHema = GetLastOrDefault(hemaList);
            var hema = HullEstimateWindow.Combine(currentWma, currentEma);
            hemaList.Add(hema);

            var signal = GetCompareSignal(currentValue - hema, prevValue - prevHema);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "He", hemaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(hemaList);
        stockData.IndicatorName = IndicatorName.HullEstimate;

        return stockData;
    }


    /// <summary>
    /// Calculates the Hampel Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="scalingFactor"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateHampelFilter(this StockData stockData, int length = 14, double scalingFactor = 3)
    {
        length = Math.Max(1, length);
        List<double> tempList = new(stockData.Count);
        List<double> hfList = new(stockData.Count);
        List<double> hfEmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        using var tempMedian = new RollingMedian(length);
        var deviations = new double[length];
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var alpha = (double)2 / (length + 1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevValue = GetLastOrDefault(tempList);
            var currentValue = inputList[i];
            tempList.Add(currentValue);

            tempMedian.Add(currentValue);
            var sampleMedian = tempMedian.Median;
            var absDiff = Math.Abs(currentValue - sampleMedian);
            var used = Math.Min(i + 1, length);
            for (var j = 0; j < used; j++) deviations[j] = Math.Abs(inputList[i - j] - sampleMedian);
            Array.Sort(deviations, 0, used);
            var mad = (deviations[(used - 1) / 2] + deviations[used / 2]) / 2;
            var hf = absDiff <= scalingFactor * mad ? currentValue : sampleMedian;
            hfList.Add(hf);

            var prevHfEma = GetLastOrDefault(hfEmaList);
            var hfEma = (alpha * hf) + ((1 - alpha) * prevHfEma);
            hfEmaList.Add(hfEma);

            var signal = GetCompareSignal(currentValue - hfEma, prevValue - prevHfEma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Hf", hfEmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(hfEmaList);
        stockData.IndicatorName = IndicatorName.HampelFilter;

        return stockData;
    }


    /// <summary>
    /// Calculates the Hybrid Convolution Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateHybridConvolutionFilter(this StockData stockData, int length = 14)
    {
        List<double> outputList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            var prevOutput = i >= 1 ? outputList[i - 1] : currentValue;
            double output = 0;
            for (var j = 1; j <= length; j++)
            {
                // Both cosine arguments are the window position scaled by pi, and neither is clamped. That
                // is what makes the weights d telescope across the window to exactly sign(length) - sign(0),
                // which is 1, so a constant series is a fixed point of the blend. With pi missing from the
                // second argument and both clamped to [0.01, 0.99], the weights summed to about 1.93 instead
                // and the filter settled at 96.54 on a series held at 50.
                var sign = 0.5 * (1 - Math.Cos((double)j / length * Math.PI));
                var d = sign - (0.5 * (1 - Math.Cos((double)(j - 1) / length * Math.PI)));
                var prevValue = i >= j - 1 ? inputList[i - (j - 1)] : 0;
                output += ((sign * prevOutput) + ((1 - sign) * prevValue)) * d;
            }
            outputList.Add(output);

            var signal = GetCompareSignal(currentValue - output, prevVal - prevOutput);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Hcf", outputList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(outputList);
        stockData.IndicatorName = IndicatorName.HybridConvolutionFilter;

        return stockData;
    }


    /// <summary>
    /// Calculates the Fibonacci Weighted Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateFibonacciWeightedMovingAverage(this StockData stockData, int length = 14)
    {
        List<double> fibonacciWmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        using var mean = new FibonacciWindowMean(length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            var prevFwma = GetLastOrDefault(fibonacciWmaList);
            var fwma = mean.Next(currentValue, true);
            fibonacciWmaList.Add(fwma);

            var signal = GetCompareSignal(currentValue - fwma, prevVal - prevFwma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Fwma", fibonacciWmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(fibonacciWmaList);
        stockData.IndicatorName = IndicatorName.FibonacciWeightedMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Farey Sequence Weighted Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateFareySequenceWeightedMovingAverage(this StockData stockData, int length = 5)
    {
        List<double> fswmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        using var mean = new FareyWindowMean(length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            var prevFswma = GetLastOrDefault(fswmaList);
            var fswma = mean.Next(currentValue, true);
            fswmaList.Add(fswma);

            var signal = GetCompareSignal(currentValue - fswma, prevVal - prevFswma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Fswma", fswmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(fswmaList);
        stockData.IndicatorName = IndicatorName.FareySequenceWeightedMovingAverage;

        return stockData;
    }

    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateFallingRisingFilter(this StockData stockData, int length = 14)
    {
        length = Math.Max(2, length);
        List<double> tempList = new(stockData.Count);
        List<double> aList = new(stockData.Count);
        List<double> errorList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingMinMax tempWindow = new(length);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var alpha = (double)2 / (length + 1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevA = i >= 1 ? aList[i - 1] : 0;
            var prevError = i >= 1 ? errorList[i - 1] : 0;

            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            tempList.Add(prevValue);
            tempWindow.Add(prevValue);

            var beta = currentValue > tempWindow.Max || currentValue < tempWindow.Min ? 1 : alpha;
            var a = prevA + (alpha * prevError) + (beta * prevError);
            aList.Add(a);

            var error = currentValue - a;
            errorList.Add(error);

            var signal = GetCompareSignal(error, prevError);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Frf", aList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(aList);
        stockData.IndicatorName = IndicatorName.FallingRisingFilter;

        return stockData;
    }


    /// <summary>
    /// Calculates the Fisher Least Squares Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateFisherLeastSquaresMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 100)
    {
        List<double> bList = new(stockData.Count);
        List<double> indexList = new(stockData.Count);
        List<double> diffList = new(stockData.Count);
        List<double> absDiffList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum absDiffSum = new();
        RollingSum diffSum = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var stdDevSrcList = GetStandardDeviationList(inputList, length);
        var smaSrcList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            double index = i;
            indexList.Add(index);
        }

        stockData.SetCustomValues(indexList);
        var indexStdDevList = GetStandardDeviationList(indexList, length);
        var indexSmaList = GetMovingAverageList(stockData, maType, length, indexList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var stdDevSrc = stdDevSrcList[i];
            var indexStdDev = indexStdDevList[i];
            var currentValue = inputList[i];
            var prevB = i >= 1 ? bList[i - 1] : currentValue;
            var indexSma = indexSmaList[i];
            var sma = smaSrcList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var diff = currentValue - prevB;
            diffList.Add(diff);
            diffSum.Add(diff);

            var absDiff = Math.Abs(diff);
            absDiffList.Add(absDiff);
            absDiffSum.Add(absDiff);

            var e = absDiffSum.Average(length);
            var z = e != 0 ? diffSum.Average(length) / e : 0;
            var r = Exp(2 * z) + 1 != 0 ? (Exp(2 * z) - 1) / (Exp(2 * z) + 1) : 0;
            var a = indexStdDev != 0 && r != 0 ? (i - indexSma) / indexStdDev * r : 0;

            var b = sma + (a * stdDevSrc);
            bList.Add(b);

            var signal = GetCompareSignal(currentValue - b, prevValue - prevB);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Flsma", bList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(bList);
        stockData.IndicatorName = IndicatorName.FisherLeastSquaresMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Kaufman Adaptive Least Squares Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateKaufmanAdaptiveLeastSquaresMovingAverage(this StockData stockData,
        MovingAvgType maType = MovingAvgType.KaufmanAdaptiveMovingAverage, int length = 100)
    {
        List<double> kalsmaList = new(stockData.Count);
        List<double> indexList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var kamaList = CalculateKaufmanAdaptiveCorrelationOscillator(stockData, maType, length);
        var indexStList = kamaList.ChainedOutputs["IndexSt"];
        var srcStList = kamaList.ChainedOutputs["SrcSt"];
        var rList = kamaList.ChainedOutputs["Kaco"];
        var srcMaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            double index = i;
            indexList.Add(index);
        }

        var indexMaList = GetMovingAverageList(stockData, maType, length, indexList);
        using var moments = maType == MovingAvgType.KaufmanAdaptiveMovingAverage && !Builder.Compute.ComponentAverage.HasOverrides
            ? new Streaming.KaufmanRegressionMoments(length) : null;
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var indexSt = indexStList[i];
            var srcSt = srcStList[i];
            var srcMa = srcMaList[i];
            var indexMa = indexMaList[i];
            var r = rList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var alpha = indexSt != 0 ? srcSt / indexSt * r : 0;
            var beta = srcMa - (alpha * indexMa);

            var prevKalsma = GetLastOrDefault(kalsmaList);
            var kalsma = moments is null ? (alpha * i) + beta
                : moments.Next(currentValue, true, out _, out _, out _);
            kalsmaList.Add(kalsma);

            var signal = GetCompareSignal(currentValue - kalsma, prevValue - prevKalsma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Kalsma", kalsmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(kalsmaList);
        stockData.IndicatorName = IndicatorName.KaufmanAdaptiveLeastSquaresMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Kalman Smoother
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateKalmanSmoother(this StockData stockData, int length = 200)
    {
        List<double> veloList = new(stockData.Count);
        List<double> kfList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevKf = i >= 1 ? kfList[i - 1] : currentValue;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var dk = currentValue - prevKf;
            var smooth = prevKf + (dk * Sqrt((double)length / 10000 * 2));

            var prevVelo = i >= 1 ? veloList[i - 1] : 0;
            var velo = prevVelo + ((double)length / 10000 * dk);
            veloList.Add(velo);

            var kf = smooth + velo;
            kfList.Add(kf);

            var signal = GetCompareSignal(currentValue - kf, prevValue - prevKf);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ks", kfList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(kfList);
        stockData.IndicatorName = IndicatorName.KalmanSmoother;

        return stockData;
    }


    /// <summary>
    /// Calculates the linear regression.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateLinearRegression(this StockData stockData, int length = 14)
    {
        List<double> slopeList = new(stockData.Count);
        List<double> interceptList = new(stockData.Count);
        List<double> predictedTomorrowList = new(stockData.Count);
        List<double> predictedTodayList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        // The line through the trailing window, x counted from its first bar and fitted through the bars there
        // are until it fills; see ExactLinearFitWindow.
        using var regression = new ExactLinearFitWindow(length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentValue = inputList[i];

            var fit = regression.Next(currentValue, isFinal: true);
            var b = fit.Slope;
            slopeList.Add(b);

            // The intercept is still reported at bar 0 of the series, as it always was.
            var a = fit.GlobalIntercept;
            interceptList.Add(a);

            var predictedToday = fit.Last;
            predictedTodayList.Add(predictedToday);

            var prevPredictedNextDay = GetLastOrDefault(predictedTomorrowList);
            var predictedNextDay = fit.Next;
            predictedTomorrowList.Add(predictedNextDay);

            var signal = GetCompareSignal(currentValue - predictedNextDay, prevValue - prevPredictedNextDay, true);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "LinearRegression", predictedTodayList },
            { "PredictedTomorrow", predictedTomorrowList },
            { "Slope", slopeList },
            { "Intercept", interceptList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(predictedTodayList);
        stockData.IndicatorName = IndicatorName.LinearRegression;

        return stockData;
    }


    /// <summary>
    /// Calculates the Elastic Volume Weighted Moving Average V1
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="mult"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateElasticVolumeWeightedMovingAverageV1(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 40, double mult = 20)
    {
        var (input, _, _, _, volumes) = GetInputValuesList(stockData);
        List<double> line = new(stockData.Count); List<Signal>? signals = CreateSignalsList(stockData);
        using var window = new ElasticVolumeAverageWindow(maType, length, mult, initializeFallback: false);
        if (Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType))
        {
            var average = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(volumes), length)?.ToList() ?? GetMovingAverageList(stockData, maType, length, volumes);
            for (var i = 0; i < input.Count; i++) line.Add(window.NextWithAverage(input[i], volumes[i], new RocBankValue(average[i]), true));
        }
        else for (var i = 0; i < input.Count; i++) line.Add(window.Next(input[i], volumes[i], true));
        for (var i = 0; i < input.Count; i++) signals?.Add(GetCompareSignal(input[i] - line[i], i == 0 ? -input[0] : input[i - 1] - line[i - 1]));
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Evwma", line } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.ElasticVolumeWeightedMovingAverageV1;
        return stockData;
    }


    /// <summary>
    /// Calculates the Elastic Volume Weighted Moving Average V2
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateElasticVolumeWeightedMovingAverageV2(this StockData stockData, int length = 14)
    {
        var (input, _, _, _, volumes) = GetInputValuesList(stockData);
        List<double> line = new(stockData.Count); List<Signal>? signals = CreateSignalsList(stockData);
        using var window = new ElasticVolumeWindow(length);
        for (var i = 0; i < input.Count; i++)
        {
            var value = window.Next(input[i], volumes[i], true); line.Add(value);
            signals?.Add(GetCompareSignal(input[i] - value, i == 0 ? -input[0] : input[i - 1] - line[i - 1]));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Evwma", line } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.ElasticVolumeWeightedMovingAverageV2;
        return stockData;
    }


    /// <summary>
    /// Calculates the Equity Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEquityMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        List<double> line = new(stockData.Count); List<Signal>? signals = CreateSignalsList(stockData);
        using var window = new EquityWindow(maType, length, initializeFallback: false);
        if (Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType))
        {
            var average = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(input), length)?.ToList() ?? GetMovingAverageList(stockData, maType, length, input);
            for (var i = 0; i < input.Count; i++) line.Add(window.NextWithAverage(input[i], average[i], true));
        }
        else foreach (var price in input) line.Add(window.Next(price, true));
        for (var i = 0; i < input.Count; i++) signals?.Add(GetCompareSignal(input[i] - line[i], i == 0 ? -input[0] : input[i - 1] - line[i - 1]));
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Eqma", line } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.EquityMovingAverage;
        return stockData;
    }


    /// <summary>
    /// Calculates the Edge Preserving Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEdgePreservingFilter(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 200, 
        int smoothLength = 50)
    {
        List<double> osList = new(stockData.Count);
        List<double> absOsList = new(stockData.Count);
        List<double> hList = new(stockData.Count);
        List<double> aList = new(stockData.Count);
        List<double> bList = new(stockData.Count);
        List<double> cList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var sma = smaList[i];

            var os = currentValue - sma;
            osList.Add(os);

            var absOs = Math.Abs(os);
            absOsList.Add(absOs);
        }

        stockData.SetCustomValues(absOsList);
        var pList = CalculateLinearRegression(stockData, smoothLength).ChainedValues;
        var (highestList, _) = GetMaxAndMinValuesList(pList, length);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var p = pList[i];
            var highest = highestList[i];
            var os = osList[i];

            var prevH = GetLastOrDefault(hList);
            var h = highest != 0 ? p / highest : 0;
            hList.Add(h);

            // A numerically flat regression peak must not create new reset edges.
            double cnd = Math.Abs(h - 1) <= 1e-12 && Math.Abs(prevH - 1) > 1e-12 ? 1 : 0;
            double sign = cnd == 1 && os < 0 ? 1 : cnd == 1 && os > 0 ? -1 : 0;
            var condition = sign != 0;

            var prevA = i >= 1 ? aList[i - 1] : 1;
            var a = condition ? 1 : prevA + 1;
            aList.Add(a);

            var prevB = i >= 1 ? bList[i - 1] : currentValue;
            var b = a == 1 ? currentValue : prevB + currentValue;
            bList.Add(b);

            var prevC = GetLastOrDefault(cList);
            var c = a != 0 ? b / a : 0;
            cList.Add(c);

            var signal = GetCompareSignal(currentValue - c, prevValue - prevC);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Epf", cList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(cList);
        stockData.IndicatorName = IndicatorName.EdgePreservingFilter;

        return stockData;
    }
}

