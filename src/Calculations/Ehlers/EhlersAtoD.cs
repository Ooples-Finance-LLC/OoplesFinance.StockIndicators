
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
    public static StockData CalculateEhlersAdaptiveCyberCycle(this StockData stockData, int length = 5, double alpha = 0.07)
    {
        length = Math.Max(length, 1);
        List<double> ipList = new(stockData.Count);
        List<double> q1List = new(stockData.Count);
        List<double> i1List = new(stockData.Count);
        List<double> dpList = new(stockData.Count);
        List<double> pList = new(stockData.Count);
        List<double> acList = new(stockData.Count);
        using var dpMedian = new RollingMedian(length);
        List<double> cycleList = new(stockData.Count);
        List<double> smoothList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevCycle = i >= 1 ? cycleList[i - 1] : 0;
            var prevSmooth = i >= 1 ? smoothList[i - 1] : 0;
            var prevIp = i >= 1 ? ipList[i - 1] : 0;
            var prevAc1 = i >= 1 ? acList[i - 1] : 0;
            var prevI1 = i >= 1 ? i1List[i - 1] : 0;
            var prevQ1 = i >= 1 ? q1List[i - 1] : 0;
            var prevP = i >= 1 ? pList[i - 1] : 0;
            var prevValue2 = i >= 2 ? inputList[i - 2] : 0;
            var prevSmooth2 = i >= 2 ? smoothList[i - 2] : 0;
            var prevCycle2 = i >= 2 ? cycleList[i - 2] : 0;
            var prevAc2 = i >= 2 ? acList[i - 2] : 0;
            var prevValue3 = i >= 3 ? inputList[i - 3] : 0;
            var prevCycle3 = i >= 3 ? cycleList[i - 3] : 0;
            var prevCycle4 = i >= 4 ? cycleList[i - 4] : 0;
            var prevCycle6 = i >= 6 ? cycleList[i - 6] : 0;

            var smooth = (currentValue + (2 * prevValue) + (2 * prevValue2) + prevValue3) / 6;
            smoothList.Add(smooth);

            var cycle = i < 7 ? (currentValue - (2 * prevValue) + prevValue2) / 4 : (Pow(1 - (0.5 * alpha), 2) * (smooth - (2 * prevSmooth) + prevSmooth2)) +
            (2 * (1 - alpha) * prevCycle) - (Pow(1 - alpha, 2) * prevCycle2);
            cycleList.Add(cycle);

            var q1 = ((0.0962 * cycle) + (0.5769 * prevCycle2) - (0.5769 * prevCycle4) - (0.0962 * prevCycle6)) * (0.5 + (0.08 * prevIp));
            q1List.Add(q1);

            var i1 = prevCycle3;
            i1List.Add(i1);

            var dp = MinOrMax(q1 != 0 && prevQ1 != 0 ? ((i1 / q1) - (prevI1 / prevQ1)) / (1 + (i1 * prevI1 / (q1 * prevQ1))) : 0, 1.1, 0.1);
            dpList.Add(dp);
            dpMedian.Add(dp);

            var medianDelta = dpMedian.Median;
            var dc = medianDelta != 0 ? (6.28318 / medianDelta) + 0.5 : 15;

            var ip = (0.33 * dc) + (0.67 * prevIp);
            ipList.Add(ip);

            var p = (0.15 * ip) + (0.85 * prevP);
            pList.Add(p);

            var a1 = 2 / (p + 1);
            var ac = i < 7 ? (currentValue - (2 * prevValue) + prevValue2) / 4 :
                (Pow(1 - (0.5 * a1), 2) * (smooth - (2 * prevSmooth) + prevSmooth2)) + (2 * (1 - a1) * prevAc1) - (Pow(1 - a1, 2) * prevAc2);
            acList.Add(ac);

            var signal = GetCompareSignal(ac - prevAc1, prevAc1 - prevAc2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Eacc", acList },
            { "Period", pList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(acList);
        stockData.IndicatorName = IndicatorName.EhlersAdaptiveCyberCycle;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Correlation Trend Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersCorrelationTrendIndicator(this StockData stockData, int length = 20)
    {
        length = Math.Max(length, 1);
        List<double> corrList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevCorr1 = i >= 1 ? corrList[i - 1] : 0;
            var prevCorr2 = i >= 2 ? corrList[i - 2] : 0;

            double sx = 0, sy = 0, sxx = 0, sxy = 0, syy = 0;
            for (var j = 0; j <= length - 1; j++)
            {
                var x = i >= j ? inputList[i - j] : 0;
                double y = -j;

                sx += x;
                sy += y;
                sxx += Pow(x, 2);
                sxy += x * y;
                syy += Pow(y, 2);
            }

            var corr = (length * sxx) - (sx * sx) > 0 && (length * syy) - (sy * sy) > 0 ? ((length * sxy) - (sx * sy)) /
                Sqrt(((length * sxx) - (sx * sx)) * ((length * syy) - (sy * sy))) : 0;
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
    public static StockData CalculateEhlersCenterofGravityOscillator(this StockData stockData, int length = 10)
    {
        length = Math.Max(length, 1);
        List<double> cgList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            double num = 0, denom = 0;
            for (var j = 0; j <= length - 1; j++)
            {
                var prevValue = i >= j ? inputList[i - j] : 0;
                num += (1 + j) * prevValue;
                denom += prevValue;
            }

            var prevCg = GetLastOrDefault(cgList);
            var cg = denom != 0 ? (-num / denom) + ((double)(length + 1) / 2) : 0;
            cgList.Add(cg);

            var signal = GetCompareSignal(cg, prevCg);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ecog", cgList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(cgList);
        stockData.IndicatorName = IndicatorName.EhlersCenterofGravityOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Adaptive Center Of Gravity Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersAdaptiveCenterOfGravityOscillator(this StockData stockData, int length = 5)
    {
        length = Math.Max(length, 1);
        List<double> cgList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var pList = CalculateEhlersAdaptiveCyberCycle(stockData, length: length).OutputValues["Period"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var p = pList[i];
            var intPeriod = (int)Math.Ceiling(p / 2);
            var prevCg1 = i >= 1 ? cgList[i - 1] : 0;
            var prevCg2 = i >= 2 ? cgList[i - 2] : 0;

            double num = 0, denom = 0;
            for (var j = 0; j <= intPeriod - 1; j++)
            {
                var prevPrice = i >= j ? inputList[i - j] : 0;
                num += (1 + j) * prevPrice;
                denom += prevPrice;
            }

            var cg = denom != 0 ? (-num / denom) + ((intPeriod + 1) / 2) : 0;
            cgList.Add(cg);

            var signal = GetCompareSignal(cg - prevCg1, prevCg1 - prevCg2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Eacog", cgList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(cgList);
        stockData.IndicatorName = IndicatorName.EhlersAdaptiveCenterOfGravityOscillator;

        return stockData;
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
    public static StockData CalculateEhlersDecyclerOscillatorV1(this StockData stockData, int fastLength = 100, int slowLength = 125, 
        double fastMult = 1.2, double slowMult = 1)
    {
        fastLength = Math.Max(fastLength, 1);
        slowLength = Math.Max(slowLength, 1);
        List<double> decycler1OscillatorList = new(stockData.Count);
        List<double> decycler2OscillatorList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var decycler1List = CalculateEhlersSimpleDecycler(stockData, fastLength).CustomValuesList;
        var decycler2List = CalculateEhlersSimpleDecycler(stockData, slowLength).CustomValuesList;
        stockData.SetCustomValues(decycler1List);
        var decycler1FilteredList = CalculateEhlersHighPassFilterV1(stockData, fastLength, 0.5).CustomValuesList;
        stockData.SetCustomValues(decycler2List);
        var decycler2FilteredList = CalculateEhlersHighPassFilterV1(stockData, slowLength, 0.5).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var decycler1Filtered = decycler1FilteredList[i];
            var decycler2Filtered = decycler2FilteredList[i];

            var prevDecyclerOsc1 = GetLastOrDefault(decycler1OscillatorList);
            var decyclerOscillator1 = currentValue != 0 ? 100 * fastMult * decycler1Filtered / currentValue : 0;
            decycler1OscillatorList.Add(decyclerOscillator1);

            var prevDecyclerOsc2 = GetLastOrDefault(decycler2OscillatorList);
            var decyclerOscillator2 = currentValue != 0 ? 100 * slowMult * decycler2Filtered / currentValue : 0;
            decycler2OscillatorList.Add(decyclerOscillator2);

            var signal = GetCompareSignal(decyclerOscillator2 - decyclerOscillator1, prevDecyclerOsc2 - prevDecyclerOsc1);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "FastEdo", decycler1OscillatorList },
            { "SlowEdo", decycler2OscillatorList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
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
    public static StockData CalculateEhlersDecyclerOscillatorV2(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int fastLength = 10, int slowLength = 20)
    {
        fastLength = Math.Max(fastLength, 1);
        slowLength = Math.Max(slowLength, 1);
        List<double> decList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var hp1List = CalculateEhlersHighPassFilterV2(stockData, maType, fastLength).CustomValuesList;
        var hp2List = CalculateEhlersHighPassFilterV2(stockData, maType, slowLength).CustomValuesList;

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
    public static StockData CalculateEhlersDecycler(this StockData stockData, int length = 60)
    {
        length = Math.Max(length, 1);
        List<double> decList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var alphaArg = Math.Min(2 * Math.PI / length, 0.99);
        var alphaCos = Math.Cos(alphaArg);
        var alpha1 = alphaCos != 0 ? (alphaCos + Math.Sin(alphaArg) - 1) / alphaCos : 0;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue1 = i >= 1 ? inputList[i - 1] : 0;

            var prevDec = GetLastOrDefault(decList);
            var dec = (alpha1 / 2 * (currentValue + prevValue1)) + ((1 - alpha1) * prevDec);
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
    public static StockData CalculateEhlersDominantCycleTunedBypassFilter(this StockData stockData, int minLength = 8, int maxLength = 50, 
        int length1 = 40, int length2 = 10)
    {
        minLength = Math.Max(minLength, 1);
        maxLength = Math.Max(maxLength, minLength);
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        List<double> v1List = new(stockData.Count);
        List<double> v2List = new(stockData.Count);
        List<double> hpList = new(stockData.Count);
        List<double> smoothHpList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var twoPiPer = MinOrMax(2 * Math.PI / length1, 0.99, 0.01);
        var alpha1 = (1 - Math.Sin(twoPiPer)) / Math.Cos(twoPiPer);

        var domCycList = CalculateEhlersSpectrumDerivedFilterBank(stockData, minLength, maxLength, length1, length2).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var domCyc = domCycList[i];
            var beta = Math.Cos(MinOrMax(2 * Math.PI / domCyc, 0.99, 0.01));
            var delta = Math.Max((-0.015 * i) + 0.5, 0.15);
            var gamma = 1 / Math.Cos(MinOrMax(4 * Math.PI * (delta / domCyc), 0.99, 0.01));
            var alpha = gamma - Sqrt((gamma * gamma) - 1);
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevHp1 = i >= 1 ? hpList[i - 1] : 0;
            var prevHp2 = i >= 2 ? hpList[i - 2] : 0;
            var prevHp3 = i >= 3 ? hpList[i - 3] : 0;
            var prevHp4 = i >= 4 ? hpList[i - 4] : 0;
            var prevHp5 = i >= 5 ? hpList[i - 5] : 0;

            var hp = i < 7 ? currentValue : (0.5 * (1 + alpha1) * (currentValue - prevValue)) + (alpha1 * prevHp1);
            hpList.Add(hp);

            var prevSmoothHp = GetLastOrDefault(smoothHpList);
            var smoothHp = i < 7 ? currentValue - prevValue : (hp + (2 * prevHp1) + (3 * prevHp2) + (3 * prevHp3) + (2 * prevHp4) + prevHp5) / 12;
            smoothHpList.Add(smoothHp);

            var prevV1 = i >= 1 ? v1List[i - 1] : 0;
            var prevV1_2 = i >= 2 ? v1List[i - 2] : 0;
            var v1 = (0.5 * (1 - alpha) * (smoothHp - prevSmoothHp)) + (beta * (1 + alpha) * prevV1) - (alpha * prevV1_2);
            v1List.Add(v1);

            var v2 = domCyc / Math.PI * 2 * (v1 - prevV1);
            v2List.Add(v2);

            var signal = GetConditionSignal(v2 > v1 && v2 >= 0, v2 < v1 || v2 < 0);
            signalsList?.Add(signal);
        }

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
    public static StockData CalculateEhlersCorrelationCycleIndicator(this StockData stockData, int length = 20)
    {
        length = Math.Max(length, 1);
        List<double> realList = new(stockData.Count);
        List<double> imagList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            double sx = 0, sy = 0, nsy = 0, sxx = 0, syy = 0, nsyy = 0, sxy = 0, nsxy = 0;
            for (var j = 1; j <= length; j++)
            {
                var x = i >= j - 1 ? inputList[i - (j - 1)] : 0;
                var v = MinOrMax(2 * Math.PI * ((double)(j - 1) / length), 0.99, 0.01);
                var y = Math.Cos(v);
                var ny = -Math.Sin(v);
                sx += x;
                sy += y;
                nsy += ny;
                sxx += Pow(x, 2);
                syy += Pow(y, 2);
                nsyy += ny * ny;
                sxy += x * y;
                nsxy += x * ny;
            }

            var prevReal = GetLastOrDefault(realList);
            var real = (length * sxx) - (sx * sx) > 0 && (length * syy) - (sy * sy) > 0 ? ((length * sxy) - (sx * sy)) /
                   Sqrt(((length * sxx) - (sx * sx)) * ((length * syy) - (sy * sy))) : 0;
            realList.Add(real);

            var prevImag = GetLastOrDefault(imagList);
            var imag = (length * sxx) - (sx * sx) > 0 && (length * nsyy) - (nsy * nsy) > 0 ? ((length * nsxy) - (sx * nsy)) /
                   Sqrt(((length * sxx) - (sx * sx)) * ((length * nsyy) - (nsy * nsy))) : 0;
            imagList.Add(imag);

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
    public static StockData CalculateEhlersCorrelationAngleIndicator(this StockData stockData, int length = 20)
    {
        length = Math.Max(length, 1);
        List<double> angleList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var ecciList = CalculateEhlersCorrelationCycleIndicator(stockData, length);
        var realList = ecciList.OutputValues["Real"];
        var imagList = ecciList.OutputValues["Imag"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var real = realList[i];
            var imag = imagList[i];

            var prevAngle = i >= 1 ? angleList[i - 1] : 0;
            var angle = imag != 0 ? 90 + Math.Atan(real / imag).ToDegrees() : 90;
            angle = imag > 0 ? angle - 180 : angle;
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
    public static StockData CalculateEhlersAnticipateIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.EhlersHannMovingAverage,
        int length = 14, double bw = 1)
    {
        length = Math.Max(length, 1);
        List<double> predictList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var hFiltList = CalculateEhlersImpulseResponse(stockData, maType, length, bw).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            double maxCorr = -1, start = 0;
            for (var j = 0; j < length; j++)
            {
                double sx = 0, sy = 0, sxx = 0, syy = 0, sxy = 0;
                for (var k = 0; k < length; k++)
                {
                    var x = i >= k ? hFiltList[i - k] : 0;
                    var y = -Math.Sin(MinOrMax(2 * Math.PI * ((double)(j + k) / length), 0.99, 0.01));
                    sx += x;
                    sy += y;
                    sxx += Pow(x, 2);
                    sxy += x * y;
                    syy += Pow(y, 2);
                }
                var corr = ((length * sxx) - Pow(sx, 2)) * ((length * syy) - Pow(sy, 2)) > 0 ? ((length * sxy) - (sx * sy)) /
                    Sqrt(((length * sxx) - Pow(sx, 2)) * ((length * syy) - Pow(sy, 2))) : 0;
                if (corr > maxCorr)
                {
                    maxCorr = corr;
                    start = length - j;
                }
            }

            var prevPredict = GetLastOrDefault(predictList);
            var predict = Math.Sin(MinOrMax(2 * Math.PI * start / length, 0.99, 0.01));
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
    public static StockData CalculateEhlersAutoCorrelationIndicator(this StockData stockData, int length1 = 48, int length2 = 10)
    {
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        List<double> corrList = new(stockData.Count);
        List<double> xList = new(stockData.Count);
        List<double> yList = new(stockData.Count);
        List<double> xxList = new(stockData.Count);
        List<double> yyList = new(stockData.Count);
        List<double> xyList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum xSum = new();
        RollingSum ySum = new();
        RollingSum xxSum = new();
        RollingSum yySum = new();
        RollingSum xySum = new();

        var roofingFilterList = CalculateEhlersRoofingFilterV2(stockData, length1, length2).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevCorr1 = i >= 1 ? corrList[i - 1] : 0;
            var prevCorr2 = i >= 2 ? corrList[i - 2] : 0;

            var x = roofingFilterList[i];
            xList.Add(x);
            xSum.Add(x);

            var y = i >= length1 ? roofingFilterList[i - length1] : 0;
            yList.Add(y);
            ySum.Add(y);

            var xx = Pow(x, 2);
            xxList.Add(xx);
            xxSum.Add(xx);

            var yy = Pow(y, 2);
            yyList.Add(yy);
            yySum.Add(yy);

            var xy = x * y;
            xyList.Add(xy);
            xySum.Add(xy);

            var sx = xSum.Sum(length1);
            var sy = ySum.Sum(length1);
            var sxx = xxSum.Sum(length1);
            var syy = yySum.Sum(length1);
            var sxy = xySum.Sum(length1);
            var count = Math.Min(i + 1, length1);

            var corr = ((count * sxx) - (sx * sx)) * ((count * syy) - (sy * sy)) > 0 ? 0.5 * ((((count * sxy) - (sx * sy)) / 
                Sqrt(((count * sxx) - (sx * sx)) * ((count * syy) - (sy * sy)))) + 1) : 0;
            corrList.Add(corr);

            var signal = GetCompareSignal(corr - prevCorr1, prevCorr1 - prevCorr2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Eaci", corrList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(corrList);
        stockData.IndicatorName = IndicatorName.EhlersAutoCorrelationIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Auto Correlation Periodogram
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersAutoCorrelationPeriodogram(this StockData stockData, int length1 = 48, int length2 = 10, int length3 = 3)
    {
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        length3 = Math.Max(length3, 0);
        List<double> domCycList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var rArray = new double[length1 + 1];

        var corrList = CalculateEhlersAutoCorrelationIndicator(stockData, length1, length2).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var corr = corrList[i];
            var prevCorr1 = i >= 1 ? corrList[i - 1] : 0;
            var prevCorr2 = i >= 2 ? corrList[i - 2] : 0;

            double maxPwr = 0;
            for (var j = length2; j <= length1; j++)
            {
                double cosPart = 0, sinPart = 0;
                for (var k = length3; k <= length1; k++)
                {
                    var prevCorr = i >= k ? corrList[i - k] : 0;
                    cosPart += prevCorr * Math.Cos(2 * Math.PI * ((double)k / j));
                    sinPart += prevCorr * Math.Sin(2 * Math.PI * ((double)k / j));
                }

                var sqSum = Pow(cosPart, 2) + Pow(sinPart, 2);
                var r = (0.2 * Pow(sqSum, 2)) + (0.8 * rArray[j]);
                rArray[j] = r;
                maxPwr = Math.Max(r, maxPwr);
            }

            double spx = 0, sp = 0;
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

            var signal = GetCompareSignal(corr - prevCorr1, prevCorr1 - prevCorr2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Eacp", domCycList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(domCycList);
        stockData.IndicatorName = IndicatorName.EhlersAutoCorrelationPeriodogram;

        return stockData;
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
    public static StockData CalculateEhlersAdaptiveRelativeStrengthIndexV2(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 48, int length2 = 10, int length3 = 3)
    {
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        length3 = Math.Max(length3, 1);
        List<double> upChgList = new(stockData.Count);
        List<double> denomList = new(stockData.Count);
        List<double> arsiList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var a1 = Exp(-1.414 * Math.PI / length2);
        var b1 = 2 * a1 * Math.Cos(Math.Min(1.414 * Math.PI / length2, 0.99));
        var c2 = b1;
        var c3 = -a1 * a1;
        var c1 = 1 - c2 - c3;

        var domCycList = CalculateEhlersAutoCorrelationPeriodogram(stockData, length1, length2, length3).CustomValuesList;
        var roofingFilterList = CalculateEhlersRoofingFilterV2(stockData, length1, length2).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var domCyc = MinOrMax(domCycList[i], length1, length2);
            var prevArsi1 = i >= 1 ? arsiList[i - 1] : 0;
            var prevArsi2 = i >= 2 ? arsiList[i - 2] : 0;

            var prevUpChg = GetLastOrDefault(upChgList);
            double upChg = 0, dnChg = 0;
            for (var j = 0; j < (int)Math.Ceiling(domCyc / 2); j++)
            {
                var filt = i >= j ? roofingFilterList[i - j] : 0;
                var prevFilt = i >= j + 1 ? roofingFilterList[i - (j + 1)] : 0;
                upChg += filt > prevFilt ? filt - prevFilt : 0;
                dnChg += filt < prevFilt ? prevFilt - filt : 0;
            }
            upChgList.Add(upChg);

            var prevDenom = GetLastOrDefault(denomList);
            var denom = upChg + dnChg;
            denomList.Add(denom);

            var arsi = denom != 0 && prevDenom != 0 ? (c1 * ((upChg / denom) + (prevUpChg / prevDenom)) / 2) + (c2 * prevArsi1) + (c3 * prevArsi2) : 0;
            arsiList.Add(arsi);
        }

        var arsiEmaList = GetMovingAverageList(stockData, maType, length2, arsiList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var arsi = arsiList[i];
            var arsiEma = arsiEmaList[i];
            var prevArsi = i >= 1 ? arsiList[i - 1] : 0;
            var prevArsiEma = i >= 1 ? arsiEmaList[i - 1] : 0;

            var signal = GetRsiSignal(arsi - arsiEma, prevArsi - prevArsiEma, arsi, prevArsi, 0.7, 0.3);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Earsi", arsiList },
            { "Signal", arsiEmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(arsiList);
        stockData.IndicatorName = IndicatorName.EhlersAdaptiveRelativeStrengthIndexV2;

        return stockData;
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
    public static StockData CalculateEhlersAdaptiveRsiFisherTransformV2(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 48, int length2 = 10, int length3 = 3)
    {
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        length3 = Math.Max(length3, 1);
        List<double> fishList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var arsiList = CalculateEhlersAdaptiveRelativeStrengthIndexV2(stockData, maType, length1, length2, length3).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var arsi = arsiList[i] / 100;
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
    public static StockData CalculateEhlersAdaptiveStochasticIndicatorV2(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 48, int length2 = 10, int length3 = 3)
    {
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        length3 = Math.Max(length3, 1);
        List<double> stocList = new(stockData.Count);
        List<double> astocList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var a1 = Exp(-1.414 * Math.PI / length2);
        var b1 = 2 * a1 * Math.Cos(Math.Min(1.414 * Math.PI / length2, 0.99));
        var c2 = b1;
        var c3 = -a1 * a1;
        var c1 = 1 - c2 - c3;

        var domCycList = CalculateEhlersAutoCorrelationPeriodogram(stockData, length1, length2, length3).CustomValuesList;
        var roofingFilterList = CalculateEhlersRoofingFilterV2(stockData, length1, length2).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var domCyc = MinOrMax(domCycList[i], length1, length2);
            var roofingFilter = roofingFilterList[i];
            var prevAstoc1 = i >= 1 ? astocList[i - 1] : 0;
            var prevAstoc2 = i >= 2 ? astocList[i - 2] : 0;

            double highest = 0, lowest = 0;
            for (var j = 0; j < (int)Math.Ceiling(domCyc); j++)
            {
                var filt = i >= j ? roofingFilterList[i - j] : 0;
                highest = filt > highest ? filt : highest;
                lowest = filt < lowest ? filt : lowest;
            }

            var prevStoc = GetLastOrDefault(stocList);
            var stoc = highest != lowest ? (roofingFilter - lowest) / (highest - lowest) : 0;
            stocList.Add(stoc);

            var astoc = (c1 * ((stoc + prevStoc) / 2)) + (c2 * prevAstoc1) + (c3 * prevAstoc2);
            astocList.Add(astoc);
        }

        var astocEmaList = GetMovingAverageList(stockData, maType, length2, astocList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var astoc = astocList[i];
            var astocEma = astocEmaList[i];
            var prevAstoc = i >= 1 ? astocList[i - 1] : 0;
            var prevAstocEma = i >= 1 ? astocEmaList[i - 1] : 0;

            var signal = GetRsiSignal(astoc - astocEma, prevAstoc - prevAstocEma, astoc, prevAstoc, 0.7, 0.3);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Easi", astocList },
            { "Signal", astocEmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(astocList);
        stockData.IndicatorName = IndicatorName.EhlersAdaptiveStochasticIndicatorV2;

        return stockData;
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
    public static StockData CalculateEhlersAdaptiveStochasticInverseFisherTransform(this StockData stockData, 
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 48, int length2 = 10, int length3 = 3)
    {
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        length3 = Math.Max(length3, 1);
        List<double> fishList = new(stockData.Count);
        List<double> triggerList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var astocList = CalculateEhlersAdaptiveStochasticIndicatorV2(stockData, maType, length1, length2, length3).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var astoc = astocList[i];
            var v1 = 2 * (astoc - 0.5);

            var prevFish = GetLastOrDefault(fishList);
            var fish = (Exp(6 * v1) - 1) / (Exp(6 * v1) + 1);
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
    public static StockData CalculateEhlersAdaptiveCommodityChannelIndexV2(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length1 = 48, int length2 = 10, int length3 = 3)
    {
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        length3 = Math.Max(length3, 1);
        List<double> acciList = new(stockData.Count);
        List<double> tempList = new(stockData.Count);
        List<double> mdList = new(stockData.Count);
        List<double> ratioList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum tempSum = new();
        RollingSum mdSum = new();

        var a1 = Exp(-1.414 * Math.PI / length2);
        var b1 = 2 * a1 * Math.Cos(Math.Min(1.414 * Math.PI / length2, 0.99));
        var c2 = b1;
        var c3 = -a1 * a1;
        var c1 = 1 - c2 - c3;

        var domCycList = CalculateEhlersAutoCorrelationPeriodogram(stockData, length1, length2, length3).CustomValuesList;
        var roofingFilterList = CalculateEhlersRoofingFilterV2(stockData, length1, length2).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var domCyc = MinOrMax(domCycList[i], length1, length2);
            var prevAcci1 = i >= 1 ? acciList[i - 1] : 0;
            var prevAcci2 = i >= 2 ? acciList[i - 2] : 0;
            var cycLength = (int)Math.Ceiling(domCyc);

            var roofingFilter = roofingFilterList[i];
            tempList.Add(roofingFilter);
            tempSum.Add(roofingFilter);

            var avg = tempSum.Average(cycLength);
            var md = Pow(roofingFilter - avg, 2);
            mdList.Add(md);
            mdSum.Add(md);

            var mdAvg = mdSum.Average(cycLength);
            var rms = cycLength >= 0 ? Sqrt(mdAvg) : 0;
            var num = roofingFilter - avg;
            var denom = 0.015 * rms;

            var prevRatio = GetLastOrDefault(ratioList);
            var ratio = denom != 0 ? num / denom : 0;
            ratioList.Add(ratio);

            var acci = (c1 * ((ratio + prevRatio) / 2)) + (c2 * prevAcci1) + (c3 * prevAcci2);
            acciList.Add(acci);
        }

        var acciEmaList = GetMovingAverageList(stockData, maType, length2, acciList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var acci = acciList[i];
            var acciEma = acciEmaList[i];
            var prevAcci = i >= 1 ? acciList[i - 1] : 0;
            var prevAcciEma = i >= 1 ? acciEmaList[i - 1] : 0;

            var signal = GetRsiSignal(acci - acciEma, prevAcci - prevAcciEma, acci, prevAcci, 100, -100);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Eacci", acciList },
            { "Signal", acciEmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(acciList);
        stockData.IndicatorName = IndicatorName.EhlersAdaptiveCommodityChannelIndexV2;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Discrete Fourier Transform Spectral Estimate
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersDiscreteFourierTransformSpectralEstimate(this StockData stockData, int length1 = 48, int length2 = 10)
    {
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        List<double> domCycList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var rArray = new double[length1 + 1];

        var roofingFilterList = CalculateEhlersRoofingFilterV2(stockData, length1, length2).CustomValuesList;

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
                    cosPart += prevFilt * Math.Cos(2 * Math.PI * ((double)k / j));
                    sinPart += prevFilt * Math.Sin(2 * Math.PI * ((double)k / j));
                }

                var sqSum = Pow(cosPart, 2) + Pow(sinPart, 2);
                var prevR = rArray[j];
                var r = (0.2 * Pow(sqSum, 2)) + (0.8 * prevR);
                rArray[j] = r;
                maxPwr = Math.Max(r, maxPwr);
                var pwr = maxPwr != 0 ? r / maxPwr : 0;

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
    public static StockData CalculateEhlersCombFilterSpectralEstimate(this StockData stockData, int length1 = 48, int length2 = 10, double bw = 0.3)
    {
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        List<double> domCycList = new(stockData.Count);
        List<double> bpList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var roofingFilterList = CalculateEhlersRoofingFilterV2(stockData, length1, length2).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var roofingFilter = roofingFilterList[i];
            var prevRoofingFilter1 = i >= 1 ? roofingFilterList[i - 1] : 0;
            var prevRoofingFilter2 = i >= 2 ? roofingFilterList[i - 2] : 0;
            var prevBp1 = i >= 1 ? bpList[i - 1] : 0;
            var prevBp2 = i >= 2 ? bpList[i - 2] : 0;

            double bp = 0, maxPwr = 0, spx = 0, sp = 0;
            for (var j = length2; j <= length1; j++)
            {
                var beta = Math.Cos(2 * Math.PI / j);
                var gamma = 1 / Math.Cos(2 * Math.PI * bw / j);
                var alpha = MinOrMax(gamma - Sqrt((gamma * gamma) - 1), 0.99, 0.01);
                bp = (0.5 * (1 - alpha) * (roofingFilter - prevRoofingFilter2)) + (beta * (1 + alpha) * prevBp1) - (alpha * prevBp2);

                double pwr = 0;
                for (var k = 1; k <= j; k++)
                {
                    var prevBp = i >= k ? bpList[i - k] : 0;
                    pwr += prevBp / j >= 0 ? Pow(prevBp / j, 2) : 0;
                }

                maxPwr = Math.Max(pwr, maxPwr);
                pwr = maxPwr != 0 ? pwr / maxPwr : 0;

                if (pwr >= 0.5)
                {
                    spx += j * pwr;
                    sp += pwr;
                }
            }
            bpList.Add(bp);

            var domCyc = sp != 0 ? spx / sp : 0;
            domCycList.Add(domCyc);

            var signal = GetCompareSignal(roofingFilter - prevRoofingFilter1, prevRoofingFilter1 - prevRoofingFilter2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ecfse", domCycList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(domCycList);
        stockData.IndicatorName = IndicatorName.EhlersCombFilterSpectralEstimate;

        return stockData;
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
    public static StockData CalculateEhlersAutoCorrelationReversals(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 48, int length2 = 10, int length3 = 3)
    {
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        length3 = Math.Max(length3, 0);
        List<double> reversalList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var corrList = CalculateEhlersAutoCorrelationIndicator(stockData, length1, length2).CustomValuesList;
        var emaList = GetMovingAverageList(stockData, maType, length2, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var ema = emaList[i];
            var currentValue = inputList[i];

            double delta = 0;
            for (var j = length3; j <= length1; j++)
            {
                var corr = i >= j ? corrList[i - j] : 0;
                var prevCorr = i >= j - 1 ? corrList[i - (j - 1)] : 0;
                delta += (corr > 0.5 && prevCorr < 0.5) || (corr < 0.5 && prevCorr > 0.5) ? 1 : 0;
            }

            double reversal = delta > (double)length1 / 2 ? 1 : 0;
            reversalList.Add(reversal);

            var signal = GetConditionSignal(currentValue < ema && reversal == 1, currentValue > ema && reversal == 1);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Eacr", reversalList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(reversalList);
        stockData.IndicatorName = IndicatorName.EhlersAutoCorrelationReversals;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Classic Hilbert Transformer
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersClassicHilbertTransformer(this StockData stockData, int length1 = 48, int length2 = 10)
    {
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        List<double> peakList = new(stockData.Count);
        List<double> realList = new(stockData.Count);
        List<double> imagList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var roofingFilterList = CalculateEhlersRoofingFilterV2(stockData, length1, length2).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var roofingFilter = roofingFilterList[i];
            var prevReal1 = i >= 1 ? realList[i - 1] : 0;
            var prevReal2 = i >= 2 ? realList[i - 2] : 0;
            var prevReal4 = i >= 4 ? realList[i - 4] : 0;
            var prevReal6 = i >= 6 ? realList[i - 6] : 0;
            var prevReal8 = i >= 8 ? realList[i - 8] : 0;
            var prevReal10 = i >= 10 ? realList[i - 10] : 0;
            var prevReal12 = i >= 12 ? realList[i - 12] : 0;
            var prevReal14 = i >= 14 ? realList[i - 14] : 0;
            var prevReal16 = i >= 16 ? realList[i - 16] : 0;
            var prevReal18 = i >= 18 ? realList[i - 18] : 0;
            var prevReal20 = i >= 20 ? realList[i - 20] : 0;
            var prevReal22 = i >= 22 ? realList[i - 22] : 0;

            var prevPeak = GetLastOrDefault(peakList);
            var peak = Math.Max(0.991 * prevPeak, Math.Abs(roofingFilter));
            peakList.Add(peak);

            var real = peak != 0 ? roofingFilter / peak : 0;
            realList.Add(real);

            var imag = ((0.091 * real) + (0.111 * prevReal2) + (0.143 * prevReal4) + (0.2 * prevReal6) + (0.333 * prevReal8) + prevReal10 -
                        prevReal12 - (0.333 * prevReal14) - (0.2 * prevReal16) - (0.143 * prevReal18) - (0.111 * prevReal20) - (0.091 * prevReal22)) / 1.865;
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
        stockData.IndicatorName = IndicatorName.EhlersClassicHilbertTransformer;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Dual Differentiator Dominant Cycle
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersDualDifferentiatorDominantCycle(this StockData stockData, int length1 = 48, int length2 = 20, int length3 = 8)
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
            var prevReal2 = i >= 2 ? realList[i - 2] : 0;
            var prevImag1 = i >= 1 ? imagList[i - 1] : 0;
            var iDot = real - prevReal1;
            var prevDomCyc1 = i >= 1 ? domCycList[i - 1] : 0;
            var prevDomCyc2 = i >= 2 ? domCycList[i - 2] : 0;
            var qDot = imag - prevImag1;

            var prevPeriod = GetLastOrDefault(periodList);
            var period = (real * qDot) - (imag * iDot) != 0 ? 2 * Math.PI * ((real * real) + (imag * imag)) / ((-real * qDot) + (imag * iDot)) : 0;
            period = MinOrMax(period, length1, length3);
            periodList.Add(period);

            var domCyc = (c1 * ((period + prevPeriod) / 2)) + (c2 * prevDomCyc1) + (c3 * prevDomCyc2);
            domCycList.Add(domCyc);

            var signal = GetCompareSignal(real - prevReal1, prevReal1 - prevReal2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Edddc", domCycList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(domCycList);
        stockData.IndicatorName = IndicatorName.EhlersDualDifferentiatorDominantCycle;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Alternate Signal To Noise Ratio
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersAlternateSignalToNoiseRatio(this StockData stockData, int length = 6)
    {
        length = Math.Max(length, 1);
        List<double> snrList = new(stockData.Count);
        List<double> rangeList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        var ehlersMamaList = CalculateEhlersMotherOfAdaptiveMovingAverages(stockData);
        var reList = ehlersMamaList.OutputValues["Real"];
        var imList = ehlersMamaList.OutputValues["Imag"];
        var mamaList = ehlersMamaList.CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var mama = mamaList[i];
            var re = reList[i];
            var im = imList[i];
            var currentValue = inputList[i];
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevMama = i >= 1 ? mamaList[i - 1] : 0;

            var prevRange = GetLastOrDefault(rangeList);
            var range = (0.1 * (currentHigh - currentLow)) + (0.9 * prevRange);
            rangeList.Add(range);

            var temp = range != 0 ? (re + im) / (range * range) : 0;
            var prevSnr = GetLastOrDefault(snrList);
            var snr = (0.25 * ((10 * Math.Log(temp) / Math.Log(10)) + length)) + (0.75 * prevSnr);
            snrList.Add(snr);

            var signal = GetVolatilitySignal(currentValue - mama, prevValue - prevMama, snr, length);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Esnr", snrList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(snrList);
        stockData.IndicatorName = IndicatorName.EhlersAlternateSignalToNoiseRatio;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Discrete Fourier Transform
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="minLength"></param>
    /// <param name="maxLength"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersDiscreteFourierTransform(this StockData stockData, int minLength = 8, int maxLength = 50, int length = 40)
    {
        minLength = Math.Max(minLength, 1);
        maxLength = Math.Max(maxLength, minLength);
        length = Math.Max(length, 1);
        List<double> cleanedDataList = new(stockData.Count);
        List<double> hpList = new(stockData.Count);
        List<double> powerList = new(stockData.Count);
        List<double> dominantCycleList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var twoPiPrd = MinOrMax(2 * Math.PI / length, 0.99, 0.01);
        var alpha = (1 - Math.Sin(twoPiPrd)) / Math.Cos(twoPiPrd);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue1 = i >= 1 ? inputList[i - 1] : 0;
            var prevHp1 = i >= 1 ? hpList[i - 1] : 0;
            var prevHp2 = i >= 2 ? hpList[i - 2] : 0;
            var prevHp3 = i >= 3 ? hpList[i - 3] : 0;
            var prevHp4 = i >= 4 ? hpList[i - 4] : 0;
            var prevHp5 = i >= 5 ? hpList[i - 5] : 0;

            var hp = i <= 5 ? currentValue : (0.5 * (1 + alpha) * (currentValue - prevValue1)) + (alpha * prevHp1);
            hpList.Add(hp);

            var cleanedData = i <= 5 ? currentValue : (hp + (2 * prevHp1) + (3 * prevHp2) + (3 * prevHp3) + (2 * prevHp4) + prevHp5) / 12;
            cleanedDataList.Add(cleanedData);

            double pwr = 0;
            for (var j = minLength; j <= maxLength; j++)
            {
                double cosPart = 0, sinPart = 0;
                for (var n = 0; n <= maxLength - 1; n++)
                {
                    var prevCleanedData = i >= n ? cleanedDataList[i - n] : 0;
                    cosPart += prevCleanedData * Math.Cos(MinOrMax(2 * Math.PI * ((double)n / j), 0.99, 0.01));
                    sinPart += prevCleanedData * Math.Sin(MinOrMax(2 * Math.PI * ((double)n / j), 0.99, 0.01));
                }

                var periodPwr = (cosPart * cosPart) + (sinPart * sinPart);
                pwr = Math.Max(pwr, periodPwr);
            }
            powerList.Add(pwr);

            var maxPwr = i >= minLength ? powerList[i - minLength] : 0;
            double num = 0, denom = 0;
            for (var period = minLength; period <= maxLength; period++)
            {
                var prevPwr = i >= period ? powerList[i - period] : 0;
                maxPwr = prevPwr > maxPwr ? prevPwr : maxPwr;
                var db = maxPwr > 0 && prevPwr > 0 ? -10 * Math.Log(0.01 / (1 - (0.99 * prevPwr / maxPwr))) / Math.Log(10) : 0;
                db = db > 20 ? 20 : db;

                num += db < 3 ? period * (3 - db) : 0;
                denom += db < 3 ? 3 - db : 0;
            }

            var dominantCycle = denom != 0 ? num / denom : 0;
            dominantCycleList.Add(dominantCycle);

            var signal = GetCompareSignal(hp, prevHp1);
            signalsList?.Add(signal);
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
    public static StockData CalculateEhlersDetrendedLeadingIndicator(this StockData stockData, int length = 14)
    {
        length = Math.Max(length, 1);
        List<double> deliList = new(stockData.Count);
        List<double> ema1List = new(stockData.Count);
        List<double> ema2List = new(stockData.Count);
        List<double> dspList = new(stockData.Count);
        List<double> tempList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (_, highList, lowList, _, _) = GetInputValuesList(stockData);

        var alpha = length > 2 ? (double)2 / (length + 1) : 0.67;
        var alpha2 = alpha / 2;

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevHigh = i >= 1 ? highList[i - 1] : 0;
            var prevLow = i >= 1 ? lowList[i - 1] : 0;
            var currentHigh = Math.Max(prevHigh, highList[i]);
            var currentLow = Math.Min(prevLow, lowList[i]);
            var currentPrice = (currentHigh + currentLow) / 2;
            var prevEma1 = i >= 1 ? GetLastOrDefault(ema1List) : currentPrice;
            var prevEma2 = i >= 1 ? GetLastOrDefault(ema2List) : currentPrice;

            var ema1 = (alpha * currentPrice) + ((1 - alpha) * prevEma1);
            ema1List.Add(ema1);

            var ema2 = (alpha2 * currentPrice) + ((1 - alpha2) * prevEma2);
            ema2List.Add(ema2);

            var dsp = ema1 - ema2;
            dspList.Add(dsp);

            var prevTemp = GetLastOrDefault(tempList);
            var temp = (alpha * dsp) + ((1 - alpha) * prevTemp);
            tempList.Add(temp);

            var prevDeli = GetLastOrDefault(deliList);
            var deli = dsp - temp;
            deliList.Add(deli);

            var signal = GetCompareSignal(deli, prevDeli);
            signalsList?.Add(signal);
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
    public static StockData CalculateEhlersBandPassFilterV1(this StockData stockData, int length = 20, double bw = 0.3)
    {
        length = Math.Max(length, 1);
        List<double> hpList = new(stockData.Count);
        List<double> bpList = new(stockData.Count);
        List<double> peakList = new(stockData.Count);
        List<double> signalList = new(stockData.Count);
        List<double> triggerList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var twoPiPrd1 = MinOrMax(0.25 * bw * 2 * Math.PI / length, 0.99, 0.01);
        var twoPiPrd2 = MinOrMax(1.5 * bw * 2 * Math.PI / length, 0.99, 0.01);
        var beta = Math.Cos(MinOrMax(2 * Math.PI / length, 0.99, 0.01));
        var gamma = 1 / Math.Cos(MinOrMax(2 * Math.PI * bw / length, 0.99, 0.01));
        var alpha1 = gamma - Sqrt(Pow(gamma, 2) - 1);
        var alpha2 = (Math.Cos(twoPiPrd1) + Math.Sin(twoPiPrd1) - 1) / Math.Cos(twoPiPrd1);
        var alpha3 = (Math.Cos(twoPiPrd2) + Math.Sin(twoPiPrd2) - 1) / Math.Cos(twoPiPrd2);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevHp1 = i >= 1 ? hpList[i - 1] : 0;
            var prevHp2 = i >= 2 ? hpList[i - 2] : 0;
            var prevBp1 = i >= 1 ? bpList[i - 1] : 0;
            var prevBp2 = i >= 2 ? bpList[i - 2] : 0;

            var hp = ((1 + (alpha2 / 2)) * MinPastValues(i, 1, currentValue - prevValue)) + ((1 - alpha2) * prevHp1);
            hpList.Add(hp);

            var bp = i > 2 ? (0.5 * (1 - alpha1) * (hp - prevHp2)) + (beta * (1 + alpha1) * prevBp1) - (alpha1 * prevBp2) : 0;
            bpList.Add(bp);

            var prevPeak = GetLastOrDefault(peakList);
            var peak = Math.Max(0.991 * prevPeak, Math.Abs(bp));
            peakList.Add(peak);

            var prevSig = GetLastOrDefault(signalList);
            var sig = peak != 0 ? bp / peak : 0;
            signalList.Add(sig);

            var prevTrigger = GetLastOrDefault(triggerList);
            var trigger = ((1 + (alpha3 / 2)) * (sig - prevSig)) + ((1 - alpha3) * prevTrigger);
            triggerList.Add(trigger);

            var signal = GetCompareSignal(sig - trigger, prevSig - prevTrigger);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ebpf", signalList },
            { "Signal", triggerList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(signalList);
        stockData.IndicatorName = IndicatorName.EhlersBandPassFilterV1;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Band Pass Filter V2
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="bw"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersBandPassFilterV2(this StockData stockData, int length = 20, double bw = 0.3)
    {
        length = Math.Max(length, 1);
        List<double> bpList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

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

            var signal = GetCompareSignal(bp - prevBp1, prevBp1 - prevBp2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ebpf", bpList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(bpList);
        stockData.IndicatorName = IndicatorName.EhlersBandPassFilterV2;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Cycle Band Pass Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="delta"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersCycleBandPassFilter(this StockData stockData, int length = 20, double delta = 0.1)
    {
        length = Math.Max(length, 1);
        List<double> bpList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var beta = Math.Cos(MinOrMax(2 * Math.PI / length, 0.99, 0.01));
        var gamma = 1 / Math.Cos(MinOrMax(4 * Math.PI * delta / length, 0.99, 0.01));
        var alpha = gamma - Sqrt(Pow(gamma, 2) - 1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 2 ? inputList[i - 2] : 0;
            var prevBp1 = i >= 1 ? bpList[i - 1] : 0;
            var prevBp2 = i >= 2 ? bpList[i - 2] : 0;

            var bp = (0.5 * (1 - alpha) * MinPastValues(i, 2, currentValue - prevValue)) + (beta * (1 + alpha) * prevBp1) - (alpha * prevBp2);
            bpList.Add(bp);

            var signal = GetCompareSignal(bp - prevBp1, prevBp1 - prevBp2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ecbpf", bpList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(bpList);
        stockData.IndicatorName = IndicatorName.EhlersCycleBandPassFilter;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Cycle Amplitude
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="delta"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersCycleAmplitude(this StockData stockData, int length = 20, double delta = 0.1)
    {
        length = Math.Max(length, 1);
        List<double> ptopList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var lbLength = (int)Math.Ceiling((double)length / 4);

        var bpList = CalculateEhlersCycleBandPassFilter(stockData, length, delta).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevPtop1 = i >= 1 ? ptopList[i - 1] : 0;
            var prevPtop2 = i >= 2 ? ptopList[i - 2] : 0;

            double power = 0;
            for (var j = 0; j < length; j++)
            {
                var prevBp1 = i >= j ? bpList[i - j] : 0;
                var prevBp2 = i >= j + lbLength ? bpList[i - (j + lbLength)] : 0;
                power += Pow(prevBp1, 2) + Pow(prevBp2, 2);
            }

            var ptop = 2 * 1.414 * Sqrt(power / length);
            ptopList.Add(ptop);

            var signal = GetCompareSignal(ptop - prevPtop1, prevPtop1 - prevPtop2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Eca", ptopList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(ptopList);
        stockData.IndicatorName = IndicatorName.EhlersCycleAmplitude;

        return stockData;
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
    public static StockData CalculateEhlersAdaptiveBandPassFilter(this StockData stockData, int length1 = 48, int length2 = 10, int length3 = 3, double bw = 0.3)
    {
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        length3 = Math.Max(length3, 1);
        List<double> bpList = new(stockData.Count);
        List<double> peakList = new(stockData.Count);
        List<double> signalList = new(stockData.Count);
        List<double> triggerList = new(stockData.Count);
        List<double> leadPeakList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var domCycList = CalculateEhlersAutoCorrelationPeriodogram(stockData, length1, length2, length3).CustomValuesList;
        var roofingFilterList = CalculateEhlersRoofingFilterV2(stockData, length1, length2).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var roofingFilter = roofingFilterList[i];
            var domCyc = MinOrMax(domCycList[i], length1, length3);
            var beta = Math.Cos(2 * Math.PI / (0.9 * domCyc));
            var gamma = 1 / Math.Cos(2 * Math.PI * bw / (0.9 * domCyc));
            var alpha = MinOrMax(gamma - Sqrt((gamma * gamma) - 1), 0.99, 0.01);
            var prevRoofingFilter2 = i >= 2 ? roofingFilterList[i - 2] : 0;
            var prevBp1 = i >= 1 ? bpList[i - 1] : 0;
            var prevBp2 = i >= 2 ? bpList[i - 2] : 0;
            var prevSignal1 = i >= 1 ? signalList[i - 1] : 0;
            var prevSignal2 = i >= 2 ? signalList[i - 2] : 0;
            var prevSignal3 = i >= 3 ? signalList[i - 3] : 0;

            var bp = i > 2 ? (0.5 * (1 - alpha) * (roofingFilter - prevRoofingFilter2)) + (beta * (1 + alpha) * prevBp1) - (alpha * prevBp2) : 0;
            bpList.Add(bp);

            var prevPeak = GetLastOrDefault(peakList);
            var peak = Math.Max(0.991 * prevPeak, Math.Abs(bp));
            peakList.Add(peak);

            var sig = peak != 0 ? bp / peak : 0;
            signalList.Add(sig);

            var lead = 1.3 * (sig + prevSignal1 - prevSignal2 - prevSignal3) / 4;
            var prevLeadPeak = GetLastOrDefault(leadPeakList);
            var leadPeak = Math.Max(0.93 * prevLeadPeak, Math.Abs(lead));
            leadPeakList.Add(leadPeak);

            var prevTrigger = GetLastOrDefault(triggerList);
            var trigger = 0.9 * prevSignal1;
            triggerList.Add(trigger);

            var signal = GetRsiSignal(sig - trigger, prevSignal1 - prevTrigger, sig, prevSignal1, MathHelper.InverseSqrt2, -MathHelper.InverseSqrt2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Eabpf", signalList },
            { "Signal", triggerList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(signalList);
        stockData.IndicatorName = IndicatorName.EhlersAdaptiveBandPassFilter;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Cyber Cycle
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="alpha"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersCyberCycle(this StockData stockData, double alpha = 0.07)
    {
        List<double> smoothList = new(stockData.Count);
        List<double> cycleList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue1 = i >= 1 ? inputList[i - 1] : 0;
            var prevValue2 = i >= 2 ? inputList[i - 2] : 0;
            var prevValue3 = i >= 3 ? inputList[i - 3] : 0;
            var prevSmooth1 = i >= 1 ? smoothList[i - 1] : 0;
            var prevSmooth2 = i >= 2 ? smoothList[i - 2] : 0;
            var prevCycle1 = i >= 1 ? cycleList[i - 1] : 0;
            var prevCycle2 = i >= 2 ? cycleList[i - 2] : 0;

            var smooth = (currentValue + (2 * prevValue1) + (2 * prevValue2) + prevValue3) / 6;
            smoothList.Add(smooth);

            var cycle = i < 7 ? (currentValue - (2 * prevValue1) + prevValue2) / 4 : (Pow(1 - (0.5 * alpha), 2) * (smooth - (2 * prevSmooth1) + prevSmooth2)) +
                (2 * (1 - alpha) * prevCycle1) - (Pow(1 - alpha, 2) * prevCycle2);
            cycleList.Add(cycle);

            var signal = GetCompareSignal(cycle - prevCycle1, prevCycle1 - prevCycle2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ecc", cycleList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(cycleList);
        stockData.IndicatorName = IndicatorName.EhlersCyberCycle;

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
    public static StockData CalculateEhlersAMDetector(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 4, 
        int length2 = 8)
    {
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        List<double> absDerList = new(stockData.Count);
        List<double> envList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingMinMax absDerWindow = new(length1);
        var (inputList, _, _, openList, _) = GetInputValuesList(stockData);

        var emaList = GetMovingAverageList(stockData, maType, length1, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentClose = inputList[i];
            var currentOpen = openList[i];
            var der = currentClose - currentOpen;

            var absDer = Math.Abs(der);
            absDerList.Add(absDer);
            absDerWindow.Add(absDer);

            var env = absDerWindow.Max;
            envList.Add(env);
        }

        var volList = GetMovingAverageList(stockData, maType, length2, envList);
        var volEmaList = GetMovingAverageList(stockData, maType, length2, volList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var vol = volList[i];
            var volEma = volEmaList[i];
            var ema = emaList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevEma = i >= 1 ? emaList[i - 1] : 0;

            var signal = GetVolatilitySignal(currentValue - ema, prevValue - prevEma, vol, volEma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Eamd", volList },
            { "Signal", volEmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(volList);
        stockData.IndicatorName = IndicatorName.EhlersAMDetector;

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
    public static StockData CalculateEhlersConvolutionIndicator(this StockData stockData, int length1 = 80, int length2 = 40, int length3 = 48)
    {
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        length3 = Math.Max(length3, 1);
        List<double> convList = new(stockData.Count);
        List<double> hpList = new(stockData.Count);
        List<double> roofingFilterList = new(stockData.Count);
        List<double> slopeList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var piPrd = MathHelper.Sqrt2 * Math.PI / length1;
        var alpha = (Math.Cos(piPrd) + Math.Sin(piPrd) - 1) / Math.Cos(piPrd);
        var a1 = Exp(-MathHelper.Sqrt2 * Math.PI / length2);
        var b1 = 2 * a1 * Math.Cos(MathHelper.Sqrt2 * Math.PI / length2);
        var c2 = b1;
        var c3 = -a1 * a1;
        var c1 = 1 - c2 - c3;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue1 = i >= 1 ? inputList[i - 1] : 0;
            var prevValue2 = i >= 2 ? inputList[i - 2] : 0;
            var prevHp1 = i >= 1 ? hpList[i - 1] : 0;
            var prevHp2 = i >= 2 ? hpList[i - 2] : 0;
            var prevRoofingFilter1 = i >= 1 ? roofingFilterList[i - 1] : 0;
            var prevRoofingFilter2 = i >= 2 ? roofingFilterList[i - 2] : 0;

            var highPass = (Pow(1 - (alpha / 2), 2) * (currentValue - (2 * prevValue1) + prevValue2)) + (2 * (1 - alpha) * prevHp1) -
                           (Pow(1 - alpha, 2) * prevHp2);
            hpList.Add(highPass);

            var roofingFilter = (c1 * ((highPass + prevHp1) / 2)) + (c2 * prevRoofingFilter1) + (c3 * prevRoofingFilter2);
            roofingFilterList.Add(roofingFilter);

            var n = i + 1;
            double sx = 0, sy = 0, sxx = 0, syy = 0, sxy = 0, corr = 0, conv = 0, slope = 0;
            for (var j = 1; j <= length3; j++)
            {
                var x = i >= j - 1 ? roofingFilterList[i - (j - 1)] : 0;
                var y = i >= j ? roofingFilterList[i - j] : 0;
                sx += x;
                sy += y;
                sxx += Pow(x, 2);
                sxy += x * y;
                syy += Pow(y, 2);
                corr = ((n * sxx) - (sx * sx)) * ((n * syy) - (sy * sy)) > 0 ? ((n * sxy) - (sx * sy)) /
                    Sqrt(((n * sxx) - (sx * sx)) * ((n * syy) - (sy * sy))) : 0;
                conv = (1 + (Exp(3 * corr) - 1)) / (Exp(3 * corr) + 1) / 2;

                var filtLength = (int)Math.Ceiling(0.5 * n);
                var prevFilt = i >= filtLength ? roofingFilterList[i - filtLength] : 0;
                slope = prevFilt < roofingFilter ? -1 : 1;
            }
            convList.Add(conv);
            slopeList.Add(slope);

            var signal = GetCompareSignal(roofingFilter - prevRoofingFilter1, prevRoofingFilter1 - prevRoofingFilter2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Eci", convList },
            { "Slope", slopeList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(convList);
        stockData.IndicatorName = IndicatorName.EhlersConvolutionIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Adaptive Relative Strength Index V1
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="cycPart"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersAdaptiveRelativeStrengthIndexV1(this StockData stockData, double cycPart = 0.5)
    {
        List<double> arsiList = new(stockData.Count);
        List<double> arsiEmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var spList = CalculateEhlersMotherOfAdaptiveMovingAverages(stockData).OutputValues["SmoothPeriod"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var sp = spList[i];
            var prevArsi1 = i >= 1 ? arsiEmaList[i - 1] : 0;
            var prevArsi2 = i >= 2 ? arsiEmaList[i - 2] : 0;

            double cu = 0, cd = 0;
            for (var j = 0; j < (int)Math.Ceiling(cycPart * sp); j++)
            {
                var price = i >= j ? inputList[i - j] : 0;
                var pPrice = i >= j + 1 ? inputList[i - (j + 1)] : 0;

                cu += price - pPrice > 0 ? price - pPrice : 0;
                cd += price - pPrice < 0 ? pPrice - price : 0;
            }

            var arsi = cu + cd != 0 ? 100 * cu / (cu + cd) : 0;
            arsiList.Add(arsi);

            var arsiEma = CalculateEMA(arsi, prevArsi1, (int)Math.Ceiling(sp));
            arsiEmaList.Add(arsiEma);

            var signal = GetRsiSignal(arsiEma - prevArsi1, prevArsi1 - prevArsi2, arsiEma, prevArsi1, 70, 30);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Earsi", arsiList },
            { "Signal", arsiEmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(arsiList);
        stockData.IndicatorName = IndicatorName.EhlersAdaptiveRelativeStrengthIndexV1;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Adaptive Relative Strength Index Fisher Transform V1
    /// </summary>
    /// <param name="stockData"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersAdaptiveRsiFisherTransformV1(this StockData stockData)
    {
        List<double> fishList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var arsiList = CalculateEhlersAdaptiveRelativeStrengthIndexV1(stockData).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var arsi = arsiList[i] / 100;
            var prevFish1 = i >= 1 ? fishList[i - 1] : 0;
            var prevFish2 = i >= 2 ? fishList[i - 2] : 0;
            var tranRsi = 2 * (arsi - 0.5);
            var ampRsi = MinOrMax(1.5 * tranRsi, 0.999, -0.999);

            var fish = 0.5 * Math.Log((1 + ampRsi) / (1 - ampRsi));
            fishList.Add(fish);

            var signal = GetCompareSignal(fish - prevFish1, prevFish1 - prevFish2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Earsift", fishList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(fishList);
        stockData.IndicatorName = IndicatorName.EhlersAdaptiveRsiFisherTransformV1;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Adaptive Stochastic Indicator V1
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="cycPart"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersAdaptiveStochasticIndicatorV1(this StockData stockData, double cycPart = 0.5)
    {
        List<double> astocList = new(stockData.Count);
        List<double> astocEmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        var spList = CalculateEhlersMotherOfAdaptiveMovingAverages(stockData).OutputValues["SmoothPeriod"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var sp = spList[i];
            var high = highList[i];
            var low = lowList[i];
            var close = inputList[i];
            var prevAstoc1 = i >= 1 ? astocEmaList[i - 1] : 0;
            var prevAstoc2 = i >= 2 ? astocEmaList[i - 2] : 0;

            var length = (int)Math.Ceiling(cycPart * sp);
            double hh = high, ll = low;
            for (var j = 0; j < length; j++)
            {
                var h = i >= j ? highList[i - j] : 0;
                var l = i >= j ? lowList[i - j] : 0;

                hh = h > hh ? h : hh;
                ll = l < ll ? l : ll;
            }

            var astoc = hh - ll != 0 ? 100 * (close - ll) / (hh - ll) : 0;
            astocList.Add(astoc);

            var astocEma = CalculateEMA(astoc, prevAstoc1, length);
            astocEmaList.Add(astocEma);

            var signal = GetRsiSignal(astocEma - prevAstoc1, prevAstoc1 - prevAstoc2, astocEma, prevAstoc1, 70, 30);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Easi", astocList },
            { "Signal", astocEmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(astocList);
        stockData.IndicatorName = IndicatorName.EhlersAdaptiveStochasticIndicatorV1;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Adaptive Commodity Channel Index V1
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="inputName"></param>
    /// <param name="cycPart"></param>
    /// <param name="constant"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersAdaptiveCommodityChannelIndexV1(this StockData stockData, InputName inputName = InputName.TypicalPrice, double cycPart = 1,
        double constant = 0.015)
    {
        List<double> acciList = new(stockData.Count);
        List<double> acciEmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _, _) = GetInputValuesList(inputName, stockData);

        var spList = CalculateEhlersMotherOfAdaptiveMovingAverages(stockData).OutputValues["SmoothPeriod"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var sp = spList[i];
            var prevAcci1 = i >= 1 ? acciEmaList[i - 1] : 0;
            var prevAcci2 = i >= 2 ? acciEmaList[i - 2] : 0;
            var tp = inputList[i];

            var length = (int)Math.Ceiling(cycPart * sp);
            double avg = 0;
            for (var j = 0; j < length; j++)
            {
                var prevMp = i >= j ? inputList[i - j] : 0;
                avg += prevMp;
            }
            avg /= length;

            double md = 0;
            for (var j = 0; j < length; j++)
            {
                var prevMp = i >= j ? inputList[i - j] : 0;
                md += Math.Abs(prevMp - avg);
            }
            md /= length;

            var acci = md != 0 ? (tp - avg) / (constant * md) : 0;
            acciList.Add(acci);

            var acciEma = CalculateEMA(acci, prevAcci1, (int)Math.Ceiling(sp));
            acciEmaList.Add(acciEma);

            var signal = GetRsiSignal(acciEma - prevAcci1, prevAcci1 - prevAcci2, acciEma, prevAcci1, 100, -100);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Eacci", acciList },
            { "Signal", acciEmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(acciList);
        stockData.IndicatorName = IndicatorName.EhlersAdaptiveCommodityChannelIndexV1;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Commodity Channel Index Inverse Fisher Transform
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="inputName"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="signalLength"></param>
    /// <param name="constant"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersCommodityChannelIndexInverseFisherTransform(this StockData stockData, InputName inputName = InputName.TypicalPrice,
        MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 20, int signalLength = 9, double constant = 0.015)
    {
        length = Math.Max(length, 1);
        signalLength = Math.Max(signalLength, 1);
        List<double> v1List = new(stockData.Count);
        List<double> iFishList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var cciList = CalculateCommodityChannelIndex(stockData, inputName, maType, length, constant).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var cci = cciList[i];

            var v1 = 0.1 * (cci - 50);
            v1List.Add(v1);
        }

        var v2List = GetMovingAverageList(stockData, maType, signalLength, v1List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var v2 = v2List[i];
            var expValue = Exp(2 * v2);
            var prevIFish1 = i >= 1 ? iFishList[i - 1] : 0;
            var prevIFish2 = i >= 2 ? iFishList[i - 2] : 0;

            var iFish = expValue + 1 != 0 ? (expValue - 1) / (expValue + 1) : 0;
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

