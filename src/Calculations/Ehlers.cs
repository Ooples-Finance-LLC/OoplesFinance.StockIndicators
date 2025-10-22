//     Ooples Finance Stock Indicator Library
//     https://ooples.github.io/OoplesFinance.StockIndicators/
//
//     Copyright © Franklin Moormann, 2020-2022
//     cheatcountry@gmail.com
//
//     This library is free software and it uses the Apache 2.0 license
//     so if you are going to re-use or modify my code then I just ask
//     that you include my copyright info and my contact info in a comment

namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Ehlers Roofing Filter V2
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="lowerLength">Length of the lower.</param>
    /// <param name="upperLength">Length of the upper.</param>
    /// <returns></returns>
    public static StockData CalculateEhlersRoofingFilterV2(this StockData stockData, int upperLength = 80, int lowerLength = 40)
    {
        List<double> highPassList = new();
        List<double> roofingFilterList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var alphaArg = Math.Min(0.707 * 2 * Math.PI / upperLength, 0.99);
        var alphaCos = Math.Cos(alphaArg);
        var alpha1 = alphaCos != 0 ? (alphaCos + Math.Sin(alphaArg) - 1) / alphaCos : 0;
        var a1 = Exp(-1.414 * Math.PI / lowerLength);
        var b1 = 2 * a1 * Math.Cos(Math.Min(1.414 * Math.PI / lowerLength, 0.99));
        var c2 = b1;
        var c3 = -1 * a1 * a1;
        var c1 = 1 - c2 - c3;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue1 = i >= 1 ? inputList[i - 1] : 0;
            var prevValue2 = i >= 2 ? inputList[i - 2] : 0;
            var prevFilter1 = i >= 1 ? roofingFilterList[i - 1] : 0;
            var prevFilter2 = i >= 2 ? roofingFilterList[i - 2] : 0;
            var prevHp1 = i >= 1 ? highPassList[i - 1] : 0;
            var prevHp2 = i >= 2 ? highPassList[i - 2] : 0;
            var test1 = Pow((1 - alpha1) / 2, 2);
            var test2 = currentValue - (2 * prevValue1) + prevValue2;
            var v1 = test1 * test2;
            var v2 = 2 * (1 - alpha1) * prevHp1;
            var v3 = Pow(1 - alpha1, 2) * prevHp2;

            var highPass = v1 + v2 - v3;
            highPassList.AddRounded(highPass);

            var prevRoofingFilter1 = i >= 1 ? roofingFilterList[i - 1] : 0;
            var prevRoofingFilter2 = i >= 2 ? roofingFilterList[i - 2] : 0;
            var roofingFilter = (c1 * ((highPass + prevHp1) / 2)) + (c2 * prevFilter1) + (c3 * prevFilter2);
            roofingFilterList.AddRounded(roofingFilter);

            var signal = GetCompareSignal(roofingFilter - prevRoofingFilter1, prevRoofingFilter1 - prevRoofingFilter2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Erf", roofingFilterList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = roofingFilterList;
        stockData.IndicatorName = IndicatorName.EhlersRoofingFilterV2;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Phase Calculation
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersPhaseCalculation(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 15)
    {
        List<double> phaseList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            double realPart = 0, imagPart = 0;
            for (var j = 0; j < length; j++)
            {
                var weight = i >= j ? inputList[i - j] : 0;
                realPart += Math.Cos(2 * Math.PI * j / length) * weight;
                imagPart += Math.Sin(2 * Math.PI * j / length) * weight;
            }

            var phase = Math.Abs(realPart) > 0.001 ? Math.Atan(imagPart / realPart).ToDegrees() : 90 * Math.Sign(imagPart);
            phase = realPart < 0 ? phase + 180 : phase;
            phase += 90;
            phase = phase < 0 ? phase + 360 : phase;
            phase = phase > 360 ? phase - 360 : phase;
            phaseList.AddRounded(phase);
        }

        var phaseEmaList = GetMovingAverageList(stockData, maType, length, phaseList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var phase = phaseList[i];
            var phaseEma = phaseEmaList[i];
            var prevPhase = i >= 1 ? phaseList[i - 1] : 0;
            var prevPhaseEma = i >= 1 ? phaseEmaList[i - 1] : 0;

            var signal = GetCompareSignal(phase - phaseEma, prevPhase - prevPhaseEma, true);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Phase", phaseList },
            { "Signal", phaseEmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = phaseList;
        stockData.IndicatorName = IndicatorName.EhlersPhaseCalculation;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Adaptive Cyber Cycle
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="alpha"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersAdaptiveCyberCycle(this StockData stockData, int length = 5, double alpha = 0.07)
    {
        List<double> ipList = new();
        List<double> q1List = new();
        List<double> i1List = new();
        List<double> dpList = new();
        List<double> pList = new();
        List<double> acList = new();
        List<double> cycleList = new();
        List<double> smoothList = new();
        List<Signal> signalsList = new();
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
            smoothList.AddRounded(smooth);

            var cycle = i < 7 ? (currentValue - (2 * prevValue) + prevValue2) / 4 : (Pow(1 - (0.5 * alpha), 2) * (smooth - (2 * prevSmooth) + prevSmooth2)) +
            (2 * (1 - alpha) * prevCycle) - (Pow(1 - alpha, 2) * prevCycle2);
            cycleList.AddRounded(cycle);

            var q1 = ((0.0962 * cycle) + (0.5769 * prevCycle2) - (0.5769 * prevCycle4) - (0.0962 * prevCycle6)) * (0.5 + (0.08 * prevIp));
            q1List.AddRounded(q1);

            var i1 = prevCycle3;
            i1List.AddRounded(i1);

            var dp = MinOrMax(q1 != 0 && prevQ1 != 0 ? ((i1 / q1) - (prevI1 / prevQ1)) / (1 + (i1 * prevI1 / (q1 * prevQ1))) : 0, 1.1, 0.1);
            dpList.AddRounded(dp);

            var medianDelta = dpList.TakeLastExt(length).Median();
            var dc = medianDelta != 0 ? (6.28318 / medianDelta) + 0.5 : 15;

            var ip = (0.33 * dc) + (0.67 * prevIp);
            ipList.AddRounded(ip);

            var p = (0.15 * ip) + (0.85 * prevP);
            pList.AddRounded(p);

            var a1 = 2 / (p + 1);
            var ac = i < 7 ? (currentValue - (2 * prevValue) + prevValue2) / 4 :
                (Pow(1 - (0.5 * a1), 2) * (smooth - (2 * prevSmooth) + prevSmooth2)) + (2 * (1 - a1) * prevAc1) - (Pow(1 - a1, 2) * prevAc2);
            acList.AddRounded(ac);

            var signal = GetCompareSignal(ac - prevAc1, prevAc1 - prevAc2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Eacc", acList },
            { "Period", pList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = acList;
        stockData.IndicatorName = IndicatorName.EhlersAdaptiveCyberCycle;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Simple Decycler
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="upperPct"></param>
    /// <param name="lowerPct"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersSimpleDecycler(this StockData stockData, int length = 125, double upperPct = 0.5, double lowerPct = 0.5)
    {
        List<double> decyclerList = new();
        List<double> upperBandList = new();
        List<double> lowerBandList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var hpList = CalculateEhlersHighPassFilterV1(stockData, length, 1).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var hp = hpList[i];

            var prevDecycler = decyclerList.LastOrDefault();
            var decycler = currentValue - hp;
            decyclerList.AddRounded(decycler);

            var upperBand = (1 + (upperPct / 100)) * decycler;
            upperBandList.AddRounded(upperBand);

            var lowerBand = (1 - (lowerPct / 100)) * decycler;
            lowerBandList.AddRounded(lowerBand);

            var signal = GetCompareSignal(currentValue - decycler, prevValue - prevDecycler);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", upperBandList },
            { "MiddleBand", decyclerList },
            { "LowerBand", lowerBandList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = decyclerList;
        stockData.IndicatorName = IndicatorName.EhlersSimpleDecycler;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers High Pass Filter V1
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="mult"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersHighPassFilterV1(this StockData stockData, int length = 125, double mult = 1)
    {
        List<double> highPassList = new();
        List<Signal> signalsList = new();
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
            highPassList.AddRounded(highPass);

            var signal = GetCompareSignal(highPass, prevHp1);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Hp", highPassList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = highPassList;
        stockData.IndicatorName = IndicatorName.EhlersHighPassFilterV1;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Rocket Relative Strength Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="obosLevel"></param>
    /// <param name="mult"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersRocketRelativeStrengthIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.Ehlers2PoleSuperSmootherFilterV2, 
        int length1 = 10, int length2 = 8, double obosLevel = 2, double mult = 1)
    {
        List<double> momList = new();
        List<double> argList = new();
        List<double> ssf2PoleRocketRsiList = new();
        List<double> ssf2PoleUpChgList = new();
        List<double> ssf2PoleDownChgList = new();
        List<double> ssf2PoleTmpList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var obLevel = obosLevel * mult;
        var osLevel = -obosLevel * mult;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= length1 - 1 ? inputList[i - (length1 - 1)] : 0;

            var prevMom = momList.LastOrDefault();
            var mom = MinPastValues(i, length1 - 1, currentValue - prevValue);
            momList.AddRounded(mom);

            var arg = (mom + prevMom) / 2;
            argList.AddRounded(arg);
        }

        var argSsf2PoleList = GetMovingAverageList(stockData, maType, length2, argList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var ssf2Pole = argSsf2PoleList[i];
            var prevSsf2Pole = i >= 1 ? argSsf2PoleList[i - 1] : 0;
            var prevRocketRsi1 = i >= 1 ? ssf2PoleRocketRsiList[i - 1] : 0;
            var prevRocketRsi2 = i >= 2 ? ssf2PoleRocketRsiList[i - 2] : 0;
            var ssf2PoleMom = ssf2Pole - prevSsf2Pole;

            var up2PoleChg = ssf2PoleMom > 0 ? ssf2PoleMom : 0;
            ssf2PoleUpChgList.AddRounded(up2PoleChg);

            var down2PoleChg = ssf2PoleMom < 0 ? Math.Abs(ssf2PoleMom) : 0;
            ssf2PoleDownChgList.AddRounded(down2PoleChg);

            var up2PoleChgSum = ssf2PoleUpChgList.TakeLastExt(length1).Sum();
            var down2PoleChgSum = ssf2PoleDownChgList.TakeLastExt(length1).Sum();

            var prevTmp2Pole = ssf2PoleTmpList.LastOrDefault();
            var tmp2Pole = up2PoleChgSum + down2PoleChgSum != 0 ?
                MinOrMax((up2PoleChgSum - down2PoleChgSum) / (up2PoleChgSum + down2PoleChgSum), 0.999, -0.999) : prevTmp2Pole;
            ssf2PoleTmpList.AddRounded(tmp2Pole);

            var ssf2PoleTempLog = 1 - tmp2Pole != 0 ? (1 + tmp2Pole) / (1 - tmp2Pole) : 0;
            var ssf2PoleLog = Math.Log(ssf2PoleTempLog);
            var ssf2PoleRocketRsi = 0.5 * ssf2PoleLog * mult;
            ssf2PoleRocketRsiList.AddRounded(ssf2PoleRocketRsi);

            var signal = GetRsiSignal(ssf2PoleRocketRsi - prevRocketRsi1, prevRocketRsi1 - prevRocketRsi2, ssf2PoleRocketRsi, prevRocketRsi1, obLevel, osLevel);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Errsi", ssf2PoleRocketRsiList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = ssf2PoleRocketRsiList;
        stockData.IndicatorName = IndicatorName.EhlersRocketRelativeStrengthIndex;

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
        List<double> corrList = new();
        List<Signal> signalsList = new();
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
            corrList.AddRounded(corr);

            var signal = GetRsiSignal(corr - prevCorr1, prevCorr1 - prevCorr2, corr, prevCorr1, 0.5, -0.5);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ecti", corrList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = corrList;
        stockData.IndicatorName = IndicatorName.EhlersCorrelationTrendIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Relative Vigor Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersRelativeVigorIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 10,
        int signalLength = 4)
    {
        List<double> rviList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, openList, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentClose = inputList[i];
            var currentOpen = openList[i];
            var currentHigh = highList[i];
            var currentLow = lowList[i];

            var rvi = currentHigh - currentLow != 0 ? (currentClose - currentOpen) / (currentHigh - currentLow) : 0;
            rviList.AddRounded(rvi);
        }

        var rviSmaList = GetMovingAverageList(stockData, maType, length, rviList);
        var rviSignalList = GetMovingAverageList(stockData, maType, signalLength, rviSmaList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var rviSma = rviSmaList[i];
            var prevRviSma = i >= 1 ? rviSmaList[i - 1] : 0;
            var rviSignal = rviSignalList[i];
            var prevRviSignal = i >= 1 ? rviSignalList[i - 1] : 0;

            var signal = GetCompareSignal(rviSma - rviSignal, prevRviSma - prevRviSignal);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ervi", rviSmaList },
            { "Signal", rviSignalList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = rviSmaList;
        stockData.IndicatorName = IndicatorName.EhlersRelativeVigorIndex;

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
        List<double> cgList = new();
        List<Signal> signalsList = new();
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

            var prevCg = cgList.LastOrDefault();
            var cg = denom != 0 ? (-num / denom) + ((double)(length + 1) / 2) : 0;
            cgList.AddRounded(cg);

            var signal = GetCompareSignal(cg, prevCg);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ecog", cgList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = cgList;
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
        List<double> cgList = new();
        List<Signal> signalsList = new();
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
            cgList.AddRounded(cg);

            var signal = GetCompareSignal(cg - prevCg1, prevCg1 - prevCg2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Eacog", cgList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = cgList;
        stockData.IndicatorName = IndicatorName.EhlersAdaptiveCenterOfGravityOscillator;

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
    public static StockData CalculateEhlersSmoothedAdaptiveMomentum(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length1 = 5, int length2 = 8)
    {
        List<double> f3List = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var a1 = Exp(-Math.PI / length2);
        var b1 = 2 * a1 * Math.Cos(1.738 * Math.PI / length2);
        var c1 = Pow(a1, 2);
        var coef2 = b1 + c1;
        var coef3 = -1 * (c1 + (b1 * c1));
        var coef4 = c1 * c1;
        var coef1 = 1 - coef2 - coef3 - coef4;

        var pList = CalculateEhlersAdaptiveCyberCycle(stockData, length1).OutputValues["Period"];

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
            f3List.AddRounded(f3);
        }

        var f3EmaList = GetMovingAverageList(stockData, maType, length2, f3List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var f3 = f3List[i];
            var f3Ema = f3EmaList[i];
            var prevF3 = i >= 1 ? f3List[i - 1] : 0;
            var prevF3Ema = i >= 1 ? f3EmaList[i - 1] : 0;

            var signal = GetCompareSignal(f3 - f3Ema, prevF3 - prevF3Ema);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Esam", f3List },
            { "Signal", f3EmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = f3List;
        stockData.IndicatorName = IndicatorName.EhlersSmoothedAdaptiveMomentumIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Stochastic Center Of Gravity Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersStochasticCenterOfGravityOscillator(this StockData stockData, int length = 8)
    {
        List<double> v1List = new();
        List<double> v2List = new();
        List<double> tList = new();
        List<Signal> signalsList = new();

        var ehlersCGOscillatorList = CalculateEhlersCenterofGravityOscillator(stockData, length).CustomValuesList;
        var (highestList, lowestList) = GetMaxAndMinValuesList(ehlersCGOscillatorList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var cg = ehlersCGOscillatorList[i];
            var maxc = highestList[i];
            var minc = lowestList[i];
            var prevV1_1 = i >= 1 ? v1List[i - 1] : 0;
            var prevV1_2 = i >= 2 ? v1List[i - 2] : 0;
            var prevV1_3 = i >= 3 ? v1List[i - 3] : 0;
            var prevV2_1 = i >= 1 ? v2List[i - 1] : 0;
            var prevT1 = i >= 1 ? tList[i - 1] : 0;
            var prevT2 = i >= 2 ? tList[i - 2] : 0;

            var v1 = maxc - minc != 0 ? (cg - minc) / (maxc - minc) : 0;
            v1List.AddRounded(v1);

            var v2_ = ((4 * v1) + (3 * prevV1_1) + (2 * prevV1_2) + prevV1_3) / 10;
            var v2 = 2 * (v2_ - 0.5);
            v2List.AddRounded(v2);

            var t = MinOrMax(0.96 * (prevV2_1 + 0.02), 1, 0);
            tList.AddRounded(t);

            var signal = GetRsiSignal(t - prevT1, prevT1 - prevT2, t, prevT1, 0.8, 0.2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Escog", tList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = tList;
        stockData.IndicatorName = IndicatorName.EhlersStochasticCenterOfGravityOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Simple Cycle Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="alpha"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersSimpleCycleIndicator(this StockData stockData, double alpha = 0.07)
    {
        List<double> smoothList = new();
        List<double> cycleList = new();
        List<double> cycle_List = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentMedianPrice = inputList[i];
            var prevMedianPrice1 = i >= 1 ? inputList[i - 1] : 0;
            var prevMedianPrice2 = i >= 2 ? inputList[i - 2] : 0;
            var prevMedianPrice3 = i >= 3 ? inputList[i - 3] : 0;
            var prevSmooth1 = smoothList.LastOrDefault();
            var prevCycle1 = cycle_List.LastOrDefault();
            var prevSmooth2 = i >= 2 ? smoothList[i - 2] : 0;
            var prevCycle2 = i >= 2 ? cycle_List[i - 2] : 0;
            var prevCyc1 = i >= 1 ? cycleList[i - 1] : 0;
            var prevCyc2 = i >= 2 ? cycleList[i - 2] : 0;

            var smooth = (currentMedianPrice + (2 * prevMedianPrice1) + (2 * prevMedianPrice2) + prevMedianPrice3) / 6;
            smoothList.AddRounded(smooth);

            var cycle_ = ((1 - (0.5 * alpha)) * (1 - (0.5 * alpha)) * (smooth - (2 * prevSmooth1) + prevSmooth2)) + (2 * (1 - alpha) * prevCycle1) -
                         ((1 - alpha) * (1 - alpha) * prevCycle2);
            cycle_List.AddRounded(cycle_);

            var cycle = i < 7 ? (currentMedianPrice - (2 * prevMedianPrice1) + prevMedianPrice2) / 4 : cycle_;
            cycleList.AddRounded(cycle);

            var signal = GetCompareSignal(cycle - prevCyc1, prevCyc1 - prevCyc2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Esci", cycleList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = cycleList;
        stockData.IndicatorName = IndicatorName.EhlersSimpleCycleIndicator;

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
        List<double> decycler1OscillatorList = new();
        List<double> decycler2OscillatorList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var decycler1List = CalculateEhlersSimpleDecycler(stockData, fastLength).CustomValuesList;
        var decycler2List = CalculateEhlersSimpleDecycler(stockData, slowLength).CustomValuesList;
        stockData.CustomValuesList = decycler1List;
        var decycler1FilteredList = CalculateEhlersHighPassFilterV1(stockData, fastLength, 0.5).CustomValuesList;
        stockData.CustomValuesList = decycler2List;
        var decycler2FilteredList = CalculateEhlersHighPassFilterV1(stockData, slowLength, 0.5).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var decycler1Filtered = decycler1FilteredList[i];
            var decycler2Filtered = decycler2FilteredList[i];

            var prevDecyclerOsc1 = decycler1OscillatorList.LastOrDefault();
            var decyclerOscillator1 = currentValue != 0 ? 100 * fastMult * decycler1Filtered / currentValue : 0;
            decycler1OscillatorList.AddRounded(decyclerOscillator1);

            var prevDecyclerOsc2 = decycler2OscillatorList.LastOrDefault();
            var decyclerOscillator2 = currentValue != 0 ? 100 * slowMult * decycler2Filtered / currentValue : 0;
            decycler2OscillatorList.AddRounded(decyclerOscillator2);

            var signal = GetCompareSignal(decyclerOscillator2 - decyclerOscillator1, prevDecyclerOsc2 - prevDecyclerOsc1);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "FastEdo", decycler1OscillatorList },
            { "SlowEdo", decycler2OscillatorList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.EhlersDecyclerOscillatorV1;

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
        List<double> hpList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var a1 = Exp(-1.414 * Math.PI / length);
        var b1 = 2 * a1 * Math.Cos(1.414 * Math.PI / length);
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
            hpList.AddRounded(hp);
        }

        var hpMa1List = GetMovingAverageList(stockData, maType, length, hpList);
        var hpMa2List = GetMovingAverageList(stockData, maType, length, hpMa1List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var hp = hpMa2List[i];
            var prevHp1 = i >= 1 ? hpMa2List[i - 1] : 0;
            var prevHp2 = i >= 2 ? hpMa2List[i - 2] : 0;

            var signal = GetCompareSignal(hp - prevHp1, prevHp1 - prevHp2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ehpf", hpMa2List }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = hpMa2List;
        stockData.IndicatorName = IndicatorName.EhlersHighPassFilterV2;

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
        List<double> decList = new();
        List<Signal> signalsList = new();

        var hp1List = CalculateEhlersHighPassFilterV2(stockData, maType, fastLength).CustomValuesList;
        var hp2List = CalculateEhlersHighPassFilterV2(stockData, maType, slowLength).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var hp1 = hp1List[i];
            var hp2 = hp2List[i];

            var prevDec = decList.LastOrDefault();
            var dec = hp2 - hp1;
            decList.AddRounded(dec);

            var signal = GetCompareSignal(dec, prevDec);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Edo", decList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = decList;
        stockData.IndicatorName = IndicatorName.EhlersDecyclerOscillatorV2;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Modified Stochastic Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersModifiedStochasticIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.Ehlers2PoleSuperSmootherFilterV1,
        int length1 = 48, int length2 = 10, int length3 = 20)
    {
        List<double> stocList = new();
        List<double> modStocList = new();
        List<Signal> signalsList = new();

        var a1 = Exp(-1.414 * Math.PI / length1);
        var b1 = 2 * a1 * Math.Cos(Math.Min(1.414 * Math.PI / length1, 0.99));
        var c2 = b1;
        var c3 = -1 * a1 * a1;
        var c1 = 1 - c2 - c3;

        var roofingFilterList = CalculateEhlersRoofingFilterV1(stockData, maType, length1, length2).CustomValuesList;
        var (highestList, lowestList) = GetMaxAndMinValuesList(roofingFilterList, length3);

        for (var i = 0; i < stockData.Count; i++)
        {
            var highest = highestList[i];
            var lowest = lowestList[i];
            var roofingFilter = roofingFilterList[i];
            var prevModStoc1 = i >= 1 ? modStocList[i - 1] : 0;
            var prevModStoc2 = i >= 2 ? modStocList[i - 2] : 0;

            var prevStoc = stocList.LastOrDefault();
            var stoc = highest - lowest != 0 ? (roofingFilter - lowest) / (highest - lowest) * 100 : 0;
            stocList.AddRounded(stoc);

            var modStoc = (c1 * ((stoc + prevStoc) / 2)) + (c2 * prevModStoc1) + (c3 * prevModStoc2);
            modStocList.AddRounded(modStoc);

            var signal = GetRsiSignal(modStoc - prevModStoc1, prevModStoc1 - prevModStoc2, modStoc, prevModStoc1, 70, 30);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Emsi", modStocList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = modStocList;
        stockData.IndicatorName = IndicatorName.EhlersModifiedStochasticIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Modified Relative Strength Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersModifiedRelativeStrengthIndex(this StockData stockData, int length1 = 48, int length2 = 10, int length3 = 10)
    {
        List<double> upChgList = new();
        List<double> upChgSumList = new();
        List<double> denomList = new();
        List<double> mrsiList = new();
        List<double> mrsiSigList = new();
        List<Signal> signalsList = new();

        var a1 = Exp(-1.414 * Math.PI / length2);
        var b1 = 2 * a1 * Math.Cos(Math.Min(1.414 * Math.PI / length2, 0.99));
        var c2 = b1;
        var c3 = -1 * a1 * a1;
        var c1 = 1 - c2 - c3;

        var roofingFilterList = CalculateEhlersRoofingFilterV2(stockData, length1, length2).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var roofingFilter = roofingFilterList[i];
            var prevRoofingFilter = i >= 1 ? roofingFilterList[i - 1] : 0;
            var prevMrsi1 = i >= 1 ? mrsiList[i - 1] : 0;
            var prevMrsi2 = i >= 2 ? mrsiList[i - 2] : 0;
            var prevMrsiSig1 = i >= 1 ? mrsiSigList[i - 1] : 0;
            var prevMrsiSig2 = i >= 2 ? mrsiSigList[i - 2] : 0;

            var upChg = roofingFilter > prevRoofingFilter ? roofingFilter - prevRoofingFilter : 0;
            upChgList.AddRounded(upChg);

            var dnChg = roofingFilter < prevRoofingFilter ? prevRoofingFilter - roofingFilter : 0;
            var prevUpChgSum = upChgSumList.LastOrDefault();
            var upChgSum = upChgList.TakeLastExt(length3).Sum();
            upChgSumList.AddRounded(upChgSum);

            var prevDenom = denomList.LastOrDefault();
            var denom = upChg + dnChg;
            denomList.AddRounded(denom);

            var mrsi = denom != 0 && prevDenom != 0 ? (c1 * (((upChgSum / denom) + (prevUpChgSum / prevDenom)) / 2)) + (c2 * prevMrsi1) + (c3 * prevMrsi2) : 0;
            mrsiList.AddRounded(mrsi);

            var mrsiSig = (c1 * ((mrsi + prevMrsi1) / 2)) + (c2 * prevMrsiSig1) + (c3 * prevMrsiSig2);
            mrsiSigList.AddRounded(mrsiSig);

            var signal = GetRsiSignal(mrsi - mrsiSig, prevMrsi1 - prevMrsiSig1, mrsi, prevMrsi1, 0.7, 0.3);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Emrsi", mrsiList },
            { "Signal", mrsiSigList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = mrsiList;
        stockData.IndicatorName = IndicatorName.EhlersModifiedRelativeStrengthIndex;

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
        List<double> highPassList = new();
        List<double> roofingFilterList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var alphaArg = Math.Min(2 * Math.PI / length1, 0.99);
        var alphaCos = Math.Cos(alphaArg);
        var alpha1 = alphaCos != 0 ? (alphaCos + Math.Sin(alphaArg) - 1) / alphaCos : 0;
        var a1 = Exp(-1.414 * Math.PI / length2);
        var b1 = 2 * a1 * Math.Cos(Math.Min(1.414 * Math.PI / length2, 0.99));
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
            highPassList.AddRounded(hp);

            var filter = (c1 * ((hp + prevHp1) / 2)) + (c2 * prevFilter1) + (c3 * prevFilter2);
            roofingFilterList.AddRounded(filter);

            var signal = GetCompareSignal(filter - prevFilter1, prevFilter1 - prevFilter2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ehplprf", roofingFilterList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = roofingFilterList;
        stockData.IndicatorName = IndicatorName.EhlersHpLpRoofingFilter;

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
        List<double> decList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var alphaArg = Math.Min(2 * Math.PI / length, 0.99);
        var alphaCos = Math.Cos(alphaArg);
        var alpha1 = alphaCos != 0 ? (alphaCos + Math.Sin(alphaArg) - 1) / alphaCos : 0;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue1 = i >= 1 ? inputList[i - 1] : 0;

            var prevDec = decList.LastOrDefault();
            var dec = (alpha1 / 2 * (currentValue + prevValue1)) + ((1 - alpha1) * prevDec);
            decList.AddRounded(dec);

            var signal = GetCompareSignal(currentValue - dec, prevValue1 - prevDec);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ed", decList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = decList;
        stockData.IndicatorName = IndicatorName.EhlersDecycler;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Zero Mean Roofing Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersZeroMeanRoofingFilter(this StockData stockData, int length1 = 48, int length2 = 10)
    {
        List<double> zmrFilterList = new();
        List<Signal> signalsList = new();

        var alphaArg = Math.Min(2 * Math.PI / length1, 0.99);
        var alphaCos = Math.Cos(alphaArg);
        var alpha1 = alphaCos != 0 ? (alphaCos + Math.Sin(alphaArg) - 1) / alphaCos : 0;

        var roofingFilterList = CalculateEhlersHpLpRoofingFilter(stockData, length1, length2).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentRf = roofingFilterList[i];
            var prevRf = i >= 1 ? roofingFilterList[i - 1] : 0;
            var prevZmrFilt1 = i >= 1 ? zmrFilterList[i - 1] : 0;
            var prevZmrFilt2 = i >= 2 ? zmrFilterList[i - 2] : 0;

            var zmrFilt = ((1 - (alpha1 / 2)) * (currentRf - prevRf)) + ((1 - alpha1) * prevZmrFilt1);
            zmrFilterList.AddRounded(zmrFilt);

            var signal = GetCompareSignal(zmrFilt - prevZmrFilt1, prevZmrFilt1 - prevZmrFilt2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ezmrf", zmrFilterList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = zmrFilterList;
        stockData.IndicatorName = IndicatorName.EhlersZeroMeanRoofingFilter;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Roofing Filter Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersRoofingFilterIndicator(this StockData stockData, int length1 = 80, int length2 = 40)
    {
        List<double> highPassList = new();
        List<double> roofingFilterList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var alphaArg = Math.Min(0.707 * 2 * Math.PI / length1, 0.99);
        var alphaCos = Math.Cos(alphaArg);
        var a1 = alphaCos != 0 ? (alphaCos + Math.Sin(alphaArg) - 1) / alphaCos : 0;
        var a2 = Exp(-1.414 * Math.PI / length2);
        var b1 = 2 * a2 * Math.Cos(Math.Min(1.414 * Math.PI / length2, 0.99));
        var c2 = b1;
        var c3 = -a2 * a2;
        var c1 = 1 - c2 - c3;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue1 = i >= 1 ? inputList[i - 1] : 0;
            var prevValue2 = i >= 2 ? inputList[i - 2] : 0;
            var prevFilter1 = i >= 1 ? roofingFilterList[i - 1] : 0;
            var prevFilter2 = i >= 2 ? roofingFilterList[i - 2] : 0;
            var prevHp1 = i >= 1 ? highPassList[i - 1] : 0;
            var prevHp2 = i >= 2 ? highPassList[i - 2] : 0;

            var hp = (Pow(1 - (a1 / 2), 2) * (currentValue - (2 * prevValue1) + prevValue2)) + (2 * (1 - a1) * prevHp1) - (Pow(1 - a1, 2) * prevHp2);
            highPassList.AddRounded(hp);

            var filter = (c1 * ((hp + prevHp1) / 2)) + (c2 * prevFilter1) + (c3 * prevFilter2);
            roofingFilterList.AddRounded(filter);

            var signal = GetCompareSignal(filter - prevFilter1, prevFilter1 - prevFilter2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Erfi", roofingFilterList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = roofingFilterList;
        stockData.IndicatorName = IndicatorName.EhlersRoofingFilterIndicator;

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
        List<double> dimenList = new();
        List<double> hurstList = new();
        List<double> smoothHurstList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var hLength = (int)Math.Ceiling((double)length1 / 2);
        var a1 = Exp(-1.414 * Math.PI / length2);
        var b1 = 2 * a1 * Math.Cos(Math.Min(1.414 * Math.PI / length2, 0.99));
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

            var prevDimen = dimenList.LastOrDefault();
            var dimen = 0.5 * (((Math.Log(n1 + n2) - Math.Log(n3)) / Math.Log(2)) + prevDimen);
            dimenList.AddRounded(dimen);

            var prevHurst = hurstList.LastOrDefault();
            var hurst = 2 - dimen;
            hurstList.AddRounded(hurst);

            var smoothHurst = (c1 * ((hurst + prevHurst) / 2)) + (c2 * prevSmoothHurst1) + (c3 * prevSmoothHurst2);
            smoothHurstList.AddRounded(smoothHurst);

            var signal = GetCompareSignal(smoothHurst - prevSmoothHurst1, prevSmoothHurst1 - prevSmoothHurst2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ehc", smoothHurstList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = smoothHurstList;
        stockData.IndicatorName = IndicatorName.EhlersHurstCoefficient;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Reflex Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersReflexIndicator(this StockData stockData, int length = 20)
    {
        List<double> filterList = new();
        List<double> msList = new();
        List<double> reflexList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var a1 = Exp(-1.414 * Math.PI / 0.5 * length);
        var b1 = 2 * a1 * Math.Cos(1.414 * Math.PI / 0.5 * length);
        var c2 = b1;
        var c3 = -a1 * a1;
        var c1 = 1 - c2 - c3;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevFilter1 = filterList.LastOrDefault();
            var prevFilter2 = i >= 2 ? filterList[i - 2] : 0;
            var priorFilter = i >= length ? filterList[i - length] : 0;
            var prevReflex1 = i >= 1 ? reflexList[i - 1] : 0;
            var prevReflex2 = i >= 2 ? reflexList[i - 2] : 0;

            var filter = (c1 * ((currentValue + prevValue) / 2)) + (c2 * prevFilter1) + (c3 * prevFilter2);
            filterList.AddRounded(filter);

            var slope = length != 0 ? (priorFilter - filter) / length : 0;
            double sum = 0;
            for (var j = 1; j <= length; j++)
            {
                var prevFilterCount = i >= j ? filterList[i - j] : 0;
                sum += filter + (j * slope) - prevFilterCount;
            }
            sum /= length;

            var prevMs = msList.LastOrDefault();
            var ms = (0.04 * sum * sum) + (0.96 * prevMs);
            msList.AddRounded(ms);

            var reflex = ms > 0 ? sum / Sqrt(ms) : 0;
            reflexList.AddRounded(reflex);

            var signal = GetCompareSignal(reflex - prevReflex1, prevReflex1 - prevReflex2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Eri", reflexList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = reflexList;
        stockData.IndicatorName = IndicatorName.EhlersReflexIndicator;

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
    public static StockData CalculateEhlersSpectrumDerivedFilterBank(this StockData stockData, int minLength = 8, int maxLength = 50, 
        int length1 = 40, int length2 = 10)
    {
        List<double> dcList = new();
        List<double> domCycList = new();
        List<double> realList = new();
        List<double> imagList = new();
        List<double> q1List = new();
        List<double> hpList = new();
        List<double> smoothHpList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var twoPiPer = MinOrMax(2 * Math.PI / length1, 0.99, 0.01);
        var alpha1 = (1 - Math.Sin(twoPiPer)) / Math.Cos(twoPiPer);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var delta = Math.Max((-0.015 * i) + 0.5, 0.15);
            var prevHp1 = i >= 1 ? hpList[i - 1] : 0;
            var prevHp2 = i >= 2 ? hpList[i - 2] : 0;
            var prevHp3 = i >= 3 ? hpList[i - 3] : 0;
            var prevHp4 = i >= 4 ? hpList[i - 4] : 0;
            var prevHp5 = i >= 5 ? hpList[i - 5] : 0;

            var hp = i < 7 ? currentValue : (0.5 * (1 + alpha1) * (currentValue - prevValue)) + (alpha1 * prevHp1);
            hpList.AddRounded(hp);

            var prevSmoothHp = smoothHpList.LastOrDefault();
            var smoothHp = i < 7 ? currentValue - prevValue : (hp + (2 * prevHp1) + (3 * prevHp2) + (3 * prevHp3) + (2 * prevHp4) + prevHp5) / 12;
            smoothHpList.AddRounded(smoothHp);

            double num = 0, denom = 0, dc = 0, real = 0, imag = 0, q1 = 0, maxAmpl = 0;
            for (var j = minLength; j <= maxLength; j++)
            {
                var beta = Math.Cos(MinOrMax(2 * Math.PI / j, 0.99, 0.01));
                var gamma = 1 / Math.Cos(MinOrMax(4 * Math.PI * delta / j, 0.99, 0.01));
                var alpha = gamma - Sqrt((gamma * gamma) - 1);
                var priorSmoothHp = i >= j ? smoothHpList[i - j] : 0;
                var prevReal = i >= j ? realList[i - j] : 0;
                var priorReal = i >= j * 2 ? realList[i - (j * 2)] : 0;
                var prevImag = i >= j ? imagList[i - j] : 0;
                var priorImag = i >= j * 2 ? imagList[i - (j * 2)] : 0;
                var prevQ1 = i >= j ? q1List[i - j] : 0;

                q1 = j / Math.PI * 2 * (smoothHp - prevSmoothHp);
                real = (0.5 * (1 - alpha) * (smoothHp - priorSmoothHp)) + (beta * (1 + alpha) * prevReal) - (alpha * priorReal);
                imag = (0.5 * (1 - alpha) * (q1 - prevQ1)) + (beta * (1 + alpha) * prevImag) - (alpha * priorImag);
                var ampl = (real * real) + (imag * imag);
                maxAmpl = ampl > maxAmpl ? ampl : maxAmpl;
                var db = maxAmpl != 0 && ampl / maxAmpl > 0 ? -length2 * Math.Log(0.01 / (1 - (0.99 * ampl / maxAmpl))) / Math.Log(length2) : 0;
                db = db > maxLength ? maxLength : db;
                num += db <= 3 ? j * (maxLength - db) : 0;
                denom += db <= 3 ? maxLength - db : 0;
                dc = denom != 0 ? num / denom : 0;
            }
            q1List.AddRounded(q1);
            realList.AddRounded(real);
            imagList.AddRounded(imag);
            dcList.AddRounded(dc);

            var domCyc = dcList.TakeLastExt(length2).Median();
            domCycList.AddRounded(domCyc);

            var signal = GetCompareSignal(smoothHp, prevSmoothHp);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Esdfb", domCycList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = domCycList;
        stockData.IndicatorName = IndicatorName.EhlersSpectrumDerivedFilterBank;

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
        List<double> v1List = new();
        List<double> v2List = new();
        List<double> hpList = new();
        List<double> smoothHpList = new();
        List<Signal> signalsList = new();
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
            hpList.AddRounded(hp);

            var prevSmoothHp = smoothHpList.LastOrDefault();
            var smoothHp = i < 7 ? currentValue - prevValue : (hp + (2 * prevHp1) + (3 * prevHp2) + (3 * prevHp3) + (2 * prevHp4) + prevHp5) / 12;
            smoothHpList.AddRounded(smoothHp);

            var prevV1 = i >= 1 ? v1List[i - 1] : 0;
            var prevV1_2 = i >= 2 ? v1List[i - 2] : 0;
            var v1 = (0.5 * (1 - alpha) * (smoothHp - prevSmoothHp)) + (beta * (1 + alpha) * prevV1) - (alpha * prevV1_2);
            v1List.AddRounded(v1);

            var v2 = domCyc / Math.PI * 2 * (v1 - prevV1);
            v2List.AddRounded(v2);

            var signal = GetConditionSignal(v2 > v1 && v2 >= 0, v2 < v1 || v2 < 0);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "V1", v1List },
            { "V2", v2List }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.EhlersDominantCycleTunedBypassFilter;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Restoring Pull Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="minLength"></param>
    /// <param name="maxLength"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersRestoringPullIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int minLength = 8, int maxLength = 50, int length1 = 40, int length2 = 10)
    {
        List<double> rpiList = new();
        List<Signal> signalsList = new();
        var (_, _, _, _, volumeList) = GetInputValuesList(stockData);

        var domCycList = CalculateEhlersSpectrumDerivedFilterBank(stockData, minLength, maxLength, length1, length2).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var domCyc = domCycList[i];
            var volume = volumeList[i];

            var rpi = volume * Pow(MinOrMax(2 * Math.PI / domCyc, 0.99, 0.01), 2);
            rpiList.AddRounded(rpi);
        }

        var rpiEmaList = GetMovingAverageList(stockData, maType, minLength, rpiList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var rpi = rpiList[i];
            var rpiEma = rpiEmaList[i];
            var prevRpi = i >= 1 ? rpiList[i - 1] : 0;
            var prevRpiEma = i >= 1 ? rpiEmaList[i - 1] : 0;

            var signal = GetCompareSignal(rpi - rpiEma, prevRpi - prevRpiEma, true);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Rpi", rpiList },
            { "Signal", rpiEmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = rpiList;
        stockData.IndicatorName = IndicatorName.EhlersRestoringPullIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Trendflex Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersTrendflexIndicator(this StockData stockData, int length = 20)
    {
        List<double> filterList = new();
        List<double> msList = new();
        List<double> trendflexList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var a1 = Exp(-1.414 * Math.PI / 0.5 * length);
        var b1 = 2 * a1 * Math.Cos(1.414 * Math.PI / 0.5 * length);
        var c2 = b1;
        var c3 = -a1 * a1;
        var c1 = 1 - c2 - c3;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevFilter1 = i >= 1 ? filterList[i - 1] : 0;
            var prevFilter2 = i >= 2 ? filterList[i - 2] : 0;

            var filter = (c1 * ((currentValue + prevValue) / 2)) + (c2 * prevFilter1) + (c3 * prevFilter2);
            filterList.AddRounded(filter);

            double sum = 0;
            for (var j = 1; j <= length; j++)
            {
                var prevFilterCount = i >= j ? filterList[i - j] : 0;
                sum += filter - prevFilterCount;
            }
            sum /= length;

            var prevMs = msList.LastOrDefault();
            var ms = (0.04 * Pow(sum, 2)) + (0.96 * prevMs);
            msList.AddRounded(ms);

            var prevTrendflex = trendflexList.LastOrDefault();
            var trendflex = ms > 0 ? sum / Sqrt(ms) : 0;
            trendflexList.AddRounded(trendflex);

            var signal = GetCompareSignal(trendflex, prevTrendflex);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Eti", trendflexList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = trendflexList;
        stockData.IndicatorName = IndicatorName.EhlersTrendflexIndicator;

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
        List<double> realList = new();
        List<double> imagList = new();
        List<Signal> signalsList = new();
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

            var prevReal = realList.LastOrDefault();
            var real = (length * sxx) - (sx * sx) > 0 && (length * syy) - (sy * sy) > 0 ? ((length * sxy) - (sx * sy)) /
                   Sqrt(((length * sxx) - (sx * sx)) * ((length * syy) - (sy * sy))) : 0;
            realList.AddRounded(real);

            var prevImag = imagList.LastOrDefault();
            var imag = (length * sxx) - (sx * sx) > 0 && (length * nsyy) - (nsy * nsy) > 0 ? ((length * nsxy) - (sx * nsy)) /
                   Sqrt(((length * sxx) - (sx * sx)) * ((length * nsyy) - (nsy * nsy))) : 0;
            imagList.AddRounded(imag);

            var signal = GetCompareSignal(real - imag, prevReal - prevImag);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Real", realList },
            { "Imag", imagList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
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
        List<double> angleList = new();
        List<Signal> signalsList = new();

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
            angleList.AddRounded(angle);

            var signal = GetCompareSignal(angle, prevAngle);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Cai", angleList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = angleList;
        stockData.IndicatorName = IndicatorName.EhlersCorrelationAngleIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Market State Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersMarketStateIndicator(this StockData stockData, int length = 20)
    {
        List<double> stateList = new();
        List<Signal> signalsList = new();

        var angleList = CalculateEhlersCorrelationAngleIndicator(stockData, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var angle = angleList[i];
            var prevAngle = i >= 1 ? angleList[i - 1] : 0;

            var prevState = stateList.LastOrDefault();
            double state = Math.Abs(angle - prevAngle) < 9 && angle < 0 ? -1 : Math.Abs(angle - prevAngle) < 9 && angle >= 0 ? 1 : 0;
            stateList.AddRounded(state);

            var signal = GetCompareSignal(state, prevState);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Emsi", stateList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = stateList;
        stockData.IndicatorName = IndicatorName.EhlersMarketStateIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Trend Extraction
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="delta"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersTrendExtraction(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 20, double delta = 0.1)
    {
        List<double> bpList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var beta = Math.Max(Math.Cos(2 * Math.PI / length), 0.99);
        var gamma = 1 / Math.Cos(4 * Math.PI * delta / length);
        var alpha = Math.Max(gamma - Sqrt((gamma * gamma) - 1), 0.99);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 2 ? inputList[i - 2] : 0;
            var prevBp1 = i >= 1 ? bpList[i - 1] : 0;
            var prevBp2 = i >= 2 ? bpList[i - 2] : 0;

            var bp = (0.5 * (1 - alpha) * MinPastValues(i, 2, currentValue - prevValue)) + (beta * (1 + alpha) * prevBp1) - (alpha * prevBp2);
            bpList.AddRounded(bp);
        }

        var trendList = GetMovingAverageList(stockData, maType, length * 2, bpList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var trend = trendList[i];
            var prevTrend = i >= 1 ? trendList[i - 1] : 0;

            var signal = GetCompareSignal(trend, prevTrend);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Trend", trendList },
            { "Bp", bpList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = trendList;
        stockData.IndicatorName = IndicatorName.EhlersTrendExtraction;

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
        List<double> peakList = new();
        List<double> valleyList = new();
        List<double> peakAvgFracList = new();
        List<double> valleyAvgFracList = new();
        List<Signal> signalsList = new();

        var eteList = CalculateEhlersTrendExtraction(stockData, maType, length1, delta);
        var trendList = eteList.OutputValues["Trend"];
        var bpList = eteList.OutputValues["Bp"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevBp1 = i >= 1 ? bpList[i - 1] : 0;
            var prevBp2 = i >= 2 ? bpList[i - 2] : 0;
            var bp = bpList[i];

            var prevPeak = peakList.LastOrDefault();
            var peak = prevBp1 > bp && prevBp1 > prevBp2 ? prevBp1 : prevPeak;
            peakList.AddRounded(peak);

            var prevValley = valleyList.LastOrDefault();
            var valley = prevBp1 < bp && prevBp1 < prevBp2 ? prevBp1 : prevValley;
            valleyList.AddRounded(valley);
        }

        var peakAvgList = GetMovingAverageList(stockData, maType, length2, peakList);
        var valleyAvgList = GetMovingAverageList(stockData, maType, length2, valleyList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var peakAvg = peakAvgList[i];
            var valleyAvg = valleyAvgList[i];
            var trend = trendList[i];
            var prevTrend = i >= 1 ? trendList[i - 1] : 0;

            var prevPeakAvgFrac = peakAvgFracList.LastOrDefault();
            var peakAvgFrac = fraction * peakAvg;
            peakAvgFracList.AddRounded(peakAvgFrac);

            var prevValleyAvgFrac = valleyAvgFracList.LastOrDefault();
            var valleyAvgFrac = fraction * valleyAvg;
            valleyAvgFracList.AddRounded(valleyAvgFrac);

            var signal = GetBullishBearishSignal(trend - Math.Max(peakAvgFrac, valleyAvgFrac), prevTrend - Math.Max(prevPeakAvgFrac, prevValleyAvgFrac),
                trend - Math.Min(peakAvgFrac, valleyAvgFrac), prevTrend - Math.Min(prevPeakAvgFrac, prevValleyAvgFrac));
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Trend", trendList },
            { "Peak", peakAvgFracList },
            { "Valley", valleyAvgFracList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
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
        List<double> peakList = new();
        List<double> quotientList = new();
        List<Signal> signalsList = new();

        var hpList = CalculateEhlersHighPassFilterV1(stockData, length2, 1).CustomValuesList;
        stockData.CustomValuesList = hpList;
        var superSmoothList = CalculateEhlersSuperSmootherFilter(stockData, length1).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var filter = superSmoothList[i];

            var prevPeak = peakList.LastOrDefault();
            var peak = Math.Abs(filter) > 0.991 * prevPeak ? Math.Abs(filter) : 0.991 * prevPeak;
            peakList.AddRounded(peak);

            var ratio = peak != 0 ? filter / peak : 0;
            var prevQuotient = quotientList.LastOrDefault();
            var quotient = (k * ratio) + 1 != 0 ? (ratio + k) / ((k * ratio) + 1) : 0;
            quotientList.AddRounded(quotient);

            var signal = GetCompareSignal(quotient, prevQuotient);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Eoti", quotientList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = quotientList;
        stockData.IndicatorName = IndicatorName.EhlersEarlyOnsetTrendIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Roofing Filter V1
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersRoofingFilterV1(this StockData stockData, MovingAvgType maType = MovingAvgType.Ehlers2PoleSuperSmootherFilterV1, 
        int length1 = 48, int length2 = 10)
    {
        List<double> argList = new();
        List<Signal> signalsList = new();

        var hpFilterList = CalculateEhlersHighPassFilterV1(stockData, length1, 1).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var highPass = hpFilterList[i];
            var prevHp1 = i >= 1 ? hpFilterList[i - 1] : 0;

            var arg = (highPass + prevHp1) / 2;
            argList.AddRounded(arg);
        }

        var roofingFilter2PoleList = GetMovingAverageList(stockData, maType, length2, argList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var roofingFilter = roofingFilter2PoleList[i];
            var prevRoofingFilter = i >= 1 ? roofingFilter2PoleList[i - 1] : 0;

            var signal = GetCompareSignal(roofingFilter, prevRoofingFilter);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Erf", roofingFilter2PoleList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = roofingFilter2PoleList;
        stockData.IndicatorName = IndicatorName.EhlersRoofingFilterV1;

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
    public static StockData CalculateEhlersSnakeUniversalTradingFilter(this StockData stockData, MovingAvgType maType = MovingAvgType.EhlersHannMovingAverage,
        int length1 = 23, int length2 = 50, double bw = 1.4)
    {
        List<double> bpList = new();
        List<double> negRmsList = new();
        List<double> filtPowList = new();
        List<double> rmsList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var l1 = Math.Cos(MinOrMax(2 * Math.PI / 2 * length1, 0.99, 0.01));
        var g1 = Math.Cos(MinOrMax(bw * 2 * Math.PI / 2 * length1, 0.99, 0.01));
        var s1 = (1 / g1) - Sqrt(1 / Pow(g1, 2) - 1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 2 ? inputList[i - 2] : 0;
            var prevBp1 = i >= 1 ? bpList[i - 1] : 0;
            var prevBp2 = i >= 2 ? bpList[i - 2] : 0;

            var bp = i < 3 ? 0 : (0.5 * (1 - s1) * (currentValue - prevValue)) + (l1 * (1 + s1) * prevBp1) - (s1 * prevBp2);
            bpList.AddRounded(bp);
        }

        var filtList = GetMovingAverageList(stockData, maType, length1, bpList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var filt = filtList[i];
            var prevFilt1 = i >= 1 ? filtList[i - 1] : 0;
            var prevFilt2 = i >= 2 ? filtList[i - 2] : 0;

            var filtPow = Pow(filt, 2);
            filtPowList.AddRounded(filtPow);

            var filtPowMa = filtPowList.TakeLastExt(length2).Average();
            var rms = Sqrt(filtPowMa);
            rmsList.AddRounded(rms);

            var negRms = -rms;
            negRmsList.AddRounded(negRms);

            var signal = GetCompareSignal(filt - prevFilt1, prevFilt1 - prevFilt2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", rmsList },
            { "Erf", filtList },
            { "LowerBand", negRmsList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = filtList;
        stockData.IndicatorName = IndicatorName.EhlersSnakeUniversalTradingFilter;

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
        List<double> bpList = new();
        List<Signal> signalsList = new();
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
            bpList.AddRounded(bp);
        }

        var filtList = GetMovingAverageList(stockData, maType, hannLength, bpList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var filt = filtList[i];
            var prevFilt1 = i >= 1 ? filtList[i - 1] : 0;
            var prevFilt2 = i >= 2 ? filtList[i - 2] : 0;

            var signal = GetCompareSignal(filt - prevFilt1, prevFilt1 - prevFilt2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Eir", filtList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = filtList;
        stockData.IndicatorName = IndicatorName.EhlersImpulseResponse;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Mesa Predict Indicator V1
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="lowerLength"></param>
    /// <param name="upperLength"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersMesaPredictIndicatorV1(this StockData stockData, int length1 = 5, int length2 = 4, int length3 = 10, 
        int lowerLength = 12, int upperLength = 54)
    {
        List<double> ssfList = new();
        List<double> hpList = new();
        List<double> prePredictList = new();
        List<double> predictList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var a1 = Exp(MinOrMax(-1.414 * Math.PI / upperLength, -0.01, -0.99));
        var b1 = 2 * a1 * Math.Cos(MinOrMax(1.414 * Math.PI / upperLength, 0.99, 0.01));
        var c2 = b1;
        var c3 = -a1 * a1;
        var c1 = (1 + c2 - c3) / 4;
        var a = Exp(MinOrMax(-1.414 * Math.PI / lowerLength, -0.01, -0.99));
        var b = 2 * a * Math.Cos(MinOrMax(1.414 * Math.PI / lowerLength, 0.99, 0.01));
        var coef2 = b;
        var coef3 = -a * a;
        var coef1 = 1 - coef2 - coef3;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue1 = i >= 1 ? inputList[i - 1] : 0;
            var prevValue2 = i >= 2 ? inputList[i - 2] : 0;
            var prevHp1 = i >= 1 ? hpList[i - 1] : 0;
            var prevHp2 = i >= 2 ? hpList[i - 2] : 0;
            var prevSsf1 = i >= 1 ? ssfList[i - 1] : 0;
            var prevSsf2 = i >= 2 ? ssfList[i - 2] : 0;
            var prevPredict1 = i >= 1 ? predictList[i - 1] : 0;
            var priorSsf = i >= upperLength - 1 ? ssfList[i - (upperLength - 1)] : 0;
            var pArray = new double[500];
            var bb1Array = new double[500];
            var bb2Array = new double[500];
            var coefArray = new double[500];
            var coefAArray = new double[500];
            var xxArray = new double[520];
            var hCoefArray = new double[520];

            var hp = i < 4 ? 0 : (c1 * (currentValue - (2 * prevValue1) + prevValue2)) + (c2 * prevHp1) + (c3 * prevHp2);
            hpList.AddRounded(hp);

            var ssf = i < 3 ? hp : (coef1 * ((hp + prevHp1) / 2)) + (coef2 * prevSsf1) + (coef3 * prevSsf2);
            ssfList.AddRounded(ssf);

            double pwrSum = 0;
            for (var j = 0; j < upperLength; j++)
            {
                var prevSsf = i >= j ? ssfList[i - j] : 0;
                pwrSum += Pow(prevSsf, 2);
            }

            var pwr = pwrSum / upperLength;
            bb1Array[1] = ssf;
            bb2Array[upperLength - 1] = priorSsf;
            for (var j = 2; j < upperLength; j++)
            {
                var prevSsf = i >= j - 1 ? ssfList[i - (j - 1)] : 0;
                bb1Array[j] = prevSsf;
                bb2Array[j - 1] = prevSsf;
            }

            double num = 0, denom = 0;
            for (var j = 1; j < upperLength; j++)
            {
                num += bb1Array[j] * bb2Array[j];
                denom += Pow(bb1Array[j], 2) + Pow(bb2Array[j], 2);
            }

            var coef = denom != 0 ? 2 * num / denom : 0;
            var p = pwr * (1 - Pow(coef, 2));
            coefArray[1] = coef;
            pArray[1] = p;
            for (var j = 2; j <= length2; j++)
            {
                for (var k = 1; k < j; k++)
                {
                    coefAArray[k] = coefArray[k];
                }

                for (var k = 1; k < upperLength; k++)
                {
                    bb1Array[k] = bb1Array[k] - (coefAArray[j - 1] * bb2Array[k]);
                    bb2Array[k] = bb2Array[k + 1] - (coefAArray[j - 1] * bb1Array[k + 1]);
                }

                double num1 = 0, denom1 = 0;
                for (var k = 1; k <= upperLength - j; k++)
                {
                    num1 += bb1Array[k] * bb2Array[k];
                    denom1 += Pow(bb1Array[k], 2) + Pow(bb2Array[k], 2);
                }

                coefArray[j] = denom1 != 0 ? 2 * num1 / denom1 : 0;
                pArray[j] = pArray[j - 1] * (1 - Pow(coefArray[j], 2));
                for (var k = 1; k < j; k++)
                {
                    coefArray[k] = coefAArray[k] - (coefArray[j] * coefAArray[j - k]);
                }
            }

            var coef1Array = new double[500];
            for (var j = 1; j <= length2; j++)
            {
                coef1Array[1] = coefArray[j];
                for (var k = lowerLength; k >= 2; k--)
                {
                    coef1Array[k] = coef1Array[k - 1];
                }
            }

            for (var j = 1; j <= length2; j++)
            {
                hCoefArray[j] = 0;
                double cc = 0;
                for (var k = 1; k <= lowerLength; k++)
                {
                    hCoefArray[j] = hCoefArray[j] + ((1 - Math.Cos(MinOrMax(2 * Math.PI * ((double)k / (lowerLength + 1)), 0.99, 0.01))) * coef1Array[k]);
                    cc += 1 - Math.Cos(MinOrMax(2 * Math.PI * ((double)k / (lowerLength + 1)), 0.99, 0.01));
                }
                hCoefArray[j] = cc != 0 ? hCoefArray[j] / cc : 0;
            }

            for (var j = 1; j <= upperLength; j++)
            {
                xxArray[j] = i >= upperLength - j ? ssfList[i - (upperLength - j)] : 0;
            }

            for (var j = 1; j <= length3; j++)
            {
                xxArray[upperLength + j] = 0;
                for (var k = 1; k <= length2; k++)
                {
                    xxArray[upperLength + j] = xxArray[upperLength + j] + (hCoefArray[k] * xxArray[upperLength + j - k]);
                }
            }

            var prevPrePredict = prePredictList.LastOrDefault();
            var prePredict = xxArray[upperLength + length1];
            prePredictList.AddRounded(prePredict);

            var predict = (prePredict + prevPrePredict) / 2;
            predictList.AddRounded(predict);

            var signal = GetCompareSignal(ssf - predict, prevSsf1 - prevPredict1);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ssf", ssfList },
            { "Predict", predictList },
            { "PrePredict", prePredictList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = predictList;
        stockData.IndicatorName = IndicatorName.EhlersMesaPredictIndicatorV1;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Mesa Predict Indicator V2
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="length4"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersMesaPredictIndicatorV2(this StockData stockData, MovingAvgType maType = MovingAvgType.EhlersHannMovingAverage,
        int length1 = 5, int length2 = 135, int length3 = 12, int length4 = 4)
    {
        List<double> ssfList = new();
        List<double> hpList = new();
        List<double> predictList = new();
        List<double> extrapList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var coefArray = new double[5];
        coefArray[0] = 4.525;
        coefArray[1] = -8.45;
        coefArray[2] = 8.145;
        coefArray[3] = -4.045;
        coefArray[4] = 0.825;

        var a1 = Exp(MinOrMax(-1.414 * Math.PI / length2, -0.01, -0.99));
        var b1 = 2 * a1 * Math.Cos(MinOrMax(1.414 * Math.PI / length2, 0.99, 0.01));
        var c2 = b1;
        var c3 = -a1 * a1;
        var c1 = (1 + c2 - c3) / 4;
        var a = Exp(MinOrMax(-1.414 * Math.PI / length3, -0.01, -0.99));
        var b = 2 * a * Math.Cos(MinOrMax(1.414 * Math.PI / length3, 0.99, 0.01));
        var coef2 = b;
        var coef3 = -a * a;
        var coef1 = 1 - coef2 - coef3;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue1 = i >= 1 ? inputList[i - 1] : 0;
            var prevValue2 = i >= 2 ? inputList[i - 2] : 0;
            var prevHp1 = i >= 1 ? hpList[i - 1] : 0;
            var prevHp2 = i >= 2 ? hpList[i - 2] : 0;
            var prevSsf1 = i >= 1 ? ssfList[i - 1] : 0;
            var prevSsf2 = i >= 2 ? ssfList[i - 2] : 0;

            var hp = i < 4 ? 0 : (c1 * (currentValue - (2 * prevValue1) + prevValue2)) + (c2 * prevHp1) + (c3 * prevHp2);
            hpList.AddRounded(hp);

            var ssf = i < 3 ? hp : (coef1 * ((hp + prevHp1) / 2)) + (coef2 * prevSsf1) + (coef3 * prevSsf2);
            ssfList.AddRounded(ssf);
        }

        var filtList = GetMovingAverageList(stockData, maType, length3, ssfList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var prevPredict1 = i >= 1 ? predictList[i - 1] : 0;
            var prevPredict2 = i >= 2 ? predictList[i - 2] : 0;

            var xxArray = new double[100];
            var yyArray = new double[100];
            for (var j = 1; j <= length1; j++)
            {
                var prevFilt = i >= length1 - j ? filtList[i - (length1 - j)] : 0;
                xxArray[j] = prevFilt;
                yyArray[j] = prevFilt;
            }

            for (var j = 1; j <= length1; j++)
            {
                xxArray[length1 + j] = 0;
                for (var k = 1; k <= 5; k++)
                {
                    xxArray[length1 + j] = xxArray[length1 + j] + (coefArray[k - 1] * xxArray[length1 + j - (k - 1)]);
                }
            }

            for (var j = 0; j <= length1; j++)
            {
                yyArray[length1 + j + 1] = (2 * yyArray[length1 + j]) - yyArray[length1 + j - 1];
            }

            var predict = xxArray[length1 + length4];
            predictList.AddRounded(predict);

            var extrap = yyArray[length1 + length4];
            extrapList.AddRounded(extrap);

            var signal = GetCompareSignal(predict - prevPredict1, prevPredict1 - prevPredict2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ssf", filtList },
            { "Predict", predictList },
            { "Extrap", extrapList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = predictList;
        stockData.IndicatorName = IndicatorName.EhlersMesaPredictIndicatorV2;

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
        List<double> predictList = new();
        List<Signal> signalsList = new();

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
                start = corr > maxCorr ? length - j : 0;
                maxCorr = corr > maxCorr ? corr : maxCorr;
            }

            var prevPredict = predictList.LastOrDefault();
            var predict = Math.Sin(MinOrMax(2 * Math.PI * start / length, 0.99, 0.01));
            predictList.AddRounded(predict);

            var signal = GetCompareSignal(predict, prevPredict);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Predict", predictList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = predictList;
        stockData.IndicatorName = IndicatorName.EhlersAnticipateIndicator;

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
        List<double> reactionList = new();
        List<double> ireactList = new();
        List<Signal> signalsList = new();
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
            reactionList.AddRounded(reaction);

            var ireact = currentValue != 0 ? 100 * reaction / currentValue : 0;
            ireactList.AddRounded(ireact);

            var signal = GetCompareSignal(ireact - prevIReact1, prevIReact1 - prevIReact2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Eir", ireactList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = ireactList;
        stockData.IndicatorName = IndicatorName.EhlersImpulseReaction;

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
    public static StockData CalculateEhlersUniversalTradingFilter(this StockData stockData, MovingAvgType maType = MovingAvgType.EhlersHannMovingAverage,
        int length1 = 16, int length2 = 50, double mult = 2)
    {
        List<double> momList = new();
        List<double> negRmsList = new();
        List<double> filtPowList = new();
        List<double> rmsList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var hannLength = (int)Math.Ceiling(mult * length1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var priorValue = i >= hannLength ? inputList[i - hannLength] : 0;

            var mom = currentValue - priorValue;
            momList.AddRounded(mom);
        }

        var filtList = GetMovingAverageList(stockData, maType, length1, momList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var filt = filtList[i];
            var prevFilt1 = i >= 1 ? filtList[i - 1] : 0;
            var prevFilt2 = i >= 2 ? filtList[i - 2] : 0;

            var filtPow = Pow(filt, 2);
            filtPowList.AddRounded(filtPow);

            var filtPowMa = filtPowList.TakeLastExt(length2).Average();
            var rms = filtPowMa > 0 ? Sqrt(filtPowMa) : 0;
            rmsList.AddRounded(rms);

            var negRms = -rms;
            negRmsList.AddRounded(negRms);

            var signal = GetCompareSignal(filt - prevFilt1, prevFilt1 - prevFilt2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Eutf", filtList },
            { "UpperBand", rmsList },
            { "LowerBand", negRmsList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = filtList;
        stockData.IndicatorName = IndicatorName.EhlersUniversalTradingFilter;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Recursive Median Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersRecursiveMedianOscillator(this StockData stockData, int length1 = 5, int length2 = 12, int length3 = 30)
    {
        List<double> rmList = new();
        List<double> tempList = new();
        List<double> rmoList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var alpha1Arg = MinOrMax(2 * Math.PI / length2, 0.99, 0.01);
        var alpha1ArgCos = Math.Cos(alpha1Arg);
        var alpha2Arg = MinOrMax(1 / Sqrt(2) * 2 * Math.PI / length3, 0.99, 0.01);
        var alpha2ArgCos = Math.Cos(alpha2Arg);
        var alpha1 = alpha1ArgCos != 0 ? (alpha1ArgCos + Math.Sin(alpha1Arg) - 1) / alpha1ArgCos : 0;
        var alpha2 = alpha2ArgCos != 0 ? (alpha2ArgCos + Math.Sin(alpha2Arg) - 1) / alpha2ArgCos : 0;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            tempList.AddRounded(currentValue);

            var median = tempList.TakeLastExt(length1).Median();
            var prevRm1 = i >= 1 ? rmList[i - 1] : 0;
            var prevRm2 = i >= 2 ? rmList[i - 2] : 0;
            var prevRmo1 = i >= 1 ? rmoList[i - 1] : 0;
            var prevRmo2 = i >= 2 ? rmoList[i - 2] : 0;

            var rm = (alpha1 * median) + ((1 - alpha1) * prevRm1);
            rmList.AddRounded(rm);

            var rmo = (Pow(1 - (alpha2 / 2), 2) * (rm - (2 * prevRm1) + prevRm2)) + (2 * (1 - alpha2) * prevRmo1) - (Pow(1 - alpha2, 2) * prevRmo2);
            rmoList.AddRounded(rmo);

            var signal = GetCompareSignal(rmo, prevRmo1);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ermo", rmoList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = rmoList;
        stockData.IndicatorName = IndicatorName.EhlersRecursiveMedianOscillator;

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
    public static StockData CalculateEhlersSuperPassbandFilter(this StockData stockData, int fastLength = 40, int slowLength = 60, int length1 = 5, int length2 = 50)
    {
        List<double> espfList = new();
        List<double> squareList = new();
        List<double> rmsList = new();
        List<double> negRmsList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var a1 = MinOrMax((double)length1 / fastLength, 0.99, 0.01);
        var a2 = MinOrMax((double)length1 / slowLength, 0.99, 0.01);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue1 = i >= 1 ? inputList[i - 1] : 0;
            var prevEspf1 = i >= 1 ? espfList[i - 1] : 0;
            var prevEspf2 = i >= 2 ? espfList[i - 2] : 0;

            var espf = ((a1 - a2) * currentValue) + (((a2 * (1 - a1)) - (a1 * (1 - a2))) * prevValue1) + ((1 - a1 + (1 - a2)) * prevEspf1) - 
                       ((1 - a1) * (1 - a2) * prevEspf2);
            espfList.AddRounded(espf);

            var espfPow = Pow(espf, 2);
            squareList.AddRounded(espfPow);

            var squareAvg = squareList.TakeLastExt(length2).Average();
            var prevRms = rmsList.LastOrDefault();
            var rms = Sqrt(squareAvg);
            rmsList.AddRounded(rms);

            var prevNegRms = negRmsList.LastOrDefault();
            var negRms = -rms;
            negRmsList.AddRounded(negRms);

            var signal = GetBullishBearishSignal(espf - rms, prevEspf1 - prevRms, espf - negRms, prevEspf1 - prevNegRms);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Espf", espfList },
            { "UpperBand", rmsList },
            { "LowerBand", negRmsList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = espfList;
        stockData.IndicatorName = IndicatorName.EhlersSuperPassbandFilter;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Simple Deriv Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersSimpleDerivIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length = 2, int signalLength = 8)
    {
        List<double> derivList = new();
        List<double> z3List = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= length ? inputList[i - length] : 0;
            var prevDeriv1 = i >= 1 ? derivList[i - 1] : 0;
            var prevDeriv2 = i >= 2 ? derivList[i - 2] : 0;
            var prevDeriv3 = i >= 3 ? derivList[i - 3] : 0;

            var deriv = MinPastValues(i, length, currentValue - prevValue);
            derivList.AddRounded(deriv);

            var z3 = deriv + prevDeriv1 + prevDeriv2 + prevDeriv3;
            z3List.AddRounded(z3);
        }

        var z3EmaList = GetMovingAverageList(stockData, maType, signalLength, z3List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var z3Ema = z3EmaList[i];
            var prevZ3Ema = i >= 1 ? z3EmaList[i - 1] : 0;

            var signal = GetCompareSignal(z3Ema, prevZ3Ema);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Esdi", z3List },
            { "Signal", z3EmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = z3List;
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
    public static StockData CalculateEhlersSimpleClipIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length1 = 2, int length2 = 10, int length3 = 50, int signalLength = 22)
    {
        List<double> derivList = new();
        List<double> clipList = new();
        List<double> z3List = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= length1 ? inputList[i - length1] : 0;
            var prevClip1 = i >= 1 ? clipList[i - 1] : 0;
            var prevClip2 = i >= 2 ? clipList[i - 2] : 0;
            var prevClip3 = i >= 3 ? clipList[i - 3] : 0;

            var deriv = MinPastValues(i, length1, currentValue - prevValue);
            derivList.AddRounded(deriv);

            double rms = 0;
            for (var j = 0; j < length3; j++)
            {
                var prevDeriv = i >= j ? derivList[i - j] : 0;
                rms += Pow(prevDeriv, 2);
            }

            var clip = rms != 0 ? MinOrMax(2 * deriv / Sqrt(rms / length3), 1, -1) : 0;
            clipList.AddRounded(clip);

            var z3 = clip + prevClip1 + prevClip2 + prevClip3;
            z3List.AddRounded(z3);
        }

        var z3EmaList = GetMovingAverageList(stockData, maType, signalLength, z3List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var z3Ema = z3EmaList[i];
            var prevZ3Ema = i >= 1 ? z3EmaList[i - 1] : 0;

            var signal = GetCompareSignal(z3Ema, prevZ3Ema);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Esci", z3List },
            { "Signal", z3EmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = z3List;
        stockData.IndicatorName = IndicatorName.EhlersSimpleClipIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Spearman Rank Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersSpearmanRankIndicator(this StockData stockData, int length = 20)
    {
        List<double> sriList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var priceArray = new double[50];
            var rankArray = new double[50];
            for (var j = 1; j <= length; j++)
            {
                var prevPrice = i >= j - 1 ? inputList[i - (j - 1)] : 0;
                priceArray[j] = prevPrice;
                rankArray[j] = j;
            }

            for (var j = 1; j <= length; j++)
            {
                var count = length + 1 - j;

                for (var k = 1; k <= length - count; k++)
                {
                    var array1 = priceArray[k + 1];

                    if (array1 < priceArray[k])
                    {
                        var tempPrice = priceArray[k];
                        var tempRank = rankArray[k];

                        priceArray[k] = array1;
                        rankArray[k] = rankArray[k + 1];
                        priceArray[k + 1] = tempPrice;
                        rankArray[k + 1] = tempRank;
                    }
                }
            }

            double sum = 0;
            for (var j = 1; j <= length; j++)
            {
                sum += Pow(j - rankArray[j], 2);
            }

            var prevSri = sriList.LastOrDefault();
            var sri = 2 * (0.5 - (1 - (6 * sum / (length * (Pow(length, 2) - 1)))));
            sriList.AddRounded(sri);

            var signal = GetCompareSignal(sri, prevSri);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Esri", sriList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = sriList;
        stockData.IndicatorName = IndicatorName.EhlersSpearmanRankIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Noise Elimination Technology
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersNoiseEliminationTechnology(this StockData stockData, int length = 14)
    {
        List<double> netList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var denom = 0.5 * length * (length - 1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var xArray = new double[50];
            for (var j = 1; j <= length; j++)
            {
                var prevPrice = i >= j - 1 ? inputList[i - (j - 1)] : 0;
                xArray[j] = prevPrice;
            }

            double num = 0;
            for (var j = 2; j <= length; j++)
            {
                for (var k = 1; k <= j - 1; k++)
                {
                    num -= Math.Sign(xArray[j] - xArray[k]);
                }
            }

            var prevNet = netList.LastOrDefault();
            var net = denom != 0 ? num / denom : 0;
            netList.AddRounded(net);

            var signal = GetCompareSignal(net, prevNet);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Enet", netList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = netList;
        stockData.IndicatorName = IndicatorName.EhlersNoiseEliminationTechnology;

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
    public static StockData CalculateEhlersTruncatedBandPassFilter(this StockData stockData, int length1 = 20, int length2 = 10, double bw = 0.1)
    {
        List<double> bptList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var l1 = Math.Cos(MinOrMax(2 * Math.PI / length1, 0.99, 0.01));
        var g1 = Math.Cos(bw * 2 * Math.PI / length1);
        var s1 = (1 / g1) - Sqrt((1 / Pow(g1, 2)) - 1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var trunArray = new double[100];
            for (var j = length2; j > 0; j--)
            {
                var prevValue1 = i >= j - 1 ? inputList[i - (j - 1)] : 0;
                var prevValue2 = i >= j + 1 ? inputList[i - (j + 1)] : 0;
                trunArray[j] = (0.5 * (1 - s1) * (prevValue1 - prevValue2)) + (l1 * (1 + s1) * trunArray[j + 1]) - (s1 * trunArray[j + 2]);
            }

            var prevBpt = bptList.LastOrDefault();
            var bpt = trunArray[1];
            bptList.AddRounded(bpt);

            var signal = GetCompareSignal(bpt, prevBpt);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Etbpf", bptList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = bptList;
        stockData.IndicatorName = IndicatorName.EhlersTruncatedBandPassFilter;

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
        List<double> corrList = new();
        List<double> xList = new();
        List<double> yList = new();
        List<double> xxList = new();
        List<double> yyList = new();
        List<double> xyList = new();
        List<Signal> signalsList = new();

        var roofingFilterList = CalculateEhlersRoofingFilterV2(stockData, length1, length2).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevCorr1 = i >= 1 ? corrList[i - 1] : 0;
            var prevCorr2 = i >= 2 ? corrList[i - 2] : 0;

            var x = roofingFilterList[i];
            xList.AddRounded(x);

            var y = i >= length1 ? roofingFilterList[i - length1] : 0;
            yList.AddRounded(y);

            var xx = Pow(x, 2);
            xxList.AddRounded(xx);

            var yy = Pow(y, 2);
            yyList.AddRounded(yy);

            var xy = x * y;
            xyList.AddRounded(xy);

            var sx = xList.TakeLastExt(length1).Sum();
            var sy = yList.TakeLastExt(length1).Sum();
            var sxx = xxList.TakeLastExt(length1).Sum();
            var syy = yyList.TakeLastExt(length1).Sum();
            var sxy = xyList.TakeLastExt(length1).Sum();

            var corr = ((i * sxx) - (sx * sx)) * ((i * syy) - (sy * sy)) > 0 ? 0.5 * ((((i * sxy) - (sx * sy)) / 
                Sqrt(((i * sxx) - (sx * sx)) * ((i * syy) - (sy * sy)))) + 1) : 0;
            corrList.AddRounded(corr);

            var signal = GetCompareSignal(corr - prevCorr1, prevCorr1 - prevCorr2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Eaci", corrList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = corrList;
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
        List<double> domCycList = new();
        List<double> rList = new();
        List<Signal> signalsList = new();

        var corrList = CalculateEhlersAutoCorrelationIndicator(stockData, length1, length2).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var corr = corrList[i];
            var prevCorr1 = i >= 1 ? corrList[i - 1] : 0;
            var prevCorr2 = i >= 2 ? corrList[i - 2] : 0;

            double maxPwr = 0, spx = 0, sp = 0;
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
                var prevR = i >= j - 1 ? rList[i - (j - 1)] : 0;
                var r = (0.2 * Pow(sqSum, 2)) + (0.8 * prevR);
                maxPwr = Math.Max(r, maxPwr);
                var pwr = maxPwr != 0 ? r / maxPwr : 0;

                if (pwr >= 0.5)
                {
                    spx += j * pwr;
                    sp += pwr;
                }
            }

            var domCyc = sp != 0 ? spx / sp : 0;
            domCycList.AddRounded(domCyc);

            var signal = GetCompareSignal(corr - prevCorr1, prevCorr1 - prevCorr2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Eacp", domCycList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = domCycList;
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
        List<double> upChgList = new();
        List<double> denomList = new();
        List<double> arsiList = new();
        List<Signal> signalsList = new();

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

            var prevUpChg = upChgList.LastOrDefault();
            double upChg = 0, dnChg = 0;
            for (var j = 0; j < (int)Math.Ceiling(domCyc / 2); j++)
            {
                var filt = i >= j ? roofingFilterList[i - j] : 0;
                var prevFilt = i >= j + 1 ? roofingFilterList[i - (j + 1)] : 0;
                upChg += filt > prevFilt ? filt - prevFilt : 0;
                dnChg += filt < prevFilt ? prevFilt - filt : 0;
            }
            upChgList.AddRounded(upChg);

            var prevDenom = denomList.LastOrDefault();
            var denom = upChg + dnChg;
            denomList.AddRounded(denom);

            var arsi = denom != 0 && prevDenom != 0 ? (c1 * ((upChg / denom) + (prevUpChg / prevDenom)) / 2) + (c2 * prevArsi1) + (c3 * prevArsi2) : 0;
            arsiList.AddRounded(arsi);
        }

        var arsiEmaList = GetMovingAverageList(stockData, maType, length2, arsiList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var arsi = arsiList[i];
            var arsiEma = arsiEmaList[i];
            var prevArsi = i >= 1 ? arsiList[i - 1] : 0;
            var prevArsiEma = i >= 1 ? arsiEmaList[i - 1] : 0;

            var signal = GetRsiSignal(arsi - arsiEma, prevArsi - prevArsiEma, arsi, prevArsi, 0.7, 0.3);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Earsi", arsiList },
            { "Signal", arsiEmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = arsiList;
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
        List<double> fishList = new();
        List<Signal> signalsList = new();

        var arsiList = CalculateEhlersAdaptiveRelativeStrengthIndexV2(stockData, maType, length1, length2, length3).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var arsi = arsiList[i] / 100;
            var prevFish1 = i >= 1 ? fishList[i - 1] : 0;
            var prevFish2 = i >= 2 ? fishList[i - 2] : 0;
            var tranRsi = 2 * (arsi - 0.5);
            var ampRsi = MinOrMax(1.5 * tranRsi, 0.999, -0.999);

            var fish = 0.5 * Math.Log((1 + ampRsi) / (1 - ampRsi));
            fishList.AddRounded(fish);

            var signal = GetRsiSignal(fish - prevFish1, prevFish1 - prevFish2, fish, prevFish1, 2, -2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Earsift", fishList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = fishList;
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
        List<double> stocList = new();
        List<double> astocList = new();
        List<Signal> signalsList = new();

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

            var prevStoc = stocList.LastOrDefault();
            var stoc = highest != lowest ? (roofingFilter - lowest) / (highest - lowest) : 0;
            stocList.AddRounded(stoc);

            var astoc = (c1 * ((stoc + prevStoc) / 2)) + (c2 * prevAstoc1) + (c3 * prevAstoc2);
            astocList.AddRounded(astoc);
        }

        var astocEmaList = GetMovingAverageList(stockData, maType, length2, astocList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var astoc = astocList[i];
            var astocEma = astocEmaList[i];
            var prevAstoc = i >= 1 ? astocList[i - 1] : 0;
            var prevAstocEma = i >= 1 ? astocEmaList[i - 1] : 0;

            var signal = GetRsiSignal(astoc - astocEma, prevAstoc - prevAstocEma, astoc, prevAstoc, 0.7, 0.3);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Easi", astocList },
            { "Signal", astocEmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = astocList;
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
        List<double> fishList = new();
        List<double> triggerList = new();
        List<Signal> signalsList = new();

        var astocList = CalculateEhlersAdaptiveStochasticIndicatorV2(stockData, maType, length1, length2, length3).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var astoc = astocList[i];
            var v1 = 2 * (astoc - 0.5);

            var prevFish = fishList.LastOrDefault();
            var fish = (Exp(6 * v1) - 1) / (Exp(6 * v1) + 1);
            fishList.AddRounded(fish);

            var prevTrigger = triggerList.LastOrDefault();
            var trigger = 0.9 * prevFish;
            triggerList.AddRounded(trigger);

            var signal = GetCompareSignal(fish - trigger, prevFish - prevTrigger);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Easift", fishList },
            { "Signal", triggerList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = fishList;
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
        List<double> acciList = new();
        List<double> tempList = new();
        List<double> mdList = new();
        List<double> ratioList = new();
        List<Signal> signalsList = new();

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
            tempList.AddRounded(roofingFilter);

            var avg = tempList.TakeLastExt(cycLength).Average();
            var md = Pow(roofingFilter - avg, 2);
            mdList.AddRounded(md);

            var mdAvg = mdList.TakeLastExt(cycLength).Average();
            var rms = cycLength >= 0 ? Sqrt(mdAvg) : 0;
            var num = roofingFilter - avg;
            var denom = 0.015 * rms;

            var prevRatio = ratioList.LastOrDefault();
            var ratio = denom != 0 ? num / denom : 0;
            ratioList.AddRounded(ratio);

            var acci = (c1 * ((ratio + prevRatio) / 2)) + (c2 * prevAcci1) + (c3 * prevAcci2);
            acciList.AddRounded(acci);
        }

        var acciEmaList = GetMovingAverageList(stockData, maType, length2, acciList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var acci = acciList[i];
            var acciEma = acciEmaList[i];
            var prevAcci = i >= 1 ? acciList[i - 1] : 0;
            var prevAcciEma = i >= 1 ? acciEmaList[i - 1] : 0;

            var signal = GetRsiSignal(acci - acciEma, prevAcci - prevAcciEma, acci, prevAcci, 100, -100);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Eacci", acciList },
            { "Signal", acciEmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = acciList;
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
        List<double> rList = new();
        List<double> domCycList = new();
        List<Signal> signalsList = new();

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
                var prevR = i >= j - 1 ? rList[i - (j - 1)] : 0;
                var r = (0.2 * Pow(sqSum, 2)) + (0.8 * prevR);
                maxPwr = Math.Max(r, maxPwr);
                var pwr = maxPwr != 0 ? r / maxPwr : 0;

                if (pwr >= 0.5)
                {
                    spx += j * pwr;
                    sp += pwr;
                }
            }

            var domCyc = sp != 0 ? spx / sp : 0;
            domCycList.AddRounded(domCyc);

            var signal = GetCompareSignal(roofingFilter - prevRoofingFilter1, prevRoofingFilter1 - prevRoofingFilter2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Edftse", domCycList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = domCycList;
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
        List<double> domCycList = new();
        List<double> bpList = new();
        List<Signal> signalsList = new();

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
            bpList.AddRounded(bp);

            var domCyc = sp != 0 ? spx / sp : 0;
            domCycList.AddRounded(domCyc);

            var signal = GetCompareSignal(roofingFilter - prevRoofingFilter1, prevRoofingFilter1 - prevRoofingFilter2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ecfse", domCycList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = domCycList;
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
        List<double> reversalList = new();
        List<Signal> signalsList = new();
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
            reversalList.AddRounded(reversal);

            var signal = GetConditionSignal(currentValue < ema && reversal == 1, currentValue > ema && reversal == 1);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Eacr", reversalList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = reversalList;
        stockData.IndicatorName = IndicatorName.EhlersAutoCorrelationReversals;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Reverse Exponential Moving Average Indicator V1
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="alpha"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersReverseExponentialMovingAverageIndicatorV1(this StockData stockData, double alpha = 0.1)
    {
        List<double> emaList = new();
        List<double> re1List = new();
        List<double> re2List = new();
        List<double> re3List = new();
        List<double> re4List = new();
        List<double> re5List = new();
        List<double> re6List = new();
        List<double> re7List = new();
        List<double> waveList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var cc = 1 - alpha;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];

            var prevEma = emaList.LastOrDefault();
            var ema = (alpha * currentValue) + (cc * prevEma);
            emaList.AddRounded(ema);

            var prevRe1 = re1List.LastOrDefault();
            var re1 = (cc * ema) + prevEma;
            re1List.AddRounded(re1);

            var prevRe2 = re2List.LastOrDefault();
            var re2 = (Pow(cc, 2) * re1) + prevRe1;
            re2List.AddRounded(re2);

            var prevRe3 = re3List.LastOrDefault();
            var re3 = (Pow(cc, 4) * re2) + prevRe2;
            re3List.AddRounded(re3);

            var prevRe4 = re4List.LastOrDefault();
            var re4 = (Pow(cc, 8) * re3) + prevRe3;
            re4List.AddRounded(re4);

            var prevRe5 = re5List.LastOrDefault();
            var re5 = (Pow(cc, 16) * re4) + prevRe4;
            re5List.AddRounded(re5);

            var prevRe6 = re6List.LastOrDefault();
            var re6 = (Pow(cc, 32) * re5) + prevRe5;
            re6List.AddRounded(re6);

            var prevRe7 = re7List.LastOrDefault();
            var re7 = (Pow(cc, 64) * re6) + prevRe6;
            re7List.AddRounded(re7);

            var re8 = (Pow(cc, 128) * re7) + prevRe7;
            var prevWave = waveList.LastOrDefault();
            var wave = ema - (alpha * re8);
            waveList.AddRounded(wave);

            var signal = GetCompareSignal(wave, prevWave);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Erema", waveList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = waveList;
        stockData.IndicatorName = IndicatorName.EhlersReverseExponentialMovingAverageIndicatorV1;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Reverse Exponential Moving Average Indicator V2
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="trendAlpha"></param>
    /// <param name="cycleAlpha"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersReverseExponentialMovingAverageIndicatorV2(this StockData stockData, double trendAlpha = 0.05, double cycleAlpha = 0.3)
    {
        List<Signal> signalsList = new();

        var trendList = CalculateEhlersReverseExponentialMovingAverageIndicatorV1(stockData, trendAlpha).CustomValuesList;
        var cycleList = CalculateEhlersReverseExponentialMovingAverageIndicatorV1(stockData, cycleAlpha).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var waveCycle = cycleList[i];
            var waveTrend = trendList[i];
            var prevWaveCycle = i >= 1 ? cycleList[i - 1] : 0;
            var prevWaveTrend = i >= 1 ? trendList[i - 1] : 0;

            var signal = GetCompareSignal(waveCycle - waveTrend, prevWaveCycle - prevWaveTrend);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "EremaCycle", cycleList },
            { "EremaTrend", trendList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.EhlersReverseExponentialMovingAverageIndicatorV2;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Moving Average Difference Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersMovingAverageDifferenceIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage, 
        int fastLength = 8, int slowLength = 23)
    {
        List<double> madList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var shortMaList = GetMovingAverageList(stockData, maType, fastLength, inputList);
        var longMaList = GetMovingAverageList(stockData, maType, slowLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var shortMa = shortMaList[i];
            var longMa = longMaList[i];
            var prevMad1 = i >= 1 ? madList[i - 1] : 0;
            var prevMad2 = i >= 2 ? madList[i - 2] : 0;

            var mad = longMa != 0 ? 100 * (shortMa - longMa) / longMa : 0;
            madList.AddRounded(mad);

            var signal = GetCompareSignal(mad - prevMad1, prevMad1 - prevMad2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Emad", madList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = madList;
        stockData.IndicatorName = IndicatorName.EhlersMovingAverageDifferenceIndicator;

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
        List<double> efdso2PoleList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var scaledFilter2PoleList = GetMovingAverageList(stockData, maType, fastLength, inputList, fastLength, slowLength);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentScaledFilter2Pole = scaledFilter2PoleList[i];
            var prevEfdsoPole1 = i >= 1 ? efdso2PoleList[i - 1] : 0;
            var prevEfdsoPole2 = i >= 2 ? efdso2PoleList[i - 2] : 0;

            var efdso2Pole = Math.Abs(currentScaledFilter2Pole) < 2 ? 0.5 * Math.Log((1 + (currentScaledFilter2Pole / 2)) / 
                (1 - (currentScaledFilter2Pole / 2))) : prevEfdsoPole1;
            efdso2PoleList.AddRounded(efdso2Pole);

            var signal = GetRsiSignal(efdso2Pole - prevEfdsoPole1, prevEfdsoPole1 - prevEfdsoPole2, efdso2Pole, prevEfdsoPole1, 2, -2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Efdso", efdso2PoleList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = efdso2PoleList;
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
        List<double> v1List = new();
        List<double> inPhaseList = new();
        List<double> quadList = new();
        List<Signal> signalsList = new();
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
            v1List.AddRounded(v1);

            var prevInPhase = inPhaseList.LastOrDefault();
            var inPhase = (1.25 * (v4 - (iMult * v2))) + (iMult * inPhase3);
            inPhaseList.AddRounded(inPhase);

            var prevQuad = quadList.LastOrDefault();
            var quad = v2 - (qMult * v1) + (qMult * quad2);
            quadList.AddRounded(quad);

            var signal = GetCompareSignal(quad - (-1 * inPhase), prevQuad - (-1 * prevInPhase));
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Quad", quadList },
            { "Inphase", inPhaseList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
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
        List<double> phaseList = new();
        List<double> dPhaseList = new();
        List<double> dcPeriodList = new();
        List<Signal> signalsList = new();

        var ehtList = CalculateEhlersHilbertTransformIndicator(stockData, length: length1);
        var ipList = ehtList.OutputValues["Inphase"];
        var quList = ehtList.OutputValues["Quad"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var ip = ipList[i];
            var qu = quList[i];
            var prevIp = i >= 1 ? ipList[i - 1] : 0;
            var prevQu = i >= 1 ? quList[i - 1] : 0;

            var prevPhase = phaseList.LastOrDefault();
            var phase = Math.Abs(ip + prevIp) > 0 ? Math.Atan(Math.Abs((qu + prevQu) / (ip + prevIp))).ToDegrees() : 0;
            phase = ip < 0 && qu > 0 ? 180 - phase : phase;
            phase = ip < 0 && qu < 0 ? 180 + phase : phase;
            phase = ip > 0 && qu < 0 ? 360 - phase : phase;
            phaseList.AddRounded(phase);

            var dPhase = prevPhase - phase;
            dPhase = prevPhase < 90 && phase > 270 ? 360 + prevPhase - phase : dPhase;
            dPhase = MinOrMax(dPhase, 60, 1);
            dPhaseList.AddRounded(dPhase);

            double instPeriod = 0, v4 = 0;
            for (var j = 0; j <= length2; j++)
            {
                var prevDPhase = i >= j ? dPhaseList[i - j] : 0;
                v4 += prevDPhase;
                instPeriod = v4 > 360 && instPeriod == 0 ? j : instPeriod;
            }

            var prevDcPeriod = dcPeriodList.LastOrDefault();
            var dcPeriod = (0.25 * instPeriod) + (0.75 * prevDcPeriod);
            dcPeriodList.AddRounded(dcPeriod);

            var signal = GetCompareSignal(qu - (-1 * ip), prevQu - (-1 * prevIp));
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Eipi", dcPeriodList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = dcPeriodList;
        stockData.IndicatorName = IndicatorName.EhlersInstantaneousPhaseIndicator;

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
    public static StockData CalculateEhlersSquelchIndicator(this StockData stockData, int length1 = 6, int length2 = 20, int length3 = 40)
    {
        List<double> phaseList = new();
        List<double> dPhaseList = new();
        List<double> dcPeriodList = new();
        List<double> v1List = new();
        List<double> ipList = new();
        List<double> quList = new();
        List<double> siList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= length1 ? inputList[i - length1] : 0;
            var priorV1 = i >= length1 ? v1List[i - length1] : 0;
            var prevV12 = i >= 2 ? v1List[i - 2] : 0;
            var prevV14 = i >= 4 ? v1List[i - 4] : 0;

            var v1 = MinPastValues(i, length1, currentValue - prevValue);
            v1List.AddRounded(v1);

            var v2 = i >= 3 ? v1List[i - 3] : 0;
            var v3 = (0.75 * (v1 - priorV1)) + (0.25 * (prevV12 - prevV14));
            var prevIp = ipList.LastOrDefault();
            var ip = (0.33 * v2) + (0.67 * prevIp);
            ipList.AddRounded(ip);

            var prevQu = quList.LastOrDefault();
            var qu = (0.2 * v3) + (0.8 * prevQu);
            quList.AddRounded(qu);

            var prevPhase = phaseList.LastOrDefault();
            var phase = Math.Abs(ip + prevIp) > 0 ? Math.Atan(Math.Abs((qu + prevQu) / (ip + prevIp))).ToDegrees() : 0;
            phase = ip < 0 && qu > 0 ? 180 - phase : phase;
            phase = ip < 0 && qu < 0 ? 180 + phase : phase;
            phase = ip > 0 && qu < 0 ? 360 - phase : phase;
            phaseList.AddRounded(phase);

            var dPhase = prevPhase - phase;
            dPhase = prevPhase < 90 && phase > 270 ? 360 + prevPhase - phase : dPhase;
            dPhase = MinOrMax(dPhase, 60, 1);
            dPhaseList.AddRounded(dPhase);

            double instPeriod = 0, v4 = 0;
            for (var j = 0; j <= length3; j++)
            {
                var prevDPhase = i >= j ? dPhaseList[i - j] : 0;
                v4 += prevDPhase;
                instPeriod = v4 > 360 && instPeriod == 0 ? j : instPeriod;
            }

            var prevDcPeriod = dcPeriodList.LastOrDefault();
            var dcPeriod = (0.25 * instPeriod) + (0.75 * prevDcPeriod);
            dcPeriodList.AddRounded(dcPeriod);

            double si = dcPeriod < length2 ? 0 : 1;
            siList.AddRounded(si);

            var signal = GetCompareSignal(qu - (-1 * ip), prevQu - (-1 * prevIp));
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Esi", siList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = siList;
        stockData.IndicatorName = IndicatorName.EhlersSquelchIndicator;

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
        List<double> peakList = new();
        List<double> realList = new();
        List<double> imagList = new();
        List<Signal> signalsList = new();

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

            var prevPeak = peakList.LastOrDefault();
            var peak = Math.Max(0.991 * prevPeak, Math.Abs(roofingFilter));
            peakList.AddRounded(peak);

            var real = peak != 0 ? roofingFilter / peak : 0;
            realList.AddRounded(real);

            var imag = ((0.091 * real) + (0.111 * prevReal2) + (0.143 * prevReal4) + (0.2 * prevReal6) + (0.333 * prevReal8) + prevReal10 -
                        prevReal12 - (0.333 * prevReal14) - (0.2 * prevReal16) - (0.143 * prevReal18) - (0.111 * prevReal20) - (0.091 * prevReal22)) / 1.865;
            imagList.AddRounded(imag);

            var signal = GetCompareSignal(real - prevReal1, prevReal1 - prevReal2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Real", realList },
            { "Imag", imagList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.EhlersClassicHilbertTransformer;

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
        List<double> peakList = new();
        List<double> realList = new();
        List<double> imagList = new();
        List<double> qPeakList = new();
        List<Signal> signalsList = new();

        var roofingFilterList = CalculateEhlersRoofingFilterV2(stockData, length1, length2).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var roofingFilter = roofingFilterList[i];
            var prevReal1 = i >= 1 ? realList[i - 1] : 0;
            var prevReal2 = i >= 2 ? realList[i - 2] : 0;

            var prevPeak = peakList.LastOrDefault();
            var peak = Math.Max(0.991 * prevPeak, Math.Abs(roofingFilter));
            peakList.AddRounded(peak);

            var prevReal = realList.LastOrDefault();
            var real = peak != 0 ? roofingFilter / peak : 0;
            realList.AddRounded(real);

            var qFilt = real - prevReal;
            var prevQPeak = qPeakList.LastOrDefault();
            var qPeak = Math.Max(0.991 * prevQPeak, Math.Abs(qFilt));
            qPeakList.AddRounded(qPeak);

            var imag = qPeak != 0 ? qFilt / qPeak : 0;
            imagList.AddRounded(imag);

            var signal = GetCompareSignal(real - prevReal1, prevReal1 - prevReal2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Real", realList },
            { "Imag", imagList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
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
        List<double> peakList = new();
        List<double> realList = new();
        List<double> imagList = new();
        List<double> qFiltList = new();
        List<double> qPeakList = new();
        List<Signal> signalsList = new();

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

            var prevPeak = peakList.LastOrDefault();
            var peak = Math.Max(0.991 * prevPeak, Math.Abs(roofingFilter));
            peakList.AddRounded(peak);

            var prevReal = realList.LastOrDefault();
            var real = peak != 0 ? roofingFilter / peak : 0;
            realList.AddRounded(real);

            var qFilt = real - prevReal;
            var prevQPeak = qPeakList.LastOrDefault();
            var qPeak = Math.Max(0.991 * prevQPeak, Math.Abs(qFilt));
            qPeakList.AddRounded(qPeak);

            qFilt = qPeak != 0 ? qFilt / qPeak : 0;
            qFiltList.AddRounded(qFilt);

            var imag = (c1 * ((qFilt + prevQFilt) / 2)) + (c2 * prevImag1) + (c3 * prevImag2);
            imagList.AddRounded(imag);

            var signal = GetCompareSignal(imag - qFilt, prevImag1 - prevQFilt);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Real", realList },
            { "Imag", imagList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.EhlersHilbertTransformerIndicator;

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
        List<double> periodList = new();
        List<double> domCycList = new();
        List<Signal> signalsList = new();

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

            var prevPeriod = periodList.LastOrDefault();
            var period = (real * qDot) - (imag * iDot) != 0 ? 2 * Math.PI * ((real * real) + (imag * imag)) / ((-real * qDot) + (imag * iDot)) : 0;
            period = MinOrMax(period, length1, length3);
            periodList.AddRounded(period);

            var domCyc = (c1 * ((period + prevPeriod) / 2)) + (c2 * prevDomCyc1) + (c3 * prevDomCyc2);
            domCycList.AddRounded(domCyc);

            var signal = GetCompareSignal(real - prevReal1, prevReal1 - prevReal2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Edddc", domCycList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = domCycList;
        stockData.IndicatorName = IndicatorName.EhlersDualDifferentiatorDominantCycle;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Phase Accumulation Dominant Cycle
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="length4"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersPhaseAccumulationDominantCycle(this StockData stockData, int length1 = 48, int length2 = 20, int length3 = 10, 
        int length4 = 40)
    {
        List<double> phaseList = new();
        List<double> dPhaseList = new();
        List<double> instPeriodList = new();
        List<double> domCycList = new();
        List<Signal> signalsList = new();

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
            var prevDomCyc1 = i >= 1 ? domCycList[i - 1] : 0;
            var prevDomCyc2 = i >= 2 ? domCycList[i - 2] : 0;

            var prevPhase = phaseList.LastOrDefault();
            var phase = Math.Abs(real) > 0 ? Math.Atan(Math.Abs(imag / real)).ToDegrees() : 0;
            phase = real < 0 && imag > 0 ? 180 - phase : phase;
            phase = real < 0 && imag < 0 ? 180 + phase : phase;
            phase = real > 0 && imag < 0 ? 360 - phase : phase;
            phaseList.AddRounded(phase);

            var dPhase = prevPhase - phase;
            dPhase = prevPhase < 90 && phase > 270 ? 360 + prevPhase - phase : dPhase;
            dPhase = MinOrMax(dPhase, length1, length3);
            dPhaseList.AddRounded(dPhase);

            var prevInstPeriod = instPeriodList.LastOrDefault();
            double instPeriod = 0, phaseSum = 0;
            for (var j = 0; j < length4; j++)
            {
                var prevDPhase = i >= j ? dPhaseList[i - j] : 0;
                phaseSum += prevDPhase;

                if (phaseSum > 360 && instPeriod == 0)
                {
                    instPeriod = j;
                }
            }
            instPeriod = instPeriod == 0 ? prevInstPeriod : instPeriod;
            instPeriodList.AddRounded(instPeriod);

            var domCyc = (c1 * ((instPeriod + prevInstPeriod) / 2)) + (c2 * prevDomCyc1) + (c3 * prevDomCyc2);
            domCycList.AddRounded(domCyc);

            var signal = GetCompareSignal(real - prevReal1, prevReal1 - prevReal2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Epadc", domCycList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = domCycList;
        stockData.IndicatorName = IndicatorName.EhlersPhaseAccumulationDominantCycle;

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
        List<double> periodList = new();
        List<double> domCycList = new();
        List<Signal> signalsList = new();

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

            var prevPeriod = periodList.LastOrDefault();
            var period = im != 0 && re != 0 ? 2 * Math.PI / Math.Abs(im / re) : 0;
            period = MinOrMax(period, length1, length3);
            periodList.AddRounded(period);

            var domCyc = (c1 * ((period + prevPeriod) / 2)) + (c2 * prevDomCyc1) + (c3 * prevDomCyc2);
            domCycList.AddRounded(domCyc);

            var signal = GetCompareSignal(real - prevReal1, prevReal1 - prevReal2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ehdc", domCycList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = domCycList;
        stockData.IndicatorName = IndicatorName.EhlersHomodyneDominantCycle;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Signal To Noise Ratio V1
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersSignalToNoiseRatioV1(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length = 7)
    {
        List<double> ampList = new();
        List<double> v2List = new();
        List<double> rangeList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        var hilbertTransformList = CalculateEhlersHilbertTransformIndicator(stockData, length: length);
        var inPhaseList = hilbertTransformList.OutputValues["Inphase"];
        var quadList = hilbertTransformList.OutputValues["Quad"];
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

            var prevV2 = v2List.LastOrDefault();
            var v2 = (0.2 * ((inPhase * inPhase) + (quad * quad))) + (0.8 * prevV2);
            v2List.AddRounded(v2);

            var prevRange = rangeList.LastOrDefault();
            var range = (0.2 * (currentHigh - currentLow)) + (0.8 * prevRange);
            rangeList.AddRounded(range);

            var prevAmp = ampList.LastOrDefault();
            var amp = range != 0 ? (0.25 * ((10 * Math.Log(v2 / (range * range)) / Math.Log(10)) + 1.9)) + (0.75 * prevAmp) : 0;
            ampList.AddRounded(amp);

            var signal = GetVolatilitySignal(currentValue - currentEma, prevValue - prevEma, amp, 1.9);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Esnr", ampList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = ampList;
        stockData.IndicatorName = IndicatorName.EhlersSignalToNoiseRatioV1;

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
        List<double> rocList = new();
        List<double> derivList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, openList, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentOpen = openList[i];
            var currentValue = inputList[i];
            
            var deriv = currentValue - currentOpen;
            derivList.AddRounded(deriv);
        }

        var filtList = GetMovingAverageList(stockData, maType, length, derivList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var filt = filtList[i];
            var prevFilt1 = i >= 1 ? filtList[i - 1] : 0;
            var prevFilt2 = i >= 2 ? filtList[i - 2] : 0;

            var roc = length / 2 * Math.PI * (filt - prevFilt1);
            rocList.AddRounded(roc);

            var signal = GetCompareSignal(filt - prevFilt1, prevFilt1 - prevFilt2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ehwi", filtList },
            { "Roc", rocList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = filtList;
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
        List<double> rocList = new();
        List<double> derivList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, openList, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentOpen = openList[i];
            var currentValue = inputList[i];
            

            var deriv = currentValue - currentOpen;
            derivList.AddRounded(deriv);
        }

        var filtList = GetMovingAverageList(stockData, maType, length, derivList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var filt = filtList[i];
            var prevFilt1 = i >= 1 ? filtList[i - 1] : 0;
            var prevFilt2 = i >= 2 ? filtList[i - 2] : 0;

            var roc = length / 2 * Math.PI * (filt - prevFilt1);
            rocList.AddRounded(roc);

            var signal = GetCompareSignal(filt - prevFilt1, prevFilt1 - prevFilt2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ehwi", filtList },
            { "Roc", rocList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = filtList;
        stockData.IndicatorName = IndicatorName.EhlersHammingWindowIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Triangle Window Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersTriangleWindowIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.EhlersTriangleMovingAverage,
        int length = 20)
    {
        List<double> rocList = new();
        List<double> derivList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, openList, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentOpen = openList[i];
            var currentValue = inputList[i];
            
            var deriv = currentValue - currentOpen;
            derivList.AddRounded(deriv);
        }

        var filtList = GetMovingAverageList(stockData, maType, length, derivList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var filt = filtList[i];
            var prevFilt1 = i >= 1 ? filtList[i - 1] : 0;
            var prevFilt2 = i >= 2 ? filtList[i - 2] : 0;

            var roc = length / 2 * Math.PI * (filt - prevFilt1);
            rocList.AddRounded(roc);

            var signal = GetCompareSignal(filt - prevFilt1, prevFilt1 - prevFilt2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Etwi", filtList },
            { "Roc", rocList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = filtList;
        stockData.IndicatorName = IndicatorName.EhlersTriangleWindowIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Simple Window Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersSimpleWindowIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 20)
    {
        List<double> rocList = new();
        List<double> derivList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, openList, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentOpen = openList[i];
            var currentValue = inputList[i];

            var deriv = currentValue - currentOpen;
            derivList.AddRounded(deriv);
        }

        var filtList = GetMovingAverageList(stockData, maType, length, derivList);
        var filtMa1List = GetMovingAverageList(stockData, maType, length, filtList);
        var filtMa2List = GetMovingAverageList(stockData, maType, length, filtMa1List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var filt = filtMa2List[i];
            var prevFilt1 = i >= 1 ? filtMa2List[i - 1] : 0;
            var prevFilt2 = i >= 2 ? filtMa2List[i - 2] : 0;

            var roc = length / 2 * Math.PI * (filt - prevFilt1);
            rocList.AddRounded(roc);

            var signal = GetCompareSignal(filt - prevFilt1, prevFilt1 - prevFilt2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Etwi", filtList },
            { "Roc", rocList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = filtList;
        stockData.IndicatorName = IndicatorName.EhlersTriangleWindowIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Signal To Noise Ratio V2
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersSignalToNoiseRatioV2(this StockData stockData, int length = 6)
    {
        List<double> snrList = new();
        List<double> rangeList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        var ehlersMamaList = CalculateEhlersMotherOfAdaptiveMovingAverages(stockData);
        var i1List = ehlersMamaList.OutputValues["I1"];
        var q1List = ehlersMamaList.OutputValues["Q1"];
        var mamaList = ehlersMamaList.CustomValuesList;

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

            var prevRange = rangeList.LastOrDefault();
            var range = (0.1 * (currentHigh - currentLow)) + (0.9 * prevRange);
            rangeList.AddRounded(range);

            var temp = range != 0 ? ((i1 * i1) + (q1 * q1)) / (range * range) : 0;
            var prevSnr = snrList.LastOrDefault();
            var snr = range > 0 ? (0.25 * ((10 * Math.Log(temp) / Math.Log(10)) + length)) + (0.75 * prevSnr) : 0;
            snrList.AddRounded(snr);

            var signal = GetVolatilitySignal(currentValue - mama, prevValue - prevMama, snr, length);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Esnr", snrList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = snrList;
        stockData.IndicatorName = IndicatorName.EhlersSignalToNoiseRatioV2;

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
        List<double> q3List = new();
        List<double> i3List = new();
        List<double> noiseList = new();
        List<double> snrList = new();
        List<Signal> signalsList = new();
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
            q3List.AddRounded(q3);

            var sp = (int)Math.Ceiling(smoothPeriod / 2);
            double i3 = 0;
            for (var j = 0; j <= sp - 1; j++)
            {
                var prevQ3 = i >= j ? q3List[i - j] : 0;
                i3 += prevQ3;
            }
            i3 = sp != 0 ? 1.57 * i3 / sp : i3;
            i3List.AddRounded(i3);

            var signalValue = (i3 * i3) + (q3 * q3);
            var prevNoise = noiseList.LastOrDefault();
            var noise = (0.1 * (currentHigh - currentLow) * (currentHigh - currentLow) * 0.25) + (0.9 * prevNoise);
            noiseList.AddRounded(noise);

            var temp = noise != 0 ? signalValue / noise : 0;
            var prevSnr = snrList.LastOrDefault();
            var snr = (0.33 * (10 * Math.Log(temp) / Math.Log(10))) + (0.67 * prevSnr);
            snrList.AddRounded(snr);

            var signal = GetVolatilitySignal(currentValue - smooth, prevValue - prevSmooth, snr, length);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Esnr", snrList },
            { "I3", i3List },
            { "Q3", q3List },
            { "SmoothPeriod", smoothPeriodList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = snrList;
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
        List<double> iqList = new();
        List<Signal> signalsList = new();

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
            iqList.AddRounded(iq);

            var signal = GetCompareSignal(iq - i3, prevIq - prevI3);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "I3", i3List },
            { "IQ", iqList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.EhlersHilbertOscillator;

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
        List<double> snrList = new();
        List<double> rangeList = new();
        List<Signal> signalsList = new();
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

            var prevRange = rangeList.LastOrDefault();
            var range = (0.1 * (currentHigh - currentLow)) + (0.9 * prevRange);
            rangeList.AddRounded(range);

            var temp = range != 0 ? (re + im) / (range * range) : 0;
            var prevSnr = snrList.LastOrDefault();
            var snr = (0.25 * ((10 * Math.Log(temp) / Math.Log(10)) + length)) + (0.75 * prevSnr);
            snrList.AddRounded(snr);

            var signal = GetVolatilitySignal(currentValue - mama, prevValue - prevMama, snr, length);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Esnr", snrList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = snrList;
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
        List<double> cleanedDataList = new();
        List<double> hpList = new();
        List<double> powerList = new();
        List<double> dominantCycleList = new();
        List<Signal> signalsList = new();
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
            hpList.AddRounded(hp);

            var cleanedData = i <= 5 ? currentValue : (hp + (2 * prevHp1) + (3 * prevHp2) + (3 * prevHp3) + (2 * prevHp4) + prevHp5) / 12;
            cleanedDataList.AddRounded(cleanedData);

            double pwr = 0, cosPart = 0, sinPart = 0;
            for (var j = minLength; j <= maxLength; j++)
            {
                for (var n = 0; n <= maxLength - 1; n++)
                {
                    var prevCleanedData = i >= n ? cleanedDataList[i - n] : 0;
                    cosPart += prevCleanedData * Math.Cos(MinOrMax(2 * Math.PI * ((double)n / j), 0.99, 0.01));
                    sinPart += prevCleanedData * Math.Sin(MinOrMax(2 * Math.PI * ((double)n / j), 0.99, 0.01));
                }

                pwr = (cosPart * cosPart) + (sinPart * sinPart);
            }
            powerList.AddRounded(pwr);

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
            dominantCycleList.AddRounded(dominantCycle);

            var signal = GetCompareSignal(hp, prevHp1);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Edft", dominantCycleList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = dominantCycleList;
        stockData.IndicatorName = IndicatorName.EhlersDiscreteFourierTransform;

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
        List<double> bp1List = new();
        List<double> bp2List = new();
        List<double> bp3List = new();
        List<double> q1List = new();
        List<double> q2List = new();
        List<double> q3List = new();
        List<double> waveList = new();
        List<double> rocList = new();
        List<Signal> signalsList = new();
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
            var prevBp1_1 = bp1List.LastOrDefault();
            var prevBp2_1 = bp2List.LastOrDefault();
            var prevBp3_1 = bp3List.LastOrDefault();
            var prevValue = i >= 2 ? inputList[i - 2] : 0;
            var prevBp1_2 = i >= 2 ? bp1List[i - 2] : 0;
            var prevBp2_2 = i >= 2 ? bp2List[i - 2] : 0;
            var prevBp3_2 = i >= 2 ? bp3List[i - 2] : 0;
            var prevWave2 = i >= 2 ? waveList[i - 2] : 0;

            var bp1 = i <= 3 ? 0 : (0.5 * (1 - s1) * (currentValue - prevValue)) + (l1 * (1 + s1) * prevBp1_1) - (s1 * prevBp1_2);
            bp1List.AddRounded(bp1);

            var q1 = i <= 4 ? 0 : length / 2 * Math.PI * (bp1 - prevBp1_1);
            q1List.AddRounded(q1);

            var bp2 = i <= 3 ? 0 : (0.5 * (1 - s2) * (currentValue - prevValue)) + (l2 * (1 + s2) * prevBp2_1) - (s2 * prevBp2_2);
            bp2List.AddRounded(bp2);

            var q2 = i <= 4 ? 0 : length / 2 * Math.PI * (bp2 - prevBp2_1);
            q2List.AddRounded(q2);

            var bp3 = i <= 3 ? 0 : (0.5 * (1 - s3) * (currentValue - prevValue)) + (l3 * (1 + s3) * prevBp3_1) - (s3 * prevBp3_2);
            bp3List.AddRounded(bp3);

            var q3 = i <= 4 ? 0 : length / 2 * Math.PI * (bp3 - prevBp3_1);
            q3List.AddRounded(q3);

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

            var prevWave = waveList.LastOrDefault();
            var wave = p1 != 0 ? bp1 + (Sqrt(p2 / p1) * bp2) + (Sqrt(p3 / p1) * bp3) : 0;
            waveList.AddRounded(wave);

            var roc = length / Math.PI * 4 * (wave - prevWave2);
            rocList.AddRounded(roc);

            var signal = GetCompareSignal(wave, prevWave);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Wave", waveList },
            { "Roc", rocList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.EhlersFourierSeriesAnalysis;

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
    public static StockData CalculateEhlersVossPredictiveFilter(this StockData stockData, int length = 20, double predict = 3, double bw = 0.25)
    {
        List<double> filtList = new();
        List<double> vossList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var order = MinOrMax((int)Math.Ceiling(3 * predict));
        var f1 = Math.Cos(2 * Math.PI / length);
        var g1 = Math.Cos(bw * 2 * Math.PI / length);
        var s1 = (1 / g1) - Sqrt((1 / (g1 * g1)) - 1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevFilt1 = i >= 1 ? filtList[i - 1] : 0;
            var prevFilt2 = i >= 2 ? filtList[i - 2] : 0;
            var prevValue = i >= 2 ? inputList[i - 2] : 0;

            var filt = i <= 5 ? 0 : (0.5 * (1 - s1) * (currentValue - prevValue)) + (f1 * (1 + s1) * prevFilt1) - (s1 * prevFilt2);
            filtList.AddRounded(filt);

            double sumC = 0;
            for (var j = 0; j <= order - 1; j++)
            {
                var prevVoss = i >= order - j ? vossList[i - (order - j)] : 0;
                sumC += (double)(j + 1) / order * prevVoss;
            }

            var prevvoss = vossList.LastOrDefault();
            var voss = ((double)(3 + order) / 2 * filt) - sumC;
            vossList.AddRounded(voss);

            var signal = GetCompareSignal(voss - filt, prevvoss - prevFilt1);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Voss", vossList },
            { "Filt", filtList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.EhlersVossPredictiveFilter;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Swiss Army Knife Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="delta"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersSwissArmyKnifeIndicator(this StockData stockData, int length = 20, double delta = 0.1)
    {
        List<double> emaFilterList = new();
        List<double> smaFilterList = new();
        List<double> gaussFilterList = new();
        List<double> butterFilterList = new();
        List<double> smoothFilterList = new();
        List<double> hpFilterList = new();
        List<double> php2FilterList = new();
        List<double> bpFilterList = new();
        List<double> bsFilterList = new();
        List<double> filterAvgList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var twoPiPrd = MinOrMax(2 * Math.PI / length, 0.99, 0.01);
        var deltaPrd = MinOrMax(2 * Math.PI * 2 * delta / length, 0.99, 0.01);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevPrice1 = i >= 1 ? inputList[i - 1] : 0;
            var prevPrice2 = i >= 2 ? inputList[i - 2] : 0;
            var prevPrice = i >= length ? inputList[i - length] : 0;
            var prevEmaFilter1 = emaFilterList.LastOrDefault();
            var prevSmaFilter1 = smaFilterList.LastOrDefault();
            var prevGaussFilter1 = gaussFilterList.LastOrDefault();
            var prevButterFilter1 = butterFilterList.LastOrDefault();
            var prevSmoothFilter1 = smoothFilterList.LastOrDefault();
            var prevHpFilter1 = hpFilterList.LastOrDefault();
            var prevPhp2Filter1 = php2FilterList.LastOrDefault();
            var prevBpFilter1 = bpFilterList.LastOrDefault();
            var prevBsFilter1 = bsFilterList.LastOrDefault();
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
            emaFilterList.AddRounded(emaFilter);

            var n = length; c0 = 1; c1 = (double)1 / n; b0 = (double)1 / n; b1 = 0; b2 = 0; a1 = 1; a2 = 0;
            var smaFilter = i <= length ? currentValue :
                (c0 * ((b0 * currentValue) + (b1 * prevPrice1) + (b2 * prevPrice2))) + (a1 * prevSmaFilter1) + (a2 * prevSmaFilter2) - (c1 * prevPrice);
            smaFilterList.AddRounded(smaFilter);

            double beta = 2.415 * (1 - Math.Cos(twoPiPrd)), sqrtData = Pow(beta, 2) + (2 * beta), sqrt = Sqrt(sqrtData); alpha = (-1 * beta) + sqrt;
            c0 = Pow(alpha, 2); c1 = 0; b0 = 1; b1 = 0; b2 = 0; a1 = 2 * (1 - alpha); a2 = -(1 - alpha) * (1 - alpha);
            var gaussFilter = i <= length ? currentValue :
                (c0 * ((b0 * currentValue) + (b1 * prevPrice1) + (b2 * prevPrice2))) + (a1 * prevGaussFilter1) + (a2 * prevGaussFilter2) - (c1 * prevPrice);
            gaussFilterList.AddRounded(gaussFilter);

            beta = 2.415 * (1 - Math.Cos(twoPiPrd)); sqrtData = (beta * beta) + (2 * beta); sqrt = sqrtData >= 0 ? Sqrt(sqrtData) : 0; alpha = (-1 * beta) + sqrt;
            c0 = Pow(alpha, 2) / 4; c1 = 0; b0 = 1; b1 = 2; b2 = 1; a1 = 2 * (1 - alpha); a2 = -(1 - alpha) * (1 - alpha);
            var butterFilter = i <= length ? currentValue :
                (c0 * ((b0 * currentValue) + (b1 * prevPrice1) + (b2 * prevPrice2))) + (a1 * prevButterFilter1) + (a2 * prevButterFilter2) - (c1 * prevPrice);
            butterFilterList.AddRounded(butterFilter);

            c0 = (double)1 / 4; c1 = 0; b0 = 1; b1 = 2; b2 = 1; a1 = 0; a2 = 0;
            var smoothFilter = (c0 * ((b0 * currentValue) + (b1 * prevPrice1) + (b2 * prevPrice2))) + (a1 * prevSmoothFilter1) + 
                (a2 * prevSmoothFilter2) - (c1 * prevPrice);
            smoothFilterList.AddRounded(smoothFilter);

            alpha = (Math.Cos(twoPiPrd) + Math.Sin(twoPiPrd) - 1) / Math.Cos(twoPiPrd); c0 = 1 - (alpha / 2); c1 = 0; b0 = 1; b1 = -1; b2 = 0; a1 = 1 - alpha; a2 = 0;
            var hpFilter = i <= length ? 0 :
                (c0 * ((b0 * currentValue) + (b1 * prevPrice1) + (b2 * prevPrice2))) + (a1 * prevHpFilter1) + (a2 * prevHpFilter2) - (c1 * prevPrice);
            hpFilterList.AddRounded(hpFilter);

            beta = 2.415 * (1 - Math.Cos(twoPiPrd)); sqrtData = Pow(beta, 2) + (2 * beta); sqrt = sqrtData >= 0 ? Sqrt(sqrtData) : 0; alpha = (-1 * beta) + sqrt; 
            c0 = (1 - (alpha / 2)) * (1 - (alpha / 2)); c1 = 0; b0 = 1; b1 = -2; b2 = 1; a1 = 2 * (1 - alpha); a2 = -(1 - alpha) * (1 - alpha);
            var php2Filter = i <= length ? 0 :
                (c0 * ((b0 * currentValue) + (b1 * prevPrice1) + (b2 * prevPrice2))) + (a1 * prevPhp2Filter1) + (a2 * prevPhp2Filter2) - (c1 * prevPrice);
            php2FilterList.AddRounded(php2Filter);

            beta = Math.Cos(twoPiPrd); var gamma = 1 / Math.Cos(deltaPrd); sqrtData = Pow(gamma, 2) - 1; sqrt = Sqrt(sqrtData);
            alpha = gamma - sqrt; c0 = (1 - alpha) / 2; c1 = 0; b0 = 1; b1 = 0; b2 = -1; a1 = beta * (1 + alpha); a2 = alpha * -1;
            var bpFilter = i <= length ? currentValue :
                (c0 * ((b0 * currentValue) + (b1 * prevPrice1) + (b2 * prevPrice2))) + (a1 * prevBpFilter1) + (a2 * prevBpFilter2) - (c1 * prevPrice);
            bpFilterList.AddRounded(bpFilter);

            beta = Math.Cos(twoPiPrd); gamma = 1 / Math.Cos(deltaPrd); sqrtData = Pow(gamma, 2) - 1; sqrt = sqrtData >= 0 ? Sqrt(sqrtData) : 0;
            alpha = gamma - sqrt; c0 = (1 + alpha) / 2; c1 = 0; b0 = 1; b1 = -2 * beta; b2 = 1; a1 = beta * (1 + alpha); a2 = alpha * -1;
            var bsFilter = i <= length ? currentValue :
                (c0 * ((b0 * currentValue) + (b1 * prevPrice1) + (b2 * prevPrice2))) + (a1 * prevBsFilter1) + (a2 * prevBsFilter2) - (c1 * prevPrice);
            bsFilterList.AddRounded(bsFilter);

            var signal = GetCompareSignal(smaFilter - prevSmaFilter1, prevSmaFilter1 - prevSmaFilter2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "EmaFilter", emaFilterList },
            { "SmaFilter", smaFilterList },
            { "GaussFilter", gaussFilterList },
            { "ButterFilter", butterFilterList },
            { "SmoothFilter", smoothFilterList },
            { "HpFilter", hpFilterList },
            { "PhpFilter", php2FilterList },
            { "BpFilter", bpFilterList },
            { "BsFilter", bsFilterList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = smaFilterList;
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
    public static StockData CalculateEhlersUniversalOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 20, int signalLength = 9)
    {
        List<double> euoList = new();
        List<double> whitenoiseList = new();
        List<double> filtList = new();
        List<double> pkList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var a1 = Exp(-MinOrMax(1.414 * Math.PI / length, 0.99, 0.01));
        var b1 = 2 * a1 * Math.Cos(1.414 * Math.PI / length);
        var c2 = b1;
        var c3 = -a1 * a1;
        var c1 = 1 - c2 - c3;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 2 ? inputList[i - 2] : 0;
            var prevFilt2 = i >= 2 ? filtList[i - 2] : 0;

            var prevWhitenoise = whitenoiseList.LastOrDefault();
            var whitenoise = MinPastValues(i, 2, currentValue - prevValue) / 2;
            whitenoiseList.AddRounded(whitenoise);

            var prevFilt1 = filtList.LastOrDefault();
            var filt = (c1 * ((whitenoise + prevWhitenoise) / 2)) + (c2 * prevFilt1) + (c3 * prevFilt2);
            filtList.AddRounded(filt);

            var prevPk = pkList.LastOrDefault();
            var pk = Math.Abs(filt) > prevPk ? Math.Abs(filt) : 0.991 * prevPk;
            pkList.AddRounded(pk);

            var denom = pk == 0 ? -1 : pk;
            var prevEuo = euoList.LastOrDefault();
            var euo = denom == -1 ? prevEuo : pk != 0 ? filt / pk : 0;
            euoList.AddRounded(euo);
        }

        var euoMaList = GetMovingAverageList(stockData, maType, signalLength, euoList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var euo = euoList[i];
            var euoMa = euoMaList[i];
            var prevEuo = i >= 1 ? euoList[i - 1] : 0;
            var prevEuoMa = i >= 1 ? euoMaList[i - 1] : 0;

            var signal = GetCompareSignal(euo - euoMa, prevEuo - prevEuoMa);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Euo", euoList },
            { "Signal", euoMaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = euoList;
        stockData.IndicatorName = IndicatorName.EhlersUniversalOscillator;

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
        List<double> deliList = new();
        List<double> ema1List = new();
        List<double> ema2List = new();
        List<double> dspList = new();
        List<double> tempList = new();
        List<Signal> signalsList = new();
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
            var prevEma1 = i >= 1 ? ema1List.LastOrDefault() : currentPrice;
            var prevEma2 = i >= 1 ? ema2List.LastOrDefault() : currentPrice;

            var ema1 = (alpha * currentPrice) + ((1 - alpha) * prevEma1);
            ema1List.AddRounded(ema1);

            var ema2 = (alpha2 * currentPrice) + ((1 - alpha2) * prevEma2);
            ema2List.AddRounded(ema2);

            var dsp = ema1 - ema2;
            dspList.AddRounded(dsp);

            var prevTemp = tempList.LastOrDefault();
            var temp = (alpha * dsp) + ((1 - alpha) * prevTemp);
            tempList.AddRounded(temp);

            var prevDeli = deliList.LastOrDefault();
            var deli = dsp - temp;
            deliList.AddRounded(deli);

            var signal = GetCompareSignal(deli, prevDeli);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Dsp", dspList },
            { "Deli", deliList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = deliList;
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
        List<double> hpList = new();
        List<double> bpList = new();
        List<double> peakList = new();
        List<double> signalList = new();
        List<double> triggerList = new();
        List<Signal> signalsList = new();
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
            hpList.AddRounded(hp);

            var bp = i > 2 ? (0.5 * (1 - alpha1) * (hp - prevHp2)) + (beta * (1 + alpha1) * prevBp1) - (alpha1 * prevBp2) : 0;
            bpList.AddRounded(bp);

            var prevPeak = peakList.LastOrDefault();
            var peak = Math.Max(0.991 * prevPeak, Math.Abs(bp));
            peakList.AddRounded(peak);

            var prevSig = signalList.LastOrDefault();
            var sig = peak != 0 ? bp / peak : 0;
            signalList.AddRounded(sig);

            var prevTrigger = triggerList.LastOrDefault();
            var trigger = ((1 + (alpha3 / 2)) * (sig - prevSig)) + ((1 - alpha3) * prevTrigger);
            triggerList.AddRounded(trigger);

            var signal = GetCompareSignal(sig - trigger, prevSig - prevTrigger);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ebpf", signalList },
            { "Signal", triggerList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = signalList;
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
        List<double> bpList = new();
        List<Signal> signalsList = new();
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
            bpList.AddRounded(bp);

            var signal = GetCompareSignal(bp - prevBp1, prevBp1 - prevBp2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ebpf", bpList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = bpList;
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
        List<double> bpList = new();
        List<Signal> signalsList = new();
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
            bpList.AddRounded(bp);

            var signal = GetCompareSignal(bp - prevBp1, prevBp1 - prevBp2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ecbpf", bpList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = bpList;
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
        List<double> ptopList = new();
        List<Signal> signalsList = new();

        var lbLength = (int)Math.Ceiling((double)length / 4);

        var bpList = CalculateEhlersCycleBandPassFilter(stockData, length, delta).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevPtop1 = i >= 1 ? ptopList[i - 1] : 0;
            var prevPtop2 = i >= 2 ? ptopList[i - 2] : 0;

            double power = 0;
            for (var j = length; j < length; j++)
            {
                var prevBp1 = i >= j ? bpList[i - j] : 0;
                var prevBp2 = i >= j + lbLength ? bpList[i - (j + lbLength)] : 0;
                power += Pow(prevBp1, 2) + Pow(prevBp2, 2);
            }

            var ptop = 2 * 1.414 * Sqrt(power / length);
            ptopList.AddRounded(ptop);

            var signal = GetCompareSignal(ptop - prevPtop1, prevPtop1 - prevPtop2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Eca", ptopList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = ptopList;
        stockData.IndicatorName = IndicatorName.EhlersCycleAmplitude;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Zero Crossings Dominant Cycle
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="bw"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersZeroCrossingsDominantCycle(this StockData stockData, int length = 20, double bw = 0.7)
    {
        List<double> dcList = new();
        List<Signal> signalsList = new();

        var counter = 0;

        var ebpfList = CalculateEhlersBandPassFilterV1(stockData, length, bw);
        var realList = ebpfList.OutputValues["Ebpf"];
        var triggerList = ebpfList.OutputValues["Signal"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var real = realList[i];
            var trigger = triggerList[i];
            var prevReal = i >= 1 ? realList[i - 1] : 0;
            var prevTrigger = i >= 1 ? triggerList[i - 1] : 0;

            var prevDc = dcList.LastOrDefault();
            var dc = Math.Max(prevDc, 6);
            counter += 1;
            if ((real > 0 && prevReal <= 0) || (real < 0 && prevReal >= 0))
            {
                dc = MinOrMax(2 * counter, 1.25 * prevDc, 0.8 * prevDc);
                counter = 0;
            }
            dcList.AddRounded(dc);

            var signal = GetCompareSignal(real - trigger, prevReal - prevTrigger);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ezcdc", dcList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = dcList;
        stockData.IndicatorName = IndicatorName.EhlersZeroCrossingsDominantCycle;

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
        List<double> bpList = new();
        List<double> peakList = new();
        List<double> signalList = new();
        List<double> triggerList = new();
        List<double> leadPeakList = new();
        List<Signal> signalsList = new();

        var domCycList = CalculateEhlersAutoCorrelationPeriodogram(stockData, length1, length2, length3).CustomValuesList;
        var roofingFilterList = CalculateEhlersRoofingFilterV2(stockData, length1, length2).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var roofingFilter = roofingFilterList[i];
            var domCyc = MinOrMax(domCycList[i], length1, length3);
            var beta = Math.Cos(2 * Math.PI / 0.9 * domCyc);
            var gamma = 1 / Math.Cos(2 * Math.PI * bw / 0.9 * domCyc);
            var alpha = MinOrMax(gamma - Sqrt((gamma * gamma) - 1), 0.99, 0.01);
            var prevRoofingFilter2 = i >= 2 ? roofingFilterList[i - 2] : 0;
            var prevBp1 = i >= 1 ? bpList[i - 1] : 0;
            var prevBp2 = i >= 2 ? bpList[i - 2] : 0;
            var prevSignal1 = i >= 1 ? signalList[i - 1] : 0;
            var prevSignal2 = i >= 2 ? signalList[i - 2] : 0;
            var prevSignal3 = i >= 3 ? signalList[i - 3] : 0;

            var bp = i > 2 ? (0.5 * (1 - alpha) * (roofingFilter - prevRoofingFilter2)) + (beta * (1 + alpha) * prevBp1) - (alpha * prevBp2) : 0;
            bpList.AddRounded(bp);

            var prevPeak = peakList.LastOrDefault();
            var peak = Math.Max(0.991 * prevPeak, Math.Abs(bp));
            peakList.AddRounded(peak);

            var sig = peak != 0 ? bp / peak : 0;
            signalList.AddRounded(sig);

            var lead = 1.3 * (sig + prevSignal1 - prevSignal2 - prevSignal3) / 4;
            var prevLeadPeak = leadPeakList.LastOrDefault();
            var leadPeak = Math.Max(0.93 * prevLeadPeak, Math.Abs(lead));
            leadPeakList.AddRounded(leadPeak);

            var prevTrigger = triggerList.LastOrDefault();
            var trigger = 0.9 * prevSignal1;
            triggerList.AddRounded(trigger);

            var signal = GetRsiSignal(sig - trigger, prevSignal1 - prevTrigger, sig, prevSignal1, 0.707, -0.707);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Eabpf", signalList },
            { "Signal", triggerList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = signalList;
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
        List<double> smoothList = new();
        List<double> cycleList = new();
        List<Signal> signalsList = new();
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
            smoothList.AddRounded(smooth);

            var cycle = i < 7 ? (currentValue - (2 * prevValue1) + prevValue2) / 4 : (Pow(1 - (0.5 * alpha), 2) * (smooth - (2 * prevSmooth1) + prevSmooth2)) +
                (2 * (1 - alpha) * prevCycle1) - (Pow(1 - alpha, 2) * prevCycle2);
            cycleList.AddRounded(cycle);

            var signal = GetCompareSignal(cycle - prevCycle1, prevCycle1 - prevCycle2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ecc", cycleList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = cycleList;
        stockData.IndicatorName = IndicatorName.EhlersCyberCycle;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Stochastic Cyber Cycle
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="alpha"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersStochasticCyberCycle(this StockData stockData, int length = 14, double alpha = 0.7)
    {
        List<double> stochList = new();
        List<double> stochCCList = new();
        List<double> triggerList = new();
        List<Signal> signalsList = new();

        var cyberCycleList = CalculateEhlersCyberCycle(stockData, alpha).CustomValuesList;
        var (maxCycleList, minCycleList) = GetMaxAndMinValuesList(cyberCycleList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevStoch1 = i >= 1 ? stochList[i - 1] : 0;
            var prevStoch2 = i >= 2 ? stochList[i - 2] : 0;
            var prevStoch3 = i >= 3 ? stochList[i - 3] : 0;
            var cycle = cyberCycleList[i];
            var maxCycle = maxCycleList[i];
            var minCycle = minCycleList[i];

            var stoch = maxCycle - minCycle != 0 ? MinOrMax((cycle - minCycle) / (maxCycle - minCycle), 1, 0) : 0;
            stochList.AddRounded(stoch);

            var prevStochCC = stochCCList.LastOrDefault();
            var stochCC = MinOrMax(2 * ((((4 * stoch) + (3 * prevStoch1) + (2 * prevStoch2) + prevStoch3) / 10) - 0.5), 1, -1);
            stochCCList.AddRounded(stochCC);

            var prevTrigger = triggerList.LastOrDefault();
            var trigger = MinOrMax(0.96 * (prevStochCC + 0.02), 1, -1);
            triggerList.AddRounded(trigger);

            var signal = GetRsiSignal(stochCC - trigger, prevStochCC - prevTrigger, stochCC, prevStochCC, 0.5, -0.5);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Escc", stochCCList },
            { "Signal", triggerList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = stochCCList;
        stockData.IndicatorName = IndicatorName.EhlersStochasticCyberCycle;

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
        List<double> hlList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, openList, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentClose = inputList[i];
            var currentOpen = openList[i];
            var der = currentClose - currentOpen;
            var hlRaw = fastLength * der;

            var hl = MinOrMax(hlRaw, 1, -1);
            hlList.AddRounded(hl);
        }

        var ssList = GetMovingAverageList(stockData, maType, slowLength, hlList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var ss = ssList[i];
            var prevSs = i >= 1 ? ssList[i - 1] : 0;

            var signal = GetCompareSignal(ss, prevSs);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Efmd", ssList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = ssList;
        stockData.IndicatorName = IndicatorName.EhlersFMDemodulatorIndicator;

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
    public static StockData CalculateEhlersStochastic(this StockData stockData, MovingAvgType maType = MovingAvgType.Ehlers2PoleSuperSmootherFilterV1, 
        int length1 = 48, int length2 = 20, int length3 = 10)
    {
        List<double> stoch2PoleList = new();
        List<double> arg2PoleList = new();
        List<Signal> signalsList = new();

        var roofingFilter2PoleList = CalculateEhlersRoofingFilterV1(stockData, maType, length1, length3).CustomValuesList;
        var (max2PoleList, min2PoleList) = GetMaxAndMinValuesList(roofingFilter2PoleList, length2);

        for (var i = 0; i < stockData.Count; i++)
        {
            var rf2Pole = roofingFilter2PoleList[i];
            var min2Pole = min2PoleList[i];
            var max2Pole = max2PoleList[i];

            var prevStoch2Pole = stoch2PoleList.LastOrDefault();
            var stoch2Pole = max2Pole - min2Pole != 0 ? MinOrMax((rf2Pole - min2Pole) / (max2Pole - min2Pole), 1, 0) : 0;
            stoch2PoleList.AddRounded(stoch2Pole);

            var arg2Pole = (stoch2Pole + prevStoch2Pole) / 2;
            arg2PoleList.AddRounded(arg2Pole);
        }

        var estoch2PoleList = GetMovingAverageList(stockData, maType, length2, arg2PoleList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var estoch2Pole = estoch2PoleList[i];
            var prevEstoch2Pole1 = i >= 1 ? estoch2PoleList[i - 1] : 0;
            var prevEstoch2Pole2 = i >= 2 ? estoch2PoleList[i - 2] : 0;

            var signal = GetRsiSignal(estoch2Pole - prevEstoch2Pole1, prevEstoch2Pole1 - prevEstoch2Pole2, estoch2Pole, prevEstoch2Pole1, 0.8, 0.2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Es", estoch2PoleList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = estoch2PoleList;
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
    public static StockData CalculateEhlersTripleDelayLineDetrender(this StockData stockData, MovingAvgType maType = MovingAvgType.EhlersModifiedOptimumEllipticFilter, 
        int length = 14)
    {
        List<double> tmp1List = new();
        List<double> tmp2List = new();
        List<double> detrenderList = new();
        List<double> histList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevTmp1_6 = i >= 6 ? tmp1List[i - 6] : 0;
            var prevTmp2_6 = i >= 6 ? tmp2List[i - 6] : 0;
            var prevTmp2_12 = i >= 12 ? tmp2List[i - 12] : 0;

            var tmp1 = currentValue + (0.088 * prevTmp1_6);
            tmp1List.AddRounded(tmp1);

            var tmp2 = tmp1 - prevTmp1_6 + (1.2 * prevTmp2_6) - (0.7 * prevTmp2_12);
            tmp2List.AddRounded(tmp2);

            var detrender = prevTmp2_12 - (2 * prevTmp2_6) + tmp2;
            detrenderList.AddRounded(detrender);
        }

        var tdldList = GetMovingAverageList(stockData, maType, length, detrenderList);
        var tdldSignalList = GetMovingAverageList(stockData, maType, length, tdldList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var tdld = tdldList[i];
            var tdldSignal = tdldSignalList[i];

            var prevHist = histList.LastOrDefault();
            var hist = tdld - tdldSignal;
            histList.AddRounded(hist);

            var signal = GetCompareSignal(hist, prevHist);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Etdld", tdldList },
            { "Signal", tdldSignalList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = tdldList;
        stockData.IndicatorName = IndicatorName.EhlersTripleDelayLineDetrender;

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
        List<double> absDerList = new();
        List<double> envList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, openList, _) = GetInputValuesList(stockData);

        var emaList = GetMovingAverageList(stockData, maType, length1, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentClose = inputList[i];
            var currentOpen = openList[i];
            var der = currentClose - currentOpen;

            var absDer = Math.Abs(der);
            absDerList.AddRounded(absDer);

            var env = absDerList.TakeLastExt(length1).Max();
            envList.AddRounded(env);
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
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Eamd", volList },
            { "Signal", volEmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = volList;
        stockData.IndicatorName = IndicatorName.EhlersAMDetector;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Sine Wave Indicator V1
    /// </summary>
    /// <param name="stockData"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersSineWaveIndicatorV1(this StockData stockData)
    {
        List<double> sineList = new();
        List<double> leadSineList = new();
        List<double> dcPhaseList = new();
        List<Signal> signalsList = new();

        var ehlersMamaList = CalculateEhlersMotherOfAdaptiveMovingAverages(stockData);
        var spList = ehlersMamaList.OutputValues["SmoothPeriod"];
        var smoothList = ehlersMamaList.OutputValues["Smooth"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var sp = spList[i];
            var dcPeriod = (int)Math.Ceiling(sp + 0.5);

            double realPart = 0, imagPart = 0;
            for (var j = 0; j <= dcPeriod - 1; j++)
            {
                var prevSmooth = i >= j ? smoothList[i - j] : 0;
                realPart += Math.Sin(MinOrMax(2 * Math.PI * ((double)j / dcPeriod), 0.99, 0.01)) * prevSmooth;
                imagPart += Math.Cos(MinOrMax(2 * Math.PI * ((double)j / dcPeriod), 0.99, 0.01)) * prevSmooth;
            }

            var dcPhase = Math.Abs(imagPart) > 0.001 ? Math.Atan(realPart / imagPart).ToDegrees() : 90 * Math.Sign(realPart);
            dcPhase += 90;
            dcPhase += sp != 0 ? 360 / sp : 0;
            dcPhase += imagPart < 0 ? 180 : 0;
            dcPhase -= dcPhase > 315 ? 360 : 0;
            dcPhaseList.AddRounded(dcPhase);

            var prevSine = sineList.LastOrDefault();
            var sine = Math.Sin(dcPhase.ToRadians());
            sineList.AddRounded(sine);

            var prevLeadSine = leadSineList.LastOrDefault();
            var leadSine = Math.Sin((dcPhase + 45).ToRadians());
            leadSineList.AddRounded(leadSine);

            var signal = GetCompareSignal(sine - leadSine, prevSine - prevLeadSine);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Sine", sineList },
            { "LeadSine", leadSineList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = sineList;
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
    public static StockData CalculateEhlersSineWaveIndicatorV2(this StockData stockData, int length = 5, double alpha = 0.07)
    {
        List<double> sineList = new();
        List<double> leadSineList = new();
        List<double> dcPhaseList = new();
        List<Signal> signalsList = new();

        var periodList = CalculateEhlersAdaptiveCyberCycle(stockData, length, alpha).OutputValues["Period"];
        var cycleList = CalculateEhlersCyberCycle(stockData).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var period = periodList[i];
            var dcPeriod = (int)Math.Ceiling(period);

            double realPart = 0, imagPart = 0;
            for (var j = 0; j <= dcPeriod - 1; j++)
            {
                var prevCycle = i >= j ? cycleList[i - j] : 0;
                realPart += Math.Sin(MinOrMax(2 * Math.PI * ((double)j / dcPeriod), 0.99, 0.01)) * prevCycle;
                imagPart += Math.Cos(MinOrMax(2 * Math.PI * ((double)j / dcPeriod), 0.99, 0.01)) * prevCycle;
            }

            var dcPhase = Math.Abs(imagPart) > 0.001 ? Math.Atan(realPart / imagPart).ToDegrees() : 90 * Math.Sign(realPart);
            dcPhase += 90;
            dcPhase += imagPart < 0 ? 180 : 0;
            dcPhase -= dcPhase > 315 ? 360 : 0;
            dcPhaseList.AddRounded(dcPhase);

            var prevSine = sineList.LastOrDefault();
            var sine = Math.Sin(dcPhase.ToRadians());
            sineList.AddRounded(sine);

            var prevLeadSine = leadSineList.LastOrDefault();
            var leadSine = Math.Sin((dcPhase + 45).ToRadians());
            leadSineList.AddRounded(leadSine);

            var signal = GetCompareSignal(sine - leadSine, prevSine - prevLeadSine);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Sine", sineList },
            { "LeadSine", leadSineList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = sineList;
        stockData.IndicatorName = IndicatorName.EhlersSineWaveIndicatorV2;

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
        List<double> hpList = new();
        List<double> filtList = new();
        List<double> ebsiList = new();
        List<Signal> signalsList = new();
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

            var prevHp = hpList.LastOrDefault();
            var hp = ((0.5 * (1 + a1)) * MinPastValues(i, 1, currentValue - prevValue)) + (a1 * prevHp);
            hpList.AddRounded(hp);

            var filt = (c1 * ((hp + prevHp) / 2)) + (c2 * prevFilt1) + (c3 * prevFilt2);
            filtList.AddRounded(filt);

            var wave = (filt + prevFilt1 + prevFilt2) / 3;
            var pwr = (Pow(filt, 2) + Pow(prevFilt1, 2) + Pow(prevFilt2, 2)) / 3;
            var prevEbsi = ebsiList.LastOrDefault();
            var ebsi = pwr > 0 ? wave / Sqrt(pwr) : 0;
            ebsiList.AddRounded(ebsi);

            var signal = GetCompareSignal(ebsi, prevEbsi);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ebsi", ebsiList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = ebsiList;
        stockData.IndicatorName = IndicatorName.EhlersEvenBetterSineWaveIndicator;

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
        List<double> convList = new();
        List<double> hpList = new();
        List<double> roofingFilterList = new();
        List<double> slopeList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var piPrd = 0.707 * 2 * Math.PI / length1;
        var alpha = (Math.Cos(piPrd) + Math.Sin(piPrd) - 1) / Math.Cos(piPrd);
        var a1 = Exp(-1.414 * Math.PI / length2);
        var b1 = 2 * a1 * Math.Cos(1.414 * Math.PI / length2);
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
            hpList.AddRounded(highPass);

            var roofingFilter = (c1 * ((highPass + prevHp1) / 2)) + (c2 * prevRoofingFilter1) + (c3 * prevRoofingFilter2);
            roofingFilterList.AddRounded(roofingFilter);

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
            convList.AddRounded(conv);
            slopeList.AddRounded(slope);

            var signal = GetCompareSignal(roofingFilter - prevRoofingFilter1, prevRoofingFilter1 - prevRoofingFilter2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Eci", convList },
            { "Slope", slopeList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = convList;
        stockData.IndicatorName = IndicatorName.EhlersConvolutionIndicator;

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
        List<double> fisherTransformList = new();
        List<double> nValueList = new();
        List<Signal> signalsList = new();
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

            var prevNValue = nValueList.LastOrDefault();
            var nValue = MinOrMax((0.33 * 2 * (ratio - 0.5)) + (0.67 * prevNValue), 0.999, -0.999);
            nValueList.AddRounded(nValue);

            var fisherTransform = (0.5 * Math.Log((1 + nValue) / (1 - nValue))) + (0.5 * prevFisherTransform1);
            fisherTransformList.AddRounded(fisherTransform);

            var signal = GetCompareSignal(fisherTransform - prevFisherTransform1, prevFisherTransform1 - prevFisherTransform2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Eft", fisherTransformList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = fisherTransformList;
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
        List<double> v1List = new();
        List<double> inverseFisherTransformList = new();
        List<Signal> signalsList = new();

        var rsiList = CalculateRelativeStrengthIndex(stockData, maType, length: length1).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentRsi = rsiList[i];

            var v1 = 0.1 * (currentRsi - 50);
            v1List.AddRounded(v1);
        }

        var v2List = GetMovingAverageList(stockData, maType, length2, v1List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var v2 = v2List[i];
            var prevIft1 = i >= 1 ? inverseFisherTransformList[i - 1] : 0;
            var prevIft2 = i >= 2 ? inverseFisherTransformList[i - 2] : 0;
            var bottom = Exp(2 * v2) + 1;

            var inverseFisherTransform = bottom != 0 ? MinOrMax((Exp(2 * v2) - 1) / bottom, 1, -1) : 0;
            inverseFisherTransformList.AddRounded(inverseFisherTransform);

            var signal = GetRsiSignal(inverseFisherTransform - prevIft1, prevIft1 - prevIft2, inverseFisherTransform, prevIft1, 0.5, -0.5);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Eift", inverseFisherTransformList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = inverseFisherTransformList;
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
        List<double> itList = new();
        List<double> lagList = new();
        List<Signal> signalsList = new();
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
            itList.AddRounded(it);

            var prevLag = lagList.LastOrDefault();
            var lag = (2 * it) - prevIt2;
            lagList.AddRounded(lag);

            var signal = GetCompareSignal(lag - it, prevLag - prevIt1);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Eit", itList },
            { "Signal", lagList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = itList;
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
        List<double> itList = new();
        List<double> trendLineList = new();
        List<Signal> signalsList = new();
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
            itList.AddRounded(iTrend);

            var prevTrendLine = trendLineList.LastOrDefault();
            var trendLine = ((4 * iTrend) + (3 * prevIt1) + (2 * prevIt2) + prevIt3) / 10;
            trendLineList.AddRounded(trendLine);

            var signal = GetCompareSignal(currentValue - trendLine, prevVal - prevTrendLine);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Eit", itList },
            { "Signal", trendLineList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = itList;
        stockData.IndicatorName = IndicatorName.EhlersInstantaneousTrendlineV1;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Laguerre Relative Strength Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="gamma"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersLaguerreRelativeStrengthIndex(this StockData stockData, double gamma = 0.5)
    {
        List<double> laguerreRsiList = new();
        List<double> l0List = new();
        List<double> l1List = new();
        List<double> l2List = new();
        List<double> l3List = new();
        List<double> cuList = new();
        List<double> cdList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevL0 = i >= 1 ? l0List.LastOrDefault() : currentValue;
            var prevL1 = i >= 1 ? l1List.LastOrDefault() : currentValue;
            var prevL2 = i >= 1 ? l2List.LastOrDefault() : currentValue;
            var prevL3 = i >= 1 ? l3List.LastOrDefault() : currentValue;
            var prevRsi1 = i >= 1 ? laguerreRsiList[i - 1] : 0;
            var prevRsi2 = i >= 2 ? laguerreRsiList[i - 2] : 0;

            var l0 = ((1 - gamma) * currentValue) + (gamma * prevL0);
            l0List.AddRounded(l0);

            var l1 = (-1 * gamma * l0) + prevL0 + (gamma * prevL1);
            l1List.AddRounded(l1);

            var l2 = (-1 * gamma * l1) + prevL1 + (gamma * prevL2);
            l2List.AddRounded(l2);

            var l3 = (-1 * gamma * l2) + prevL2 + (gamma * prevL3);
            l3List.AddRounded(l3);

            var cu = (l0 >= l1 ? l0 - l1 : 0) + (l1 >= l2 ? l1 - l2 : 0) + (l2 >= l3 ? l2 - l3 : 0);
            cuList.AddRounded(cu);

            var cd = (l0 >= l1 ? 0 : l1 - l0) + (l1 >= l2 ? 0 : l2 - l1) + (l2 >= l3 ? 0 : l3 - l2);
            cdList.AddRounded(cd);

            var laguerreRsi = cu + cd != 0 ? MinOrMax(cu / (cu + cd), 1, 0) : 0;
            laguerreRsiList.AddRounded(laguerreRsi);

            var signal = GetRsiSignal(laguerreRsi - prevRsi1, prevRsi1 - prevRsi2, laguerreRsi, prevRsi1, 0.8, 0.2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Elrsi", laguerreRsiList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = laguerreRsiList;
        stockData.IndicatorName = IndicatorName.EhlersLaguerreRelativeStrengthIndex;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Laguerre Relative Strength Index With Self Adjusting Alpha
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersLaguerreRelativeStrengthIndexWithSelfAdjustingAlpha(this StockData stockData, int length = 13)
    {
        List<double> laguerreRsiList = new();
        List<double> ratioList = new();
        List<double> l0List = new();
        List<double> l1List = new();
        List<double> l2List = new();
        List<double> l3List = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, openList, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentOpen = openList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var highestHigh = highestList[i];
            var lowestLow = lowestList[i];
            var prevRsi1 = i >= 1 ? laguerreRsiList[i - 1] : 0;
            var prevRsi2 = i >= 2 ? laguerreRsiList[i - 2] : 0;
            var oc = (currentOpen + prevValue) / 2;
            var hc = Math.Max(currentHigh, prevValue);
            var lc = Math.Min(currentLow, prevValue);
            var feValue = (oc + hc + lc + currentValue) / 4;

            var ratio = highestHigh - lowestLow != 0 ? (hc - lc) / (highestHigh - lowestLow) : 0;
            ratioList.AddRounded(ratio);

            var ratioSum = ratioList.TakeLastExt(length).Sum();
            var alpha = ratioSum > 0 ? MinOrMax(Math.Log(ratioSum) / Math.Log(length), 0.99, 0.01) : 0.01;
            var prevL0 = l0List.LastOrDefault();
            var l0 = (alpha * feValue) + ((1 - alpha) * prevL0);
            l0List.AddRounded(l0);

            var prevL1 = l1List.LastOrDefault();
            var l1 = (-(1 - alpha) * l0) + prevL0 + ((1 - alpha) * prevL1);
            l1List.AddRounded(l1);

            var prevL2 = l2List.LastOrDefault();
            var l2 = (-(1 - alpha) * l1) + prevL1 + ((1 - alpha) * prevL2);
            l2List.AddRounded(l2);

            var prevL3 = l3List.LastOrDefault();
            var l3 = (-(1 - alpha) * l2) + prevL2 + ((1 - alpha) * prevL3);
            l3List.AddRounded(l3);

            var cu = (l0 >= l1 ? l0 - l1 : 0) + (l1 >= l2 ? l1 - l2 : 0) + (l2 >= l3 ? l2 - l3 : 0);
            var cd = (l0 >= l1 ? 0 : l1 - l0) + (l1 >= l2 ? 0 : l2 - l1) + (l2 >= l3 ? 0 : l3 - l2);
            var laguerreRsi = cu + cd != 0 ? MinOrMax(cu / (cu + cd), 1, 0) : 0;
            laguerreRsiList.AddRounded(laguerreRsi);

            var signal = GetRsiSignal(laguerreRsi - prevRsi1, prevRsi1 - prevRsi2, laguerreRsi, prevRsi1, 0.8, 0.2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Elrsiwsa", laguerreRsiList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = laguerreRsiList;
        stockData.IndicatorName = IndicatorName.EhlersLaguerreRelativeStrengthIndexWithSelfAdjustingAlpha;

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
        List<double> arsiList = new();
        List<double> arsiEmaList = new();
        List<Signal> signalsList = new();
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
            arsiList.AddRounded(arsi);

            var arsiEma = CalculateEMA(arsi, prevArsi1, (int)Math.Ceiling(sp));
            arsiEmaList.AddRounded(arsiEma);

            var signal = GetRsiSignal(arsiEma - prevArsi1, prevArsi1 - prevArsi2, arsiEma, prevArsi1, 70, 30);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Earsi", arsiList },
            { "Signal", arsiEmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = arsiList;
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
        List<double> fishList = new();
        List<Signal> signalsList = new();

        var arsiList = CalculateEhlersAdaptiveRelativeStrengthIndexV1(stockData).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var arsi = arsiList[i] / 100;
            var prevFish1 = i >= 1 ? fishList[i - 1] : 0;
            var prevFish2 = i >= 2 ? fishList[i - 2] : 0;
            var tranRsi = 2 * (arsi - 0.5);
            var ampRsi = MinOrMax(1.5 * tranRsi, 0.999, -0.999);

            var fish = 0.5 * Math.Log((1 + ampRsi) / (1 - ampRsi));
            fishList.AddRounded(fish);

            var signal = GetCompareSignal(fish - prevFish1, prevFish1 - prevFish2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Earsift", fishList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = fishList;
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
        List<double> astocList = new();
        List<double> astocEmaList = new();
        List<Signal> signalsList = new();
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
            astocList.AddRounded(astoc);

            var astocEma = CalculateEMA(astoc, prevAstoc1, length);
            astocEmaList.AddRounded(astocEma);

            var signal = GetRsiSignal(astocEma - prevAstoc1, prevAstoc1 - prevAstoc2, astocEma, prevAstoc1, 70, 30);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Easi", astocList },
            { "Signal", astocEmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = astocList;
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
        List<double> acciList = new();
        List<double> acciEmaList = new();
        List<Signal> signalsList = new();
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
            acciList.AddRounded(acci);

            var acciEma = CalculateEMA(acci, prevAcci1, (int)Math.Ceiling(sp));
            acciEmaList.AddRounded(acciEma);

            var signal = GetRsiSignal(acciEma - prevAcci1, prevAcci1 - prevAcci2, acciEma, prevAcci1, 100, -100);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Eacci", acciList },
            { "Signal", acciEmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = acciList;
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
        List<double> v1List = new();
        List<double> iFishList = new();
        List<Signal> signalsList = new();

        var cciList = CalculateCommodityChannelIndex(stockData, inputName, maType, length, constant).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var cci = cciList[i];

            var v1 = 0.1 * (cci - 50);
            v1List.AddRounded(v1);
        }

        var v2List = GetMovingAverageList(stockData, maType, signalLength, v1List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var v2 = v2List[i];
            var expValue = Exp(2 * v2);
            var prevIFish1 = i >= 1 ? iFishList[i - 1] : 0;
            var prevIFish2 = i >= 2 ? iFishList[i - 2] : 0;

            var iFish = expValue + 1 != 0 ? (expValue - 1) / (expValue + 1) : 0;
            iFishList.AddRounded(iFish);

            var signal = GetRsiSignal(iFish - prevIFish1, prevIFish1 - prevIFish2, iFish, prevIFish1, 0.5, -0.5);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Eiftcci", iFishList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = iFishList;
        stockData.IndicatorName = IndicatorName.EhlersCommodityChannelIndexInverseFisherTransform;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Relative Strength Index Inverse Fisher Transform
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersRelativeStrengthIndexInverseFisherTransform(this StockData stockData, 
        MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 14, int signalLength = 9)
    {
        List<double> v1List = new();
        List<double> iFishList = new();
        List<Signal> signalsList = new();

        var rsiList = CalculateRelativeStrengthIndex(stockData, maType, length: length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var rsi = rsiList[i];

            var v1 = 0.1 * (rsi - 50);
            v1List.AddRounded(v1);
        }

        var v2List = GetMovingAverageList(stockData, maType, signalLength, v1List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var v2 = v2List[i];
            var expValue = Exp(2 * v2);
            var prevIfish1 = i >= 1 ? iFishList[i - 1] : 0;
            var prevIfish2 = i >= 2 ? iFishList[i - 2] : 0;

            var iFish = expValue + 1 != 0 ? MinOrMax((expValue - 1) / (expValue + 1), 1, -1) : 0;
            iFishList.AddRounded(iFish);

            var signal = GetRsiSignal(iFish - prevIfish1, prevIfish1 - prevIfish2, iFish, prevIfish1, 0.5, -0.5);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Eiftrsi", iFishList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = iFishList;
        stockData.IndicatorName = IndicatorName.EhlersRelativeStrengthIndexInverseFisherTransform;

        return stockData;
    }
}