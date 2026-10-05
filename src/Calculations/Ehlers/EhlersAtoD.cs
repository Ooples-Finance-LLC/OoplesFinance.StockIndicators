
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Ehlers Adaptive Cyber Cycle
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="alpha"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersAdaptiveCyberCycle(this StockData stockData, int length = 5, double alpha = 0.07)
    {
        var (selected, _, _, _, _) = GetInputValuesList(stockData); var window = new AdaptiveCyberWindow(length, alpha);
        var cycles = new List<double>(stockData.Count); var periods = new List<double>(stockData.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < stockData.Count; i++)
        {
            var point = window.Next(selected[i], true); cycles.Add(point.Cycle); periods.Add(point.Period);
            var previous = i == 0 ? 0 : cycles[i - 1]; var older = i < 2 ? 0 : cycles[i - 2]; signals?.Add(GetCompareSignal(point.Cycle - previous, previous - older));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Eacc", cycles }, { "Period", periods } }); stockData.SetSignals(signals); stockData.SetCustomValues(cycles); stockData.IndicatorName = IndicatorName.EhlersAdaptiveCyberCycle; return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Correlation Trend Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersCorrelationTrendIndicator(this StockData stockData, int length = 20)
    {
        length = Math.Max(length, 1);
        List<double> corrList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        using var correlation = new EhlersCorrelationWindow(length, true);
        for (var i = 0; i < stockData.Count; i++)
        {
            var prevCorr1 = i >= 1 ? corrList[i - 1] : 0;
            var prevCorr2 = i >= 2 ? corrList[i - 2] : 0;

            var corr = correlation.Next(inputList[i], true).Real;
            corrList.Add(corr);

            var signal = GetRsiSignal(corr - prevCorr1, prevCorr1 - prevCorr2, corr, prevCorr1, 0.5, -0.5);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ecti", corrList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(corrList);
        stockData.IndicatorName = IndicatorName.EhlersCorrelationTrendIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Center Of Gravity
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersCenterofGravityOscillator(this StockData stockData, int length = 10)
    {
        var (input,_,_,_,_)=GetInputValuesList(stockData);using var window=new CenterGravityWindow(length,input.Count);List<double> values=new(input.Count);var signals=CreateSignalsList(stockData);
        foreach(var price in input){var value=window.Next(price,true);signals?.Add(GetCompareSignal(value,values.Count>0?values[values.Count-1]:0));values.Add(value);}
        stockData.SetOutputValues(()=>new Dictionary<string,List<double>>{{"Ecog",values}});stockData.SetSignals(signals);stockData.SetCustomValues(values);stockData.IndicatorName=IndicatorName.EhlersCenterofGravityOscillator;return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Adaptive Center Of Gravity Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersAdaptiveCenterOfGravityOscillator(this StockData stockData, int length = 5)
    {
        var (selected, _, _, _, _) = GetInputValuesList(stockData); var window = new AdaptiveGravityWindow(length); var values = new List<double>(stockData.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < stockData.Count; i++)
        {
            var value = window.Next(selected[i], true); values.Add(value); var previous = i == 0 ? 0 : values[i - 1]; var older = i < 2 ? 0 : values[i - 2]; signals?.Add(GetCompareSignal(value - previous, previous - older));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Eacog", values } }); stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.EhlersAdaptiveCenterOfGravityOscillator; return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Decycler Oscillator V1
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="fastMult"></param>
    /// <param name="slowMult"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersDecyclerOscillatorV1(this StockData stockData, int fastLength = 100, int slowLength = 125, 
        double fastMult = 1.2, double slowMult = 1)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        var fastWindow = new DecyclerOscillatorWindow(fastLength, fastMult); var slowWindow = new DecyclerOscillatorWindow(slowLength, slowMult);
        List<double> fast = new(stockData.Count), slow = new(stockData.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < stockData.Count; i++)
        {
            fast.Add(fastWindow.Next(input[i], true)); slow.Add(slowWindow.Next(input[i], true));
            signals?.Add(GetCompareSignal(slow[i] - fast[i], i == 0 ? 0 : slow[i - 1] - fast[i - 1]));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "FastEdo", fast }, { "SlowEdo", slow } });
        stockData.SetSignals(signals); stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.EhlersDecyclerOscillatorV1;
        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Decycler Oscillator V2
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersDecyclerOscillatorV2(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int fastLength = 10, int slowLength = 20)
    {
        if (StrengthWindow.Supports(maType) && !Builder.Compute.ComponentAverage.HasOverrides)
        {
            var input = stockData.ChainedValues.Count > 0 ? stockData.ChainedValues : stockData.InputValues;
            using var window = new DecyclerV2Window(maType, fastLength, slowLength);
            var values = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
            for (var i = 0; i < input.Count; i++)
            { var value = window.Next(input[i], true); signals?.Add(GetCompareSignal(value, i > 0 ? values[i - 1] : 0)); values.Add(value); }
            stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Edo", values } });
            stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.EhlersDecyclerOscillatorV2;
            return stockData;
        }
        var callerSeries = stockData.CaptureInputSeries();
        fastLength = Math.Max(fastLength, 1);
        slowLength = Math.Max(slowLength, 1);
        List<double> decList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var hp1List = GetCustomValuesListInternal(stockData,
            data => CalculateEhlersHighPassFilterV2(data, maType, fastLength));
        // The next component reads the caller's series, not the previous component's output.
        stockData.RestoreInputSeries(callerSeries);
        var hp2List = GetCustomValuesListInternal(stockData,
            data => CalculateEhlersHighPassFilterV2(data, maType, slowLength));

        for (var i = 0; i < stockData.Count; i++)
        {
            var hp1 = hp1List[i];
            var hp2 = hp2List[i];

            var prevDec = GetLastOrDefault(decList);
            var dec = hp2 - hp1;
            decList.Add(dec);

            var signal = GetCompareSignal(dec, prevDec);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Edo", decList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(decList);
        stockData.IndicatorName = IndicatorName.EhlersDecyclerOscillatorV2;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Decycler
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersDecycler(this StockData stockData, int length = 60)
    {
        length = Math.Max(length, 1);
        List<double> decList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var window = new DecyclerWindow(length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue1 = i >= 1 ? inputList[i - 1] : 0;

            var prevDec = GetLastOrDefault(decList);
            var dec = window.Next(currentValue, true);
            decList.Add(dec);

            var signal = GetCompareSignal(currentValue - dec, prevValue1 - prevDec);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ed", decList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(decList);
        stockData.IndicatorName = IndicatorName.EhlersDecycler;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Dominant Cycle Tuned Bypass Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="minLength"></param>
    /// <param name="maxLength"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersDominantCycleTunedBypassFilter(this StockData stockData, int minLength = 8, int maxLength = 50, 
        int length1 = 40, int length2 = 10)
    {
        var result=TunedBypassWindow.Calculate(stockData,minLength,maxLength,length1,length2);
        var v1List=result.Outputs["V1"];var v2List=result.Outputs["V2"];var signalsList=result.Signals;
        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "V1", v1List },
            { "V2", v2List }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.EhlersDominantCycleTunedBypassFilter;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Correlation Cycle Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersCorrelationCycleIndicator(this StockData stockData, int length = 20)
    {
        length = Math.Max(length, 1);
        List<double> realList = new(stockData.Count);
        List<double> imagList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        using var correlation = new EhlersCorrelationWindow(length, false);
        for (var i = 0; i < stockData.Count; i++)
        {
            var (real, imag) = correlation.Next(inputList[i], true);
            var prevReal = GetLastOrDefault(realList);
            var prevImag = GetLastOrDefault(imagList);
            realList.Add(real); imagList.Add(imag);

            var signal = GetCompareSignal(real - imag, prevReal - prevImag);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Real", realList },
            { "Imag", imagList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.EhlersCorrelationCycleIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Correlation Angle Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersCorrelationAngleIndicator(this StockData stockData, int length = 20)
    {
        length = Math.Max(length, 1);
        List<double> angleList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var ecciOutputs = GetOutputValuesInternal(stockData,
            data => CalculateEhlersCorrelationCycleIndicator(data, length));
        var realList = ecciOutputs["Real"];
        var imagList = ecciOutputs["Imag"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var real = realList[i];
            var imag = imagList[i];

            var prevAngle = i >= 1 ? angleList[i - 1] : 0;
            var angle = EhlersCorrelationPhase.Angle(real, imag);
            angle = prevAngle - angle < 270 && angle < prevAngle ? prevAngle : angle;
            angleList.Add(angle);

            var signal = GetCompareSignal(angle, prevAngle);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Cai", angleList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(angleList);
        stockData.IndicatorName = IndicatorName.EhlersCorrelationAngleIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Anticipate Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="bw"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersAnticipateIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.EhlersHannMovingAverage,
        int length = 14, double bw = 1)
    {
        length = AnticipateWindow.ValidateLength(length);
        if (ImpulseResponseWindow.Supports(maType))
        {
            using var window = new AnticipateWindow(maType, length, bw);
            var (input, _, _, _, _) = GetInputValuesList(stockData);
            foreach (var series in new[] { input, stockData.OpenPrices, stockData.HighPrices, stockData.LowPrices, stockData.ClosePrices, stockData.Volumes })
                foreach (var price in series) Streaming.StreamingInputValidation.Finite(price, nameof(stockData));
            var values = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
            foreach (var price in input) { var point = window.Next(price, true); values.Add(point.Value); signals?.Add(point.Signal); }
            stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Predict", values } });
            stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.EhlersAnticipateIndicator;
            return stockData;
        }
        length = Math.Max(length, 1);
        List<double> predictList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var hFiltList = GetCustomValuesListInternal(stockData,
            data => CalculateEhlersImpulseResponse(data, maType, length, bw));

        var phaseMatcher = new EhlersAnticipatePhase(length);
        var history = new double[length];
        for (var i = 0; i < stockData.Count; i++)
        {
            for (var lag = 0; lag < length; lag++)
                history[lag] = i >= lag ? hFiltList[i - lag] : 0;

            var prevPredict = GetLastOrDefault(predictList);
            var predict = phaseMatcher.Predict(history);
            predictList.Add(predict);

            var signal = GetCompareSignal(predict, prevPredict);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Predict", predictList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(predictList);
        stockData.IndicatorName = IndicatorName.EhlersAnticipateIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Auto Correlation Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersAutoCorrelationIndicator(this StockData stockData, int length1 = 48, int length2 = 10)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); var window = new RoofAutocorrelationWindow(length1, length2); var values = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        foreach (var price in input) { var point = window.Next(price, true); values.Add(point.Value); signals?.Add(point.Signal); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Eaci", values } }); stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.EhlersAutoCorrelationIndicator; return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Auto Correlation Periodogram
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersAutoCorrelationPeriodogram(this StockData stockData, int length1 = 48, int length2 = 10, int length3 = 3)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); var window = new AutocorrelationSpectrumWindow(length1, length2, length3); var values = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        foreach (var price in input) { var point = window.Next(price, true); values.Add(point.Value); signals?.Add(point.Signal); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Eacp", values } }); stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.EhlersAutoCorrelationPeriodogram; return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Adaptive Relative Strength Index V2
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersAdaptiveRelativeStrengthIndexV2(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 48, int length2 = 10, int length3 = 3)
    {
        length2 = Math.Max(1, length2); var (input, _, _, _, _) = GetInputValuesList(stockData); var external = Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType);
        using var window = new AdaptiveRsiV2Window(length1, length2, length3, maType, external); var values = new List<double>(input.Count); var averages = new List<double>(input.Count);
        for (var i = 0; i < input.Count; i++) { var point = window.Next(input[i], true); values.Add(point.Value); averages.Add(point.Average); }
        if (external) averages = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(values), length2)?.ToList() ?? GetMovingAverageList(stockData, maType, length2, values);
        var signals = CreateSignalsList(stockData);
        for (var i = 0; i < values.Count; i++) signals?.Add(GetRsiSignal(values[i] - averages[i], i == 0 ? 0 : values[i - 1] - averages[i - 1], values[i], i == 0 ? 0 : values[i - 1], .7, .3));
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Earsi", values }, { "Signal", averages } }); stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.EhlersAdaptiveRelativeStrengthIndexV2; return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Adaptive Relative Strength Index Fisher Transform V2
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersAdaptiveRsiFisherTransformV2(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 48, int length2 = 10, int length3 = 3)
    {
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        length3 = Math.Max(length3, 1);
        List<double> fishList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var arsiList = GetCustomValuesListInternal(stockData,
            data => CalculateEhlersAdaptiveRelativeStrengthIndexV2(data, maType, length1, length2, length3));

        for (var i = 0; i < stockData.Count; i++)
        {
            var arsi = arsiList[i];
            var prevFish1 = i >= 1 ? fishList[i - 1] : 0;
            var prevFish2 = i >= 2 ? fishList[i - 2] : 0;
            var tranRsi = 2 * (arsi - 0.5);
            var ampRsi = MinOrMax(1.5 * tranRsi, 0.999, -0.999);

            var fish = 0.5 * Math.Log((1 + ampRsi) / (1 - ampRsi));
            fishList.Add(fish);

            var signal = GetRsiSignal(fish - prevFish1, prevFish1 - prevFish2, fish, prevFish1, 2, -2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Earsift", fishList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(fishList);
        stockData.IndicatorName = IndicatorName.EhlersAdaptiveRsiFisherTransformV2;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Adaptive Stochastic Indicator V2
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersAdaptiveStochasticIndicatorV2(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 48, int length2 = 10, int length3 = 3)
    {
        length2 = Math.Max(1, length2); var (input, _, _, _, _) = GetInputValuesList(stockData); var external = Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType);
        using var window = new AdaptiveRangeV2Window(length1, length2, length3, maType, false, external); var values = new List<double>(input.Count); var averages = new List<double>(input.Count);
        for (var i = 0; i < input.Count; i++) { var point = window.Next(input[i], true); values.Add(point.Value); averages.Add(point.Average); }
        if (external) averages = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(values), length2)?.ToList() ?? GetMovingAverageList(stockData, maType, length2, values);
        var signals = CreateSignalsList(stockData);
        for (var i = 0; i < values.Count; i++) signals?.Add(GetRsiSignal(values[i] - averages[i], i == 0 ? 0 : values[i - 1] - averages[i - 1], values[i], i == 0 ? 0 : values[i - 1], .7, .3));
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Easi", values }, { "Signal", averages } }); stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.EhlersAdaptiveStochasticIndicatorV2; return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Adaptive Stochastic Inverse Fisher Transform
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersAdaptiveStochasticInverseFisherTransform(this StockData stockData, 
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 48, int length2 = 10, int length3 = 3)
    {
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        length3 = Math.Max(length3, 1);
        List<double> fishList = new(stockData.Count);
        List<double> triggerList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var astocList = GetCustomValuesListInternal(stockData,
            data => CalculateEhlersAdaptiveStochasticIndicatorV2(data, maType, length1, length2, length3));

        for (var i = 0; i < stockData.Count; i++)
        {
            var astoc = astocList[i];
            var v1 = 2 * (astoc - 0.5);

            var prevFish = GetLastOrDefault(fishList);
            var fish = Math.Tanh(3 * v1);
            fishList.Add(fish);

            var prevTrigger = GetLastOrDefault(triggerList);
            var trigger = 0.9 * prevFish;
            triggerList.Add(trigger);

            var signal = GetCompareSignal(fish - trigger, prevFish - prevTrigger);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Easift", fishList },
            { "Signal", triggerList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(fishList);
        stockData.IndicatorName = IndicatorName.EhlersAdaptiveStochasticInverseFisherTransform;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Adaptive Commodity Channel Index V2
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersAdaptiveCommodityChannelIndexV2(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length1 = 48, int length2 = 10, int length3 = 3)
    {
        length2 = Math.Max(1, length2); var (input, _, _, _, _) = GetInputValuesList(stockData); var external = Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType);
        using var window = new AdaptiveRangeV2Window(length1, length2, length3, maType, true, external); var values = new List<double>(input.Count); var averages = new List<double>(input.Count);
        for (var i = 0; i < input.Count; i++) { var point = window.Next(input[i], true); values.Add(point.Value); averages.Add(point.Average); }
        if (external) averages = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(values), length2)?.ToList() ?? GetMovingAverageList(stockData, maType, length2, values);
        var signals = CreateSignalsList(stockData);
        for (var i = 0; i < values.Count; i++) signals?.Add(GetRsiSignal(values[i] - averages[i], i == 0 ? 0 : values[i - 1] - averages[i - 1], values[i], i == 0 ? 0 : values[i - 1], 100, -100));
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Eacci", values }, { "Signal", averages } }); stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.EhlersAdaptiveCommodityChannelIndexV2; return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Discrete Fourier Transform Spectral Estimate
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersDiscreteFourierTransformSpectralEstimate(this StockData stockData, int length1 = 48, int length2 = 10)
    {
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        List<double> domCycList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var rArray = new double[length1 + 1];

        var roofingFilterList = GetCustomValuesListInternal(stockData,
            data => CalculateEhlersRoofingFilterV2(data, length1, length2));

        // DFT basis cos/sin(2π·k/j) depends only on (period j, lag k), not the bar i — precompute once.
        var cosTable = new double[length1 + 1, length1 + 1];
        var sinTable = new double[length1 + 1, length1 + 1];
        for (var jj = length2; jj <= length1; jj++)
        {
            for (var kk = 0; kk <= length1; kk++)
            {
                var angle = 2 * Math.PI * ((double)kk / jj);
                cosTable[jj, kk] = Math.Cos(angle);
                sinTable[jj, kk] = Math.Sin(angle);
            }
        }

        for (var i = 0; i < stockData.Count; i++)
        {
            var roofingFilter = roofingFilterList[i];
            var prevRoofingFilter1 = i >= 1 ? roofingFilterList[i - 1] : 0;
            var prevRoofingFilter2 = i >= 2 ? roofingFilterList[i - 2] : 0;

            double maxPwr = 0, spx = 0, sp = 0;
            for (var j = length2; j <= length1; j++)
            {
                double cosPart = 0, sinPart = 0;
                for (var k = 0; k <= length1; k++)
                {
                    var prevFilt = i >= k ? roofingFilterList[i - k] : 0;
                    cosPart += prevFilt * cosTable[j, k];
                    sinPart += prevFilt * sinTable[j, k];
                }

                var sqSum = (cosPart * cosPart) + (sinPart * sinPart);
                var prevR = rArray[j];
                var r = (0.2 * (sqSum * sqSum)) + (0.8 * prevR);
                rArray[j] = r;
                maxPwr = Math.Max(r, maxPwr);
            }

            // Normalize every bin against the same complete spectrum, not a prefix maximum.
            for (var j = length2; j <= length1; j++)
            {
                var pwr = maxPwr != 0 ? rArray[j] / maxPwr : 0;

                if (pwr >= 0.5)
                {
                    spx += j * pwr;
                    sp += pwr;
                }
            }

            var domCyc = sp != 0 ? spx / sp : 0;
            domCycList.Add(domCyc);

            var signal = GetCompareSignal(roofingFilter - prevRoofingFilter1, prevRoofingFilter1 - prevRoofingFilter2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Edftse", domCycList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(domCycList);
        stockData.IndicatorName = IndicatorName.EhlersDiscreteFourierTransformSpectralEstimate;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Comb Filter Spectral Estimate
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="bw"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersCombFilterSpectralEstimate(this StockData stockData, int length1 = 48, int length2 = 10, double bw = 0.3)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        foreach (var series in new[] { input, stockData.OpenPrices, stockData.HighPrices, stockData.LowPrices, stockData.ClosePrices, stockData.Volumes })
            foreach (var value in series) Streaming.StreamingInputValidation.Finite(value, nameof(stockData));
        var window = new CombSpectrumWindow(length1, length2, bw); var values = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        foreach (var price in input) { var point = window.Next(price, true); values.Add(point.Value); signals?.Add(point.Signal); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Ecfse", values } });
        stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.EhlersCombFilterSpectralEstimate; return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Auto Correlation Reversals
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersAutoCorrelationReversals(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 48, int length2 = 10, int length3 = 3)
    {
        length2 = Math.Max(1, length2); var (input, _, _, _, _) = GetInputValuesList(stockData); var external = Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType); List<double>? averages = null;
        if (external) averages = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(input), length2)?.ToList() ?? GetMovingAverageList(stockData, maType, length2, input);
        using var window = new AutocorrelationReversalWindow(maType, length1, length2, length3, external); var values = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++) { var point = window.Next(input[i], true, averages?[i]); values.Add(point.Value); signals?.Add(point.Signal); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Eacr", values } }); stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.EhlersAutoCorrelationReversals; return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Classic Hilbert Transformer
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersClassicHilbertTransformer(this StockData stockData, int length1 = 48, int length2 = 10)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); var window = new ClassicHilbertWindow(length1, length2); var real = new List<double>(input.Count); var imaginary = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        foreach (var price in input) { var point = window.Next(price, true); real.Add(point.Real); imaginary.Add(point.Imaginary); signals?.Add(point.Signal); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Real", real }, { "Imag", imaginary } }); stockData.SetSignals(signals); stockData.SetCustomValues(new List<double>()); stockData.IndicatorName = IndicatorName.EhlersClassicHilbertTransformer; return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Dual Differentiator Dominant Cycle
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersDualDifferentiatorDominantCycle(this StockData stockData, int length1 = 48, int length2 = 20, int length3 = 8)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); var window = new HilbertCycleWindow(length1, length2, length3, 1, 0); var values = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        foreach (var price in input) { var point = window.Next(price, true); values.Add(point.Value); signals?.Add(point.Signal); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Edddc", values } }); stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.EhlersDualDifferentiatorDominantCycle; return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Alternate Signal To Noise Ratio
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersAlternateSignalToNoiseRatio(this StockData stockData, int length = 6)
    {
        var (input, high, low, _, _) = GetInputValuesList(stockData); var window = new MamaNoiseWindow(length, 0); var values = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++) { var point = window.Next(input[i], high[i], low[i], true); values.Add(point.Snr); signals?.Add(point.Signal); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Esnr", values } }); stockData.SetCustomValues(values); stockData.SetSignals(signals); stockData.IndicatorName = IndicatorName.EhlersAlternateSignalToNoiseRatio; return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Discrete Fourier Transform
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="minLength"></param>
    /// <param name="maxLength"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersDiscreteFourierTransform(this StockData stockData, int minLength = 8, int maxLength = 50, int length = 40)
    {
        List<double> dominantCycleList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        using var spectrum = new Streaming.DiscreteFourierCycle(minLength, maxLength, length);
        double previousHighPass = 0;
        for (var i = 0; i < stockData.Count; i++)
        {
            dominantCycleList.Add(spectrum.Next(inputList[i], true, out var hp));
            signalsList?.Add(GetCompareSignal(hp, previousHighPass));
            previousHighPass = hp;
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Edft", dominantCycleList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(dominantCycleList);
        stockData.IndicatorName = IndicatorName.EhlersDiscreteFourierTransform;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Detrended Leading Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersDetrendedLeadingIndicator(this StockData stockData, int length = 14)
    {
        List<double> dspList = new(stockData.Count), deliList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var highList = stockData.HighPrices; var lowList = stockData.LowPrices; var window = new DetrendedLeadingWindow(length);
        for (var i = 0; i < stockData.Count; i++)
        {
            var previous = GetLastOrDefault(deliList); var point = window.Next(highList[i], lowList[i], true);
            dspList.Add(point.Dsp); deliList.Add(point.Deli); signalsList?.Add(GetCompareSignal(point.Deli, previous));
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Dsp", dspList },
            { "Deli", deliList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(deliList);
        stockData.IndicatorName = IndicatorName.EhlersDetrendedLeadingIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Band Pass Filter V1
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="bw"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersBandPassFilterV1(this StockData stockData, int length = 20, double bw = 0.3)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); var window = new ClampedBandPassWindow(length, bw, 0); List<double> values = new(input.Count), triggers = new(input.Count); var signals = CreateSignalsList(stockData);
        foreach (var price in input)
        {
            var p = window.Next(price, true); var previous = values.Count > 0 ? values[values.Count - 1] : 0; var older = values.Count > 1 ? values[values.Count - 2] : 0; var previousTrigger = triggers.Count > 0 ? triggers[triggers.Count - 1] : 0;
            signals?.Add(GetCompareSignal(p.Value - p.Signal, previous - previousTrigger)); values.Add(p.Value); triggers.Add(p.Signal);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Ebpf", values }, { "Signal", triggers } }); stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.EhlersBandPassFilterV1; return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Band Pass Filter V2
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="bw"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersBandPassFilterV2(this StockData stockData, int length = 20, double bw = 0.3)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); var window = new ClampedBandPassWindow(length, bw, 1); List<double> values = new(input.Count), triggers = new(input.Count); var signals = CreateSignalsList(stockData);
        foreach (var price in input)
        {
            var p = window.Next(price, true); var previous = values.Count > 0 ? values[values.Count - 1] : 0; var older = values.Count > 1 ? values[values.Count - 2] : 0; var previousTrigger = triggers.Count > 0 ? triggers[triggers.Count - 1] : 0;
            signals?.Add(GetCompareSignal(p.Value - previous, previous - older)); values.Add(p.Value); triggers.Add(p.Signal);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Ebpf", values } }); stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.EhlersBandPassFilterV2; return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Cycle Band Pass Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="delta"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersCycleBandPassFilter(this StockData stockData, int length = 20, double delta = 0.1)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); var window = new ClampedBandPassWindow(length, delta, 2); List<double> values = new(input.Count), triggers = new(input.Count); var signals = CreateSignalsList(stockData);
        foreach (var price in input)
        {
            var p = window.Next(price, true); var previous = values.Count > 0 ? values[values.Count - 1] : 0; var older = values.Count > 1 ? values[values.Count - 2] : 0; var previousTrigger = triggers.Count > 0 ? triggers[triggers.Count - 1] : 0;
            signals?.Add(GetCompareSignal(p.Value - previous, previous - older)); values.Add(p.Value); triggers.Add(p.Signal);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Ecbpf", values } }); stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.EhlersCycleBandPassFilter; return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Cycle Amplitude
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="delta"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersCycleAmplitude(this StockData stockData, int length = 20, double delta = 0.1)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); using var window = new CycleAmplitudeWindow(length, delta, input.Count); List<double> values = new(input.Count); var signals = CreateSignalsList(stockData);
        foreach (var price in input)
        {
            var value = window.Next(price, true); var previous = values.Count > 0 ? values[values.Count - 1] : 0; var older = values.Count > 1 ? values[values.Count - 2] : 0;
            signals?.Add(GetCompareSignal(value - previous, previous - older)); values.Add(value);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Eca", values } }); stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.EhlersCycleAmplitude; return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Adaptive Band Pass Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="bw"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersAdaptiveBandPassFilter(this StockData stockData, int length1 = 48, int length2 = 10, int length3 = 3, double bw = 0.3)
    {
        using var window = new AdaptiveBandPassWindow(length1, length2, length3, bw); var (input, _, _, _, _) = GetInputValuesList(stockData); var values = new List<double>(input.Count); var triggers = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++) { var point = window.Next(input[i], true); values.Add(point.Value); triggers.Add(point.Trigger); signals?.Add(point.Signal); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Eabpf", values }, { "Signal", triggers } }); stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.EhlersAdaptiveBandPassFilter; return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Cyber Cycle
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="alpha"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersCyberCycle(this StockData stockData, double alpha = .07)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); var window = new CyberCycleWindow(alpha);
        var output = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            var value = window.Next(input[i], true); var previous = i == 0 ? 0 : output[i-1];
            signals?.Add(GetCompareSignal(value-previous, previous-(i<2 ? 0 : output[i-2]))); output.Add(value);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Ecc", output } });
        stockData.SetSignals(signals); stockData.SetCustomValues(output); stockData.IndicatorName = IndicatorName.EhlersCyberCycle;
        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers AM Detector
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersAMDetector(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 4, int length2 = 8)
    {
        var (input, _, _, open, _) = GetInputValuesList(stockData);
        using var window = new AmDetectorWindow(maType, length1, length2);
        var average = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(input), Math.Max(1, length1))?.ToList() ?? GetMovingAverageList(stockData, maType, Math.Max(1, length1), input);
        var line = new List<double>(input.Count); var signal = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        if (Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType))
        {
            var envelope = new List<double>(input.Count);
            for (var i = 0; i < input.Count; i++) envelope.Add(window.Envelope(open[i], input[i], true).Publish());
            line = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(envelope), Math.Max(1, length2))?.ToList() ?? GetMovingAverageList(stockData, maType, Math.Max(1, length2), envelope);
            signal = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(line), Math.Max(1, length2))?.ToList() ?? GetMovingAverageList(stockData, maType, Math.Max(1, length2), line);
        }
        else for (var i = 0; i < input.Count; i++)
        { var point = window.Next(open[i], input[i], true); line.Add(point.Line); signal.Add(point.Signal); }
        for (var i = 0; i < input.Count; i++)
            signals?.Add(GetVolatilitySignal(input[i] - average[i], i == 0 ? 0 : input[i - 1] - average[i - 1], line[i], signal[i]));
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Eamd", line }, { "Signal", signal } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.EhlersAMDetector;
        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Convolution Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersConvolutionIndicator(this StockData stockData, int length1 = 80, int length2 = 40, int length3 = 48)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); var window = new EhlersConvolutionWindow(length1, length2, length3);
        var values = new List<double>(input.Count); var slopes = new List<double>(input.Count); var signals = CreateSignalsList(stockData, input.Count);
        foreach (var price in input) { var point = window.Next(price, true); values.Add(point.Value); slopes.Add(point.Slope); signals?.Add(point.Trade); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Eci", values }, { "Slope", slopes } }); stockData.SetSignals(signals);
        stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.EhlersConvolutionIndicator; return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Adaptive Relative Strength Index V1
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="cycPart"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersAdaptiveRelativeStrengthIndexV1(this StockData stockData, double cycPart = 0.5)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); var window = new AdaptiveRsiV1Window(cycPart, false); var values = new List<double>(input.Count); var average = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        foreach (var price in input) { var point = window.Next(price, true); values.Add(point.Value); average.Add(point.Average); signals?.Add(point.Signal); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Earsi", values }, { "Signal", average } }); stockData.SetCustomValues(values); stockData.SetSignals(signals); stockData.IndicatorName = IndicatorName.EhlersAdaptiveRelativeStrengthIndexV1; return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Adaptive Relative Strength Index Fisher Transform V1
    /// </summary>
    /// <param name="stockData"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersAdaptiveRsiFisherTransformV1(this StockData stockData)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); var window = new AdaptiveRsiV1Window(.5, true); var values = new List<double>(input.Count); var average = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        foreach (var price in input) { var point = window.Next(price, true); values.Add(point.Value); average.Add(point.Average); signals?.Add(point.Signal); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Earsift", values } }); stockData.SetCustomValues(values); stockData.SetSignals(signals); stockData.IndicatorName = IndicatorName.EhlersAdaptiveRsiFisherTransformV1; return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Adaptive Stochastic Indicator V1
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="cycPart"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersAdaptiveStochasticIndicatorV1(this StockData stockData, double cycPart = 0.5)
    {
        var cycle = stockData.ChainedValues.Count > 0 ? stockData.ChainedValues : stockData.InputValues; var input = cycle; var window = new AdaptiveRangeV1Window(cycPart, false, .015); var values = new List<double>(input.Count); var average = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++) { var point = window.Next(input[i], stockData.HighPrices[i], stockData.LowPrices[i], cycle[i], true); values.Add(point.Value); average.Add(point.Average); signals?.Add(point.Signal); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Easi", values }, { "Signal", average } }); stockData.SetCustomValues(values); stockData.SetSignals(signals); stockData.IndicatorName = IndicatorName.EhlersAdaptiveStochasticIndicatorV1; return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Adaptive Commodity Channel Index V1
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="cycPart"></param>
    /// <param name="constant"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersAdaptiveCommodityChannelIndexV1(this StockData stockData, double cycPart = 1,
        double constant = 0.015)
    {
        var cycle = stockData.ChainedValues.Count > 0 ? stockData.ChainedValues : stockData.InputValues; var input = CommodityIndexWindow.Prices(stockData); var window = new AdaptiveRangeV1Window(cycPart, true, constant); var values = new List<double>(input.Count); var average = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++) { var point = window.Next(input[i], stockData.HighPrices[i], stockData.LowPrices[i], cycle[i], true); values.Add(point.Value); average.Add(point.Average); signals?.Add(point.Signal); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Eacci", values }, { "Signal", average } }); stockData.SetCustomValues(values); stockData.SetSignals(signals); stockData.IndicatorName = IndicatorName.EhlersAdaptiveCommodityChannelIndexV1; return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Commodity Channel Index Inverse Fisher Transform
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="signalLength"></param>
    /// <param name="constant"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersCommodityChannelIndexInverseFisherTransform(this StockData stockData,
        MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 20, int signalLength = 9, double constant = 0.015)
    {
        length = Math.Max(length, 1);
        signalLength = Math.Max(signalLength, 1);
        List<double> v1List = new(stockData.Count);
        List<double> iFishList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var cciList = GetCustomValuesListInternal(stockData,
            data => CalculateCommodityChannelIndex(data, maType, length, constant));

        for (var i = 0; i < stockData.Count; i++)
        {
            var cci = cciList[i];

            var v1 = 0.1 * (cci - 50);
            v1List.Add(v1);
        }

        List<double> v2List;
        if (StrengthWindow.Supports(maType) && !Builder.Compute.ComponentAverage.HasOverrides)
            v2List = StrengthWindow.Smooth(v1List, maType, signalLength);
        else v2List = GetMovingAverageList(stockData, maType, signalLength, v1List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var v2 = v2List[i];
            var prevIFish1 = i >= 1 ? iFishList[i - 1] : 0;
            var prevIFish2 = i >= 2 ? iFishList[i - 2] : 0;

            var iFish = Math.Tanh(v2);
            iFishList.Add(iFish);

            var signal = GetRsiSignal(iFish - prevIFish1, prevIFish1 - prevIFish2, iFish, prevIFish1, 0.5, -0.5);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Eiftcci", iFishList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(iFishList);
        stockData.IndicatorName = IndicatorName.EhlersCommodityChannelIndexInverseFisherTransform;

        return stockData;
    }

}

