using OoplesFinance.StockIndicators.Compatibility;
using OoplesFinance.StockIndicators.Core;

namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the McClellan Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="signalLength"></param>
    /// <param name="mult"></param>
    /// <returns></returns>
    public static StockData CalculateMcClellanOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int fastLength = 19, int slowLength = 39, int signalLength = 9, double mult = 1000)
    {
        List<double> advancesSumList = new(stockData.Count);
        List<double> declinesSumList = new(stockData.Count);
        List<double> ranaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var advancesSumWindow = new RollingSum();
        var declinesSumWindow = new RollingSum();

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            double advance = currentValue > prevValue ? 1 : 0;
            advancesSumWindow.Add(advance);

            double decline = currentValue < prevValue ? 1 : 0;
            declinesSumWindow.Add(decline);

            var advanceSum = advancesSumWindow.Sum(fastLength);
            advancesSumList.Add(advanceSum);

            var declineSum = declinesSumWindow.Sum(fastLength);
            declinesSumList.Add(declineSum);

            var rana = advanceSum + declineSum != 0 ? mult * (advanceSum - declineSum) / (advanceSum + declineSum) : 0;
            ranaList.Add(rana);
        }

        stockData.SetCustomValues(ranaList);
        var moList = CalculateMovingAverageConvergenceDivergence(stockData, maType, fastLength, slowLength, signalLength);
        var mcclellanOscillatorList = moList.OutputValues["Macd"];
        var mcclellanSignalLineList = moList.OutputValues["Signal"];
        var mcclellanHistogramList = moList.OutputValues["Histogram"];
        for (var i = 0; i < stockData.Count; i++)
        {
            var mcclellanHistogram = mcclellanHistogramList[i];
            var prevMcclellanHistogram = i >= 1 ? mcclellanHistogramList[i - 1] : 0;

            var signal = GetCompareSignal(mcclellanHistogram, prevMcclellanHistogram);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "AdvSum", advancesSumList },
            { "DecSum", declinesSumList },
            { "Mo", mcclellanOscillatorList },
            { "Signal", mcclellanSignalLineList },
            { "Histogram", mcclellanHistogramList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(mcclellanOscillatorList);
        stockData.IndicatorName = IndicatorName.McClellanOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Quantitative Qualitative Estimation
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <param name="fastFactor"></param>
    /// <param name="slowFactor"></param>
    /// <returns></returns>
    public static StockData CalculateQuantitativeQualitativeEstimation(this StockData stockData,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14, int smoothLength = 5, double fastFactor = 2.618,
        double slowFactor = 4.236)
    {
        List<double> atrRsiList = new(stockData.Count);
        List<double> fastAtrRsiList = new(stockData.Count);
        List<double> slowAtrRsiList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var wildersLength = (length * 2) - 1;

        var rsiValueList = CalculateRelativeStrengthIndex(stockData, maType, length, smoothLength);
        var rsiEmaList = rsiValueList.OutputValues["Signal"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentRsiEma = rsiEmaList[i];
            var prevRsiEma = i >= 1 ? rsiEmaList[i - 1] : 0;

            var atrRsi = Math.Abs(currentRsiEma - prevRsiEma);
            atrRsiList.Add(atrRsi);
        }

        var atrRsiEmaList = GetMovingAverageList(stockData, maType, wildersLength, atrRsiList);
        var atrRsiEmaSmoothList = GetMovingAverageList(stockData, maType, wildersLength, atrRsiEmaList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var atrRsiEmaSmooth = atrRsiEmaSmoothList[i];
            var prevAtrRsiEmaSmooth = i >= 1 ? atrRsiEmaSmoothList[i - 1] : 0;

            var prevFastTl = GetLastOrDefault(fastAtrRsiList);
            var fastTl = atrRsiEmaSmooth * fastFactor;
            fastAtrRsiList.Add(fastTl);

            var prevSlowTl = GetLastOrDefault(slowAtrRsiList);
            var slowTl = atrRsiEmaSmooth * slowFactor;
            slowAtrRsiList.Add(slowTl);

            var signal = GetBullishBearishSignal(atrRsiEmaSmooth - Math.Max(fastTl, slowTl), prevAtrRsiEmaSmooth - Math.Max(prevFastTl, prevSlowTl),
                atrRsiEmaSmooth - Math.Min(fastTl, slowTl), prevAtrRsiEmaSmooth - Math.Min(prevFastTl, prevSlowTl));
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "FastAtrRsi", fastAtrRsiList },
            { "SlowAtrRsi", slowAtrRsiList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.QuantitativeQualitativeEstimation;

        return stockData;
    }


    /// <summary>
    /// Calculates the Quasi White Noise
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="noiseLength"></param>
    /// <param name="divisor"></param>
    /// <returns></returns>
    public static StockData CalculateQuasiWhiteNoise(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod,
        int length = 20, int noiseLength = 500, double divisor = 40)
    {
        List<double> whiteNoiseList = new(stockData.Count);
        List<double> whiteNoiseVarianceList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var connorsRsiList = CalculateConnorsRelativeStrengthIndex(stockData, maType, noiseLength, noiseLength, length).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var connorsRsi = connorsRsiList[i];
            var prevConnorsRsi1 = i >= 1 ? connorsRsiList[i - 1] : 0;
            var prevConnorsRsi2 = i >= 2 ? connorsRsiList[i - 2] : 0;

            var whiteNoise = (connorsRsi - 50) * (1 / divisor);
            whiteNoiseList.Add(whiteNoise);

            var signal = GetRsiSignal(connorsRsi - prevConnorsRsi1, prevConnorsRsi1 - prevConnorsRsi2, connorsRsi, prevConnorsRsi1, 70, 30);
            signalsList?.Add(signal);
        }

        var whiteNoiseSmaList = GetMovingAverageList(stockData, maType, noiseLength, whiteNoiseList);
        stockData.SetCustomValues(whiteNoiseList);
        var whiteNoiseStdDevList = CalculateStandardDeviationVolatility(stockData, maType, noiseLength).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var whiteNoiseStdDev = whiteNoiseStdDevList[i];

            var whiteNoiseVariance = Pow(whiteNoiseStdDev, 2);
            whiteNoiseVarianceList.Add(whiteNoiseVariance);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "WhiteNoise", whiteNoiseList },
            { "WhiteNoiseMa", whiteNoiseSmaList },
            { "WhiteNoiseStdDev", whiteNoiseStdDevList },
            { "WhiteNoiseVariance", whiteNoiseVarianceList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(whiteNoiseList);
        stockData.IndicatorName = IndicatorName.QuasiWhiteNoise;

        return stockData;
    }


    /// <summary>
    /// Calculates the LBR Paint Bars
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="lbLength"></param>
    /// <param name="atrMult"></param>
    /// <returns></returns>
    public static StockData CalculateLBRPaintBars(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 9,
        int lbLength = 16, double atrMult = 2.5)
    {
        List<double> upperBandList = new(stockData.Count);
        List<double> lowerBandList = new(stockData.Count);
        List<double> aatrList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, lbLength);

        var atrList = CalculateAverageTrueRange(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var highest = highestList[i];
            var lowest = lowestList[i];
            var currentAtr = atrList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var aatr = atrMult * currentAtr;
            aatrList.Add(aatr);

            var prevLowerBand = GetLastOrDefault(lowerBandList);
            var lowerBand = lowest + aatr;
            lowerBandList.Add(lowerBand);

            var prevUpperBand = GetLastOrDefault(upperBandList);
            var upperBand = highest - aatr;
            upperBandList.Add(upperBand);

            var signal = GetBullishBearishSignal(currentValue - Math.Max(lowerBand, upperBand), prevValue - Math.Max(prevLowerBand, prevUpperBand),
                currentValue - Math.Min(lowerBand, upperBand), prevValue - Math.Min(prevLowerBand, prevUpperBand));
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", upperBandList },
            { "LowerBand", lowerBandList },
            { "MiddleBand", aatrList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.LBRPaintBars;

        return stockData;
    }


    /// <summary>
    /// Calculates the Linear Quadratic Convergence Divergence Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculateLinearQuadraticConvergenceDivergenceOscillator(this StockData stockData,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 50, int signalLength = 25)
    {
        List<double> lqcdList = new(stockData.Count);
        List<double> histList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var linregList = CalculateLinearRegression(stockData, length).CustomValuesList;
        var yList = CalculateQuadraticRegression(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var linreg = linregList[i];
            var y = yList[i];

            var lqcd = y - linreg;
            lqcdList.Add(lqcd);
        }

        var signList = GetMovingAverageList(stockData, maType, signalLength, lqcdList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var sign = signList[i];
            var lqcd = lqcdList[i];
            var osc = lqcd - sign;

            var prevHist = GetLastOrDefault(histList);
            var hist = osc - sign;
            histList.Add(hist);

            var signal = GetCompareSignal(hist, prevHist);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Lqcdo", histList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(histList);
        stockData.IndicatorName = IndicatorName.LinearQuadraticConvergenceDivergenceOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Logistic Correlation
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="k"></param>
    /// <returns></returns>
    public static StockData CalculateLogisticCorrelation(this StockData stockData, int length = 100, double k = 10)
    {
        List<double> logList = new(stockData.Count);
        List<double> corrList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var corrWindow = new RollingCorrelation();

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            corrWindow.Add(i, currentValue);

            var corr = corrWindow.R(length);
            corr = IsValueNullOrInfinity(corr) ? 0 : corr;
            corrList.Add((double)corr);
        }

        for (var i = 0; i < stockData.Count; i++)
        {
            var corr = corrList[i];
            var prevLog1 = i >= 1 ? logList[i - 1] : 0;
            var prevLog2 = i >= 2 ? logList[i - 2] : 0;

            var log = 1 / (1 + Exp(k * -corr));
            logList.Add(log);

            var signal = GetCompareSignal(log - prevLog1, prevLog1 - prevLog2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "LogCorr", logList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(logList);
        stockData.IndicatorName = IndicatorName.LogisticCorrelation;

        return stockData;
    }


    /// <summary>
    /// Calculates the Linda Raschke 3/10 Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateLindaRaschke3_10Oscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int fastLength = 3, int slowLength = 10, int smoothLength = 16)
    {
        List<double> macdList = new(stockData.Count);
        List<double> macdHistogramList = new(stockData.Count);
        List<double> ppoList = new(stockData.Count);
        List<double> ppoHistogramList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var fastSmaList = GetMovingAverageList(stockData, maType, fastLength, inputList);
        var slowSmaList = GetMovingAverageList(stockData, maType, slowLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var sma3 = fastSmaList[i];
            var sma10 = slowSmaList[i];

            var ppo = sma10 != 0 ? (sma3 - sma10) / sma10 * 100 : 0;
            ppoList.Add(ppo);

            var macd = sma3 - sma10;
            macdList.Add(macd);
        }

        var macdSignalLineList = GetMovingAverageList(stockData, maType, smoothLength, macdList);
        var ppoSignalLineList = GetMovingAverageList(stockData, maType, smoothLength, ppoList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var ppo = ppoList[i];
            var ppoSignalLine = ppoSignalLineList[i];
            var macd = macdList[i];
            var macdSignalLine = macdSignalLineList[i];

            var ppoHistogram = ppo - ppoSignalLine;
            ppoHistogramList.Add(ppoHistogram);

            var prevMacdHistogram = GetLastOrDefault(macdHistogramList);
            var macdHistogram = macd - macdSignalLine;
            macdHistogramList.Add(macdHistogram);

            var signal = GetCompareSignal(macdHistogram, prevMacdHistogram);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "LindaMacd", macdList },
            { "LindaMacdSignal", macdSignalLineList },
            { "LindaMacdHistogram", macdHistogramList },
            { "LindaPpo", ppoList },
            { "LindaPpoSignal", ppoSignalLineList },
            { "LindaPpoHistogram", ppoHistogramList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(macdList);
        stockData.IndicatorName = IndicatorName.LindaRaschke3_10Oscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Relative Volatility Index V1
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateRelativeVolatilityIndexV1(this StockData stockData,
        MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int length = 10, int smoothLength = 14)
    {
        List<double> upList = new(stockData.Count);
        List<double> downList = new(stockData.Count);
        List<double> rviOriginalList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var stdDeviationList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentStdDeviation = stdDeviationList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var up = currentValue > prevValue ? currentStdDeviation : 0;
            upList.Add(up);

            var down = currentValue < prevValue ? currentStdDeviation : 0;
            downList.Add(down);
        }

        var upAvgList = GetMovingAverageList(stockData, maType, smoothLength, upList);
        var downAvgList = GetMovingAverageList(stockData, maType, smoothLength, downList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var avgUp = upAvgList[i];
            var avgDown = downAvgList[i];
            var prevRvi1 = i >= 1 ? rviOriginalList[i - 1] : 0;
            var prevRvi2 = i >= 2 ? rviOriginalList[i - 2] : 0;
            var rs = avgDown != 0 ? avgUp / avgDown : 0;

            var rvi = avgDown == 0 ? 100 : avgUp == 0 ? 0 : MinOrMax(100 - (100 / (1 + rs)), 100, 0);
            rviOriginalList.Add(rvi);

            var signal = GetRsiSignal(rvi - prevRvi1, prevRvi1 - prevRvi2, rvi, prevRvi1, 70, 30);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Rvi", rviOriginalList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(rviOriginalList);
        stockData.IndicatorName = IndicatorName.RelativeVolatilityIndexV1;

        return stockData;
    }


    /// <summary>
    /// Calculates the Relative Volatility Index V2
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateRelativeVolatilityIndexV2(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod,
        int length = 10, int smoothLength = 14)
    {
        List<double> rviList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (_, highList, lowList, _, _) = GetInputValuesList(stockData);

        stockData.SetCustomValues(highList);
        var rviHighList = CalculateRelativeVolatilityIndexV1(stockData, maType, length, smoothLength).CustomValuesList;
        stockData.SetCustomValues(lowList);
        var rviLowList = CalculateRelativeVolatilityIndexV1(stockData, maType, length, smoothLength).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var rviOriginalHigh = rviHighList[i];
            var rviOriginalLow = rviLowList[i];
            var prevRvi1 = i >= 1 ? rviList[i - 1] : 0;
            var prevRvi2 = i >= 2 ? rviList[i - 2] : 0;

            var rvi = (rviOriginalHigh + rviOriginalLow) / 2;
            rviList.Add(rvi);

            var signal = GetRsiSignal(rvi - prevRvi1, prevRvi1 - prevRvi2, rvi, prevRvi1, 70, 30);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Rvi", rviList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(rviList);
        stockData.IndicatorName = IndicatorName.RelativeVolatilityIndexV2;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ocean Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateOceanIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 14)
    {
        List<double> lnList = new(stockData.Count);
        List<double> oiList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevLn = i >= length ? lnList[i - length] : 0;

            var ln = currentValue > 0 ? Math.Log(currentValue) * 1000 : 0;
            lnList.Add(ln);

            var oi = (ln - prevLn) / Sqrt(length) * 100;
            oiList.Add(oi);
        }

        var oiEmaList = GetMovingAverageList(stockData, maType, length, oiList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var oiEma = oiEmaList[i];
            var prevOiEma1 = i >= 1 ? oiEmaList[i - 1] : 0;
            var prevOiEma2 = i >= 2 ? oiEmaList[i - 2] : 0;

            var signal = GetCompareSignal(oiEma - prevOiEma1, prevOiEma1 - prevOiEma2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Oi", oiList },
            { "Signal", oiEmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(oiList);
        stockData.IndicatorName = IndicatorName.OceanIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Oscar Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateOscarIndicator(this StockData stockData, int length = 8)
    {
        List<double> oscarList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var highest = highestList[i];
            var lowest = lowestList[i];
            var rough = highest - lowest != 0 ? MinOrMax((currentValue - lowest) / (highest - lowest) * 100, 100, 0) : 0;
            var prevOscar1 = i >= 1 ? oscarList[i - 1] : 0;
            var prevOscar2 = i >= 2 ? oscarList[i - 2] : 0;

            var oscar = (prevOscar1 / 6) + (rough / 3);
            oscarList.Add(oscar);

            var signal = GetRsiSignal(oscar - prevOscar1, prevOscar1 - prevOscar2, oscar, prevOscar1, 80, 20);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Oscar", oscarList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(oscarList);
        stockData.IndicatorName = IndicatorName.OscarIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the OC Histogram
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateOCHistogram(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 10)
    {
        List<double> ocHistogramList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, openList, _) = GetInputValuesList(stockData);

        var openEmaList = GetMovingAverageList(stockData, maType, length, openList);
        var closeEmaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentCloseEma = closeEmaList[i];
            var currentOpenEma = openEmaList[i];

            var prevOcHistogram = GetLastOrDefault(ocHistogramList);
            var ocHistogram = currentCloseEma - currentOpenEma;
            ocHistogramList.Add(ocHistogram);

            var signal = GetCompareSignal(ocHistogram, prevOcHistogram);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "OcHistogram", ocHistogramList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(ocHistogramList);
        stockData.IndicatorName = IndicatorName.OCHistogram;

        return stockData;
    }


    /// <summary>
    /// Calculates the Osc Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <returns></returns>
    public static StockData CalculateOscOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int fastLength = 7, int slowLength = 14)
    {
        List<double> oscList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var fastSmaList = GetMovingAverageList(stockData, maType, fastLength, inputList);
        var slowSmaList = GetMovingAverageList(stockData, maType, slowLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var fastSma = fastSmaList[i];
            var slowSma = slowSmaList[i];
            var prevOsc1 = i >= 1 ? oscList[i - 1] : 0;
            var prevOsc2 = i >= 2 ? oscList[i - 2] : 0;

            var osc = slowSma - fastSma;
            oscList.Add(osc);

            var signal = GetCompareSignal(osc - prevOsc1, prevOsc1 - prevOsc2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "OscOscillator", oscList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(oscList);
        stockData.IndicatorName = IndicatorName.OscOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Natural Directional Combo
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateNaturalDirectionalCombo(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 40, int smoothLength = 20)
    {
        List<double> nxcList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var ndxList = CalculateNaturalDirectionalIndex(stockData, maType, length, smoothLength).CustomValuesList;
        var nstList = CalculateNaturalStochasticIndicator(stockData, maType, length, smoothLength).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var ndx = ndxList[i];
            var nst = nstList[i];
            var prevNxc1 = i >= 1 ? nxcList[i - 1] : 0;
            var prevNxc2 = i >= 2 ? nxcList[i - 2] : 0;
            var v3 = Math.Sign(ndx) != Math.Sign(nst) ? ndx * nst : ((Math.Abs(ndx) * nst) + (Math.Abs(nst) * ndx)) / 2;

            var nxc = Math.Sign(v3) * Sqrt(Math.Abs(v3));
            nxcList.Add(nxc);

            var signal = GetCompareSignal(nxc - prevNxc1, prevNxc1 - prevNxc2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Nxc", nxcList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(nxcList);
        stockData.IndicatorName = IndicatorName.NaturalDirectionalCombo;

        return stockData;
    }


    /// <summary>
    /// Calculates the Natural Directional Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateNaturalDirectionalIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 40, int smoothLength = 20)
    {
        List<double> lnList = new(stockData.Count);
        List<double> rawNdxList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];

            var ln = currentValue > 0 ? Math.Log(currentValue) * 1000 : 0;
            lnList.Add(ln);

            double weightSum = 0, denomSum = 0, absSum = 0;
            for (var j = 0; j < length; j++)
            {
                var prevLn = i >= j + 1 ? lnList[i - (j + 1)] : 0;
                var currLn = i >= j ? lnList[i - j] : 0;
                var diff = prevLn - currLn;
                absSum += Math.Abs(diff);
                var frac = absSum != 0 ? (ln - currLn) / absSum : 0;
                var ratio = 1 / Sqrt(j + 1);
                weightSum += frac * ratio;
                denomSum += ratio;
            }

            var rawNdx = denomSum != 0 ? weightSum / denomSum * 100 : 0;
            rawNdxList.Add(rawNdx);
        }

        var ndxList = GetMovingAverageList(stockData, maType, smoothLength, rawNdxList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var ndx = ndxList[i];
            var prevNdx1 = i >= 1 ? ndxList[i - 1] : 0;
            var prevNdx2 = i >= 2 ? ndxList[i - 2] : 0;

            var signal = GetCompareSignal(ndx - prevNdx1, prevNdx1 - prevNdx2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ndx", ndxList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(ndxList);
        stockData.IndicatorName = IndicatorName.NaturalDirectionalIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Natural Market Mirror
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateNaturalMarketMirror(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 40)
    {
        List<double> lnList = new(stockData.Count);
        List<double> oiAvgList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];

            var ln = currentValue > 0 ? Math.Log(currentValue) * 1000 : 0;
            lnList.Add(ln);

            double oiSum = 0;
            for (var j = 1; j <= length; j++)
            {
                var prevLn = i >= j ? lnList[i - j] : 0;
                oiSum += (ln - prevLn) / Sqrt(j) * 100;
            }

            var oiAvg = oiSum / length;
            oiAvgList.Add(oiAvg);
        }

        var nmmList = GetMovingAverageList(stockData, maType, length, oiAvgList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var nmm = nmmList[i];
            var prevNmm1 = i >= 1 ? nmmList[i - 1] : 0;
            var prevNmm2 = i >= 2 ? nmmList[i - 2] : 0;

            var signal = GetCompareSignal(nmm - prevNmm1, prevNmm1 - prevNmm2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Nmm", nmmList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(nmmList);
        stockData.IndicatorName = IndicatorName.NaturalMarketMirror;

        return stockData;
    }


    /// <summary>
    /// Calculates the Natural Market River
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateNaturalMarketRiver(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 40)
    {
        List<double> lnList = new(stockData.Count);
        List<double> oiSumList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];

            var ln = currentValue > 0 ? Math.Log(currentValue) * 1000 : 0;
            lnList.Add(ln);

            double oiSum = 0;
            for (var j = 0; j < length; j++)
            {
                var currentLn = i >= j ? lnList[i - j] : 0;
                var prevLn = i >= j + 1 ? lnList[i - (j + 1)] : 0;

                oiSum += (prevLn - currentLn) * (Sqrt(j) - Sqrt(j + 1));
            }
            oiSumList.Add(oiSum);
        }

        var nmrList = GetMovingAverageList(stockData, maType, length, oiSumList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var nmr = nmrList[i];
            var prevNmr1 = i >= 1 ? nmrList[i - 1] : 0;
            var prevNmr2 = i >= 2 ? nmrList[i - 2] : 0;

            var signal = GetCompareSignal(nmr - prevNmr1, prevNmr1 - prevNmr2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Nmr", nmrList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(nmrList);
        stockData.IndicatorName = IndicatorName.NaturalMarketRiver;

        return stockData;
    }


    /// <summary>
    /// Calculates the Natural Market Combo
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateNaturalMarketCombo(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length = 40, int smoothLength = 20)
    {
        List<double> nmcList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var nmrList = CalculateNaturalMarketRiver(stockData, maType, length).CustomValuesList;
        var nmmList = CalculateNaturalMarketMirror(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var nmr = nmrList[i];
            var nmm = nmmList[i];
            var v3 = Math.Sign(nmm) != Math.Sign(nmr) ? nmm * nmr : ((Math.Abs(nmm) * nmr) + (Math.Abs(nmr) * nmm)) / 2;

            var nmc = Math.Sign(v3) * Sqrt(Math.Abs(v3));
            nmcList.Add(nmc);
        }

        var nmcMaList = GetMovingAverageList(stockData, maType, smoothLength, nmcList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var nmc = nmcMaList[i];
            var prevNmc1 = i >= 1 ? nmcMaList[i - 1] : 0;
            var prevNmc2 = i >= 2 ? nmcMaList[i - 2] : 0;

            var signal = GetCompareSignal(nmc - prevNmc1, prevNmc1 - prevNmc2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Nmc", nmcList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(nmcList);
        stockData.IndicatorName = IndicatorName.NaturalMarketCombo;

        return stockData;
    }


    /// <summary>
    /// Calculates the Natural Market Slope
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateNaturalMarketSlope(this StockData stockData, int length = 40)
    {
        List<double> lnList = new(stockData.Count);
        List<double> nmsList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];

            var ln = currentValue > 0 ? Math.Log(currentValue) * 1000 : 0;
            lnList.Add(ln);
        }

        stockData.SetCustomValues(lnList);
        var linRegList = CalculateLinearRegression(stockData, length).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var linReg = linRegList[i];
            var prevLinReg = i >= 1 ? linRegList[i - 1] : 0;
            var prevNms1 = i >= 1 ? nmsList[i - 1] : 0;
            var prevNms2 = i >= 2 ? nmsList[i - 2] : 0;

            var nms = (linReg - prevLinReg) * Math.Log(length);
            nmsList.Add(nms);

            var signal = GetCompareSignal(nms - prevNms1, prevNms1 - prevNms2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Nms", nmsList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(nmsList);
        stockData.IndicatorName = IndicatorName.NaturalMarketSlope;

        return stockData;
    }


    /// <summary>
    /// Calculates the Narrow Bandpass Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateNarrowBandpassFilter(this StockData stockData, int length = 50)
    {
        List<double> sumList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevSum1 = i >= 1 ? sumList[i - 1] : 0;
            var prevSum2 = i >= 2 ? sumList[i - 2] : 0;

            double sum = 0;
            for (var j = 0; j <= length - 1; j++)
            {
                var prevValue = i >= j ? inputList[i - j] : 0;
                var x = j / (double)(length - 1);
                var win = 0.42 - (0.5 * Math.Cos(2 * Math.PI * x)) + (0.08 * Math.Cos(4 * Math.PI * x));
                var w = Math.Sin(2 * Math.PI * j / length) * win;
                sum += prevValue * w;
            }
            sumList.Add(sum);

            var signal = GetCompareSignal(sum - prevSum1, prevSum1 - prevSum2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Nbpf", sumList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(sumList);
        stockData.IndicatorName = IndicatorName.NarrowBandpassFilter;

        return stockData;
    }


    /// <summary>
    /// Calculates the Nth Order Differencing Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="lbLength"></param>
    /// <returns></returns>
    public static StockData CalculateNthOrderDifferencingOscillator(this StockData stockData, int length = 14, int lbLength = 2)
    {
        List<double> nodoList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];

            double sum = 0, w = 1;
            for (var j = 0; j <= lbLength; j++)
            {
                var prevValue = i >= length * (j + 1) ? inputList[i - (length * (j + 1))] : 0;
                double x = Math.Sign(((j + 1) % 2) - 0.5);
                w *= (lbLength - j) / (double)(j + 1);
                sum += prevValue * w * x;
            }

            var prevNodo = GetLastOrDefault(nodoList);
            var nodo = currentValue - sum;
            nodoList.Add(nodo);

            var signal = GetCompareSignal(nodo, prevNodo);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Nodo", nodoList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(nodoList);
        stockData.IndicatorName = IndicatorName.NthOrderDifferencingOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Normalized Relative Vigor Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateNormalizedRelativeVigorIndex(this StockData stockData,
        MovingAvgType maType = MovingAvgType.SymmetricallyWeightedMovingAverage, int length = 10)
    {
        List<double> closeOpenList = new(stockData.Count);
        List<double> highLowList = new(stockData.Count);
        List<double> swmaCloseOpenSumList = new(stockData.Count);
        List<double> swmaHighLowSumList = new(stockData.Count);
        List<double> rvgiList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, openList, _) = GetInputValuesList(stockData);
        var swmaCloseOpenSumWindow = new RollingSum();
        var swmaHighLowSumWindow = new RollingSum();

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentClose = inputList[i];
            var currentOpen = openList[i];
            var currentHigh = highList[i];
            var currentLow = lowList[i];

            var closeOpen = currentClose - currentOpen;
            closeOpenList.Add(closeOpen);

            var highLow = currentHigh - currentLow;
            highLowList.Add(highLow);
        }

        var swmaCloseOpenList = GetMovingAverageList(stockData, maType, length, closeOpenList);
        var swmaHighLowList = GetMovingAverageList(stockData, maType, length, highLowList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var swmaCloseOpen = swmaCloseOpenList[i];
            swmaCloseOpenSumWindow.Add(swmaCloseOpen);
            var closeOpenSum = swmaCloseOpenSumWindow.Sum(length);
            swmaCloseOpenSumList.Add(closeOpenSum);

            var swmaHighLow = swmaHighLowList[i];
            swmaHighLowSumWindow.Add(swmaHighLow);
            var highLowSum = swmaHighLowSumWindow.Sum(length);
            swmaHighLowSumList.Add(highLowSum);

            var rvgi = highLowSum != 0 ? closeOpenSum / highLowSum * 100 : 0;
            rvgiList.Add(rvgi);
        }

        var rvgiSignalList = GetMovingAverageList(stockData, maType, length, rvgiList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var rvgi = rvgiList[i];
            var rvgiSig = rvgiSignalList[i];
            var prevRvgi = i >= 1 ? rvgiList[i - 1] : 0;
            var prevRvgiSig = i >= 1 ? rvgiSignalList[i - 1] : 0;

            var signal = GetCompareSignal(rvgi - rvgiSig, prevRvgi - prevRvgiSig);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Nrvi", rvgiList },
            { "Signal", rvgiSignalList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(rvgiList);
        stockData.IndicatorName = IndicatorName.NormalizedRelativeVigorIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Math.PIvot Detector Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculatePivotDetectorOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length1 = 200, int length2 = 14)
    {
        List<double> pdoList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var rsiList = CalculateRelativeStrengthIndex(stockData, maType, length2).CustomValuesList;
        var smaList = GetMovingAverageList(stockData, maType, length1, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var sma = smaList[i];
            var rsi = rsiList[i];
            var prevPdo1 = i >= 1 ? pdoList[i - 1] : 0;
            var prevPdo2 = i >= 2 ? pdoList[i - 2] : 0;

            var pdo = currentValue > sma ? (rsi - 35) / (85 - 35) * 100 : currentValue <= sma ? (rsi - 20) / (70 - 20) * 100 : 0;
            pdoList.Add(pdo);

            var signal = GetCompareSignal(pdo - prevPdo1, prevPdo1 - prevPdo2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Pdo", pdoList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(pdoList);
        stockData.IndicatorName = IndicatorName.PivotDetectorOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Percent Change Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculatePercentChangeOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length = 14)
    {
        List<double> percentChangeList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevPcc = GetLastOrDefault(percentChangeList);
            var pcc = prevValue - 1 != 0 ? prevPcc + (currentValue / (prevValue - 1)) : 0;
            percentChangeList.Add(pcc);
        }

        var pctChgWmaList = GetMovingAverageList(stockData, maType, length, percentChangeList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var pcc = percentChangeList[i];
            var pccWma = pctChgWmaList[i];
            var prevPcc = i >= 1 ? percentChangeList[i - 1] : 0;
            var prevPccWma = i >= 1 ? pctChgWmaList[i - 1] : 0;

            var signal = GetCompareSignal(pcc - pccWma, prevPcc - prevPccWma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Pcco", percentChangeList },
            { "Signal", pctChgWmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(percentChangeList);
        stockData.IndicatorName = IndicatorName.PercentChangeOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Prime Number Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculatePrimeNumberOscillator(this StockData stockData, int length = 5)
    {
        List<double> pnoList = new(stockData.Count);
        List<double> pno1List = new(stockData.Count);
        List<double> pno2List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var ratio = currentValue * length / 100;
            var convertedValue = (long)Math.Round(currentValue);
            var sqrtValue = currentValue >= 0 ? (long)Math.Round(Sqrt(currentValue)) : 0;
            var maxValue = (long)Math.Round(currentValue + ratio);
            var minValue = (long)Math.Round(currentValue - ratio);

            double pno1 = 0, pno2 = 0;
            for (var j = convertedValue; j <= maxValue; j++)
            {
                pno1 = j;
                for (var k = 2; k <= sqrtValue; k++)
                {
                    pno1 = j % k == 0 ? 0 : j;
                    if (pno1 == 0)
                    {
                        break;
                    }
                }

                if (pno1 > 0)
                {
                    break;
                }
            }
            pno1 = pno1 == 0 ? GetLastOrDefault(pno1List) : pno1;
            pno1List.Add(pno1);

            for (var l = convertedValue; l >= minValue; l--)
            {
                pno2 = l;
                for (var m = 2; m <= sqrtValue; m++)
                {
                    pno2 = l % m == 0 ? 0 : l;
                    if (pno2 == 0)
                    {
                        break;
                    }
                }

                if (pno2 > 0)
                {
                    break;
                }
            }
            pno2 = pno2 == 0 ? GetLastOrDefault(pno2List) : pno2;
            pno2List.Add(pno2);

            var prevPno = GetLastOrDefault(pnoList);
            var pno = pno1 - currentValue < currentValue - pno2 ? pno1 - currentValue : pno2 - currentValue;
            pno = pno == 0 ? prevPno : pno;
            pnoList.Add(pno);

            var signal = GetCompareSignal(pno, prevPno);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Pno", pnoList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(pnoList);
        stockData.IndicatorName = IndicatorName.PrimeNumberOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Pring Special K
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="length4"></param>
    /// <param name="length5"></param>
    /// <param name="length6"></param>
    /// <param name="length7"></param>
    /// <param name="length8"></param>
    /// <param name="length9"></param>
    /// <param name="length10"></param>
    /// <param name="length11"></param>
    /// <param name="length12"></param>
    /// <param name="length13"></param>
    /// <param name="length14"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculatePringSpecialK(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 10,
        int length2 = 15, int length3 = 20, int length4 = 30, int length5 = 40, int length6 = 50, int length7 = 65, int length8 = 75, int length9 = 100,
        int length10 = 130, int length11 = 195, int length12 = 265, int length13 = 390, int length14 = 530, int smoothLength = 10)
    {
        List<double> specialKList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var rocList = CalculateRateOfChange(stockData, length1).CustomValuesList;
        var roc15List = CalculateRateOfChange(stockData, length2).CustomValuesList;
        var roc20List = CalculateRateOfChange(stockData, length3).CustomValuesList;
        var roc30List = CalculateRateOfChange(stockData, length4).CustomValuesList;
        var roc40List = CalculateRateOfChange(stockData, length5).CustomValuesList;
        var roc65List = CalculateRateOfChange(stockData, length7).CustomValuesList;
        var roc75List = CalculateRateOfChange(stockData, length8).CustomValuesList;
        var roc100List = CalculateRateOfChange(stockData, length9).CustomValuesList;
        var roc195List = CalculateRateOfChange(stockData, length11).CustomValuesList;
        var roc265List = CalculateRateOfChange(stockData, length12).CustomValuesList;
        var roc390List = CalculateRateOfChange(stockData, length13).CustomValuesList;
        var roc530List = CalculateRateOfChange(stockData, length14).CustomValuesList;
        var roc10SmaList = GetMovingAverageList(stockData, maType, length1, rocList);
        var roc15SmaList = GetMovingAverageList(stockData, maType, length1, roc15List);
        var roc20SmaList = GetMovingAverageList(stockData, maType, length1, roc20List);
        var roc30SmaList = GetMovingAverageList(stockData, maType, length2, roc30List);
        var roc40SmaList = GetMovingAverageList(stockData, maType, length6, roc40List);
        var roc65SmaList = GetMovingAverageList(stockData, maType, length7, roc65List);
        var roc75SmaList = GetMovingAverageList(stockData, maType, length8, roc75List);
        var roc100SmaList = GetMovingAverageList(stockData, maType, length9, roc100List);
        var roc195SmaList = GetMovingAverageList(stockData, maType, length10, roc195List);
        var roc265SmaList = GetMovingAverageList(stockData, maType, length10, roc265List);
        var roc390SmaList = GetMovingAverageList(stockData, maType, length10, roc390List);
        var roc530SmaList = GetMovingAverageList(stockData, maType, length11, roc530List);

        for (var i = 0; i < stockData.Count; i++)
        {
            var roc10Sma = roc10SmaList[i];
            var roc15Sma = roc15SmaList[i];
            var roc20Sma = roc20SmaList[i];
            var roc30Sma = roc30SmaList[i];
            var roc40Sma = roc40SmaList[i];
            var roc65Sma = roc65SmaList[i];
            var roc75Sma = roc75SmaList[i];
            var roc100Sma = roc100SmaList[i];
            var roc195Sma = roc195SmaList[i];
            var roc265Sma = roc265SmaList[i];
            var roc390Sma = roc390SmaList[i];
            var roc530Sma = roc530SmaList[i];

            var specialK = (roc10Sma * 1) + (roc15Sma * 2) + (roc20Sma * 3) + (roc30Sma * 4) + (roc40Sma * 1) + (roc65Sma * 2) + (roc75Sma * 3) +
                           (roc100Sma * 4) + (roc195Sma * 1) + (roc265Sma * 2) + (roc390Sma * 3) + (roc530Sma * 4);
            specialKList.Add(specialK);
        }

        var specialKSignalList = GetMovingAverageList(stockData, maType, smoothLength, specialKList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var specialK = specialKList[i];
            var specialKSignal = specialKSignalList[i];
            var prevSpecialK = i >= 1 ? specialKList[i - 1] : 0;
            var prevSpecialKSignal = i >= 1 ? specialKSignalList[i - 1] : 0;

            var signal = GetCompareSignal(specialK - specialKSignal, prevSpecialK - prevSpecialKSignal);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "PringSpecialK", specialKList },
            { "Signal", specialKSignalList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(specialKList);
        stockData.IndicatorName = IndicatorName.PringSpecialK;

        return stockData;
    }


    /// <summary>
    /// Calculates the Price Zone Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculatePriceZoneOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length = 20)
    {
        List<double> pzoList = new(stockData.Count);
        List<double> dvolList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var emaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var dvol = Math.Sign(MinPastValues(i, 1, currentValue - prevValue)) * currentValue;
            dvolList.Add(dvol);
        }

        var dvmaList = GetMovingAverageList(stockData, maType, length, dvolList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var vma = emaList[i];
            var dvma = dvmaList[i];
            var prevPzo1 = i >= 1 ? pzoList[i - 1] : 0;
            var prevPzo2 = i >= 2 ? pzoList[i - 2] : 0;

            var pzo = vma != 0 ? MinOrMax(100 * dvma / vma, 100, -100) : 0;
            pzoList.Add(pzo);

            var signal = GetRsiSignal(pzo - prevPzo1, prevPzo1 - prevPzo2, pzo, prevPzo1, 40, -40);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Pzo", pzoList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(pzoList);
        stockData.IndicatorName = IndicatorName.PriceZoneOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Performance Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculatePerformanceIndex(this StockData stockData, int length = 14)
    {
        List<double> kpiList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= length ? inputList[i - length] : 0;

            var prevKpi = GetLastOrDefault(kpiList);
            var kpi = prevValue != 0 ? MinPastValues(i, length, currentValue - prevValue) * 100 / prevValue : 0;
            kpiList.Add(kpi);

            var signal = GetCompareSignal(kpi, prevKpi);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Math.PI", kpiList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(kpiList);
        stockData.IndicatorName = IndicatorName.PerformanceIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Polarized Fractal Efficiency
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculatePolarizedFractalEfficiency(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 9, int smoothLength = 5)
    {
        List<double> fracEffList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var c2cSumWindow = new RollingSum();

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var priorValue = i >= length ? inputList[i - length] : 0;
            var pfe = Sqrt(Pow(MinPastValues(i, length, currentValue - priorValue), 2) + 100);

            var c2c = Sqrt(Pow(MinPastValues(i, 1, currentValue - prevValue), 2) + 1);
            c2cSumWindow.Add(c2c);

            var c2cSum = c2cSumWindow.Sum(length);
            var efRatio = c2cSum != 0 ? pfe / c2cSum * 100 : 0;

            var fracEff = i >= length && currentValue - priorValue > 0 ? efRatio : -efRatio;
            fracEffList.Add(fracEff);
        }

        var emaList = GetMovingAverageList(stockData, maType, smoothLength, fracEffList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var ema = emaList[i];
            var prevEma = i >= 1 ? emaList[i - 1] : 0;

            var signal = GetCompareSignal(ema, prevEma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Pfe", emaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(emaList);
        stockData.IndicatorName = IndicatorName.PolarizedFractalEfficiency;

        return stockData;
    }


    /// <summary>
    /// Calculates the Pretty Good Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculatePrettyGoodOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 14)
    {
        List<double> pgoList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);
        var atrList = CalculateAverageTrueRange(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var sma = smaList[i];
            var atr = atrList[i];

            var prevPgo = GetLastOrDefault(pgoList);
            var pgo = atr != 0 ? (currentValue - sma) / atr : 0;
            pgoList.Add(pgo);

            var signal = GetCompareSignal(pgo, prevPgo);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Pgo", pgoList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(pgoList);
        stockData.IndicatorName = IndicatorName.PrettyGoodOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Price Cycle Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculatePriceCycleOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 22)
    {
        List<double> pcoList = new(stockData.Count);
        List<double> diffList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, lowList, _, _) = GetInputValuesList(stockData);

        var atrList = CalculateAverageTrueRange(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentClose = inputList[i];
            var currentLow = lowList[i];
            
            var diff = currentClose - currentLow;
            diffList.Add(diff);
        }

        var diffSmaList = GetMovingAverageList(stockData, maType, length, diffList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentAtr = atrList[i];
            var prevPco1 = i >= 1 ? pcoList[i - 1] : 0;
            var prevPco2 = i >= 2 ? pcoList[i - 2] : 0;
            var diffSma = diffSmaList[i];

            var pco = currentAtr != 0 ? diffSma / currentAtr * 100 : 0;
            pcoList.Add(pco);

            var signal = GetCompareSignal(pco - prevPco1, prevPco1 - prevPco2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Pco", pcoList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(pcoList);
        stockData.IndicatorName = IndicatorName.PriceCycleOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Phase Change Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculatePhaseChangeIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 35, int smoothLength = 3)
    {
        List<double> pciList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= length ? inputList[i - length] : 0;
            var mom = MinPastValues(i, length, currentValue - prevValue);

            double positiveSum = 0, negativeSum = 0;
            for (var j = 0; j <= length - 1; j++)
            {
                var prevValue2 = i >= length - j ? inputList[i - (length - j)] : 0;
                var gradient = prevValue + (mom * (length - j) / (length - 1));
                var deviation = prevValue2 - gradient;
                positiveSum = deviation > 0 ? positiveSum + deviation : positiveSum + 0;
                negativeSum = deviation < 0 ? negativeSum - deviation : negativeSum + 0;
            }
            var sum = positiveSum + negativeSum;

            var pci = sum != 0 ? MinOrMax(100 * positiveSum / sum, 100, 0) : 0;
            pciList.Add(pci);
        }

        var pciSmoothedList = GetMovingAverageList(stockData, maType, smoothLength, pciList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var pciSmoothed = pciSmoothedList[i];
            var prevPciSmoothed1 = i >= 1 ? pciSmoothedList[i - 1] : 0;
            var prevPciSmoothed2 = i >= 2 ? pciSmoothedList[i - 2] : 0;

            var signal = GetRsiSignal(pciSmoothed - prevPciSmoothed1, prevPciSmoothed1 - prevPciSmoothed2, pciSmoothed, prevPciSmoothed1, 80, 20);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Pci", pciList },
            { "Signal", pciSmoothedList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(pciList);
        stockData.IndicatorName = IndicatorName.PhaseChangeIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Peak Valley Estimation
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculatePeakValleyEstimation(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 500, int smoothLength = 100)
    {
        List<double> sign1List = new(stockData.Count);
        List<double> sign2List = new(stockData.Count);
        List<double> sign3List = new(stockData.Count);
        List<double> absOsList = new(stockData.Count);
        List<double> osList = new(stockData.Count);
        List<double> hList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var sma = smaList[i];

            var os = currentValue - sma;
            osList.Add(os);

            var absOs = Math.Abs(os);
            absOsList.Add(absOs);
        }

        stockData.SetCustomValues(absOsList);
        var pList = CalculateLinearRegression(stockData, smoothLength).CustomValuesList;
        var (highestList, _) = GetMaxAndMinValuesList(pList, length);
        for (var i = 0; i < stockData.Count; i++)
        {
            var os = osList[i];
            var p = pList[i];
            var highest = highestList[i];

            var prevH = i >= 1 ? hList[i - 1] : 0;
            var h = highest != 0 ? p / highest : 0;
            hList.Add(h);

            double mod1 = h == 1 && prevH != 1 ? 1 : 0;
            double mod2 = h < 0.8 ? 1 : 0;
            double mod3 = prevH == 1 && h < prevH ? 1 : 0;

            double sign1 = mod1 == 1 && os < 0 ? 1 : mod1 == 1 && os > 0 ? -1 : 0;
            sign1List.Add(sign1);

            double sign2 = mod2 == 1 && os < 0 ? 1 : mod2 == 1 && os > 0 ? -1 : 0;
            sign2List.Add(sign2);

            double sign3 = mod3 == 1 && os < 0 ? 1 : mod3 == 1 && os > 0 ? -1 : 0;
            sign3List.Add(sign3);

            var signal = GetConditionSignal(sign1 > 0, sign1 < 0);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Sign1", sign1List },
            { "Sign2", sign2List },
            { "Sign3", sign3List }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(sign1List);
        stockData.IndicatorName = IndicatorName.PeakValleyEstimation;

        return stockData;
    }


    /// <summary>
    /// Calculates the Psychological Line
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculatePsychologicalLine(this StockData stockData, int length = 20)
    {
        List<double> psyList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var condSumWindow = new RollingSum();

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevPsy1 = i >= 1 ? psyList[i - 1] : 0;
            var prevPsy2 = i >= 2 ? psyList[i - 2] : 0;

            double cond = currentValue > prevValue ? 1 : 0;
            condSumWindow.Add(cond);

            var condSum = condSumWindow.Sum(length);
            var psy = length != 0 ? condSum / length * 100 : 0;
            psyList.Add(psy);

            var signal = GetCompareSignal(psy - prevPsy1, prevPsy1 - prevPsy2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Pl", psyList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(psyList);
        stockData.IndicatorName = IndicatorName.PsychologicalLine;

        return stockData;
    }


    /// <summary>
    /// Calculates the Rahul Mohindar Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="length4"></param>
    /// <returns></returns>
    public static StockData CalculateRahulMohindarOscillator(this StockData stockData, int length1 = 2, int length2 = 10, int length3 = 30, 
        int length4 = 81)
    {
        List<double> swingTrd1List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(inputList, length2);

        var r1List = GetMovingAverageList(stockData, MovingAvgType.SimpleMovingAverage, length1, inputList);
        var r2List = GetMovingAverageList(stockData, MovingAvgType.SimpleMovingAverage, length1, r1List); //-V3056
        var r3List = GetMovingAverageList(stockData, MovingAvgType.SimpleMovingAverage, length1, r2List);
        var r4List = GetMovingAverageList(stockData, MovingAvgType.SimpleMovingAverage, length1, r3List);
        var r5List = GetMovingAverageList(stockData, MovingAvgType.SimpleMovingAverage, length1, r4List);
        var r6List = GetMovingAverageList(stockData, MovingAvgType.SimpleMovingAverage, length1, r5List);
        var r7List = GetMovingAverageList(stockData, MovingAvgType.SimpleMovingAverage, length1, r6List);
        var r8List = GetMovingAverageList(stockData, MovingAvgType.SimpleMovingAverage, length1, r7List);
        var r9List = GetMovingAverageList(stockData, MovingAvgType.SimpleMovingAverage, length1, r8List);
        var r10List = GetMovingAverageList(stockData, MovingAvgType.SimpleMovingAverage, length1, r9List);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var highest = highestList[i];
            var lowest = lowestList[i];
            var r1 = r1List[i];
            var r2 = r2List[i];
            var r3 = r3List[i];
            var r4 = r4List[i];
            var r5 = r5List[i];
            var r6 = r6List[i];
            var r7 = r7List[i];
            var r8 = r8List[i];
            var r9 = r9List[i];
            var r10 = r10List[i];

            var swingTrd1 = highest - lowest != 0 ? 100 * (currentValue - ((r1 + r2 + r3 + r4 + r5 + r6 + r7 + r8 + r9 + r10) / 10)) / 
                                                    (highest - lowest) : 0;
            swingTrd1List.Add(swingTrd1);
        }

        var swingTrd2List = GetMovingAverageList(stockData, MovingAvgType.ExponentialMovingAverage, length3, swingTrd1List);
        var swingTrd3List = GetMovingAverageList(stockData, MovingAvgType.ExponentialMovingAverage, length3, swingTrd2List);
        var rmoList = GetMovingAverageList(stockData, MovingAvgType.ExponentialMovingAverage, length4, swingTrd1List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var rmo = rmoList[i];
            var prevRmo = i >= 1 ? rmoList[i - 1] : 0;

            var signal = GetCompareSignal(rmo, prevRmo);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Rmo", rmoList },
            { "SwingTrade1", swingTrd1List },
            { "SwingTrade2", swingTrd2List },
            { "SwingTrade3", swingTrd3List }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(rmoList);
        stockData.IndicatorName = IndicatorName.RahulMohindarOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Rainbow Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculateRainbowOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length1 = 2, int length2 = 10)
    {
        List<double> rainbowOscillatorList = new(stockData.Count);
        List<double> upperBandList = new(stockData.Count);
        List<double> lowerBandList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(inputList, length2);

        var r1List = GetMovingAverageList(stockData, maType, length1, inputList);
        var r2List = GetMovingAverageList(stockData, maType, length1, r1List); //-V3056
        var r3List = GetMovingAverageList(stockData, maType, length1, r2List);
        var r4List = GetMovingAverageList(stockData, maType, length1, r3List);
        var r5List = GetMovingAverageList(stockData, maType, length1, r4List);
        var r6List = GetMovingAverageList(stockData, maType, length1, r5List);
        var r7List = GetMovingAverageList(stockData, maType, length1, r6List);
        var r8List = GetMovingAverageList(stockData, maType, length1, r7List);
        var r9List = GetMovingAverageList(stockData, maType, length1, r8List);
        var r10List = GetMovingAverageList(stockData, maType, length1, r9List);

        for (var i = 0; i < stockData.Count; i++)
        {
            var highest = highestList[i];
            var lowest = lowestList[i];
            var currentValue = inputList[i];
            var r1 = r1List[i];
            var r2 = r2List[i];
            var r3 = r3List[i];
            var r4 = r4List[i];
            var r5 = r5List[i];
            var r6 = r6List[i];
            var r7 = r7List[i];
            var r8 = r8List[i];
            var r9 = r9List[i];
            var r10 = r10List[i];
            var highestRainbow = Math.Max(r1, Math.Max(r2, Math.Max(r3, Math.Max(r4, Math.Max(r5, Math.Max(r6, Math.Max(r7, Math.Max(r8, 
                Math.Max(r9, r10)))))))));
            var lowestRainbow = Math.Min(r1, Math.Min(r2, Math.Min(r3, Math.Min(r4, Math.Min(r5, Math.Min(r6, Math.Min(r7, Math.Min(r8, 
                Math.Min(r9, r10)))))))));

            var prevRainbowOscillator = GetLastOrDefault(rainbowOscillatorList);
            var rainbowOscillator = highest - lowest != 0 ? 100 * ((currentValue - ((r1 + r2 + r3 + r4 + r5 + r6 + r7 + r8 + r9 + r10) / 10)) / 
                                                                   (highest - lowest)) : 0;
            rainbowOscillatorList.Add(rainbowOscillator);

            var upperBand = highest - lowest != 0 ? 100 * ((highestRainbow - lowestRainbow) / (highest - lowest)) : 0;
            upperBandList.Add(upperBand);

            var lowerBand = -upperBand;
            lowerBandList.Add(lowerBand);

            var signal = GetCompareSignal(rainbowOscillator, prevRainbowOscillator);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ro", rainbowOscillatorList },
            { "UpperBand", upperBandList },
            { "LowerBand", lowerBandList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(rainbowOscillatorList);
        stockData.IndicatorName = IndicatorName.RainbowOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Random Walk Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateRandomWalkIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, 
        int length = 14)
    {
        List<double> rwiLowList = new(stockData.Count);
        List<double> rwiHighList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (_, highList, lowList, _, _) = GetInputValuesList(stockData);

        var atrList = CalculateAverageTrueRange(stockData, maType, length).CustomValuesList;
        var sqrt = Sqrt(length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentAtr = atrList[i];
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var prevHigh = i >= length ? highList[i - length] : 0;
            var prevLow = i >= length ? lowList[i - length] : 0;
            var bottom = currentAtr * sqrt;

            var prevRwiLow = GetLastOrDefault(rwiLowList);
            var rwiLow = bottom != 0 ? (prevHigh - currentLow) / bottom : 0;
            rwiLowList.Add(rwiLow);

            var prevRwiHigh = GetLastOrDefault(rwiHighList);
            var rwiHigh = bottom != 0 ? (currentHigh - prevLow) / bottom : 0;
            rwiHighList.Add(rwiHigh);

            var signal = GetCompareSignal(rwiHigh - rwiLow, prevRwiHigh - prevRwiLow);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "RwiHigh", rwiHighList },
            { "RwiLow", rwiLowList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.RandomWalkIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Range Action Verification Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <returns></returns>
    public static StockData CalculateRangeActionVerificationIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int fastLength = 7, int slowLength = 65)
    {
        List<double> raviList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaFastList = GetMovingAverageList(stockData, maType, fastLength, inputList);
        var smaSlowList = GetMovingAverageList(stockData, maType, slowLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var fastMA = smaFastList[i];
            var slowMA = smaSlowList[i];
            var prevRavi1 = i >= 1 ? raviList[i - 1] : 0;
            var prevRavi2 = i >= 2 ? raviList[i - 2] : 0;

            var ravi = slowMA != 0 ? (fastMA - slowMA) / slowMA * 100 : 0;
            raviList.Add(ravi);

            var signal = GetCompareSignal(ravi - prevRavi1, prevRavi1 - prevRavi2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ravi", raviList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(raviList);
        stockData.IndicatorName = IndicatorName.RangeActionVerificationIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Really Simple Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateReallySimpleIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length = 21, int smoothLength = 10)
    {
        List<double> rsiList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, lowList, _, _) = GetInputValuesList(stockData);

        var maList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentLow = lowList[i];
            var currentMa = maList[i];

            var rsi = currentValue != 0 ? (currentLow - currentMa) / currentValue * 100 : 0;
            rsiList.Add(rsi);
        }

        var rsiMaList = GetMovingAverageList(stockData, maType, smoothLength, rsiList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var rsi = rsiMaList[i];
            var prevRsiMa = i >= 1 ? rsiMaList[i - 1] : 0;
            var prevRsi = i >= 1 ? rsiList[i - 1] : 0;
            var rsiMa = rsiMaList[i];

            var signal = GetCompareSignal(rsi - rsiMa, prevRsi - prevRsiMa);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Rsi", rsiList },
            { "Signal", rsiMaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(rsiList);
        stockData.IndicatorName = IndicatorName.ReallySimpleIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Recursive Differenciator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="alpha"></param>
    /// <returns></returns>
    public static StockData CalculateRecursiveDifferenciator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length = 14, double alpha = 0.6)
    {
        List<double> bList = new(stockData.Count);
        List<double> bChgList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var emaList = GetMovingAverageList(stockData, maType, length, inputList);
        stockData.SetCustomValues(emaList);
        var rsiList = CalculateRelativeStrengthIndex(stockData, length: length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var rsi = rsiList[i];
            var priorB = i >= length ? bList[i - length] : 0;
            var a = rsi / 100;
            var prevBChg1 = i >= 1 ? bChgList[i - 1] : a;
            var prevBChg2 = i >= 2 ? bChgList[i - 2] : 0;

            var b = (alpha * a) + ((1 - alpha) * prevBChg1);
            bList.Add(b);

            var bChg = b - priorB;
            bChgList.Add(bChg);

            var signal = GetCompareSignal(bChg - prevBChg1, prevBChg1 - prevBChg2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Rd", bList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(bList);
        stockData.IndicatorName = IndicatorName.RecursiveDifferenciator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Regression Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateRegressionOscillator(this StockData stockData, int length = 63)
    {
        List<double> roscList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var linRegList = CalculateLinearRegression(stockData, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentLinReg = linRegList[i];

            var prevRosc = GetLastOrDefault(roscList);
            var rosc = currentLinReg != 0 ? 100 * ((currentValue / currentLinReg) - 1) : 0;
            roscList.Add(rosc);

            var signal = GetCompareSignal(rosc, prevRosc);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Rosc", roscList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(roscList);
        stockData.IndicatorName = IndicatorName.RegressionOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Relative Difference Of Squares Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateRelativeDifferenceOfSquaresOscillator(this StockData stockData, int length = 20)
    {
        List<double> rdosList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var aSumWindow = new RollingSum();
        var dSumWindow = new RollingSum();
        var nSumWindow = new RollingSum();

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            double a = currentValue > prevValue ? 1 : 0;
            aSumWindow.Add(a);

            double d = currentValue < prevValue ? 1 : 0;
            dSumWindow.Add(d);

            double n = currentValue == prevValue ? 1 : 0;
            nSumWindow.Add(n);

            var prevRdos = GetLastOrDefault(rdosList);
            var aSum = aSumWindow.Sum(length);
            var dSum = dSumWindow.Sum(length);
            var nSum = nSumWindow.Sum(length);
            var rdos = aSum > 0 || dSum > 0 || nSum > 0 ? (Pow(aSum, 2) - Pow(dSum, 2)) / Pow(aSum + nSum + dSum, 2) : 0;
            rdosList.Add(rdos);

            var signal = GetCompareSignal(rdos, prevRdos);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Rdos", rdosList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(rdosList);
        stockData.IndicatorName = IndicatorName.RelativeDifferenceOfSquaresOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Relative Spread Strength
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateRelativeSpreadStrength(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int fastLength = 10, int slowLength = 40, int length = 14, int smoothLength = 5)
    {
        List<double> spreadList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var fastEmaList = GetMovingAverageList(stockData, maType, fastLength, inputList);
        var slowEmaList = GetMovingAverageList(stockData, maType, slowLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var fastEma = fastEmaList[i];
            var slowEma = slowEmaList[i];

            var spread = fastEma - slowEma;
            spreadList.Add(spread);
        }

        stockData.SetCustomValues(spreadList);
        var rsList = CalculateRelativeStrengthIndex(stockData, length: length).CustomValuesList;
        var rssList = GetMovingAverageList(stockData, maType, smoothLength, rsList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var rss = rssList[i];
            var prevRss1 = i >= 1 ? rssList[i - 1] : 0;
            var prevRss2 = i >= 2 ? rssList[i - 2] : 0;

            var signal = GetRsiSignal(rss - prevRss1, prevRss1 - prevRss2, rss, prevRss1, 80, 20);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Rss", rssList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(rssList);
        stockData.IndicatorName = IndicatorName.RelativeSpreadStrength;

        return stockData;
    }


    /// <summary>
    /// Calculates the Relative Strength 3D Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="marketData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="length4"></param>
    /// <param name="length5"></param>
    /// <returns></returns>
    public static StockData CalculateRelativeStrength3DIndicator(this StockData stockData,
            StockData marketData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 4, int length2 = 7, int length3 = 10,
            int length4 = 15, int length5 = 30)
    {
        List<double> r1List = new(stockData.Count);
        List<double> rs3List = new(stockData.Count);
        List<double> rs2List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var (spInputList, _, _, _, _) = GetInputValuesList(marketData);
        var xSumWindow = new RollingSum();

        if (stockData.Count == marketData.Count)
        {
            for (var i = 0; i < stockData.Count; i++)
            {
                var currentValue = inputList[i];
                var currentSp = spInputList[i];

                var prevR1 = GetLastOrDefault(r1List);
                var r1 = currentSp != 0 ? currentValue / currentSp * 100 : prevR1;
                r1List.Add(r1);
            }

            var fastMaList = GetMovingAverageList(stockData, maType, length3, r1List);
            var medMaList = GetMovingAverageList(stockData, maType, length2, fastMaList);
            var slowMaList = GetMovingAverageList(stockData, maType, length4, fastMaList);
            var vSlowMaList = GetMovingAverageList(stockData, maType, length5, slowMaList);
            for (var i = 0; i < stockData.Count; i++)
            {
                var fastMa = fastMaList[i];
                var medMa = medMaList[i];
                var slowMa = slowMaList[i];
                var vSlowMa = vSlowMaList[i];
                double t1 = fastMa >= medMa && medMa >= slowMa && slowMa >= vSlowMa ? 10 : 0;
                double t2 = fastMa >= medMa && medMa >= slowMa && slowMa < vSlowMa ? 9 : 0;
                double t3 = fastMa < medMa && medMa >= slowMa && slowMa >= vSlowMa ? 9 : 0;
                double t4 = fastMa < medMa && medMa >= slowMa && slowMa < vSlowMa ? 5 : 0;

                var rs2 = t1 + t2 + t3 + t4;
                rs2List.Add(rs2);
            }

            var rs2MaList = GetMovingAverageList(stockData, maType, length1, rs2List);
            for (var i = 0; i < stockData.Count; i++)
            {
                var rs2 = rs2List[i];
                var rs2Ma = rs2MaList[i];
                var prevRs3_1 = i >= 1 ? rs3List[i - 1] : 0;
                var prevRs3_2 = i >= 2 ? rs3List[i - 1] : 0;

                double x = rs2 >= 5 ? 1 : 0;
                xSumWindow.Add(x);

                var rs3 = rs2 >= 5 || rs2 > rs2Ma ? xSumWindow.Sum(length4) / length4 * 100 : 0;
                rs3List.Add(rs3);

                var signal = GetCompareSignal(rs3 - prevRs3_1, prevRs3_1 - prevRs3_2);
                signalsList?.Add(signal);
            }
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Rs3d", rs3List }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(rs3List);
        stockData.IndicatorName = IndicatorName.RelativeStrength3DIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Relative Vigor Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateRelativeVigorIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 14)
    {
        List<double> rviList = new(stockData.Count);
        List<double> numeratorList = new(stockData.Count);
        List<double> denominatorList = new(stockData.Count);
        List<double> signalLineList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, openList, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentClose = inputList[i];
            var currentOpen = openList[i];
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var prevOpen1 = i >= 1 ? openList[i - 1] : 0;
            var prevClose1 = i >= 1 ? inputList[i - 1] : 0;
            var prevHigh1 = i >= 1 ? highList[i - 1] : 0;
            var prevOpen2 = i >= 2 ? openList[i - 2] : 0;
            var prevClose2 = i >= 2 ? inputList[i - 2] : 0;
            var prevHigh2 = i >= 2 ? highList[i - 2] : 0;
            var prevOpen3 = i >= 3 ? openList[i - 3] : 0;
            var prevClose3 = i >= 3 ? inputList[i - 3] : 0;
            var prevHigh3 = i >= 3 ? highList[i - 3] : 0;
            var a = currentClose - currentOpen;
            var b = prevClose1 - prevOpen1;
            var c = prevClose2 - prevOpen2;
            var d = prevClose3 - prevOpen3;
            var e = currentHigh - currentLow;
            var f = prevHigh1 - prevOpen1;
            var g = prevHigh2 - prevOpen2;
            var h = prevHigh3 - prevOpen3;

            var numerator = (a + (2 * b) + (2 * c) + d) / 6;
            numeratorList.Add(numerator);

            var denominator = (e + (2 * f) + (2 * g) + h) / 6;
            denominatorList.Add(denominator);
        }

        var numeratorAvgList = GetMovingAverageList(stockData, maType, length, numeratorList);
        var denominatorAvgList = GetMovingAverageList(stockData, maType, length, denominatorList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var numeratorAvg = numeratorAvgList[i];
            var denominatorAvg = denominatorAvgList[i];
            var k = i >= 1 ? rviList[i - 1] : 0;
            var l = i >= 2 ? rviList[i - 2] : 0;
            var m = i >= 3 ? rviList[i - 3] : 0;

            var rvi = denominatorAvg != 0 ? numeratorAvg / denominatorAvg : 0;
            rviList.Add(rvi);

            var prevSignalLine = GetLastOrDefault(signalLineList);
            var signalLine = (rvi + (2 * k) + (2 * l) + m) / 6;
            signalLineList.Add(signalLine);

            var signal = GetCompareSignal(rvi - signalLine, k - prevSignalLine);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Rvi", rviList },
            { "Signal", signalLineList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(rviList);
        stockData.IndicatorName = IndicatorName.RelativeVigorIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Repulse
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateRepulse(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 5)
    {
        List<double> bullPowerList = new(stockData.Count);
        List<double> bearPowerList = new(stockData.Count);
        List<double> repulseList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, openList, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentClose = inputList[i];
            var lowestLow = lowestList[i];
            var highestHigh = highestList[i];
            var prevOpen = i >= 1 ? openList[i - 1] : 0;

            var bullPower = currentClose != 0 ? 100 * ((3 * currentClose) - (2 * lowestLow) - prevOpen) / currentClose : 0;
            bullPowerList.Add(bullPower);

            var bearPower = currentClose != 0 ? 100 * (prevOpen + (2 * highestHigh) - (3 * currentClose)) / currentClose : 0;
            bearPowerList.Add(bearPower);
        }

        var bullPowerEmaList = GetMovingAverageList(stockData, maType, length * 5, bullPowerList);
        var bearPowerEmaList = GetMovingAverageList(stockData, maType, length * 5, bearPowerList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var bullPowerEma = bullPowerEmaList[i];
            var bearPowerEma = bearPowerEmaList[i];

            var repulse = bullPowerEma - bearPowerEma;
            repulseList.Add(repulse);
        }

        var repulseEmaList = GetMovingAverageList(stockData, maType, length, repulseList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var repulse = repulseList[i];
            var prevRepulse = i >= 1 ? repulseList[i - 1] : 0;
            var repulseEma = repulseEmaList[i];
            var prevRepulseEma = i >= 1 ? repulseEmaList[i - 1] : 0;

            var signal = GetCompareSignal(repulse - repulseEma, prevRepulse - prevRepulseEma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Repulse", repulseList },
            { "Signal", repulseEmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(repulseList);
        stockData.IndicatorName = IndicatorName.Repulse;

        return stockData;
    }


    /// <summary>
    /// Calculates the Retrospective Candlestick Chart
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateRetrospectiveCandlestickChart(this StockData stockData, int length = 100)
    {
        List<double> kList = new(stockData.Count);
        List<double> cList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, openList, _) = GetInputValuesList(stockData);
        var absChgWindow = new RollingMinMax(length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentClose = inputList[i];
            var prevClose = i >= 1 ? inputList[i - 1] : 0;
            var currentOpen = openList[i];
            var prevK1 = i >= 1 ? kList[i - 1] : 0;
            var prevK2 = i >= 2 ? kList[i - 2] : 0;

            var absChg = Math.Abs(currentClose - prevClose);
            absChgWindow.Add(absChg);
            var highest = absChgWindow.Max;
            var lowest = absChgWindow.Min;
            var s = highest - lowest != 0 ? (absChg - lowest) / (highest - lowest) * 100 : 0;
            var weight = s / 100;

            var prevC = i >= 1 ? cList[i - 1] : currentClose;
            var c = (weight * currentClose) + ((1 - weight) * prevC);
            cList.Add(c);

            var prevH = i >= 1 ? prevC : currentHigh;
            var h = (weight * currentHigh) + ((1 - weight) * prevH);
            var prevL = i >= 1 ? prevC : currentLow;
            var l = (weight * currentLow) + ((1 - weight) * prevL);
            var prevO = i >= 1 ? prevC : currentOpen;
            var o = (weight * currentOpen) + ((1 - weight) * prevO);

            var k = (c + h + l + o) / 4;
            kList.Add(k);

            var signal = GetCompareSignal(k - prevK1, prevK1 - prevK2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Rcc", kList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(kList);
        stockData.IndicatorName = IndicatorName.RetrospectiveCandlestickChart;

        return stockData;
    }


    /// <summary>
    /// Calculates the Rex Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateRexOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length = 14)
    {
        List<double> tvbList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, openList, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var close = inputList[i];
            var open = openList[i];
            var high = highList[i];
            var low = lowList[i];

            var tvb = (3 * close) - (low + open + high);
            tvbList.Add(tvb);
        }

        var roList = GetMovingAverageList(stockData, maType, length, tvbList);
        var roEmaList = GetMovingAverageList(stockData, maType, length, roList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var ro = roList[i];
            var roEma = roEmaList[i];
            var prevRo = i >= 1 ? roList[i - 1] : 0;
            var prevRoEma = i >= 1 ? roEmaList[i - 1] : 0;

            var signal = GetCompareSignal(ro - roEma, prevRo - prevRoEma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ro", roList },
            { "Signal", roEmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(roList);
        stockData.IndicatorName = IndicatorName.RexOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Robust Weighting Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateRobustWeightingOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 200)
    {
        List<double> indexList = new(stockData.Count);
        List<double> corrList = new(stockData.Count);
        List<double> lList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var corrWindow = new RollingCorrelation();

        for (var i = 0; i < stockData.Count; i++)
        {
            double index = i;
            indexList.Add(index);

            var currentValue = inputList[i];
            corrWindow.Add(index, currentValue);
            var corr = corrWindow.R(length);
            corr = IsValueNullOrInfinity(corr) ? 0 : corr;
            corrList.Add((double)corr);
        }

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);
        var indexSmaList = GetMovingAverageList(stockData, maType, length, indexList);
        var stdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        stockData.SetCustomValues(indexList);
        var indexStdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var corr = corrList[i];
            var stdDev = stdDevList[i];
            var indexStdDev = indexStdDevList[i];
            var sma = smaList[i];
            var indexSma = indexSmaList[i];
            var a = indexStdDev != 0 ? corr * (stdDev / indexStdDev) : 0;
            var b = sma - (a * indexSma);

            var l = currentValue - a - (b * currentValue);
            lList.Add(l);
        }

        var lSmaList = GetMovingAverageList(stockData, maType, length, lList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var l = lSmaList[i];
            var prevL1 = i >= 1 ? lSmaList[i - 1] : 0;
            var prevL2 = i >= 2 ? lSmaList[i - 2] : 0;

            var signal = GetCompareSignal(l - prevL1, prevL1 - prevL2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Rwo", lSmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(lSmaList);
        stockData.IndicatorName = IndicatorName.RobustWeightingOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the RSING Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateRSINGIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 20)
    {
        List<double> rsingList = new(stockData.Count);
        List<double> upList = new(stockData.Count);
        List<double> dnList = new(stockData.Count);
        List<double> rangeList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, volumeList) = GetInputValuesList(stockData);

        var maList = GetMovingAverageList(stockData, maType, length, volumeList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var high = highList[i];
            var low = lowList[i];

            var range = high - low;
            rangeList.Add(range);
        }

        stockData.SetCustomValues(rangeList);
        var stdevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentVolume = volumeList[i];
            var ma = maList[i];
            var stdev = stdevList[i];
            var range = rangeList[i];
            var currentValue = inputList[i];
            var prevValue = i >= length ? inputList[i - length] : 0;
            var vwr = ma != 0 ? currentVolume / ma : 0;
            var blr = stdev != 0 ? range / stdev : 0;
            var isUp = currentValue > prevValue;
            var isDn = currentValue < prevValue;
            var isEq = currentValue == prevValue;

            var prevUpCount = GetLastOrDefault(upList);
            var upCount = isEq ? 0 : isUp ? (prevUpCount <= 0 ? 1 : prevUpCount + 1) : (prevUpCount >= 0 ? -1 : prevUpCount - 1);
            upList.Add(upCount);

            var prevDnCount = GetLastOrDefault(dnList);
            var dnCount = isEq ? 0 : isDn ? (prevDnCount <= 0 ? 1 : prevDnCount + 1) : (prevDnCount >= 0 ? -1 : prevDnCount - 1);
            dnList.Add(dnCount);

            var pmo = MinPastValues(i, length, currentValue - prevValue);
            var rsing = vwr * blr * pmo;
            rsingList.Add(rsing);
        }

        var rsingMaList = GetMovingAverageList(stockData, maType, length, rsingList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var rsing = rsingMaList[i];
            var prevRsing1 = i >= 1 ? rsingMaList[i - 1] : 0;
            var prevRsing2 = i >= 2 ? rsingMaList[i - 2] : 0;

            var signal = GetCompareSignal(rsing - prevRsing1, prevRsing1 - prevRsing2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Rsing", rsingList },
            { "Signal", rsingMaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(rsingList);
        stockData.IndicatorName = IndicatorName.RSINGIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the RSMK Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="marketData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateRSMKIndicator(this StockData stockData, StockData marketData, 
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 90, int smoothLength = 3)
    {
        List<double> rsmkList = new(stockData.Count);
        List<double> logRatioList = new(stockData.Count);
        List<double> logDiffList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var (spInputList, _, _, _, _) = GetInputValuesList(marketData);

        if (stockData.Count == marketData.Count)
        {
            for (var i = 0; i < stockData.Count; i++)
            {
                var currentValue = inputList[i];
                var spValue = spInputList[i];
                var prevLogRatio = i >= length ? logRatioList[i - length] : 0;

                var logRatio = spValue != 0 ? currentValue / spValue : 0;
                logRatioList.Add(logRatio);

                var logDiff = logRatio - prevLogRatio;
                logDiffList.Add(logDiff);
            }

            var logDiffEmaList = GetMovingAverageList(stockData, maType, smoothLength, logDiffList);
            for (var i = 0; i < stockData.Count; i++)
            {
                var logDiffEma = logDiffEmaList[i];

                var prevRsmk = GetLastOrDefault(rsmkList);
                var rsmk = logDiffEma * 100;
                rsmkList.Add(rsmk);

                var signal = GetCompareSignal(rsmk, prevRsmk);
                signalsList?.Add(signal);
            }
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Rsmk", rsmkList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(rsmkList);
        stockData.IndicatorName = IndicatorName.RSMKIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Running Equity
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateRunningEquity(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 100)
    {
        List<double> reqList = new(stockData.Count);
        List<double> xList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var chgXSumWindow = new RollingSum();

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var sma = smaList[i];

            var prevX = GetLastOrDefault(xList);
            double x = Math.Sign(currentValue - sma);
            xList.Add(x);

            var chgX = MinPastValues(i, 1, currentValue - prevValue) * prevX;
            chgXSumWindow.Add(chgX);

            var prevReq = GetLastOrDefault(reqList);
            var req = chgXSumWindow.Sum(length);
            reqList.Add(req);

            var signal = GetCompareSignal(req, prevReq);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Req", reqList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(reqList);
        stockData.IndicatorName = IndicatorName.RunningEquity;

        return stockData;
    }


    /// <summary>
    /// Calculates the Mass Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculateMassIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 21, int length2 = 21, int length3 = 25, int signalLength = 9)
    {
        List<double> highLowList = new(stockData.Count);
        List<double> massIndexList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (_, highList, lowList, _, _) = GetInputValuesList(stockData);
        var ratioSumWindow = new RollingSum();

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];

            var highLow = currentHigh - currentLow;
            highLowList.Add(highLow);
        }

        var firstEmaList = GetMovingAverageList(stockData, maType, length1, highLowList);
        var secondEmaList = GetMovingAverageList(stockData, maType, length2, firstEmaList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var firstEma = firstEmaList[i];
            var secondEma = secondEmaList[i];

            var ratio = secondEma != 0 ? firstEma / secondEma : 0;
            ratioSumWindow.Add(ratio);
            var massIndex = ratioSumWindow.Sum(length3);
            massIndexList.Add(massIndex);
        }

        var massIndexSignalList = GetMovingAverageList(stockData, maType, signalLength, massIndexList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var massIndex = massIndexList[i];
            var massIndexEma = massIndexSignalList[i];
            var prevMassIndex = i >= 1 ? massIndexList[i - 1] : 0;
            var prevMassIndexEma = i >= 1 ? massIndexSignalList[i - 1] : 0;

            var signal = GetCompareSignal(massIndex - massIndexEma, prevMassIndex - prevMassIndexEma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Mi", massIndexList },
            { "Signal", massIndexSignalList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(massIndexList);
        stockData.IndicatorName = IndicatorName.MassIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Mass Thrust Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateMassThrustOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length = 14)
    {
        List<double> topList = new(stockData.Count);
        List<double> botList = new(stockData.Count);
        List<double> mtoList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);
        var advSumWindow = new RollingSum();
        var decSumWindow = new RollingSum();
        var advVolSumWindow = new RollingSum();
        var decVolSumWindow = new RollingSum();

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentVolume = volumeList[i];

            var adv = i >= 1 && currentValue > prevValue ? MinPastValues(i, 1, currentValue - prevValue) : 0;
            advSumWindow.Add(adv);

            var dec = i >= 1 && currentValue < prevValue ? MinPastValues(i, 1, prevValue - currentValue) : 0;
            decSumWindow.Add(dec);

            var advSum = advSumWindow.Sum(length);
            var decSum = decSumWindow.Sum(length);

            var advVol = currentValue > prevValue && advSum != 0 ? currentVolume / advSum : 0;
            advVolSumWindow.Add(advVol);

            var decVol = currentValue < prevValue && decSum != 0 ? currentVolume / decSum : 0;
            decVolSumWindow.Add(decVol);

            var advVolSum = advVolSumWindow.Sum(length);
            var decVolSum = decVolSumWindow.Sum(length);

            var top = (advSum * advVolSum) - (decSum * decVolSum);
            topList.Add(top);

            var bot = (advSum * advVolSum) + (decSum * decVolSum);
            botList.Add(bot);

            var mto = bot != 0 ? 100 * top / bot : 0;
            mtoList.Add(mto);
        }

        var mtoEmaList = GetMovingAverageList(stockData, maType, length, mtoList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var mto = mtoList[i];
            var mtoEma = mtoEmaList[i];
            var prevMto = i >= 1 ? mtoList[i - 1] : 0;
            var prevMtoEma = i >= 1 ? mtoEmaList[i - 1] : 0;

            var signal = GetRsiSignal(mto - mtoEma, prevMto - prevMtoEma, mto, prevMto, 50, -50);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Mto", mtoList },
            { "Signal", mtoEmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(mtoList);
        stockData.IndicatorName = IndicatorName.MassThrustOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Midpoint Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculateMidpointOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 26, int signalLength = 9)
    {
        List<double> moList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var hh = highestList[i];
            var ll = lowestList[i];

            var mo = hh - ll != 0 ? MinOrMax(100 * ((2 * currentValue) - hh - ll) / (hh - ll), 100, -100) : 0;
            moList.Add(mo);
        }

        var moEmaList = GetMovingAverageList(stockData, maType, signalLength, moList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var mo = moList[i];
            var moEma = moEmaList[i];
            var prevMo = i >= 1 ? moList[i - 1] : 0;
            var prevMoEma = i >= 1 ? moEmaList[i - 1] : 0;

            var signal = GetRsiSignal(mo - moEma, prevMo - prevMoEma, mo, prevMo, 70, -70);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Mo", moList },
            { "Signal", moEmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(moList);
        stockData.IndicatorName = IndicatorName.MidpointOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Morphed Sine Wave
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="power"></param>
    /// <returns></returns>
    public static StockData CalculateMorphedSineWave(this StockData stockData, int length = 14, double power = 100)
    {
        List<double> sList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var p = length / (2 * Math.PI);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevS1 = i >= 1 ? sList[i - 1] : 0;
            var prevS2 = i >= 2 ? sList[i - 2] : 0;
            var c = (currentValue * power) + Math.Sin(i / p);

            var s = c / power;
            sList.Add(s);

            var signal = GetCompareSignal(s - prevS1, prevS1 - prevS2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Msw", sList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(sList);
        stockData.IndicatorName = IndicatorName.MorphedSineWave;

        return stockData;
    }


    /// <summary>
    /// Calculates the Move Tracker
    /// </summary>
    /// <param name="stockData"></param>
    /// <returns></returns>
    public static StockData CalculateMoveTracker(this StockData stockData)
    {
        List<double> mtList = new(stockData.Count);
        List<double> mtSignalList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevMt = GetLastOrDefault(mtList);
            var mt = MinPastValues(i, 1, currentValue - prevValue);
            mtList.Add(mt);

            var prevMtSignal = GetLastOrDefault(mtSignalList);
            var mtSignal = mt - prevMt;
            mtSignalList.Add(mtSignal);

            var signal = GetCompareSignal(mt - mtSignal, prevMt - prevMtSignal);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Mt", mtList },
            { "Signal", mtSignalList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(mtList);
        stockData.IndicatorName = IndicatorName.MoveTracker;

        return stockData;
    }


    /// <summary>
    /// Calculates the Multi Level Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="factor"></param>
    /// <returns></returns>
    public static StockData CalculateMultiLevelIndicator(this StockData stockData, int length = 14, double factor = 10000)
    {
        List<double> zList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, openList, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevOpen = i >= length ? openList[i - length] : 0;
            var currentOpen = openList[i];
            var currentClose = inputList[i];
            var prevZ1 = i >= 1 ? zList[i - 1] : 0;
            var prevZ2 = i >= 2 ? zList[i - 2] : 0;

            var z = (currentClose - currentOpen - (currentClose - prevOpen)) * factor;
            zList.Add(z);

            var signal = GetRsiSignal(z - prevZ1, prevZ1 - prevZ2, z, prevZ1, 5, -5);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Mli", zList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(zList);
        stockData.IndicatorName = IndicatorName.MultiLevelIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Modified Gann Hilo Activator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="mult"></param>
    /// <returns></returns>
    public static StockData CalculateModifiedGannHiloActivator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 50, double mult = 1)
    {
        List<double> gannHiloList = new(stockData.Count);
        List<double> cList = new(stockData.Count);
        List<double> dList = new(stockData.Count);
        List<double> gList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, openList, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var highestHigh = highestList[i];
            var lowestLow = lowestList[i];
            var currentClose = inputList[i];
            var currentOpen = openList[i];
            var max = Math.Max(currentClose, currentOpen);
            var min = Math.Min(currentClose, currentOpen);
            var a = highestHigh - max;
            var b = min - lowestLow;

            var c = max + (a * mult);
            cList.Add(c);

            var d = min - (b * mult);
            dList.Add(d);
        }

        var eList = GetMovingAverageList(stockData, maType, length, cList);
        var fList = GetMovingAverageList(stockData, maType, length, dList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentClose = inputList[i];
            var prevClose = i >= 1 ? inputList[i - 1] : 0;
            var f = fList[i];
            var e = eList[i];

            var prevG = GetLastOrDefault(gList);
            var g = currentClose > e ? 1 : currentClose > f ? 0 : prevG;
            gList.Add(g);

            var prevGannHilo = GetLastOrDefault(gannHiloList);
            var gannHilo = (g * f) + ((1 - g) * e);
            gannHiloList.Add(gannHilo);

            var signal = GetCompareSignal(currentClose - gannHilo, prevClose - prevGannHilo);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ghla", gannHiloList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(gannHiloList);
        stockData.IndicatorName = IndicatorName.ModifiedGannHiloActivator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Market Direction Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <returns></returns>
    public static StockData CalculateMarketDirectionIndicator(this StockData stockData, int fastLength = 13, int slowLength = 55)
    {
        List<double> mdiList = new(stockData.Count);
        List<double> cp2List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var tempSumWindow = new RollingSum();

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            tempSumWindow.Add(currentValue);

            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var len1Sum = tempSumWindow.Sum(fastLength - 1);
            var len2Sum = tempSumWindow.Sum(slowLength - 1);

            var prevCp2 = GetLastOrDefault(cp2List);
            var cp2 = ((fastLength * len2Sum) - (slowLength * len1Sum)) / (slowLength - fastLength);
            cp2List.Add(cp2);

            var prevMdi = GetLastOrDefault(mdiList);
            var mdi = currentValue + prevValue != 0 ? 100 * (prevCp2 - cp2) / ((currentValue + prevValue) / 2) : 0;
            mdiList.Add(mdi);

            var signal = GetCompareSignal(mdi, prevMdi);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Mdi", mdiList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(mdiList);
        stockData.IndicatorName = IndicatorName.MarketDirectionIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Mobility Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculateMobilityOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length1 = 10, int length2 = 14, int signalLength = 7)
    {
        List<double> moList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length2);

        for (var i = 0; i < stockData.Count; i++)
        {
            var hMax = highestList[i];
            var lMin = lowestList[i];
            var prevC = i >= length2 ? inputList[i - length2] : 0;
            var rx = length1 != 0 ? (hMax - lMin) / length1 : 0;

            var imx = 1;
            double pdfmx = 0, pdfc = 0, rx1, bu, bl, bu1, bl1, pdf;
            for (var j = 1; j <= length1; j++)
            {
                bu = lMin + (j * rx);
                bl = bu - rx;

                var currHigh = i >= j ? highList[i - j] : 0;
                var currLow = i >= j ? lowList[i - j] : 0;
                double hMax1 = currHigh, lMin1 = currLow;
                for (var k = 2; k < length2; k++)
                {
                    var high = i >= j + k ? highList[i - (j + k)] : 0;
                    var low = i >= j + k ? lowList[i - (j + k)] : 0;
                    hMax1 = Math.Max(high, hMax1);
                    lMin1 = Math.Min(low, lMin1);
                }

                rx1 = length1 != 0 ? (hMax1 - lMin1) / length1 : 0; //-V3022
                bl1 = lMin1 + ((j - 1) * rx1);
                bu1 = lMin1 + (j * rx1);

                pdf = 0;
                for (var k = 1; k <= length2; k++)
                {
                    var high = i >= j + k ? highList[i - (j + k)] : 0;
                    var low = i >= j + k ? lowList[i - (j + k)] : 0;

                    if (high <= bu1)
                    {
                        pdf += 1;
                    }
                    if (high <= bu1 || low >= bu1)
                    {
                        if (high <= bl1)
                        {
                            pdf -= 1;
                        }
                        if (high <= bl || low >= bl1)
                        {
                            continue;
                        }
                        else
                        {
                            pdf -= high - low != 0 ? (bl1 - low) / (high - low) : 0;
                        }
                    }
                    else
                    {
                        pdf += high - low != 0 ? (bu1 - low) / (high - low) : 0;
                    }
                }

                pdf = length2 != 0 ? pdf / length2 : 0;
                pdfmx = j == 1 ? pdf : pdfmx;
                imx = j == 1 ? j : imx;
                pdfmx = Math.Max(pdf, pdfmx);
                pdfc = j == 1 ? pdf : pdfc;
                pdfc = prevC > bl && prevC <= bu ? pdf : pdfc;
            }

            var pmo = lMin + ((imx - 0.5) * rx);
            var mo = pdfmx != 0 ? 100 * (1 - (pdfc / pdfmx)) : 0;
            mo = prevC < pmo ? -mo : mo;
            moList.Add(-mo);
        }

        var moWmaList = GetMovingAverageList(stockData, maType, signalLength, moList);
        var moSigList = GetMovingAverageList(stockData, maType, signalLength, moWmaList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var mo = moWmaList[i];
            var moSig = moSigList[i];
            var prevMo = i >= 1 ? moWmaList[i - 1] : 0;
            var prevMoSig = i >= 1 ? moSigList[i - 1] : 0;

            var signal = GetCompareSignal(mo - moSig, prevMo - prevMoSig);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Mo", moWmaList },
            { "Signal", moSigList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(moWmaList);
        stockData.IndicatorName = IndicatorName.MobilityOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Mass Thrust Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateMassThrustIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 14)
    {
        List<double> mtiList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);
        var advSumWindow = new RollingSum();
        var decSumWindow = new RollingSum();
        var advVolSumWindow = new RollingSum();
        var decVolSumWindow = new RollingSum();

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentVolume = volumeList[i];

            var adv = i >= 1 && currentValue > prevValue ? MinPastValues(i, 1, currentValue - prevValue) : 0;
            advSumWindow.Add(adv);

            var dec = i >= 1 && currentValue < prevValue ? MinPastValues(i, 1, prevValue - currentValue) : 0;
            decSumWindow.Add(dec);

            var advSum = advSumWindow.Sum(length);
            var decSum = decSumWindow.Sum(length);

            var advVol = currentValue > prevValue && advSum != 0 ? currentVolume / advSum : 0;
            advVolSumWindow.Add(advVol);

            var decVol = currentValue < prevValue && decSum != 0 ? currentVolume / decSum : 0;
            decVolSumWindow.Add(decVol);

            var advVolSum = advVolSumWindow.Sum(length);
            var decVolSum = decVolSumWindow.Sum(length);

            var mti = ((advSum * advVolSum) - (decSum * decVolSum)) / 1000000;
            mtiList.Add(mti);
        }

        var mtiEmaList = GetMovingAverageList(stockData, maType, length, mtiList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var mtiEma = mtiEmaList[i];
            var prevMtiEma = i >= 1 ? mtiEmaList[i - 1] : 0;

            var signal = GetCompareSignal(mtiEma, prevMtiEma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Mti", mtiList },
            { "Signal", mtiEmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(mtiList);
        stockData.IndicatorName = IndicatorName.MassThrustIndicator;

        return stockData;
    }

}

