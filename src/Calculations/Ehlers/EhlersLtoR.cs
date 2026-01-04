
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
        upperLength = Math.Max(upperLength, 1);
        lowerLength = Math.Max(lowerLength, 1);
        List<double> highPassList = new(stockData.Count);
        List<double> roofingFilterList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var alphaArg = Math.Min(MathHelper.Sqrt2 * Math.PI / upperLength, 0.99);
        var alphaCos = Math.Cos(alphaArg);
        var alpha1 = alphaCos != 0 ? (alphaCos + Math.Sin(alphaArg) - 1) / alphaCos : 0;
        var a1 = Exp(-MathHelper.Sqrt2 * Math.PI / lowerLength);
        var b1 = 2 * a1 * Math.Cos(Math.Min(MathHelper.Sqrt2 * Math.PI / lowerLength, 0.99));
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
            highPassList.Add(highPass);

            var prevRoofingFilter1 = i >= 1 ? roofingFilterList[i - 1] : 0;
            var prevRoofingFilter2 = i >= 2 ? roofingFilterList[i - 2] : 0;
            var roofingFilter = (c1 * ((highPass + prevHp1) / 2)) + (c2 * prevFilter1) + (c3 * prevFilter2);
            roofingFilterList.Add(roofingFilter);

            var signal = GetCompareSignal(roofingFilter - prevRoofingFilter1, prevRoofingFilter1 - prevRoofingFilter2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Erf", roofingFilterList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(roofingFilterList);
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
        length = Math.Max(length, 1);
        List<double> phaseList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
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
            phaseList.Add(phase);
        }

        var phaseEmaList = GetMovingAverageList(stockData, maType, length, phaseList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var phase = phaseList[i];
            var phaseEma = phaseEmaList[i];
            var prevPhase = i >= 1 ? phaseList[i - 1] : 0;
            var prevPhaseEma = i >= 1 ? phaseEmaList[i - 1] : 0;

            var signal = GetCompareSignal(phase - phaseEma, prevPhase - prevPhaseEma, true);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Phase", phaseList },
            { "Signal", phaseEmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(phaseList);
        stockData.IndicatorName = IndicatorName.EhlersPhaseCalculation;

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
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        List<double> momList = new(stockData.Count);
        List<double> argList = new(stockData.Count);
        List<double> ssf2PoleRocketRsiList = new(stockData.Count);
        List<double> ssf2PoleUpChgList = new(stockData.Count);
        List<double> ssf2PoleDownChgList = new(stockData.Count);
        List<double> ssf2PoleTmpList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum ssf2PoleUpChgSum = new();
        RollingSum ssf2PoleDownChgSum = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var obLevel = obosLevel * mult;
        var osLevel = -obosLevel * mult;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= length1 - 1 ? inputList[i - (length1 - 1)] : 0;

            var prevMom = GetLastOrDefault(momList);
            var mom = MinPastValues(i, length1 - 1, currentValue - prevValue);
            momList.Add(mom);

            var arg = (mom + prevMom) / 2;
            argList.Add(arg);
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
            ssf2PoleUpChgList.Add(up2PoleChg);
            ssf2PoleUpChgSum.Add(up2PoleChg);

            var down2PoleChg = ssf2PoleMom < 0 ? Math.Abs(ssf2PoleMom) : 0;
            ssf2PoleDownChgList.Add(down2PoleChg);
            ssf2PoleDownChgSum.Add(down2PoleChg);

            var up2PoleChgSum = ssf2PoleUpChgSum.Sum(length1);
            var down2PoleChgSum = ssf2PoleDownChgSum.Sum(length1);

            var prevTmp2Pole = GetLastOrDefault(ssf2PoleTmpList);
            var tmp2Pole = up2PoleChgSum + down2PoleChgSum != 0 ?
                MinOrMax((up2PoleChgSum - down2PoleChgSum) / (up2PoleChgSum + down2PoleChgSum), 0.999, -0.999) : prevTmp2Pole;
            ssf2PoleTmpList.Add(tmp2Pole);

            var ssf2PoleTempLog = 1 - tmp2Pole != 0 ? (1 + tmp2Pole) / (1 - tmp2Pole) : 0;
            var ssf2PoleLog = Math.Log(ssf2PoleTempLog);
            var ssf2PoleRocketRsi = 0.5 * ssf2PoleLog * mult;
            ssf2PoleRocketRsiList.Add(ssf2PoleRocketRsi);

            var signal = GetRsiSignal(ssf2PoleRocketRsi - prevRocketRsi1, prevRocketRsi1 - prevRocketRsi2, ssf2PoleRocketRsi, prevRocketRsi1, obLevel, osLevel);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Errsi", ssf2PoleRocketRsiList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(ssf2PoleRocketRsiList);
        stockData.IndicatorName = IndicatorName.EhlersRocketRelativeStrengthIndex;

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
        length = Math.Max(length, 1);
        signalLength = Math.Max(signalLength, 1);
        List<double> rviList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, openList, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentClose = inputList[i];
            var currentOpen = openList[i];
            var currentHigh = highList[i];
            var currentLow = lowList[i];

            var rvi = currentHigh - currentLow != 0 ? (currentClose - currentOpen) / (currentHigh - currentLow) : 0;
            rviList.Add(rvi);
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
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ervi", rviSmaList },
            { "Signal", rviSignalList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(rviSmaList);
        stockData.IndicatorName = IndicatorName.EhlersRelativeVigorIndex;

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
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        length3 = Math.Max(length3, 1);
        List<double> stocList = new(stockData.Count);
        List<double> modStocList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var a1 = Exp(-MathHelper.Sqrt2 * Math.PI / length1);
        var b1 = 2 * a1 * Math.Cos(Math.Min(MathHelper.Sqrt2 * Math.PI / length1, 0.99));
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

            var prevStoc = GetLastOrDefault(stocList);
            var stoc = highest - lowest != 0 ? (roofingFilter - lowest) / (highest - lowest) * 100 : 0;
            stocList.Add(stoc);

            var modStoc = (c1 * ((stoc + prevStoc) / 2)) + (c2 * prevModStoc1) + (c3 * prevModStoc2);
            modStocList.Add(modStoc);

            var signal = GetRsiSignal(modStoc - prevModStoc1, prevModStoc1 - prevModStoc2, modStoc, prevModStoc1, 70, 30);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Emsi", modStocList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(modStocList);
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
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        length3 = Math.Max(length3, 1);
        List<double> upChgList = new(stockData.Count);
        List<double> upChgSumList = new(stockData.Count);
        List<double> denomList = new(stockData.Count);
        List<double> mrsiList = new(stockData.Count);
        List<double> mrsiSigList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum upChgSumWindow = new();

        var a1 = Exp(-MathHelper.Sqrt2 * Math.PI / length2);
        var b1 = 2 * a1 * Math.Cos(Math.Min(MathHelper.Sqrt2 * Math.PI / length2, 0.99));
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
            upChgList.Add(upChg);
            upChgSumWindow.Add(upChg);

            var dnChg = roofingFilter < prevRoofingFilter ? prevRoofingFilter - roofingFilter : 0;
            var prevUpChgSum = GetLastOrDefault(upChgSumList);
            var upChgSum = upChgSumWindow.Sum(length3);
            upChgSumList.Add(upChgSum);

            var prevDenom = GetLastOrDefault(denomList);
            var denom = upChg + dnChg;
            denomList.Add(denom);

            var mrsi = denom != 0 && prevDenom != 0 ? (c1 * (((upChgSum / denom) + (prevUpChgSum / prevDenom)) / 2)) + (c2 * prevMrsi1) + (c3 * prevMrsi2) : 0;
            mrsiList.Add(mrsi);

            var mrsiSig = (c1 * ((mrsi + prevMrsi1) / 2)) + (c2 * prevMrsiSig1) + (c3 * prevMrsiSig2);
            mrsiSigList.Add(mrsiSig);

            var signal = GetRsiSignal(mrsi - mrsiSig, prevMrsi1 - prevMrsiSig1, mrsi, prevMrsi1, 0.7, 0.3);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Emrsi", mrsiList },
            { "Signal", mrsiSigList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(mrsiList);
        stockData.IndicatorName = IndicatorName.EhlersModifiedRelativeStrengthIndex;

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
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        List<double> highPassList = new(stockData.Count);
        List<double> roofingFilterList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var alphaArg = Math.Min(MathHelper.Sqrt2 * Math.PI / length1, 0.99);
        var alphaCos = Math.Cos(alphaArg);
        var a1 = alphaCos != 0 ? (alphaCos + Math.Sin(alphaArg) - 1) / alphaCos : 0;
        var a2 = Exp(-MathHelper.Sqrt2 * Math.PI / length2);
        var b1 = 2 * a2 * Math.Cos(Math.Min(MathHelper.Sqrt2 * Math.PI / length2, 0.99));
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
            highPassList.Add(hp);

            var filter = (c1 * ((hp + prevHp1) / 2)) + (c2 * prevFilter1) + (c3 * prevFilter2);
            roofingFilterList.Add(filter);

            var signal = GetCompareSignal(filter - prevFilter1, prevFilter1 - prevFilter2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Erfi", roofingFilterList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(roofingFilterList);
        stockData.IndicatorName = IndicatorName.EhlersRoofingFilterIndicator;

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
        length = Math.Max(length, 1);
        List<double> filterList = new(stockData.Count);
        List<double> msList = new(stockData.Count);
        List<double> reflexList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var a1 = Exp(-MathHelper.Sqrt2 * Math.PI / 0.5 * length);
        var b1 = 2 * a1 * Math.Cos(MathHelper.Sqrt2 * Math.PI / 0.5 * length);
        var c2 = b1;
        var c3 = -a1 * a1;
        var c1 = 1 - c2 - c3;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevFilter1 = GetLastOrDefault(filterList);
            var prevFilter2 = i >= 2 ? filterList[i - 2] : 0;
            var priorFilter = i >= length ? filterList[i - length] : 0;
            var prevReflex1 = i >= 1 ? reflexList[i - 1] : 0;
            var prevReflex2 = i >= 2 ? reflexList[i - 2] : 0;

            var filter = (c1 * ((currentValue + prevValue) / 2)) + (c2 * prevFilter1) + (c3 * prevFilter2);
            filterList.Add(filter);

            var slope = length != 0 ? (priorFilter - filter) / length : 0;
            double sum = 0;
            for (var j = 1; j <= length; j++)
            {
                var prevFilterCount = i >= j ? filterList[i - j] : 0;
                sum += filter + (j * slope) - prevFilterCount;
            }
            sum /= length;

            var prevMs = GetLastOrDefault(msList);
            var ms = (0.04 * sum * sum) + (0.96 * prevMs);
            msList.Add(ms);

            var reflex = ms > 0 ? sum / Sqrt(ms) : 0;
            reflexList.Add(reflex);

            var signal = GetCompareSignal(reflex - prevReflex1, prevReflex1 - prevReflex2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Eri", reflexList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(reflexList);
        stockData.IndicatorName = IndicatorName.EhlersReflexIndicator;

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
        minLength = Math.Max(minLength, 1);
        maxLength = Math.Max(maxLength, minLength);
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        List<double> rpiList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (_, _, _, _, volumeList) = GetInputValuesList(stockData);

        var domCycList = CalculateEhlersSpectrumDerivedFilterBank(stockData, minLength, maxLength, length1, length2).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var domCyc = domCycList[i];
            var volume = volumeList[i];

            var rpi = volume * Pow(MinOrMax(2 * Math.PI / domCyc, 0.99, 0.01), 2);
            rpiList.Add(rpi);
        }

        var rpiEmaList = GetMovingAverageList(stockData, maType, minLength, rpiList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var rpi = rpiList[i];
            var rpiEma = rpiEmaList[i];
            var prevRpi = i >= 1 ? rpiList[i - 1] : 0;
            var prevRpiEma = i >= 1 ? rpiEmaList[i - 1] : 0;

            var signal = GetCompareSignal(rpi - rpiEma, prevRpi - prevRpiEma, true);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Rpi", rpiList },
            { "Signal", rpiEmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(rpiList);
        stockData.IndicatorName = IndicatorName.EhlersRestoringPullIndicator;

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
        length = Math.Max(length, 1);
        List<double> stateList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var angleList = CalculateEhlersCorrelationAngleIndicator(stockData, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var angle = angleList[i];
            var prevAngle = i >= 1 ? angleList[i - 1] : 0;

            var prevState = GetLastOrDefault(stateList);
            double state = Math.Abs(angle - prevAngle) < 9 && angle < 0 ? -1 : Math.Abs(angle - prevAngle) < 9 && angle >= 0 ? 1 : 0;
            stateList.Add(state);

            var signal = GetCompareSignal(state, prevState);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Emsi", stateList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(stateList);
        stockData.IndicatorName = IndicatorName.EhlersMarketStateIndicator;

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
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        List<double> argList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var hpFilterList = CalculateEhlersHighPassFilterV1(stockData, length1, 1).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var highPass = hpFilterList[i];
            var prevHp1 = i >= 1 ? hpFilterList[i - 1] : 0;

            var arg = (highPass + prevHp1) / 2;
            argList.Add(arg);
        }

        var roofingFilter2PoleList = GetMovingAverageList(stockData, maType, length2, argList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var roofingFilter = roofingFilter2PoleList[i];
            var prevRoofingFilter = i >= 1 ? roofingFilter2PoleList[i - 1] : 0;

            var signal = GetCompareSignal(roofingFilter, prevRoofingFilter);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Erf", roofingFilter2PoleList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(roofingFilter2PoleList);
        stockData.IndicatorName = IndicatorName.EhlersRoofingFilterV1;

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
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        length3 = Math.Max(length3, 1);
        lowerLength = Math.Max(lowerLength, 1);
        upperLength = Math.Max(upperLength, 1);
        List<double> ssfList = new(stockData.Count);
        List<double> hpList = new(stockData.Count);
        List<double> prePredictList = new(stockData.Count);
        List<double> predictList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var a1 = Exp(MinOrMax(-MathHelper.Sqrt2 * Math.PI / upperLength, -0.01, -0.99));
        var b1 = 2 * a1 * Math.Cos(MinOrMax(MathHelper.Sqrt2 * Math.PI / upperLength, 0.99, 0.01));
        var c2 = b1;
        var c3 = -a1 * a1;
        var c1 = (1 + c2 - c3) / 4;
        var a = Exp(MinOrMax(-MathHelper.Sqrt2 * Math.PI / lowerLength, -0.01, -0.99));
        var b = 2 * a * Math.Cos(MinOrMax(MathHelper.Sqrt2 * Math.PI / lowerLength, 0.99, 0.01));
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
            hpList.Add(hp);

            var ssf = i < 3 ? hp : (coef1 * ((hp + prevHp1) / 2)) + (coef2 * prevSsf1) + (coef3 * prevSsf2);
            ssfList.Add(ssf);

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

            var prevPrePredict = GetLastOrDefault(prePredictList);
            var prePredict = xxArray[upperLength + length1];
            prePredictList.Add(prePredict);

            var predict = (prePredict + prevPrePredict) / 2;
            predictList.Add(predict);

            var signal = GetCompareSignal(ssf - predict, prevSsf1 - prevPredict1);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ssf", ssfList },
            { "Predict", predictList },
            { "PrePredict", prePredictList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(predictList);
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
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        length3 = Math.Max(length3, 1);
        length4 = Math.Max(length4, 1);
        List<double> ssfList = new(stockData.Count);
        List<double> hpList = new(stockData.Count);
        List<double> predictList = new(stockData.Count);
        List<double> extrapList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
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
            hpList.Add(hp);

            var ssf = i < 3 ? hp : (coef1 * ((hp + prevHp1) / 2)) + (coef2 * prevSsf1) + (coef3 * prevSsf2);
            ssfList.Add(ssf);
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
            predictList.Add(predict);

            var extrap = yyArray[length1 + length4];
            extrapList.Add(extrap);

            var signal = GetCompareSignal(predict - prevPredict1, prevPredict1 - prevPredict2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ssf", filtList },
            { "Predict", predictList },
            { "Extrap", extrapList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(predictList);
        stockData.IndicatorName = IndicatorName.EhlersMesaPredictIndicatorV2;

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
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        length3 = Math.Max(length3, 1);
        List<double> rmList = new(stockData.Count);
        List<double> tempList = new(stockData.Count);
        List<double> rmoList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        using var tempMedian = new RollingMedian(length1);
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
            tempList.Add(currentValue);
            tempMedian.Add(currentValue);

            var median = tempMedian.Median;
            var prevRm1 = i >= 1 ? rmList[i - 1] : 0;
            var prevRm2 = i >= 2 ? rmList[i - 2] : 0;
            var prevRmo1 = i >= 1 ? rmoList[i - 1] : 0;
            var prevRmo2 = i >= 2 ? rmoList[i - 2] : 0;

            var rm = (alpha1 * median) + ((1 - alpha1) * prevRm1);
            rmList.Add(rm);

            var rmo = (Pow(1 - (alpha2 / 2), 2) * (rm - (2 * prevRm1) + prevRm2)) + (2 * (1 - alpha2) * prevRmo1) - (Pow(1 - alpha2, 2) * prevRmo2);
            rmoList.Add(rmo);

            var signal = GetCompareSignal(rmo, prevRmo1);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ermo", rmoList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(rmoList);
        stockData.IndicatorName = IndicatorName.EhlersRecursiveMedianOscillator;

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
        length = Math.Max(length, 1);
        List<double> netList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
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

            var prevNet = GetLastOrDefault(netList);
            var net = denom != 0 ? num / denom : 0;
            netList.Add(net);

            var signal = GetCompareSignal(net, prevNet);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Enet", netList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(netList);
        stockData.IndicatorName = IndicatorName.EhlersNoiseEliminationTechnology;

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
        List<double> emaList = new(stockData.Count);
        List<double> re1List = new(stockData.Count);
        List<double> re2List = new(stockData.Count);
        List<double> re3List = new(stockData.Count);
        List<double> re4List = new(stockData.Count);
        List<double> re5List = new(stockData.Count);
        List<double> re6List = new(stockData.Count);
        List<double> re7List = new(stockData.Count);
        List<double> waveList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var cc = 1 - alpha;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];

            var prevEma = GetLastOrDefault(emaList);
            var ema = (alpha * currentValue) + (cc * prevEma);
            emaList.Add(ema);

            var prevRe1 = GetLastOrDefault(re1List);
            var re1 = (cc * ema) + prevEma;
            re1List.Add(re1);

            var prevRe2 = GetLastOrDefault(re2List);
            var re2 = (Pow(cc, 2) * re1) + prevRe1;
            re2List.Add(re2);

            var prevRe3 = GetLastOrDefault(re3List);
            var re3 = (Pow(cc, 4) * re2) + prevRe2;
            re3List.Add(re3);

            var prevRe4 = GetLastOrDefault(re4List);
            var re4 = (Pow(cc, 8) * re3) + prevRe3;
            re4List.Add(re4);

            var prevRe5 = GetLastOrDefault(re5List);
            var re5 = (Pow(cc, 16) * re4) + prevRe4;
            re5List.Add(re5);

            var prevRe6 = GetLastOrDefault(re6List);
            var re6 = (Pow(cc, 32) * re5) + prevRe5;
            re6List.Add(re6);

            var prevRe7 = GetLastOrDefault(re7List);
            var re7 = (Pow(cc, 64) * re6) + prevRe6;
            re7List.Add(re7);

            var re8 = (Pow(cc, 128) * re7) + prevRe7;
            var prevWave = GetLastOrDefault(waveList);
            var wave = ema - (alpha * re8);
            waveList.Add(wave);

            var signal = GetCompareSignal(wave, prevWave);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Erema", waveList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(waveList);
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
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var trendList = CalculateEhlersReverseExponentialMovingAverageIndicatorV1(stockData, trendAlpha).CustomValuesList;
        var cycleList = CalculateEhlersReverseExponentialMovingAverageIndicatorV1(stockData, cycleAlpha).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var waveCycle = cycleList[i];
            var waveTrend = trendList[i];
            var prevWaveCycle = i >= 1 ? cycleList[i - 1] : 0;
            var prevWaveTrend = i >= 1 ? trendList[i - 1] : 0;

            var signal = GetCompareSignal(waveCycle - waveTrend, prevWaveCycle - prevWaveTrend);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "EremaCycle", cycleList },
            { "EremaTrend", trendList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
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
        fastLength = Math.Max(fastLength, 1);
        slowLength = Math.Max(slowLength, 1);
        List<double> madList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
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
            madList.Add(mad);

            var signal = GetCompareSignal(mad - prevMad1, prevMad1 - prevMad2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Emad", madList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(madList);
        stockData.IndicatorName = IndicatorName.EhlersMovingAverageDifferenceIndicator;

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
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        length3 = Math.Max(length3, 1);
        length4 = Math.Max(length4, 1);
        List<double> phaseList = new(stockData.Count);
        List<double> dPhaseList = new(stockData.Count);
        List<double> instPeriodList = new(stockData.Count);
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
            var prevDomCyc1 = i >= 1 ? domCycList[i - 1] : 0;
            var prevDomCyc2 = i >= 2 ? domCycList[i - 2] : 0;

            var prevPhase = GetLastOrDefault(phaseList);
            var phase = Math.Abs(real) > 0 ? Math.Atan(Math.Abs(imag / real)).ToDegrees() : 0;
            phase = real < 0 && imag > 0 ? 180 - phase : phase;
            phase = real < 0 && imag < 0 ? 180 + phase : phase;
            phase = real > 0 && imag < 0 ? 360 - phase : phase;
            phaseList.Add(phase);

            var dPhase = prevPhase - phase;
            dPhase = prevPhase < 90 && phase > 270 ? 360 + prevPhase - phase : dPhase;
            dPhase = MinOrMax(dPhase, length1, length3);
            dPhaseList.Add(dPhase);

            var prevInstPeriod = GetLastOrDefault(instPeriodList);
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
            instPeriodList.Add(instPeriod);

            var domCyc = (c1 * ((instPeriod + prevInstPeriod) / 2)) + (c2 * prevDomCyc1) + (c3 * prevDomCyc2);
            domCycList.Add(domCyc);

            var signal = GetCompareSignal(real - prevReal1, prevReal1 - prevReal2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Epadc", domCycList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(domCycList);
        stockData.IndicatorName = IndicatorName.EhlersPhaseAccumulationDominantCycle;

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
        List<double> laguerreRsiList = new(stockData.Count);
        List<double> l0List = new(stockData.Count);
        List<double> l1List = new(stockData.Count);
        List<double> l2List = new(stockData.Count);
        List<double> l3List = new(stockData.Count);
        List<double> cuList = new(stockData.Count);
        List<double> cdList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevL0 = i >= 1 ? GetLastOrDefault(l0List) : currentValue;
            var prevL1 = i >= 1 ? GetLastOrDefault(l1List) : currentValue;
            var prevL2 = i >= 1 ? GetLastOrDefault(l2List) : currentValue;
            var prevL3 = i >= 1 ? GetLastOrDefault(l3List) : currentValue;
            var prevRsi1 = i >= 1 ? laguerreRsiList[i - 1] : 0;
            var prevRsi2 = i >= 2 ? laguerreRsiList[i - 2] : 0;

            var l0 = ((1 - gamma) * currentValue) + (gamma * prevL0);
            l0List.Add(l0);

            var l1 = (-1 * gamma * l0) + prevL0 + (gamma * prevL1);
            l1List.Add(l1);

            var l2 = (-1 * gamma * l1) + prevL1 + (gamma * prevL2);
            l2List.Add(l2);

            var l3 = (-1 * gamma * l2) + prevL2 + (gamma * prevL3);
            l3List.Add(l3);

            var cu = (l0 >= l1 ? l0 - l1 : 0) + (l1 >= l2 ? l1 - l2 : 0) + (l2 >= l3 ? l2 - l3 : 0);
            cuList.Add(cu);

            var cd = (l0 >= l1 ? 0 : l1 - l0) + (l1 >= l2 ? 0 : l2 - l1) + (l2 >= l3 ? 0 : l3 - l2);
            cdList.Add(cd);

            var laguerreRsi = cu + cd != 0 ? MinOrMax(cu / (cu + cd), 1, 0) : 0;
            laguerreRsiList.Add(laguerreRsi);

            var signal = GetRsiSignal(laguerreRsi - prevRsi1, prevRsi1 - prevRsi2, laguerreRsi, prevRsi1, 0.8, 0.2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Elrsi", laguerreRsiList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(laguerreRsiList);
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
        length = Math.Max(length, 1);
        List<double> laguerreRsiList = new(stockData.Count);
        List<double> ratioList = new(stockData.Count);
        List<double> l0List = new(stockData.Count);
        List<double> l1List = new(stockData.Count);
        List<double> l2List = new(stockData.Count);
        List<double> l3List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum ratioSumWindow = new();
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
            ratioList.Add(ratio);
            ratioSumWindow.Add(ratio);

            var ratioSum = ratioSumWindow.Sum(length);
            var alpha = ratioSum > 0 ? MinOrMax(Math.Log(ratioSum) / Math.Log(length), 0.99, 0.01) : 0.01;
            var prevL0 = GetLastOrDefault(l0List);
            var l0 = (alpha * feValue) + ((1 - alpha) * prevL0);
            l0List.Add(l0);

            var prevL1 = GetLastOrDefault(l1List);
            var l1 = (-(1 - alpha) * l0) + prevL0 + ((1 - alpha) * prevL1);
            l1List.Add(l1);

            var prevL2 = GetLastOrDefault(l2List);
            var l2 = (-(1 - alpha) * l1) + prevL1 + ((1 - alpha) * prevL2);
            l2List.Add(l2);

            var prevL3 = GetLastOrDefault(l3List);
            var l3 = (-(1 - alpha) * l2) + prevL2 + ((1 - alpha) * prevL3);
            l3List.Add(l3);

            var cu = (l0 >= l1 ? l0 - l1 : 0) + (l1 >= l2 ? l1 - l2 : 0) + (l2 >= l3 ? l2 - l3 : 0);
            var cd = (l0 >= l1 ? 0 : l1 - l0) + (l1 >= l2 ? 0 : l2 - l1) + (l2 >= l3 ? 0 : l3 - l2);
            var laguerreRsi = cu + cd != 0 ? MinOrMax(cu / (cu + cd), 1, 0) : 0;
            laguerreRsiList.Add(laguerreRsi);

            var signal = GetRsiSignal(laguerreRsi - prevRsi1, prevRsi1 - prevRsi2, laguerreRsi, prevRsi1, 0.8, 0.2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Elrsiwsa", laguerreRsiList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(laguerreRsiList);
        stockData.IndicatorName = IndicatorName.EhlersLaguerreRelativeStrengthIndexWithSelfAdjustingAlpha;

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
        length = Math.Max(length, 1);
        signalLength = Math.Max(signalLength, 1);
        List<double> v1List = new(stockData.Count);
        List<double> iFishList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var rsiList = CalculateRelativeStrengthIndex(stockData, maType, length: length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var rsi = rsiList[i];

            var v1 = 0.1 * (rsi - 50);
            v1List.Add(v1);
        }

        var v2List = GetMovingAverageList(stockData, maType, signalLength, v1List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var v2 = v2List[i];
            var expValue = Exp(2 * v2);
            var prevIfish1 = i >= 1 ? iFishList[i - 1] : 0;
            var prevIfish2 = i >= 2 ? iFishList[i - 2] : 0;

            var iFish = expValue + 1 != 0 ? MinOrMax((expValue - 1) / (expValue + 1), 1, -1) : 0;
            iFishList.Add(iFish);

            var signal = GetRsiSignal(iFish - prevIfish1, prevIfish1 - prevIfish2, iFish, prevIfish1, 0.5, -0.5);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Eiftrsi", iFishList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(iFishList);
        stockData.IndicatorName = IndicatorName.EhlersRelativeStrengthIndexInverseFisherTransform;

        return stockData;
    }
}

