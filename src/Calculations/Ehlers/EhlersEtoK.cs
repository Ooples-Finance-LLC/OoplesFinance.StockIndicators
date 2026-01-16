
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
    public static StockData CalculateEhlersHighPassFilterV1(this StockData stockData, int length = 125, double mult = 1)
    {
        length = Math.Max(length, 1);
        List<double> highPassList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var alphaArg = MinOrMax(2 * Math.PI / (mult * length * Sqrt(2)), 0.99, 0.01);
        var alphaCos = Math.Cos(alphaArg);
        var alpha = alphaCos != 0 ? (alphaCos + Math.Sin(alphaArg) - 1) / alphaCos : 0;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue1 = i >= 1 ? inputList[i - 1] : 0;
            var prevValue2 = i >= 2 ? inputList[i - 2] : 0;
            var prevHp1 = i >= 1 ? highPassList[i - 1] : 0;
            var prevHp2 = i >= 2 ? highPassList[i - 2] : 0;
            var pow1 = Pow(1 - (alpha / 2), 2);
            var pow2 = Pow(1 - alpha, 2);

            var highPass = (pow1 * (currentValue - (2 * prevValue1) + prevValue2)) + (2 * (1 - alpha) * prevHp1) - (pow2 * prevHp2);
            highPassList.Add(highPass);

            var signal = GetCompareSignal(highPass, prevHp1);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Hp", highPassList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(highPassList);
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
    public static StockData CalculateEhlersHighPassFilterV2(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 20)
    {
        length = Math.Max(length, 1);
        List<double> hpList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var a1 = Exp(-MathHelper.Sqrt2 * Math.PI / length);
        var b1 = 2 * a1 * Math.Cos(MathHelper.Sqrt2 * Math.PI / length);
        var c2 = b1;
        var c3 = -a1 * a1;
        var c1 = (1 + c2 - c3) / 4;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue1 = i >= 1 ? inputList[i - 1] : 0;
            var prevValue2 = i >= 2 ? inputList[i - 2] : 0;
            var prevHp1 = i >= 1 ? hpList[i - 1] : 0;
            var prevHp2 = i >= 2 ? hpList[i - 2] : 0;

            var hp = i < 4 ? 0 : (c1 * (currentValue - (2 * prevValue1) + prevValue2)) + (c2 * prevHp1) + (c3 * prevHp2);
            hpList.Add(hp);
        }

        var hpMa1List = GetMovingAverageList(stockData, maType, length, hpList);
        var hpMa2List = GetMovingAverageList(stockData, maType, length, hpMa1List);
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
    public static StockData CalculateEhlersHpLpRoofingFilter(this StockData stockData, int length1 = 48, int length2 = 10)
    {
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        List<double> highPassList = new(stockData.Count);
        List<double> roofingFilterList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var alphaArg = Math.Min(2 * Math.PI / length1, 0.99);
        var alphaCos = Math.Cos(alphaArg);
        var alpha1 = alphaCos != 0 ? (alphaCos + Math.Sin(alphaArg) - 1) / alphaCos : 0;
        var a1 = Exp(-MathHelper.Sqrt2 * Math.PI / length2);
        var b1 = 2 * a1 * Math.Cos(Math.Min(MathHelper.Sqrt2 * Math.PI / length2, 0.99));
        var c2 = b1;
        var c3 = -1 * a1 * a1;
        var c1 = 1 - c2 - c3;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevFilter1 = i >= 1 ? roofingFilterList[i - 1] : 0;
            var prevFilter2 = i >= 2 ? roofingFilterList[i - 2] : 0;
            var prevHp1 = i >= 1 ? highPassList[i - 1] : 0;

            var hp = ((1 - (alpha1 / 2)) * MinPastValues(i, 1, currentValue - prevValue)) + ((1 - alpha1) * prevHp1);
            highPassList.Add(hp);

            var filter = (c1 * ((hp + prevHp1) / 2)) + (c2 * prevFilter1) + (c3 * prevFilter2);
            roofingFilterList.Add(filter);

            var signal = GetCompareSignal(filter - prevFilter1, prevFilter1 - prevFilter2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ehplprf", roofingFilterList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(roofingFilterList);
        stockData.IndicatorName = IndicatorName.EhlersHpLpRoofingFilter;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Hurst Coefficient
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersHurstCoefficient(this StockData stockData, int length1 = 30, int length2 = 20)
    {
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        List<double> dimenList = new(stockData.Count);
        List<double> hurstList = new(stockData.Count);
        List<double> smoothHurstList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var hLength = (int)Math.Ceiling((double)length1 / 2);
        var a1 = Exp(-MathHelper.Sqrt2 * Math.PI / length2);
        var b1 = 2 * a1 * Math.Cos(Math.Min(MathHelper.Sqrt2 * Math.PI / length2, 0.99));
        var c2 = b1;
        var c3 = -a1 * a1;
        var c1 = 1 - c2 - c3;

        var (hh3List, ll3List) = GetMaxAndMinValuesList(inputList, length1);
        var (hh1List, ll1List) = GetMaxAndMinValuesList(inputList, hLength);

        for (var i = 0; i < stockData.Count; i++)
        {
            var hh3 = hh3List[i];
            var ll3 = ll3List[i];
            var hh1 = hh1List[i];
            var ll1 = ll1List[i];
            var currentValue = inputList[i];
            var priorValue = i >= hLength ? inputList[i - hLength] : currentValue;
            var prevSmoothHurst1 = i >= 1 ? smoothHurstList[i - 1] : 0;
            var prevSmoothHurst2 = i >= 2 ? smoothHurstList[i - 2] : 0;
            var n3 = (hh3 - ll3) / length1;
            var n1 = (hh1 - ll1) / hLength;
            var hh2 = i >= hLength ? priorValue : currentValue;
            var ll2 = i >= hLength ? priorValue : currentValue;

            for (var j = hLength; j < length1; j++)
            {
                var price = i >= j ? inputList[i - j] : 0;
                hh2 = price > hh2 ? price : hh2;
                ll2 = price < ll2 ? price : ll2;
            }
            var n2 = (hh2 - ll2) / hLength;

            var prevDimen = GetLastOrDefault(dimenList);
            var dimen = 0.5 * (((Math.Log(n1 + n2) - Math.Log(n3)) / Math.Log(2)) + prevDimen);
            dimenList.Add(dimen);

            var prevHurst = GetLastOrDefault(hurstList);
            var hurst = 2 - dimen;
            hurstList.Add(hurst);

            var smoothHurst = (c1 * ((hurst + prevHurst) / 2)) + (c2 * prevSmoothHurst1) + (c3 * prevSmoothHurst2);
            smoothHurstList.Add(smoothHurst);

            var signal = GetCompareSignal(smoothHurst - prevSmoothHurst1, prevSmoothHurst1 - prevSmoothHurst2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ehc", smoothHurstList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(smoothHurstList);
        stockData.IndicatorName = IndicatorName.EhlersHurstCoefficient;

        return stockData;
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
    public static StockData CalculateEhlersEmpiricalModeDecomposition(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length1 = 20, int length2 = 50, double delta = 0.5, double fraction = 0.1)
    {
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        List<double> peakList = new(stockData.Count);
        List<double> valleyList = new(stockData.Count);
        List<double> peakAvgFracList = new(stockData.Count);
        List<double> valleyAvgFracList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var eteList = CalculateEhlersTrendExtraction(stockData, maType, length1, delta);
        var trendList = eteList.OutputValues["Trend"];
        var bpList = eteList.OutputValues["Bp"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevBp1 = i >= 1 ? bpList[i - 1] : 0;
            var prevBp2 = i >= 2 ? bpList[i - 2] : 0;
            var bp = bpList[i];

            var prevPeak = GetLastOrDefault(peakList);
            var peak = prevBp1 > bp && prevBp1 > prevBp2 ? prevBp1 : prevPeak;
            peakList.Add(peak);

            var prevValley = GetLastOrDefault(valleyList);
            var valley = prevBp1 < bp && prevBp1 < prevBp2 ? prevBp1 : prevValley;
            valleyList.Add(valley);
        }

        var peakAvgList = GetMovingAverageList(stockData, maType, length2, peakList);
        var valleyAvgList = GetMovingAverageList(stockData, maType, length2, valleyList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var peakAvg = peakAvgList[i];
            var valleyAvg = valleyAvgList[i];
            var trend = trendList[i];
            var prevTrend = i >= 1 ? trendList[i - 1] : 0;

            var prevPeakAvgFrac = GetLastOrDefault(peakAvgFracList);
            var peakAvgFrac = fraction * peakAvg;
            peakAvgFracList.Add(peakAvgFrac);

            var prevValleyAvgFrac = GetLastOrDefault(valleyAvgFracList);
            var valleyAvgFrac = fraction * valleyAvg;
            valleyAvgFracList.Add(valleyAvgFrac);

            var signal = GetBullishBearishSignal(trend - Math.Max(peakAvgFrac, valleyAvgFrac), prevTrend - Math.Max(prevPeakAvgFrac, prevValleyAvgFrac),
                trend - Math.Min(peakAvgFrac, valleyAvgFrac), prevTrend - Math.Min(prevPeakAvgFrac, prevValleyAvgFrac));
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Trend", trendList },
            { "Peak", peakAvgFracList },
            { "Valley", valleyAvgFracList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.EhlersEmpiricalModeDecomposition;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Early Onset Trend Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="k"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersEarlyOnsetTrendIndicator(this StockData stockData, int length1 = 30, int length2 = 100, double k = 0.85)
    {
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        List<double> peakList = new(stockData.Count);
        List<double> quotientList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var hpList = CalculateEhlersHighPassFilterV1(stockData, length2, 1).CustomValuesList;
        stockData.SetCustomValues(hpList);
        var superSmoothList = CalculateEhlersSuperSmootherFilter(stockData, length1).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var filter = superSmoothList[i];

            var prevPeak = GetLastOrDefault(peakList);
            var peak = Math.Abs(filter) > 0.991 * prevPeak ? Math.Abs(filter) : 0.991 * prevPeak;
            peakList.Add(peak);

            var ratio = peak != 0 ? filter / peak : 0;
            var prevQuotient = GetLastOrDefault(quotientList);
            var quotient = (k * ratio) + 1 != 0 ? (ratio + k) / ((k * ratio) + 1) : 0;
            quotientList.Add(quotient);

            var signal = GetCompareSignal(quotient, prevQuotient);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Eoti", quotientList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(quotientList);
        stockData.IndicatorName = IndicatorName.EhlersEarlyOnsetTrendIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Impulse Response
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="bw"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersImpulseResponse(this StockData stockData, MovingAvgType maType = MovingAvgType.EhlersHannMovingAverage,
        int length = 20, double bw = 1)
    {
        length = Math.Max(length, 1);
        List<double> bpList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var hannLength = MinOrMax((int)Math.Ceiling(length / 1.4));
        var l1 = Math.Cos(MinOrMax(2 * Math.PI / length, 0.99, 0.01));
        var g1 = Math.Cos(MinOrMax(bw * 2 * Math.PI / length, 0.99, 0.01));
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

        var filtList = GetMovingAverageList(stockData, maType, hannLength, bpList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var filt = filtList[i];
            var prevFilt1 = i >= 1 ? filtList[i - 1] : 0;
            var prevFilt2 = i >= 2 ? filtList[i - 2] : 0;

            var signal = GetCompareSignal(filt - prevFilt1, prevFilt1 - prevFilt2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Eir", filtList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(filtList);
        stockData.IndicatorName = IndicatorName.EhlersImpulseResponse;

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
    public static StockData CalculateEhlersImpulseReaction(this StockData stockData, int length1 = 2, int length2 = 20, double qq = 0.9)
    {
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        List<double> reactionList = new(stockData.Count);
        List<double> ireactList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var c2 = 2 * qq * Math.Cos(2 * Math.PI / length2);
        var c3 = -qq * qq;
        var c1 = (1 + c3) / 2;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var priorValue = i >= length1 ? inputList[i - length1] : 0;
            var prevReaction1 = i >= 1 ? reactionList[i - 1] : 0;
            var prevReaction2 = i >= 2 ? reactionList[i - 2] : 0;
            var prevIReact1 = i >= 1 ? ireactList[i - 1] : 0;
            var prevIReact2 = i >= 2 ? ireactList[i - 2] : 0;

            var reaction = (c1 * (currentValue - priorValue)) + (c2 * prevReaction1) + (c3 * prevReaction2);
            reactionList.Add(reaction);

            var ireact = currentValue != 0 ? 100 * reaction / currentValue : 0;
            ireactList.Add(ireact);

            var signal = GetCompareSignal(ireact - prevIReact1, prevIReact1 - prevIReact2);
            signalsList?.Add(signal);
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

            var efdso2Pole = Math.Abs(currentScaledFilter2Pole) < 2 ? 0.5 * Math.Log((1 + (currentScaledFilter2Pole / 2)) / 
                (1 - (currentScaledFilter2Pole / 2))) : prevEfdsoPole1;
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
    public static StockData CalculateEhlersHilbertTransformIndicator(this StockData stockData, int length = 7, double iMult = 0.635, double qMult = 0.338)
    {
        length = Math.Max(length, 1);
        List<double> v1List = new(stockData.Count);
        List<double> inPhaseList = new(stockData.Count);
        List<double> quadList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= length ? inputList[i - length] : 0;
            var v2 = i >= 2 ? v1List[i - 2] : 0;
            var v4 = i >= 4 ? v1List[i - 4] : 0;
            var inPhase3 = i >= 3 ? inPhaseList[i - 3] : 0;
            var quad2 = i >= 2 ? quadList[i - 2] : 0;

            var v1 = MinPastValues(i, length, currentValue - prevValue);
            v1List.Add(v1);

            var prevInPhase = GetLastOrDefault(inPhaseList);
            var inPhase = (1.25 * (v4 - (iMult * v2))) + (iMult * inPhase3);
            inPhaseList.Add(inPhase);

            var prevQuad = GetLastOrDefault(quadList);
            var quad = v2 - (qMult * v1) + (qMult * quad2);
            quadList.Add(quad);

            var signal = GetCompareSignal(quad - (-1 * inPhase), prevQuad - (-1 * prevInPhase));
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Quad", quadList },
            { "Inphase", inPhaseList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.EhlersHilbertTransformIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Instantaneous Phase Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersInstantaneousPhaseIndicator(this StockData stockData, int length1 = 7, int length2 = 50)
    {
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        List<double> phaseList = new(stockData.Count);
        List<double> dPhaseList = new(stockData.Count);
        List<double> dcPeriodList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var ehtList = CalculateEhlersHilbertTransformIndicator(stockData, length: length1);
        var ipList = ehtList.OutputValues["Inphase"];
        var quList = ehtList.OutputValues["Quad"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var ip = ipList[i];
            var qu = quList[i];
            var prevIp = i >= 1 ? ipList[i - 1] : 0;
            var prevQu = i >= 1 ? quList[i - 1] : 0;

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
            for (var j = 0; j <= length2; j++)
            {
                var prevDPhase = i >= j ? dPhaseList[i - j] : 0;
                v4 += prevDPhase;
                instPeriod = v4 > 360 && instPeriod == 0 ? j : instPeriod;
            }

            var prevDcPeriod = GetLastOrDefault(dcPeriodList);
            var dcPeriod = (0.25 * instPeriod) + (0.75 * prevDcPeriod);
            dcPeriodList.Add(dcPeriod);

            var signal = GetCompareSignal(qu - (-1 * ip), prevQu - (-1 * prevIp));
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Eipi", dcPeriodList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(dcPeriodList);
        stockData.IndicatorName = IndicatorName.EhlersInstantaneousPhaseIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Hilbert Transformer
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersHilbertTransformer(this StockData stockData, int length1 = 48, int length2 = 20)
    {
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        List<double> peakList = new(stockData.Count);
        List<double> realList = new(stockData.Count);
        List<double> imagList = new(stockData.Count);
        List<double> qPeakList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var roofingFilterList = CalculateEhlersRoofingFilterV2(stockData, length1, length2).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var roofingFilter = roofingFilterList[i];
            var prevReal1 = i >= 1 ? realList[i - 1] : 0;
            var prevReal2 = i >= 2 ? realList[i - 2] : 0;

            var prevPeak = GetLastOrDefault(peakList);
            var peak = Math.Max(0.991 * prevPeak, Math.Abs(roofingFilter));
            peakList.Add(peak);

            var prevReal = GetLastOrDefault(realList);
            var real = peak != 0 ? roofingFilter / peak : 0;
            realList.Add(real);

            var qFilt = real - prevReal;
            var prevQPeak = GetLastOrDefault(qPeakList);
            var qPeak = Math.Max(0.991 * prevQPeak, Math.Abs(qFilt));
            qPeakList.Add(qPeak);

            var imag = qPeak != 0 ? qFilt / qPeak : 0;
            imagList.Add(imag);

            var signal = GetCompareSignal(real - prevReal1, prevReal1 - prevReal2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Real", realList },
            { "Imag", imagList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.EhlersHilbertTransformer;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Hilbert Transformer Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersHilbertTransformerIndicator(this StockData stockData, int length1 = 48, int length2 = 20, int length3 = 10)
    {
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        length3 = Math.Max(length3, 1);
        List<double> peakList = new(stockData.Count);
        List<double> realList = new(stockData.Count);
        List<double> imagList = new(stockData.Count);
        List<double> qFiltList = new(stockData.Count);
        List<double> qPeakList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var a1 = Exp(-1.414 * Math.PI / length3);
        var b2 = 2 * a1 * Math.Cos(1.414 * Math.PI / length3);
        var c2 = b2;
        var c3 = -a1 * a1;
        var c1 = 1 - c2 - c3;

        var roofingFilterList = CalculateEhlersRoofingFilterV2(stockData, length1, length2).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var roofingFilter = roofingFilterList[i];
            var prevQFilt = i >= 1 ? qFiltList[i - 1] : 0;
            var prevImag1 = i >= 1 ? imagList[i - 1] : 0;
            var prevImag2 = i >= 2 ? imagList[i - 2] : 0;

            var prevPeak = GetLastOrDefault(peakList);
            var peak = Math.Max(0.991 * prevPeak, Math.Abs(roofingFilter));
            peakList.Add(peak);

            var prevReal = GetLastOrDefault(realList);
            var real = peak != 0 ? roofingFilter / peak : 0;
            realList.Add(real);

            var qFilt = real - prevReal;
            var prevQPeak = GetLastOrDefault(qPeakList);
            var qPeak = Math.Max(0.991 * prevQPeak, Math.Abs(qFilt));
            qPeakList.Add(qPeak);

            qFilt = qPeak != 0 ? qFilt / qPeak : 0;
            qFiltList.Add(qFilt);

            var imag = (c1 * ((qFilt + prevQFilt) / 2)) + (c2 * prevImag1) + (c3 * prevImag2);
            imagList.Add(imag);

            var signal = GetCompareSignal(imag - qFilt, prevImag1 - prevQFilt);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Real", realList },
            { "Imag", imagList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.EhlersHilbertTransformerIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Homodyne Dominant Cycle
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersHomodyneDominantCycle(this StockData stockData, int length1 = 48, int length2 = 20, int length3 = 10)
    {
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        length3 = Math.Max(length3, 1);
        List<double> periodList = new(stockData.Count);
        List<double> domCycList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var a1 = Exp(-1.414 * Math.PI / length2);
        var b1 = 2 * a1 * Math.Cos(1.414 * Math.PI / length2);
        var c2 = b1;
        var c3 = -a1 * a1;
        var c1 = 1 - c2 - c3;

        var hilbertList = CalculateEhlersHilbertTransformer(stockData, length1, length2);
        var realList = hilbertList.OutputValues["Real"];
        var imagList = hilbertList.OutputValues["Imag"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var real = realList[i];
            var imag = imagList[i];
            var prevReal1 = i >= 1 ? realList[i - 1] : 0;
            var prevImag1 = i >= 1 ? imagList[i - 1] : 0;
            var prevReal2 = i >= 2 ? realList[i - 2] : 0;
            var prevDomCyc1 = i >= 1 ? domCycList[i - 1] : 0;
            var prevDomCyc2 = i >= 2 ? domCycList[i - 2] : 0;
            var re = (real * prevReal1) + (imag * prevImag1);
            var im = (prevReal1 * imag) - (real * prevImag1);

            var prevPeriod = GetLastOrDefault(periodList);
            var period = im != 0 && re != 0 ? 2 * Math.PI / Math.Abs(im / re) : 0;
            period = MinOrMax(period, length1, length3);
            periodList.Add(period);

            var domCyc = (c1 * ((period + prevPeriod) / 2)) + (c2 * prevDomCyc1) + (c3 * prevDomCyc2);
            domCycList.Add(domCyc);

            var signal = GetCompareSignal(real - prevReal1, prevReal1 - prevReal2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ehdc", domCycList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(domCycList);
        stockData.IndicatorName = IndicatorName.EhlersHomodyneDominantCycle;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Hann Window Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersHannWindowIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.EhlersHannMovingAverage, int length = 20)
    {
        length = Math.Max(length, 1);
        List<double> rocList = new(stockData.Count);
        List<double> derivList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, openList, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentOpen = openList[i];
            var currentValue = inputList[i];
            
            var deriv = currentValue - currentOpen;
            derivList.Add(deriv);
        }

        var filtList = GetMovingAverageList(stockData, maType, length, derivList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var filt = filtList[i];
            var prevFilt1 = i >= 1 ? filtList[i - 1] : 0;
            var prevFilt2 = i >= 2 ? filtList[i - 2] : 0;

            var roc = length / 2 * Math.PI * (filt - prevFilt1);
            rocList.Add(roc);

            var signal = GetCompareSignal(filt - prevFilt1, prevFilt1 - prevFilt2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ehwi", filtList },
            { "Roc", rocList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(filtList);
        stockData.IndicatorName = IndicatorName.EhlersHannWindowIndicator;

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
    public static StockData CalculateEhlersHammingWindowIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.EhlersHammingMovingAverage,
        int length = 20, double pedestal = 10)
    {
        length = Math.Max(length, 1);
        List<double> rocList = new(stockData.Count);
        List<double> derivList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, openList, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentOpen = openList[i];
            var currentValue = inputList[i];
            

            var deriv = currentValue - currentOpen;
            derivList.Add(deriv);
        }

        var filtList = GetMovingAverageList(stockData, maType, length, derivList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var filt = filtList[i];
            var prevFilt1 = i >= 1 ? filtList[i - 1] : 0;
            var prevFilt2 = i >= 2 ? filtList[i - 2] : 0;

            var roc = length / 2 * Math.PI * (filt - prevFilt1);
            rocList.Add(roc);

            var signal = GetCompareSignal(filt - prevFilt1, prevFilt1 - prevFilt2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ehwi", filtList },
            { "Roc", rocList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(filtList);
        stockData.IndicatorName = IndicatorName.EhlersHammingWindowIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Enhanced Signal To Noise Ratio
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersEnhancedSignalToNoiseRatio(this StockData stockData, int length = 6)
    {
        length = Math.Max(length, 1);
        List<double> q3List = new(stockData.Count);
        List<double> i3List = new(stockData.Count);
        List<double> noiseList = new(stockData.Count);
        List<double> snrList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        var ehlersMamaList = CalculateEhlersMotherOfAdaptiveMovingAverages(stockData);
        var smoothList = ehlersMamaList.OutputValues["Smooth"];
        var smoothPeriodList = ehlersMamaList.OutputValues["SmoothPeriod"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var smooth = smoothList[i];
            var prevSmooth2 = i >= 2 ? smoothList[i - 2] : 0;
            var smoothPeriod = smoothPeriodList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevSmooth = i >= 1 ? smoothList[i - 1] : 0;

            var q3 = 0.5 * (smooth - prevSmooth2) * ((0.1759 * smoothPeriod) + 0.4607);
            q3List.Add(q3);

            var sp = (int)Math.Ceiling(smoothPeriod / 2);
            double i3 = 0;
            for (var j = 0; j <= sp - 1; j++)
            {
                var prevQ3 = i >= j ? q3List[i - j] : 0;
                i3 += prevQ3;
            }
            i3 = sp != 0 ? 1.57 * i3 / sp : i3;
            i3List.Add(i3);

            var signalValue = (i3 * i3) + (q3 * q3);
            var prevNoise = GetLastOrDefault(noiseList);
            var noise = (0.1 * (currentHigh - currentLow) * (currentHigh - currentLow) * 0.25) + (0.9 * prevNoise);
            noiseList.Add(noise);

            var temp = noise != 0 ? signalValue / noise : 0;
            var prevSnr = GetLastOrDefault(snrList);
            var snr = (0.33 * (10 * Math.Log(temp) / Math.Log(10))) + (0.67 * prevSnr);
            snrList.Add(snr);

            var signal = GetVolatilitySignal(currentValue - smooth, prevValue - prevSmooth, snr, length);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Esnr", snrList },
            { "I3", i3List },
            { "Q3", q3List },
            { "SmoothPeriod", smoothPeriodList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(snrList);
        stockData.IndicatorName = IndicatorName.EhlersEnhancedSignalToNoiseRatio;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Hilbert Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersHilbertOscillator(this StockData stockData, int length = 7)
    {
        length = Math.Max(length, 1);
        List<double> iqList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var snrv2List = CalculateEhlersEnhancedSignalToNoiseRatio(stockData, length);
        var smoothPeriodList = snrv2List.OutputValues["SmoothPeriod"];
        var q3List = snrv2List.OutputValues["Q3"];
        var i3List = snrv2List.OutputValues["I3"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var smoothPeriod = smoothPeriodList[i];
            var i3 = i3List[i];
            var prevI3 = i >= 1 ? i3List[i - 1] : 0;
            var prevIq = i >= 1 ? iqList[i - 1] : 0;

            var maxCount = (int)Math.Ceiling(smoothPeriod / 4);
            double iq = 0;
            for (var j = 0; j <= maxCount - 1; j++)
            {
                var prevQ3 = i >= j ? q3List[i - j] : 0;
                iq += prevQ3;
            }
            iq = maxCount != 0 ? 1.25 * iq / maxCount : iq;
            iqList.Add(iq);

            var signal = GetCompareSignal(iq - i3, prevIq - prevI3);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "I3", i3List },
            { "IQ", iqList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.EhlersHilbertOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Fourier Series Analysis
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="bw"></param>
    /// <returns></returns>
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
        var g1 = Math.Cos(bw * 2 * Math.PI / length);
        var s1 = (1 / g1) - Sqrt((1 / (g1 * g1)) - 1);
        var l2 = Math.Cos(2 * Math.PI / ((double)length / 2));
        var g2 = Math.Cos(bw * 2 * Math.PI / ((double)length / 2));
        var s2 = (1 / g2) - Sqrt((1 / (g2 * g2)) - 1);
        var l3 = Math.Cos(2 * Math.PI / ((double)length / 3));
        var g3 = Math.Cos(bw * 2 * Math.PI / ((double)length / 3));
        var s3 = (1 / g3) - Sqrt((1 / (g3 * g3)) - 1);

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

            var q1 = i <= 4 ? 0 : length / 2 * Math.PI * (bp1 - prevBp1_1);
            q1List.Add(q1);

            var bp2 = i <= 3 ? 0 : (0.5 * (1 - s2) * (currentValue - prevValue)) + (l2 * (1 + s2) * prevBp2_1) - (s2 * prevBp2_2);
            bp2List.Add(bp2);

            var q2 = i <= 4 ? 0 : length / 2 * Math.PI * (bp2 - prevBp2_1);
            q2List.Add(q2);

            var bp3 = i <= 3 ? 0 : (0.5 * (1 - s3) * (currentValue - prevValue)) + (l3 * (1 + s3) * prevBp3_1) - (s3 * prevBp3_2);
            bp3List.Add(bp3);

            var q3 = i <= 4 ? 0 : length / 2 * Math.PI * (bp3 - prevBp3_1);
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

            var roc = length / Math.PI * 4 * (wave - prevWave2);
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
    public static StockData CalculateEhlersEvenBetterSineWaveIndicator(this StockData stockData, int length1 = 40, int length2 = 10)
    {
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        List<double> hpList = new(stockData.Count);
        List<double> filtList = new(stockData.Count);
        List<double> ebsiList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var piHp = MinOrMax(2 * Math.PI / length1, 0.99, 0.01);
        var a1 = (1 - Math.Sin(piHp)) / Math.Cos(piHp);
        var a2 = Exp(MinOrMax(-1.414 * Math.PI / length2, -0.01, -0.99));
        var b = 2 * a2 * Math.Cos(MinOrMax(1.414 * Math.PI / length2, 0.99, 0.01));
        var c2 = b;
        var c3 = -a2 * a2;
        var c1 = 1 - c2 - c3;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevFilt1 = i >= 1 ? filtList[i - 1] : 0;
            var prevFilt2 = i >= 2 ? filtList[i - 2] : 0;

            var prevHp = GetLastOrDefault(hpList);
            var hp = ((0.5 * (1 + a1)) * MinPastValues(i, 1, currentValue - prevValue)) + (a1 * prevHp);
            hpList.Add(hp);

            var filt = (c1 * ((hp + prevHp) / 2)) + (c2 * prevFilt1) + (c3 * prevFilt2);
            filtList.Add(filt);

            var wave = (filt + prevFilt1 + prevFilt2) / 3;
            var pwr = (Pow(filt, 2) + Pow(prevFilt1, 2) + Pow(prevFilt2, 2)) / 3;
            var prevEbsi = GetLastOrDefault(ebsiList);
            var ebsi = pwr > 0 ? wave / Sqrt(pwr) : 0;
            ebsiList.Add(ebsi);

            var signal = GetCompareSignal(ebsi, prevEbsi);
            signalsList?.Add(signal);
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
    public static StockData CalculateEhlersFisherTransform(this StockData stockData, int length = 10)
    {
        length = Math.Max(length, 1);
        List<double> fisherTransformList = new(stockData.Count);
        List<double> nValueList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var (maxList, minList) = GetMaxAndMinValuesList(inputList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var maxH = maxList[i];
            var minL = minList[i];
            var ratio = maxH - minL != 0 ? (currentValue - minL) / (maxH - minL) : 0;
            var prevFisherTransform1 = i >= 1 ? fisherTransformList[i - 1] : 0;
            var prevFisherTransform2 = i >= 2 ? fisherTransformList[i - 2] : 0;

            var prevNValue = GetLastOrDefault(nValueList);
            var nValue = MinOrMax((0.33 * 2 * (ratio - 0.5)) + (0.67 * prevNValue), 0.999, -0.999);
            nValueList.Add(nValue);

            var fisherTransform = (0.5 * Math.Log((1 + nValue) / (1 - nValue))) + (0.5 * prevFisherTransform1);
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
    public static StockData CalculateEhlersInverseFisherTransform(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage, 
        int length1 = 5, int length2 = 9)
    {
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        List<double> v1List = new(stockData.Count);
        List<double> inverseFisherTransformList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var rsiList = CalculateRelativeStrengthIndex(stockData, maType, length: length1).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentRsi = rsiList[i];

            var v1 = 0.1 * (currentRsi - 50);
            v1List.Add(v1);
        }

        var v2List = GetMovingAverageList(stockData, maType, length2, v1List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var v2 = v2List[i];
            var prevIft1 = i >= 1 ? inverseFisherTransformList[i - 1] : 0;
            var prevIft2 = i >= 2 ? inverseFisherTransformList[i - 2] : 0;
            var bottom = Exp(2 * v2) + 1;

            var inverseFisherTransform = bottom != 0 ? MinOrMax((Exp(2 * v2) - 1) / bottom, 1, -1) : 0;
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
    public static StockData CalculateEhlersInstantaneousTrendlineV2(this StockData stockData, double alpha = 0.07)
    {
        List<double> itList = new(stockData.Count);
        List<double> lagList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue1 = i >= 1 ? inputList[i - 1] : 0;
            var prevIt1 = i >= 1 ? itList[i - 1] : 0;
            var prevValue2 = i >= 2 ? inputList[i - 2] : 0;
            var prevIt2 = i >= 2 ? itList[i - 2] : 0;

            var it = i < 7 ? (currentValue + (2 * prevValue1) + prevValue2) / 4 : ((alpha - (Pow(alpha, 2) / 4)) * currentValue) + 
                (0.5 * Pow(alpha, 2) * prevValue1) - ((alpha - (0.75 * Pow(alpha, 2))) * prevValue2) + (2 * (1 - alpha) * prevIt1) - (Pow(1 - alpha, 2) * prevIt2);
            itList.Add(it);

            var prevLag = GetLastOrDefault(lagList);
            var lag = (2 * it) - prevIt2;
            lagList.Add(lag);

            var signal = GetCompareSignal(lag - it, prevLag - prevIt1);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Eit", itList },
            { "Signal", lagList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(itList);
        stockData.IndicatorName = IndicatorName.EhlersInstantaneousTrendlineV2;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Instantaneous Trendline V1
    /// </summary>
    /// <param name="stockData"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersInstantaneousTrendlineV1(this StockData stockData)
    {
        List<double> itList = new(stockData.Count);
        List<double> trendLineList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var spList = CalculateEhlersMotherOfAdaptiveMovingAverages(stockData).OutputValues["SmoothPeriod"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var sp = spList[i];
            var currentValue = inputList[i];
            var prevIt1 = i >= 1 ? itList[i - 1] : 0;
            var prevIt2 = i >= 2 ? itList[i - 2] : 0;
            var prevIt3 = i >= 3 ? itList[i - 3] : 0;
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            var dcPeriod = (int)Math.Ceiling(sp + 0.5);
            double iTrend = 0;
            for (var j = 0; j <= dcPeriod - 1; j++)
            {
                var prevValue = i >= j ? inputList[i - j] : 0;

                iTrend += prevValue;
            }
            iTrend = dcPeriod != 0 ? iTrend / dcPeriod : iTrend;
            itList.Add(iTrend);

            var prevTrendLine = GetLastOrDefault(trendLineList);
            var trendLine = ((4 * iTrend) + (3 * prevIt1) + (2 * prevIt2) + prevIt3) / 10;
            trendLineList.Add(trendLine);

            var signal = GetCompareSignal(currentValue - trendLine, prevVal - prevTrendLine);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Eit", itList },
            { "Signal", trendLineList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(itList);
        stockData.IndicatorName = IndicatorName.EhlersInstantaneousTrendlineV1;

        return stockData;
    }

}

