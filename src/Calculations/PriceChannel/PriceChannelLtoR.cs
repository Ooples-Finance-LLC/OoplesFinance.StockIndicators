
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Price Channel
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <param name="pct">The PCT.</param>
    /// <returns></returns>
    public static StockData CalculatePriceChannel(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 21, 
        double pct = 0.06)
    {
        List<double> upperPriceChannelList = new(stockData.Count);
        List<double> lowerPriceChannelList = new(stockData.Count);
        List<double> midPriceChannelList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var emaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentEma = emaList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var upperPriceChannel = currentEma * (1 + pct);
            upperPriceChannelList.Add(upperPriceChannel);

            var lowerPriceChannel = currentEma * (1 - pct);
            lowerPriceChannelList.Add(lowerPriceChannel);

            var prevMidPriceChannel = GetLastOrDefault(midPriceChannelList);
            var midPriceChannel = (upperPriceChannel + lowerPriceChannel) / 2;
            midPriceChannelList.Add(midPriceChannel);

            var signal = GetCompareSignal(currentValue - midPriceChannel, prevValue - prevMidPriceChannel);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperChannel", upperPriceChannelList },
            { "LowerChannel", lowerPriceChannelList },
            { "MiddleChannel", midPriceChannelList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.PriceChannel;

        return stockData;
    }


    /// <summary>
    /// Calculates the Moving Average Channel
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateMovingAverageChannel(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 20)
    {
        List<double> midChannelList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        var highMaList = GetMovingAverageList(stockData, maType, length, highList);
        var lowMaList = GetMovingAverageList(stockData, maType, length, lowList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var upperChannel = highMaList[i];
            var lowerChannel = lowMaList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevMidChannel = GetLastOrDefault(midChannelList);
            var midChannel = (upperChannel + lowerChannel) / 2;
            midChannelList.Add(midChannel);

            var signal = GetCompareSignal(currentValue - midChannel, prevValue - prevMidChannel);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", highMaList },
            { "MiddleBand", midChannelList },
            { "LowerBand", lowMaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.MovingAverageChannel;

        return stockData;
    }


    /// <summary>
    /// Calculates the Moving Average Envelope
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <param name="mult">The mult.</param>
    /// <returns></returns>
    public static StockData CalculateMovingAverageEnvelope(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 20, double mult = 0.025)
    {
        List<double> upperEnvelopeList = new(stockData.Count);
        List<double> lowerEnvelopeList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentSma20 = smaList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevSma20 = i >= 1 ? smaList[i - 1] : 0;
            var factor = currentSma20 * mult;

            var upperEnvelope = currentSma20 + factor;
            upperEnvelopeList.Add(upperEnvelope);

            var lowerEnvelope = currentSma20 - factor;
            lowerEnvelopeList.Add(lowerEnvelope);

            var signal = GetCompareSignal(currentValue - currentSma20, prevValue - prevSma20);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", upperEnvelopeList },
            { "MiddleBand", smaList },
            { "LowerBand", lowerEnvelopeList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.MovingAverageEnvelope;

        return stockData;
    }


    /// <summary>
    /// Calculates the Linear Channels
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="mult"></param>
    /// <returns></returns>
    public static StockData CalculateLinearChannels(this StockData stockData, int length = 14, double mult = 50)
    {
        List<double> aList = new(stockData.Count);
        List<double> upperList = new(stockData.Count);
        List<double> lowerList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var s = (double)1 / length;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevA = i >= 1 ? aList[i - 1] : currentValue;
            var prevA2 = i >= 2 ? aList[i - 2] : currentValue;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var x = currentValue + ((prevA - prevA2) * mult);

            var a = x > prevA + s ? prevA + s : x < prevA - s ? prevA - s : prevA;
            aList.Add(a);

            var up = a + (Math.Abs(a - prevA) * mult);
            var dn = a - (Math.Abs(a - prevA) * mult);

            var prevUpper = GetLastOrDefault(upperList);
            var upper = up == a ? prevUpper : up;
            upperList.Add(upper);

            var prevLower = GetLastOrDefault(lowerList);
            var lower = dn == a ? prevLower : dn;
            lowerList.Add(lower);

            var signal = GetBollingerBandsSignal(currentValue - a, prevValue - prevA, currentValue, prevValue, upper, prevUpper, lower, prevLower);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", upperList },
            { "LowerBand", lowerList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.LinearChannels;

        return stockData;
    }


    /// <summary>
    /// Calculates the Narrow Sideways Channel
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="stdDevMult"></param>
    /// <returns></returns>
    public static StockData CalculateNarrowSidewaysChannel(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 14, double stdDevMult = 3)
    {
        var narrowChannelList = CalculateBollingerBands(stockData, maType, length, stdDevMult);
        var upperBandList = narrowChannelList.OutputValues["UpperBand"];
        var middleBandList = narrowChannelList.OutputValues["MiddleBand"];
        var lowerBandList = narrowChannelList.OutputValues["LowerBand"];
        var signalsList = narrowChannelList.SignalsList;

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", upperBandList },
            { "MiddleBand", middleBandList },
            { "LowerBand", lowerBandList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.NarrowSidewaysChannel;

        return stockData;
    }


    /// <summary>
    /// Calculates the Price Headley Acceleration Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="factor"></param>
    /// <returns></returns>
    public static StockData CalculatePriceHeadleyAccelerationBands(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 20, double factor = 0.001)
    {
        List<double> ubList = new(stockData.Count);
        List<double> lbList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        var middleBandList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var mult = currentHigh + currentLow != 0 ? 4 * factor * 1000 * (currentHigh - currentLow) / (currentHigh + currentLow) : 0;

            var outerUb = currentHigh * (1 + mult);
            ubList.Add(outerUb);

            var outerLb = currentLow * (1 - mult);
            lbList.Add(outerLb);
        }

        var suList = GetMovingAverageList(stockData, maType, length, ubList);
        var slList = GetMovingAverageList(stockData, maType, length, lbList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var middleBand = middleBandList[i];
            var prevMiddleBand = i >= 1 ? middleBandList[i - 1] : 0;
            var outerUbSma = suList[i];
            var prevOuterUbSma = i >= 1 ? suList[i - 1] : 0;
            var outerLbSma = slList[i];
            var prevOuterLbSma = i >= 1 ? slList[i - 1] : 0;

            var signal = GetBollingerBandsSignal(currentValue - middleBand, prevValue - prevMiddleBand, currentValue, prevValue, 
                outerUbSma, prevOuterUbSma, outerLbSma, prevOuterLbSma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", suList },
            { "MiddleBand", middleBandList },
            { "LowerBand", slList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.PriceHeadleyAccelerationBands;

        return stockData;
    }


    /// <summary>
    /// Calculates the Pseudo Polynomial Channel
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="morph"></param>
    /// <returns></returns>
    public static StockData CalculatePseudoPolynomialChannel(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 14, double morph = 0.9)
    {
        List<double> kList = new(stockData.Count);
        List<double> yK1List = new(stockData.Count);
        List<double> indexList = new(stockData.Count);
        List<double> middleBandList = new(stockData.Count);
        List<double> upperBandList = new(stockData.Count);
        List<double> lowerBandList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        double yk1Sum = 0;
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var y = inputList[i];
            var prevK = i >= length ? kList[i - length] : y;
            var prevK2 = i >= length * 2 ? kList[i - (length * 2)] : y;
            var prevIndex = i >= length ? indexList[i - length] : 0;
            var prevIndex2 = i >= length * 2 ? indexList[i - (length * 2)] : 0;
            var ky = (morph * prevK) + ((1 - morph) * y);
            var ky2 = (morph * prevK2) + ((1 - morph) * y);

            double index = i;
            indexList.Add(i);

            var k = prevIndex2 - prevIndex != 0 ? ky + ((index - prevIndex) / (prevIndex2 - prevIndex) * (ky2 - ky)) : 0;
            kList.Add(k);
        }

        var k1List = GetMovingAverageList(stockData, maType, length, kList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var k1 = k1List[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var yk1 = Math.Abs(currentValue - k1);
            yK1List.Add(yk1);

            yk1Sum += yk1;
            var er = i != 0 ? yk1Sum / i : 0;
            var prevUpperBand = GetLastOrDefault(upperBandList);
            var upperBand = k1 + er;
            upperBandList.Add(upperBand);

            var prevLowerBand = GetLastOrDefault(lowerBandList);
            var lowerBand = k1 - er;
            lowerBandList.Add(lowerBand);

            var prevMiddleBand = GetLastOrDefault(middleBandList);
            var middleBand = (upperBand + lowerBand) / 2;
            middleBandList.Add(middleBand);

            var signal = GetBollingerBandsSignal(currentValue - middleBand, prevValue - prevMiddleBand, currentValue, prevValue, 
                upperBand, prevUpperBand, lowerBand, prevLowerBand);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", upperBandList },
            { "MiddleBand", middleBandList },
            { "LowerBand", lowerBandList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.PseudoPolynomialChannel;

        return stockData;
    }


    /// <summary>
    /// Calculates the Projected Support and Resistance
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateProjectedSupportAndResistance(this StockData stockData, int length = 25)
    {
        List<double> support1List = new(stockData.Count);
        List<double> resistance1List = new(stockData.Count);
        List<double> support2List = new(stockData.Count);
        List<double> resistance2List = new(stockData.Count);
        List<double> middleList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var highestHigh = highestList[i];
            var lowestLow = lowestList[i];
            var range = highestHigh - lowestLow;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var support1 = lowestLow - (0.25 * range);
            support1List.Add(support1);

            var support2 = lowestLow - (0.5 * range);
            support2List.Add(support2);

            var resistance1 = highestHigh + (0.25 * range);
            resistance1List.Add(resistance1);

            var resistance2 = highestHigh + (0.5 * range);
            resistance2List.Add(resistance2);

            var prevMiddle = GetLastOrDefault(middleList);
            var middle = (support1 + support2 + resistance1 + resistance2) / 4;
            middleList.Add(middle);

            var signal = GetCompareSignal(currentValue - middle, prevValue - prevMiddle);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Support1", support1List },
            { "Support2", support2List },
            { "Resistance1", resistance1List },
            { "Resistance2", resistance2List },
            { "MiddleBand", middleList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.ProjectedSupportAndResistance;

        return stockData;
    }


    /// <summary>
    /// Calculates the Prime Number Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculatePrimeNumberBands(this StockData stockData, int length = 5)
    {
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        stockData.SetCustomValues(highList);
        var pnoUpBandList = CalculatePrimeNumberOscillator(stockData, length).CustomValuesList;
        stockData.SetCustomValues(lowList);
        var pnoDnBandList = CalculatePrimeNumberOscillator(stockData, length).CustomValuesList;
        var (upperBandList, _) = GetMaxAndMinValuesList(pnoUpBandList, length);
        var (_, lowerBandList) = GetMaxAndMinValuesList(pnoDnBandList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var close = inputList[i];
            var prevUpBand1 = i >= 1 ? upperBandList[i - 1] : 0;
            var prevUpBand2 = i >= 2 ? upperBandList[i - 1] : 0;
            var prevDnBand1 = i >= 1 ? lowerBandList[i - 1] : 0;
            var prevDnBand2 = i >= 2 ? lowerBandList[i - 1] : 0;
            var prevClose = i >= 1 ? inputList[i - 1] : 0;

            var signal = GetBullishBearishSignal(close - prevUpBand1, prevClose - prevUpBand2, close - prevDnBand1, prevClose - prevDnBand2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", upperBandList },
            { "LowerBand", lowerBandList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.PrimeNumberBands;

        return stockData;
    }


    /// <summary>
    /// Calculates the Periodic Channel
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculatePeriodicChannel(this StockData stockData, int length1 = 500, int length2 = 2)
    {
        List<double> tempList = new(stockData.Count);
        List<double> indexList = new(stockData.Count);
        List<double> corrList = new(stockData.Count);
        List<double> absIndexCumDiffList = new(stockData.Count);
        List<double> sinList = new(stockData.Count);
        List<double> inSinList = new(stockData.Count);
        List<double> absSinCumDiffList = new(stockData.Count);
        List<double> absInSinCumDiffList = new(stockData.Count);
        List<double> absDiffList = new(stockData.Count);
        List<double> kList = new(stockData.Count);
        RollingCorrelation corrWindow = new();
        List<double> absKDiffList = new(stockData.Count);
        List<double> osList = new(stockData.Count);
        List<double> apList = new(stockData.Count);
        List<double> bpList = new(stockData.Count);
        List<double> cpList = new(stockData.Count);
        List<double> alList = new(stockData.Count);
        List<double> blList = new(stockData.Count);
        List<double> clList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        double indexSum = 0;
        double absIndexCumDiffSum = 0;
        double corrSum = 0;
        double sinSum = 0;
        double inSinSum = 0;
        double absSinCumDiffSum = 0;
        double absInSinCumDiffSum = 0;
        double tempSum = 0;
        double absDiffSum = 0;
        double absKDiffSum = 0;
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevValue = GetLastOrDefault(tempList);
            var currentValue = inputList[i];
            tempList.Add(currentValue);
            tempSum += currentValue;

            double index = i;
            indexList.Add(index);
            indexSum += index;
            corrWindow.Add(index, currentValue);

            var indexCum = i != 0 ? indexSum / i : 0;
            var indexCumDiff = i - indexCum;
            var absIndexCumDiff = Math.Abs(i - indexCum);
            absIndexCumDiffList.Add(absIndexCumDiff);
            absIndexCumDiffSum += absIndexCumDiff;

            var absIndexCum = i != 0 ? absIndexCumDiffSum / i : 0;       
            var z = absIndexCum != 0 ? indexCumDiff / absIndexCum : 0;

            var corr = corrWindow.R(length2);
            corr = IsValueNullOrInfinity(corr) ? 0 : corr;
            corrList.Add((double)corr);
            corrSum += corr;

            double s = i * Math.Sign(corrSum);
            var sin = Math.Sin(s / length1);
            sinList.Add(sin);
            sinSum += sin;

            var inSin = Math.Sin(s / length1) * -1;
            inSinList.Add(inSin);
            inSinSum += inSin;

            var sinCum = i != 0 ? sinSum / i : 0;
            var inSinCum = i != 0 ? inSinSum / i : 0;
            var sinCumDiff = sin - sinCum;
            var inSinCumDiff = inSin - inSinCum;

            var absSinCumDiff = Math.Abs(sin - sinCum);
            absSinCumDiffList.Add(absSinCumDiff);
            absSinCumDiffSum += absSinCumDiff;

            var absSinCum = i != 0 ? absSinCumDiffSum / i : 0;
            var absInSinCumDiff = Math.Abs(inSin - inSinCum);
            absInSinCumDiffList.Add(absInSinCumDiff);
            absInSinCumDiffSum += absInSinCumDiff;

            var absInSinCum = i != 0 ? absInSinCumDiffSum / i : 0;       
            var zs = absSinCum != 0 ? sinCumDiff / absSinCum : 0;
            var inZs = absInSinCum != 0 ? inSinCumDiff / absInSinCum : 0;       
            var cum = i != 0 ? tempSum / i : 0;

            var absDiff = Math.Abs(currentValue - cum);
            absDiffList.Add(absDiff);
            absDiffSum += absDiff;

            var absDiffCum = i != 0 ? absDiffSum / i : 0;
            var prevK = GetLastOrDefault(kList);
            var k = cum + ((z + zs) * absDiffCum);
            kList.Add(k);

            var inK = cum + ((z + inZs) * absDiffCum);
            var absKDiff = Math.Abs(currentValue - k);
            absKDiffList.Add(absKDiff);
            absKDiffSum += absKDiff;

            var absInKDiff = Math.Abs(currentValue - inK);
            var os = i != 0 ? absKDiffSum / i : 0;
            osList.Add(os);

            var ap = k + os;
            apList.Add(ap);

            var bp = ap + os;
            bpList.Add(bp);

            var cp = bp + os;
            cpList.Add(cp);

            var al = k - os;
            alList.Add(al);

            var bl = al - os;
            blList.Add(bl);

            var cl = bl - os;
            clList.Add(cl);

            var signal = GetCompareSignal(currentValue - k, prevValue - prevK);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "K", kList },
            { "Os", osList },
            { "Ap", apList },
            { "Bp", bpList },
            { "Cp", cpList },
            { "Al", alList },
            { "Bl", blList },
            { "Cl", clList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.PeriodicChannel;

        return stockData;
    }


    /// <summary>
    /// Calculates the Price Line Channel
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculatePriceLineChannel(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, 
        int length = 100)
    {
        List<double> aList = new(stockData.Count);
        List<double> bList = new(stockData.Count);
        List<double> sizeAList = new(stockData.Count);
        List<double> sizeBList = new(stockData.Count);
        List<double> sizeCList = new(stockData.Count);
        List<double> midList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var atrList = CalculateAverageTrueRange(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var atr = atrList[i];
            var prevA1 = i >= 1 ? aList[i - 1] : currentValue;
            var prevB1 = i >= 1 ? bList[i - 1] : currentValue;
            var prevA2 = i >= 2 ? aList[i - 2] : 0;
            var prevB2 = i >= 2 ? bList[i - 2] : 0;
            var prevSizeA = i >= 1 ? sizeAList[i - 1] : atr / length;
            var prevSizeB = i >= 1 ? sizeBList[i - 1] : atr / length;
            var prevSizeC = i >= 1 ? sizeCList[i - 1] : atr / length;

            var sizeA = prevA1 - prevA2 > 0 ? atr : prevSizeA;
            sizeAList.Add(sizeA);

            var sizeB = prevB1 - prevB2 < 0 ? atr : prevSizeB;
            sizeBList.Add(sizeB);

            var sizeC = prevA1 - prevA2 > 0 || prevB1 - prevB2 < 0 ? atr : prevSizeC;
            sizeCList.Add(sizeC);

            var a = Math.Max(currentValue, prevA1) - (sizeA / length);
            aList.Add(a);

            var b = Math.Min(currentValue, prevB1) + (sizeB / length);
            bList.Add(b);

            var prevMid = GetLastOrDefault(midList);
            var mid = (a + b) / 2;
            midList.Add(mid);

            var signal = GetCompareSignal(currentValue - mid, prevValue - prevMid);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", aList },
            { "MiddleBand", midList },
            { "LowerBand", bList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.PriceLineChannel;

        return stockData;
    }


    /// <summary>
    /// Calculates the Price Curve Channel
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculatePriceCurveChannel(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, 
        int length = 100)
    {
        List<double> aList = new(stockData.Count);
        List<double> bList = new(stockData.Count);
        List<double> sizeList = new(stockData.Count);
        List<double> aChgList = new(stockData.Count);
        List<double> bChgList = new(stockData.Count);
        List<double> midList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var atrList = CalculateAverageTrueRange(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var atr = atrList[i];
            var prevA1 = i >= 1 ? aList[i - 1] : currentValue;
            var prevB1 = i >= 1 ? bList[i - 1] : currentValue;
            var prevA2 = i >= 2 ? aList[i - 2] : 0;
            var prevB2 = i >= 2 ? bList[i - 2] : 0;
            var prevSize = i >= 1 ? sizeList[i - 1] : atr / length;

            var size = prevA1 - prevA2 > 0 || prevB1 - prevB2 < 0 ? atr : prevSize;
            sizeList.Add(size);

            double aChg = prevA1 > prevA2 ? 1 : 0;
            aChgList.Add(aChg);

            double bChg = prevB1 < prevB2 ? 1 : 0;
            bChgList.Add(bChg);

            var maxIndexA = aChgList.LastIndexOf(1);
            var maxIndexB = bChgList.LastIndexOf(1);
            var barsSinceA = aChgList.Count - 1 - maxIndexA;
            var barsSinceB = bChgList.Count - 1 - maxIndexB;

            var a = Math.Max(currentValue, prevA1) - (size / Pow(length, 2) * (barsSinceA + 1));
            aList.Add(a);

            var b = Math.Min(currentValue, prevB1) + (size / Pow(length, 2) * (barsSinceB + 1));
            bList.Add(b);

            var prevMid = GetLastOrDefault(midList);
            var mid = (a + b) / 2;
            midList.Add(mid);

            var signal = GetCompareSignal(currentValue - mid, prevValue - prevMid);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", aList },
            { "MiddleBand", midList },
            { "LowerBand", bList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.PriceCurveChannel;

        return stockData;
    }


    /// <summary>
    /// Calculates the Projection Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateProjectionBands(this StockData stockData, int length = 14)
    {
        List<double> puList = new(stockData.Count);
        List<double> plList = new(stockData.Count);
        List<double> middleBandList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        stockData.SetCustomValues(lowList);
        var lowSlopeList = CalculateLinearRegression(stockData, length).OutputValues["Slope"];
        stockData.SetCustomValues(highList);
        var highSlopeList = CalculateLinearRegression(stockData, length).OutputValues["Slope"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var prevPu = i >= 1 ? puList[i - 1] : 0;
            var prevPl = i >= 1 ? plList[i - 1] : 0;

            double pu = currentHigh, pl = currentLow;
            for (var j = 1; j <= length; j++)
            {
                var highSlope = i >= j ? highSlopeList[i - j] : 0;
                var lowSlope = i >= j ? lowSlopeList[i - j] : 0;
                var pHigh = i >= j - 1 ? highList[i - (j - 1)] : 0;
                var pLow = i >= j - 1 ? lowList[i - (j - 1)] : 0;
                var vHigh = pHigh + (highSlope * j);
                var vLow = pLow + (lowSlope * j);
                pu = Math.Max(pu, vHigh);
                pl = Math.Min(pl, vLow);
            }
            puList.Add(pu);
            plList.Add(pl);

            var prevMiddleBand = GetLastOrDefault(middleBandList);
            var middleBand = (pu + pl) / 2;
            middleBandList.Add(middleBand);

            var signal = GetBollingerBandsSignal(currentValue - middleBand, prevValue - prevMiddleBand, currentValue, prevValue, pu, prevPu, pl, prevPl);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", puList },
            { "MiddleBand", middleBandList },
            { "LowerBand", plList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.ProjectionBands;

        return stockData;
    }


    /// <summary>
    /// Calculates the Range Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="stdDevFactor"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateRangeBands(this StockData stockData, double stdDevFactor = 1, 
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14)
    {
        List<double> upperBandList = new(stockData.Count);
        List<double> lowerBandList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);
        var (highestList, lowestList) = GetMaxAndMinValuesList(smaList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var middleBand = smaList[i];
            var currentValue = inputList[i];
            var highest = highestList[i];
            var lowest = lowestList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevMiddleBand = i >= 1 ? smaList[i - 1] : 0;
            var rangeDev = highest - lowest;

            var prevUpperBand = GetLastOrDefault(upperBandList);
            var upperBand = middleBand + (rangeDev * stdDevFactor);
            upperBandList.Add(upperBand);

            var prevLowerBand = GetLastOrDefault(lowerBandList);
            var lowerBand = middleBand - (rangeDev * stdDevFactor);
            lowerBandList.Add(lowerBand);

            var signal = GetBollingerBandsSignal(currentValue - middleBand, prevValue - prevMiddleBand, currentValue, prevValue, 
                upperBand, prevUpperBand, lowerBand, prevLowerBand);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", upperBandList },
            { "MiddleBand", smaList },
            { "LowerBand", lowerBandList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.RangeBands;

        return stockData;
    }


    /// <summary>
    /// Calculates the Range Identifier
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateRangeIdentifier(this StockData stockData, int length = 34)
    {
        List<double> upList = new(stockData.Count);
        List<double> downList = new(stockData.Count);
        List<double> midList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var prevUp = GetLastOrDefault(upList);
            var prevDown = GetLastOrDefault(downList);

            var up = currentValue < prevUp && currentValue > prevDown ? prevUp : currentHigh;
            upList.Add(up);

            var down = currentValue < prevUp && currentValue > prevDown ? prevDown : currentLow;
            downList.Add(down);

            var prevMid = GetLastOrDefault(midList);
            var mid = (up + down) / 2;
            midList.Add(mid);

            var signal = GetCompareSignal(currentValue - mid, prevValue - prevMid);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", upList },
            { "MiddleBand", midList },
            { "LowerBand", downList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.RangeIdentifier;

        return stockData;
    }


    /// <summary>
    /// Calculates the Rate Of Change Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateRateOfChangeBands(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length = 12, int smoothLength = 3)
    {
        List<double> rocSquaredList = new(stockData.Count);
        List<double> upperBandList = new(stockData.Count);
        List<double> lowerBandList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum rocSquaredSum = new();

        var rocList = CalculateRateOfChange(stockData, length).CustomValuesList;
        var middleBandList = GetMovingAverageList(stockData, maType, smoothLength, rocList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var roc = rocList[i];
            var middleBand = middleBandList[i];
            var prevMiddleBand1 = i >= 1 ? middleBandList[i - 1] : 0;
            var prevMiddleBand2 = i >= 2 ? middleBandList[i - 2] : 0;

            var rocSquared = Pow(roc, 2);
            rocSquaredList.Add(rocSquared);
            rocSquaredSum.Add(rocSquared);

            var squaredAvg = rocSquaredSum.Average(length);
            var prevUpperBand = GetLastOrDefault(upperBandList);
            var upperBand = Sqrt(squaredAvg);
            upperBandList.Add(upperBand);

            var prevLowerBand = GetLastOrDefault(lowerBandList);
            var lowerBand = -upperBand;
            lowerBandList.Add(lowerBand);

            var signal = GetBollingerBandsSignal(middleBand - prevMiddleBand1, prevMiddleBand1 - prevMiddleBand2, middleBand, prevMiddleBand1, 
                upperBand, prevUpperBand, lowerBand, prevLowerBand);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", upperBandList },
            { "MiddleBand", middleBandList },
            { "LowerBand", lowerBandList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.RateOfChangeBands;

        return stockData;
    }


    /// <summary>
    /// Calculates the Root Moving Average Squared Error Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="stdDevFactor"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateRootMovingAverageSquaredErrorBands(this StockData stockData, double stdDevFactor = 1, 
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14)
    {
        List<double> upperBandList = new(stockData.Count);
        List<double> lowerBandList = new(stockData.Count);
        List<double> powList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var sma = smaList[i];
            var currentValue = inputList[i];

            var pow = Pow(currentValue - sma, 2);
            powList.Add(pow);
        }

        var powSmaList = GetMovingAverageList(stockData, maType, length, powList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var middleBand = smaList[i];
            var currentValue = inputList[i];
            var powSma = powSmaList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevMiddleBand = i >= 1 ? smaList[i - 1] : 0;
            var rmaseDev = Sqrt(powSma);

            var prevUpperBand = GetLastOrDefault(upperBandList);
            var upperBand = middleBand + (rmaseDev * stdDevFactor);
            upperBandList.Add(upperBand);

            var prevLowerBand = GetLastOrDefault(lowerBandList);
            var lowerBand = middleBand - (rmaseDev * stdDevFactor);
            lowerBandList.Add(lowerBand);

            var signal = GetBollingerBandsSignal(currentValue - middleBand, prevValue - prevMiddleBand, currentValue, prevValue, 
                upperBand, prevUpperBand, lowerBand, prevLowerBand);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", upperBandList },
            { "MiddleBand", smaList },
            { "LowerBand", lowerBandList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.RootMovingAverageSquaredErrorBands;

        return stockData;
    }


    /// <summary>
    /// Calculates the Moving Average Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="mult"></param>
    /// <returns></returns>
    public static StockData CalculateMovingAverageBands(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int fastLength = 10, int slowLength = 50, double mult = 1)
    {
        List<double> sqList = new(stockData.Count);
        List<double> upperBandList = new(stockData.Count);
        List<double> lowerBandList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum sqSumWindow = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var fastMaList = GetMovingAverageList(stockData, maType, fastLength, inputList);
        var slowMaList = GetMovingAverageList(stockData, maType, slowLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var fastMa = fastMaList[i];
            var slowMa = slowMaList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevFastMa = i >= 1 ? fastMaList[i - 1] : 0;

            var sq = Pow(slowMa - fastMa, 2);
            sqList.Add(sq);
            sqSumWindow.Add(sq);

            var dev = Sqrt(sqSumWindow.Average(fastLength)) * mult;
            var prevUpperBand = GetLastOrDefault(upperBandList);
            var upperBand = slowMa + dev;
            upperBandList.Add(upperBand);

            var prevLowerBand = GetLastOrDefault(lowerBandList);
            var lowerBand = slowMa - dev;
            lowerBandList.Add(lowerBand);

            var signal = GetBollingerBandsSignal(currentValue - fastMa, prevValue - prevFastMa, currentValue, prevValue, 
                upperBand, prevUpperBand, lowerBand, prevLowerBand);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", upperBandList },
            { "MiddleBand", fastMaList },
            { "LowerBand", lowerBandList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.MovingAverageBands;

        return stockData;
    }


    /// <summary>
    /// Calculates the Moving Average Support Resistance
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="factor"></param>
    /// <returns></returns>
    public static StockData CalculateMovingAverageSupportResistance(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 10, double factor = 2)
    {
        List<double> topList = new(stockData.Count);
        List<double> bottomList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var supportLevel = 1 + (factor / 100);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentSma = smaList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevSma = i >= 1 ? smaList[i - 1] : 0;

            var top = currentSma * supportLevel;
            topList.Add(top);

            var bottom = supportLevel != 0 ? currentSma / supportLevel : 0;
            bottomList.Add(bottom);

            var signal = GetCompareSignal(currentValue - currentSma, prevValue - prevSma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", topList },
            { "MiddleBand", smaList },
            { "LowerBand", bottomList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.MovingAverageSupportResistance;

        return stockData;
    }


    /// <summary>
    /// Calculates the Motion To Attraction Channels
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateMotionToAttractionChannels(this StockData stockData, int length = 14)
    {
        List<double> aList = new(stockData.Count);
        List<double> bList = new(stockData.Count);
        List<double> cList = new(stockData.Count);
        List<double> dList = new(stockData.Count);
        List<double> aMaList = new(stockData.Count);
        List<double> bMaList = new(stockData.Count);
        List<double> avgMaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var alpha = (double)1 / length;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevAMa = i >= 1 ? aMaList[i - 1] : currentValue;
            var prevBMa = i >= 1 ? bMaList[i - 1] : currentValue;

            var prevA = i >= 1 ? aList[i - 1] : currentValue;
            var a = currentValue > prevAMa ? currentValue : prevA;
            aList.Add(a);

            var prevB = i >= 1 ? bList[i - 1] : currentValue;
            var b = currentValue < prevBMa ? currentValue : prevB;
            bList.Add(b);

            var prevC = GetLastOrDefault(cList);
            var c = b - prevB != 0 ? prevC + alpha : a - prevA != 0 ? 0 : prevC;
            cList.Add(c);

            var prevD = GetLastOrDefault(dList);
            var d = a - prevA != 0 ? prevD + alpha : b - prevB != 0 ? 0 : prevD;
            dList.Add(d);

            var avg = (a + b) / 2;
            var aMa = (c * avg) + ((1 - c) * a);
            aMaList.Add(aMa);

            var bMa = (d * avg) + ((1 - d) * b);
            bMaList.Add(bMa);

            var prevAvgMa = GetLastOrDefault(avgMaList);
            var avgMa = (aMa + bMa) / 2;
            avgMaList.Add(avgMa);

            var signal = GetCompareSignal(currentValue - avgMa, prevValue - prevAvgMa);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", aMaList },
            { "MiddleBand", avgMaList },
            { "LowerBand", bMaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.MotionToAttractionChannels;

        return stockData;
    }


    /// <summary>
    /// Calculates the Mean Absolute Error Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="stdDevFactor"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateMeanAbsoluteErrorBands(this StockData stockData, double stdDevFactor = 1, 
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14)
    {
        List<double> upperBandList = new(stockData.Count);
        List<double> lowerBandList = new(stockData.Count);
        List<double> devList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        double devSum = 0;
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var middleBand = smaList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevMiddleBand = i >= 1 ? smaList[i - 1] : 0;

            var dev = Math.Abs(currentValue - middleBand);
            devList.Add(dev);

            devSum += dev;
            var maeDev = i != 0 ? devSum / i : 0;
            var prevUpperBand = GetLastOrDefault(upperBandList);
            var upperBand = middleBand + (maeDev * stdDevFactor);
            upperBandList.Add(upperBand);

            var prevLowerBand = GetLastOrDefault(lowerBandList);
            var lowerBand = middleBand - (maeDev * stdDevFactor);
            lowerBandList.Add(lowerBand);

            var signal = GetBollingerBandsSignal(currentValue - middleBand, prevValue - prevMiddleBand, currentValue, prevValue, 
                upperBand, prevUpperBand, lowerBand, prevLowerBand);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", upperBandList },
            { "MiddleBand", smaList },
            { "LowerBand", lowerBandList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.MeanAbsoluteErrorBands;

        return stockData;
    }


    /// <summary>
    /// Calculates the Mean Absolute Deviation Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="stdDevFactor"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateMeanAbsoluteDeviationBands(this StockData stockData, double stdDevFactor = 2, 
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 20)
    {
        List<double> upperBandList = new(stockData.Count);
        List<double> lowerBandList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);
        var devList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var middleBand = smaList[i];
            var currentValue = inputList[i];
            var currentStdDeviation = devList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevMiddleBand = i >= 1 ? smaList[i - 1] : 0;

            var prevUpperBand = GetLastOrDefault(upperBandList);
            var upperBand = middleBand + (currentStdDeviation * stdDevFactor);
            upperBandList.Add(upperBand);

            var prevLowerBand = GetLastOrDefault(lowerBandList);
            var lowerBand = middleBand - (currentStdDeviation * stdDevFactor);
            lowerBandList.Add(lowerBand);

            var signal = GetBollingerBandsSignal(currentValue - middleBand, prevValue - prevMiddleBand, currentValue, prevValue, 
                upperBand, prevUpperBand, lowerBand, prevLowerBand);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", upperBandList },
            { "MiddleBand", smaList },
            { "LowerBand", lowerBandList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.MeanAbsoluteErrorBands;

        return stockData;
    }


    /// <summary>
    /// Calculates the Moving Average Displaced Envelope
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="pct"></param>
    /// <returns></returns>
    public static StockData CalculateMovingAverageDisplacedEnvelope(this StockData stockData, 
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 9, int length2 = 13, double pct = 0.5)
    {
        List<double> upperEnvelopeList = new(stockData.Count);
        List<double> lowerEnvelopeList = new(stockData.Count);
        List<double> middleEnvelopeList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var emaList = GetMovingAverageList(stockData, maType, length1, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevEma = i >= length2 ? emaList[i - length2] : 0;

            var prevUpperEnvelope = GetLastOrDefault(upperEnvelopeList);
            var upperEnvelope = prevEma * ((100 + pct) / 100);
            upperEnvelopeList.Add(upperEnvelope);

            var prevLowerEnvelope = GetLastOrDefault(lowerEnvelopeList);
            var lowerEnvelope = prevEma * ((100 - pct) / 100);
            lowerEnvelopeList.Add(lowerEnvelope);

            var prevMiddleEnvelope = GetLastOrDefault(middleEnvelopeList);
            var middleEnvelope = (upperEnvelope + lowerEnvelope) / 2;
            middleEnvelopeList.Add(middleEnvelope);

            var signal = GetBollingerBandsSignal(currentValue - middleEnvelope, prevValue - prevMiddleEnvelope, currentValue, prevValue,
                upperEnvelope, prevUpperEnvelope, lowerEnvelope, prevLowerEnvelope);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", upperEnvelopeList },
            { "MiddleBand", middleEnvelopeList },
            { "LowerBand", lowerEnvelopeList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.MovingAverageDisplacedEnvelope;

        return stockData;
    }

}

