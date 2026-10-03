using OoplesFinance.StockIndicators.Compatibility;

namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Ehlers High Pass Filter V1
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="mult"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersHighPassFilterV1(this StockData stockData, int length = 125, double mult = 1)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        var window = new HighPassWindow(length, mult); List<double> output = new(stockData.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < stockData.Count; i++)
        {
            var value = window.Next(input[i], true); output.Add(value);
            signals?.Add(GetCompareSignal(value, i == 0 ? 0 : output[i - 1]));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Hp", output } });
        stockData.SetSignals(signals); stockData.SetCustomValues(output);
        stockData.IndicatorName = IndicatorName.EhlersHighPassFilterV1;
        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers High Pass Filter V2
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersHighPassFilterV2(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 20)
    {
        length = Math.Max(length, 1);
        List<double> hpList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        using var window = new HighPassV2Window(maType, length);
        var external = Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType);
        for (var i = 0; i < stockData.Count; i++)
            hpList.Add((external ? window.Raw(inputList[i], true) : window.Next(inputList[i], true)).Publish());
        List<double> hpMa2List;
        if (external)
        {
            var hpMa1List = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(hpList), length)?.ToList()
                ?? GetMovingAverageList(stockData, maType, length, hpList);
            hpMa2List = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(hpMa1List), length)?.ToList()
                ?? GetMovingAverageList(stockData, maType, length, hpMa1List);
        }
        else hpMa2List = hpList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var hp = hpMa2List[i];
            var prevHp1 = i >= 1 ? hpMa2List[i - 1] : 0;
            var prevHp2 = i >= 2 ? hpMa2List[i - 2] : 0;

            var signal = GetCompareSignal(hp - prevHp1, prevHp1 - prevHp2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ehpf", hpMa2List }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(hpMa2List);
        stockData.IndicatorName = IndicatorName.EhlersHighPassFilterV2;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Hp Lp Roofing Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersHpLpRoofingFilter(this StockData stockData, int length1 = 48, int length2 = 10)
    {
        List<double> output = new(stockData.Count); List<Signal>? signals = CreateSignalsList(stockData);
        var (input, _, _, _, _) = GetInputValuesList(stockData); var window = new HpLpRoofingWindow(length1, length2);
        for (var i = 0; i < stockData.Count; i++)
        {
            var previous1 = i > 0 ? output[i - 1] : 0; var previous2 = i > 1 ? output[i - 2] : 0;
            var value = window.Next(input[i], true).Roof; output.Add(value); signals?.Add(GetCompareSignal(value - previous1, previous1 - previous2));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Ehplprf", output } });
        stockData.SetSignals(signals); stockData.SetCustomValues(output); stockData.IndicatorName = IndicatorName.EhlersHpLpRoofingFilter;
        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Hurst Coefficient
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersHurstCoefficient(this StockData stockData, int length1 = 30, int length2 = 20)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); var window = new HurstCoefficientWindow(length1, length2);
        var values = new List<double>(input.Count); var signals = CreateSignalsList(stockData, input.Count);
        foreach (var price in input) { var point = window.Next(price, true); values.Add(point.Value); signals?.Add(point.Trade); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Ehc", values } }); stockData.SetSignals(signals);
        stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.EhlersHurstCoefficient; return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Empirical Mode Decomposition
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="delta"></param>
    /// <param name="fraction"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersEmpiricalModeDecomposition(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length1 = 20, int length2 = 50, double delta = 0.5, double fraction = 0.1)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); var external = Builder.Compute.ComponentAverage.HasOverrides || !EmpiricalDecompositionWindow.Supports(maType);
        using var window = new EmpiricalDecompositionWindow(maType, length1, length2, delta, fraction, external); var trends = new List<double>(stockData.Count); var peaks = new List<double>(stockData.Count); var valleys = new List<double>(stockData.Count); var signals = CreateSignalsList(stockData);
        if (external)
        {
            var bands = new List<double>(input.Count); foreach (var price in input) { var point = window.Prepare(price, true); bands.Add(point.Band); peaks.Add(point.Peak); valleys.Add(point.Valley); }
            List<double> Smooth(List<double> values, int period) => Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(values), period)?.ToList() ?? GetMovingAverageList(stockData, maType, period, values);
            trends = Smooth(bands, TrendExtractionWindow.ExternalPeriod(length1)); peaks = Smooth(peaks, Math.Max(1, length2)).Select(v => fraction * v).ToList(); valleys = Smooth(valleys, Math.Max(1, length2)).Select(v => fraction * v).ToList();
        }
        else foreach (var price in input) { var point = window.Next(price, true); trends.Add(point.Trend); peaks.Add(point.Peak); valleys.Add(point.Valley); }
        for (var i = 0; i < trends.Count; i++) { var previous = i == 0 ? 0 : trends[i - 1]; var priorPeak = i == 0 ? 0 : peaks[i - 1]; var priorValley = i == 0 ? 0 : valleys[i - 1]; signals?.Add(GetBullishBearishSignal(trends[i] - Math.Max(peaks[i], valleys[i]), previous - Math.Max(priorPeak, priorValley), trends[i] - Math.Min(peaks[i], valleys[i]), previous - Math.Min(priorPeak, priorValley))); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Trend", trends }, { "Peak", peaks }, { "Valley", valleys } }); stockData.SetSignals(signals); stockData.SetCustomValues(new List<double>()); stockData.IndicatorName = IndicatorName.EhlersEmpiricalModeDecomposition; return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Early Onset Trend Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="k"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersEarlyOnsetTrendIndicator(this StockData stockData, int length1 = 30, int length2 = 100, double k = 0.85)
    {
        var window = new EarlyOnsetWindow(length1, length2, k); var (input, _, _, _, _) = GetInputValuesList(stockData);
        var values = new List<double>(input.Count); var signals = CreateSignalsList(stockData); double previous = 0;
        foreach (var price in input) { var value = window.Next(price, true); values.Add(value); signals?.Add(GetCompareSignal(value, previous)); previous = value; }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Eoti", values } }); stockData.SetSignals(signals);
        stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.EhlersEarlyOnsetTrendIndicator; return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Impulse Response
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="bw"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersImpulseResponse(this StockData stockData, MovingAvgType maType = MovingAvgType.EhlersHannMovingAverage,
        int length = 20, double bw = 1)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); using var window = new ImpulseResponseWindow(maType, length, bw);
        var output = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            var value = window.Next(input[i], true); var previous = i == 0 ? 0 : output[i - 1];
            signals?.Add(GetCompareSignal(value - previous, previous - (i < 2 ? 0 : output[i - 2]))); output.Add(value);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Eir", output } });
        stockData.SetSignals(signals); stockData.SetCustomValues(output); stockData.IndicatorName = IndicatorName.EhlersImpulseResponse;
        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Impulse Reaction
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="qq"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersImpulseReaction(this StockData stockData, int length1 = 2, int length2 = 20, double qq = 0.9)
    {
        List<double> ireactList = new(stockData.Count); List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData); var window = new ImpulseReactionWindow(length1, length2, qq);
        for (var i = 0; i < stockData.Count; i++)
        {
            var previous1 = i > 0 ? ireactList[i - 1] : 0; var previous2 = i > 1 ? ireactList[i - 2] : 0;
            var value = window.Next(inputList[i], true); ireactList.Add(value);
            signalsList?.Add(GetCompareSignal(value - previous1, previous1 - previous2));
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Eir", ireactList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(ireactList);
        stockData.IndicatorName = IndicatorName.EhlersImpulseReaction;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Fisherized Deviation Scaled Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersFisherizedDeviationScaledOscillator(this StockData stockData, 
        MovingAvgType maType = MovingAvgType.EhlersDeviationScaledMovingAverage, int fastLength = 20, int slowLength = 40)
    {
        fastLength = Math.Max(fastLength, 1);
        slowLength = Math.Max(slowLength, 1);
        List<double> efdso2PoleList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var scaledFilter2PoleList = GetMovingAverageList(stockData, maType, fastLength, inputList, fastLength, slowLength);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentScaledFilter2Pole = scaledFilter2PoleList[i];
            var prevEfdsoPole1 = i >= 1 ? efdso2PoleList[i - 1] : 0;
            var prevEfdsoPole2 = i >= 2 ? efdso2PoleList[i - 2] : 0;

            var efdso2Pole = Math.Abs(currentScaledFilter2Pole) < 2 ? FisherArithmetic.Transform(currentScaledFilter2Pole / 2) : prevEfdsoPole1;
            efdso2PoleList.Add(efdso2Pole);

            var signal = GetRsiSignal(efdso2Pole - prevEfdsoPole1, prevEfdsoPole1 - prevEfdsoPole2, efdso2Pole, prevEfdsoPole1, 2, -2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Efdso", efdso2PoleList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(efdso2PoleList);
        stockData.IndicatorName = IndicatorName.EhlersFisherizedDeviationScaledOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Hilbert Transform Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="iMult"></param>
    /// <param name="qMult"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersHilbertTransformIndicator(this StockData stockData, int length = 7, double iMult = 0.635, double qMult = 0.338)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); var window = new HilbertPhaseWindow(length, iMult, qMult, 1, false); var real = new List<double>(input.Count); var imaginary = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        foreach (var price in input) { var point = window.Next(price, true); real.Add(point.Real); imaginary.Add(point.Imaginary); signals?.Add(point.Signal); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Quad", imaginary }, { "Inphase", real } }); stockData.SetSignals(signals); stockData.SetCustomValues(new List<double>()); stockData.IndicatorName = IndicatorName.EhlersHilbertTransformIndicator; return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Instantaneous Phase Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersInstantaneousPhaseIndicator(this StockData stockData, int length1 = 7, int length2 = 50)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); var window = new HilbertPhaseWindow(length1, .635, .338, length2, true); var values = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        foreach (var price in input) { var point = window.Next(price, true); values.Add(point.Cycle); signals?.Add(point.Signal); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Eipi", values } }); stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.EhlersInstantaneousPhaseIndicator; return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Hilbert Transformer
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersHilbertTransformer(this StockData stockData, int length1 = 48, int length2 = 20)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); var window = new HilbertTransformerWindow(length1, length2, 1, false); var real = new List<double>(input.Count); var imaginary = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        foreach (var price in input) { var point = window.Next(price, true); real.Add(point.Real); imaginary.Add(point.Imaginary); signals?.Add(point.Signal); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Real", real }, { "Imag", imaginary } }); stockData.SetSignals(signals); stockData.SetCustomValues(new List<double>()); stockData.IndicatorName = IndicatorName.EhlersHilbertTransformer; return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Hilbert Transformer Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersHilbertTransformerIndicator(this StockData stockData, int length1 = 48, int length2 = 20, int length3 = 10)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); var window = new HilbertTransformerWindow(length1, length2, length3, true); var real = new List<double>(input.Count); var imaginary = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        foreach (var price in input) { var point = window.Next(price, true); real.Add(point.Real); imaginary.Add(point.Imaginary); signals?.Add(point.Signal); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Real", real }, { "Imag", imaginary } }); stockData.SetSignals(signals); stockData.SetCustomValues(new List<double>()); stockData.IndicatorName = IndicatorName.EhlersHilbertTransformerIndicator; return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Homodyne Dominant Cycle
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersHomodyneDominantCycle(this StockData stockData, int length1 = 48, int length2 = 20, int length3 = 10)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); var window = new HilbertCycleWindow(length1, length2, length3, 1, 1); var values = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        foreach (var price in input) { var point = window.Next(price, true); values.Add(point.Value); signals?.Add(point.Signal); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Ehdc", values } }); stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.EhlersHomodyneDominantCycle; return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Hann Window Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersHannWindowIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.EhlersHannMovingAverage,
        int length = 20)
    {
        length = Math.Max(1, length);
        var (input, _, _, open, _) = GetInputValuesList(stockData);
        using var window = new HannIndicatorWindow(maType, length);
        var external = Builder.Compute.ComponentAverage.HasOverrides || !HannIndicatorWindow.Supports(maType);
        List<double>? averaged = null;
        if (external)
        {
            var differences = input.Select((v, i) => TriangleIndicatorWindow.Difference(v, open[i]).Publish()).ToList();
            averaged = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(differences), length)?.ToList()
                ?? GetMovingAverageList(stockData, maType, length, differences);
        }
        var line = new List<double>(input.Count); var roc = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            var point = external ? window.Finish(new RocBankValue(averaged![i]), true) : window.Next(open[i], input[i], true);
            var previous = i > 0 ? line[i - 1] : 0; var older = i > 1 ? line[i - 2] : 0;
            signals?.Add(GetCompareSignal(point.Line - previous, previous - older)); line.Add(point.Line); roc.Add(point.Roc);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Ehwi", line }, { "Roc", roc } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.EhlersHannWindowIndicator;
        return stockData;
    }



    /// <summary>
    /// Calculates the Ehlers Hamming Window Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="pedestal"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersHammingWindowIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.EhlersHammingMovingAverage,
        int length = 20, double pedestal = 10)
    {
        // Preserve the existing fixed-pedestal moving-average contract.
        _ = pedestal;
        length = Math.Max(1, length);
        var (input, _, _, open, _) = GetInputValuesList(stockData);
        using var window = new HammingIndicatorWindow(maType, length);
        var external = Builder.Compute.ComponentAverage.HasOverrides || !HammingIndicatorWindow.Supports(maType);
        List<double>? averaged = null;
        if (external)
        {
            var differences = input.Select((v, i) => TriangleIndicatorWindow.Difference(v, open[i]).Publish()).ToList();
            averaged = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(differences), length)?.ToList()
                ?? GetMovingAverageList(stockData, maType, length, differences);
        }
        var line = new List<double>(input.Count); var roc = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            var point = external ? window.Finish(new RocBankValue(averaged![i]), true) : window.Next(open[i], input[i], true);
            var previous = i > 0 ? line[i - 1] : 0; var older = i > 1 ? line[i - 2] : 0;
            signals?.Add(GetCompareSignal(point.Line - previous, previous - older)); line.Add(point.Line); roc.Add(point.Roc);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Ehwi", line }, { "Roc", roc } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.EhlersHammingWindowIndicator;
        return stockData;
    }



    /// <summary>
    /// Calculates the Ehlers Enhanced Signal To Noise Ratio
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersEnhancedSignalToNoiseRatio(this StockData stockData, int length = 6)
    {
        var (input, high, low, _, _) = GetInputValuesList(stockData); var window = new MamaNoiseWindow(length, 2); var values = new List<double>(input.Count); var real = new List<double>(input.Count); var quadrature = new List<double>(input.Count); var period = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++) { var point = window.Next(input[i], high[i], low[i], true); values.Add(point.Snr); real.Add(point.InPhase); quadrature.Add(point.Quadrature); period.Add(point.Period); signals?.Add(point.Signal); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Esnr", values }, { "I3", real }, { "Q3", quadrature }, { "SmoothPeriod", period } }); stockData.SetCustomValues(values); stockData.SetSignals(signals); stockData.IndicatorName = IndicatorName.EhlersEnhancedSignalToNoiseRatio; return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Hilbert Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersHilbertOscillator(this StockData stockData, int length = 7)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); var window = new MamaDerivedWindow(1); var first = new List<double>(input.Count); var second = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        foreach (var price in input) { var point = window.Next(price, true); first.Add(point.First); second.Add(point.Second); signals?.Add(point.Signal); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "I3", first }, { "IQ", second } }); stockData.SetSignals(signals); stockData.SetCustomValues(new List<double>()); stockData.IndicatorName = IndicatorName.EhlersHilbertOscillator; return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Fourier Series Analysis
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="bw"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersFourierSeriesAnalysis(this StockData stockData, int length = 20, double bw = 0.1)
    {
        length = Math.Max(length, 1);
        List<double> bp1List = new(stockData.Count);
        List<double> bp2List = new(stockData.Count);
        List<double> bp3List = new(stockData.Count);
        List<double> q1List = new(stockData.Count);
        List<double> q2List = new(stockData.Count);
        List<double> q3List = new(stockData.Count);
        List<double> waveList = new(stockData.Count);
        List<double> rocList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var l1 = Math.Cos(2 * Math.PI / length);
        var s1 = FourierHarmonicPole.For((double)length/1, bw);
        var l2 = Math.Cos(2 * Math.PI / ((double)length / 2));
        var s2 = FourierHarmonicPole.For((double)length/2, bw);
        var l3 = Math.Cos(2 * Math.PI / ((double)length / 3));
        var s3 = FourierHarmonicPole.For((double)length/3, bw);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevBp1_1 = GetLastOrDefault(bp1List);
            var prevBp2_1 = GetLastOrDefault(bp2List);
            var prevBp3_1 = GetLastOrDefault(bp3List);
            var prevValue = i >= 2 ? inputList[i - 2] : 0;
            var prevBp1_2 = i >= 2 ? bp1List[i - 2] : 0;
            var prevBp2_2 = i >= 2 ? bp2List[i - 2] : 0;
            var prevBp3_2 = i >= 2 ? bp3List[i - 2] : 0;
            var prevWave2 = i >= 2 ? waveList[i - 2] : 0;

            var bp1 = i <= 3 ? 0 : (0.5 * (1 - s1) * (currentValue - prevValue)) + (l1 * (1 + s1) * prevBp1_1) - (s1 * prevBp1_2);
            bp1List.Add(bp1);

            var q1 = i <= 4 ? 0 : length / (2 * Math.PI) * (bp1 - prevBp1_1);
            q1List.Add(q1);

            var bp2 = i <= 3 ? 0 : (0.5 * (1 - s2) * (currentValue - prevValue)) + (l2 * (1 + s2) * prevBp2_1) - (s2 * prevBp2_2);
            bp2List.Add(bp2);

            var q2 = i <= 4 ? 0 : length / (4 * Math.PI) * (bp2 - prevBp2_1);
            q2List.Add(q2);

            var bp3 = i <= 3 ? 0 : (0.5 * (1 - s3) * (currentValue - prevValue)) + (l3 * (1 + s3) * prevBp3_1) - (s3 * prevBp3_2);
            bp3List.Add(bp3);

            var q3 = i <= 4 ? 0 : length / (6 * Math.PI) * (bp3 - prevBp3_1);
            q3List.Add(q3);

            double p1 = 0, p2 = 0, p3 = 0;
            for (var j = 0; j <= length - 1; j++)
            {
                var prevBp1 = i >= j ? bp1List[i - j] : 0;
                var prevBp2 = i >= j ? bp2List[i - j] : 0;
                var prevBp3 = i >= j ? bp3List[i - j] : 0;
                var prevQ1 = i >= j ? q1List[i - j] : 0;
                var prevQ2 = i >= j ? q2List[i - j] : 0;
                var prevQ3 = i >= j ? q3List[i - j] : 0;

                p1 += (prevBp1 * prevBp1) + (prevQ1 * prevQ1);
                p2 += (prevBp2 * prevBp2) + (prevQ2 * prevQ2);
                p3 += (prevBp3 * prevBp3) + (prevQ3 * prevQ3);
            }

            var prevWave = GetLastOrDefault(waveList);
            var wave = p1 != 0 ? bp1 + (Sqrt(p2 / p1) * bp2) + (Sqrt(p3 / p1) * bp3) : 0;
            waveList.Add(wave);

            var roc = length / (4 * Math.PI) * (wave - prevWave2);
            rocList.Add(roc);

            var signal = GetCompareSignal(wave, prevWave);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Wave", waveList },
            { "Roc", rocList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.EhlersFourierSeriesAnalysis;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers FM Demodulator Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersFMDemodulatorIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.Ehlers2PoleSuperSmootherFilterV2, 
        int fastLength = 10, int slowLength = 30)
    {
        fastLength = Math.Max(fastLength, 1);
        slowLength = Math.Max(slowLength, 1);
        List<double> hlList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, openList, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentClose = inputList[i];
            var currentOpen = openList[i];
            var der = currentClose - currentOpen;
            var hlRaw = fastLength * der;

            var hl = MinOrMax(hlRaw, 1, -1);
            hlList.Add(hl);
        }

        var ssList = GetMovingAverageList(stockData, maType, slowLength, hlList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var ss = ssList[i];
            var prevSs = i >= 1 ? ssList[i - 1] : 0;

            var signal = GetCompareSignal(ss, prevSs);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Efmd", ssList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(ssList);
        stockData.IndicatorName = IndicatorName.EhlersFMDemodulatorIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Even Better Sine Wave Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersEvenBetterSineWaveIndicator(this StockData stockData, int length1 = 40, int length2 = 10)
    {
        List<double> ebsiList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var kernel = new Streaming.EvenBetterSineWaveKernel(length1, length2);
        for (var i = 0; i < stockData.Count; i++)
        {
            var previous = i == 0 ? 0 : ebsiList[i - 1];
            var value = kernel.Next(inputList[i], true);
            ebsiList.Add(value);
            signalsList?.Add(GetCompareSignal(value, previous));
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ebsi", ebsiList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(ebsiList);
        stockData.IndicatorName = IndicatorName.EhlersEvenBetterSineWaveIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Fisher Transform
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersFisherTransform(this StockData stockData, int length = 10)
    {
        length = Math.Max(length, 1);
        List<double> fisherTransformList = new(stockData.Count);
        List<double> nValueList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var (maxList, minList) = GetMaxAndMinValuesList(inputList, inputList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var maxH = maxList[i];
            var minL = minList[i];
            var ratio = FisherArithmetic.Position(currentValue, minL, maxH);
            var prevFisherTransform1 = i >= 1 ? fisherTransformList[i - 1] : 0;
            var prevFisherTransform2 = i >= 2 ? fisherTransformList[i - 2] : 0;

            var prevNValue = GetLastOrDefault(nValueList);
            var nValue = MinOrMax((0.33 * 2 * (ratio - 0.5)) + (0.67 * prevNValue), 0.999, -0.999);
            nValueList.Add(nValue);

            var fisherTransform = FisherArithmetic.Transform(nValue) + (0.5 * prevFisherTransform1);
            fisherTransformList.Add(fisherTransform);

            var signal = GetCompareSignal(fisherTransform - prevFisherTransform1, prevFisherTransform1 - prevFisherTransform2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Eft", fisherTransformList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(fisherTransformList);
        stockData.IndicatorName = IndicatorName.EhlersFisherTransform;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Inverse Fisher Transform
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersInverseFisherTransform(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage, 
        int length1 = 5, int length2 = 9)
    {
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        List<double> v1List = new(stockData.Count);
        List<double> inverseFisherTransformList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var rsiList = CalculateRelativeStrengthIndex(stockData, maType, length: length1).ChainedValues;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentRsi = rsiList[i];

            var v1 = 0.1 * (currentRsi - 50);
            v1List.Add(v1);
        }

        List<double> v2List;
        if (StrengthWindow.Supports(maType) && !Builder.Compute.ComponentAverage.HasOverrides)
        {
            using var average = new StrengthAverage(maType, length2, v1List.Count);
            v2List = v1List.Select(v => average.Next(new StrengthValue(v), true).Mantissa).ToList();
        }
        else v2List = GetMovingAverageList(stockData, maType, length2, v1List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var v2 = v2List[i];
            var prevIft1 = i >= 1 ? inverseFisherTransformList[i - 1] : 0;
            var prevIft2 = i >= 2 ? inverseFisherTransformList[i - 2] : 0;

            var inverseFisherTransform = Math.Tanh(v2);
            inverseFisherTransformList.Add(inverseFisherTransform);

            var signal = GetRsiSignal(inverseFisherTransform - prevIft1, prevIft1 - prevIft2, inverseFisherTransform, prevIft1, 0.5, -0.5);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Eift", inverseFisherTransformList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(inverseFisherTransformList);
        stockData.IndicatorName = IndicatorName.EhlersInverseFisherTransform;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Instantaneous Trendline V2
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="alpha"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersInstantaneousTrendlineV2(this StockData stockData, double alpha = 0.07)
    {
        var window = new InstantaneousTrendWindow(alpha); var (input, _, _, _, _) = GetInputValuesList(stockData);
        var line = new List<double>(input.Count); var signal = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        double previousLine = 0, previousSignal = 0;
        for (var i = 0; i < input.Count; i++)
        {
            var p = window.Next(input[i], true); line.Add(p.Line); signal.Add(p.Signal);
            signals?.Add(GetCompareSignal(p.Signal - p.Line, previousSignal - previousLine)); previousLine = p.Line; previousSignal = p.Signal;
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Eit", line }, { "Signal", signal } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.EhlersInstantaneousTrendlineV2; return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Instantaneous Trendline V1
    /// </summary>
    /// <param name="stockData"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersInstantaneousTrendlineV1(this StockData stockData)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); var window = new MamaDerivedWindow(2); var first = new List<double>(input.Count); var second = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        foreach (var price in input) { var point = window.Next(price, true); first.Add(point.First); second.Add(point.Second); signals?.Add(point.Signal); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Eit", first }, { "Signal", second } }); stockData.SetSignals(signals); stockData.SetCustomValues(first); stockData.IndicatorName = IndicatorName.EhlersInstantaneousTrendlineV1; return stockData;
    }

}

