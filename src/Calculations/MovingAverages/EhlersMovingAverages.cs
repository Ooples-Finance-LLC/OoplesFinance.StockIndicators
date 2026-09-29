using OoplesFinance.StockIndicators.Compatibility;
using OoplesFinance.StockIndicators.Core;

namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the ehlers mother of adaptive moving averages.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="fastAlpha">The fast alpha.</param>
    /// <param name="slowAlpha">The slow alpha.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersMotherOfAdaptiveMovingAverages(this StockData stockData, double fastAlpha = 0.5, double slowAlpha = 0.05)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); var window = new MamaWindow(fastAlpha, slowAlpha); var fama = new List<double>(input.Count); var mama = new List<double>(input.Count); var inphase = new List<double>(input.Count); var quadrature = new List<double>(input.Count); var period = new List<double>(input.Count); var smooth = new List<double>(input.Count); var real = new List<double>(input.Count); var imaginary = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        foreach (var price in input) { var point = window.Next(price, true); var value = point.Values; fama.Add(value.Fama); mama.Add(value.Mama); inphase.Add(value.I1); quadrature.Add(value.Q1); period.Add(value.SmoothPeriod); smooth.Add(value.Smooth); real.Add(value.Real); imaginary.Add(value.Imag); signals?.Add(point.Signal); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Fama", fama }, { "Mama", mama }, { "I1", inphase }, { "Q1", quadrature }, { "SmoothPeriod", period }, { "Smooth", smooth }, { "Real", real }, { "Imag", imaginary } }); stockData.SetSignals(signals); stockData.SetCustomValues(mama); stockData.IndicatorName = IndicatorName.EhlersMotherOfAdaptiveMovingAverages; return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Fractal Adaptive Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersFractalAdaptiveMovingAverage(this StockData stockData, int length = 20)
    {
        var window = new FramaWindow(length);
        List<double> filterList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i]; var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevFilter = i >= 1 ? filterList[i - 1] : currentValue;
            var filter = window.Next(currentValue, stockData.HighPrices[i], stockData.LowPrices[i], true); filterList.Add(filter);

            var signal = GetCompareSignal(currentValue - filter, prevValue - prevFilter);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Fama", filterList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(filterList);
        stockData.IndicatorName = IndicatorName.EhlersFractalAdaptiveMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Median Average Adaptive Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="threshold"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersMedianAverageAdaptiveFilter(this StockData stockData, int length = 39, double threshold = 0.002)
    {
        List<double> filterList = new(stockData.Count);
        List<double> value2List = new(stockData.Count);
        List<double> smthList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var windowTree = new OrderStatisticTree();
        var removedValues = new List<double>();

        static double GetMedian(OrderStatisticTree tree, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            if ((count & 1) == 1)
            {
                return tree.SelectByRank((count + 1) / 2);
            }

            var left = tree.SelectByRank(count / 2);
            var right = tree.SelectByRank((count / 2) + 1);
            return (left + right) / 2;
        }

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentPrice = inputList[i];
            var prevP1 = i >= 1 ? inputList[i - 1] : 0;
            var prevP2 = i >= 2 ? inputList[i - 2] : 0;
            var prevP3 = i >= 3 ? inputList[i - 3] : 0;

            var smth = (currentPrice + (2 * prevP1) + (2 * prevP2) + prevP3) / 6;
            smthList.Add(smth);
            windowTree.Insert(smth);
            if (smthList.Count > length)
            {
                windowTree.Remove(smthList[smthList.Count - length - 1]);
            }

            var len = length;
            double value3 = 0.2, value2 = 0, prevV2 = GetLastOrDefault(value2List), alpha;
            var available = Math.Min(length, smthList.Count);
            var windowStart = smthList.Count - available;
            var removedOffset = 0;
            removedValues.Clear();
            while (value3 > threshold && len > 0)
            {
                alpha = (double)2 / (len + 1);
                var value1 = GetMedian(windowTree, Math.Min(len, available));
                value2 = (alpha * smth) + ((1 - alpha) * prevV2);
                value3 = value1 != 0 ? Math.Abs(value1 - value2) / Math.Abs(value1) : value3;
                len -= 2;

                if (value3 > threshold && len > 0)
                {
                    // Startup can cross from an even available count to an odd requested count.
                    // Remove only the actual excess, so the median ranks describe the retained window.
                    var excess = available - removedOffset - Math.Min(len, available);
                    for (var drop = 0; drop < excess; drop++)
                    {
                        var expired = smthList[windowStart + removedOffset++];
                        windowTree.Remove(expired);
                        removedValues.Add(expired);
                    }
                }
            }
            foreach (var removedValue in removedValues)
            {
                windowTree.Insert(removedValue);
            }
            value2List.Add(value2);

            len = len < 3 ? 3 : len;
            alpha = (double)2 / (len + 1);

            var prevFilter = GetLastOrDefault(filterList);
            var filter = (alpha * smth) + ((1 - alpha) * prevFilter);
            filterList.Add(filter);

            var signal = GetCompareSignal(currentPrice - filter, prevP1 - prevFilter);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Maaf", filterList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(filterList);
        stockData.IndicatorName = IndicatorName.EhlersMedianAverageAdaptiveFilter;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers 2 Pole Super Smoother Filter V2
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlers2PoleSuperSmootherFilterV2(this StockData stockData, int length = 10)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        var window = new TwoPoleWindow(length, 3); List<double> output = new(stockData.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < stockData.Count; i++)
        {
            var value = window.Next(input[i], true); output.Add(value);
            signals?.Add(GetCompareSignal(input[i] - value, i == 0 ? 0 : input[i - 1] - output[i - 1]));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "E2ssf", output } });
        stockData.SetSignals(signals); stockData.SetCustomValues(output);
        stockData.IndicatorName = IndicatorName.Ehlers2PoleSuperSmootherFilterV2;
        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers 3 Pole Super Smoother Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlers3PoleSuperSmootherFilter(this StockData stockData, int length = 20)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        var window = new ThreePoleWindow(length, 2); List<double> output = new(stockData.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < stockData.Count; i++)
        {
            var value = window.Next(input[i], true); output.Add(value);
            signals?.Add(GetCompareSignal(input[i] - value, i == 0 ? 0 : input[i - 1] - output[i - 1]));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "E3ssf", output } });
        stockData.SetSignals(signals); stockData.SetCustomValues(output);
        stockData.IndicatorName = IndicatorName.Ehlers3PoleSuperSmootherFilter;
        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers 2 Pole Butterworth Filter V1
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlers2PoleButterworthFilterV1(this StockData stockData, int length = 10)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        var window = new TwoPoleWindow(length, 0); List<double> output = new(stockData.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < stockData.Count; i++)
        {
            var value = window.Next(input[i], true); output.Add(value);
            signals?.Add(GetCompareSignal(input[i] - value, i == 0 ? 0 : input[i - 1] - output[i - 1]));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "E2bf", output } });
        stockData.SetSignals(signals); stockData.SetCustomValues(output);
        stockData.IndicatorName = IndicatorName.Ehlers2PoleButterworthFilterV1;
        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers 2 Pole Butterworth Filter V2
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlers2PoleButterworthFilterV2(this StockData stockData, int length = 15)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        var window = new TwoPoleWindow(length, 1); List<double> output = new(stockData.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < stockData.Count; i++)
        {
            var value = window.Next(input[i], true); output.Add(value);
            signals?.Add(GetCompareSignal(input[i] - value, i == 0 ? 0 : input[i - 1] - output[i - 1]));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "E2bf", output } });
        stockData.SetSignals(signals); stockData.SetCustomValues(output);
        stockData.IndicatorName = IndicatorName.Ehlers2PoleButterworthFilterV2;
        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers 3 Pole Butterworth Filter V1
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlers3PoleButterworthFilterV1(this StockData stockData, int length = 10)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        var window = new ThreePoleWindow(length, 0); List<double> output = new(stockData.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < stockData.Count; i++)
        {
            var value = window.Next(input[i], true); output.Add(value);
            signals?.Add(GetCompareSignal(input[i] - value, i == 0 ? 0 : input[i - 1] - output[i - 1]));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "E3bf", output } });
        stockData.SetSignals(signals); stockData.SetCustomValues(output);
        stockData.IndicatorName = IndicatorName.Ehlers3PoleButterworthFilterV1;
        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers 3 Pole Butterworth Filter V2
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlers3PoleButterworthFilterV2(this StockData stockData, int length = 15)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        var window = new ThreePoleWindow(length, 1); List<double> output = new(stockData.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < stockData.Count; i++)
        {
            var value = window.Next(input[i], true); output.Add(value);
            signals?.Add(GetCompareSignal(input[i] - value, i == 0 ? 0 : input[i - 1] - output[i - 1]));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "E3bf", output } });
        stockData.SetSignals(signals); stockData.SetCustomValues(output);
        stockData.IndicatorName = IndicatorName.Ehlers3PoleButterworthFilterV2;
        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Gaussian Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="poles">
    /// Which of Ehlers' one- to four-pole filters is the single series; all four are always published as Egf1 to
    /// Egf4, and the four-pole filter stays the default.
    /// </param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersGaussianFilter(this StockData stockData, int length = 14, int poles = 4)
    {
        var resolvedPoles = Math.Min(Math.Max(poles, 1), 4);
        List<double> gf1List = new(stockData.Count);
        List<double> gf2List = new(stockData.Count);
        List<double> gf3List = new(stockData.Count);
        List<double> gf4List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var alpha1 = EhlersGaussian.Gain(length, 1);
        var alpha2 = EhlersGaussian.Gain(length, 2);
        var alpha3 = EhlersGaussian.Gain(length, 3);
        var alpha4 = EhlersGaussian.Gain(length, 4);
        var stages1 = new double[1];
        var stages2 = new double[2];
        var stages3 = new double[3];
        var stages4 = new double[4];

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevGf1 = i >= 1 ? gf1List[i - 1] : 0;
            var prevGf2_1 = i >= 1 ? gf2List[i - 1] : 0;
            var prevGf3_1 = i >= 1 ? gf3List[i - 1] : 0;
            var prevGf4_1 = i >= 1 ? gf4List[i - 1] : 0;
            var gf1 = EhlersGaussian.Next(currentValue, alpha1, stages1, true);
            var gf2 = EhlersGaussian.Next(currentValue, alpha2, stages2, true);
            var gf3 = EhlersGaussian.Next(currentValue, alpha3, stages3, true);
            var gf4 = EhlersGaussian.Next(currentValue, alpha4, stages4, true);
            gf1List.Add(gf1);
            gf2List.Add(gf2);
            gf3List.Add(gf3);
            gf4List.Add(gf4);

            var gf = resolvedPoles switch { 1 => gf1, 2 => gf2, 3 => gf3, _ => gf4 };
            var prevGf = resolvedPoles switch { 1 => prevGf1, 2 => prevGf2_1, 3 => prevGf3_1, _ => prevGf4_1 };
            var signal = GetCompareSignal(currentValue - gf, prevValue - prevGf);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Egf1", gf1List },
            { "Egf2", gf2List },
            { "Egf3", gf3List },
            { "Egf4", gf4List }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(resolvedPoles switch { 1 => gf1List, 2 => gf2List, 3 => gf3List, _ => gf4List });
        stockData.IndicatorName = IndicatorName.EhlersGaussianFilter;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Recursive Median Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersRecursiveMedianFilter(this StockData stockData, int length1 = 5, int length2 = 12)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); var window = new RecursiveMedianWindow(length1, length2);
        var output = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            var value = window.Next(input[i], true);
            signals?.Add(GetCompareSignal(input[i] - value, i == 0 ? 0 : input[i - 1] - output[i - 1])); output.Add(value);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Ermf", output } });
        stockData.SetSignals(signals); stockData.SetCustomValues(output); stockData.IndicatorName = IndicatorName.EhlersRecursiveMedianFilter;
        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Super Smoother Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersSuperSmootherFilter(this StockData stockData, int length = 10)
    {
        var engine = new Streaming.EhlersSuperSmootherFilterEngine(length); var (input, _, _, _, _) = GetInputValuesList(stockData); var values = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++) { var point = engine.Step(input[i], true); values.Add(point.Value); signals?.Add(point.Signal); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Essf", values } }); stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.EhlersSuperSmootherFilter; return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers 2 Pole Super Smoother Filter V1
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlers2PoleSuperSmootherFilterV1(this StockData stockData, int length = 15)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        var window = new TwoPoleWindow(length, 2); List<double> output = new(stockData.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < stockData.Count; i++)
        {
            var value = window.Next(input[i], true); output.Add(value);
            signals?.Add(GetCompareSignal(input[i] - value, i == 0 ? 0 : input[i - 1] - output[i - 1]));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Essf", output } });
        stockData.SetSignals(signals); stockData.SetCustomValues(output);
        stockData.IndicatorName = IndicatorName.Ehlers2PoleSuperSmootherFilterV1;
        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Average Error Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersAverageErrorFilter(this StockData stockData, int length = 27)
    {
        List<double> filtList = new(stockData.Count); List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData); var window = new AverageErrorWindow(length);
        for (var i = 0; i < stockData.Count; i++)
        {
            var previous = GetLastOrDefault(filtList); var value = window.Next(inputList[i], true); filtList.Add(value);
            signalsList?.Add(GetCompareSignal(inputList[i] - value, (i == 0 ? 0 : inputList[i - 1]) - previous));
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Eaef", filtList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(filtList);
        stockData.IndicatorName = IndicatorName.EhlersAverageErrorFilter;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Laguerre Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="alpha"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersLaguerreFilter(this StockData stockData, double alpha = 0.2)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        List<double> line = new(stockData.Count); List<Signal>? signals = CreateSignalsList(stockData);
        var window = new LaguerreFilterWindow(alpha); double previousFir = 0;
        for (var i = 0; i < input.Count; i++)
        {
            var value = window.Next(input[i], true);
            var firSum = new ExactMeanAccumulator(); firSum.Add(input[i]);
            if (i > 0) firSum.Add(input[i - 1], 2); if (i > 1) firSum.Add(input[i - 2], 2); if (i > 2) firSum.Add(input[i - 3]);
            var fir = firSum.Mean(6);
            signals?.Add(GetCompareSignal(value - fir, i == 0 ? 0 : line[i - 1] - previousFir));
            line.Add(value); previousFir = fir;
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Elf", line } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.EhlersLaguerreFilter;
        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Adaptive Laguerre Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersAdaptiveLaguerreFilter(this StockData stockData, int length1 = 14, int length2 = 5)
    {
        var window = new AdaptiveLaguerreWindow(length1, length2);
        List<double> filterList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i]; var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevFilter = i >= 1 ? filterList[i - 1] : currentValue;
            var filter = window.Next(currentValue, true); filterList.Add(filter);

            var signal = GetCompareSignal(currentValue - filter, prevValue - prevFilter);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ealf", filterList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(filterList);
        stockData.IndicatorName = IndicatorName.EhlersAdaptiveLaguerreFilter;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Leading Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="alpha1"></param>
    /// <param name="alpha2"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersLeadingIndicator(this StockData stockData, double alpha1 = 0.25, double alpha2 = 0.33)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        List<double> line = new(stockData.Count); var signals = CreateSignalsList(stockData);
        var window = new LeadingWindow(alpha1, alpha2);
        for (var i = 0; i < input.Count; i++)
        {
            var value = window.Next(input[i], true); line.Add(value);
            signals?.Add(GetCompareSignal(input[i] - value, i == 0 ? 0 : input[i - 1] - line[i - 1]));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Eli", line } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.EhlersLeadingIndicator;
        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Optimum Elliptic Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersOptimumEllipticFilter(this StockData stockData)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        List<double> line = new(stockData.Count); List<Signal>? signals = CreateSignalsList(stockData);
        var window = new EllipticWindow(false);
        for (var i = 0; i < input.Count; i++)
        {
            var value = window.Next(input[i], true); line.Add(value);
            signals?.Add(GetCompareSignal(input[i] - value, i == 0 ? 0 : input[i - 1] - line[i - 1]));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Emoef", line } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.EhlersOptimumEllipticFilter;
        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Modified Optimum Elliptic Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersModifiedOptimumEllipticFilter(this StockData stockData)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        List<double> line = new(stockData.Count); List<Signal>? signals = CreateSignalsList(stockData);
        var window = new EllipticWindow(true);
        for (var i = 0; i < input.Count; i++)
        {
            var value = window.Next(input[i], true); line.Add(value);
            signals?.Add(GetCompareSignal(input[i] - value, i == 0 ? 0 : input[i - 1] - line[i - 1]));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Emoef", line } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.EhlersModifiedOptimumEllipticFilter;
        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersFilter(this StockData stockData, int length1 = 15, int length2 = 5)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); var window = new EhlersDistanceFilterWindow(length1, length2);
        var output = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            var value = window.Next(input[i], true); signals?.Add(GetCompareSignal(input[i]-value, i==0 ? 0 : input[i-1]-output[i-1])); output.Add(value);
        }
        stockData.SetOutputValues(() => new Dictionary<string,List<double>> { { "Ef", output } });
        stockData.SetSignals(signals); stockData.SetCustomValues(output); stockData.IndicatorName = IndicatorName.EhlersFilter;
        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Distance Coefficient Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersDistanceCoefficientFilter(this StockData stockData,int length=14)
    {
        var(input,_,_,_,_)=GetInputValuesList(stockData);var window=new DistanceCoefficientWindow(length);
        var output=new List<double>(input.Count);var signals=CreateSignalsList(stockData);
        for(var i=0;i<input.Count;i++)
        {
            var value=window.Next(input[i],true);signals?.Add(GetCompareSignal(input[i]-value,i==0?0:input[i-1]-output[i-1]));output.Add(value);
        }
        stockData.SetOutputValues(()=>new Dictionary<string,List<double>>{{"Edcf",output}});
        stockData.SetSignals(signals);stockData.SetCustomValues(output);stockData.IndicatorName=IndicatorName.EhlersDistanceCoefficientFilter;
        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Finite Impulse Response Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="coef1"></param>
    /// <param name="coef2"></param>
    /// <param name="coef3"></param>
    /// <param name="coef4"></param>
    /// <param name="coef5"></param>
    /// <param name="coef6"></param>
    /// <param name="coef7"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersFiniteImpulseResponseFilter(this StockData stockData, double coef1 = 1, double coef2 = 3.5, double coef3 = 4.5,
        double coef4 = 3, double coef5 = 0.5, double coef6 = -0.5, double coef7 = -1.5)
    {
        List<double> line = new(stockData.Count); List<Signal>? signals = CreateSignalsList(stockData);
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        using var window = new EhlersFirWindow(coef1, coef2, coef3, coef4, coef5, coef6, coef7);
        for (var i = 0; i < input.Count; i++)
        {
            var value = window.Next(input[i], true);
            signals?.Add(GetCompareSignal(input[i] - value, i == 0 ? 0 : input[i - 1] - line[i - 1])); line.Add(value);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Efirf", line } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.EhlersFiniteImpulseResponseFilter;
        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Infinite Impulse Response Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersInfiniteImpulseResponseFilter(this StockData stockData, int length = 14)
    {
        List<double> line = new(stockData.Count); List<Signal>? signals = CreateSignalsList(stockData);
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        using var window = new EhlersIirWindow(length);
        var alpha = 2d / (Math.Max(1, length) + 1d);
        var lag = Math.Min(530, Math.Max(2, (int)Math.Ceiling(1 / alpha - 1)));
        for (var i = 0; i < input.Count; i++)
        {
            var value = window.Next(input[i], true);
            signals?.Add(GetCompareSignal(input[i] - value, (i >= lag ? input[i - lag] : 0) - (i == 0 ? 0 : line[i - 1]))); line.Add(value);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Eiirf", line } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.EhlersInfiniteImpulseResponseFilter;
        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Deviation Scaled Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersDeviationScaledMovingAverage(this StockData stockData, 
        MovingAvgType maType = MovingAvgType.Ehlers2PoleSuperSmootherFilterV2, int fastLength = 20, int slowLength = 40)
    {
        List<double> edsma2PoleList = new(stockData.Count);
        List<double> zerosList = new(stockData.Count);
        List<double> avgZerosList = new(stockData.Count);
        List<double> scaledFilter2PoleList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var precise = maType == MovingAvgType.Ehlers2PoleSuperSmootherFilterV2 && !Builder.Compute.ComponentAverage.HasOverrides ? new DeviationScaledWindow(fastLength, slowLength) : null;
        if (precise == null)
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 2 ? inputList[i - 2] : 0;

            var prevZeros = GetLastOrDefault(zerosList);
            var zeros = MinPastValues(i, 2, currentValue - prevValue);
            zerosList.Add(zeros);

            var avgZeros = (zeros + prevZeros) / 2;
            avgZerosList.Add(avgZeros);
        }

        var ssf2PoleList = precise == null ? GetMovingAverageList(stockData, maType, fastLength, avgZerosList) : new List<double>();
        if (precise == null) stockData.SetCustomValues(ssf2PoleList);
        var ssf2PoleStdDevList = precise == null ? GetStandardDeviationList(ssf2PoleList, slowLength) : new List<double>();
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevEdsma2pole = GetLastOrDefault(edsma2PoleList);
            double edsma2Pole;
            if (precise != null) edsma2Pole = precise.Next(currentValue, true, out _);
            else
            {
                var currentSsf2Pole = ssf2PoleList[i]; var currentSsf2PoleStdDev = ssf2PoleStdDevList[i];
                var scaledFilter2Pole = currentSsf2PoleStdDev != 0 ? currentSsf2Pole / currentSsf2PoleStdDev : GetLastOrDefault(scaledFilter2PoleList);
                scaledFilter2PoleList.Add(scaledFilter2Pole);
                var alpha2Pole = MinOrMax(5 * Math.Abs(scaledFilter2Pole) / slowLength, .99, .01);
                edsma2Pole = alpha2Pole * currentValue + (1 - alpha2Pole) * prevEdsma2pole;
            }
            edsma2PoleList.Add(edsma2Pole);

            var signal = GetCompareSignal(currentValue - edsma2Pole, prevValue - prevEdsma2pole);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Edsma", edsma2PoleList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(edsma2PoleList);
        stockData.IndicatorName = IndicatorName.EhlersDeviationScaledMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Hann Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersHannMovingAverage(this StockData stockData, int length = 20)
    {
        List<double> filtList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        using var mean = new HannWindowMean(stockData.Count == 0 ? 1 : length);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevFilt = GetLastOrDefault(filtList);
            var filt = mean.Next(currentValue, true);
            filtList.Add(filt);
            signalsList?.Add(GetCompareSignal(currentValue - filt, prevValue - prevFilt));
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ehma", filtList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(filtList);
        stockData.IndicatorName = IndicatorName.EhlersHannMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Deviation Scaled Super Smoother
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersDeviationScaledSuperSmoother(this StockData stockData, MovingAvgType maType = MovingAvgType.EhlersHannMovingAverage,
        int length1 = 12, int length2 = 50)
    {
        if (DeviationSuperSmootherWindow.Supports(maType) && !Builder.Compute.ComponentAverage.HasOverrides)
        {
            var (selected, _, _, _, _) = GetInputValuesList(stockData); var window = new DeviationSuperSmootherWindow(maType, length1, length2);
            var values = new List<double>(stockData.Count); var signals = CreateSignalsList(stockData);
            for (var i = 0; i < stockData.Count; i++)
            {
                var value = window.Next(selected[i], true); values.Add(value);
                signals?.Add(GetCompareSignal(selected[i] - value, i == 0 ? 0 : selected[i - 1] - values[i - 1]));
            }
            stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Edsss", values } }); stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.EhlersDeviationScaledSuperSmoother; return stockData;
        }
        List<double> momList = new(stockData.Count);
        List<double> dsssList = new(stockData.Count);
        List<double> filtPowList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum filtPowSumWindow = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var hannLength = (int)Math.Ceiling(length1 / 1.4m);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var priorValue = i >= length1 ? inputList[i - length1] : 0;

            var mom = currentValue - priorValue;
            momList.Add(mom);
        }

        var filtList = GetMovingAverageList(stockData, maType, hannLength, momList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var filt = filtList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevDsss1 = i >= 1 ? dsssList[i - 1] : 0;
            var prevDsss2 = i >= 2 ? dsssList[i - 2] : 0;

            var filtPow = Pow(filt, 2);
            filtPowList.Add(filtPow);
            filtPowSumWindow.Add(filtPow);

            var filtPowMa = filtPowSumWindow.Average(length2);
            var rms = filtPowMa > 0 ? Sqrt(filtPowMa) : 0;
            var scaledFilt = rms != 0 ? filt / rms : 0;

            var (c1, c2, c3) = DeviationScaledSuperSmootherCoefficients(scaledFilt, length1);

            var dsss = (c1 * ((currentValue + prevValue) / 2)) + (c2 * prevDsss1) + (c3 * prevDsss2);
            dsssList.Add(dsss);

            var signal = GetCompareSignal(currentValue - dsss, prevValue - prevDsss1);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Edsss", dsssList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(dsssList);
        stockData.IndicatorName = IndicatorName.EhlersDeviationScaledSuperSmoother;

        return stockData;
    }

    /// <summary>
    /// The two-pole super smoother coefficients for a momentum already divided by its own RMS.
    /// </summary>
    /// <remarks>
    /// <para>
    /// scaledFilt is a momentum in units of its own RMS, so its natural magnitude is one. Zero is the
    /// single value these coefficients cannot take: a1 becomes exp(0) = 1, so c2 = 2, c3 = -1 and
    /// c1 = 1 - c2 - c3 = 0. That is a double integrator with both poles at z = 1 - it stops reading its
    /// input altogether and carries whatever straight line it already held. On a market that never moved
    /// the output drifted linearly and for ever, by 0.01286 per bar: 112.65 at bar 1000, 164.07 at 5000
    /// and 356.90 at 20000, the same slope across both spans.
    /// </para>
    /// <para>
    /// A flat market arrives at zero two ways - an all-zero window making rms zero, and a zero filt
    /// against a still non-zero rms during warmup - so the floor belongs on the ratio rather than on rms.
    /// With no deviation to scale by there is no adaptive information to act on, and a magnitude of one
    /// is the neutral: it gives exactly the coefficients CalculateEhlersSuperSmootherFilter uses at this
    /// length.
    /// </para>
    /// </remarks>
    internal static (double C1, double C2, double C3) DeviationScaledSuperSmootherCoefficients(double scaledFilt, int length1)
    {
        var scaledAbs = Math.Abs(scaledFilt);
        scaledAbs = scaledAbs != 0 ? scaledAbs : 1;
        var a1 = Exp(-MathHelper.Sqrt2 * Math.PI * scaledAbs / length1);
        var b1 = 2 * a1 * Math.Cos(MathHelper.Sqrt2 * Math.PI * scaledAbs / length1);
        var c2 = b1;
        var c3 = -a1 * a1;

        return (1 - c2 - c3, c2, c3);
    }

    /// <summary>
    /// Calculates the Ehlers Zero Lag Exponential Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersZeroLagExponentialMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 14)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        var custom = Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType);
        using var window = new EhlersZeroLagWindow(maType, length, initializeFallback: !custom);
        List<double> line = new(stockData.Count); List<Signal>? signals = CreateSignalsList(stockData);
        if (custom)
        {
            var corrected = input.Select(price => window.Correct(price, true).Publish()).ToList();
            line = GetMovingAverageList(stockData, maType, length, corrected);
        }
        else foreach (var price in input) line.Add(window.Next(price, true));
        for (var i = 0; i < input.Count; i++)
            signals?.Add(GetCompareSignal(input[i] - line[i], i == 0 ? 0 : input[i - 1] - line[i - 1]));
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Ezlema", line } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.EhlersZeroLagExponentialMovingAverage;
        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Variable Index Dynamic Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersVariableIndexDynamicAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage, 
        int fastLength = 9, int slowLength = 30)
    {
        List<double> vidyaList = new(stockData.Count);
        List<double> longPowList = new(stockData.Count);
        List<double> shortPowList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum shortPowSumWindow = new();
        RollingSum longPowSumWindow = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var shortAvgList = GetMovingAverageList(stockData, maType, fastLength, inputList);
        var longAvgList = GetMovingAverageList(stockData, maType, slowLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var shortAvg = shortAvgList[i];
            var longAvg = longAvgList[i];

            var shortPow = Pow(currentValue - shortAvg, 2);
            shortPowList.Add(shortPow);
            shortPowSumWindow.Add(shortPow);

            var shortMa = shortPowSumWindow.Average(fastLength);
            var shortRms = shortMa > 0 ? Sqrt(shortMa) : 0;

            var longPow = Pow(currentValue - longAvg, 2);
            longPowList.Add(longPow);
            longPowSumWindow.Add(longPow);

            var longMa = longPowSumWindow.Average(slowLength);
            var longRms = longMa > 0 ? Sqrt(longMa) : 0;
            var kk = longRms != 0 ? MinOrMax(0.2 * shortRms / longRms, 0.99, 0.01) : 0;

            var prevVidya = i > 0 ? vidyaList[i - 1] : currentValue;
            var vidya = prevVidya + (kk * (currentValue - prevVidya));
            vidyaList.Add(vidya);

            var signal = GetCompareSignal(currentValue - vidya, prevValue - prevVidya);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Evidya", vidyaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(vidyaList);
        stockData.IndicatorName = IndicatorName.EhlersVariableIndexDynamicAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Kaufman Adaptive Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersKaufmanAdaptiveMovingAverage(this StockData stockData, int length = 20)
    {
        List<double> line = new(stockData.Count); List<Signal>? signals = CreateSignalsList(stockData);
        var (input, _, _, _, _) = GetInputValuesList(stockData); var window = new EhlersKaufmanWindow(length);
        for (var i = 0; i < input.Count; i++)
        {
            var value = window.Next(input[i], true);
            signals?.Add(GetCompareSignal(input[i] - value, i == 0 ? 0 : input[i - 1] - line[i - 1])); line.Add(value);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Ekama", line } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.EhlersKaufmanAdaptiveMovingAverage;
        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers All Pass Phase Shifter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="qq"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersAllPassPhaseShifter(this StockData stockData, int length = 20, double qq = 0.5)
    {
        List<double> phaserList = new(stockData.Count); List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData); var window = new AllPassPhaseWindow(length, qq);
        for (var i = 0; i < stockData.Count; i++)
        {
            var previous = GetLastOrDefault(phaserList); var value = window.Next(inputList[i], true); phaserList.Add(value);
            signalsList?.Add(GetCompareSignal(inputList[i] - value, (i == 0 ? 0 : inputList[i - 1]) - previous));
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Eapps", phaserList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(phaserList);
        stockData.IndicatorName = IndicatorName.EhlersAllPassPhaseShifter;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Chebyshev Low Pass Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersChebyshevLowPassFilter(this StockData stockData)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        var windows = Enumerable.Range(0, 9).Select(w => new ChebyshevWaveWindow(w)).ToArray();
        var output = Enumerable.Range(0, 9).Select(_ => new List<double>(input.Count)).ToArray(); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            for (var wave = 0; wave < 9; wave++) output[wave].Add(windows[wave].Next(input[i], true));
            signals?.Add(GetCompareSignal(input[i] - output[0][i], i == 0 ? 0 : input[i - 1] - output[0][i - 1]));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> {
            { "Eclpf-2", output[0] }, { "Eclpf-1", output[1] }, { "Eclpf0", output[2] },
            { "Eclpf1", output[3] }, { "Eclpf2", output[4] }, { "Eclpf3", output[5] },
            { "Eclpf4", output[6] }, { "Eclpf5", output[7] }, { "Eclpf6", output[8] }
        });
        stockData.SetSignals(signals); stockData.SetCustomValues(output[0]); stockData.IndicatorName = IndicatorName.EhlersChebyshevLowPassFilter;
        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Better Exponential Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersBetterExponentialMovingAverage(this StockData stockData, int length = 20)
    {
        List<double> line = new(stockData.Count); List<Signal>? signals = CreateSignalsList(stockData);
        var (input, _, _, _, _) = GetInputValuesList(stockData); var window = new BetterEmaWindow(length);
        for (var i = 0; i < input.Count; i++)
        {
            var value = window.Next(input[i], true);
            signals?.Add(GetCompareSignal(input[i] - value, i == 0 ? 0 : input[i - 1] - line[i - 1])); line.Add(value);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Ebema", line } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.EhlersBetterExponentialMovingAverage;
        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Hamming Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="pedestal"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersHammingMovingAverage(this StockData stockData, int length = 20, double pedestal = 3)
    {
        List<double> filtList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        using var mean = new HammingWindowMean(length, pedestal);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevFilt = i >= 1 ? filtList[i - 1] : 0;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var filt = mean.Next(currentValue, true);
            filtList.Add(filt);

            var signal = GetCompareSignal(currentValue - filt, prevValue - prevFilt);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ehma", filtList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(filtList);
        stockData.IndicatorName = IndicatorName.EhlersHammingMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Triangle Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersTriangleMovingAverage(this StockData stockData, int length = 20)
    {
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var values = new double[inputList.Count];
        Core.MovingAverageCore.EhlersTriangleMovingAverage(inputList.ToArray(), values, length);
        var result = values.ToList();
        var signals = CreateSignalsList(stockData);
        for (var i = 0; i < values.Length; i++)
            signals?.Add(GetCompareSignal(inputList[i] - values[i], i == 0 ? 0 : inputList[i - 1] - values[i - 1]));
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Etma", result } });
        stockData.SetSignals(signals);
        stockData.SetCustomValues(result);
        stockData.IndicatorName = IndicatorName.EhlersTriangleMovingAverage;
        return stockData;
    }
}

