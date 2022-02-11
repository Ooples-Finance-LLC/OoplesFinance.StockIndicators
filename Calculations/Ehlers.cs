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
        List<decimal> highPassList = new();
        List<decimal> roofingFilterList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        decimal alphaArg = Math.Min(0.707m * 2 * Pi / upperLength, 0.99m);
        decimal alphaCos = Cos(alphaArg);
        decimal alpha1 = alphaCos != 0 ? (alphaCos + Sin(alphaArg) - 1) / alphaCos : 0;
        decimal a1 = Exp(-1.414m * Pi / lowerLength);
        decimal b1 = 2 * a1 * Cos(Math.Min(1.414m * Pi / lowerLength, 0.99m));
        decimal c2 = b1;
        decimal c3 = -1 * a1 * a1;
        decimal c1 = 1 - c2 - c3;

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal currentValue = inputList.ElementAtOrDefault(i);
            decimal prevValue1 = i >= 1 ? inputList.ElementAtOrDefault(i - 1) : 0;
            decimal prevValue2 = i >= 2 ? inputList.ElementAtOrDefault(i - 2) : 0;
            decimal prevFilter1 = i >= 1 ? roofingFilterList.ElementAtOrDefault(i - 1) : 0;
            decimal prevFilter2 = i >= 2 ? roofingFilterList.ElementAtOrDefault(i - 2) : 0;
            decimal prevHp1 = i >= 1 ? highPassList.ElementAtOrDefault(i - 1) : 0;
            decimal prevHp2 = i >= 2 ? highPassList.ElementAtOrDefault(i - 2) : 0;
            decimal test1 = Pow((1 - alpha1) / 2, 2);
            decimal test2 = currentValue - (2 * prevValue1) + prevValue2;
            decimal v1 = test1 * test2;
            decimal v2 = 2 * (1 - alpha1) * prevHp1;
            decimal v3 = Pow(1 - alpha1, 2) * prevHp2;

            decimal highPass = v1 + v2 - v3;
            highPassList.Add(highPass);

            decimal prevRoofingFilter1 = i >= 1 ? roofingFilterList.ElementAtOrDefault(i - 1) : 0;
            decimal prevRoofingFilter2 = i >= 2 ? roofingFilterList.ElementAtOrDefault(i - 2) : 0;
            decimal roofingFilter = (c1 * ((highPass + prevHp1) / 2)) + (c2 * prevFilter1) + (c3 * prevFilter2);
            roofingFilterList.Add(roofingFilter);

            var signal = GetCompareSignal(roofingFilter - prevRoofingFilter1, prevRoofingFilter1 - prevRoofingFilter2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> phaseList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal realPart = 0, imagPart = 0;
            for (int j = 0; j < length; j++)
            {
                decimal weight = i >= j ? inputList.ElementAtOrDefault(i - j) : 0;
                realPart += Cos(2 * Pi * (decimal)j / length) * weight;
                imagPart += Sin(2 * Pi * (decimal)j / length) * weight;
            }

            decimal phase = Math.Abs(realPart) > 0.001m ? Atan(imagPart / realPart).ToDegrees() : 90 * Math.Sign(imagPart);
            phase = realPart < 0 ? phase + 180 : phase;
            phase += 90;
            phase = phase < 0 ? phase + 360 : phase;
            phase = phase > 360 ? phase - 360 : phase;
            phaseList.AddRounded(phase);
        }

        var phaseEmaList = GetMovingAverageList(stockData, maType, length, phaseList);
        for (int i = 0; i < stockData.Count; i++)
        {
            decimal phase = phaseList.ElementAtOrDefault(i);
            decimal phaseEma = phaseEmaList.ElementAtOrDefault(i);
            decimal prevPhase = i >= 1 ? phaseList.ElementAtOrDefault(i - 1) : 0;
            decimal prevPhaseEma = i >= 1 ? phaseEmaList.ElementAtOrDefault(i - 1) : 0;

            var signal = GetCompareSignal(phase - phaseEma, prevPhase - prevPhaseEma, true);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
    public static StockData CalculateEhlersAdaptiveCyberCycle(this StockData stockData, int length = 5, decimal alpha = 0.07m)
    {
        List<decimal> ipList = new();
        List<decimal> q1List = new();
        List<decimal> i1List = new();
        List<decimal> dpList = new();
        List<decimal> pList = new();
        List<decimal> acList = new();
        List<decimal> cycleList = new();
        List<decimal> smoothList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal currentValue = inputList.ElementAtOrDefault(i);
            decimal prevValue = i >= 1 ? inputList.ElementAtOrDefault(i - 1) : 0;
            decimal prevCycle = i >= 1 ? cycleList.ElementAtOrDefault(i - 1) : 0;
            decimal prevSmooth = i >= 1 ? smoothList.ElementAtOrDefault(i - 1) : 0;
            decimal prevIp = i >= 1 ? ipList.ElementAtOrDefault(i - 1) : 0;
            decimal prevAc1 = i >= 1 ? acList.ElementAtOrDefault(i - 1) : 0;
            decimal prevI1 = i >= 1 ? i1List.ElementAtOrDefault(i - 1) : 0;
            decimal prevQ1 = i >= 1 ? q1List.ElementAtOrDefault(i - 1) : 0;
            decimal prevP = i >= 1 ? pList.ElementAtOrDefault(i - 1) : 0;
            decimal prevValue2 = i >= 2 ? inputList.ElementAtOrDefault(i - 2) : 0;
            decimal prevSmooth2 = i >= 2 ? smoothList.ElementAtOrDefault(i - 2) : 0;
            decimal prevCycle2 = i >= 2 ? cycleList.ElementAtOrDefault(i - 2) : 0;
            decimal prevAc2 = i >= 2 ? acList.ElementAtOrDefault(i - 2) : 0;
            decimal prevValue3 = i >= 3 ? inputList.ElementAtOrDefault(i - 3) : 0;
            decimal prevCycle3 = i >= 3 ? cycleList.ElementAtOrDefault(i - 3) : 0;
            decimal prevCycle4 = i >= 4 ? cycleList.ElementAtOrDefault(i - 4) : 0;
            decimal prevCycle6 = i >= 6 ? cycleList.ElementAtOrDefault(i - 6) : 0;

            decimal smooth = (currentValue + (2 * prevValue) + (2 * prevValue2) + prevValue3) / 6;
            smoothList.Add(smooth);

            decimal cycle = i < 7 ? (currentValue - (2 * prevValue) + prevValue2) / 4 : (Pow(1 - (0.5m * alpha), 2) * (smooth - (2 * prevSmooth) + prevSmooth2)) +
            (2 * (1 - alpha) * prevCycle) - (Pow(1 - alpha, 2) * prevCycle2);
            cycleList.Add(cycle);

            decimal q1 = ((0.0962m * cycle) + (0.5769m * prevCycle2) - (0.5769m * prevCycle4) - (0.0962m * prevCycle6)) * (0.5m + (0.08m * prevIp));
            q1List.Add(q1);

            decimal i1 = prevCycle3;
            i1List.Add(i1);

            decimal dp = MinOrMax(q1 != 0 && prevQ1 != 0 ? ((i1 / q1) - (prevI1 / prevQ1)) / (1 + (i1 * prevI1 / (q1 * prevQ1))) : 0, 1.1m, 0.1m);
            dpList.Add(dp);

            decimal medianDelta = dpList.TakeLastExt(length).Median();
            decimal dc = medianDelta != 0 ? (6.28318m / medianDelta) + 0.5m : 15;

            decimal ip = (0.33m * dc) + (0.67m * prevIp);
            ipList.Add(ip);

            decimal p = (0.15m * ip) + (0.85m * prevP);
            pList.Add(p);

            decimal a1 = 2 / (p + 1);
            decimal ac = i < 7 ? (currentValue - (2 * prevValue) + prevValue2) / 4 :
                (Pow(1 - (0.5m * a1), 2) * (smooth - (2 * prevSmooth) + prevSmooth2)) + (2 * (1 - a1) * prevAc1) - (Pow(1 - a1, 2) * prevAc2);
            acList.Add(ac);

            var signal = GetCompareSignal(ac - prevAc1, prevAc1 - prevAc2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
    public static StockData CalculateEhlersSimpleDecycler(this StockData stockData, int length = 125, decimal upperPct = 0.5m, decimal lowerPct = 0.5m)
    {
        List<decimal> decyclerList = new();
        List<decimal> upperBandList = new();
        List<decimal> lowerBandList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var hpList = CalculateEhlersHighPassFilterV1(stockData, length, 1).CustomValuesList;

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal currentValue = inputList.ElementAtOrDefault(i);
            decimal prevValue = i >= 1 ? inputList.ElementAtOrDefault(i - 1) : 0;
            decimal hp = hpList.ElementAtOrDefault(i);

            decimal prevDecycler = decyclerList.LastOrDefault();
            decimal decycler = currentValue - hp;
            decyclerList.Add(decycler);

            decimal upperBand = (1 + (upperPct / 100)) * decycler;
            upperBandList.Add(upperBand);

            decimal lowerBand = (1 - (lowerPct / 100)) * decycler;
            lowerBandList.Add(lowerBand);

            var signal = GetCompareSignal(currentValue - decycler, prevValue - prevDecycler);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
    public static StockData CalculateEhlersHighPassFilterV1(this StockData stockData, int length = 125, decimal mult = 1)
    {
        List<decimal> highPassList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        decimal alphaArg = MinOrMax(2 * Pi / (mult * length * Sqrt(2)), 0.99m, 0.01m);
        decimal alphaCos = Cos(alphaArg);
        decimal alpha = alphaCos != 0 ? (alphaCos + Sin(alphaArg) - 1) / alphaCos : 0;

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal currentValue = inputList.ElementAtOrDefault(i);
            decimal prevValue1 = i >= 1 ? inputList.ElementAtOrDefault(i - 1) : 0;
            decimal prevValue2 = i >= 2 ? inputList.ElementAtOrDefault(i - 2) : 0;
            decimal prevHp1 = i >= 1 ? highPassList.ElementAtOrDefault(i - 1) : 0;
            decimal prevHp2 = i >= 2 ? highPassList.ElementAtOrDefault(i - 2) : 0;
            decimal pow1 = Pow(1 - (alpha / 2), 2);
            decimal pow2 = Pow(1 - alpha, 2);

            decimal highPass = (pow1 * (currentValue - (2 * prevValue1) + prevValue2)) + (2 * (1 - alpha) * prevHp1) - (pow2 * prevHp2);
            highPassList.Add(highPass);

            var signal = GetCompareSignal(highPass, prevHp1);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        int length1 = 10, int length2 = 8, decimal obosLevel = 2, decimal mult = 1)
    {
        List<decimal> momList = new();
        List<decimal> argList = new();
        List<decimal> ssf2PoleRocketRsiList = new();
        List<decimal> ssf2PoleUpChgList = new();
        List<decimal> ssf2PoleDownChgList = new();
        List<decimal> ssf2PoleTmpList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        decimal obLevel = obosLevel * mult;
        decimal osLevel = -obosLevel * mult;

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal currentValue = inputList.ElementAtOrDefault(i);
            decimal prevValue = i >= length1 - 1 ? inputList.ElementAtOrDefault(i - (length1 - 1)) : 0;

            decimal prevMom = momList.LastOrDefault();
            decimal mom = currentValue - prevValue;
            momList.Add(mom);

            decimal arg = (mom + prevMom) / 2;
            argList.Add(arg);
        }

        var argSsf2PoleList = GetMovingAverageList(stockData, maType, length2, argList);
        for (int j = 0; j < stockData.Count; j++)
        {
            decimal ssf2Pole = argSsf2PoleList.ElementAtOrDefault(j);
            decimal prevSsf2Pole = j >= 1 ? argSsf2PoleList.ElementAtOrDefault(j - 1) : 0;
            decimal prevRocketRsi1 = j >= 1 ? ssf2PoleRocketRsiList.ElementAtOrDefault(j - 1) : 0;
            decimal prevRocketRsi2 = j >= 2 ? ssf2PoleRocketRsiList.ElementAtOrDefault(j - 2) : 0;
            decimal ssf2PoleMom = ssf2Pole - prevSsf2Pole;

            decimal up2PoleChg = ssf2PoleMom > 0 ? ssf2PoleMom : 0;
            ssf2PoleUpChgList.Add(up2PoleChg);

            decimal down2PoleChg = ssf2PoleMom < 0 ? Math.Abs(ssf2PoleMom) : 0;
            ssf2PoleDownChgList.Add(down2PoleChg);

            decimal up2PoleChgSum = ssf2PoleUpChgList.TakeLastExt(length1).Sum();
            decimal down2PoleChgSum = ssf2PoleDownChgList.TakeLastExt(length1).Sum();

            decimal prevTmp2Pole = ssf2PoleTmpList.LastOrDefault();
            decimal tmp2Pole = up2PoleChgSum + down2PoleChgSum != 0 ?
                MinOrMax((up2PoleChgSum - down2PoleChgSum) / (up2PoleChgSum + down2PoleChgSum), 0.999m, -0.999m) : prevTmp2Pole;
            ssf2PoleTmpList.Add(tmp2Pole);

            decimal ssf2PoleTempLog = 1 - tmp2Pole != 0 ? (1 + tmp2Pole) / (1 - tmp2Pole) : 0;
            decimal ssf2PoleLog = Log(ssf2PoleTempLog);
            decimal ssf2PoleRocketRsi = 0.5m * ssf2PoleLog * mult;
            ssf2PoleRocketRsiList.Add(ssf2PoleRocketRsi);

            var signal = GetRsiSignal(ssf2PoleRocketRsi - prevRocketRsi1, prevRocketRsi1 - prevRocketRsi2, ssf2PoleRocketRsi, prevRocketRsi1, obLevel, osLevel);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> corrList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal prevCorr1 = i >= 1 ? corrList.ElementAtOrDefault(i - 1) : 0;
            decimal prevCorr2 = i >= 2 ? corrList.ElementAtOrDefault(i - 2) : 0;

            decimal sx = 0, sy = 0, sxx = 0, sxy = 0, syy = 0;
            for (int j = 0; j <= length - 1; j++)
            {
                decimal x = i >= j ? inputList.ElementAtOrDefault(i - j) : 0;
                decimal y = -j;

                sx += x;
                sy += y;
                sxx += Pow(x, 2);
                sxy += x * y;
                syy += Pow(y, 2);
            }

            decimal corr = (length * sxx) - (sx * sx) > 0 && (length * syy) - (sy * sy) > 0 ? ((length * sxy) - (sx * sy)) /
                Sqrt(((length * sxx) - (sx * sx)) * ((length * syy) - (sy * sy))) : 0;
            corrList.Add(corr);

            var signal = GetRsiSignal(corr - prevCorr1, prevCorr1 - prevCorr2, corr, prevCorr1, 0.5m, -0.5m);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> rviList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, openList, _) = GetInputValuesList(stockData);

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal currentClose = inputList.ElementAtOrDefault(i);
            decimal currentOpen = openList.ElementAtOrDefault(i);
            decimal currentHigh = highList.ElementAtOrDefault(i);
            decimal currentLow = lowList.ElementAtOrDefault(i);

            decimal rvi = currentHigh - currentLow != 0 ? (currentClose - currentOpen) / (currentHigh - currentLow) : 0;
            rviList.Add(rvi);
        }

        var rviSmaList = GetMovingAverageList(stockData, maType, length, rviList);
        var rviSignalList = GetMovingAverageList(stockData, maType, signalLength, rviSmaList);
        for (int i = 0; i < stockData.Count; i++)
        {
            decimal rviSma = rviSmaList.ElementAtOrDefault(i);
            decimal prevRviSma = i >= 1 ? rviSmaList.ElementAtOrDefault(i - 1) : 0;
            decimal rviSignal = rviSignalList.ElementAtOrDefault(i);
            decimal prevRviSignal = i >= 1 ? rviSignalList.ElementAtOrDefault(i - 1) : 0;

            var signal = GetCompareSignal(rviSma - rviSignal, prevRviSma - prevRviSignal);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> cgList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal num = 0, denom = 0;
            for (int j = 0; j <= length - 1; j++)
            {
                decimal prevValue = i >= j ? inputList.ElementAtOrDefault(i - j) : 0;
                num += (1 + j) * prevValue;
                denom += prevValue;
            }

            decimal prevCg = cgList.LastOrDefault();
            decimal cg = denom != 0 ? (-num / denom) + ((decimal)(length + 1) / 2) : 0;
            cgList.Add(cg);

            var signal = GetCompareSignal(cg, prevCg);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> cgList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var pList = CalculateEhlersAdaptiveCyberCycle(stockData, length: length).OutputValues["Period"];

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal p = pList.ElementAtOrDefault(i);
            int intPeriod = (int)Math.Ceiling(p / 2);
            decimal prevCg1 = i >= 1 ? cgList.ElementAtOrDefault(i - 1) : 0;
            decimal prevCg2 = i >= 2 ? cgList.ElementAtOrDefault(i - 2) : 0;

            decimal num = 0, denom = 0;
            for (int j = 0; j <= intPeriod - 1; j++)
            {
                decimal prevPrice = i >= j ? inputList.ElementAtOrDefault(i - j) : 0;
                num += (1 + j) * prevPrice;
                denom += prevPrice;
            }

            decimal cg = denom != 0 ? (-num / denom) + ((intPeriod + 1) / 2) : 0;
            cgList.Add(cg);

            var signal = GetCompareSignal(cg - prevCg1, prevCg1 - prevCg2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> f3List = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        decimal a1 = Exp(-Pi / length2);
        decimal b1 = 2 * a1 * Cos(1.738m * Pi / length2);
        decimal c1 = Pow(a1, 2);
        decimal coef2 = b1 + c1;
        decimal coef3 = -1 * (c1 + (b1 * c1));
        decimal coef4 = c1 * c1;
        decimal coef1 = 1 - coef2 - coef3 - coef4;

        var pList = CalculateEhlersAdaptiveCyberCycle(stockData, length1).OutputValues["Period"];

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal currentValue = inputList.ElementAtOrDefault(i);
            decimal prevP = i >= 1 ? pList.ElementAtOrDefault(i - 1) : 0;
            decimal p = pList.ElementAtOrDefault(i);
            decimal prevF3_1 = i >= 1 ? f3List.ElementAtOrDefault(i - 1) : 0;
            decimal prevF3_2 = i >= 2 ? f3List.ElementAtOrDefault(i - 2) : 0;
            decimal prevF3_3 = i >= 3 ? f3List.ElementAtOrDefault(i - 3) : 0;
            int pr = (int)Math.Ceiling(Math.Abs(p - 1));
            decimal prevValue = i >= pr ? inputList.ElementAtOrDefault(i - pr) : 0;
            decimal v1 = currentValue - prevValue;

            decimal f3 = (coef1 * v1) + (coef2 * prevF3_1) + (coef3 * prevF3_2) + (coef4 * prevF3_3);
            f3List.Add(f3);
        }

        var f3EmaList = GetMovingAverageList(stockData, maType, length2, f3List);
        for (int j = 0; j < stockData.Count; j++)
        {
            decimal f3 = f3List.ElementAtOrDefault(j);
            decimal f3Ema = f3EmaList.ElementAtOrDefault(j);
            decimal prevF3 = j >= 1 ? f3List.ElementAtOrDefault(j - 1) : 0;
            decimal prevF3Ema = j >= 1 ? f3EmaList.ElementAtOrDefault(j - 1) : 0;

            var signal = GetCompareSignal(f3 - f3Ema, prevF3 - prevF3Ema);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> v1List = new();
        List<decimal> v2List = new();
        List<decimal> tList = new();
        List<Signal> signalsList = new();

        var ehlersCGOscillatorList = CalculateEhlersCenterofGravityOscillator(stockData, length).CustomValuesList;
        var (highestList, lowestList) = GetMaxAndMinValuesList(ehlersCGOscillatorList, length);

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal cg = ehlersCGOscillatorList.ElementAtOrDefault(i);
            decimal maxc = highestList.ElementAtOrDefault(i);
            decimal minc = lowestList.ElementAtOrDefault(i);
            decimal prevV1_1 = i >= 1 ? v1List.ElementAtOrDefault(i - 1) : 0;
            decimal prevV1_2 = i >= 2 ? v1List.ElementAtOrDefault(i - 2) : 0;
            decimal prevV1_3 = i >= 3 ? v1List.ElementAtOrDefault(i - 3) : 0;
            decimal prevV2_1 = i >= 1 ? v2List.ElementAtOrDefault(i - 1) : 0;
            decimal prevT1 = i >= 1 ? tList.ElementAtOrDefault(i - 1) : 0;
            decimal prevT2 = i >= 2 ? tList.ElementAtOrDefault(i - 2) : 0;

            decimal v1 = maxc - minc != 0 ? (cg - minc) / (maxc - minc) : 0;
            v1List.Add(v1);

            decimal v2_ = ((4 * v1) + (3 * prevV1_1) + (2 * prevV1_2) + prevV1_3) / 10;
            decimal v2 = 2 * (v2_ - 0.5m);
            v2List.Add(v2);

            decimal t = MinOrMax(0.96m * (prevV2_1 + 0.02m), 1, 0);
            tList.Add(t);

            var signal = GetRsiSignal(t - prevT1, prevT1 - prevT2, t, prevT1, 0.8m, 0.2m);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
    public static StockData CalculateEhlersSimpleCycleIndicator(this StockData stockData, decimal alpha = 0.07m)
    {
        List<decimal> smoothList = new();
        List<decimal> cycleList = new();
        List<decimal> cycle_List = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal currentMedianPrice = inputList.ElementAtOrDefault(i);
            decimal prevMedianPrice1 = i >= 1 ? inputList.ElementAtOrDefault(i - 1) : 0;
            decimal prevMedianPrice2 = i >= 2 ? inputList.ElementAtOrDefault(i - 2) : 0;
            decimal prevMedianPrice3 = i >= 3 ? inputList.ElementAtOrDefault(i - 3) : 0;
            decimal prevSmooth1 = smoothList.LastOrDefault();
            decimal prevCycle1 = cycle_List.LastOrDefault();
            decimal prevSmooth2 = i >= 2 ? smoothList.ElementAtOrDefault(i - 2) : 0;
            decimal prevCycle2 = i >= 2 ? cycle_List.ElementAtOrDefault(i - 2) : 0;
            decimal prevCyc1 = i >= 1 ? cycleList.ElementAtOrDefault(i - 1) : 0;
            decimal prevCyc2 = i >= 2 ? cycleList.ElementAtOrDefault(i - 2) : 0;

            decimal smooth = (currentMedianPrice + (2 * prevMedianPrice1) + (2 * prevMedianPrice2) + prevMedianPrice3) / 6;
            smoothList.Add(smooth);

            decimal cycle_ = ((1 - (0.5m * alpha)) * (1 - (0.5m * alpha)) * (smooth - (2 * prevSmooth1) + prevSmooth2)) + (2 * (1 - alpha) * prevCycle1) -
                ((1 - alpha) * (1 - alpha) * prevCycle2);
            cycle_List.Add(cycle_);

            decimal cycle = i < 7 ? (currentMedianPrice - (2 * prevMedianPrice1) + prevMedianPrice2) / 4 : cycle_;
            cycleList.Add(cycle);

            var signal = GetCompareSignal(cycle - prevCyc1, prevCyc1 - prevCyc2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        decimal fastMult = 1.2m, decimal slowMult = 1)
    {
        List<decimal> decycler1OscillatorList = new();
        List<decimal> decycler2OscillatorList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var decycler1List = CalculateEhlersSimpleDecycler(stockData, fastLength).CustomValuesList;
        var decycler2List = CalculateEhlersSimpleDecycler(stockData, slowLength).CustomValuesList;
        stockData.CustomValuesList = decycler1List;
        var decycler1FilteredList = CalculateEhlersHighPassFilterV1(stockData, fastLength, 0.5m).CustomValuesList;
        stockData.CustomValuesList = decycler2List;
        var decycler2FilteredList = CalculateEhlersHighPassFilterV1(stockData, slowLength, 0.5m).CustomValuesList;

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal currentValue = inputList.ElementAtOrDefault(i);
            decimal decycler1Filtered = decycler1FilteredList.ElementAtOrDefault(i);
            decimal decycler2Filtered = decycler2FilteredList.ElementAtOrDefault(i);

            decimal prevDecyclerOsc1 = decycler1OscillatorList.LastOrDefault();
            decimal decyclerOscillator1 = currentValue != 0 ? 100 * fastMult * decycler1Filtered / currentValue : 0;
            decycler1OscillatorList.Add(decyclerOscillator1);

            decimal prevDecyclerOsc2 = decycler2OscillatorList.LastOrDefault();
            decimal decyclerOscillator2 = currentValue != 0 ? 100 * slowMult * decycler2Filtered / currentValue : 0;
            decycler2OscillatorList.Add(decyclerOscillator2);

            var signal = GetCompareSignal(decyclerOscillator2 - decyclerOscillator1, prevDecyclerOsc2 - prevDecyclerOsc1);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
        {
            { "FastEdo", decycler1OscillatorList },
            { "SlowEdo", decycler2OscillatorList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<decimal>();
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
        List<decimal> hpList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        decimal a1 = Exp(-1.414m * Pi / length);
        decimal b1 = 2 * a1 * Cos(1.414m * Pi / length);
        decimal c2 = b1;
        decimal c3 = -a1 * a1;
        decimal c1 = (1 + c2 - c3) / 4;

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal currentValue = inputList.ElementAtOrDefault(i);
            decimal prevValue1 = i >= 1 ? inputList.ElementAtOrDefault(i - 1) : 0;
            decimal prevValue2 = i >= 2 ? inputList.ElementAtOrDefault(i - 2) : 0;
            decimal prevHp1 = i >= 1 ? hpList.ElementAtOrDefault(i - 1) : 0;
            decimal prevHp2 = i >= 2 ? hpList.ElementAtOrDefault(i - 2) : 0;

            decimal hp = i < 4 ? 0 : (c1 * (currentValue - (2 * prevValue1) + prevValue2)) + (c2 * prevHp1) + (c3 * prevHp2);
            hpList.Add(hp);
        }

        var hpMa1List = GetMovingAverageList(stockData, maType, length, hpList);
        var hpMa2List = GetMovingAverageList(stockData, maType, length, hpMa1List);
        for (int i = 0; i < stockData.Count; i++)
        {
            decimal hp = hpMa2List.ElementAtOrDefault(i);
            decimal prevHp1 = i >= 1 ? hpMa2List.ElementAtOrDefault(i - 1) : 0;
            decimal prevHp2 = i >= 2 ? hpMa2List.ElementAtOrDefault(i - 2) : 0;

            var signal = GetCompareSignal(hp - prevHp1, prevHp1 - prevHp2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> decList = new();
        List<Signal> signalsList = new();

        var hp1List = CalculateEhlersHighPassFilterV2(stockData, maType, fastLength).CustomValuesList;
        var hp2List = CalculateEhlersHighPassFilterV2(stockData, maType, slowLength).CustomValuesList;

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal hp1 = hp1List.ElementAtOrDefault(i);
            decimal hp2 = hp2List.ElementAtOrDefault(i);

            decimal prevDec = decList.LastOrDefault();
            decimal dec = hp2 - hp1;
            decList.Add(dec);

            var signal = GetCompareSignal(dec, prevDec);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> stocList = new();
        List<decimal> modStocList = new();
        List<Signal> signalsList = new();

        decimal a1 = Exp(-1.414m * Pi / length1);
        decimal b1 = 2 * a1 * Cos(Math.Min(1.414m * Pi / length1, 0.99m));
        decimal c2 = b1;
        decimal c3 = -1 * a1 * a1;
        decimal c1 = 1 - c2 - c3;

        var roofingFilterList = CalculateEhlersRoofingFilterV1(stockData, maType, length1, length2).CustomValuesList;
        var (highestList, lowestList) = GetMaxAndMinValuesList(roofingFilterList, length3);

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal highest = highestList.ElementAtOrDefault(i);
            decimal lowest = lowestList.ElementAtOrDefault(i);
            decimal roofingFilter = roofingFilterList.ElementAtOrDefault(i);
            decimal prevModStoc1 = i >= 1 ? modStocList.ElementAtOrDefault(i - 1) : 0;
            decimal prevModStoc2 = i >= 2 ? modStocList.ElementAtOrDefault(i - 2) : 0;

            decimal prevStoc = stocList.LastOrDefault();
            decimal stoc = highest - lowest != 0 ? (roofingFilter - lowest) / (highest - lowest) * 100 : 0;
            stocList.Add(stoc);

            decimal modStoc = (c1 * ((stoc + prevStoc) / 2)) + (c2 * prevModStoc1) + (c3 * prevModStoc2);
            modStocList.Add(modStoc);

            var signal = GetRsiSignal(modStoc - prevModStoc1, prevModStoc1 - prevModStoc2, modStoc, prevModStoc1, 70, 30);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> upChgList = new();
        List<decimal> upChgSumList = new();
        List<decimal> denomList = new();
        List<decimal> mrsiList = new();
        List<decimal> mrsiSigList = new();
        List<Signal> signalsList = new();

        decimal a1 = Exp(-1.414m * Pi / length2);
        decimal b1 = 2 * a1 * Cos(Math.Min(1.414m * Pi / length2, 0.99m));
        decimal c2 = b1;
        decimal c3 = -1 * a1 * a1;
        decimal c1 = 1 - c2 - c3;

        var roofingFilterList = CalculateEhlersRoofingFilterV2(stockData, length1, length2).CustomValuesList;

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal roofingFilter = roofingFilterList.ElementAtOrDefault(i);
            decimal prevRoofingFilter = i >= 1 ? roofingFilterList.ElementAtOrDefault(i - 1) : 0;
            decimal prevMrsi1 = i >= 1 ? mrsiList.ElementAtOrDefault(i - 1) : 0;
            decimal prevMrsi2 = i >= 2 ? mrsiList.ElementAtOrDefault(i - 2) : 0;
            decimal prevMrsiSig1 = i >= 1 ? mrsiSigList.ElementAtOrDefault(i - 1) : 0;
            decimal prevMrsiSig2 = i >= 2 ? mrsiSigList.ElementAtOrDefault(i - 2) : 0;

            decimal upChg = roofingFilter > prevRoofingFilter ? roofingFilter - prevRoofingFilter : 0;
            upChgList.Add(upChg);

            decimal dnChg = roofingFilter < prevRoofingFilter ? prevRoofingFilter - roofingFilter : 0;
            decimal prevUpChgSum = upChgSumList.LastOrDefault();
            decimal upChgSum = upChgList.TakeLastExt(length3).Sum();
            upChgSumList.Add(upChgSum);

            decimal prevDenom = denomList.LastOrDefault();
            decimal denom = upChg + dnChg;
            denomList.Add(denom);

            decimal mrsi = denom != 0 && prevDenom != 0 ? (c1 * (((upChgSum / denom) + (prevUpChgSum / prevDenom)) / 2)) + (c2 * prevMrsi1) + (c3 * prevMrsi2) : 0;
            mrsiList.Add(mrsi);

            decimal mrsiSig = (c1 * ((mrsi + prevMrsi1) / 2)) + (c2 * prevMrsiSig1) + (c3 * prevMrsiSig2);
            mrsiSigList.Add(mrsiSig);

            var signal = GetRsiSignal(mrsi - mrsiSig, prevMrsi1 - prevMrsiSig1, mrsi, prevMrsi1, 0.7m, 0.3m);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> highPassList = new();
        List<decimal> roofingFilterList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        decimal alphaArg = Math.Min(2 * Pi / length1, 0.99m);
        decimal alphaCos = Cos(alphaArg);
        decimal alpha1 = alphaCos != 0 ? (alphaCos + Sin(alphaArg) - 1) / alphaCos : 0;
        decimal a1 = Exp(-1.414m * Pi / length2);
        decimal b1 = 2 * a1 * Cos(Math.Min(1.414m * Pi / length2, 0.99m));
        decimal c2 = b1;
        decimal c3 = -1 * a1 * a1;
        decimal c1 = 1 - c2 - c3;

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal currentValue = inputList.ElementAtOrDefault(i);
            decimal prevValue1 = i >= 1 ? inputList.ElementAtOrDefault(i - 1) : 0;
            decimal prevFilter1 = i >= 1 ? roofingFilterList.ElementAtOrDefault(i - 1) : 0;
            decimal prevFilter2 = i >= 2 ? roofingFilterList.ElementAtOrDefault(i - 2) : 0;
            decimal prevHp1 = i >= 1 ? highPassList.ElementAtOrDefault(i - 1) : 0;

            decimal hp = ((1 - (alpha1 / 2)) * (currentValue - prevValue1)) + ((1 - alpha1) * prevHp1);
            highPassList.Add(hp);

            decimal filter = (c1 * ((hp + prevHp1) / 2)) + (c2 * prevFilter1) + (c3 * prevFilter2);
            roofingFilterList.Add(filter);

            var signal = GetCompareSignal(filter - prevFilter1, prevFilter1 - prevFilter2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> decList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        decimal alphaArg = Math.Min(2 * Pi / length, 0.99m);
        decimal alphaCos = Cos(alphaArg);
        decimal alpha1 = alphaCos != 0 ? (alphaCos + Sin(alphaArg) - 1) / alphaCos : 0;

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal currentValue = inputList.ElementAtOrDefault(i);
            decimal prevValue1 = i >= 1 ? inputList.ElementAtOrDefault(i - 1) : 0;

            decimal prevDec = decList.LastOrDefault();
            decimal dec = (alpha1 / 2 * (currentValue + prevValue1)) + ((1 - alpha1) * prevDec);
            decList.Add(dec);

            var signal = GetCompareSignal(currentValue - dec, prevValue1 - prevDec);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> zmrFilterList = new();
        List<Signal> signalsList = new();

        decimal alphaArg = Math.Min(2 * Pi / length1, 0.99m);
        decimal alphaCos = Cos(alphaArg);
        decimal alpha1 = alphaCos != 0 ? (alphaCos + Sin(alphaArg) - 1) / alphaCos : 0;

        var roofingFilterList = CalculateEhlersHpLpRoofingFilter(stockData, length1, length2).CustomValuesList;

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal currentRf = roofingFilterList.ElementAtOrDefault(i);
            decimal prevRf = i >= 1 ? roofingFilterList.ElementAtOrDefault(i - 1) : 0;
            decimal prevZmrFilt1 = i >= 1 ? zmrFilterList.ElementAtOrDefault(i - 1) : 0;
            decimal prevZmrFilt2 = i >= 2 ? zmrFilterList.ElementAtOrDefault(i - 2) : 0;

            decimal zmrFilt = ((1 - (alpha1 / 2)) * (currentRf - prevRf)) + ((1 - alpha1) * prevZmrFilt1);
            zmrFilterList.Add(zmrFilt);

            var signal = GetCompareSignal(zmrFilt - prevZmrFilt1, prevZmrFilt1 - prevZmrFilt2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> highPassList = new();
        List<decimal> roofingFilterList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        decimal alphaArg = Math.Min(0.707m * 2 * Pi / length1, 0.99m);
        decimal alphaCos = Cos(alphaArg);
        decimal a1 = alphaCos != 0 ? (alphaCos + Sin(alphaArg) - 1) / alphaCos : 0;
        decimal a2 = Exp(-1.414m * Pi / length2);
        decimal b1 = 2 * a2 * Cos(Math.Min(1.414m * Pi / length2, 0.99m));
        decimal c2 = b1;
        decimal c3 = -a2 * a2;
        decimal c1 = 1 - c2 - c3;

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal currentValue = inputList.ElementAtOrDefault(i);
            decimal prevValue1 = i >= 1 ? inputList.ElementAtOrDefault(i - 1) : 0;
            decimal prevValue2 = i >= 2 ? inputList.ElementAtOrDefault(i - 2) : 0;
            decimal prevFilter1 = i >= 1 ? roofingFilterList.ElementAtOrDefault(i - 1) : 0;
            decimal prevFilter2 = i >= 2 ? roofingFilterList.ElementAtOrDefault(i - 2) : 0;
            decimal prevHp1 = i >= 1 ? highPassList.ElementAtOrDefault(i - 1) : 0;
            decimal prevHp2 = i >= 2 ? highPassList.ElementAtOrDefault(i - 2) : 0;

            decimal hp = (Pow(1 - (a1 / 2), 2) * (currentValue - (2 * prevValue1) + prevValue2)) + (2 * (1 - a1) * prevHp1) - (Pow(1 - a1, 2) * prevHp2);
            highPassList.Add(hp);

            decimal filter = (c1 * ((hp + prevHp1) / 2)) + (c2 * prevFilter1) + (c3 * prevFilter2);
            roofingFilterList.Add(filter);

            var signal = GetCompareSignal(filter - prevFilter1, prevFilter1 - prevFilter2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> dimenList = new();
        List<decimal> hurstList = new();
        List<decimal> smoothHurstList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        int hLength = (int)Math.Ceiling((decimal)length1 / 2);
        decimal a1 = Exp(-1.414m * Pi / length2);
        decimal b1 = 2 * a1 * Cos(Math.Min(1.414m * Pi / length2, 0.99m));
        decimal c2 = b1;
        decimal c3 = -a1 * a1;
        decimal c1 = 1 - c2 - c3;

        var (hh3List, ll3List) = GetMaxAndMinValuesList(inputList, length1);
        var (hh1List, ll1List) = GetMaxAndMinValuesList(inputList, hLength);

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal hh3 = hh3List.ElementAtOrDefault(i);
            decimal ll3 = ll3List.ElementAtOrDefault(i);
            decimal hh1 = hh1List.ElementAtOrDefault(i);
            decimal ll1 = ll1List.ElementAtOrDefault(i);
            decimal currentValue = inputList.ElementAtOrDefault(i);
            decimal priorValue = i >= hLength ? inputList.ElementAtOrDefault(i - hLength) : currentValue;
            decimal prevSmoothHurst1 = i >= 1 ? smoothHurstList.ElementAtOrDefault(i - 1) : 0;
            decimal prevSmoothHurst2 = i >= 2 ? smoothHurstList.ElementAtOrDefault(i - 2) : 0;
            decimal n3 = (hh3 - ll3) / length1;
            decimal n1 = (hh1 - ll1) / hLength;
            decimal hh2 = i >= hLength ? priorValue : currentValue;
            decimal ll2 = i >= hLength ? priorValue : currentValue;

            for (int j = hLength; j < length1; j++)
            {
                decimal price = i >= j ? inputList.ElementAtOrDefault(i - j) : 0;
                hh2 = price > hh2 ? price : hh2;
                ll2 = price < ll2 ? price : ll2;
            }
            decimal n2 = (hh2 - ll2) / hLength;

            decimal prevDimen = dimenList.LastOrDefault();
            decimal dimen = 0.5m * (((Log(n1 + n2) - Log(n3)) / Log(2)) + prevDimen);
            dimenList.Add(dimen);

            decimal prevHurst = hurstList.LastOrDefault();
            decimal hurst = 2 - dimen;
            hurstList.Add(hurst);

            decimal smoothHurst = (c1 * ((hurst + prevHurst) / 2)) + (c2 * prevSmoothHurst1) + (c3 * prevSmoothHurst2);
            smoothHurstList.Add(smoothHurst);

            var signal = GetCompareSignal(smoothHurst - prevSmoothHurst1, prevSmoothHurst1 - prevSmoothHurst2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> filterList = new();
        List<decimal> msList = new();
        List<decimal> reflexList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        decimal a1 = Exp(-1.414m * Pi / 0.5m * length);
        decimal b1 = 2 * a1 * Cos(1.414m * Pi / 0.5m * length);
        decimal c2 = b1;
        decimal c3 = -a1 * a1;
        decimal c1 = 1 - c2 - c3;

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal currentValue = inputList.ElementAtOrDefault(i);
            decimal prevValue = i >= 1 ? inputList.ElementAtOrDefault(i - 1) : 0;
            decimal prevFilter1 = filterList.LastOrDefault();
            decimal prevFilter2 = i >= 2 ? filterList.ElementAtOrDefault(i - 2) : 0;
            decimal priorFilter = i >= length ? filterList.ElementAtOrDefault(i - length) : 0;
            decimal prevReflex1 = i >= 1 ? reflexList.ElementAtOrDefault(i - 1) : 0;
            decimal prevReflex2 = i >= 2 ? reflexList.ElementAtOrDefault(i - 2) : 0;

            decimal filter = (c1 * ((currentValue + prevValue) / 2)) + (c2 * prevFilter1) + (c3 * prevFilter2);
            filterList.Add(filter);

            decimal slope = length != 0 ? (priorFilter - filter) / length : 0;
            decimal sum = 0;
            for (int j = 1; j <= length; j++)
            {
                decimal prevFilterCount = i >= j ? filterList.ElementAtOrDefault(i - j) : 0;
                sum += filter + (j * slope) - prevFilterCount;
            }
            sum /= length;

            decimal prevMs = msList.LastOrDefault();
            decimal ms = (0.04m * sum * sum) + (0.96m * prevMs);
            msList.Add(ms);

            decimal reflex = ms > 0 ? sum / Sqrt(ms) : 0;
            reflexList.Add(reflex);

            var signal = GetCompareSignal(reflex - prevReflex1, prevReflex1 - prevReflex2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> dcList = new();
        List<decimal> domCycList = new();
        List<decimal> realList = new();
        List<decimal> imagList = new();
        List<decimal> q1List = new();
        List<decimal> hpList = new();
        List<decimal> smoothHpList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        decimal twoPiPer = MinOrMax(2 * Pi / length1, 0.99m, 0.01m);
        decimal alpha1 = (1 - Sin(twoPiPer)) / Cos(twoPiPer);

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal currentValue = inputList.ElementAtOrDefault(i);
            decimal prevValue = i >= 1 ? inputList.ElementAtOrDefault(i - 1) : 0;
            decimal delta = Math.Max((-0.015m * i) + 0.5m, 0.15m);
            decimal prevHp1 = i >= 1 ? hpList.ElementAtOrDefault(i - 1) : 0;
            decimal prevHp2 = i >= 2 ? hpList.ElementAtOrDefault(i - 2) : 0;
            decimal prevHp3 = i >= 3 ? hpList.ElementAtOrDefault(i - 3) : 0;
            decimal prevHp4 = i >= 4 ? hpList.ElementAtOrDefault(i - 4) : 0;
            decimal prevHp5 = i >= 5 ? hpList.ElementAtOrDefault(i - 5) : 0;

            decimal hp = i < 7 ? currentValue : (0.5m * (1 + alpha1) * (currentValue - prevValue)) + (alpha1 * prevHp1);
            hpList.Add(hp);

            decimal prevSmoothHp = smoothHpList.LastOrDefault();
            decimal smoothHp = i < 7 ? currentValue - prevValue : (hp + (2 * prevHp1) + (3 * prevHp2) + (3 * prevHp3) + (2 * prevHp4) + prevHp5) / 12;
            smoothHpList.Add(smoothHp);

            decimal num = 0, denom = 0, dc = 0, real = 0, imag = 0, q1 = 0, maxAmpl = 0;
            for (int j = minLength; j <= maxLength; j++)
            {
                decimal beta = Cos(MinOrMax(2 * Pi / j, 0.99m, 0.01m));
                decimal gamma = 1 / Cos(MinOrMax(4 * Pi * delta / j, 0.99m, 0.01m));
                decimal alpha = gamma - Sqrt((gamma * gamma) - 1);
                decimal priorSmoothHp = i >= j ? smoothHpList.ElementAtOrDefault(i - j) : 0;
                decimal prevReal = i >= j ? realList.ElementAtOrDefault(i - j) : 0;
                decimal priorReal = i >= j * 2 ? realList.ElementAtOrDefault(i - (j * 2)) : 0;
                decimal prevImag = i >= j ? imagList.ElementAtOrDefault(i - j) : 0;
                decimal priorImag = i >= j * 2 ? imagList.ElementAtOrDefault(i - (j * 2)) : 0;
                decimal prevQ1 = i >= j ? q1List.ElementAtOrDefault(i - j) : 0;

                q1 = j / Pi * 2 * (smoothHp - prevSmoothHp);
                real = (0.5m * (1 - alpha) * (smoothHp - priorSmoothHp)) + (beta * (1 + alpha) * prevReal) - (alpha * priorReal);
                imag = (0.5m * (1 - alpha) * (q1 - prevQ1)) + (beta * (1 + alpha) * prevImag) - (alpha * priorImag);
                decimal ampl = (real * real) + (imag * imag);
                maxAmpl = ampl > maxAmpl ? ampl : maxAmpl;
                decimal db = maxAmpl != 0 && ampl / maxAmpl > 0 ? -length2 * Log(0.01m / (1 - (0.99m * ampl / maxAmpl))) / Log(length2) : 0;
                db = db > maxLength ? maxLength : db;
                num += db <= 3 ? j * (maxLength - db) : 0;
                denom += db <= 3 ? maxLength - db : 0;
                dc = denom != 0 ? num / denom : 0;
            }
            q1List.Add(q1);
            realList.Add(real);
            imagList.Add(imag);
            dcList.Add(dc);

            decimal domCyc = dcList.TakeLastExt(length2).Median();
            domCycList.Add(domCyc);

            var signal = GetCompareSignal(smoothHp, prevSmoothHp);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> v1List = new();
        List<decimal> v2List = new();
        List<decimal> hpList = new();
        List<decimal> smoothHpList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        decimal twoPiPer = MinOrMax(2 * Pi / length1, 0.99m, 0.01m);
        decimal alpha1 = (1 - Sin(twoPiPer)) / Cos(twoPiPer);

        var domCycList = CalculateEhlersSpectrumDerivedFilterBank(stockData, minLength, maxLength, length1, length2).CustomValuesList;

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal domCyc = domCycList.ElementAtOrDefault(i);
            decimal beta = Cos(MinOrMax(2 * Pi / domCyc, 0.99m, 0.01m));
            decimal delta = Math.Max((-0.015m * i) + 0.5m, 0.15m);
            decimal gamma = 1 / Cos(MinOrMax(4 * Pi * (delta / domCyc), 0.99m, 0.01m));
            decimal alpha = gamma - Sqrt((gamma * gamma) - 1);
            decimal currentValue = inputList.ElementAtOrDefault(i);
            decimal prevValue = i >= 1 ? inputList.ElementAtOrDefault(i - 1) : 0;
            decimal prevHp1 = i >= 1 ? hpList.ElementAtOrDefault(i - 1) : 0;
            decimal prevHp2 = i >= 2 ? hpList.ElementAtOrDefault(i - 2) : 0;
            decimal prevHp3 = i >= 3 ? hpList.ElementAtOrDefault(i - 3) : 0;
            decimal prevHp4 = i >= 4 ? hpList.ElementAtOrDefault(i - 4) : 0;
            decimal prevHp5 = i >= 5 ? hpList.ElementAtOrDefault(i - 5) : 0;

            decimal hp = i < 7 ? currentValue : (0.5m * (1 + alpha1) * (currentValue - prevValue)) + (alpha1 * prevHp1);
            hpList.Add(hp);

            decimal prevSmoothHp = smoothHpList.LastOrDefault();
            decimal smoothHp = i < 7 ? currentValue - prevValue : (hp + (2 * prevHp1) + (3 * prevHp2) + (3 * prevHp3) + (2 * prevHp4) + prevHp5) / 12;
            smoothHpList.Add(smoothHp);

            decimal prevV1 = i >= 1 ? v1List.ElementAtOrDefault(i - 1) : 0;
            decimal prevV1_2 = i >= 2 ? v1List.ElementAtOrDefault(i - 2) : 0;
            decimal v1 = (0.5m * (1 - alpha) * (smoothHp - prevSmoothHp)) + (beta * (1 + alpha) * prevV1) - (alpha * prevV1_2);
            v1List.Add(v1);

            decimal v2 = domCyc / Pi * 2 * (v1 - prevV1);
            v2List.Add(v2);

            var signal = GetConditionSignal(v2 > v1 && v2 >= 0, v2 < v1 || v2 < 0);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
        {
            { "V1", v1List },
            { "V2", v2List }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<decimal>();
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
        List<decimal> rpiList = new();
        List<Signal> signalsList = new();
        var (_, _, _, _, volumeList) = GetInputValuesList(stockData);

        var domCycList = CalculateEhlersSpectrumDerivedFilterBank(stockData, minLength, maxLength, length1, length2).CustomValuesList;

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal domCyc = domCycList.ElementAtOrDefault(i);
            decimal volume = volumeList.ElementAtOrDefault(i);

            decimal rpi = volume * Pow(MinOrMax(2 * Pi / domCyc, 0.99m, 0.01m), 2);
            rpiList.Add(rpi);
        }

        var rpiEmaList = GetMovingAverageList(stockData, maType, minLength, rpiList);
        for (int i = 0; i < stockData.Count; i++)
        {
            decimal rpi = rpiList.ElementAtOrDefault(i);
            decimal rpiEma = rpiEmaList.ElementAtOrDefault(i);
            decimal prevRpi = i >= 1 ? rpiList.ElementAtOrDefault(i - 1) : 0;
            decimal prevRpiEma = i >= 1 ? rpiEmaList.ElementAtOrDefault(i - 1) : 0;

            var signal = GetCompareSignal(rpi - rpiEma, prevRpi - prevRpiEma, true);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> filterList = new();
        List<decimal> msList = new();
        List<decimal> trendflexList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        decimal a1 = Exp(-1.414m * Pi / 0.5m * length);
        decimal b1 = 2 * a1 * Cos(1.414m * Pi / 0.5m * length);
        decimal c2 = b1;
        decimal c3 = -a1 * a1;
        decimal c1 = 1 - c2 - c3;

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal currentValue = inputList.ElementAtOrDefault(i);
            decimal prevValue = i >= 1 ? inputList.ElementAtOrDefault(i - 1) : 0;
            decimal prevFilter1 = i >= 1 ? filterList.ElementAtOrDefault(i - 1) : 0;
            decimal prevFilter2 = i >= 2 ? filterList.ElementAtOrDefault(i - 2) : 0;

            decimal filter = (c1 * ((currentValue + prevValue) / 2)) + (c2 * prevFilter1) + (c3 * prevFilter2);
            filterList.Add(filter);

            decimal sum = 0;
            for (int j = 1; j <= length; j++)
            {
                decimal prevFilterCount = i >= j ? filterList.ElementAtOrDefault(i - j) : 0;
                sum += filter - prevFilterCount;
            }
            sum /= length;

            decimal prevMs = msList.LastOrDefault();
            decimal ms = (0.04m * Pow(sum, 2)) + (0.96m * prevMs);
            msList.Add(ms);

            decimal prevTrendflex = trendflexList.LastOrDefault();
            decimal trendflex = ms > 0 ? sum / Sqrt(ms) : 0;
            trendflexList.Add(trendflex);

            var signal = GetCompareSignal(trendflex, prevTrendflex);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> realList = new();
        List<decimal> imagList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal sx = 0, sy = 0, nsy = 0, sxx = 0, syy = 0, nsyy = 0, sxy = 0, nsxy = 0;
            for (int j = 1; j <= length; j++)
            {
                decimal x = i >= j - 1 ? inputList.ElementAtOrDefault(i - (j - 1)) : 0;
                decimal v = MinOrMax(2 * Pi * ((decimal)(j - 1) / length), 0.99m, 0.01m);
                decimal y = Cos(v);
                decimal ny = -Sin(v);
                sx += x;
                sy += y;
                nsy += ny;
                sxx += Pow(x, 2);
                syy += Pow(y, 2);
                nsyy += ny * ny;
                sxy += x * y;
                nsxy += x * ny;
            }

            decimal prevReal = realList.LastOrDefault();
            decimal real = (length * sxx) - (sx * sx) > 0 && (length * syy) - (sy * sy) > 0 ? ((length * sxy) - (sx * sy)) /
                   Sqrt(((length * sxx) - (sx * sx)) * ((length * syy) - (sy * sy))) : 0;
            realList.Add(real);

            decimal prevImag = imagList.LastOrDefault();
            decimal imag = (length * sxx) - (sx * sx) > 0 && (length * nsyy) - (nsy * nsy) > 0 ? ((length * nsxy) - (sx * nsy)) /
                   Sqrt(((length * sxx) - (sx * sx)) * ((length * nsyy) - (nsy * nsy))) : 0;
            imagList.Add(imag);

            var signal = GetCompareSignal(real - imag, prevReal - prevImag);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
        {
            { "Real", realList },
            { "Imag", imagList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<decimal>();
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
        List<decimal> angleList = new();
        List<Signal> signalsList = new();

        var ecciList = CalculateEhlersCorrelationCycleIndicator(stockData, length);
        var realList = ecciList.OutputValues["Real"];
        var imagList = ecciList.OutputValues["Imag"];

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal real = realList.ElementAtOrDefault(i);
            decimal imag = imagList.ElementAtOrDefault(i);

            decimal prevAngle = i >= 1 ? angleList.ElementAtOrDefault(i - 1) : 0;
            decimal angle = imag != 0 ? 90 + Atan(real / imag).ToDegrees() : 90;
            angle = imag > 0 ? angle - 180 : angle;
            angle = prevAngle - angle < 270 && angle < prevAngle ? prevAngle : angle;
            angleList.Add(angle);

            var signal = GetCompareSignal(angle, prevAngle);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> stateList = new();
        List<Signal> signalsList = new();

        var angleList = CalculateEhlersCorrelationAngleIndicator(stockData, length).CustomValuesList;

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal angle = angleList.ElementAtOrDefault(i);
            decimal prevAngle = i >= 1 ? angleList.ElementAtOrDefault(i - 1) : 0;

            decimal prevState = stateList.LastOrDefault();
            decimal state = Math.Abs(angle - prevAngle) < 9 && angle < 0 ? -1 : Math.Abs(angle - prevAngle) < 9 && angle >= 0 ? 1 : 0;
            stateList.Add(state);

            var signal = GetCompareSignal(state, prevState);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        int length = 20, decimal delta = 0.1m)
    {
        List<decimal> bpList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        decimal beta = Math.Max(Cos(2 * Pi / length), 0.99m);
        decimal gamma = 1 / Cos(4 * Pi * delta / length);
        decimal alpha = Math.Max(gamma - Sqrt((gamma * gamma) - 1), 0.99m);

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal currentValue = inputList.ElementAtOrDefault(i);
            decimal prevValue2 = i >= 2 ? inputList.ElementAtOrDefault(i - 2) : 0;
            decimal prevBp1 = i >= 1 ? bpList.ElementAtOrDefault(i - 1) : 0;
            decimal prevBp2 = i >= 2 ? bpList.ElementAtOrDefault(i - 2) : 0;

            decimal bp = (0.5m * (1 - alpha) * (currentValue - prevValue2)) + (beta * (1 + alpha) * prevBp1) - (alpha * prevBp2);
            bpList.Add(bp);
        }

        var trendList = GetMovingAverageList(stockData, maType, length * 2, bpList);
        for (int i = 0; i < stockData.Count; i++)
        {
            decimal trend = trendList.ElementAtOrDefault(i);
            decimal prevTrend = i >= 1 ? trendList.ElementAtOrDefault(i - 1) : 0;

            var signal = GetCompareSignal(trend, prevTrend);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        int length1 = 20, int length2 = 50, decimal delta = 0.5m, decimal fraction = 0.1m)
    {
        List<decimal> peakList = new();
        List<decimal> valleyList = new();
        List<decimal> peakAvgFracList = new();
        List<decimal> valleyAvgFracList = new();
        List<Signal> signalsList = new();

        var eteList = CalculateEhlersTrendExtraction(stockData, maType, length1, delta);
        var trendList = eteList.OutputValues["Trend"];
        var bpList = eteList.OutputValues["Bp"];

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal prevBp1 = i >= 1 ? bpList.ElementAtOrDefault(i - 1) : 0;
            decimal prevBp2 = i >= 2 ? bpList.ElementAtOrDefault(i - 2) : 0;
            decimal bp = bpList.ElementAtOrDefault(i);

            decimal prevPeak = peakList.LastOrDefault();
            decimal peak = prevBp1 > bp && prevBp1 > prevBp2 ? prevBp1 : prevPeak;
            peakList.Add(peak);

            decimal prevValley = valleyList.LastOrDefault();
            decimal valley = prevBp1 < bp && prevBp1 < prevBp2 ? prevBp1 : prevValley;
            valleyList.Add(valley);
        }

        var peakAvgList = GetMovingAverageList(stockData, maType, length2, peakList);
        var valleyAvgList = GetMovingAverageList(stockData, maType, length2, valleyList);
        for (int i = 0; i < stockData.Count; i++)
        {
            decimal peakAvg = peakAvgList.ElementAtOrDefault(i);
            decimal valleyAvg = valleyAvgList.ElementAtOrDefault(i);
            decimal trend = trendList.ElementAtOrDefault(i);
            decimal prevTrend = i >= 1 ? trendList.ElementAtOrDefault(i - 1) : 0;

            decimal prevPeakAvgFrac = peakAvgFracList.LastOrDefault();
            decimal peakAvgFrac = fraction * peakAvg;
            peakAvgFracList.Add(peakAvgFrac);

            decimal prevValleyAvgFrac = valleyAvgFracList.LastOrDefault();
            decimal valleyAvgFrac = fraction * valleyAvg;
            valleyAvgFracList.Add(valleyAvgFrac);

            var signal = GetBullishBearishSignal(trend - Math.Max(peakAvgFrac, valleyAvgFrac), prevTrend - Math.Max(prevPeakAvgFrac, prevValleyAvgFrac),
                trend - Math.Min(peakAvgFrac, valleyAvgFrac), prevTrend - Math.Min(prevPeakAvgFrac, prevValleyAvgFrac));
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
        {
            { "Trend", trendList },
            { "Peak", peakAvgFracList },
            { "Valley", valleyAvgFracList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<decimal>();
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
    public static StockData CalculateEhlersEarlyOnsetTrendIndicator(this StockData stockData, int length1 = 30, int length2 = 100, decimal k = 0.85m)
    {
        List<decimal> peakList = new();
        List<decimal> quotientList = new();
        List<Signal> signalsList = new();

        var hpList = CalculateEhlersHighPassFilterV1(stockData, length2, 1).CustomValuesList;
        stockData.CustomValuesList = hpList;
        var superSmoothList = CalculateEhlersSuperSmootherFilter(stockData, length1).CustomValuesList;

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal filter = superSmoothList.ElementAtOrDefault(i);

            decimal prevPeak = peakList.LastOrDefault();
            decimal peak = Math.Abs(filter) > 0.991m * prevPeak ? Math.Abs(filter) : 0.991m * prevPeak;
            peakList.Add(peak);

            decimal ratio = peak != 0 ? filter / peak : 0;
            decimal prevQuotient = quotientList.LastOrDefault();
            decimal quotient = (k * ratio) + 1 != 0 ? (ratio + k) / ((k * ratio) + 1) : 0;
            quotientList.Add(quotient);

            var signal = GetCompareSignal(quotient, prevQuotient);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> argList = new();
        List<Signal> signalsList = new();

        var hpFilterList = CalculateEhlersHighPassFilterV1(stockData, length1, 1).CustomValuesList;

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal highPass = hpFilterList.ElementAtOrDefault(i);
            decimal prevHp1 = i >= 1 ? hpFilterList.ElementAtOrDefault(i - 1) : 0;

            decimal arg = (highPass + prevHp1) / 2;
            argList.Add(arg);
        }

        var roofingFilter2PoleList = GetMovingAverageList(stockData, maType, length2, argList);
        for (int i = 0; i < stockData.Count; i++)
        {
            decimal roofingFilter = roofingFilter2PoleList.ElementAtOrDefault(i);
            decimal prevRoofingFilter = i >= 1 ? roofingFilter2PoleList.ElementAtOrDefault(i - 1) : 0;

            var signal = GetCompareSignal(roofingFilter, prevRoofingFilter);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        int length1 = 23, int length2 = 50, decimal bw = 1.4m)
    {
        List<decimal> bpList = new();
        List<decimal> negRmsList = new();
        List<decimal> filtPowList = new();
        List<decimal> rmsList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        decimal l1 = Cos(MinOrMax(2 * Pi / 2 * length1, 0.99m, 0.01m));
        decimal g1 = Cos(MinOrMax(bw * 2 * Pi / 2 * length1, 0.99m, 0.01m));
        decimal s1 = (1 / g1) - Sqrt(1 / Pow(g1, 2) - 1);

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal currentValue = inputList.ElementAtOrDefault(i);
            decimal prevValue2 = i >= 2 ? inputList.ElementAtOrDefault(i - 2) : 0;
            decimal prevBp1 = i >= 1 ? bpList.ElementAtOrDefault(i - 1) : 0;
            decimal prevBp2 = i >= 2 ? bpList.ElementAtOrDefault(i - 2) : 0;

            decimal bp = i < 3 ? 0 : (0.5m * (1 - s1) * (currentValue - prevValue2)) + (l1 * (1 + s1) * prevBp1) - (s1 * prevBp2);
            bpList.Add(bp);
        }

        var filtList = GetMovingAverageList(stockData, maType, length1, bpList);
        for (int i = 0; i < stockData.Count; i++)
        {
            decimal filt = filtList.ElementAtOrDefault(i);
            decimal prevFilt1 = i >= 1 ? filtList.ElementAtOrDefault(i - 1) : 0;
            decimal prevFilt2 = i >= 2 ? filtList.ElementAtOrDefault(i - 2) : 0;

            decimal filtPow = Pow(filt, 2);
            filtPowList.Add(filtPow);

            decimal filtPowMa = filtPowList.TakeLastExt(length2).Average();
            decimal rms = Sqrt(filtPowMa);
            rmsList.Add(rms);

            decimal negRms = -rms;
            negRmsList.Add(negRms);

            var signal = GetCompareSignal(filt - prevFilt1, prevFilt1 - prevFilt2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        int length = 20, decimal bw = 1)
    {
        List<decimal> bpList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        int hannLength = MinOrMax((int)Math.Ceiling(length / 1.4m));
        decimal l1 = Cos(MinOrMax(2 * Pi / length, 0.99m, 0.01m));
        decimal g1 = Cos(MinOrMax(bw * 2 * Pi / length, 0.99m, 0.01m));
        decimal s1 = (1 / g1) - Sqrt(1 / Pow(g1, 2) - 1);

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal currentValue = inputList.ElementAtOrDefault(i);
            decimal prevValue2 = i >= 2 ? inputList.ElementAtOrDefault(i - 2) : 0;
            decimal prevBp1 = i >= 1 ? bpList.ElementAtOrDefault(i - 1) : 0;
            decimal prevBp2 = i >= 2 ? bpList.ElementAtOrDefault(i - 2) : 0;

            decimal bp = i < 3 ? 0 : (0.5m * (1 - s1) * (currentValue - prevValue2)) + (l1 * (1 + s1) * prevBp1) - (s1 * prevBp2);
            bpList.Add(bp);
        }

        var filtList = GetMovingAverageList(stockData, maType, hannLength, bpList);
        for (int i = 0; i < stockData.Count; i++)
        {
            decimal filt = filtList.ElementAtOrDefault(i);
            decimal prevFilt1 = i >= 1 ? filtList.ElementAtOrDefault(i - 1) : 0;
            decimal prevFilt2 = i >= 2 ? filtList.ElementAtOrDefault(i - 2) : 0;

            var signal = GetCompareSignal(filt - prevFilt1, prevFilt1 - prevFilt2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> ssfList = new();
        List<decimal> hpList = new();
        List<decimal> prePredictList = new();
        List<decimal> predictList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        decimal a1 = Exp(MinOrMax(-1.414m * Pi / upperLength, -0.01m, -0.99m));
        decimal b1 = 2 * a1 * Cos(MinOrMax(1.414m * Pi / upperLength, 0.99m, 0.01m));
        decimal c2 = b1;
        decimal c3 = -a1 * a1;
        decimal c1 = (1 + c2 - c3) / 4;
        decimal a = Exp(MinOrMax(-1.414m * Pi / lowerLength, -0.01m, -0.99m));
        decimal b = 2 * a * Cos(MinOrMax(1.414m * Pi / lowerLength, 0.99m, 0.01m));
        decimal coef2 = b;
        decimal coef3 = -a * a;
        decimal coef1 = 1 - coef2 - coef3;

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal currentValue = inputList.ElementAtOrDefault(i);
            decimal prevValue1 = i >= 1 ? inputList.ElementAtOrDefault(i - 1) : 0;
            decimal prevValue2 = i >= 2 ? inputList.ElementAtOrDefault(i - 2) : 0;
            decimal prevHp1 = i >= 1 ? hpList.ElementAtOrDefault(i - 1) : 0;
            decimal prevHp2 = i >= 2 ? hpList.ElementAtOrDefault(i - 2) : 0;
            decimal prevSsf1 = i >= 1 ? ssfList.ElementAtOrDefault(i - 1) : 0;
            decimal prevSsf2 = i >= 2 ? ssfList.ElementAtOrDefault(i - 2) : 0;
            decimal prevPredict1 = i >= 1 ? predictList.ElementAtOrDefault(i - 1) : 0;
            decimal priorSsf = i >= upperLength - 1 ? ssfList.ElementAtOrDefault(i - (upperLength - 1)) : 0;
            var pArray = new decimal[500];
            var bb1Array = new decimal[500];
            var bb2Array = new decimal[500];
            var coefArray = new decimal[500];
            var coefAArray = new decimal[500];
            var xxArray = new decimal[520];
            var hCoefArray = new decimal[520];

            decimal hp = i < 4 ? 0 : (c1 * (currentValue - (2 * prevValue1) + prevValue2)) + (c2 * prevHp1) + (c3 * prevHp2);
            hpList.Add(hp);

            decimal ssf = i < 3 ? hp : (coef1 * ((hp + prevHp1) / 2)) + (coef2 * prevSsf1) + (coef3 * prevSsf2);
            ssfList.Add(ssf);

            decimal pwrSum = 0;
            for (int j = 0; j < upperLength; j++)
            {
                decimal prevSsf = i >= j ? ssfList.ElementAtOrDefault(i - j) : 0;
                pwrSum += Pow(prevSsf, 2);
            }

            decimal pwr = pwrSum / upperLength;
            bb1Array[1] = ssf;
            bb2Array[upperLength - 1] = priorSsf;
            for (int j = 2; j < upperLength; j++)
            {
                decimal prevSsf = i >= j - 1 ? ssfList.ElementAtOrDefault(i - (j - 1)) : 0;
                bb1Array[j] = prevSsf;
                bb2Array[j - 1] = prevSsf;
            }

            decimal num = 0, denom = 0;
            for (int j = 1; j < upperLength; j++)
            {
                num += bb1Array[j] * bb2Array[j];
                denom += Pow(bb1Array[j], 2) + Pow(bb2Array[j], 2);
            }

            decimal coef = denom != 0 ? 2 * num / denom : 0;
            decimal p = pwr * (1 - Pow(coef, 2));
            coefArray[1] = coef;
            pArray[1] = p;
            for (int j = 2; j <= length2; j++)
            {
                for (int k = 1; k < j; k++)
                {
                    coefAArray[k] = coefArray[k];
                }

                for (int k = 1; k < upperLength; k++)
                {
                    bb1Array[k] = bb1Array[k] - (coefAArray[j - 1] * bb2Array[k]);
                    bb2Array[k] = bb2Array[k + 1] - (coefAArray[j - 1] * bb1Array[k + 1]);
                }

                decimal num1 = 0, denom1 = 0;
                for (int k = 1; k <= upperLength - j; k++)
                {
                    num1 += bb1Array[k] * bb2Array[k];
                    denom1 += Pow(bb1Array[k], 2) + Pow(bb2Array[k], 2);
                }

                coefArray[j] = denom1 != 0 ? 2 * num1 / denom1 : 0;
                pArray[j] = pArray[j - 1] * (1 - Pow(coefArray[j], 2));
                for (int k = 1; k < j; k++)
                {
                    coefArray[k] = coefAArray[k] - (coefArray[j] * coefAArray[j - k]);
                }
            }

            var coef1Array = new decimal[500];
            for (int j = 1; j <= length2; j++)
            {
                coef1Array[1] = coefArray[j];
                for (int k = lowerLength; k >= 2; k--)
                {
                    coef1Array[k] = coef1Array[k - 1];
                }
            }

            for (int j = 1; j <= length2; j++)
            {
                hCoefArray[j] = 0;
                decimal cc = 0;
                for (int k = 1; k <= lowerLength; k++)
                {
                    hCoefArray[j] = hCoefArray[j] + ((1 - Cos(MinOrMax(2 * Pi * ((decimal)k / (lowerLength + 1)), 0.99m, 0.01m))) * coef1Array[k]);
                    cc += 1 - Cos(MinOrMax(2 * Pi * ((decimal)k / (lowerLength + 1)), 0.99m, 0.01m));
                }
                hCoefArray[j] = cc != 0 ? hCoefArray[j] / cc : 0;
            }

            for (int j = 1; j <= upperLength; j++)
            {
                xxArray[j] = i >= upperLength - j ? ssfList.ElementAtOrDefault(i - (upperLength - j)) : 0;
            }

            for (int j = 1; j <= length3; j++)
            {
                xxArray[upperLength + j] = 0;
                for (int k = 1; k <= length2; k++)
                {
                    xxArray[upperLength + j] = xxArray[upperLength + j] + (hCoefArray[k] * xxArray[upperLength + j - k]);
                }
            }

            decimal prevPrePredict = prePredictList.LastOrDefault();
            decimal prePredict = xxArray[upperLength + length1];
            prePredictList.Add(prePredict);

            decimal predict = (prePredict + prevPrePredict) / 2;
            predictList.Add(predict);

            var signal = GetCompareSignal(ssf - predict, prevSsf1 - prevPredict1);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> ssfList = new();
        List<decimal> hpList = new();
        List<decimal> predictList = new();
        List<decimal> extrapList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var coefArray = new decimal[5];
        coefArray[0] = 4.525m;
        coefArray[1] = -8.45m;
        coefArray[2] = 8.145m;
        coefArray[3] = -4.045m;
        coefArray[4] = 0.825m;

        decimal a1 = Exp(MinOrMax(-1.414m * Pi / length2, -0.01m, -0.99m));
        decimal b1 = 2 * a1 * Cos(MinOrMax(1.414m * Pi / length2, 0.99m, 0.01m));
        decimal c2 = b1;
        decimal c3 = -a1 * a1;
        decimal c1 = (1 + c2 - c3) / 4;
        decimal a = Exp(MinOrMax(-1.414m * Pi / length3, -0.01m, -0.99m));
        decimal b = 2 * a * Cos(MinOrMax(1.414m * Pi / length3, 0.99m, 0.01m));
        decimal coef2 = b;
        decimal coef3 = -a * a;
        decimal coef1 = 1 - coef2 - coef3;

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal currentValue = inputList.ElementAtOrDefault(i);
            decimal prevValue1 = i >= 1 ? inputList.ElementAtOrDefault(i - 1) : 0;
            decimal prevValue2 = i >= 2 ? inputList.ElementAtOrDefault(i - 2) : 0;
            decimal prevHp1 = i >= 1 ? hpList.ElementAtOrDefault(i - 1) : 0;
            decimal prevHp2 = i >= 2 ? hpList.ElementAtOrDefault(i - 2) : 0;
            decimal prevSsf1 = i >= 1 ? ssfList.ElementAtOrDefault(i - 1) : 0;
            decimal prevSsf2 = i >= 2 ? ssfList.ElementAtOrDefault(i - 2) : 0;

            decimal hp = i < 4 ? 0 : (c1 * (currentValue - (2 * prevValue1) + prevValue2)) + (c2 * prevHp1) + (c3 * prevHp2);
            hpList.Add(hp);

            decimal ssf = i < 3 ? hp : (coef1 * ((hp + prevHp1) / 2)) + (coef2 * prevSsf1) + (coef3 * prevSsf2);
            ssfList.Add(ssf);
        }

        var filtList = GetMovingAverageList(stockData, maType, length3, ssfList);
        for (int i = 0; i < stockData.Count; i++)
        {
            decimal prevPredict1 = i >= 1 ? predictList.ElementAtOrDefault(i - 1) : 0;
            decimal prevPredict2 = i >= 2 ? predictList.ElementAtOrDefault(i - 2) : 0;

            var xxArray = new decimal[100];
            var yyArray = new decimal[100];
            for (int j = 1; j <= length1; j++)
            {
                decimal prevFilt = i >= length1 - j ? filtList.ElementAtOrDefault(i - (length1 - j)) : 0;
                xxArray[j] = prevFilt;
                yyArray[j] = prevFilt;
            }

            for (int j = 1; j <= length1; j++)
            {
                xxArray[length1 + j] = 0;
                for (int k = 1; k <= 5; k++)
                {
                    xxArray[length1 + j] = xxArray[length1 + j] + (coefArray[k - 1] * xxArray[length1 + j - (k - 1)]);
                }
            }

            for (int j = 0; j <= length1; j++)
            {
                yyArray[length1 + j + 1] = (2 * yyArray[length1 + j]) - yyArray[length1 + j - 1];
            }

            decimal predict = xxArray[length1 + length4];
            predictList.Add(predict);

            decimal extrap = yyArray[length1 + length4];
            extrapList.Add(extrap);

            var signal = GetCompareSignal(predict - prevPredict1, prevPredict1 - prevPredict2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        int length = 14, decimal bw = 1)
    {
        List<decimal> predictList = new();
        List<Signal> signalsList = new();

        var hFiltList = CalculateEhlersImpulseResponse(stockData, maType, length, bw).CustomValuesList;

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal maxCorr = -1, start = 0;
            for (int j = 0; j < length; j++)
            {
                decimal sx = 0, sy = 0, sxx = 0, syy = 0, sxy = 0;
                for (int k = 0; k < length; k++)
                {
                    decimal x = i >= k ? hFiltList.ElementAtOrDefault(i - k) : 0;
                    decimal y = -Sin(MinOrMax(2 * Pi * ((decimal)(j + k) / length), 0.99m, 0.01m));
                    sx += x;
                    sy += y;
                    sxx += Pow(x, 2);
                    sxy += x * y;
                    syy += Pow(y, 2);
                }
                decimal corr = ((length * sxx) - Pow(sx, 2)) * ((length * syy) - Pow(sy, 2)) > 0 ? ((length * sxy) - (sx * sy)) /
                    Sqrt(((length * sxx) - Pow(sx, 2)) * ((length * syy) - Pow(sy, 2))) : 0;
                start = corr > maxCorr ? length - j : 0;
                maxCorr = corr > maxCorr ? corr : maxCorr;
            }

            decimal prevPredict = predictList.LastOrDefault();
            decimal predict = Sin(MinOrMax(2 * Pi * start / length, 0.99m, 0.01m));
            predictList.Add(predict);

            var signal = GetCompareSignal(predict, prevPredict);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
    public static StockData CalculateEhlersImpulseReaction(this StockData stockData, int length1 = 2, int length2 = 20, decimal qq = 0.9m)
    {
        List<decimal> reactionList = new();
        List<decimal> ireactList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        decimal c2 = 2 * qq * Cos(2 * Pi / length2);
        decimal c3 = -qq * qq;
        decimal c1 = (1 + c3) / 2;

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal currentValue = inputList.ElementAtOrDefault(i);
            decimal priorValue = i >= length1 ? inputList.ElementAtOrDefault(i - length1) : 0;
            decimal prevReaction1 = i >= 1 ? reactionList.ElementAtOrDefault(i - 1) : 0;
            decimal prevReaction2 = i >= 2 ? reactionList.ElementAtOrDefault(i - 2) : 0;
            decimal prevIReact1 = i >= 1 ? ireactList.ElementAtOrDefault(i - 1) : 0;
            decimal prevIReact2 = i >= 2 ? ireactList.ElementAtOrDefault(i - 2) : 0;

            decimal reaction = (c1 * (currentValue - priorValue)) + (c2 * prevReaction1) + (c3 * prevReaction2);
            reactionList.Add(reaction);

            decimal ireact = currentValue != 0 ? 100 * reaction / currentValue : 0;
            ireactList.Add(ireact);

            var signal = GetCompareSignal(ireact - prevIReact1, prevIReact1 - prevIReact2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        int length1 = 16, int length2 = 50, decimal mult = 2)
    {
        List<decimal> momList = new();
        List<decimal> negRmsList = new();
        List<decimal> filtPowList = new();
        List<decimal> rmsList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        int hannLength = (int)Math.Ceiling(mult * length1);

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal currentValue = inputList.ElementAtOrDefault(i);
            decimal priorValue = i >= hannLength ? inputList.ElementAtOrDefault(i - hannLength) : 0;

            decimal mom = currentValue - priorValue;
            momList.Add(mom);
        }

        var filtList = GetMovingAverageList(stockData, maType, length1, momList);
        for (int i = 0; i < stockData.Count; i++)
        {
            decimal filt = filtList.ElementAtOrDefault(i);
            decimal prevFilt1 = i >= 1 ? filtList.ElementAtOrDefault(i - 1) : 0;
            decimal prevFilt2 = i >= 2 ? filtList.ElementAtOrDefault(i - 2) : 0;

            decimal filtPow = Pow(filt, 2);
            filtPowList.Add(filtPow);

            decimal filtPowMa = filtPowList.TakeLastExt(length2).Average();
            decimal rms = filtPowMa > 0 ? Sqrt(filtPowMa) : 0;
            rmsList.Add(rms);

            decimal negRms = -rms;
            negRmsList.Add(negRms);

            var signal = GetCompareSignal(filt - prevFilt1, prevFilt1 - prevFilt2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> rmList = new();
        List<decimal> tempList = new();
        List<decimal> rmoList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        decimal alpha1Arg = MinOrMax(2 * Pi / length2, 0.99m, 0.01m);
        decimal alpha1ArgCos = Cos(alpha1Arg);
        decimal alpha2Arg = MinOrMax(1 / Sqrt(2) * 2 * Pi / length3, 0.99m, 0.01m);
        decimal alpha2ArgCos = Cos(alpha2Arg);
        decimal alpha1 = alpha1ArgCos != 0 ? (alpha1ArgCos + Sin(alpha1Arg) - 1) / alpha1ArgCos : 0;
        decimal alpha2 = alpha2ArgCos != 0 ? (alpha2ArgCos + Sin(alpha2Arg) - 1) / alpha2ArgCos : 0;

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal currentValue = inputList.ElementAtOrDefault(i);
            tempList.Add(currentValue);

            decimal median = tempList.TakeLastExt(length1).Median();
            decimal prevRm1 = i >= 1 ? rmList.ElementAtOrDefault(i - 1) : 0;
            decimal prevRm2 = i >= 2 ? rmList.ElementAtOrDefault(i - 2) : 0;
            decimal prevRmo1 = i >= 1 ? rmoList.ElementAtOrDefault(i - 1) : 0;
            decimal prevRmo2 = i >= 2 ? rmoList.ElementAtOrDefault(i - 2) : 0;

            decimal rm = (alpha1 * median) + ((1 - alpha1) * prevRm1);
            rmList.Add(rm);

            decimal rmo = (Pow(1 - (alpha2 / 2), 2) * (rm - (2 * prevRm1) + prevRm2)) + (2 * (1 - alpha2) * prevRmo1) - (Pow(1 - alpha2, 2) * prevRmo2);
            rmoList.Add(rmo);

            var signal = GetCompareSignal(rmo, prevRmo1);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> espfList = new();
        List<decimal> squareList = new();
        List<decimal> rmsList = new();
        List<decimal> negRmsList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        decimal a1 = MinOrMax((decimal)length1 / fastLength, 0.99m, 0.01m);
        decimal a2 = MinOrMax((decimal)length1 / slowLength, 0.99m, 0.01m);

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal currentValue = inputList.ElementAtOrDefault(i);
            decimal prevValue1 = i >= 1 ? inputList.ElementAtOrDefault(i - 1) : 0;
            decimal prevEspf1 = i >= 1 ? espfList.ElementAtOrDefault(i - 1) : 0;
            decimal prevEspf2 = i >= 2 ? espfList.ElementAtOrDefault(i - 2) : 0;

            decimal espf = ((a1 - a2) * currentValue) + (((a2 * (1 - a1)) - (a1 * (1 - a2))) * prevValue1) + ((1 - a1 + (1 - a2)) * prevEspf1) - 
                ((1 - a1) * (1 - a2) * prevEspf2);
            espfList.Add(espf);

            decimal espfPow = Pow(espf, 2);
            squareList.Add(espfPow);

            decimal squareAvg = squareList.TakeLastExt(length2).Average();
            decimal prevRms = rmsList.LastOrDefault();
            decimal rms = Sqrt(squareAvg);
            rmsList.Add(rms);

            decimal prevNegRms = negRmsList.LastOrDefault();
            decimal negRms = -rms;
            negRmsList.Add(negRms);

            var signal = GetBullishBearishSignal(espf - rms, prevEspf1 - prevRms, espf - negRms, prevEspf1 - prevNegRms);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> derivList = new();
        List<decimal> z3List = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal currentValue = inputList.ElementAtOrDefault(i);
            decimal prevValue = i >= length ? inputList.ElementAtOrDefault(i - length) : 0;
            decimal prevDeriv1 = i >= 1 ? derivList.ElementAtOrDefault(i - 1) : 0;
            decimal prevDeriv2 = i >= 2 ? derivList.ElementAtOrDefault(i - 2) : 0;
            decimal prevDeriv3 = i >= 3 ? derivList.ElementAtOrDefault(i - 3) : 0;

            decimal deriv = currentValue - prevValue;
            derivList.Add(deriv);

            decimal z3 = deriv + prevDeriv1 + prevDeriv2 + prevDeriv3;
            z3List.Add(z3);
        }

        var z3EmaList = GetMovingAverageList(stockData, maType, signalLength, z3List);
        for (int i = 0; i < stockData.Count; i++)
        {
            decimal z3Ema = z3EmaList.ElementAtOrDefault(i);
            decimal prevZ3Ema = i >= 1 ? z3EmaList.ElementAtOrDefault(i - 1) : 0;

            var signal = GetCompareSignal(z3Ema, prevZ3Ema);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> derivList = new();
        List<decimal> clipList = new();
        List<decimal> z3List = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal currentValue = inputList.ElementAtOrDefault(i);
            decimal prevValue = i >= length1 ? inputList.ElementAtOrDefault(i - length1) : 0;
            decimal prevClip1 = i >= 1 ? clipList.ElementAtOrDefault(i - 1) : 0;
            decimal prevClip2 = i >= 2 ? clipList.ElementAtOrDefault(i - 2) : 0;
            decimal prevClip3 = i >= 3 ? clipList.ElementAtOrDefault(i - 3) : 0;

            decimal deriv = currentValue - prevValue;
            derivList.Add(deriv);

            decimal rms = 0;
            for (int j = 0; j < length3; j++)
            {
                decimal prevDeriv = i >= j ? derivList.ElementAtOrDefault(i - j) : 0;
                rms += Pow(prevDeriv, 2);
            }

            decimal clip = rms != 0 ? MinOrMax(2 * deriv / Sqrt(rms / length3), 1, -1) : 0;
            clipList.Add(clip);

            decimal z3 = clip + prevClip1 + prevClip2 + prevClip3;
            z3List.Add(z3);
        }

        var z3EmaList = GetMovingAverageList(stockData, maType, signalLength, z3List);
        for (int i = 0; i < stockData.Count; i++)
        {
            decimal z3Ema = z3EmaList.ElementAtOrDefault(i);
            decimal prevZ3Ema = i >= 1 ? z3EmaList.ElementAtOrDefault(i - 1) : 0;

            var signal = GetCompareSignal(z3Ema, prevZ3Ema);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> sriList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (int i = 0; i < stockData.Count; i++)
        {
            var priceArray = new decimal[50];
            var rankArray = new decimal[50];
            for (int j = 0; j < length; j++)
            {
                var prevPrice = i >= j - 1 ? inputList.ElementAtOrDefault(i - j - 1) : 0;
                priceArray[j] = prevPrice;
                rankArray[j] = j;
            }

            for (int j = 0; j < length; j++)
            {
                var count = length + 1 - j;

                for (int k = 0; k < length - count; k++)
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

            decimal sum = 0;
            for (int j = 0; j < length; j++)
            {
                sum += Pow(j - rankArray[j], 2);
            }

            decimal prevSri = sriList.LastOrDefault();
            decimal sri = 2 * (0.5m - (1 - (6 * sum / (length * (Pow(length, 2) - 1)))));
            sriList.Add(sri);

            var signal = GetCompareSignal(sri, prevSri);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> netList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        decimal denom = 0.5m * length * (length - 1);

        for (int i = 0; i < stockData.Count; i++)
        {
            var xArray = new decimal[50];
            for (int j = 0; j < length; j++)
            {
                var prevPrice = i >= j - 1 ? inputList.ElementAtOrDefault(i - j - 1) : 0;
                xArray[j] = prevPrice;
            }

            decimal num = 0;
            for (int j = 1; j < length; j++)
            {
                for (int k = 0; k < j - 1; k++)
                {
                    num -= Math.Sign(xArray[j] - xArray[k]);
                }
            }

            decimal prevNet = netList.LastOrDefault();
            decimal net = denom != 0 ? num / denom : 0;
            netList.Add(net);

            var signal = GetCompareSignal(net, prevNet);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
    public static StockData CalculateEhlersTruncatedBandPassFilter(this StockData stockData, int length1 = 20, int length2 = 10, decimal bw = 0.1m)
    {
        List<decimal> bptList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        decimal l1 = Cos(MinOrMax(2 * Pi / length1, 0.99m, 0.01m));
        decimal g1 = Cos(bw * 2 * Pi / length1);
        decimal s1 = (1 / g1) - Sqrt((1 / Pow(g1, 2)) - 1);

        for (int i = 0; i < stockData.Count; i++)
        {
            var trunArray = new decimal[100];
            for (int j = length2; j > 0; j--)
            {
                decimal prevValue1 = i >= j - 1 ? inputList.ElementAtOrDefault(i - j - 1) : 0;
                decimal prevValue2 = i >= j + 1 ? inputList.ElementAtOrDefault(i - (j + 1)) : 0;
                trunArray[j] = (0.5m * (1 - s1) * (prevValue1 - prevValue2)) + (l1 * (1 + s1) * trunArray[j + 1]) - (s1 * trunArray[j + 2]);
            }

            decimal prevBpt = bptList.LastOrDefault();
            decimal bpt = trunArray[1];
            bptList.Add(bpt);

            var signal = GetCompareSignal(bpt, prevBpt);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> corrList = new();
        List<decimal> xList = new();
        List<decimal> yList = new();
        List<decimal> xxList = new();
        List<decimal> yyList = new();
        List<decimal> xyList = new();
        List<Signal> signalsList = new();

        var roofingFilterList = CalculateEhlersRoofingFilterV2(stockData, length1, length2).CustomValuesList;

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal prevCorr1 = i >= 1 ? corrList.ElementAtOrDefault(i - 1) : 0;
            decimal prevCorr2 = i >= 2 ? corrList.ElementAtOrDefault(i - 2) : 0;

            decimal x = roofingFilterList.ElementAtOrDefault(i);
            xList.Add(x);

            decimal y = i >= length1 ? roofingFilterList.ElementAtOrDefault(i - length1) : 0;
            yList.Add(y);

            decimal xx = Pow(x, 2);
            xxList.Add(xx);

            decimal yy = Pow(y, 2);
            yyList.Add(yy);

            decimal xy = x * y;
            xyList.Add(xy);

            decimal sx = xList.TakeLastExt(length1).Sum();
            decimal sy = yList.TakeLastExt(length1).Sum();
            decimal sxx = xxList.TakeLastExt(length1).Sum();
            decimal syy = yyList.TakeLastExt(length1).Sum();
            decimal sxy = xyList.TakeLastExt(length1).Sum();

            decimal corr = ((i * sxx) - (sx * sx)) * ((i * syy) - (sy * sy)) > 0 ? 0.5m * ((((i * sxy) - (sx * sy)) / 
                Sqrt(((i * sxx) - (sx * sx)) * ((i * syy) - (sy * sy)))) + 1) : 0;
            corrList.Add(corr);

            var signal = GetCompareSignal(corr - prevCorr1, prevCorr1 - prevCorr2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> domCycList = new();
        List<decimal> rList = new();
        List<Signal> signalsList = new();

        var corrList = CalculateEhlersAutoCorrelationIndicator(stockData, length1, length2).CustomValuesList;

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal corr = corrList.ElementAtOrDefault(i);
            decimal prevCorr1 = i >= 1 ? corrList.ElementAtOrDefault(i - 1) : 0;
            decimal prevCorr2 = i >= 2 ? corrList.ElementAtOrDefault(i - 2) : 0;

            decimal maxPwr = 0, spx = 0, sp = 0;
            for (int j = length2; j <= length1; j++)
            {
                decimal cosPart = 0, sinPart = 0;
                for (int k = length3; k <= length1; k++)
                {
                    decimal prevCorr = i >= k ? corrList.ElementAtOrDefault(i - k) : 0;
                    cosPart += prevCorr * Cos(2 * Pi * ((decimal)k / j));
                    sinPart += prevCorr * Sin(2 * Pi * ((decimal)k / j));
                }

                decimal sqSum = Pow(cosPart, 2) + Pow(sinPart, 2);
                decimal prevR = i >= j - 1 ? rList.ElementAtOrDefault(i - (j - 1)) : 0;
                decimal r = (0.2m * Pow(sqSum, 2)) + (0.8m * prevR);
                maxPwr = Math.Max(r, maxPwr);
                decimal pwr = maxPwr != 0 ? r / maxPwr : 0;

                if (pwr >= 0.5m)
                {
                    spx += j * pwr;
                    sp += pwr;
                }
            }

            decimal domCyc = sp != 0 ? spx / sp : 0;
            domCycList.Add(domCyc);

            var signal = GetCompareSignal(corr - prevCorr1, prevCorr1 - prevCorr2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> upChgList = new();
        List<decimal> denomList = new();
        List<decimal> arsiList = new();
        List<Signal> signalsList = new();

        decimal a1 = Exp(-1.414m * Pi / length2);
        decimal b1 = 2 * a1 * Cos(Math.Min(1.414m * Pi / length2, 0.99m));
        decimal c2 = b1;
        decimal c3 = -a1 * a1;
        decimal c1 = 1 - c2 - c3;

        var domCycList = CalculateEhlersAutoCorrelationPeriodogram(stockData, length1, length2, length3).CustomValuesList;
        var roofingFilterList = CalculateEhlersRoofingFilterV2(stockData, length1, length2).CustomValuesList;

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal domCyc = MinOrMax(domCycList.ElementAtOrDefault(i), length1, length2);
            decimal prevArsi1 = i >= 1 ? arsiList.ElementAtOrDefault(i - 1) : 0;
            decimal prevArsi2 = i >= 2 ? arsiList.ElementAtOrDefault(i - 2) : 0;

            decimal prevUpChg = upChgList.LastOrDefault();
            decimal upChg = 0, dnChg = 0;
            for (int j = 0; j < (int)Math.Ceiling(domCyc / 2); j++)
            {
                decimal filt = i >= j ? roofingFilterList.ElementAtOrDefault(i - j) : 0;
                decimal prevFilt = i >= j + 1 ? roofingFilterList.ElementAtOrDefault(i - (j + 1)) : 0;
                upChg += filt > prevFilt ? filt - prevFilt : 0;
                dnChg += filt < prevFilt ? prevFilt - filt : 0;
            }
            upChgList.Add(upChg);

            decimal prevDenom = denomList.LastOrDefault();
            decimal denom = upChg + dnChg;
            denomList.Add(denom);

            decimal arsi = denom != 0 && prevDenom != 0 ? (c1 * ((upChg / denom) + (prevUpChg / prevDenom)) / 2) + (c2 * prevArsi1) + (c3 * prevArsi2) : 0;
            arsiList.Add(arsi);
        }

        var arsiEmaList = GetMovingAverageList(stockData, maType, length2, arsiList);
        for (int i = 0; i < stockData.Count; i++)
        {
            decimal arsi = arsiList.ElementAtOrDefault(i);
            decimal arsiEma = arsiEmaList.ElementAtOrDefault(i);
            decimal prevArsi = i >= 1 ? arsiList.ElementAtOrDefault(i - 1) : 0;
            decimal prevArsiEma = i >= 1 ? arsiEmaList.ElementAtOrDefault(i - 1) : 0;

            var signal = GetRsiSignal(arsi - arsiEma, prevArsi - prevArsiEma, arsi, prevArsi, 0.7m, 0.3m);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
    public static StockData CalculateEhlersAdaptiveRsiV2FisherTransform(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 48, int length2 = 10, int length3 = 3)
    {
        List<decimal> fishList = new();
        List<Signal> signalsList = new();

        var arsiList = CalculateEhlersAdaptiveRelativeStrengthIndexV2(stockData, maType, length1, length2, length3).CustomValuesList;

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal arsi = arsiList.ElementAtOrDefault(i) / 100;
            decimal prevFish1 = i >= 1 ? fishList.ElementAtOrDefault(i - 1) : 0;
            decimal prevFish2 = i >= 2 ? fishList.ElementAtOrDefault(i - 2) : 0;
            decimal tranRsi = 2 * (arsi - 0.5m);
            decimal ampRsi = MinOrMax(1.5m * tranRsi, 0.999m, -0.999m);

            decimal fish = 0.5m * Log((1 + ampRsi) / (1 - ampRsi));
            fishList.Add(fish);

            var signal = GetRsiSignal(fish - prevFish1, prevFish1 - prevFish2, fish, prevFish1, 2, -2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
        {
            { "Earsift", fishList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = fishList;
        stockData.IndicatorName = IndicatorName.EhlersAdaptiveRsiV2FisherTransform;

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
        List<decimal> stocList = new();
        List<decimal> astocList = new();
        List<Signal> signalsList = new();

        decimal a1 = Exp(-1.414m * Pi / length2);
        decimal b1 = 2 * a1 * Cos(Math.Min(1.414m * Pi / length2, 0.99m));
        decimal c2 = b1;
        decimal c3 = -a1 * a1;
        decimal c1 = 1 - c2 - c3;

        var domCycList = CalculateEhlersAutoCorrelationPeriodogram(stockData, length1, length2, length3).CustomValuesList;
        var roofingFilterList = CalculateEhlersRoofingFilterV2(stockData, length1, length2).CustomValuesList;

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal domCyc = MinOrMax(domCycList.ElementAtOrDefault(i), length1, length2);
            decimal roofingFilter = roofingFilterList.ElementAtOrDefault(i);
            decimal prevAstoc1 = i >= 1 ? astocList.ElementAtOrDefault(i - 1) : 0;
            decimal prevAstoc2 = i >= 2 ? astocList.ElementAtOrDefault(i - 2) : 0;

            decimal highest = 0, lowest = 0;
            for (int j = 0; j < (int)Math.Ceiling(domCyc); j++)
            {
                decimal filt = i >= j ? roofingFilterList.ElementAtOrDefault(i - j) : 0;
                highest = filt > highest ? filt : highest;
                lowest = filt < lowest ? filt : lowest;
            }

            decimal prevStoc = stocList.LastOrDefault();
            decimal stoc = highest != lowest ? (roofingFilter - lowest) / (highest - lowest) : 0;
            stocList.Add(stoc);

            decimal astoc = (c1 * ((stoc + prevStoc) / 2)) + (c2 * prevAstoc1) + (c3 * prevAstoc2);
            astocList.Add(astoc);
        }

        var astocEmaList = GetMovingAverageList(stockData, maType, length2, astocList);
        for (int i = 0; i < stockData.Count; i++)
        {
            decimal astoc = astocList.ElementAtOrDefault(i);
            decimal astocEma = astocEmaList.ElementAtOrDefault(i);
            decimal prevAstoc = i >= 1 ? astocList.ElementAtOrDefault(i - 1) : 0;
            decimal prevAstocEma = i >= 1 ? astocEmaList.ElementAtOrDefault(i - 1) : 0;

            var signal = GetRsiSignal(astoc - astocEma, prevAstoc - prevAstocEma, astoc, prevAstoc, 0.7m, 0.3m);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> fishList = new();
        List<decimal> triggerList = new();
        List<Signal> signalsList = new();

        var astocList = CalculateEhlersAdaptiveStochasticIndicatorV2(stockData, maType, length1, length2, length3).CustomValuesList;

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal astoc = astocList.ElementAtOrDefault(i);
            decimal v1 = 2 * (astoc - 0.5m);

            decimal prevFish = fishList.LastOrDefault();
            decimal fish = (Exp(6 * v1) - 1) / (Exp(6 * v1) + 1);
            fishList.Add(fish);

            decimal prevTrigger = triggerList.LastOrDefault();
            decimal trigger = 0.9m * prevFish;
            triggerList.Add(trigger);

            var signal = GetCompareSignal(fish - trigger, prevFish - prevTrigger);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> acciList = new();
        List<decimal> tempList = new();
        List<decimal> mdList = new();
        List<decimal> ratioList = new();
        List<Signal> signalsList = new();

        decimal a1 = Exp(-1.414m * Pi / length2);
        decimal b1 = 2 * a1 * Cos(Math.Min(1.414m * Pi / length2, 0.99m));
        decimal c2 = b1;
        decimal c3 = -a1 * a1;
        decimal c1 = 1 - c2 - c3;

        var domCycList = CalculateEhlersAutoCorrelationPeriodogram(stockData, length1, length2, length3).CustomValuesList;
        var roofingFilterList = CalculateEhlersRoofingFilterV2(stockData, length1, length2).CustomValuesList;

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal domCyc = MinOrMax(domCycList.ElementAtOrDefault(i), length1, length2);
            decimal prevAcci1 = i >= 1 ? acciList.ElementAtOrDefault(i - 1) : 0;
            decimal prevAcci2 = i >= 2 ? acciList.ElementAtOrDefault(i - 2) : 0;
            int cycLength = (int)Math.Ceiling(domCyc);

            decimal roofingFilter = roofingFilterList.ElementAtOrDefault(i);
            tempList.Add(roofingFilter);

            decimal avg = tempList.TakeLastExt(cycLength).Average();
            decimal md = Pow(roofingFilter - avg, 2);
            mdList.Add(md);

            decimal mdAvg = mdList.TakeLastExt(cycLength).Average();
            decimal rms = cycLength >= 0 ? Sqrt(mdAvg) : 0;
            decimal num = roofingFilter - avg;
            decimal denom = 0.015m * rms;

            decimal prevRatio = ratioList.LastOrDefault();
            decimal ratio = denom != 0 ? num / denom : 0;
            ratioList.Add(ratio);

            decimal acci = (c1 * ((ratio + prevRatio) / 2)) + (c2 * prevAcci1) + (c3 * prevAcci2);
            acciList.Add(acci);
        }

        var acciEmaList = GetMovingAverageList(stockData, maType, length2, acciList);
        for (int i = 0; i < stockData.Count; i++)
        {
            decimal acci = acciList.ElementAtOrDefault(i);
            decimal acciEma = acciEmaList.ElementAtOrDefault(i);
            decimal prevAcci = i >= 1 ? acciList.ElementAtOrDefault(i - 1) : 0;
            decimal prevAcciEma = i >= 1 ? acciEmaList.ElementAtOrDefault(i - 1) : 0;

            var signal = GetRsiSignal(acci - acciEma, prevAcci - prevAcciEma, acci, prevAcci, 100, -100);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> rList = new();
        List<decimal> domCycList = new();
        List<Signal> signalsList = new();

        var roofingFilterList = CalculateEhlersRoofingFilterV2(stockData, length1, length2).CustomValuesList;

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal roofingFilter = roofingFilterList.ElementAtOrDefault(i);
            decimal prevRoofingFilter1 = i >= 1 ? roofingFilterList.ElementAtOrDefault(i - 1) : 0;
            decimal prevRoofingFilter2 = i >= 2 ? roofingFilterList.ElementAtOrDefault(i - 2) : 0;

            decimal maxPwr = 0, spx = 0, sp = 0;
            for (int j = length2; j <= length1; j++)
            {
                decimal cosPart = 0, sinPart = 0;
                for (int k = 0; k <= length1; k++)
                {
                    decimal prevFilt = i >= k ? roofingFilterList.ElementAtOrDefault(i - k) : 0;
                    cosPart += prevFilt * Cos(2 * Pi * ((decimal)k / j));
                    sinPart += prevFilt * Sin(2 * Pi * ((decimal)k / j));
                }

                decimal sqSum = Pow(cosPart, 2) + Pow(sinPart, 2);
                decimal prevR = i >= j - 1 ? rList.ElementAtOrDefault(i - (j - 1)) : 0;
                decimal r = (0.2m * Pow(sqSum, 2)) + (0.8m * prevR);
                maxPwr = Math.Max(r, maxPwr);
                decimal pwr = maxPwr != 0 ? r / maxPwr : 0;

                if (pwr >= 0.5m)
                {
                    spx += j * pwr;
                    sp += pwr;
                }
            }

            decimal domCyc = sp != 0 ? spx / sp : 0;
            domCycList.Add(domCyc);

            var signal = GetCompareSignal(roofingFilter - prevRoofingFilter1, prevRoofingFilter1 - prevRoofingFilter2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
    public static StockData CalculateEhlersCombFilterSpectralEstimate(this StockData stockData, int length1 = 48, int length2 = 10, decimal bw = 0.3m)
    {
        List<decimal> domCycList = new();
        List<decimal> bpList = new();
        List<Signal> signalsList = new();

        var roofingFilterList = CalculateEhlersRoofingFilterV2(stockData, length1, length2).CustomValuesList;

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal roofingFilter = roofingFilterList.ElementAtOrDefault(i);
            decimal prevRoofingFilter1 = i >= 1 ? roofingFilterList.ElementAtOrDefault(i - 1) : 0;
            decimal prevRoofingFilter2 = i >= 2 ? roofingFilterList.ElementAtOrDefault(i - 2) : 0;
            decimal prevBp1 = i >= 1 ? bpList.ElementAtOrDefault(i - 1) : 0;
            decimal prevBp2 = i >= 2 ? bpList.ElementAtOrDefault(i - 2) : 0;

            decimal bp = 0, maxPwr = 0, spx = 0, sp = 0;
            for (int j = length2; j <= length1; j++)
            {
                decimal beta = Cos(2 * Pi / j);
                decimal gamma = 1 / Cos(2 * Pi * bw / j);
                decimal alpha = MinOrMax(gamma - Sqrt((gamma * gamma) - 1), 0.99m, 0.01m);
                bp = (0.5m * (1 - alpha) * (roofingFilter - prevRoofingFilter2)) + (beta * (1 + alpha) * prevBp1) - (alpha * prevBp2);

                decimal pwr = 0;
                for (int k = 1; k <= j; k++)
                {
                    decimal prevBp = i >= k ? bpList.ElementAtOrDefault(i - k) : 0;
                    pwr += prevBp / j >= 0 ? Pow(prevBp / j, 2) : 0;
                }

                maxPwr = Math.Max(pwr, maxPwr);
                pwr = maxPwr != 0 ? pwr / maxPwr : 0;

                if (pwr >= 0.5m)
                {
                    spx += j * pwr;
                    sp += pwr;
                }
            }
            bpList.Add(bp);

            decimal domCyc = sp != 0 ? spx / sp : 0;
            domCycList.Add(domCyc);

            var signal = GetCompareSignal(roofingFilter - prevRoofingFilter1, prevRoofingFilter1 - prevRoofingFilter2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> reversalList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var corrList = CalculateEhlersAutoCorrelationIndicator(stockData, length1, length2).CustomValuesList;
        var emaList = GetMovingAverageList(stockData, maType, length2, inputList);

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal ema = emaList.ElementAtOrDefault(i);
            decimal currentValue = inputList.ElementAtOrDefault(i);

            decimal delta = 0;
            for (int j = length3; j <= length1; j++)
            {
                decimal corr = i >= j ? corrList.ElementAtOrDefault(i - j) : 0;
                decimal prevCorr = i >= j - 1 ? corrList.ElementAtOrDefault(i - (j - 1)) : 0;
                delta += (corr > 0.5m && prevCorr < 0.5m) || (corr < 0.5m && prevCorr > 0.5m) ? 1 : 0;
            }

            decimal reversal = delta > (decimal)length1 / 2 ? 1 : 0;
            reversalList.Add(reversal);

            var signal = GetConditionSignal(currentValue < ema && reversal == 1, currentValue > ema && reversal == 1);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
    public static StockData CalculateEhlersReverseExponentialMovingAverageIndicatorV1(this StockData stockData, decimal alpha = 0.1m)
    {
        List<decimal> emaList = new();
        List<decimal> re1List = new();
        List<decimal> re2List = new();
        List<decimal> re3List = new();
        List<decimal> re4List = new();
        List<decimal> re5List = new();
        List<decimal> re6List = new();
        List<decimal> re7List = new();
        List<decimal> waveList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        decimal cc = 1 - alpha;

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal currentValue = inputList.ElementAtOrDefault(i);

            decimal prevEma = emaList.LastOrDefault();
            decimal ema = (alpha * currentValue) + (cc * prevEma);
            emaList.Add(ema);

            decimal prevRe1 = re1List.LastOrDefault();
            decimal re1 = (cc * ema) + prevEma;
            re1List.Add(re1);

            decimal prevRe2 = re2List.LastOrDefault();
            decimal re2 = (Pow(cc, 2) * re1) + prevRe1;
            re2List.Add(re2);

            decimal prevRe3 = re3List.LastOrDefault();
            decimal re3 = (Pow(cc, 4) * re2) + prevRe2;
            re3List.Add(re3);

            decimal prevRe4 = re4List.LastOrDefault();
            decimal re4 = (Pow(cc, 8) * re3) + prevRe3;
            re4List.Add(re4);

            decimal prevRe5 = re5List.LastOrDefault();
            decimal re5 = (Pow(cc, 16) * re4) + prevRe4;
            re5List.Add(re5);

            decimal prevRe6 = re6List.LastOrDefault();
            decimal re6 = (Pow(cc, 32) * re5) + prevRe5;
            re6List.Add(re6);

            decimal prevRe7 = re7List.LastOrDefault();
            decimal re7 = (Pow(cc, 64) * re6) + prevRe6;
            re7List.Add(re7);

            decimal re8 = (Pow(cc, 128) * re7) + prevRe7;
            decimal prevWave = waveList.LastOrDefault();
            decimal wave = ema - (alpha * re8);
            waveList.Add(wave);

            var signal = GetCompareSignal(wave, prevWave);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
    public static StockData CalculateEhlersReverseExponentialMovingAverageIndicatorV2(this StockData stockData, decimal trendAlpha = 0.05m, decimal cycleAlpha = 0.3m)
    {
        List<Signal> signalsList = new();

        var trendList = CalculateEhlersReverseExponentialMovingAverageIndicatorV1(stockData, trendAlpha).CustomValuesList;
        var cycleList = CalculateEhlersReverseExponentialMovingAverageIndicatorV1(stockData, cycleAlpha).CustomValuesList;

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal waveCycle = cycleList.ElementAtOrDefault(i);
            decimal waveTrend = trendList.ElementAtOrDefault(i);
            decimal prevWaveCycle = i >= 1 ? cycleList.ElementAtOrDefault(i - 1) : 0;
            decimal prevWaveTrend = i >= 1 ? trendList.ElementAtOrDefault(i - 1) : 0;

            var signal = GetCompareSignal(waveCycle - waveTrend, prevWaveCycle - prevWaveTrend);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
        {
            { "EremaCycle", cycleList },
            { "EremaTrend", trendList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<decimal>();
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
        List<decimal> madList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var shortMaList = GetMovingAverageList(stockData, maType, fastLength, inputList);
        var longMaList = GetMovingAverageList(stockData, maType, slowLength, inputList);

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal shortMa = shortMaList.ElementAtOrDefault(i);
            decimal longMa = longMaList.ElementAtOrDefault(i);
            decimal prevMad1 = i >= 1 ? madList.ElementAtOrDefault(i - 1) : 0;
            decimal prevMad2 = i >= 2 ? madList.ElementAtOrDefault(i - 2) : 0;

            decimal mad = longMa != 0 ? 100 * (shortMa - longMa) / longMa : 0;
            madList.Add(mad);

            var signal = GetCompareSignal(mad - prevMad1, prevMad1 - prevMad2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> efdso2PoleList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var scaledFilter2PoleList = GetMovingAverageList(stockData, maType, fastLength, inputList, fastLength, slowLength);

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal currentScaledFilter2Pole = scaledFilter2PoleList.ElementAtOrDefault(i);
            decimal prevEfdsoPole1 = i >= 1 ? efdso2PoleList.ElementAtOrDefault(i - 1) : 0;
            decimal prevEfdsoPole2 = i >= 2 ? efdso2PoleList.ElementAtOrDefault(i - 2) : 0;

            decimal efdso2Pole = Math.Abs(currentScaledFilter2Pole) < 2 ? 0.5m * Log((1 + (currentScaledFilter2Pole / 2)) / 
                (1 - (currentScaledFilter2Pole / 2))) : prevEfdsoPole1;
            efdso2PoleList.Add(efdso2Pole);

            var signal = GetRsiSignal(efdso2Pole - prevEfdsoPole1, prevEfdsoPole1 - prevEfdsoPole2, efdso2Pole, prevEfdsoPole1, 2, -2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
    public static StockData CalculateEhlersHilbertTransformIndicator(this StockData stockData, int length = 7, decimal iMult = 0.635m, decimal qMult = 0.338m)
    {
        List<decimal> v1List = new();
        List<decimal> inPhaseList = new();
        List<decimal> quadList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal currentValue = inputList.ElementAtOrDefault(i);
            decimal prevValue = i >= length ? inputList.ElementAtOrDefault(i - length) : 0;
            decimal v2 = i >= 2 ? v1List.ElementAtOrDefault(i - 2) : 0;
            decimal v4 = i >= 4 ? v1List.ElementAtOrDefault(i - 4) : 0;
            decimal inPhase3 = i >= 3 ? inPhaseList.ElementAtOrDefault(i - 3) : 0;
            decimal quad2 = i >= 2 ? quadList.ElementAtOrDefault(i - 2) : 0;

            decimal v1 = currentValue - prevValue;
            v1List.Add(v1);

            decimal prevInPhase = inPhaseList.LastOrDefault();
            decimal inPhase = (1.25m * (v4 - (iMult * v2))) + (iMult * inPhase3);
            inPhaseList.Add(inPhase);

            decimal prevQuad = quadList.LastOrDefault();
            decimal quad = v2 - (qMult * v1) + (qMult * quad2);
            quadList.Add(quad);

            var signal = GetCompareSignal(quad - (-1 * inPhase), prevQuad - (-1 * prevInPhase));
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
        {
            { "Quad", quadList },
            { "Inphase", inPhaseList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<decimal>();
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
        List<decimal> phaseList = new();
        List<decimal> dPhaseList = new();
        List<decimal> dcPeriodList = new();
        List<Signal> signalsList = new();

        var ehtList = CalculateEhlersHilbertTransformIndicator(stockData, length: length1);
        var ipList = ehtList.OutputValues["Inphase"];
        var quList = ehtList.OutputValues["Quad"];

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal ip = ipList.ElementAtOrDefault(i);
            decimal qu = quList.ElementAtOrDefault(i);
            decimal prevIp = i >= 1 ? ipList.ElementAtOrDefault(i - 1) : 0;
            decimal prevQu = i >= 1 ? quList.ElementAtOrDefault(i - 1) : 0;

            decimal prevPhase = phaseList.LastOrDefault();
            decimal phase = Math.Abs(ip + prevIp) > 0 ? Atan(Math.Abs((qu + prevQu) / (ip + prevIp))).ToDegrees() : 0;
            phase = ip < 0 && qu > 0 ? 180 - phase : phase;
            phase = ip < 0 && qu < 0 ? 180 + phase : phase;
            phase = ip > 0 && qu < 0 ? 360 - phase : phase;
            phaseList.Add(phase);

            decimal dPhase = prevPhase - phase;
            dPhase = prevPhase < 90 && phase > 270 ? 360 + prevPhase - phase : dPhase;
            dPhase = MinOrMax(dPhase, 60, 1);
            dPhaseList.Add(dPhase);

            decimal instPeriod = 0, v4 = 0;
            for (int j = 0; j <= length2; j++)
            {
                decimal prevDPhase = i >= j ? dPhaseList.ElementAtOrDefault(i - j) : 0;
                v4 += prevDPhase;
                instPeriod = v4 > 360 && instPeriod == 0 ? j : instPeriod;
            }

            decimal prevDcPeriod = dcPeriodList.LastOrDefault();
            decimal dcPeriod = (0.25m * instPeriod) + (0.75m * prevDcPeriod);
            dcPeriodList.Add(dcPeriod);

            var signal = GetCompareSignal(qu - (-1 * ip), prevQu - (-1 * prevIp));
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> phaseList = new();
        List<decimal> dPhaseList = new();
        List<decimal> dcPeriodList = new();
        List<decimal> v1List = new();
        List<decimal> ipList = new();
        List<decimal> quList = new();
        List<decimal> siList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal currentValue = inputList.ElementAtOrDefault(i);
            decimal prevValue = i >= length1 ? inputList.ElementAtOrDefault(i - length1) : 0;
            decimal priorV1 = i >= length1 ? v1List.ElementAtOrDefault(i - length1) : 0;
            decimal prevV12 = i >= 2 ? v1List.ElementAtOrDefault(i - 2) : 0;
            decimal prevV14 = i >= 4 ? v1List.ElementAtOrDefault(i - 4) : 0;

            decimal v1 = currentValue - prevValue;
            v1List.Add(v1);

            decimal v2 = i >= 3 ? v1List.ElementAtOrDefault(i - 3) : 0;
            decimal v3 = (0.75m * (v1 - priorV1)) + (0.25m * (prevV12 - prevV14));
            decimal prevIp = ipList.LastOrDefault();
            decimal ip = (0.33m * v2) + (0.67m * prevIp);
            ipList.Add(ip);

            decimal prevQu = quList.LastOrDefault();
            decimal qu = (0.2m * v3) + (0.8m * prevQu);
            quList.Add(qu);

            decimal prevPhase = phaseList.LastOrDefault();
            decimal phase = Math.Abs(ip + prevIp) > 0 ? Atan(Math.Abs((qu + prevQu) / (ip + prevIp))).ToDegrees() : 0;
            phase = ip < 0 && qu > 0 ? 180 - phase : phase;
            phase = ip < 0 && qu < 0 ? 180 + phase : phase;
            phase = ip > 0 && qu < 0 ? 360 - phase : phase;
            phaseList.Add(phase);

            decimal dPhase = prevPhase - phase;
            dPhase = prevPhase < 90 && phase > 270 ? 360 + prevPhase - phase : dPhase;
            dPhase = MinOrMax(dPhase, 60, 1);
            dPhaseList.Add(dPhase);

            decimal instPeriod = 0, v4 = 0;
            for (int j = 0; j <= length3; j++)
            {
                decimal prevDPhase = i >= j ? dPhaseList.ElementAtOrDefault(i - j) : 0;
                v4 += prevDPhase;
                instPeriod = v4 > 360 && instPeriod == 0 ? j : instPeriod;
            }

            decimal prevDcPeriod = dcPeriodList.LastOrDefault();
            decimal dcPeriod = (0.25m * instPeriod) + (0.75m * prevDcPeriod);
            dcPeriodList.Add(dcPeriod);

            decimal si = dcPeriod < length2 ? 0 : 1;
            siList.Add(si);

            var signal = GetCompareSignal(qu - (-1 * ip), prevQu - (-1 * prevIp));
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> peakList = new();
        List<decimal> realList = new();
        List<decimal> imagList = new();
        List<Signal> signalsList = new();

        var roofingFilterList = CalculateEhlersRoofingFilterV2(stockData, length1, length2).CustomValuesList;

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal roofingFilter = roofingFilterList.ElementAtOrDefault(i);
            decimal prevReal1 = i >= 1 ? realList.ElementAtOrDefault(i - 1) : 0;
            decimal prevReal2 = i >= 2 ? realList.ElementAtOrDefault(i - 2) : 0;
            decimal prevReal4 = i >= 4 ? realList.ElementAtOrDefault(i - 4) : 0;
            decimal prevReal6 = i >= 6 ? realList.ElementAtOrDefault(i - 6) : 0;
            decimal prevReal8 = i >= 8 ? realList.ElementAtOrDefault(i - 8) : 0;
            decimal prevReal10 = i >= 10 ? realList.ElementAtOrDefault(i - 10) : 0;
            decimal prevReal12 = i >= 12 ? realList.ElementAtOrDefault(i - 12) : 0;
            decimal prevReal14 = i >= 14 ? realList.ElementAtOrDefault(i - 14) : 0;
            decimal prevReal16 = i >= 16 ? realList.ElementAtOrDefault(i - 16) : 0;
            decimal prevReal18 = i >= 18 ? realList.ElementAtOrDefault(i - 18) : 0;
            decimal prevReal20 = i >= 20 ? realList.ElementAtOrDefault(i - 20) : 0;
            decimal prevReal22 = i >= 22 ? realList.ElementAtOrDefault(i - 22) : 0;

            decimal prevPeak = peakList.LastOrDefault();
            decimal peak = Math.Max(0.991m * prevPeak, Math.Abs(roofingFilter));
            peakList.Add(peak);

            decimal real = peak != 0 ? roofingFilter / peak : 0;
            realList.Add(real);

            decimal imag = ((0.091m * real) + (0.111m * prevReal2) + (0.143m * prevReal4) + (0.2m * prevReal6) + (0.333m * prevReal8) + prevReal10 -
                prevReal12 - (0.333m * prevReal14) - (0.2m * prevReal16) - (0.143m * prevReal18) - (0.111m * prevReal20) - (0.091m * prevReal22)) / 1.865m;
            imagList.Add(imag);

            var signal = GetCompareSignal(real - prevReal1, prevReal1 - prevReal2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
        {
            { "Real", realList },
            { "Imag", imagList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<decimal>();
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
        List<decimal> peakList = new();
        List<decimal> realList = new();
        List<decimal> imagList = new();
        List<decimal> qPeakList = new();
        List<Signal> signalsList = new();

        var roofingFilterList = CalculateEhlersRoofingFilterV2(stockData, length1, length2).CustomValuesList;

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal roofingFilter = roofingFilterList.ElementAtOrDefault(i);
            decimal prevReal1 = i >= 1 ? realList.ElementAtOrDefault(i - 1) : 0;
            decimal prevReal2 = i >= 2 ? realList.ElementAtOrDefault(i - 2) : 0;

            decimal prevPeak = peakList.LastOrDefault();
            decimal peak = Math.Max(0.991m * prevPeak, Math.Abs(roofingFilter));
            peakList.Add(peak);

            decimal prevReal = realList.LastOrDefault();
            decimal real = peak != 0 ? roofingFilter / peak : 0;
            realList.Add(real);

            decimal qFilt = real - prevReal;
            decimal prevQPeak = qPeakList.LastOrDefault();
            decimal qPeak = Math.Max(0.991m * prevQPeak, Math.Abs(qFilt));
            qPeakList.Add(qPeak);

            decimal imag = qPeak != 0 ? qFilt / qPeak : 0;
            imagList.Add(imag);

            var signal = GetCompareSignal(real - prevReal1, prevReal1 - prevReal2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
        {
            { "Real", realList },
            { "Imag", imagList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<decimal>();
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
        List<decimal> peakList = new();
        List<decimal> realList = new();
        List<decimal> imagList = new();
        List<decimal> qFiltList = new();
        List<decimal> qPeakList = new();
        List<Signal> signalsList = new();

        decimal a1 = Exp(-1.414m * Pi / length3);
        decimal b2 = 2 * a1 * Cos(1.414m * Pi / length3);
        decimal c2 = b2;
        decimal c3 = -a1 * a1;
        decimal c1 = 1 - c2 - c3;

        var roofingFilterList = CalculateEhlersRoofingFilterV2(stockData, length1, length2).CustomValuesList;

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal roofingFilter = roofingFilterList.ElementAtOrDefault(i);
            decimal prevQFilt = i >= 1 ? qFiltList.ElementAtOrDefault(i - 1) : 0;
            decimal prevImag1 = i >= 1 ? imagList.ElementAtOrDefault(i - 1) : 0;
            decimal prevImag2 = i >= 2 ? imagList.ElementAtOrDefault(i - 2) : 0;

            decimal prevPeak = peakList.LastOrDefault();
            decimal peak = Math.Max(0.991m * prevPeak, Math.Abs(roofingFilter));
            peakList.Add(peak);

            decimal prevReal = realList.LastOrDefault();
            decimal real = peak != 0 ? roofingFilter / peak : 0;
            realList.Add(real);

            decimal qFilt = real - prevReal;
            decimal prevQPeak = qPeakList.LastOrDefault();
            decimal qPeak = Math.Max(0.991m * prevQPeak, Math.Abs(qFilt));
            qPeakList.Add(qPeak);

            qFilt = qPeak != 0 ? qFilt / qPeak : 0;
            qFiltList.Add(qFilt);

            decimal imag = (c1 * ((qFilt + prevQFilt) / 2)) + (c2 * prevImag1) + (c3 * prevImag2);
            imagList.Add(imag);

            var signal = GetCompareSignal(imag - qFilt, prevImag1 - prevQFilt);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
        {
            { "Real", realList },
            { "Imag", imagList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<decimal>();
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
        List<decimal> periodList = new();
        List<decimal> domCycList = new();
        List<Signal> signalsList = new();

        decimal a1 = Exp(-1.414m * Pi / length2);
        decimal b1 = 2 * a1 * Cos(1.414m * Pi / length2);
        decimal c2 = b1;
        decimal c3 = -a1 * a1;
        decimal c1 = 1 - c2 - c3;

        var hilbertList = CalculateEhlersHilbertTransformer(stockData, length1, length2);
        var realList = hilbertList.OutputValues["Real"];
        var imagList = hilbertList.OutputValues["Imag"];

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal real = realList.ElementAtOrDefault(i);
            decimal imag = imagList.ElementAtOrDefault(i);
            decimal prevReal1 = i >= 1 ? realList.ElementAtOrDefault(i - 1) : 0;
            decimal prevReal2 = i >= 2 ? realList.ElementAtOrDefault(i - 2) : 0;
            decimal prevImag1 = i >= 1 ? imagList.ElementAtOrDefault(i - 1) : 0;
            decimal iDot = real - prevReal1;
            decimal prevDomCyc1 = i >= 1 ? domCycList.ElementAtOrDefault(i - 1) : 0;
            decimal prevDomCyc2 = i >= 2 ? domCycList.ElementAtOrDefault(i - 2) : 0;
            decimal qDot = imag - prevImag1;

            decimal prevPeriod = periodList.LastOrDefault();
            decimal period = (real * qDot) - (imag * iDot) != 0 ? 2 * Pi * ((real * real) + (imag * imag)) / ((-real * qDot) + (imag * iDot)) : 0;
            period = MinOrMax(period, length1, length3);
            periodList.Add(period);

            decimal domCyc = (c1 * ((period + prevPeriod) / 2)) + (c2 * prevDomCyc1) + (c3 * prevDomCyc2);
            domCycList.Add(domCyc);

            var signal = GetCompareSignal(real - prevReal1, prevReal1 - prevReal2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> phaseList = new();
        List<decimal> dPhaseList = new();
        List<decimal> instPeriodList = new();
        List<decimal> domCycList = new();
        List<Signal> signalsList = new();

        decimal a1 = Exp(-1.414m * Pi / length2);
        decimal b1 = 2 * a1 * Cos(1.414m * Pi / length2);
        decimal c2 = b1;
        decimal c3 = -a1 * a1;
        decimal c1 = 1 - c2 - c3;

        var hilbertList = CalculateEhlersHilbertTransformer(stockData, length1, length2);
        var realList = hilbertList.OutputValues["Real"];
        var imagList = hilbertList.OutputValues["Imag"];

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal real = realList.ElementAtOrDefault(i);
            decimal imag = imagList.ElementAtOrDefault(i);
            decimal prevReal1 = i >= 1 ? realList.ElementAtOrDefault(i - 1) : 0;
            decimal prevReal2 = i >= 2 ? realList.ElementAtOrDefault(i - 2) : 0;
            decimal prevDomCyc1 = i >= 1 ? domCycList.ElementAtOrDefault(i - 1) : 0;
            decimal prevDomCyc2 = i >= 2 ? domCycList.ElementAtOrDefault(i - 2) : 0;

            decimal prevPhase = phaseList.LastOrDefault();
            decimal phase = Math.Abs(real) > 0 ? Atan(Math.Abs(imag / real)).ToDegrees() : 0;
            phase = real < 0 && imag > 0 ? 180 - phase : phase;
            phase = real < 0 && imag < 0 ? 180 + phase : phase;
            phase = real > 0 && imag < 0 ? 360 - phase : phase;
            phaseList.Add(phase);

            decimal dPhase = prevPhase - phase;
            dPhase = prevPhase < 90 && phase > 270 ? 360 + prevPhase - phase : dPhase;
            dPhase = MinOrMax(dPhase, length1, length3);
            dPhaseList.Add(dPhase);

            decimal prevInstPeriod = instPeriodList.LastOrDefault();
            decimal instPeriod = 0, phaseSum = 0;
            for (int j = 0; j < length4; j++)
            {
                decimal prevDPhase = i >= j ? dPhaseList.ElementAtOrDefault(i - j) : 0;
                phaseSum += prevDPhase;

                if (phaseSum > 360 && instPeriod == 0)
                {
                    instPeriod = j;
                }
            }
            instPeriod = instPeriod == 0 ? prevInstPeriod : instPeriod;
            instPeriodList.Add(instPeriod);

            decimal domCyc = (c1 * ((instPeriod + prevInstPeriod) / 2)) + (c2 * prevDomCyc1) + (c3 * prevDomCyc2);
            domCycList.Add(domCyc);

            var signal = GetCompareSignal(real - prevReal1, prevReal1 - prevReal2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> periodList = new();
        List<decimal> domCycList = new();
        List<Signal> signalsList = new();

        decimal a1 = Exp(-1.414m * Pi / length2);
        decimal b1 = 2 * a1 * Cos(1.414m * Pi / length2);
        decimal c2 = b1;
        decimal c3 = -a1 * a1;
        decimal c1 = 1 - c2 - c3;

        var hilbertList = CalculateEhlersHilbertTransformer(stockData, length1, length2);
        var realList = hilbertList.OutputValues["Real"];
        var imagList = hilbertList.OutputValues["Imag"];

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal real = realList.ElementAtOrDefault(i);
            decimal imag = imagList.ElementAtOrDefault(i);
            decimal prevReal1 = i >= 1 ? realList.ElementAtOrDefault(i - 1) : 0;
            decimal prevImag1 = i >= 1 ? imagList.ElementAtOrDefault(i - 1) : 0;
            decimal prevReal2 = i >= 2 ? realList.ElementAtOrDefault(i - 2) : 0;
            decimal prevDomCyc1 = i >= 1 ? domCycList.ElementAtOrDefault(i - 1) : 0;
            decimal prevDomCyc2 = i >= 2 ? domCycList.ElementAtOrDefault(i - 2) : 0;
            decimal re = (real * prevReal1) + (imag * prevImag1);
            decimal im = (prevReal1 * imag) - (real * prevImag1);

            decimal prevPeriod = periodList.LastOrDefault();
            decimal period = im != 0 && re != 0 ? 2 * Pi / Math.Abs(im / re) : 0;
            period = MinOrMax(period, length1, length3);
            periodList.Add(period);

            decimal domCyc = (c1 * ((period + prevPeriod) / 2)) + (c2 * prevDomCyc1) + (c3 * prevDomCyc2);
            domCycList.Add(domCyc);

            var signal = GetCompareSignal(real - prevReal1, prevReal1 - prevReal2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> ampList = new();
        List<decimal> v2List = new();
        List<decimal> rangeList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        var hilbertTransformList = CalculateEhlersHilbertTransformIndicator(stockData, length: length);
        var inPhaseList = hilbertTransformList.OutputValues["Inphase"];
        var quadList = hilbertTransformList.OutputValues["Quad"];
        var emaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal currentEma = emaList.ElementAtOrDefault(i);
            decimal currentHigh = highList.ElementAtOrDefault(i);
            decimal currentLow = lowList.ElementAtOrDefault(i);
            decimal inPhase = inPhaseList.ElementAtOrDefault(i);
            decimal quad = quadList.ElementAtOrDefault(i);
            decimal currentValue = inputList.ElementAtOrDefault(i);
            decimal prevValue = i >= 1 ? inputList.ElementAtOrDefault(i - 1) : 0;
            decimal prevEma = i >= 1 ? emaList.ElementAtOrDefault(i - 1) : 0;

            decimal prevV2 = v2List.LastOrDefault();
            decimal v2 = (0.2m * ((inPhase * inPhase) + (quad * quad))) + (0.8m * prevV2);
            v2List.Add(v2);

            decimal prevRange = rangeList.LastOrDefault();
            decimal range = (0.2m * (currentHigh - currentLow)) + (0.8m * prevRange);
            rangeList.Add(range);

            decimal prevAmp = ampList.LastOrDefault();
            decimal amp = range != 0 ? (0.25m * ((10 * Log(v2 / (range * range)) / Log(10)) + 1.9m)) + (0.75m * prevAmp) : 0;
            ampList.Add(amp);

            var signal = GetVolatilitySignal(currentValue - currentEma, prevValue - prevEma, amp, 1.9m);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> rocList = new();
        List<decimal> derivList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, openList, _) = GetInputValuesList(stockData);

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal currentOpen = openList.ElementAtOrDefault(i);
            decimal currentValue = inputList.ElementAtOrDefault(i);
            
            decimal deriv = currentValue - currentOpen;
            derivList.Add(deriv);
        }

        var filtList = GetMovingAverageList(stockData, maType, length, derivList);
        for (int i = 0; i < stockData.Count; i++)
        {
            decimal filt = filtList.ElementAtOrDefault(i);
            decimal prevFilt1 = i >= 1 ? filtList.ElementAtOrDefault(i - 1) : 0;
            decimal prevFilt2 = i >= 2 ? filtList.ElementAtOrDefault(i - 2) : 0;

            decimal roc = length / 2 * Pi * (filt - prevFilt1);
            rocList.Add(roc);

            var signal = GetCompareSignal(filt - prevFilt1, prevFilt1 - prevFilt2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        int length = 20, decimal pedestal = 10)
    {
        List<decimal> rocList = new();
        List<decimal> derivList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, openList, _) = GetInputValuesList(stockData);

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal currentOpen = openList.ElementAtOrDefault(i);
            decimal currentValue = inputList.ElementAtOrDefault(i);
            

            decimal deriv = currentValue - currentOpen;
            derivList.Add(deriv);
        }

        var filtList = GetMovingAverageList(stockData, maType, length, derivList);
        for (int i = 0; i < stockData.Count; i++)
        {
            decimal filt = filtList.ElementAtOrDefault(i);
            decimal prevFilt1 = i >= 1 ? filtList.ElementAtOrDefault(i - 1) : 0;
            decimal prevFilt2 = i >= 2 ? filtList.ElementAtOrDefault(i - 2) : 0;

            decimal roc = length / 2 * Pi * (filt - prevFilt1);
            rocList.Add(roc);

            var signal = GetCompareSignal(filt - prevFilt1, prevFilt1 - prevFilt2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> rocList = new();
        List<decimal> derivList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, openList, _) = GetInputValuesList(stockData);

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal currentOpen = openList.ElementAtOrDefault(i);
            decimal currentValue = inputList.ElementAtOrDefault(i);
            
            decimal deriv = currentValue - currentOpen;
            derivList.Add(deriv);
        }

        var filtList = GetMovingAverageList(stockData, maType, length, derivList);
        for (int i = 0; i < stockData.Count; i++)
        {
            decimal filt = filtList.ElementAtOrDefault(i);
            decimal prevFilt1 = i >= 1 ? filtList.ElementAtOrDefault(i - 1) : 0;
            decimal prevFilt2 = i >= 2 ? filtList.ElementAtOrDefault(i - 2) : 0;

            decimal roc = length / 2 * Pi * (filt - prevFilt1);
            rocList.Add(roc);

            var signal = GetCompareSignal(filt - prevFilt1, prevFilt1 - prevFilt2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> rocList = new();
        List<decimal> derivList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, openList, _) = GetInputValuesList(stockData);

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal currentOpen = openList.ElementAtOrDefault(i);
            decimal currentValue = inputList.ElementAtOrDefault(i);

            decimal deriv = currentValue - currentOpen;
            derivList.Add(deriv);
        }

        var filtList = GetMovingAverageList(stockData, maType, length, derivList);
        var filtMa1List = GetMovingAverageList(stockData, maType, length, filtList);
        var filtMa2List = GetMovingAverageList(stockData, maType, length, filtMa1List);
        for (int i = 0; i < stockData.Count; i++)
        {
            decimal filt = filtMa2List.ElementAtOrDefault(i);
            decimal prevFilt1 = i >= 1 ? filtMa2List.ElementAtOrDefault(i - 1) : 0;
            decimal prevFilt2 = i >= 2 ? filtMa2List.ElementAtOrDefault(i - 2) : 0;

            decimal roc = length / 2 * Pi * (filt - prevFilt1);
            rocList.Add(roc);

            var signal = GetCompareSignal(filt - prevFilt1, prevFilt1 - prevFilt2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> snrList = new();
        List<decimal> rangeList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        var ehlersMamaList = CalculateEhlersMotherOfAdaptiveMovingAverages(stockData);
        var i1List = ehlersMamaList.OutputValues["I1"];
        var q1List = ehlersMamaList.OutputValues["Q1"];
        var mamaList = ehlersMamaList.CustomValuesList;

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal currentValue = inputList.ElementAtOrDefault(i);
            decimal currentHigh = highList.ElementAtOrDefault(i);
            decimal currentLow = lowList.ElementAtOrDefault(i);
            decimal prevMama = i >= 1 ? mamaList.ElementAtOrDefault(i - 1) : 0;
            decimal i1 = i1List.ElementAtOrDefault(i);
            decimal q1 = q1List.ElementAtOrDefault(i);
            decimal mama = mamaList.ElementAtOrDefault(i);
            decimal prevValue = i >= 1 ? inputList.ElementAtOrDefault(i - 1) : 0;

            decimal prevRange = rangeList.LastOrDefault();
            decimal range = (0.1m * (currentHigh - currentLow)) + (0.9m * prevRange);
            rangeList.Add(range);

            decimal temp = range != 0 ? ((i1 * i1) + (q1 * q1)) / (range * range) : 0;
            decimal prevSnr = snrList.LastOrDefault();
            decimal snr = range > 0 ? (0.25m * ((10 * Log(temp) / Log(10)) + length)) + (0.75m * prevSnr) : 0;
            snrList.Add(snr);

            var signal = GetVolatilitySignal(currentValue - mama, prevValue - prevMama, snr, length);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> q3List = new();
        List<decimal> i3List = new();
        List<decimal> noiseList = new();
        List<decimal> snrList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        var ehlersMamaList = CalculateEhlersMotherOfAdaptiveMovingAverages(stockData);
        var smoothList = ehlersMamaList.OutputValues["Smooth"];
        var smoothPeriodList = ehlersMamaList.OutputValues["SmoothPeriod"];

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal currentHigh = highList.ElementAtOrDefault(i);
            decimal currentLow = lowList.ElementAtOrDefault(i);
            decimal smooth = smoothList.ElementAtOrDefault(i);
            decimal prevSmooth2 = i >= 2 ? smoothList.ElementAtOrDefault(i - 2) : 0;
            decimal smoothPeriod = smoothPeriodList.ElementAtOrDefault(i);
            decimal currentValue = inputList.ElementAtOrDefault(i);
            decimal prevValue = i >= 1 ? inputList.ElementAtOrDefault(i - 1) : 0;
            decimal prevSmooth = i >= 1 ? smoothList.ElementAtOrDefault(i - 1) : 0;

            decimal q3 = 0.5m * (smooth - prevSmooth2) * ((0.1759m * smoothPeriod) + 0.4607m);
            q3List.Add(q3);

            int sp = (int)Math.Ceiling(smoothPeriod / 2);
            decimal i3 = 0;
            for (int j = 0; j <= sp - 1; j++)
            {
                decimal prevQ3 = i >= j ? q3List.ElementAtOrDefault(i - j) : 0;
                i3 += prevQ3;
            }
            i3 = sp != 0 ? 1.57m * i3 / sp : i3;
            i3List.Add(i3);

            decimal signalValue = (i3 * i3) + (q3 * q3);
            decimal prevNoise = noiseList.LastOrDefault();
            decimal noise = (0.1m * (currentHigh - currentLow) * (currentHigh - currentLow) * 0.25m) + (0.9m * prevNoise);
            noiseList.Add(noise);

            decimal temp = noise != 0 ? signalValue / noise : 0;
            decimal prevSnr = snrList.LastOrDefault();
            decimal snr = (0.33m * (10 * Log(temp) / Log(10))) + (0.67m * prevSnr);
            snrList.Add(snr);

            var signal = GetVolatilitySignal(currentValue - smooth, prevValue - prevSmooth, snr, length);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> iqList = new();
        List<Signal> signalsList = new();

        var snrv2List = CalculateEhlersEnhancedSignalToNoiseRatio(stockData, length);
        var smoothPeriodList = snrv2List.OutputValues["SmoothPeriod"];
        var q3List = snrv2List.OutputValues["Q3"];
        var i3List = snrv2List.OutputValues["I3"];

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal smoothPeriod = smoothPeriodList.ElementAtOrDefault(i);
            decimal i3 = i3List.ElementAtOrDefault(i);
            decimal prevI3 = i >= 1 ? i3List.ElementAtOrDefault(i - 1) : 0;
            decimal prevIq = i >= 1 ? iqList.ElementAtOrDefault(i - 1) : 0;

            int maxCount = (int)Math.Ceiling(smoothPeriod / 4);
            decimal iq = 0;
            for (int j = 0; j <= maxCount - 1; j++)
            {
                decimal prevQ3 = i >= j ? q3List.ElementAtOrDefault(i - j) : 0;
                iq += prevQ3;
            }
            iq = maxCount != 0 ? 1.25m * iq / maxCount : iq;
            iqList.Add(iq);

            var signal = GetCompareSignal(iq - i3, prevIq - prevI3);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
        {
            { "I3", i3List },
            { "IQ", iqList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<decimal>();
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
        List<decimal> snrList = new();
        List<decimal> rangeList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        var ehlersMamaList = CalculateEhlersMotherOfAdaptiveMovingAverages(stockData);
        var reList = ehlersMamaList.OutputValues["Real"];
        var imList = ehlersMamaList.OutputValues["Imag"];
        var mamaList = ehlersMamaList.CustomValuesList;

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal mama = mamaList.ElementAtOrDefault(i);
            decimal re = reList.ElementAtOrDefault(i);
            decimal im = imList.ElementAtOrDefault(i);
            decimal currentValue = inputList.ElementAtOrDefault(i);
            decimal currentHigh = highList.ElementAtOrDefault(i);
            decimal currentLow = lowList.ElementAtOrDefault(i);
            decimal prevValue = i >= 1 ? inputList.ElementAtOrDefault(i - 1) : 0;
            decimal prevMama = i >= 1 ? mamaList.ElementAtOrDefault(i - 1) : 0;

            decimal prevRange = rangeList.LastOrDefault();
            decimal range = (0.1m * (currentHigh - currentLow)) + (0.9m * prevRange);
            rangeList.Add(range);

            decimal temp = range != 0 ? (re + im) / (range * range) : 0;
            decimal prevSnr = snrList.LastOrDefault();
            decimal snr = (0.25m * ((10 * Log(temp) / Log(10)) + length)) + (0.75m * prevSnr);
            snrList.Add(snr);

            var signal = GetVolatilitySignal(currentValue - mama, prevValue - prevMama, snr, length);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> cleanedDataList = new();
        List<decimal> hpList = new();
        List<decimal> powerList = new();
        List<decimal> dominantCycleList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        decimal twoPiPrd = MinOrMax(2 * Pi / length, 0.99m, 0.01m);
        decimal alpha = (1 - Sin(twoPiPrd)) / Cos(twoPiPrd);

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal currentValue = inputList.ElementAtOrDefault(i);
            decimal prevValue1 = i >= 1 ? inputList.ElementAtOrDefault(i - 1) : 0;
            decimal prevHp1 = i >= 1 ? hpList.ElementAtOrDefault(i - 1) : 0;
            decimal prevHp2 = i >= 2 ? hpList.ElementAtOrDefault(i - 2) : 0;
            decimal prevHp3 = i >= 3 ? hpList.ElementAtOrDefault(i - 3) : 0;
            decimal prevHp4 = i >= 4 ? hpList.ElementAtOrDefault(i - 4) : 0;
            decimal prevHp5 = i >= 5 ? hpList.ElementAtOrDefault(i - 5) : 0;

            decimal hp = i <= 5 ? currentValue : (0.5m * (1 + alpha) * (currentValue - prevValue1)) + (alpha * prevHp1);
            hpList.Add(hp);

            decimal cleanedData = i <= 5 ? currentValue : (hp + (2 * prevHp1) + (3 * prevHp2) + (3 * prevHp3) + (2 * prevHp4) + prevHp5) / 12;
            cleanedDataList.Add(cleanedData);

            decimal pwr = 0, cosPart = 0, sinPart = 0;
            for (int j = minLength; j <= maxLength; j++)
            {
                for (int n = 0; n <= maxLength - 1; n++)
                {
                    decimal prevCleanedData = i >= n ? cleanedDataList.ElementAtOrDefault(i - n) : 0;
                    cosPart += prevCleanedData * Cos(MinOrMax(2 * Pi * ((decimal)n / j), 0.99m, 0.01m));
                    sinPart += prevCleanedData * Sin(MinOrMax(2 * Pi * ((decimal)n / j), 0.99m, 0.01m));
                }

                pwr = (cosPart * cosPart) + (sinPart * sinPart);
            }
            powerList.Add(pwr);

            decimal maxPwr = i >= minLength ? powerList.ElementAtOrDefault(i - minLength) : 0;
            decimal num = 0, denom = 0;
            for (int period = minLength; period <= maxLength; period++)
            {
                decimal prevPwr = i >= period ? powerList.ElementAtOrDefault(i - period) : 0;
                maxPwr = prevPwr > maxPwr ? prevPwr : maxPwr;
                decimal db = maxPwr > 0 && prevPwr > 0 ? -10 * Log(0.01m / (1 - (0.99m * prevPwr / maxPwr))) / Log(10) : 0;
                db = db > 20 ? 20 : db;

                num += db < 3 ? period * (3 - db) : 0;
                denom += db < 3 ? 3 - db : 0;
            }

            decimal dominantCycle = denom != 0 ? num / denom : 0;
            dominantCycleList.Add(dominantCycle);

            var signal = GetCompareSignal(hp, prevHp1);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
    public static StockData CalculateEhlersFourierSeriesAnalysis(this StockData stockData, int length = 20, decimal bw = 0.1m)
    {
        List<decimal> bp1List = new();
        List<decimal> bp2List = new();
        List<decimal> bp3List = new();
        List<decimal> q1List = new();
        List<decimal> q2List = new();
        List<decimal> q3List = new();
        List<decimal> waveList = new();
        List<decimal> rocList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        decimal l1 = Cos(2 * Pi / length);
        decimal g1 = Cos(bw * 2 * Pi / length);
        decimal s1 = (1 / g1) - Sqrt((1 / (g1 * g1)) - 1);
        decimal l2 = Cos(2 * Pi / ((decimal)length / 2));
        decimal g2 = Cos(bw * 2 * Pi / ((decimal)length / 2));
        decimal s2 = (1 / g2) - Sqrt((1 / (g2 * g2)) - 1);
        decimal l3 = Cos(2 * Pi / ((decimal)length / 3));
        decimal g3 = Cos(bw * 2 * Pi / ((decimal)length / 3));
        decimal s3 = (1 / g3) - Sqrt((1 / (g3 * g3)) - 1);

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal currentValue = inputList.ElementAtOrDefault(i);
            decimal prevBp1_1 = bp1List.LastOrDefault();
            decimal prevBp2_1 = bp2List.LastOrDefault();
            decimal prevBp3_1 = bp3List.LastOrDefault();
            decimal prevValue2 = i >= 2 ? inputList.ElementAtOrDefault(i - 2) : 0;
            decimal prevBp1_2 = i >= 2 ? bp1List.ElementAtOrDefault(i - 2) : 0;
            decimal prevBp2_2 = i >= 2 ? bp2List.ElementAtOrDefault(i - 2) : 0;
            decimal prevBp3_2 = i >= 2 ? bp3List.ElementAtOrDefault(i - 2) : 0;
            decimal prevWave2 = i >= 2 ? waveList.ElementAtOrDefault(i - 2) : 0;

            decimal bp1 = i <= 3 ? 0 : (0.5m * (1 - s1) * (currentValue - prevValue2)) + (l1 * (1 + s1) * prevBp1_1) - (s1 * prevBp1_2);
            bp1List.Add(bp1);

            decimal q1 = i <= 4 ? 0 : length / 2 * Pi * (bp1 - prevBp1_1);
            q1List.Add(q1);

            decimal bp2 = i <= 3 ? 0 : (0.5m * (1 - s2) * (currentValue - prevValue2)) + (l2 * (1 + s2) * prevBp2_1) - (s2 * prevBp2_2);
            bp2List.Add(bp2);

            decimal q2 = i <= 4 ? 0 : length / 2 * Pi * (bp2 - prevBp2_1);
            q2List.Add(q2);

            decimal bp3 = i <= 3 ? 0 : (0.5m * (1 - s3) * (currentValue - prevValue2)) + (l3 * (1 + s3) * prevBp3_1) - (s3 * prevBp3_2);
            bp3List.Add(bp3);

            decimal q3 = i <= 4 ? 0 : length / 2 * Pi * (bp3 - prevBp3_1);
            q3List.Add(q3);

            decimal p1 = 0, p2 = 0, p3 = 0;
            for (int j = 0; j <= length - 1; j++)
            {
                decimal prevBp1 = i >= j ? bp1List.ElementAtOrDefault(i - j) : 0;
                decimal prevBp2 = i >= j ? bp2List.ElementAtOrDefault(i - j) : 0;
                decimal prevBp3 = i >= j ? bp3List.ElementAtOrDefault(i - j) : 0;
                decimal prevQ1 = i >= j ? q1List.ElementAtOrDefault(i - j) : 0;
                decimal prevQ2 = i >= j ? q2List.ElementAtOrDefault(i - j) : 0;
                decimal prevQ3 = i >= j ? q3List.ElementAtOrDefault(i - j) : 0;

                p1 += (prevBp1 * prevBp1) + (prevQ1 * prevQ1);
                p2 += (prevBp2 * prevBp2) + (prevQ2 * prevQ2);
                p3 += (prevBp3 * prevBp3) + (prevQ3 * prevQ3);
            }

            decimal prevWave = waveList.LastOrDefault();
            decimal wave = p1 != 0 ? bp1 + (Sqrt(p2 / p1) * bp2) + (Sqrt(p3 / p1) * bp3) : 0;
            waveList.Add(wave);

            decimal roc = length / Pi * 4 * (wave - prevWave2);
            rocList.Add(roc);

            var signal = GetCompareSignal(wave, prevWave);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
        {
            { "Wave", waveList },
            { "Roc", rocList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<decimal>();
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
    public static StockData CalculateEhlersVossPredictiveFilter(this StockData stockData, int length = 20, decimal predict = 3, decimal bw = 0.25m)
    {
        List<decimal> filtList = new();
        List<decimal> vossList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        int order = MinOrMax((int)Math.Ceiling(3 * predict));
        decimal f1 = Cos(2 * Pi / length);
        decimal g1 = Cos(bw * 2 * Pi / length);
        decimal s1 = (1 / g1) - Sqrt((1 / (g1 * g1)) - 1);

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal currentValue = inputList.ElementAtOrDefault(i);
            decimal prevFilt1 = i >= 1 ? filtList.ElementAtOrDefault(i - 1) : 0;
            decimal prevFilt2 = i >= 2 ? filtList.ElementAtOrDefault(i - 2) : 0;
            decimal prevValue2 = i >= 2 ? inputList.ElementAtOrDefault(i - 2) : 0;

            decimal filt = i <= 5 ? 0 : (0.5m * (1 - s1) * (currentValue - prevValue2)) + (f1 * (1 + s1) * prevFilt1) - (s1 * prevFilt2);
            filtList.Add(filt);

            decimal sumC = 0;
            for (int j = 0; j <= order - 1; j++)
            {
                decimal prevVoss = i >= order - j ? vossList.ElementAtOrDefault(i - (order - j)) : 0;
                sumC += (decimal)(j + 1) / order * prevVoss;
            }

            decimal prevvoss = vossList.LastOrDefault();
            decimal voss = ((decimal)(3 + order) / 2 * filt) - sumC;
            vossList.Add(voss);

            var signal = GetCompareSignal(voss - filt, prevvoss - prevFilt1);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
        {
            { "Voss", vossList },
            { "Filt", filtList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<decimal>();
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
    public static StockData CalculateEhlersSwissArmyKnifeIndicator(this StockData stockData, int length = 20, decimal delta = 0.1m)
    {
        List<decimal> emaFilterList = new();
        List<decimal> smaFilterList = new();
        List<decimal> gaussFilterList = new();
        List<decimal> butterFilterList = new();
        List<decimal> smoothFilterList = new();
        List<decimal> hpFilterList = new();
        List<decimal> php2FilterList = new();
        List<decimal> bpFilterList = new();
        List<decimal> bsFilterList = new();
        List<decimal> filterAvgList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        decimal twoPiPrd = MinOrMax(2 * Pi / length, 0.99m, 0.01m);
        decimal deltaPrd = MinOrMax(2 * Pi * 2 * delta / length, 0.99m, 0.01m);

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal currentValue = inputList.ElementAtOrDefault(i);
            decimal prevPrice1 = i >= 1 ? inputList.ElementAtOrDefault(i - 1) : 0;
            decimal prevPrice2 = i >= 2 ? inputList.ElementAtOrDefault(i - 2) : 0;
            decimal prevPrice = i >= length ? inputList.ElementAtOrDefault(i - length) : 0;
            decimal prevEmaFilter1 = emaFilterList.LastOrDefault();
            decimal prevSmaFilter1 = smaFilterList.LastOrDefault();
            decimal prevGaussFilter1 = gaussFilterList.LastOrDefault();
            decimal prevButterFilter1 = butterFilterList.LastOrDefault();
            decimal prevSmoothFilter1 = smoothFilterList.LastOrDefault();
            decimal prevHpFilter1 = hpFilterList.LastOrDefault();
            decimal prevPhp2Filter1 = php2FilterList.LastOrDefault();
            decimal prevBpFilter1 = bpFilterList.LastOrDefault();
            decimal prevBsFilter1 = bsFilterList.LastOrDefault();
            decimal prevEmaFilter2 = i >= 2 ? emaFilterList.ElementAtOrDefault(i - 2) : 0;
            decimal prevSmaFilter2 = i >= 2 ? smaFilterList.ElementAtOrDefault(i - 2) : 0;
            decimal prevGaussFilter2 = i >= 2 ? gaussFilterList.ElementAtOrDefault(i - 2) : 0;
            decimal prevButterFilter2 = i >= 2 ? butterFilterList.ElementAtOrDefault(i - 2) : 0;
            decimal prevSmoothFilter2 = i >= 2 ? smoothFilterList.ElementAtOrDefault(i - 2) : 0;
            decimal prevHpFilter2 = i >= 2 ? hpFilterList.ElementAtOrDefault(i - 2) : 0;
            decimal prevPhp2Filter2 = i >= 2 ? php2FilterList.ElementAtOrDefault(i - 2) : 0;
            decimal prevBpFilter2 = i >= 2 ? bpFilterList.ElementAtOrDefault(i - 2) : 0;
            decimal prevBsFilter2 = i >= 2 ? bsFilterList.ElementAtOrDefault(i - 2) : 0;
            decimal alpha = (Cos(twoPiPrd) + Sin(twoPiPrd) - 1) / Cos(twoPiPrd), c0 = 1, c1 = 0, b0 = alpha, b1 = 0, b2 = 0, a1 = 1 - alpha, a2 = 0;

            decimal emaFilter = i <= length ? currentValue :
                (c0 * ((b0 * currentValue) + (b1 * prevPrice1) + (b2 * prevPrice2))) + (a1 * prevEmaFilter1) + (a2 * prevEmaFilter2) - (c1 * prevPrice);
            emaFilterList.Add(emaFilter);

            int n = length; c0 = 1; c1 = (decimal)1 / n; b0 = (decimal)1 / n; b1 = 0; b2 = 0; a1 = 1; a2 = 0;
            decimal smaFilter = i <= length ? currentValue :
                (c0 * ((b0 * currentValue) + (b1 * prevPrice1) + (b2 * prevPrice2))) + (a1 * prevSmaFilter1) + (a2 * prevSmaFilter2) - (c1 * prevPrice);
            smaFilterList.Add(smaFilter);

            decimal beta = 2.415m * (1 - Cos(twoPiPrd)), sqrtData = Pow(beta, 2) + (2 * beta), sqrt = Sqrt(sqrtData); alpha = (-1 * beta) + sqrt;
            c0 = Pow(alpha, 2); c1 = 0; b0 = 1; b1 = 0; b2 = 0; a1 = 2 * (1 - alpha); a2 = -(1 - alpha) * (1 - alpha);
            decimal gaussFilter = i <= length ? currentValue :
                (c0 * ((b0 * currentValue) + (b1 * prevPrice1) + (b2 * prevPrice2))) + (a1 * prevGaussFilter1) + (a2 * prevGaussFilter2) - (c1 * prevPrice);
            gaussFilterList.Add(gaussFilter);

            beta = 2.415m * (1 - Cos(twoPiPrd)); sqrtData = (beta * beta) + (2 * beta); sqrt = sqrtData >= 0 ? Sqrt(sqrtData) : 0; alpha = (-1 * beta) + sqrt;
            c0 = Pow(alpha, 2) / 4; c1 = 0; b0 = 1; b1 = 2; b2 = 1; a1 = 2 * (1 - alpha); a2 = -(1 - alpha) * (1 - alpha);
            decimal butterFilter = i <= length ? currentValue :
                (c0 * ((b0 * currentValue) + (b1 * prevPrice1) + (b2 * prevPrice2))) + (a1 * prevButterFilter1) + (a2 * prevButterFilter2) - (c1 * prevPrice);
            butterFilterList.Add(butterFilter);

            c0 = (decimal)1 / 4; c1 = 0; b0 = 1; b1 = 2; b2 = 1; a1 = 0; a2 = 0;
            decimal smoothFilter = (c0 * ((b0 * currentValue) + (b1 * prevPrice1) + (b2 * prevPrice2))) + (a1 * prevSmoothFilter1) + 
                (a2 * prevSmoothFilter2) - (c1 * prevPrice);
            smoothFilterList.Add(smoothFilter);

            alpha = (Cos(twoPiPrd) + Sin(twoPiPrd) - 1) / Cos(twoPiPrd); c0 = 1 - (alpha / 2); c1 = 0; b0 = 1; b1 = -1; b2 = 0; a1 = 1 - alpha; a2 = 0;
            decimal hpFilter = i <= length ? 0 :
                (c0 * ((b0 * currentValue) + (b1 * prevPrice1) + (b2 * prevPrice2))) + (a1 * prevHpFilter1) + (a2 * prevHpFilter2) - (c1 * prevPrice);
            hpFilterList.Add(hpFilter);

            beta = 2.415m * (1 - Cos(twoPiPrd)); sqrtData = Pow(beta, 2) + (2 * beta); sqrt = sqrtData >= 0 ? Sqrt(sqrtData) : 0; alpha = (-1 * beta) + sqrt; 
            c0 = (1 - (alpha / 2)) * (1 - (alpha / 2)); c1 = 0; b0 = 1; b1 = -2; b2 = 1; a1 = 2 * (1 - alpha); a2 = -(1 - alpha) * (1 - alpha);
            decimal php2Filter = i <= length ? 0 :
                (c0 * ((b0 * currentValue) + (b1 * prevPrice1) + (b2 * prevPrice2))) + (a1 * prevPhp2Filter1) + (a2 * prevPhp2Filter2) - (c1 * prevPrice);
            php2FilterList.Add(php2Filter);

            beta = Cos(twoPiPrd); decimal gamma = 1 / Cos(deltaPrd); sqrtData = Pow(gamma, 2) - 1; sqrt = Sqrt(sqrtData);
            alpha = gamma - sqrt; c0 = (1 - alpha) / 2; c1 = 0; b0 = 1; b1 = 0; b2 = -1; a1 = beta * (1 + alpha); a2 = alpha * -1;
            decimal bpFilter = i <= length ? currentValue :
                (c0 * ((b0 * currentValue) + (b1 * prevPrice1) + (b2 * prevPrice2))) + (a1 * prevBpFilter1) + (a2 * prevBpFilter2) - (c1 * prevPrice);
            bpFilterList.Add(bpFilter);

            beta = Cos(twoPiPrd); gamma = 1 / Cos(deltaPrd); sqrtData = Pow(gamma, 2) - 1; sqrt = sqrtData >= 0 ? Sqrt(sqrtData) : 0;
            alpha = gamma - sqrt; c0 = (1 + alpha) / 2; c1 = 0; b0 = 1; b1 = -2 * beta; b2 = 1; a1 = beta * (1 + alpha); a2 = alpha * -1;
            decimal bsFilter = i <= length ? currentValue :
                (c0 * ((b0 * currentValue) + (b1 * prevPrice1) + (b2 * prevPrice2))) + (a1 * prevBsFilter1) + (a2 * prevBsFilter2) - (c1 * prevPrice);
            bsFilterList.Add(bsFilter);

            var signal = GetCompareSignal(smaFilter - prevSmaFilter1, prevSmaFilter1 - prevSmaFilter2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> euoList = new();
        List<decimal> whitenoiseList = new();
        List<decimal> filtList = new();
        List<decimal> pkList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        decimal a1 = Exp(-MinOrMax(1.414m * Pi / length, 0.99m, 0.01m));
        decimal b1 = 2 * a1 * Cos(1.414m * Pi / length);
        decimal c2 = b1;
        decimal c3 = -a1 * a1;
        decimal c1 = 1 - c2 - c3;

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal currentValue = inputList.ElementAtOrDefault(i);
            decimal prevValue2 = i >= 2 ? inputList.ElementAtOrDefault(i - 2) : 0;
            decimal prevFilt2 = i >= 2 ? filtList.ElementAtOrDefault(i - 2) : 0;

            decimal prevWhitenoise = whitenoiseList.LastOrDefault();
            decimal whitenoise = (currentValue - prevValue2) / 2;
            whitenoiseList.Add(whitenoise);

            decimal prevFilt1 = filtList.LastOrDefault();
            decimal filt = (c1 * ((whitenoise + prevWhitenoise) / 2)) + (c2 * prevFilt1) + (c3 * prevFilt2);
            filtList.Add(filt);

            decimal prevPk = pkList.LastOrDefault();
            decimal pk = Math.Abs(filt) > prevPk ? Math.Abs(filt) : 0.991m * prevPk;
            pkList.Add(pk);

            decimal denom = pk == 0 ? -1 : pk;
            decimal prevEuo = euoList.LastOrDefault();
            decimal euo = denom == -1 ? prevEuo : pk != 0 ? filt / pk : 0;
            euoList.Add(euo);
        }

        var euoMaList = GetMovingAverageList(stockData, maType, signalLength, euoList);
        for (int i = 0; i < stockData.Count; i++)
        {
            decimal euo = euoList.ElementAtOrDefault(i);
            decimal euoMa = euoMaList.ElementAtOrDefault(i);
            decimal prevEuo = i >= 1 ? euoList.ElementAtOrDefault(i - 1) : 0;
            decimal prevEuoMa = i >= 1 ? euoMaList.ElementAtOrDefault(i - 1) : 0;

            var signal = GetCompareSignal(euo - euoMa, prevEuo - prevEuoMa);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> deliList = new();
        List<decimal> ema1List = new();
        List<decimal> ema2List = new();
        List<decimal> dspList = new();
        List<decimal> tempList = new();
        List<Signal> signalsList = new();
        var (_, highList, lowList, _, _) = GetInputValuesList(stockData);

        decimal alpha = length > 2 ? (decimal)2 / (length + 1) : 0.67m;
        decimal alpha2 = alpha / 2;

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal prevHigh = i >= 1 ? highList.ElementAtOrDefault(i - 1) : 0;
            decimal prevLow = i >= 1 ? lowList.ElementAtOrDefault(i - 1) : 0;
            decimal currentHigh = Math.Max(prevHigh, highList.ElementAtOrDefault(i));
            decimal currentLow = Math.Min(prevLow, lowList.ElementAtOrDefault(i));
            decimal currentPrice = (currentHigh + currentLow) / 2;
            decimal prevEma1 = i >= 1 ? ema1List.LastOrDefault() : currentPrice;
            decimal prevEma2 = i >= 1 ? ema2List.LastOrDefault() : currentPrice;

            decimal ema1 = (alpha * currentPrice) + ((1 - alpha) * prevEma1);
            ema1List.Add(ema1);

            decimal ema2 = (alpha2 * currentPrice) + ((1 - alpha2) * prevEma2);
            ema2List.Add(ema2);

            decimal dsp = ema1 - ema2;
            dspList.Add(dsp);

            decimal prevTemp = tempList.LastOrDefault();
            decimal temp = (alpha * dsp) + ((1 - alpha) * prevTemp);
            tempList.Add(temp);

            decimal prevDeli = deliList.LastOrDefault();
            decimal deli = dsp - temp;
            deliList.Add(deli);

            var signal = GetCompareSignal(deli, prevDeli);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
    public static StockData CalculateEhlersBandPassFilterV1(this StockData stockData, int length = 20, decimal bw = 0.3m)
    {
        List<decimal> hpList = new();
        List<decimal> bpList = new();
        List<decimal> peakList = new();
        List<decimal> signalList = new();
        List<decimal> triggerList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        decimal twoPiPrd1 = MinOrMax(0.25m * bw * 2 * Pi / length, 0.99m, 0.01m);
        decimal twoPiPrd2 = MinOrMax(1.5m * bw * 2 * Pi / length, 0.99m, 0.01m);
        decimal beta = Cos(MinOrMax(2 * Pi / length, 0.99m, 0.01m));
        decimal gamma = 1 / Cos(MinOrMax(2 * Pi * bw / length, 0.99m, 0.01m));
        decimal alpha1 = gamma - Sqrt(Pow(gamma, 2) - 1);
        decimal alpha2 = (Cos(twoPiPrd1) + Sin(twoPiPrd1) - 1) / Cos(twoPiPrd1);
        decimal alpha3 = (Cos(twoPiPrd2) + Sin(twoPiPrd2) - 1) / Cos(twoPiPrd2);

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal currentValue = inputList.ElementAtOrDefault(i);
            decimal prevValue = i >= 1 ? inputList.ElementAtOrDefault(i - 1) : 0;
            decimal prevHp1 = i >= 1 ? hpList.ElementAtOrDefault(i - 1) : 0;
            decimal prevHp2 = i >= 2 ? hpList.ElementAtOrDefault(i - 2) : 0;
            decimal prevBp1 = i >= 1 ? bpList.ElementAtOrDefault(i - 1) : 0;
            decimal prevBp2 = i >= 2 ? bpList.ElementAtOrDefault(i - 2) : 0;

            decimal hp = ((1 + (alpha2 / 2)) * (currentValue - prevValue)) + ((1 - alpha2) * prevHp1);
            hpList.Add(hp);

            decimal bp = i > 2 ? (0.5m * (1 - alpha1) * (hp - prevHp2)) + (beta * (1 + alpha1) * prevBp1) - (alpha1 * prevBp2) : 0;
            bpList.Add(bp);

            decimal prevPeak = peakList.LastOrDefault();
            decimal peak = Math.Max(0.991m * prevPeak, Math.Abs(bp));
            peakList.Add(peak);

            decimal prevSig = signalList.LastOrDefault();
            decimal sig = peak != 0 ? bp / peak : 0;
            signalList.Add(sig);

            decimal prevTrigger = triggerList.LastOrDefault();
            decimal trigger = ((1 + (alpha3 / 2)) * (sig - prevSig)) + ((1 - alpha3) * prevTrigger);
            triggerList.Add(trigger);

            var signal = GetCompareSignal(sig - trigger, prevSig - prevTrigger);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
    public static StockData CalculateEhlersBandPassFilterV2(this StockData stockData, int length = 20, decimal bw = 0.3m)
    {
        List<decimal> bpList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        decimal l1 = Cos(MinOrMax(2 * Pi / length, 0.99m, 0.01m));
        decimal g1 = Cos(MinOrMax(bw * 2 * Pi / length, 0.99m, 0.01m));
        decimal s1 = (1 / g1) - Sqrt(1 / Pow(g1, 2) - 1);

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal currentValue = inputList.ElementAtOrDefault(i);
            decimal prevValue = i >= 2 ? inputList.ElementAtOrDefault(i - 2) : 0;
            decimal prevBp1 = i >= 1 ? bpList.ElementAtOrDefault(i - 1) : 0;
            decimal prevBp2 = i >= 2 ? bpList.ElementAtOrDefault(i - 2) : 0;

            decimal bp = i < 3 ? 0 : (0.5m * (1 - s1) * (currentValue - prevValue)) + (l1 * (1 + s1) * prevBp1) - (s1 * prevBp2);
            bpList.Add(bp);

            var signal = GetCompareSignal(bp - prevBp1, prevBp1 - prevBp2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
    public static StockData CalculateEhlersCycleBandPassFilter(this StockData stockData, int length = 20, decimal delta = 0.1m)
    {
        List<decimal> bpList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        decimal beta = Cos(MinOrMax(2 * Pi / length, 0.99m, 0.01m));
        decimal gamma = 1 / Cos(MinOrMax(4 * Pi * delta / length, 0.99m, 0.01m));
        decimal alpha = gamma - Sqrt(Pow(gamma, 2) - 1);

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal currentValue = inputList.ElementAtOrDefault(i);
            decimal prevValue = i >= 2 ? inputList.ElementAtOrDefault(i - 2) : 0;
            decimal prevBp1 = i >= 1 ? bpList.ElementAtOrDefault(i - 1) : 0;
            decimal prevBp2 = i >= 2 ? bpList.ElementAtOrDefault(i - 2) : 0;

            decimal bp = (0.5m * (1 - alpha) * (currentValue - prevValue)) + (beta * (1 + alpha) * prevBp1) - (alpha * prevBp2);
            bpList.Add(bp);

            var signal = GetCompareSignal(bp - prevBp1, prevBp1 - prevBp2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
    public static StockData CalculateEhlersCycleAmplitude(this StockData stockData, int length = 20, decimal delta = 0.1m)
    {
        List<decimal> ptopList = new();
        List<Signal> signalsList = new();

        int lbLength = (int)Math.Ceiling((decimal)length / 4);

        var bpList = CalculateEhlersCycleBandPassFilter(stockData, length, delta).CustomValuesList;

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal prevPtop1 = i >= 1 ? ptopList.ElementAtOrDefault(i - 1) : 0;
            decimal prevPtop2 = i >= 2 ? ptopList.ElementAtOrDefault(i - 2) : 0;

            decimal power = 0;
            for (int j = length; j < length; j++)
            {
                decimal prevBp1 = i >= j ? bpList.ElementAtOrDefault(i - j) : 0;
                decimal prevBp2 = i >= j + lbLength ? bpList.ElementAtOrDefault(i - (j + lbLength)) : 0;
                power += Pow(prevBp1, 2) + Pow(prevBp2, 2);
            }

            decimal ptop = 2 * 1.414m * Sqrt(power / length);
            ptopList.Add(ptop);

            var signal = GetCompareSignal(ptop - prevPtop1, prevPtop1 - prevPtop2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
    public static StockData CalculateEhlersZeroCrossingsDominantCycle(this StockData stockData, int length = 20, decimal bw = 0.7m)
    {
        List<decimal> dcList = new();
        List<Signal> signalsList = new();

        int counter = 0;

        var ebpfList = CalculateEhlersBandPassFilterV1(stockData, length, bw);
        var realList = ebpfList.OutputValues["Ebpf"];
        var triggerList = ebpfList.OutputValues["Signal"];

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal real = realList.ElementAtOrDefault(i);
            decimal trigger = triggerList.ElementAtOrDefault(i);
            decimal prevReal = i >= 1 ? realList.ElementAtOrDefault(i - 1) : 0;
            decimal prevTrigger = i >= 1 ? triggerList.ElementAtOrDefault(i - 1) : 0;

            decimal prevDc = dcList.LastOrDefault();
            decimal dc = Math.Max(prevDc, 6);
            counter += 1;
            if ((real > 0 && prevReal <= 0) || (real < 0 && prevReal >= 0))
            {
                dc = MinOrMax(2 * counter, 1.25m * prevDc, 0.8m * prevDc);
                counter = 0;
            }
            dcList.Add(dc);

            var signal = GetCompareSignal(real - trigger, prevReal - prevTrigger);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
    public static StockData CalculateEhlersAdaptiveBandPassFilter(this StockData stockData, int length1 = 48, int length2 = 10, int length3 = 3, decimal bw = 0.3m)
    {
        List<decimal> bpList = new();
        List<decimal> peakList = new();
        List<decimal> signalList = new();
        List<decimal> triggerList = new();
        List<decimal> leadPeakList = new();
        List<Signal> signalsList = new();

        var domCycList = CalculateEhlersAutoCorrelationPeriodogram(stockData, length1, length2, length3).CustomValuesList;
        var roofingFilterList = CalculateEhlersRoofingFilterV2(stockData, length1, length2).CustomValuesList;

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal roofingFilter = roofingFilterList.ElementAtOrDefault(i);
            decimal domCyc = MinOrMax(domCycList.ElementAtOrDefault(i), length1, length3);
            decimal beta = Cos(2 * Pi / 0.9m * domCyc);
            decimal gamma = 1 / Cos(2 * Pi * bw / 0.9m * domCyc);
            decimal alpha = MinOrMax(gamma - Sqrt((gamma * gamma) - 1), 0.99m, 0.01m);
            decimal prevRoofingFilter2 = i >= 2 ? roofingFilterList.ElementAtOrDefault(i - 2) : 0;
            decimal prevBp1 = i >= 1 ? bpList.ElementAtOrDefault(i - 1) : 0;
            decimal prevBp2 = i >= 2 ? bpList.ElementAtOrDefault(i - 2) : 0;
            decimal prevSignal1 = i >= 1 ? signalList.ElementAtOrDefault(i - 1) : 0;
            decimal prevSignal2 = i >= 2 ? signalList.ElementAtOrDefault(i - 2) : 0;
            decimal prevSignal3 = i >= 3 ? signalList.ElementAtOrDefault(i - 3) : 0;

            decimal bp = i > 2 ? (0.5m * (1 - alpha) * (roofingFilter - prevRoofingFilter2)) + (beta * (1 + alpha) * prevBp1) - (alpha * prevBp2) : 0;
            bpList.Add(bp);

            decimal prevPeak = peakList.LastOrDefault();
            decimal peak = Math.Max(0.991m * prevPeak, Math.Abs(bp));
            peakList.Add(peak);

            decimal sig = peak != 0 ? bp / peak : 0;
            signalList.Add(sig);

            decimal lead = 1.3m * (sig + prevSignal1 - prevSignal2 - prevSignal3) / 4;
            decimal prevLeadPeak = leadPeakList.LastOrDefault();
            decimal leadPeak = Math.Max(0.93m * prevLeadPeak, Math.Abs(lead));
            leadPeakList.Add(leadPeak);

            decimal prevTrigger = triggerList.LastOrDefault();
            decimal trigger = 0.9m * prevSignal1;
            triggerList.Add(trigger);

            var signal = GetRsiSignal(sig - trigger, prevSignal1 - prevTrigger, sig, prevSignal1, 0.707m, -0.707m);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
    public static StockData CalculateEhlersCyberCycle(this StockData stockData, decimal alpha = 0.07m)
    {
        List<decimal> smoothList = new();
        List<decimal> cycleList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal currentValue = inputList.ElementAtOrDefault(i);
            decimal prevValue1 = i >= 1 ? inputList.ElementAtOrDefault(i - 1) : 0;
            decimal prevValue2 = i >= 2 ? inputList.ElementAtOrDefault(i - 2) : 0;
            decimal prevValue3 = i >= 3 ? inputList.ElementAtOrDefault(i - 3) : 0;
            decimal prevSmooth1 = i >= 1 ? smoothList.ElementAtOrDefault(i - 1) : 0;
            decimal prevSmooth2 = i >= 2 ? smoothList.ElementAtOrDefault(i - 2) : 0;
            decimal prevCycle1 = i >= 1 ? cycleList.ElementAtOrDefault(i - 1) : 0;
            decimal prevCycle2 = i >= 2 ? cycleList.ElementAtOrDefault(i - 2) : 0;

            decimal smooth = (currentValue + (2 * prevValue1) + (2 * prevValue2) + prevValue3) / 6;
            smoothList.Add(smooth);

            decimal cycle = i < 7 ? (currentValue - (2 * prevValue1) + prevValue2) / 4 : (Pow(1 - (0.5m * alpha), 2) * (smooth - (2 * prevSmooth1) + prevSmooth2)) +
                (2 * (1 - alpha) * prevCycle1) - (Pow(1 - alpha, 2) * prevCycle2);
            cycleList.Add(cycle);

            var signal = GetCompareSignal(cycle - prevCycle1, prevCycle1 - prevCycle2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
    public static StockData CalculateEhlersStochasticCyberCycle(this StockData stockData, int length = 14, decimal alpha = 0.7m)
    {
        List<decimal> stochList = new();
        List<decimal> stochCCList = new();
        List<decimal> triggerList = new();
        List<Signal> signalsList = new();

        var cyberCycleList = CalculateEhlersCyberCycle(stockData, alpha).CustomValuesList;
        var (maxCycleList, minCycleList) = GetMaxAndMinValuesList(cyberCycleList, length);

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal prevStoch1 = i >= 1 ? stochList.ElementAtOrDefault(i - 1) : 0;
            decimal prevStoch2 = i >= 2 ? stochList.ElementAtOrDefault(i - 2) : 0;
            decimal prevStoch3 = i >= 3 ? stochList.ElementAtOrDefault(i - 3) : 0;
            decimal cycle = cyberCycleList.ElementAtOrDefault(i);
            decimal maxCycle = maxCycleList.ElementAtOrDefault(i);
            decimal minCycle = minCycleList.ElementAtOrDefault(i);

            decimal stoch = maxCycle - minCycle != 0 ? MinOrMax((cycle - minCycle) / (maxCycle - minCycle), 1, 0) : 0;
            stochList.Add(stoch);

            decimal prevStochCC = stochCCList.LastOrDefault();
            decimal stochCC = MinOrMax(2 * ((((4 * stoch) + (3 * prevStoch1) + (2 * prevStoch2) + prevStoch3) / 10) - 0.5m), 1, -1);
            stochCCList.Add(stochCC);

            decimal prevTrigger = triggerList.LastOrDefault();
            decimal trigger = MinOrMax(0.96m * (prevStochCC + 0.02m), 1, -1);
            triggerList.Add(trigger);

            var signal = GetRsiSignal(stochCC - trigger, prevStochCC - prevTrigger, stochCC, prevStochCC, 0.5m, -0.5m);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> hlList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, openList, _) = GetInputValuesList(stockData);

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal currentClose = inputList.ElementAtOrDefault(i);
            decimal currentOpen = openList.ElementAtOrDefault(i);
            decimal der = currentClose - currentOpen;
            decimal hlRaw = fastLength * der;

            decimal hl = MinOrMax(hlRaw, 1, -1);
            hlList.Add(hl);
        }

        var ssList = GetMovingAverageList(stockData, maType, slowLength, hlList);
        for (int i = 0; i < stockData.Count; i++)
        {
            decimal ss = ssList.ElementAtOrDefault(i);
            decimal prevSs = i >= 1 ? ssList.ElementAtOrDefault(i - 1) : 0;

            var signal = GetCompareSignal(ss, prevSs);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> stoch2PoleList = new();
        List<decimal> arg2PoleList = new();
        List<Signal> signalsList = new();

        var roofingFilter2PoleList = CalculateEhlersRoofingFilterV1(stockData, maType, length1, length3).CustomValuesList;
        var (max2PoleList, min2PoleList) = GetMaxAndMinValuesList(roofingFilter2PoleList, length2);

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal rf2Pole = roofingFilter2PoleList.ElementAtOrDefault(i);
            decimal min2Pole = min2PoleList.ElementAtOrDefault(i);
            decimal max2Pole = max2PoleList.ElementAtOrDefault(i);

            decimal prevStoch2Pole = stoch2PoleList.LastOrDefault();
            decimal stoch2Pole = max2Pole - min2Pole != 0 ? MinOrMax((rf2Pole - min2Pole) / (max2Pole - min2Pole), 1, 0) : 0;
            stoch2PoleList.Add(stoch2Pole);

            decimal arg2Pole = (stoch2Pole + prevStoch2Pole) / 2;
            arg2PoleList.Add(arg2Pole);
        }

        var estoch2PoleList = GetMovingAverageList(stockData, maType, length2, arg2PoleList);
        for (int j = 0; j < stockData.Count; j++)
        {
            decimal estoch2Pole = estoch2PoleList.ElementAtOrDefault(j);
            decimal prevEstoch2Pole1 = j >= 1 ? estoch2PoleList.ElementAtOrDefault(j - 1) : 0;
            decimal prevEstoch2Pole2 = j >= 2 ? estoch2PoleList.ElementAtOrDefault(j - 2) : 0;

            var signal = GetRsiSignal(estoch2Pole - prevEstoch2Pole1, prevEstoch2Pole1 - prevEstoch2Pole2, estoch2Pole, prevEstoch2Pole1, 0.8m, 0.2m);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> tmp1List = new();
        List<decimal> tmp2List = new();
        List<decimal> detrenderList = new();
        List<decimal> histList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal currentValue = inputList.ElementAtOrDefault(i);
            decimal prevTmp1_6 = i >= 6 ? tmp1List.ElementAtOrDefault(i - 6) : 0;
            decimal prevTmp2_6 = i >= 6 ? tmp2List.ElementAtOrDefault(i - 6) : 0;
            decimal prevTmp2_12 = i >= 12 ? tmp2List.ElementAtOrDefault(i - 12) : 0;

            decimal tmp1 = currentValue + (0.088m * prevTmp1_6);
            tmp1List.Add(tmp1);

            decimal tmp2 = tmp1 - prevTmp1_6 + (1.2m * prevTmp2_6) - (0.7m * prevTmp2_12);
            tmp2List.Add(tmp2);

            decimal detrender = prevTmp2_12 - (2 * prevTmp2_6) + tmp2;
            detrenderList.Add(detrender);
        }

        var tdldList = GetMovingAverageList(stockData, maType, length, detrenderList);
        var tdldSignalList = GetMovingAverageList(stockData, maType, length, tdldList);
        for (int i = 0; i < stockData.Count; i++)
        {
            decimal tdld = tdldList.ElementAtOrDefault(i);
            decimal tdldSignal = tdldSignalList.ElementAtOrDefault(i);

            decimal prevHist = histList.LastOrDefault();
            decimal hist = tdld - tdldSignal;
            histList.Add(hist);

            var signal = GetCompareSignal(hist, prevHist);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> absDerList = new();
        List<decimal> envList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, openList, _) = GetInputValuesList(stockData);

        var emaList = GetMovingAverageList(stockData, maType, length1, inputList);

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal currentClose = inputList.ElementAtOrDefault(i);
            decimal currentOpen = openList.ElementAtOrDefault(i);
            decimal der = currentClose - currentOpen;

            decimal absDer = Math.Abs(der);
            absDerList.Add(absDer);

            decimal env = absDerList.TakeLastExt(length1).Max();
            envList.Add(env);
        }

        var volList = GetMovingAverageList(stockData, maType, length2, envList);
        var volEmaList = GetMovingAverageList(stockData, maType, length2, volList);
        for (int i = 0; i < stockData.Count; i++)
        {
            decimal vol = volList.ElementAtOrDefault(i);
            decimal volEma = volEmaList.ElementAtOrDefault(i);
            decimal ema = emaList.ElementAtOrDefault(i);
            decimal currentValue = inputList.ElementAtOrDefault(i);
            decimal prevValue = i >= 1 ? inputList.ElementAtOrDefault(i - 1) : 0;
            decimal prevEma = i >= 1 ? emaList.ElementAtOrDefault(i - 1) : 0;

            var signal = GetVolatilitySignal(currentValue - ema, prevValue - prevEma, vol, volEma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> sineList = new();
        List<decimal> leadSineList = new();
        List<decimal> dcPhaseList = new();
        List<Signal> signalsList = new();

        var ehlersMamaList = CalculateEhlersMotherOfAdaptiveMovingAverages(stockData);
        var spList = ehlersMamaList.OutputValues["SmoothPeriod"];
        var smoothList = ehlersMamaList.OutputValues["Smooth"];

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal sp = spList.ElementAtOrDefault(i);
            int dcPeriod = (int)Math.Ceiling(sp + 0.5m);

            decimal realPart = 0, imagPart = 0;
            for (int j = 0; j <= dcPeriod - 1; j++)
            {
                decimal prevSmooth = i >= j ? smoothList.ElementAtOrDefault(i - j) : 0;
                realPart += Sin(MinOrMax(2 * Pi * ((decimal)j / dcPeriod), 0.99m, 0.01m)) * prevSmooth;
                imagPart += Cos(MinOrMax(2 * Pi * ((decimal)j / dcPeriod), 0.99m, 0.01m)) * prevSmooth;
            }

            decimal dcPhase = Math.Abs(imagPart) > 0.001m ? Atan(realPart / imagPart).ToDegrees() : 90 * Math.Sign(realPart);
            dcPhase += 90;
            dcPhase += sp != 0 ? 360 / sp : 0;
            dcPhase += imagPart < 0 ? 180 : 0;
            dcPhase -= dcPhase > 315 ? 360 : 0;
            dcPhaseList.Add(dcPhase);

            decimal prevSine = sineList.LastOrDefault();
            decimal sine = Sin(dcPhase.ToRadians());
            sineList.Add(sine);

            decimal prevLeadSine = leadSineList.LastOrDefault();
            decimal leadSine = Sin((dcPhase + 45).ToRadians());
            leadSineList.Add(leadSine);

            var signal = GetCompareSignal(sine - leadSine, prevSine - prevLeadSine);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
    public static StockData CalculateEhlersSineWaveIndicatorV2(this StockData stockData, int length = 5, decimal alpha = 0.07m)
    {
        List<decimal> sineList = new();
        List<decimal> leadSineList = new();
        List<decimal> dcPhaseList = new();
        List<Signal> signalsList = new();

        var periodList = CalculateEhlersAdaptiveCyberCycle(stockData, length, alpha).OutputValues["Period"];
        var cycleList = CalculateEhlersCyberCycle(stockData).CustomValuesList;

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal period = periodList.ElementAtOrDefault(i);
            int dcPeriod = (int)Math.Ceiling(period);

            decimal realPart = 0, imagPart = 0;
            for (int j = 0; j <= dcPeriod - 1; j++)
            {
                decimal prevCycle = i >= j ? cycleList.ElementAtOrDefault(i - j) : 0;
                realPart += Sin(MinOrMax(2 * Pi * ((decimal)j / dcPeriod), 0.99m, 0.01m)) * prevCycle;
                imagPart += Cos(MinOrMax(2 * Pi * ((decimal)j / dcPeriod), 0.99m, 0.01m)) * prevCycle;
            }

            decimal dcPhase = Math.Abs(imagPart) > 0.001m ? Atan(realPart / imagPart).ToDegrees() : 90 * Math.Sign(realPart);
            dcPhase += 90;
            dcPhase += imagPart < 0 ? 180 : 0;
            dcPhase -= dcPhase > 315 ? 360 : 0;
            dcPhaseList.Add(dcPhase);

            decimal prevSine = sineList.LastOrDefault();
            decimal sine = Sin(dcPhase.ToRadians());
            sineList.Add(sine);

            decimal prevLeadSine = leadSineList.LastOrDefault();
            decimal leadSine = Sin((dcPhase + 45).ToRadians());
            leadSineList.Add(leadSine);

            var signal = GetCompareSignal(sine - leadSine, prevSine - prevLeadSine);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
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
        List<decimal> hpList = new();
        List<decimal> filtList = new();
        List<decimal> ebsiList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        decimal piHp = MinOrMax(2 * Pi / length1, 0.99m, 0.01m);
        decimal a1 = (1 - Sin(piHp)) / Cos(piHp);
        decimal a2 = Exp(MinOrMax(-1.414m * Pi / length2, -0.01m, -0.99m));
        decimal b = 2 * a2 * Cos(MinOrMax(1.414m * Pi / length2, 0.99m, 0.01m));
        decimal c2 = b;
        decimal c3 = -a2 * a2;
        decimal c1 = 1 - c2 - c3;

        for (int i = 0; i < stockData.Count; i++)
        {
            decimal currentValue = inputList.ElementAtOrDefault(i);
            decimal prevValue = i >= 1 ? inputList.ElementAtOrDefault(i - 1) : 0;
            decimal prevFilt1 = i >= 1 ? filtList.ElementAtOrDefault(i - 1) : 0;
            decimal prevFilt2 = i >= 2 ? filtList.ElementAtOrDefault(i - 2) : 0;

            decimal prevHp = hpList.LastOrDefault();
            decimal hp = ((0.5m * (1 + a1)) * (currentValue - prevValue)) + (a1 * prevHp);
            hpList.Add(hp);

            decimal filt = (c1 * ((hp + prevHp) / 2)) + (c2 * prevFilt1) + (c3 * prevFilt2);
            filtList.Add(filt);

            decimal wave = (filt + prevFilt1 + prevFilt2) / 3;
            decimal pwr = (Pow(filt, 2) + Pow(prevFilt1, 2) + Pow(prevFilt2, 2)) / 3;
            decimal prevEbsi = ebsiList.LastOrDefault();
            decimal ebsi = pwr > 0 ? wave / Sqrt(pwr) : 0;
            ebsiList.Add(ebsi);

            var signal = GetCompareSignal(ebsi, prevEbsi);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new()
        {
            { "Ebsi", ebsiList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = ebsiList;
        stockData.IndicatorName = IndicatorName.EhlersEvenBetterSineWaveIndicator;

        return stockData;
    }
}