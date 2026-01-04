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
    public static StockData CalculateArnaudLegouxMovingAverage(this StockData stockData, int length = 9, double offset = 0.85, int sigma = 6)
    {
        List<double> almaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var m = offset * (length - 1);
        var s = (double)length / sigma;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            double sum = 0, weightedSum = 0;
            for (var j = 0; j <= length - 1; j++)
            {
                var weight = s != 0 ? Exp(-1 * Pow(j - m, 2) / (2 * Pow(s, 2))) : 0;
                var prevValue = i >= length - 1 - j ? inputList[i - (length - 1 - j)] : 0;

                sum += prevValue * weight;
                weightedSum += weight;
            }

            var prevAlma = GetLastOrDefault(almaList);
            var alma = weightedSum != 0 ? sum / weightedSum : 0;
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
    public static StockData CalculateAhrensMovingAverage(this StockData stockData, int length = 9)
    {
        List<double> ahmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var priorAhma = i >= length ? ahmaList[i - length] : currentValue;

            var prevAhma = GetLastOrDefault(ahmaList);
            var ahma = prevAhma + ((currentValue - ((prevAhma + priorAhma) / 2)) / length);
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
    public static StockData CalculateAdaptiveAutonomousRecursiveMovingAverage(this StockData stockData, int length = 14, double gamma = 3)
    {
        List<double> ma1List = new(stockData.Count);
        List<double> ma2List = new(stockData.Count);
        List<double> absDiffList = new(stockData.Count);
        List<double> dList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        double absDiffSum = 0;
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var erList = CalculateKaufmanAdaptiveMovingAverage(stockData, length: length).OutputValues["Er"];

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
            var priorValue = i >= length ? inputList[i - momLength] : 0;
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
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var tr = CalculateTrueRange(currentHigh, currentLow, prevValue);

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

            var stdDev = stdDevA - stdDevB >= 0 ? Sqrt(stdDevA - stdDevB) : 0;
            stdDevList.Add(stdDev);
            stdDevWindow.Add(stdDev);

            var stdDevLow = stdDevWindow.Min;
            var stdDevFactorAFP = stdDev != 0 ? stdDevLow / stdDev : 0;
            var stdDevFactorCTP = stdDevLow != 0 ? stdDev / stdDevLow : 0;
            var stdDevFactorAFPLow = Math.Min(stdDevFactorAFP, min);
            var stdDevFactorCTPLow = Math.Min(stdDevFactorCTP, min);
            var alphaAfp = (2 * stdDevFactorAFPLow) / (length + 1);
            var alphaCtp = (2 * stdDevFactorCTPLow) / (length + 1);

            var prevEmaAfp = GetLastOrDefault(emaAFPList);
            var emaAfp = (alphaAfp * currentValue) + ((1 - alphaAfp) * prevEmaAfp);
            emaAFPList.Add(emaAfp);

            var prevEmaCtp = GetLastOrDefault(emaCTPList);
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
    public static StockData CalculateAdaptiveLeastSquares(this StockData stockData, int length = 500, double smooth = 1.5)
    {
        List<double> xList = new(stockData.Count);
        List<double> yList = new(stockData.Count);
        List<double> mxList = new(stockData.Count);
        List<double> myList = new(stockData.Count);
        List<double> regList = new(stockData.Count);
        List<double> tempList = new(stockData.Count);
        List<double> mxxList = new(stockData.Count);
        List<double> myyList = new(stockData.Count);
        List<double> mxyList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingMinMax trWindow = new(length);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            double index = i;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentHigh = highList[i];
            var currentLow = lowList[i];

            var tr = CalculateTrueRange(currentHigh, currentLow, prevValue);
            tempList.Add(tr);
            trWindow.Add(tr);

            var highest = trWindow.Max;
            var alpha = highest != 0 ? MinOrMax(Pow(tr / highest, smooth), 0.99, 0.01) : 0.01;
            var xx = index * index;
            var yy = currentValue * currentValue;
            var xy = index * currentValue;

            var prevX = i >= 1 ? xList[i - 1] : index;
            var x = (alpha * index) + ((1 - alpha) * prevX);
            xList.Add(x);

            var prevY = i >= 1 ? yList[i - 1] : currentValue;
            var y = (alpha * currentValue) + ((1 - alpha) * prevY);
            yList.Add(y);

            var dx = Math.Abs(index - x);
            var dy = Math.Abs(currentValue - y);

            var prevMx = i >= 1 ? mxList[i - 1] : dx;
            var mx = (alpha * dx) + ((1 - alpha) * prevMx);
            mxList.Add(mx);

            var prevMy = i >= 1 ? myList[i - 1] : dy;
            var my = (alpha * dy) + ((1 - alpha) * prevMy);
            myList.Add(my);

            var prevMxx = i >= 1 ? mxxList[i - 1] : xx;
            var mxx = (alpha * xx) + ((1 - alpha) * prevMxx);
            mxxList.Add(mxx);

            var prevMyy = i >= 1 ? myyList[i - 1] : yy;
            var myy = (alpha * yy) + ((1 - alpha) * prevMyy);
            myyList.Add(myy);

            var prevMxy = i >= 1 ? mxyList[i - 1] : xy;
            var mxy = (alpha * xy) + ((1 - alpha) * prevMxy);
            mxyList.Add(mxy);

            var alphaVal = (2 / alpha) + 1;
            var a1 = alpha != 0 ? (Pow(alphaVal, 2) * mxy) - (alphaVal * mx * alphaVal * my) : 0;
            var tempVal = ((Pow(alphaVal, 2) * mxx) - Pow(alphaVal * mx, 2)) * ((Pow(alphaVal, 2) * myy) - Pow(alphaVal * my, 2));
            var b1 = tempVal >= 0 ? Sqrt(tempVal) : 0;
            var r = b1 != 0 ? a1 / b1 : 0;
            var a = mx != 0 ? r * (my / mx) : 0;
            var b = y - (a * x);

            var prevReg = GetLastOrDefault(regList);
            var reg = (x * a) + b;
            regList.Add(reg);

            var signal = GetCompareSignal(currentValue - reg, prevValue - prevReg);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Als", regList }
        });
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
    public static StockData CalculateAlphaDecreasingExponentialMovingAverage(this StockData stockData)
    {
        List<double> emaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var alpha = (double)2 / (i + 1);

            var prevEma = GetLastOrDefault(emaList);
            var ema = (alpha * currentValue) + ((1 - alpha) * prevEma);
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
        var devList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;

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
        var mxList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
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
    public static StockData CalculateAutoLine(this StockData stockData, int length = 500)
    {
        List<double> xList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var devList = CalculateStandardDeviationVolatility(stockData, length: length).CustomValuesList;

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
    public static StockData CalculateAutoLineWithDrift(this StockData stockData, int length = 500)
    {
        List<double> aList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var stdDevList = CalculateStandardDeviationVolatility(stockData, length: length).CustomValuesList;

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
        var stdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;

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
    public static StockData Calculate3HMA(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 50)
    {
        List<double> midList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var p = MinOrMax((int)Math.Ceiling((double)length / 2));
        var p1 = MinOrMax((int)Math.Ceiling((double)p / 3));
        var p2 = MinOrMax((int)Math.Ceiling((double)p / 2));

        var wma1List = GetMovingAverageList(stockData, maType, p1, inputList);
        var wma2List = GetMovingAverageList(stockData, maType, p2, inputList);
        var wma3List = GetMovingAverageList(stockData, maType, p, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var wma1 = wma1List[i];
            var wma2 = wma2List[i];
            var wma3 = wma3List[i];

            var mid = (wma1 * 3) - wma2 - wma3;
            midList.Add(mid);
        }

        var aList = GetMovingAverageList(stockData, maType, p, midList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var a = aList[i];
            var prevA = i >= 1 ? aList[i - 1] : 0;
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var signal = GetCompareSignal(currentValue - a, prevValue - prevA);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "3hma", aList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(aList);
        stockData.IndicatorName = IndicatorName._3HMA;

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
    public static StockData CalculateBryantAdaptiveMovingAverage(this StockData stockData, int length = 14, int maxLength = 100, double trend = -1)
    {
        List<double> bamaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var erList = CalculateKaufmanAdaptiveMovingAverage(stockData, length: length).OutputValues["Er"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var er = erList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var ver = Pow(er - (((2 * er) - 1) / 2 * (1 - trend)) + 0.5, 2);
            var vLength = ver != 0 ? (length - ver + 1) / ver : 0;
            vLength = Math.Min(vLength, maxLength);
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
    public static StockData CalculateCompoundRatioMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage, 
        int length = 20)
    {
        List<double> coraRawList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var r = Pow(length, ((double)1 / (length - 1)) - 1);
        var smoothLength = Math.Max((int)Math.Round(Math.Sqrt(length)), 1);

        for (var i = 0; i < stockData.Count; i++)
        {
            double sum = 0, weightedSum = 0, bas = 1 + (r * 2);
            for (var j = 0; j <= length - 1; j++)
            {
                var weight = Pow(bas, length - i);
                var prevValue = i >= j ? inputList[i - j] : 0;

                sum += prevValue * weight;
                weightedSum += weight;
            }

            var coraRaw = weightedSum != 0 ? sum / weightedSum : 0;
            coraRawList.Add(coraRaw);
        }

        var coraWaveList = GetMovingAverageList(stockData, maType, smoothLength, coraRawList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var coraWave = coraWaveList[i];
            var prevCoraWave = i >= 1 ? coraWaveList[i - 1] : 0;

            var signal = GetCompareSignal(currentValue - coraWave, prevValue - prevCoraWave);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Crma", coraWaveList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(coraWaveList);
        stockData.IndicatorName = IndicatorName.CompoundRatioMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Cubed Weighted Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateCubedWeightedMovingAverage(this StockData stockData, int length = 14)
    {
        List<double> cwmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            double sum = 0, weightedSum = 0;
            for (var j = 0; j <= length - 1; j++)
            {
                var weight = Pow(length - j, 3);
                var prevValue = i >= j ? inputList[i - j] : 0;

                sum += prevValue * weight;
                weightedSum += weight;
            }

            var prevCwma = GetLastOrDefault(cwmaList);
            var cwma = weightedSum != 0 ? sum / weightedSum : 0;
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
    public static StockData CalculateCorrectedMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 35)
    {
        List<double> cmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);
        var v1List = CalculateStandardDeviationVolatility(stockData, maType, length).OutputValues["Variance"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var sma = smaList[i];
            var prevCma = i >= 1 ? cmaList[i - 1] : sma;
            var v1 = v1List[i];
            var v2 = Pow(prevCma - sma, 2);
            var v3 = v1 == 0 || v2 == 0 ? 1 : v2 / (v1 + v2);

            double tolerance = Pow(10, -5), err = 1, kPrev = 1, k = 1;
            for (var j = 0; j <= 5000; j++)
            {
                if (err > tolerance)
                {
                    k = v3 * kPrev * (2 - kPrev);
                    err = kPrev - k;
                    kPrev = k;
                }
            }

            var cma = prevCma + (k * (sma - prevCma));
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
            var dema = (2 * currentEma) - currentEma2;
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
    public static StockData CalculateDampedSineWaveWeightedFilter(this StockData stockData, int length = 50)
    {
        List<double> dswwfList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            double w, wSum = 0, wvSum = 0;
            for (var j = 1; j <= length; j++)
            {
                var prevValue = i >= j - 1 ? inputList[i - (j - 1)] : 0;

                w = Math.Sin(MinOrMax(2 * Math.PI * ((double)j / length), 0.99, 0.01)) / j;
                wvSum += w * prevValue;
                wSum += w;
            }

            var prevDswwf = GetLastOrDefault(dswwfList);
            var dswwf = wSum != 0 ? wvSum / wSum : 0;
            dswwfList.Add(dswwf);

            var signal = GetCompareSignal(currentValue - dswwf, prevVal - prevDswwf);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Dswwf", dswwfList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(dswwfList);
        stockData.IndicatorName = IndicatorName.DampedSineWaveWeightedFilter;

        return stockData;
    }


    /// <summary>
    /// Calculates the Double Exponential Smoothing
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="alpha"></param>
    /// <param name="gamma"></param>
    /// <returns></returns>
    public static StockData CalculateDoubleExponentialSmoothing(this StockData stockData, double alpha = 0.01, double gamma = 0.9)
    {
        List<double> sList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var x = inputList[i];
            var prevX = i >= 1 ? inputList[i - 1] : 0;
            var prevS = i >= 1 ? sList[i - 1] : 0;
            var prevS2 = i >= 2 ? sList[i - 2] : 0;
            var sChg = prevS - prevS2;

            var s = (alpha * x) + ((1 - alpha) * (prevS + (gamma * (sChg + ((1 - gamma) * sChg)))));
            sList.Add(s);

            var signal = GetCompareSignal(x - s, prevX - prevS);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Des", sList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(sList);
        stockData.IndicatorName = IndicatorName.DoubleExponentialSmoothing;

        return stockData;
    }


    /// <summary>
    /// Calculates the Distance Weighted Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateDistanceWeightedMovingAverage(this StockData stockData, int length = 14)
    {
        List<double> dwmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            double sum = 0, weightedSum = 0;
            for (var j = 0; j < length; j++)
            {
                var prevValue = i >= j ? inputList[i - j] : 0;

                double distanceSum = 0;
                for (var k = 0; k < length; k++)
                {
                    var prevValue2 = i >= k ? inputList[i - k] : 0;

                    distanceSum += Math.Abs(prevValue - prevValue2);
                }

                var weight = distanceSum != 0 ? 1 / distanceSum : 0;

                sum += prevValue * weight;
                weightedSum += weight;
            }

            var prevDwma = GetLastOrDefault(dwmaList);
            var dwma = weightedSum != 0 ? sum / weightedSum : 0;
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
    public static StockData CalculateDynamicallyAdjustableMovingAverage(this StockData stockData, int fastLength = 6, int slowLength = 200)
    {
        List<double> kList = new(stockData.Count);
        List<double> amaList = new(stockData.Count);
        List<double> tempList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        double tempSum = 0;
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var shortStdDevList = CalculateStandardDeviationVolatility(stockData, length: fastLength).CustomValuesList;
        var longStdDevList = CalculateStandardDeviationVolatility(stockData, length: slowLength).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var a = shortStdDevList[i];
            var b = longStdDevList[i];
            var v = a != 0 ? (b / a) + fastLength : fastLength;

            var prevValue = GetLastOrDefault(tempList);
            var currentValue = inputList[i];
            tempList.Add(currentValue);
            tempSum += currentValue;

            var p = (int)Math.Round(MinOrMax(v, slowLength, fastLength));       
            var prevK = i >= p ? kList[i - p] : 0;
            var k = tempSum;
            kList.Add(k);

            var prevAma = GetLastOrDefault(amaList);
            var ama = p != 0 ? (k - prevK) / p : 0;
            amaList.Add(ama);

            var signal = GetCompareSignal(currentValue - ama, prevValue - prevAma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Dama", amaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(amaList);
        stockData.IndicatorName = IndicatorName.DynamicallyAdjustableMovingAverage;

        return stockData;
    }

}

