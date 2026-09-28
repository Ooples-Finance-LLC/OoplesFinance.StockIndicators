using OoplesFinance.StockIndicators.Compatibility;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Ehlers Simple Decycler
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="upperPct"></param>
    /// <param name="lowerPct"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersSimpleDecycler(this StockData stockData, int length = 125, double upperPct = 0.5, double lowerPct = 0.5)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        var window = new SimpleDecyclerWindow(length, upperPct, lowerPct);
        List<double> middle = new(stockData.Count), upper = new(stockData.Count), lower = new(stockData.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < stockData.Count; i++)
        {
            var value = window.Next(input[i], true); middle.Add(value.Middle); upper.Add(value.Upper); lower.Add(value.Lower);
            signals?.Add(GetCompareSignal(input[i] - value.Middle, i == 0 ? 0 : input[i - 1] - middle[i - 1]));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "UpperBand", upper }, { "MiddleBand", middle }, { "LowerBand", lower } });
        stockData.SetSignals(signals); stockData.SetCustomValues(middle);
        stockData.IndicatorName = IndicatorName.EhlersSimpleDecycler;
        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Smoothed Adaptive Momentum
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersSmoothedAdaptiveMomentum(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length1 = 5, int length2 = 8)
    {
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 2);
        List<double> f3List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var a1 = Exp(-Math.PI / length2);
        var b1 = 2 * a1 * Math.Cos(1.738 * Math.PI / length2);
        var c1 = Pow(a1, 2);
        var coef2 = b1 + c1;
        var coef3 = -1 * (c1 + (b1 * c1));
        var coef4 = c1 * c1;
        var coef1 = 1 - coef2 - coef3 - coef4;

        var pList = GetOutputValuesInternal(stockData,
            data => CalculateEhlersAdaptiveCyberCycle(data, length1))["Period"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var p = pList[i];
            var prevF3_1 = i >= 1 ? f3List[i - 1] : 0;
            var prevF3_2 = i >= 2 ? f3List[i - 2] : 0;
            var prevF3_3 = i >= 3 ? f3List[i - 3] : 0;
            var pr = (int)Math.Ceiling(Math.Abs(p - 1));
            var prevValue = i >= pr ? inputList[i - pr] : 0;
            var v1 = MinPastValues(i, pr, currentValue - prevValue);

            var f3 = (coef1 * v1) + (coef2 * prevF3_1) + (coef3 * prevF3_2) + (coef4 * prevF3_3);
            f3List.Add(f3);
        }

        var f3EmaList = GetMovingAverageList(stockData, maType, length2, f3List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var f3 = f3List[i];
            var f3Ema = f3EmaList[i];
            var prevF3 = i >= 1 ? f3List[i - 1] : 0;
            var prevF3Ema = i >= 1 ? f3EmaList[i - 1] : 0;

            var signal = GetCompareSignal(f3 - f3Ema, prevF3 - prevF3Ema);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Esam", f3List },
            { "Signal", f3EmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(f3List);
        stockData.IndicatorName = IndicatorName.EhlersSmoothedAdaptiveMomentumIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Stochastic Center Of Gravity Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersStochasticCenterOfGravityOscillator(this StockData stockData, int length = 8)
    {
        var (input,_,_,_,_)=GetInputValuesList(stockData);using var window=new StochasticGravityWindow(length,input.Count);List<double> values=new(input.Count);var signals=CreateSignalsList(stockData);
        foreach(var price in input){var value=window.Next(price,true);var previous=values.Count>0?values[values.Count-1]:0;var previous2=values.Count>1?values[values.Count-2]:0;signals?.Add(GetRsiSignal(value-previous,previous-previous2,value,previous,.8,.2));values.Add(value);}
        stockData.SetOutputValues(()=>new Dictionary<string,List<double>>{{"Escog",values}});stockData.SetSignals(signals);stockData.SetCustomValues(values);stockData.IndicatorName=IndicatorName.EhlersStochasticCenterOfGravityOscillator;return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Simple Cycle Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="alpha"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersSimpleCycleIndicator(this StockData stockData, double alpha = 0.07)
    {
        List<double> cycleList = new(stockData.Count); List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData); var window = new SimpleCycleIndicatorWindow(alpha);
        for (var i = 0; i < stockData.Count; i++)
        {
            var previous1 = i > 0 ? cycleList[i - 1] : 0; var previous2 = i > 1 ? cycleList[i - 2] : 0;
            var value = window.Next(inputList[i], true); cycleList.Add(value); signalsList?.Add(GetCompareSignal(value - previous1, previous1 - previous2));
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Esci", cycleList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(cycleList);
        stockData.IndicatorName = IndicatorName.EhlersSimpleCycleIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Zero Mean Roofing Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersZeroMeanRoofingFilter(this StockData stockData, int length1 = 48, int length2 = 10)
    {
        List<double> output = new(stockData.Count); List<Signal>? signals = CreateSignalsList(stockData);
        var (input, _, _, _, _) = GetInputValuesList(stockData); var window = new HpLpRoofingWindow(length1, length2);
        for (var i = 0; i < stockData.Count; i++)
        {
            var previous1 = i > 0 ? output[i - 1] : 0; var previous2 = i > 1 ? output[i - 2] : 0;
            var value = window.Next(input[i], true).Zero; output.Add(value); signals?.Add(GetCompareSignal(value - previous1, previous1 - previous2));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Ezmrf", output } });
        stockData.SetSignals(signals); stockData.SetCustomValues(output); stockData.IndicatorName = IndicatorName.EhlersZeroMeanRoofingFilter;
        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Spectrum Derived Filter Bank
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="minLength"></param>
    /// <param name="maxLength"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersSpectrumDerivedFilterBank(this StockData stockData, int minLength = 8, int maxLength = 50,
        int length1 = 40, int length2 = 10)
    {
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        List<double> cycles = new(stockData.Count);
        List<Signal>? signals = CreateSignalsList(stockData);
        using var bank = new EhlersSpectrumDerivedFilterBankEngine(minLength, maxLength, length1, length2);
        foreach (var price in inputList)
        {
            cycles.Add(bank.Next(price, true));
            signals?.Add(GetCompareSignal(bank.SmoothedHighPass, bank.PreviousSmoothedHighPass));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Esdfb", cycles } });
        stockData.SetSignals(signals);
        stockData.SetCustomValues(cycles);
        stockData.IndicatorName = IndicatorName.EhlersSpectrumDerivedFilterBank;
        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Trendflex Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersTrendflexIndicator(this StockData stockData, int length = 20)
    {
        var window = new TrendflexWindow(length); var (input, _, _, _, _) = GetInputValuesList(stockData);
        var values = new List<double>(input.Count); var signals = CreateSignalsList(stockData); double previous = 0, older = 0;
        for (var i = 0; i < input.Count; i++)
        { var value = window.Next(input[i], true); values.Add(value); signals?.Add(GetCompareSignal(value - previous, previous - older)); older = previous; previous = value; }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Eti", values } }); stockData.SetSignals(signals);
        stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.EhlersTrendflexIndicator; return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Trend Extraction
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="delta"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersTrendExtraction(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 20, double delta = 0.1)
    {
        length = Math.Max(length, 1);
        List<double> bpList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var beta = Math.Cos(MinOrMax(2 * Math.PI / length, 0.99, 0.01));
        var gamma = 1 / Math.Cos(MinOrMax(4 * Math.PI * delta / length, 0.99, 0.01));
        var alpha = MinOrMax(gamma - Sqrt((gamma * gamma) - 1), 0.99, 0.01);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 2 ? inputList[i - 2] : 0;
            var prevBp1 = i >= 1 ? bpList[i - 1] : 0;
            var prevBp2 = i >= 2 ? bpList[i - 2] : 0;

            var bp = (0.5 * (1 - alpha) * MinPastValues(i, 2, currentValue - prevValue)) + (beta * (1 + alpha) * prevBp1) - (alpha * prevBp2);
            bpList.Add(bp);
        }

        var trendList = GetMovingAverageList(stockData, maType, length * 2, bpList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var trend = trendList[i];
            var prevTrend = i >= 1 ? trendList[i - 1] : 0;

            var signal = GetCompareSignal(trend, prevTrend);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Trend", trendList },
            { "Bp", bpList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(trendList);
        stockData.IndicatorName = IndicatorName.EhlersTrendExtraction;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Snake Universal Trading Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="bw"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersSnakeUniversalTradingFilter(this StockData stockData, MovingAvgType maType = MovingAvgType.EhlersHannMovingAverage,
        int length1 = 23, int length2 = 50, double bw = 1.4)
    {
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        List<double> bpList = new(stockData.Count);
        List<double> negRmsList = new(stockData.Count);
        List<double> filtPowList = new(stockData.Count);
        List<double> rmsList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum filtPowSum = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var l1 = Math.Cos(MinOrMax(2 * Math.PI / (2 * length1), 0.99, 0.01));
        var g1 = Math.Cos(MinOrMax(bw * 2 * Math.PI / (2 * length1), 0.99, 0.01));
        var s1 = (1 / g1) - Sqrt(1 / Pow(g1, 2) - 1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 2 ? inputList[i - 2] : 0;
            var prevBp1 = i >= 1 ? bpList[i - 1] : 0;
            var prevBp2 = i >= 2 ? bpList[i - 2] : 0;

            var bp = i < 3 ? 0 : (0.5 * (1 - s1) * (currentValue - prevValue)) + (l1 * (1 + s1) * prevBp1) - (s1 * prevBp2);
            bpList.Add(bp);
        }

        var filtList = GetMovingAverageList(stockData, maType, length1, bpList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var filt = filtList[i];
            var prevFilt1 = i >= 1 ? filtList[i - 1] : 0;
            var prevFilt2 = i >= 2 ? filtList[i - 2] : 0;

            var filtPow = Pow(filt, 2);
            filtPowList.Add(filtPow);
            filtPowSum.Add(filtPow);

            var filtPowMa = filtPowSum.Average(length2);
            var rms = Sqrt(filtPowMa);
            rmsList.Add(rms);

            var negRms = -rms;
            negRmsList.Add(negRms);

            var signal = GetCompareSignal(filt - prevFilt1, prevFilt1 - prevFilt2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", rmsList },
            { "Erf", filtList },
            { "LowerBand", negRmsList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(filtList);
        stockData.IndicatorName = IndicatorName.EhlersSnakeUniversalTradingFilter;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Universal Trading Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="mult"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersUniversalTradingFilter(this StockData stockData, MovingAvgType maType = MovingAvgType.EhlersHannMovingAverage,
        int length1 = 16, int length2 = 50, double mult = 2)
    {
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        List<double> momList = new(stockData.Count);
        List<double> negRmsList = new(stockData.Count);
        List<double> filtPowList = new(stockData.Count);
        List<double> rmsList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum filtPowSum = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var hannLength = (int)Math.Ceiling(mult * length1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var priorValue = i >= hannLength ? inputList[i - hannLength] : 0;

            var mom = currentValue - priorValue;
            momList.Add(mom);
        }

        var filtList = GetMovingAverageList(stockData, maType, length1, momList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var filt = filtList[i];
            var prevFilt1 = i >= 1 ? filtList[i - 1] : 0;
            var prevFilt2 = i >= 2 ? filtList[i - 2] : 0;

            var filtPow = Pow(filt, 2);
            filtPowList.Add(filtPow);
            filtPowSum.Add(filtPow);

            var filtPowMa = filtPowSum.Average(length2);
            var rms = filtPowMa > 0 ? Sqrt(filtPowMa) : 0;
            rmsList.Add(rms);

            var negRms = -rms;
            negRmsList.Add(negRms);

            var signal = GetCompareSignal(filt - prevFilt1, prevFilt1 - prevFilt2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Eutf", filtList },
            { "UpperBand", rmsList },
            { "LowerBand", negRmsList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(filtList);
        stockData.IndicatorName = IndicatorName.EhlersUniversalTradingFilter;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Super Passband Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersSuperPassbandFilter(this StockData stockData, int fastLength = 40, int slowLength = 60, int length1 = 5, int length2 = 50)
    {
        var window = new SuperPassbandWindow(fastLength, slowLength, length1, length2); var (input, _, _, _, _) = GetInputValuesList(stockData);
        var line = new List<double>(input.Count); var upper = new List<double>(input.Count); var lower = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        double previous = 0, previousUpper = 0, previousLower = 0;
        foreach (var price in input)
        {
            var value = window.Next(price, true); line.Add(value.Line); upper.Add(value.Upper); lower.Add(value.Lower);
            signals?.Add(GetBullishBearishSignal(value.Line - value.Upper, previous - previousUpper, value.Line - value.Lower, previous - previousLower));
            previous = value.Line; previousUpper = value.Upper; previousLower = value.Lower;
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Espf", line }, { "UpperBand", upper }, { "LowerBand", lower } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.EhlersSuperPassbandFilter; return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Simple Deriv Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersSimpleDerivIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length = 2, int signalLength = 8)
    {
        signalLength = Math.Max(1, signalLength); var (input, _, _, _, _) = GetInputValuesList(stockData);
        using var window = new EhlersDerivWindow(maType, length, signalLength);
        List<double> z3List = new(stockData.Count), z3EmaList; List<Signal>? signalsList = CreateSignalsList(stockData);
        if (Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType))
        {
            foreach (var price in input) z3List.Add(window.Line(price, true).Publish());
            z3EmaList = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(z3List), signalLength)?.ToList() ?? GetMovingAverageList(stockData, maType, signalLength, z3List);
        }
        else
        {
            z3EmaList = new(stockData.Count);
            foreach (var price in input) { var value = window.Next(price, true); z3List.Add(value.Line); z3EmaList.Add(value.Signal); }
        }
        for (var i = 0; i < stockData.Count; i++)
        {
            var z3Ema = z3EmaList[i];
            var prevZ3Ema = i >= 1 ? z3EmaList[i - 1] : 0;

            var signal = GetCompareSignal(z3Ema, prevZ3Ema);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Esdi", z3List },
            { "Signal", z3EmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(z3List);
        stockData.IndicatorName = IndicatorName.EhlersSimpleDerivIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Simple Clip Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersSimpleClipIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length1 = 2, int length2 = 10, int length3 = 50, int signalLength = 22)
    {
        signalLength = Math.Max(1, signalLength); _ = length2; // Retained public parameter; the published variant does not use it.
        var (inputList, _, _, _, _) = GetInputValuesList(stockData); using var window = new EhlersClipWindow(maType, length1, length3, signalLength);
        List<double> z3List = new(stockData.Count), z3EmaList = new(stockData.Count); List<Signal>? signalsList = CreateSignalsList(stockData);
        if (Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType))
        {
            for (var i = 0; i < stockData.Count; i++) z3List.Add(window.Line(inputList[i], true));
            z3EmaList = Builder.Compute.ComponentAverage.Take(z3List.ToArray(), signalLength)?.ToList() ?? GetMovingAverageList(stockData, maType, signalLength, z3List);
        }
        else for (var i = 0; i < stockData.Count; i++) { var point = window.Next(inputList[i], true); z3List.Add(point.Line); z3EmaList.Add(point.Signal); }
        for (var i = 0; i < stockData.Count; i++)
        {
            var z3Ema = z3EmaList[i];
            var prevZ3Ema = i >= 1 ? z3EmaList[i - 1] : 0;

            var signal = GetCompareSignal(z3Ema, prevZ3Ema);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Esci", z3List },
            { "Signal", z3EmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(z3List);
        stockData.IndicatorName = IndicatorName.EhlersSimpleClipIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Spearman Rank Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersSpearmanRankIndicator(this StockData stockData, int length = 20)
    {
        length = Math.Max(length, 1);
        List<double> sriList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var prices = new double[length];
        var positions = new double[length];
        for (var i = 0; i < stockData.Count; i++)
        {
            for (var j = 0; j < length; j++)
            {
                var index = i - length + 1 + j;
                prices[j] = index < 0 ? 0 : inputList[index];
            }
            var prevSri = GetLastOrDefault(sriList);
            var sri = ChronologicalSpearman.Compute(prices, positions);
            sriList.Add(sri);

            var signal = GetCompareSignal(sri, prevSri);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Esri", sriList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(sriList);
        stockData.IndicatorName = IndicatorName.EhlersSpearmanRankIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Truncated Bandpass Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="bw"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersTruncatedBandPassFilter(this StockData stockData, int length1 = 20, int length2 = 10, double bw = .1)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        var window = new TruncatedBandPassWindow(length1, length2, bw); var output = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            var value = window.Next(input[i], true); signals?.Add(GetCompareSignal(value, i == 0 ? 0 : output[i - 1])); output.Add(value);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Etbpf", output } });
        stockData.SetSignals(signals); stockData.SetCustomValues(output); stockData.IndicatorName = IndicatorName.EhlersTruncatedBandPassFilter;
        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Squelch Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersSquelchIndicator(this StockData stockData, int length1 = 6, int length2 = 20, int length3 = 40)
    {
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        length3 = Math.Max(length3, 1);
        List<double> phaseList = new(stockData.Count);
        List<double> dPhaseList = new(stockData.Count);
        List<double> dcPeriodList = new(stockData.Count);
        List<double> v1List = new(stockData.Count);
        List<double> ipList = new(stockData.Count);
        List<double> quList = new(stockData.Count);
        List<double> siList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= length1 ? inputList[i - length1] : 0;
            var priorV1 = i >= length1 ? v1List[i - length1] : 0;
            var prevV12 = i >= 2 ? v1List[i - 2] : 0;
            var prevV14 = i >= 4 ? v1List[i - 4] : 0;

            var v1 = MinPastValues(i, length1, currentValue - prevValue);
            v1List.Add(v1);

            var v2 = i >= 3 ? v1List[i - 3] : 0;
            var v3 = (0.75 * (v1 - priorV1)) + (0.25 * (prevV12 - prevV14));
            var prevIp = GetLastOrDefault(ipList);
            var ip = (0.33 * v2) + (0.67 * prevIp);
            ipList.Add(ip);

            var prevQu = GetLastOrDefault(quList);
            var qu = (0.2 * v3) + (0.8 * prevQu);
            quList.Add(qu);

            var prevPhase = GetLastOrDefault(phaseList);
            var phase = Math.Abs(ip + prevIp) > 0 ? Math.Atan(Math.Abs((qu + prevQu) / (ip + prevIp))).ToDegrees() : 0;
            phase = ip < 0 && qu > 0 ? 180 - phase : phase;
            phase = ip < 0 && qu < 0 ? 180 + phase : phase;
            phase = ip > 0 && qu < 0 ? 360 - phase : phase;
            phaseList.Add(phase);

            var dPhase = prevPhase - phase;
            dPhase = prevPhase < 90 && phase > 270 ? 360 + prevPhase - phase : dPhase;
            dPhase = MinOrMax(dPhase, 60, 1);
            dPhaseList.Add(dPhase);

            double instPeriod = 0, v4 = 0;
            for (var j = 0; j <= length3; j++)
            {
                var prevDPhase = i >= j ? dPhaseList[i - j] : 0;
                v4 += prevDPhase;
                instPeriod = v4 > 360 && instPeriod == 0 ? j : instPeriod;
            }

            var prevDcPeriod = GetLastOrDefault(dcPeriodList);
            var dcPeriod = (0.25 * instPeriod) + (0.75 * prevDcPeriod);
            dcPeriodList.Add(dcPeriod);

            double si = dcPeriod < length2 ? 0 : 1;
            siList.Add(si);

            var signal = GetCompareSignal(qu - (-1 * ip), prevQu - (-1 * prevIp));
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Esi", siList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(siList);
        stockData.IndicatorName = IndicatorName.EhlersSquelchIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Signal To Noise Ratio V1
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersSignalToNoiseRatioV1(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length = 7)
    {
        length = Math.Max(length, 1);
        List<double> ampList = new(stockData.Count);
        List<double> v2List = new(stockData.Count);
        List<double> rangeList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        var hilbertOutputs = GetOutputValuesInternal(stockData,
            data => CalculateEhlersHilbertTransformIndicator(data, length: length));
        var inPhaseList = hilbertOutputs["Inphase"];
        var quadList = hilbertOutputs["Quad"];
        var emaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentEma = emaList[i];
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var inPhase = inPhaseList[i];
            var quad = quadList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevEma = i >= 1 ? emaList[i - 1] : 0;

            var prevV2 = GetLastOrDefault(v2List);
            var v2 = (0.2 * ((inPhase * inPhase) + (quad * quad))) + (0.8 * prevV2);
            v2List.Add(v2);

            var prevRange = GetLastOrDefault(rangeList);
            var range = (0.2 * (currentHigh - currentLow)) + (0.8 * prevRange);
            rangeList.Add(range);

            var prevAmp = GetLastOrDefault(ampList);
            var temp = range != 0 ? v2 / (range * range) : 0;
            var logTemp = temp > 0 ? Math.Log10(temp) : 0;
            var amp = range != 0 ? (0.25 * ((10 * logTemp) + 1.9)) + (0.75 * prevAmp) : 0;
            ampList.Add(amp);

            var signal = GetVolatilitySignal(currentValue - currentEma, prevValue - prevEma, amp, 1.9);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Esnr", ampList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(ampList);
        stockData.IndicatorName = IndicatorName.EhlersSignalToNoiseRatioV1;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Triangle Window Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersTriangleWindowIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.EhlersTriangleMovingAverage,
        int length = 20)
    {
        length = Math.Max(1, length);
        var (input, _, _, open, _) = GetInputValuesList(stockData);
        using var window = new TriangleIndicatorWindow(maType, length);
        var external = Builder.Compute.ComponentAverage.HasOverrides || !TriangleIndicatorWindow.Supports(maType);
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
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Etwi", line }, { "Roc", roc } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.EhlersTriangleWindowIndicator;
        return stockData;
    }



    /// <summary>
    /// Calculates the Ehlers Simple Window Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersSimpleWindowIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 20)
    {
        length = Math.Max(1, length); var (input, _, _, _, _) = GetInputValuesList(stockData);
        using var window = new EhlersSimpleWindow(maType, length);
        List<double> filtList = new(stockData.Count), rocList = new(stockData.Count), filtered = new(stockData.Count); List<Signal>? signalsList = CreateSignalsList(stockData);
        if (Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType))
        {
            var derivList = input.Select((value, i) => EhlersSimpleWindow.Difference(value, stockData.OpenPrices[i]).Publish()).ToList();
            filtList = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(derivList), length)?.ToList() ?? GetMovingAverageList(stockData, maType, length, derivList);
            var second = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(filtList), length)?.ToList() ?? GetMovingAverageList(stockData, maType, length, filtList);
            filtered = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(second), length)?.ToList() ?? GetMovingAverageList(stockData, maType, length, second);
            for (var i = 0; i < input.Count; i++) rocList.Add(window.Finish(new RocBankValue(filtList[i]), new RocBankValue(filtered[i]), true).Roc);
        }
        else
        {
            for (var i = 0; i < input.Count; i++) { var value = window.Next(stockData.OpenPrices[i], input[i], true); filtList.Add(value.Line); rocList.Add(value.Roc); filtered.Add(value.Filtered); }
        }
        for (var i = 0; i < input.Count; i++) { var previous = i > 0 ? filtered[i - 1] : 0; var older = i > 1 ? filtered[i - 2] : 0; signalsList?.Add(GetCompareSignal(filtered[i] - previous, previous - older)); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Etwi", filtList },
            { "Roc", rocList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(filtList);
        stockData.IndicatorName = IndicatorName.EhlersSimpleWindowIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Signal To Noise Ratio V2
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersSignalToNoiseRatioV2(this StockData stockData, int length = 6)
    {
        length = Math.Max(length, 1);
        List<double> snrList = new(stockData.Count);
        List<double> rangeList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        var ehlersMamaOutputs = GetOutputValuesInternal(stockData,
            data => CalculateEhlersMotherOfAdaptiveMovingAverages(data));
        var i1List = ehlersMamaOutputs["I1"];
        var q1List = ehlersMamaOutputs["Q1"];
        var mamaList = GetCustomValuesListInternal(stockData,
            data => CalculateEhlersMotherOfAdaptiveMovingAverages(data));

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var prevMama = i >= 1 ? mamaList[i - 1] : 0;
            var i1 = i1List[i];
            var q1 = q1List[i];
            var mama = mamaList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevRange = GetLastOrDefault(rangeList);
            var range = (0.1 * (currentHigh - currentLow)) + (0.9 * prevRange);
            rangeList.Add(range);

            var temp = range != 0 ? ((i1 * i1) + (q1 * q1)) / (range * range) : 0;
            var logTemp = temp > 0 ? Math.Log10(temp) : 0;
            var prevSnr = GetLastOrDefault(snrList);
            var snr = range > 0 ? (0.25 * ((10 * logTemp) + length)) + (0.75 * prevSnr) : 0;
            snrList.Add(snr);

            var signal = GetVolatilitySignal(currentValue - mama, prevValue - prevMama, snr, length);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Esnr", snrList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(snrList);
        stockData.IndicatorName = IndicatorName.EhlersSignalToNoiseRatioV2;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Voss Predictive Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="predict"></param>
    /// <param name="bw"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersVossPredictiveFilter(this StockData stockData, int length = 20, double predict = 3, double bw = .25)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); var window = new VossPredictiveWindow(length, predict, bw);
        var voss = new List<double>(input.Count); var filter = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            var point = window.Next(input[i], true);
            signals?.Add(GetCompareSignal(point.Voss - point.Filter, i == 0 ? 0 : voss[i - 1] - filter[i - 1]));
            voss.Add(point.Voss); filter.Add(point.Filter);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Voss", voss }, { "Filt", filter } });
        stockData.SetSignals(signals); stockData.SetCustomValues(new List<double>()); stockData.IndicatorName = IndicatorName.EhlersVossPredictiveFilter;
        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Swiss Army Knife Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="delta"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersSwissArmyKnifeIndicator(this StockData stockData, int length = 20, double delta = 0.1)
    {
        length = Math.Max(length, 1);
        List<double> emaFilterList = new(stockData.Count);
        List<double> smaFilterList = new(stockData.Count);
        List<double> gaussFilterList = new(stockData.Count);
        List<double> butterFilterList = new(stockData.Count);
        List<double> smoothFilterList = new(stockData.Count);
        List<double> hpFilterList = new(stockData.Count);
        List<double> php2FilterList = new(stockData.Count);
        List<double> bpFilterList = new(stockData.Count);
        List<double> bsFilterList = new(stockData.Count);
        List<double> filterAvgList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var twoPiPrd = MinOrMax(2 * Math.PI / length, 0.99, 0.01);
        var deltaPrd = MinOrMax(2 * Math.PI * 2 * delta / length, 0.99, 0.01);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevPrice1 = i >= 1 ? inputList[i - 1] : 0;
            var prevPrice2 = i >= 2 ? inputList[i - 2] : 0;
            var prevPrice = i >= length ? inputList[i - length] : 0;
            var prevEmaFilter1 = GetLastOrDefault(emaFilterList);
            var prevSmaFilter1 = GetLastOrDefault(smaFilterList);
            var prevGaussFilter1 = GetLastOrDefault(gaussFilterList);
            var prevButterFilter1 = GetLastOrDefault(butterFilterList);
            var prevSmoothFilter1 = GetLastOrDefault(smoothFilterList);
            var prevHpFilter1 = GetLastOrDefault(hpFilterList);
            var prevPhp2Filter1 = GetLastOrDefault(php2FilterList);
            var prevBpFilter1 = GetLastOrDefault(bpFilterList);
            var prevBsFilter1 = GetLastOrDefault(bsFilterList);
            var prevEmaFilter2 = i >= 2 ? emaFilterList[i - 2] : 0;
            var prevSmaFilter2 = i >= 2 ? smaFilterList[i - 2] : 0;
            var prevGaussFilter2 = i >= 2 ? gaussFilterList[i - 2] : 0;
            var prevButterFilter2 = i >= 2 ? butterFilterList[i - 2] : 0;
            var prevSmoothFilter2 = i >= 2 ? smoothFilterList[i - 2] : 0;
            var prevHpFilter2 = i >= 2 ? hpFilterList[i - 2] : 0;
            var prevPhp2Filter2 = i >= 2 ? php2FilterList[i - 2] : 0;
            var prevBpFilter2 = i >= 2 ? bpFilterList[i - 2] : 0;
            var prevBsFilter2 = i >= 2 ? bsFilterList[i - 2] : 0;
            double alpha = (Math.Cos(twoPiPrd) + Math.Sin(twoPiPrd) - 1) / Math.Cos(twoPiPrd), c0 = 1, c1 = 0, b0 = alpha, b1 = 0, b2 = 0, a1 = 1 - alpha, a2 = 0;

            var emaFilter = i <= length ? currentValue :
                (c0 * ((b0 * currentValue) + (b1 * prevPrice1) + (b2 * prevPrice2))) + (a1 * prevEmaFilter1) + (a2 * prevEmaFilter2) - (c1 * prevPrice);
            emaFilterList.Add(emaFilter);

            var n = length; c0 = 1; c1 = (double)1 / n; b0 = (double)1 / n; b1 = 0; b2 = 0; a1 = 1; a2 = 0;
            var smaFilter = i <= length ? currentValue :
                (c0 * ((b0 * currentValue) + (b1 * prevPrice1) + (b2 * prevPrice2))) + (a1 * prevSmaFilter1) + (a2 * prevSmaFilter2) - (c1 * prevPrice);
            smaFilterList.Add(smaFilter);

            double beta = 2.415 * (1 - Math.Cos(twoPiPrd)), sqrtData = Pow(beta, 2) + (2 * beta), sqrt = Sqrt(sqrtData); alpha = (-1 * beta) + sqrt;
            c0 = Pow(alpha, 2); c1 = 0; b0 = 1; b1 = 0; b2 = 0; a1 = 2 * (1 - alpha); a2 = -(1 - alpha) * (1 - alpha);
            var gaussFilter = i <= length ? currentValue :
                (c0 * ((b0 * currentValue) + (b1 * prevPrice1) + (b2 * prevPrice2))) + (a1 * prevGaussFilter1) + (a2 * prevGaussFilter2) - (c1 * prevPrice);
            gaussFilterList.Add(gaussFilter);

            beta = 2.415 * (1 - Math.Cos(twoPiPrd)); sqrtData = (beta * beta) + (2 * beta); sqrt = sqrtData >= 0 ? Sqrt(sqrtData) : 0; alpha = (-1 * beta) + sqrt;
            c0 = Pow(alpha, 2) / 4; c1 = 0; b0 = 1; b1 = 2; b2 = 1; a1 = 2 * (1 - alpha); a2 = -(1 - alpha) * (1 - alpha);
            var butterFilter = i <= length ? currentValue :
                (c0 * ((b0 * currentValue) + (b1 * prevPrice1) + (b2 * prevPrice2))) + (a1 * prevButterFilter1) + (a2 * prevButterFilter2) - (c1 * prevPrice);
            butterFilterList.Add(butterFilter);

            c0 = (double)1 / 4; c1 = 0; b0 = 1; b1 = 2; b2 = 1; a1 = 0; a2 = 0;
            var smoothFilter = (c0 * ((b0 * currentValue) + (b1 * prevPrice1) + (b2 * prevPrice2))) + (a1 * prevSmoothFilter1) + 
                (a2 * prevSmoothFilter2) - (c1 * prevPrice);
            smoothFilterList.Add(smoothFilter);

            alpha = (Math.Cos(twoPiPrd) + Math.Sin(twoPiPrd) - 1) / Math.Cos(twoPiPrd); c0 = 1 - (alpha / 2); c1 = 0; b0 = 1; b1 = -1; b2 = 0; a1 = 1 - alpha; a2 = 0;
            var hpFilter = i <= length ? 0 :
                (c0 * ((b0 * currentValue) + (b1 * prevPrice1) + (b2 * prevPrice2))) + (a1 * prevHpFilter1) + (a2 * prevHpFilter2) - (c1 * prevPrice);
            hpFilterList.Add(hpFilter);

            beta = 2.415 * (1 - Math.Cos(twoPiPrd)); sqrtData = Pow(beta, 2) + (2 * beta); sqrt = sqrtData >= 0 ? Sqrt(sqrtData) : 0; alpha = (-1 * beta) + sqrt; 
            c0 = (1 - (alpha / 2)) * (1 - (alpha / 2)); c1 = 0; b0 = 1; b1 = -2; b2 = 1; a1 = 2 * (1 - alpha); a2 = -(1 - alpha) * (1 - alpha);
            var php2Filter = i <= length ? 0 :
                (c0 * ((b0 * currentValue) + (b1 * prevPrice1) + (b2 * prevPrice2))) + (a1 * prevPhp2Filter1) + (a2 * prevPhp2Filter2) - (c1 * prevPrice);
            php2FilterList.Add(php2Filter);

            beta = Math.Cos(twoPiPrd); var gamma = 1 / Math.Cos(deltaPrd); sqrtData = Pow(gamma, 2) - 1; sqrt = Sqrt(sqrtData);
            alpha = gamma - sqrt; c0 = (1 - alpha) / 2; c1 = 0; b0 = 1; b1 = 0; b2 = -1; a1 = beta * (1 + alpha); a2 = alpha * -1;
            var bpFilter = i <= length ? currentValue :
                (c0 * ((b0 * currentValue) + (b1 * prevPrice1) + (b2 * prevPrice2))) + (a1 * prevBpFilter1) + (a2 * prevBpFilter2) - (c1 * prevPrice);
            bpFilterList.Add(bpFilter);

            beta = Math.Cos(twoPiPrd); gamma = 1 / Math.Cos(deltaPrd); sqrtData = Pow(gamma, 2) - 1; sqrt = sqrtData >= 0 ? Sqrt(sqrtData) : 0;
            alpha = gamma - sqrt; c0 = (1 + alpha) / 2; c1 = 0; b0 = 1; b1 = -2 * beta; b2 = 1; a1 = beta * (1 + alpha); a2 = alpha * -1;
            var bsFilter = i <= length ? currentValue :
                (c0 * ((b0 * currentValue) + (b1 * prevPrice1) + (b2 * prevPrice2))) + (a1 * prevBsFilter1) + (a2 * prevBsFilter2) - (c1 * prevPrice);
            bsFilterList.Add(bsFilter);

            var signal = GetCompareSignal(smaFilter - prevSmaFilter1, prevSmaFilter1 - prevSmaFilter2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "EmaFilter", emaFilterList },
            { "SmaFilter", smaFilterList },
            { "GaussFilter", gaussFilterList },
            { "ButterFilter", butterFilterList },
            { "SmoothFilter", smoothFilterList },
            { "HpFilter", hpFilterList },
            { "PhpFilter", php2FilterList },
            { "BpFilter", bpFilterList },
            { "BsFilter", bsFilterList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(smaFilterList);
        stockData.IndicatorName = IndicatorName.EhlersSwissArmyKnifeIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Universal Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersUniversalOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 20, int signalLength = 9)
    {
        signalLength = Math.Max(1, signalLength); var (input, _, _, _, _) = GetInputValuesList(stockData);
        using var window = new UniversalOscillatorWindow(maType, length, signalLength); var line = new List<double>(input.Count); List<double> signal;
        if (Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType))
        {
            foreach (var price in input) line.Add(window.Line(price, true));
            signal = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(line), signalLength)?.ToList() ?? GetMovingAverageList(stockData, maType, signalLength, line);
        }
        else
        {
            signal = new(input.Count);
            foreach (var price in input) { var value = window.Next(price, true); line.Add(value.Line); signal.Add(value.Signal); }
        }
        var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++) signals?.Add(GetCompareSignal(line[i] - signal[i], i == 0 ? 0 : line[i - 1] - signal[i - 1]));
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Euo", line }, { "Signal", signal } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.EhlersUniversalOscillator; return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Zero Crossings Dominant Cycle
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="bw"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersZeroCrossingsDominantCycle(this StockData stockData, int length = 20, double bw = 0.7)
    {
        length = Math.Max(length, 1);
        List<double> dcList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var counter = 0;

        var ebpfOutputs = GetOutputValuesInternal(stockData,
            data => CalculateEhlersBandPassFilterV1(data, length, bw));
        var realList = ebpfOutputs["Ebpf"];
        var triggerList = ebpfOutputs["Signal"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var real = realList[i];
            var trigger = triggerList[i];
            var prevReal = i >= 1 ? realList[i - 1] : 0;
            var prevTrigger = i >= 1 ? triggerList[i - 1] : 0;

            var prevDc = GetLastOrDefault(dcList);
            var dc = Math.Max(prevDc, 6);
            counter += 1;
            if ((real > 0 && prevReal <= 0) || (real < 0 && prevReal >= 0))
            {
                dc = MinOrMax(2 * counter, 1.25 * prevDc, 0.8 * prevDc);
                counter = 0;
            }
            dcList.Add(dc);

            var signal = GetCompareSignal(real - trigger, prevReal - prevTrigger);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ezcdc", dcList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(dcList);
        stockData.IndicatorName = IndicatorName.EhlersZeroCrossingsDominantCycle;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Stochastic Cyber Cycle
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="alpha"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersStochasticCyberCycle(this StockData stockData, int length = 14, double alpha = .7)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); var window = new StochasticCyberWindow(length, alpha);
        var line = new List<double>(input.Count); var trigger = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            var point = window.Next(input[i], true); var previous = i == 0 ? 0 : line[i-1]; var previousTrigger = i == 0 ? 0 : trigger[i-1];
            signals?.Add(GetRsiSignal(point.Line-point.Signal, previous-previousTrigger, point.Line, previous, .5, -.5)); line.Add(point.Line); trigger.Add(point.Signal);
        }
        stockData.SetOutputValues(() => new Dictionary<string,List<double>> { { "Escc", line }, { "Signal", trigger } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.EhlersStochasticCyberCycle;
        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Stochastic
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersStochastic(this StockData stockData, MovingAvgType maType = MovingAvgType.Ehlers2PoleSuperSmootherFilterV1, 
        int length1 = 48, int length2 = 20, int length3 = 10)
    {
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        length3 = Math.Max(length3, 1);
        List<double> stoch2PoleList = new(stockData.Count);
        List<double> arg2PoleList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var roofingFilter2PoleList = GetCustomValuesListInternal(stockData,
            data => CalculateEhlersRoofingFilterV1(data, maType, length1, length3));
        var (max2PoleList, min2PoleList) = GetMaxAndMinValuesList(roofingFilter2PoleList, length2);

        for (var i = 0; i < stockData.Count; i++)
        {
            var rf2Pole = roofingFilter2PoleList[i];
            var min2Pole = min2PoleList[i];
            var max2Pole = max2PoleList[i];

            var prevStoch2Pole = GetLastOrDefault(stoch2PoleList);
            var stoch2Pole = max2Pole - min2Pole != 0 ? MinOrMax((rf2Pole - min2Pole) / (max2Pole - min2Pole), 1, 0) : 0;
            stoch2PoleList.Add(stoch2Pole);

            var arg2Pole = (stoch2Pole + prevStoch2Pole) / 2;
            arg2PoleList.Add(arg2Pole);
        }

        var estoch2PoleList = GetMovingAverageList(stockData, maType, length2, arg2PoleList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var estoch2Pole = estoch2PoleList[i];
            var prevEstoch2Pole1 = i >= 1 ? estoch2PoleList[i - 1] : 0;
            var prevEstoch2Pole2 = i >= 2 ? estoch2PoleList[i - 2] : 0;

            var signal = GetRsiSignal(estoch2Pole - prevEstoch2Pole1, prevEstoch2Pole1 - prevEstoch2Pole2, estoch2Pole, prevEstoch2Pole1, 0.8, 0.2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Es", estoch2PoleList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(estoch2PoleList);
        stockData.IndicatorName = IndicatorName.EhlersStochastic;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Triple Delay Line Detrender
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersTripleDelayLineDetrender(this StockData stockData, MovingAvgType maType = MovingAvgType.EhlersModifiedOptimumEllipticFilter, int length = 14)
    {
        length = Math.Max(1, length); var (input, _, _, _, _) = GetInputValuesList(stockData); using var window = new TripleDelayWindow(maType, length);
        var line = new List<double>(input.Count); var signal = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        if (Builder.Compute.ComponentAverage.HasOverrides || !TripleDelayWindow.Supports(maType))
        {
            var raw = new List<double>(input.Count); for (var i = 0; i < input.Count; i++) raw.Add(window.Detrend(input[i], true).Publish());
            line = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(raw), length)?.ToList() ?? GetMovingAverageList(stockData, maType, length, raw);
            signal = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(line), length)?.ToList() ?? GetMovingAverageList(stockData, maType, length, line);
        }
        else for (var i = 0; i < input.Count; i++) { var point = window.Next(input[i], true); line.Add(point.Line); signal.Add(point.Signal); }
        for (var i = 0; i < input.Count; i++) signals?.Add(GetCompareSignal(line[i] - signal[i], i == 0 ? 0 : line[i - 1] - signal[i - 1]));
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Etdld", line }, { "Signal", signal } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.EhlersTripleDelayLineDetrender;
        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Sine Wave Indicator V1
    /// </summary>
    /// <param name="stockData"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersSineWaveIndicatorV1(this StockData stockData)
    {
        List<double> sineList = new(stockData.Count);
        List<double> leadSineList = new(stockData.Count);
        List<double> dcPhaseList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var ehlersMamaOutputs = GetOutputValuesInternal(stockData,
            data => CalculateEhlersMotherOfAdaptiveMovingAverages(data));
        var spList = ehlersMamaOutputs["SmoothPeriod"];
        var smoothList = ehlersMamaOutputs["Smooth"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var sp = spList[i];
            var dcPeriod = (int)Math.Ceiling(sp + 0.5);

            double realPart = 0, imagPart = 0, projectionScale = 0;
            for (var j = 0; j <= dcPeriod - 1; j++)
            {
                var prevSmooth = i >= j ? smoothList[i - j] : 0;
                projectionScale += Math.Abs(prevSmooth);
                realPart += Math.Sin(2 * Math.PI * ((double)j / dcPeriod)) * prevSmooth;
                imagPart += Math.Cos(2 * Math.PI * ((double)j / dcPeriod)) * prevSmooth;
            }

            var resolution = 64 * 2.2204460492503131e-16 * projectionScale;
            if (Math.Abs(realPart) <= resolution) realPart = 0;
            if (Math.Abs(imagPart) <= resolution) imagPart = 0;
            var dcPhase = Math.Abs(imagPart) > 0.001 ? Math.Atan(realPart / imagPart).ToDegrees() : 90 * Math.Sign(realPart);
            dcPhase += 90;
            dcPhase += sp != 0 ? 360 / sp : 0;
            dcPhase += imagPart < 0 ? 180 : 0;
            dcPhase -= dcPhase > 315 ? 360 : 0;
            dcPhaseList.Add(dcPhase);

            var prevSine = GetLastOrDefault(sineList);
            var sine = Math.Sin(dcPhase.ToRadians());
            sineList.Add(sine);

            var prevLeadSine = GetLastOrDefault(leadSineList);
            var leadSine = Math.Sin((dcPhase + 45).ToRadians());
            leadSineList.Add(leadSine);

            var signal = GetCompareSignal(sine - leadSine, prevSine - prevLeadSine);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Sine", sineList },
            { "LeadSine", leadSineList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(sineList);
        stockData.IndicatorName = IndicatorName.EhlersSineWaveIndicatorV1;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Sine Wave Indicator V2
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="alpha"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateEhlersSineWaveIndicatorV2(this StockData stockData, int length = 5, double alpha = 0.07)
    {
        var callerSeries = stockData.CaptureInputSeries();
        length = Math.Max(length, 1);
        List<double> sineList = new(stockData.Count);
        List<double> leadSineList = new(stockData.Count);
        List<double> dcPhaseList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var periodList = GetOutputValuesInternal(stockData,
            data => CalculateEhlersAdaptiveCyberCycle(data, length, alpha))["Period"];
        // The next component reads the caller's series, not the previous component's output.
        stockData.RestoreInputSeries(callerSeries);
        var cycleList = GetCustomValuesListInternal(stockData,
            data => CalculateEhlersCyberCycle(data));

        for (var i = 0; i < stockData.Count; i++)
        {
            var period = periodList[i];
            var dcPeriod = MathHelper.CeilingCycle(period);

            double realPart = 0, imagPart = 0, projectionScale = 0;
            for (var j = 0; j <= dcPeriod - 1; j++)
            {
                var prevCycle = i >= j ? cycleList[i - j] : 0;
                projectionScale += Math.Abs(prevCycle);
                realPart += Math.Sin(2 * Math.PI * ((double)j / dcPeriod)) * prevCycle;
                imagPart += Math.Cos(2 * Math.PI * ((double)j / dcPeriod)) * prevCycle;
            }

            var resolution = 64 * 2.2204460492503131e-16 * projectionScale;
            if (Math.Abs(realPart) <= resolution) realPart = 0;
            if (Math.Abs(imagPart) <= resolution) imagPart = 0;
            var dcPhase = Math.Abs(imagPart) > 0.001 ? Math.Atan(realPart / imagPart).ToDegrees() : 90 * Math.Sign(realPart);
            dcPhase += 90;
            dcPhase += imagPart < 0 ? 180 : 0;
            dcPhase -= dcPhase > 315 ? 360 : 0;
            dcPhaseList.Add(dcPhase);

            var prevSine = GetLastOrDefault(sineList);
            var sine = Math.Sin(dcPhase.ToRadians());
            sineList.Add(sine);

            var prevLeadSine = GetLastOrDefault(leadSineList);
            var leadSine = Math.Sin((dcPhase + 45).ToRadians());
            leadSineList.Add(leadSine);

            var signal = GetCompareSignal(sine - leadSine, prevSine - prevLeadSine);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Sine", sineList },
            { "LeadSine", leadSineList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(sineList);
        stockData.IndicatorName = IndicatorName.EhlersSineWaveIndicatorV2;

        return stockData;
    }

}

