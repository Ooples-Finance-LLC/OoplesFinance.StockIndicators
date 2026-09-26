using OoplesFinance.StockIndicators.Compatibility;
using OoplesFinance.StockIndicators.Core;

namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the arnaud legoux moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="sigma">The sigma.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateArnaudLegouxMovingAverage(this StockData stockData, int length = 9, double offset = 0.85, int sigma = 6)
    {
        List<double> almaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        using var mean = new AlmaWindowMean(length, offset, sigma);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            var prevAlma = GetLastOrDefault(almaList);
            var alma = mean.Next(currentValue, true);
            almaList.Add(alma);

            var signal = GetCompareSignal(currentValue - alma, prevVal - prevAlma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Alma", almaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(almaList);
        stockData.IndicatorName = IndicatorName.ArnaudLegouxMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the ahrens moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateAhrensMovingAverage(this StockData stockData, int length = 9)
    {
        List<double> ahmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        using var window = new AhrensWindow(length);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevAhma = GetLastOrDefault(ahmaList);
            var ahma = window.Next(currentValue, true);
            ahmaList.Add(ahma);

            var signal = GetCompareSignal(currentValue - ahma, prevValue - prevAhma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ahma", ahmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(ahmaList);
        stockData.IndicatorName = IndicatorName.AhrensMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the adaptive moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="fastLength">Length of the fast.</param>
    /// <param name="slowLength">Length of the slow.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateAdaptiveMovingAverage(this StockData stockData, int fastLength = 2, int slowLength = 14, int length = 14)
    {
        List<double> amaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length + 1);

        var fastAlpha = (double)2 / (fastLength + 1);
        var slowAlpha = (double)2 / (slowLength + 1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var hh = highestList[i];
            var ll = lowestList[i];
            var mltp = hh - ll != 0 ? MinOrMax(Math.Abs((2 * currentValue) - ll - hh) / (hh - ll), 1, 0) : 0;
            var ssc = (mltp * (fastAlpha - slowAlpha)) + slowAlpha;

            var prevAma = GetLastOrDefault(amaList);
            var ama = prevAma + (Pow(ssc, 2) * (currentValue - prevAma));
            amaList.Add(ama);

            var signal = GetCompareSignal(currentValue - ama, prevValue - prevAma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ama", amaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(amaList);
        stockData.IndicatorName = IndicatorName.AdaptiveMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the adaptive exponential moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateAdaptiveExponentialMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 10)
    {
        List<double> aemaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        var mltp1 = (double)2 / (length + 1);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var hh = highestList[i];
            var ll = lowestList[i];
            var sma = smaList[i];
            var mltp2 = hh - ll != 0 ? MinOrMax(Math.Abs((2 * currentValue) - ll - hh) / (hh - ll), 1, 0) : 0;
            var rate = mltp1 * (1 + mltp2);

            var prevAema = i >= 1 ? GetLastOrDefault(aemaList) : currentValue;
            var aema = i <= length ? sma : prevAema + (rate * (currentValue - prevAema));
            aemaList.Add(aema);

            var signal = GetCompareSignal(currentValue - aema, prevValue - prevAema);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Aema", aemaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(aemaList);
        stockData.IndicatorName = IndicatorName.AdaptiveExponentialMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the adaptive autonomous recursive moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <param name="gamma">The gamma.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateAdaptiveAutonomousRecursiveMovingAverage(this StockData stockData, int length = 14, double gamma = 3)
    {
        List<double> ma1List = new(stockData.Count);
        List<double> ma2List = new(stockData.Count);
        List<double> absDiffList = new(stockData.Count);
        List<double> dList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        double absDiffSum = 0;
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var erList = CalculateKaufmanAdaptiveMovingAverage(stockData, length: length).ChainedOutputs["Er"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var er = erList[i];
            var prevMa2 = i >= 1 ? ma2List[i - 1] : currentValue;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var absDiff = Math.Abs(currentValue - prevMa2);
            absDiffList.Add(absDiff);

            absDiffSum += absDiff;
            var d = i != 0 ? absDiffSum / i * gamma : 0;
            dList.Add(d);

            var c = currentValue > prevMa2 + d ? currentValue + d : currentValue < prevMa2 - d ? currentValue - d : prevMa2;
            var prevMa1 = i >= 1 ? ma1List[i - 1] : currentValue;
            var ma1 = (er * c) + ((1 - er) * prevMa1);
            ma1List.Add(ma1);

            var ma2 = (er * ma1) + ((1 - er) * prevMa2);
            ma2List.Add(ma2);

            var signal = GetCompareSignal(currentValue - ma2, prevValue - prevMa2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "D", dList },
            { "Aarma", ma2List }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(ma2List);
        stockData.IndicatorName = IndicatorName.AdaptiveAutonomousRecursiveMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the autonomous recursive moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <param name="momLength">Length of the mom.</param>
    /// <param name="gamma">The gamma.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateAutonomousRecursiveMovingAverage(this StockData stockData, int length = 14, int momLength = 7, double gamma = 3)
    {
        List<double> madList = new(stockData.Count);
        List<double> ma1List = new(stockData.Count);
        List<double> absDiffList = new(stockData.Count);
        List<double> cList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum cSumWindow = new();
        RollingSum ma1SumWindow = new();
        double absDiffSum = 0;
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var priorValue = i >= momLength ? inputList[i - momLength] : 0;
            var prevMad = i >= 1 ? madList[i - 1] : currentValue;

            var absDiff = Math.Abs(priorValue - prevMad);
            absDiffList.Add(absDiff);

            absDiffSum += absDiff;
            var d = i != 0 ? absDiffSum / i * gamma : 0;
            var c = currentValue > prevMad + d ? currentValue + d : currentValue < prevMad - d ? currentValue - d : prevMad;
            cList.Add(c);
            cSumWindow.Add(c);

            var ma1 = cSumWindow.Average(length);
            ma1List.Add(ma1);
            ma1SumWindow.Add(ma1);

            var mad = ma1SumWindow.Average(length);
            madList.Add(mad);

            var signal = GetCompareSignal(currentValue - mad, prevValue - prevMad);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Arma", madList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(madList);
        stockData.IndicatorName = IndicatorName.AutonomousRecursiveMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the atr filtered exponential moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <param name="atrLength">Length of the atr.</param>
    /// <param name="stdDevLength">Length of the standard dev.</param>
    /// <param name="lbLength">Length of the lb.</param>
    /// <param name="min">The minimum.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateAtrFilteredExponentialMovingAverage(this StockData stockData, 
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 45, int atrLength = 20, int stdDevLength = 10, int lbLength = 20, 
        double min = 5)
    {
        List<double> trValList = new(stockData.Count);
        List<double> atrValPowList = new(stockData.Count);
        List<double> tempList = new(stockData.Count);
        List<double> stdDevList = new(stockData.Count);
        List<double> emaAFPList = new(stockData.Count);
        List<double> emaCTPList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum atrValSumWindow = new();
        RollingMinMax stdDevWindow = new(lbLength);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            // For TrueRange on first bar, use current close to avoid inflated TR
            var prevValue = i >= 1 ? inputList[i - 1] : inputList[i];
            var tr = CalculationsHelper.CalculateTrueRange(currentHigh, currentLow, prevValue);

            var trVal = currentValue != 0 ? tr / currentValue : tr;
            trValList.Add(trVal);
        }

        var atrValList = GetMovingAverageList(stockData, maType, atrLength, trValList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var atrVal = atrValList[i];

            var atrValPow = Pow(atrVal, 2);
            atrValPowList.Add(atrValPow);
        }

        var stdDevAList = GetMovingAverageList(stockData, maType, stdDevLength, atrValPowList);
        var stableDeviation = maType == MovingAvgType.SimpleMovingAverage
            ? GetStandardDeviationList(atrValList, stdDevLength) : null;
        for (var i = 0; i < stockData.Count; i++)
        {
            var stdDevA = stdDevAList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var atrVal = atrValList[i];
            tempList.Add(atrVal);
            atrValSumWindow.Add(atrVal);

            var atrValSum = atrValSumWindow.Sum(stdDevLength);
            var stdDevB = Pow(atrValSum, 2) / Pow(stdDevLength, 2);

            var stdDev = stableDeviation is not null ? stableDeviation[i]
                : stdDevA - stdDevB >= 0 ? Sqrt(stdDevA - stdDevB) : 0;
            stdDevList.Add(stdDev);
            stdDevWindow.Add(stdDev);

            var stdDevLow = stdDevWindow.Min;
            // stdDevLow is the lowest stdDev in the window, so a stdDev of zero makes both zero and both
            // ratios 0/0 - two equal deviations, which is 1. Reading them as 0 kills the smoothing factor.
            var stdDevFactorAFP = stdDev != 0 ? stdDevLow / stdDev : 1;
            var stdDevFactorCTP = stdDevLow != 0 ? stdDev / stdDevLow : 1;
            var stdDevFactorAFPLow = Math.Min(stdDevFactorAFP, min);
            var stdDevFactorCTPLow = Math.Min(stdDevFactorCTP, min);
            var alphaAfp = (2 * stdDevFactorAFPLow) / (length + 1);
            var alphaCtp = (2 * stdDevFactorCTPLow) / (length + 1);

            // An exponential average starts at a price, not at zero.
            var prevEmaAfp = i >= 1 ? emaAFPList[i - 1] : currentValue;
            var emaAfp = (alphaAfp * currentValue) + ((1 - alphaAfp) * prevEmaAfp);
            emaAFPList.Add(emaAfp);

            var prevEmaCtp = i >= 1 ? emaCTPList[i - 1] : currentValue;
            var emaCtp = (alphaCtp * currentValue) + ((1 - alphaCtp) * prevEmaCtp);
            emaCTPList.Add(emaCtp);

            var signal = GetCompareSignal(currentValue - emaAfp, prevValue - prevEmaAfp);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Afp", emaAFPList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(emaAFPList);
        stockData.IndicatorName = IndicatorName.AtrFilteredExponentialMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the adaptive least squares.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <param name="smooth">The smooth.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateAdaptiveLeastSquares(this StockData stockData, int length = 500, double smooth = 1.5)
    {
        List<double> regList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var trWindow = new RollingMinMax(Math.Max(1, length));
        var regression = new AdaptiveLeastSquaresMoments();
        for (var i = 0; i < stockData.Count; i++)
        {
            var current = inputList[i];
            var previous = i == 0 ? current : inputList[i - 1];
            var tr = CalculationsHelper.CalculateTrueRange(highList[i], lowList[i], previous);
            trWindow.Add(tr);
            var gain = trWindow.Max == 0 ? .01 : MinOrMax(Pow(tr / trWindow.Max, smooth), .99, .01);
            var estimate = regression.Next(current, gain);
            var previousEstimate = i == 0 ? 0 : regList[i - 1];
            regList.Add(estimate);
            signalsList?.Add(GetCompareSignal(current - estimate, previous - previousEstimate));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Als", regList } });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(regList);
        stockData.IndicatorName = IndicatorName.AdaptiveLeastSquares;
        return stockData;
    }


    /// <summary>
    /// Calculates the alpha decreasing exponential moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateAlphaDecreasingExponentialMovingAverage(this StockData stockData)
    {
        List<double> emaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var window = new AlphaDecreasingWindow();
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevEma = GetLastOrDefault(emaList);
            var ema = window.Next(currentValue, true);
            emaList.Add(ema);

            var signal = GetCompareSignal(currentValue - ema, prevValue - prevEma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ema", emaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(emaList);
        stockData.IndicatorName = IndicatorName.AlphaDecreasingExponentialMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the automatic filter.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateAutoFilter(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 500)
    {
        List<double> regList = new(stockData.Count);
        List<double> corrList = new(stockData.Count);
        List<double> interList = new(stockData.Count);
        List<double> slopeList = new(stockData.Count);
        List<double> tempList = new(stockData.Count);
        List<double> xList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingCorrelation corrWindow = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var yMaList = GetMovingAverageList(stockData, maType, length, inputList);
        var devList = GetStandardDeviationList(inputList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var dev = devList[i];

            var currentValue = inputList[i];
            tempList.Add(currentValue);

            var prevX = i >= 1 ? xList[i - 1] : currentValue;
            var x = currentValue > prevX + dev ? currentValue : currentValue < prevX - dev ? currentValue : prevX;
            xList.Add(x);

            corrWindow.Add(currentValue, x);
            var corr = corrWindow.R(length);
            corr = IsValueNullOrInfinity(corr) ? 0 : corr;
            corrList.Add((double)corr);
        }

        var xMaList = GetMovingAverageList(stockData, maType, length, xList);
        stockData.SetCustomValues(xList);
        var mxList = GetStandardDeviationList(xList, length);
        for (var i = 0; i < stockData.Count; i++)
        {
            var my = devList[i];
            var mx = mxList[i];
            var corr = corrList[i];
            var yMa = yMaList[i];
            var xMa = xMaList[i];
            var x = xList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var slope = mx != 0 ? corr * (my / mx) : 0;
            var inter = yMa - (slope * xMa);

            var prevReg = GetLastOrDefault(regList);
            var reg = (x * slope) + inter;
            regList.Add(reg);

            var signal = GetCompareSignal(currentValue - reg, prevValue - prevReg);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Af", regList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(regList);
        stockData.IndicatorName = IndicatorName.AutoFilter;

        return stockData;
    }


    /// <summary>
    /// Calculates the automatic line.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateAutoLine(this StockData stockData, int length = 500)
    {
        List<double> xList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        // The deviation of the window about its own mean, not the mean squared residual from a moving average
        // of it. The line holds until price escapes a band one deviation either side of it, and sigma in a band
        // is the windowed deviation; CalculateStandardDeviationVolatility is a different quantity, about 55%
        // wider on a typical price series, so the band was that much too wide and the line held through moves
        // that should have moved it. See #190.
        //
        // The deviation is 0 until the window fills, which at the default length of 500 is a long warm-up. The
        // band is then zero-width and the line simply follows price, which is the honest answer where no
        // deviation is known yet - and unlike a carried-forward length, it costs nothing later: the line picks
        // up its band as soon as the window fills.
        var devList = GetStandardDeviationList(inputList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var dev = devList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevX = i >= 1 ? xList[i - 1] : currentValue;
            var x = currentValue > prevX + dev ? currentValue : currentValue < prevX - dev ? currentValue : prevX;
            xList.Add(x);

            var signal = GetCompareSignal(currentValue - x, prevValue - prevX);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Al", xList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(xList);
        stockData.IndicatorName = IndicatorName.AutoLine;

        return stockData;
    }


    /// <summary>
    /// Calculates the automatic line with drift.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateAutoLineWithDrift(this StockData stockData, int length = 500)
    {
        List<double> aList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        // The deviation of the window about its own mean, as in the automatic line above: the band the line
        // holds within is one deviation either side of it, and sigma in a band is the windowed deviation. The
        // quantity this replaces is about 55% wider, so the band was too wide and the line drifted where it
        // should have jumped. See #190.
        var stdDevList = GetStandardDeviationList(inputList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var dev = stdDevList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var r = Math.Round(currentValue);

            var prevA = i >= 1 ? aList[i - 1] : r;
            var priorA = i >= length + 1 ? aList[i - (length + 1)] : r;
            var a = currentValue > prevA + dev ? currentValue : currentValue < prevA - dev ? currentValue :
                prevA + ((double)1 / (length * 2) * (prevA - priorA));
            aList.Add(a);

            var signal = GetCompareSignal(currentValue - a, prevValue - prevA);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Alwd", aList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(aList);
        stockData.IndicatorName = IndicatorName.AutoLineWithDrift;

        return stockData;
    }


    /// <summary>
    /// Calculates the 1LC Least Squares Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData Calculate1LCLeastSquaresMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 14)
    {
        List<double> yList = new(stockData.Count);
        List<double> tempList = new(stockData.Count);
        List<double> corrList = new(stockData.Count);
        List<double> indexList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingCorrelation corrWindow = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);
        // stdev(src, length) in the original: the prices' own standard deviation.
        var stdDevList = GetStandardDeviationList(inputList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            tempList.Add(currentValue);

            double index = i;
            indexList.Add(index);

            corrWindow.Add(index, currentValue);
            var corr = corrWindow.R(length);
            corr = IsValueNullOrInfinity(corr) ? 0 : corr;
            corrList.Add((double)corr);
        }

        for (var i = 0; i < stockData.Count; i++)
        {
            var sma = smaList[i];
            var corr = corrList[i];
            var stdDev = stdDevList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevY = GetLastOrDefault(yList);
            var y = sma + (corr * stdDev * 1.7);
            yList.Add(y);

            var signal = GetCompareSignal(currentValue - y, prevValue - prevY);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "1lsma", yList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(yList);
        stockData.IndicatorName = IndicatorName._1LCLeastSquaresMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the 3HMA
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData Calculate3HMA(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 50)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        List<double> line = new(stockData.Count); List<Signal>? signals = CreateSignalsList(stockData);
        if (Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType))
        {
            var p = ThreeHullWindow.Period(length);
            var first = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(input), ThreeHullWindow.Third(p))?.ToList() ?? GetMovingAverageList(stockData, maType, ThreeHullWindow.Third(p), input);
            var second = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(input), ThreeHullWindow.Half(p))?.ToList() ?? GetMovingAverageList(stockData, maType, ThreeHullWindow.Half(p), input);
            var third = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(input), p)?.ToList() ?? GetMovingAverageList(stockData, maType, p, input);
            var adjusted = first.Select((v, i) => ThreeHullWindow.Combine(v, second[i], third[i])).ToList();
            line = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(adjusted), p)?.ToList() ?? GetMovingAverageList(stockData, maType, p, adjusted);
        }
        else
        {
            using var window = new ThreeHullWindow(maType, length);
            foreach (var price in input) line.Add(window.Next(price, true));
        }
        for (var i = 0; i < input.Count; i++) signals?.Add(GetCompareSignal(input[i] - line[i], i == 0 ? 0 : input[i - 1] - line[i - 1]));
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "3hma", line } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName._3HMA;
        return stockData;
    }


    /// <summary>
    /// Calculates the Bryant Adaptive Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="maxLength"></param>
    /// <param name="trend"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateBryantAdaptiveMovingAverage(this StockData stockData, int length = 14, int maxLength = 100, double trend = -1)
    {
        List<double> bamaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var erList = CalculateKaufmanAdaptiveMovingAverage(stockData, length: length).ChainedOutputs["Er"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var er = erList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var ver = Pow(er - (((2 * er) - 1) / 2 * (1 - trend)) + 0.5, 2);
            var vLength = ver != 0 ? (length - ver + 1) / ver : 0;
            vLength = Math.Max(1, Math.Min(vLength, maxLength));
            var vAlpha = 2 / (vLength + 1);

            var prevBama = GetLastOrDefault(bamaList);
            var bama = (vAlpha * currentValue) + ((1 - vAlpha) * prevBama);
            bamaList.Add(bama);

            var signal = GetCompareSignal(currentValue - bama, prevValue - prevBama);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Bama", bamaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(bamaList);
        stockData.IndicatorName = IndicatorName.BryantAdaptiveMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Compound Ratio Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateCompoundRatioMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage, 
        int length = 20)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        List<double> line = new(stockData.Count); List<Signal>? signals = CreateSignalsList(stockData);
        using var window = new CompoundRatioWindow(maType, length, initializeFallback: false);
        if (Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType))
        {
            var raw = input.Select(price => window.Raw(price, true)).ToList();
            line = Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(raw), CompoundRatioWindow.SmoothPeriod(length))?.ToList() ?? GetMovingAverageList(stockData, maType, CompoundRatioWindow.SmoothPeriod(length), raw);
        }
        else foreach (var price in input) line.Add(window.Next(price, true));
        for (var i = 0; i < input.Count; i++) signals?.Add(GetCompareSignal(input[i] - line[i], i == 0 ? 0 : input[i - 1] - line[i - 1]));
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Crma", line } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.CompoundRatioMovingAverage;
        return stockData;
    }


    /// <summary>
    /// Calculates the Cubed Weighted Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateCubedWeightedMovingAverage(this StockData stockData, int length = 14)
    {
        List<double> cwmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        using var mean = new IntegerPowerWindowMean(length, 3);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            var prevCwma = GetLastOrDefault(cwmaList);
            var cwma = mean.Next(currentValue, true);
            cwmaList.Add(cwma);

            var signal = GetCompareSignal(currentValue - cwma, prevVal - prevCwma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Cwma", cwmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(cwmaList);
        stockData.IndicatorName = IndicatorName.CubedWeightedMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Corrected Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateCorrectedMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 35)
    {
        List<double> cmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);
        // Uhl's v1 is the variance of the source over the window: a plain population variance of the prices.
        var stdDevList = GetStandardDeviationList(inputList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var sma = smaList[i];
            var prevCma = i >= 1 ? cmaList[i - 1] : sma;
            var v1 = stdDevList[i] * stdDevList[i];
            var v2 = Pow(prevCma - sma, 2);
            // Exact attracting fixed point; truncating the iteration leaves a spurious gain
            // when the variance is at or above the squared displacement.
            var k = v1 == 0 ? 1 : v2 <= v1 ? 0 : 1 - v1 / v2;

            // Seeded at the average until the window is full, as the original's na(cma[1]) ? sma.
            var cma = i < length ? sma : prevCma + (k * (sma - prevCma));
            cmaList.Add(cma);

            var signal = GetCompareSignal(currentValue - cma, prevValue - prevCma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Cma", cmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(cmaList);
        stockData.IndicatorName = IndicatorName.CorrectedMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Double Exponential Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateDoubleExponentialMovingAverage(this StockData stockData, 
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14)
    {
        List<double> demaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var ema1List = GetMovingAverageList(stockData, maType, length, inputList);
        var ema2List = GetMovingAverageList(stockData, maType, length, ema1List);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentEma = ema1List[i];
            var currentEma2 = ema2List[i];

            var prevDema = GetLastOrDefault(demaList);
            var dema = ExponentialExtrapolation.Double(currentEma, currentEma2);
            demaList.Add(dema);

            var signal = GetCompareSignal(currentValue - dema, prevValue - prevDema);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Dema", demaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(demaList);
        stockData.IndicatorName = IndicatorName.DoubleExponentialMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Damped Sine Wave Weighted Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateDampedSineWaveWeightedFilter(this StockData stockData, int length = 50)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        using var window = new DampedSineWindow(length);
        List<double> line = new(stockData.Count); List<Signal>? signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            var value = window.Next(input[i], true);
            signals?.Add(GetCompareSignal(input[i] - value, i == 0 ? 0 : input[i - 1] - line[i - 1])); line.Add(value);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Dswwf", line } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.DampedSineWaveWeightedFilter;
        return stockData;
    }


    /// <summary>
    /// Calculates the Double Exponential Smoothing
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="alpha"></param>
    /// <param name="gamma"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateDoubleExponentialSmoothing(this StockData stockData, double alpha = 0.01, double gamma = 0.9)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        var window = new DoubleSmoothingWindow(alpha, gamma);
        List<double> line = new(stockData.Count); List<Signal>? signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            var value = window.Next(input[i], true);
            signals?.Add(GetCompareSignal(input[i] - value, i == 0 ? 0 : input[i - 1] - line[i - 1])); line.Add(value);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Des", line } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.DoubleExponentialSmoothing;
        return stockData;
    }


    /// <summary>
    /// Calculates the Distance Weighted Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateDistanceWeightedMovingAverage(this StockData stockData, int length = 14)
    {
        List<double> dwmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        using var mean = new DistanceMassWindowMean(length, reciprocal: true);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            var prevDwma = GetLastOrDefault(dwmaList);
            var dwma = mean.Next(currentValue, true);
            dwmaList.Add(dwma);

            var signal = GetCompareSignal(currentValue - dwma, prevVal - prevDwma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Dwma", dwmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(dwmaList);
        stockData.IndicatorName = IndicatorName.DistanceWeightedMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Dynamically Adjustable Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateDynamicallyAdjustableFilter(this StockData stockData, int length = 14)
    {
        List<double> outList = new(stockData.Count);
        List<double> kList = new(stockData.Count);
        List<double> srcList = new(stockData.Count);
        List<double> srcDevList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum srcSumWindow = new();
        RollingSum srcDevSumWindow = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevOut = i >= 1 ? outList[i - 1] : currentValue;
            var prevK = i >= 1 ? kList[i - 1] : 0;

            var src = currentValue + (currentValue - prevOut);
            srcList.Add(src);
            srcSumWindow.Add(src);

            var outVal = prevOut + (prevK * (src - prevOut));
            outList.Add(outVal);

            var srcSma = srcSumWindow.Average(length);
            var srcDev = Pow(src - srcSma, 2);
            srcDevList.Add(srcDev);
            srcDevSumWindow.Add(srcDev);

            var srcStdDev = Sqrt(srcDevSumWindow.Average(length));
            var k = src - outVal != 0 ? Math.Abs(src - outVal) / (Math.Abs(src - outVal) + (srcStdDev * length)) : 0;
            kList.Add(k);

            var signal = GetCompareSignal(currentValue - outVal, prevValue - prevOut);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Daf", outList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(outList);
        stockData.IndicatorName = IndicatorName.DynamicallyAdjustableFilter;

        return stockData;
    }


    /// <summary>
    /// Calculates the Dynamically Adjustable Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateDynamicallyAdjustableMovingAverage(this StockData stockData, int fastLength = 6, int slowLength = 200)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        var window = new DynamicAverageWindow(fastLength, slowLength);
        List<double> output = new(stockData.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < stockData.Count; i++)
        {
            var value = window.Next(input[i], true); output.Add(value);
            signals?.Add(GetCompareSignal(input[i] - value, i == 0 ? 0 : input[i - 1] - output[i - 1]));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Dama", output } });
        stockData.SetSignals(signals); stockData.SetCustomValues(output);
        stockData.IndicatorName = IndicatorName.DynamicallyAdjustableMovingAverage;
        return stockData;
    }

}

