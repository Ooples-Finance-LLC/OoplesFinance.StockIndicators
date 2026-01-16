using OoplesFinance.StockIndicators.Compatibility;
using OoplesFinance.StockIndicators.Core;

namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the powered kaufman adaptive moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <param name="factor">The factor.</param>
    /// <returns></returns>
    public static StockData CalculatePoweredKaufmanAdaptiveMovingAverage(this StockData stockData, int length = 100, double factor = 3)
    {
        List<double> aList = new(stockData.Count);
        List<double> aSpList = new(stockData.Count);
        List<double> perList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var erList = CalculateKaufmanAdaptiveMovingAverage(stockData, length: length).OutputValues["Er"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var er = erList[i];
            var powSp = er != 0 ? 1 / er : factor;
            var perSp = Pow(er, powSp);

            var per = Pow(er, factor);
            perList.Add(per);

            var prevA = i >= 1 ? GetLastOrDefault(aList) : currentValue;
            var a = (per * currentValue) + ((1 - per) * prevA);
            aList.Add(a);

            var prevASp = i >= 1 ? GetLastOrDefault(aSpList) : currentValue;
            var aSp = (perSp * currentValue) + ((1 - perSp) * prevASp);
            aSpList.Add(aSp);

            var signal = GetCompareSignal(currentValue - a, prevValue - prevA);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Per", perList },
            { "Pkama", aList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(aList);
        stockData.IndicatorName = IndicatorName.PoweredKaufmanAdaptiveMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Quick Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateQuickMovingAverage(this StockData stockData, int length = 14)
    {
        List<double> qmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var peak = MinOrMax((int)Math.Ceiling((double)length / 3));

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            double num = 0, denom = 0;
            for (var j = 1; j <= length + 1; j++)
            {
                var mult = j <= peak ? (double)j / peak : (double)(length + 1 - j) / (length + 1 - peak);
                var prevValue = i >= j - 1 ? inputList[i - (j - 1)] : 0;

                num += prevValue * mult;
                denom += mult;
            }

            var prevQma = GetLastOrDefault(qmaList);
            var qma = denom != 0 ? num / denom : 0;
            qmaList.Add(qma);

            var signal = GetCompareSignal(currentValue - qma, prevVal - prevQma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Qma", qmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(qmaList);
        stockData.IndicatorName = IndicatorName.QuickMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Quadratic Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateQuadraticMovingAverage(this StockData stockData, int length = 14)
    {
        List<double> qmaList = new(stockData.Count);
        List<double> powList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum powSumWindow = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var pow = Pow(currentValue, 2);
            powList.Add(pow);
            powSumWindow.Add(pow);

            var prevQma = GetLastOrDefault(qmaList);
            var powSma = powSumWindow.Average(length);
            var qma = powSma >= 0 ? Sqrt(powSma) : 0;
            qmaList.Add(qma);

            var signal = GetCompareSignal(currentValue - qma, prevValue - prevQma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Qma", qmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(qmaList);
        stockData.IndicatorName = IndicatorName.QuadraticMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Quadruple Exponential Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateQuadrupleExponentialMovingAverage(this StockData stockData,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 20)
    {
        List<double> qemaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var ema1List = GetMovingAverageList(stockData, maType, length, inputList);
        var ema2List = GetMovingAverageList(stockData, maType, length, ema1List);
        var ema3List = GetMovingAverageList(stockData, maType, length, ema2List);
        var ema4List = GetMovingAverageList(stockData, maType, length, ema3List);
        var ema5List = GetMovingAverageList(stockData, maType, length, ema4List);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var ema1 = ema1List[i];
            var ema2 = ema2List[i];
            var ema3 = ema3List[i];
            var ema4 = ema4List[i];
            var ema5 = ema5List[i];

            var prevQema = GetLastOrDefault(qemaList);
            var qema = (5 * ema1) - (10 * ema2) + (10 * ema3) - (5 * ema4) + ema5;
            qemaList.Add(qema);

            var signal = GetCompareSignal(currentValue - qema, prevValue - prevQema);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Qema", qemaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(qemaList);
        stockData.IndicatorName = IndicatorName.QuadrupleExponentialMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Quadratic Least Squares Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="forecastLength"></param>
    /// <returns></returns>
    public static StockData CalculateQuadraticLeastSquaresMovingAverage(this StockData stockData,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 50, int forecastLength = 14)
    {
        List<double> nList = new(stockData.Count);
        List<double> n2List = new(stockData.Count);
        List<double> nn2List = new(stockData.Count);
        List<double> nn2CovList = new(stockData.Count);
        List<double> n2vList = new(stockData.Count);
        List<double> n2vCovList = new(stockData.Count);
        List<double> nvList = new(stockData.Count);
        List<double> nvCovList = new(stockData.Count);
        List<double> qlsmaList = new(stockData.Count);
        List<double> fcastList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];

            double n = i;
            nList.Add(n);

            var n2 = Pow(n, 2);
            n2List.Add(n2);

            var nn2 = n * n2;
            nn2List.Add(nn2);

            var n2v = n2 * currentValue;
            n2vList.Add(n2v);

            var nv = n * currentValue;
            nvList.Add(nv);
        }

        var nSmaList = GetMovingAverageList(stockData, maType, length, nList);
        var n2SmaList = GetMovingAverageList(stockData, maType, length, n2List);
        var n2vSmaList = GetMovingAverageList(stockData, maType, length, n2vList);
        var nvSmaList = GetMovingAverageList(stockData, maType, length, nvList);
        var nn2SmaList = GetMovingAverageList(stockData, maType, length, nn2List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var nSma = nSmaList[i];
            var n2Sma = n2SmaList[i];
            var n2vSma = n2vSmaList[i];
            var nvSma = nvSmaList[i];
            var nn2Sma = nn2SmaList[i];
            var sma = smaList[i];

            var nn2Cov = nn2Sma - (nSma * n2Sma);
            nn2CovList.Add(nn2Cov);

            var n2vCov = n2vSma - (n2Sma * sma);
            n2vCovList.Add(n2vCov);

            var nvCov = nvSma - (nSma * sma);
            nvCovList.Add(nvCov);
        }

        stockData.SetCustomValues(nList);
        var nVarianceList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        stockData.SetCustomValues(n2List);
        var n2VarianceList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var n2Variance = n2VarianceList[i];
            var nVariance = nVarianceList[i];
            var nn2Cov = nn2CovList[i];
            var n2vCov = n2vCovList[i];
            var nvCov = nvCovList[i];
            var sma = smaList[i];
            var n2Sma = n2SmaList[i];
            var nSma = nSmaList[i];
            var n2 = n2List[i];
            var norm = (n2Variance * nVariance) - Pow(nn2Cov, 2);
            var a = norm != 0 ? ((n2vCov * nVariance) - (nvCov * nn2Cov)) / norm : 0;
            var b = norm != 0 ? ((nvCov * n2Variance) - (n2vCov * nn2Cov)) / norm : 0;
            var c = sma - (a * n2Sma) - (b * nSma);

            var prevQlsma = GetLastOrDefault(qlsmaList);
            var qlsma = (a * n2) + (b * i) + c;
            qlsmaList.Add(qlsma);

            var fcast = (a * Pow(i + forecastLength, 2)) + (b * (i + forecastLength)) + c;
            fcastList.Add(fcast);

            var signal = GetCompareSignal(currentValue - qlsma, prevValue - prevQlsma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Qlma", qlsmaList },
            { "Forecast", fcastList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(qlsmaList);
        stockData.IndicatorName = IndicatorName.QuadraticLeastSquaresMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Quadratic Regression
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateQuadraticRegression(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 500)
    {
        List<double> tempList = new(stockData.Count);
        List<double> x1List = new(stockData.Count);
        List<double> x2List = new(stockData.Count);
        List<double> x1SumList = new(stockData.Count);
        List<double> x2SumList = new(stockData.Count);
        List<double> x1x2List = new(stockData.Count);
        List<double> x1x2SumList = new(stockData.Count);
        List<double> x2PowList = new(stockData.Count);
        List<double> x2PowSumList = new(stockData.Count);
        List<double> ySumList = new(stockData.Count);
        List<double> yx1List = new(stockData.Count);
        List<double> yx2List = new(stockData.Count);
        List<double> yx1SumList = new(stockData.Count);
        List<double> yx2SumList = new(stockData.Count);
        List<double> yList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum ySumWindow = new();
        RollingSum x1SumWindow = new();
        RollingSum x2SumWindow = new();
        RollingSum x1x2SumWindow = new();
        RollingSum yx1SumWindow = new();
        RollingSum yx2SumWindow = new();
        RollingSum x2PowSumWindow = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var y = inputList[i];
            tempList.Add(y);
            ySumWindow.Add(y);

            double x1 = i;
            x1List.Add(x1);
            x1SumWindow.Add(x1);

            var x2 = Pow(x1, 2);
            x2List.Add(x2);
            x2SumWindow.Add(x2);

            var x1x2 = x1 * x2;
            x1x2List.Add(x1x2);
            x1x2SumWindow.Add(x1x2);

            var yx1 = y * x1;
            yx1List.Add(yx1);
            yx1SumWindow.Add(yx1);

            var yx2 = y * x2;
            yx2List.Add(yx2);
            yx2SumWindow.Add(yx2);

            var x2Pow = Pow(x2, 2);
            x2PowList.Add(x2Pow);
            x2PowSumWindow.Add(x2Pow);

            var ySum = ySumWindow.Sum(length);
            ySumList.Add(ySum);

            var x1Sum = x1SumWindow.Sum(length);
            x1SumList.Add(x1Sum);

            var x2Sum = x2SumWindow.Sum(length);
            x2SumList.Add(x2Sum);

            var x1x2Sum = x1x2SumWindow.Sum(length);
            x1x2SumList.Add(x1x2Sum);

            var yx1Sum = yx1SumWindow.Sum(length);
            yx1SumList.Add(yx1Sum);

            var yx2Sum = yx2SumWindow.Sum(length);
            yx2SumList.Add(yx2Sum);

            var x2PowSum = x2PowSumWindow.Sum(length);
            x2PowSumList.Add(x2PowSum);
        }

        var max1List = GetMovingAverageList(stockData, maType, length, x1List);
        var max2List = GetMovingAverageList(stockData, maType, length, x2List);
        var mayList = GetMovingAverageList(stockData, maType, length, inputList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var x1Sum = x1SumList[i];
            var x2Sum = x2SumList[i];
            var x1x2Sum = x1x2SumList[i];
            var x2PowSum = x2PowSumList[i];
            var yx1Sum = yx1SumList[i];
            var yx2Sum = yx2SumList[i];
            var ySum = ySumList[i];
            var may = mayList[i];
            var max1 = max1List[i];
            var max2 = max2List[i];
            var x1 = x1List[i];
            var x2 = x2List[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var s11 = x2Sum - (Pow(x1Sum, 2) / length);
            var s12 = x1x2Sum - ((x1Sum * x2Sum) / length);
            var s22 = x2PowSum - (Pow(x2Sum, 2) / length);
            var sy1 = yx1Sum - ((ySum * x1Sum) / length);
            var sy2 = yx2Sum - ((ySum * x2Sum) / length);
            var bot = (s22 * s11) - Pow(s12, 2);
            var b2 = bot != 0 ? ((sy1 * s22) - (sy2 * s12)) / bot : 0;
            var b3 = bot != 0 ? ((sy2 * s11) - (sy1 * s12)) / bot : 0;
            var b1 = may - (b2 * max1) - (b3 * max2);

            var prevY = GetLastOrDefault(yList);
            var y = b1 + (b2 * x1) + (b3 * x2);
            yList.Add(y);

            var signal = GetCompareSignal(currentValue - y, prevValue - prevY);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "QuadReg", yList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(yList);
        stockData.IndicatorName = IndicatorName.QuadraticRegression;

        return stockData;
    }


    /// <summary>
    /// Calculates the Optimal Weighted Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateOptimalWeightedMovingAverage(this StockData stockData, int length = 14)
    {
        List<double> tempList = new(stockData.Count);
        List<double> owmaList = new(stockData.Count);
        List<double> prevOwmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingCorrelation corrWindow = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevVal = GetLastOrDefault(tempList);
            var currentValue = inputList[i];
            tempList.Add(currentValue);

            var prevOwma = i >= 1 ? owmaList[i - 1] : 0;
            prevOwmaList.Add(prevOwma);

            corrWindow.Add(currentValue, prevOwma);
            var corr = corrWindow.R(length);
            corr = IsValueNullOrInfinity(corr) ? 0 : corr;

            double sum = 0, weightedSum = 0;
            for (var j = 0; j <= length - 1; j++)
            {
                var weight = Pow(length - j, (double)corr);
                var prevValue = i >= j ? inputList[i - j] : 0;

                sum += prevValue * weight;
                weightedSum += weight;
            }

            var owma = weightedSum != 0 ? sum / weightedSum : 0;
            owmaList.Add(owma);

            var signal = GetCompareSignal(currentValue - owma, prevVal - prevOwma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Owma", owmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(owmaList);
        stockData.IndicatorName = IndicatorName.OptimalWeightedMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Overshoot Reduction Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateOvershootReductionMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 14)
    {
        List<double> indexList = new(stockData.Count);
        List<double> bList = new(stockData.Count);
        List<double> dList = new(stockData.Count);
        List<double> bSmaList = new(stockData.Count);
        List<double> corrList = new(stockData.Count);
        List<double> tempList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingCorrelation corrWindow = new();
        RollingSum bSumWindow = new();
        RollingMinMax bSmaWindow = new(length);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var length1 = (int)Math.Ceiling((double)length / 2);

        for (var i = 0; i < stockData.Count; i++)
        {
            double index = i;
            indexList.Add(index);

            var currentValue = inputList[i];
            tempList.Add(currentValue);

            corrWindow.Add(index, currentValue);
            var corr = corrWindow.R(length);
            corr = IsValueNullOrInfinity(corr) ? 0 : corr;
            corrList.Add((double)corr);
        }

        var indexSmaList = GetMovingAverageList(stockData, maType, length, indexList);
        var smaList = GetMovingAverageList(stockData, maType, length, inputList);
        var stdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        stockData.SetCustomValues(indexList);
        var indexStdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var index = indexList[i];
            var indexSma = indexSmaList[i];
            var indexStdDev = indexStdDevList[i];
            var corr = corrList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevD = i >= 1 ? dList[i - 1] != 0 ? dList[i - 1] : prevValue : prevValue;
            var sma = smaList[i];
            var stdDev = stdDevList[i];
            var a = indexStdDev != 0 && corr != 0 ? (index - indexSma) / indexStdDev * corr : 0;

            var b = Math.Abs(prevD - currentValue);
            bList.Add(b);
            bSumWindow.Add(b);

            var bSma = bSumWindow.Average(length1);
            bSmaList.Add(bSma);
            bSmaWindow.Add(bSma);

            var highest = bSmaWindow.Max;
            var c = highest != 0 ? b / highest : 0;

            var d = sma + (a * (stdDev * c));
            dList.Add(d);

            var signal = GetCompareSignal(currentValue - d, prevValue - prevD);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Orma", dList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(dList);
        stockData.IndicatorName = IndicatorName.OvershootReductionMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Natural Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateNaturalMovingAverage(this StockData stockData, int length = 40)
    {
        List<double> lnList = new(stockData.Count);
        List<double> nmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var ln = currentValue > 0 ? Math.Log(currentValue) * 1000 : 0;
            lnList.Add(ln);

            double num = 0, denom = 0;
            for (var j = 0; j < length; j++)
            {
                var currentLn = i >= j ? lnList[i - j] : 0;
                var prevLn = i >= j + 1 ? lnList[i - (j + 1)] : 0;
                var oi = Math.Abs(currentLn - prevLn);
                num += oi * (Sqrt(j + 1) - Sqrt(j));
                denom += oi;
            }

            var ratio = denom != 0 ? num / denom : 0;
            var prevNma = GetLastOrDefault(nmaList);
            var nma = (currentValue * ratio) + (prevValue * (1 - ratio));
            nmaList.Add(nma);

            var signal = GetCompareSignal(currentValue - nma, prevValue - prevNma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Nma", nmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(nmaList);
        stockData.IndicatorName = IndicatorName.NaturalMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the McNicholl Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateMcNichollMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 20)
    {
        List<double> mnmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var alpha = (double)2 / (length + 1);

        var ema1List = GetMovingAverageList(stockData, maType, length, inputList);
        var ema2List = GetMovingAverageList(stockData, maType, length, ema1List);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var ema1 = ema1List[i];
            var ema2 = ema2List[i];

            var prevMnma = GetLastOrDefault(mnmaList);
            var mnma = 1 - alpha != 0 ? (((2 - alpha) * ema1) - ema2) / (1 - alpha) : 0;
            mnmaList.Add(mnma);

            var signal = GetCompareSignal(currentValue - mnma, prevValue - prevMnma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Mnma", mnmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(mnmaList);
        stockData.IndicatorName = IndicatorName.McNichollMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Pentuple Exponential Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculatePentupleExponentialMovingAverage(this StockData stockData, 
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 20)
    {
        List<double> pemaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var ema1List = GetMovingAverageList(stockData, maType, length, inputList);
        var ema2List = GetMovingAverageList(stockData, maType, length, ema1List);
        var ema3List = GetMovingAverageList(stockData, maType, length, ema2List);
        var ema4List = GetMovingAverageList(stockData, maType, length, ema3List);
        var ema5List = GetMovingAverageList(stockData, maType, length, ema4List);
        var ema6List = GetMovingAverageList(stockData, maType, length, ema5List);
        var ema7List = GetMovingAverageList(stockData, maType, length, ema6List);
        var ema8List = GetMovingAverageList(stockData, maType, length, ema7List);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var ema1 = ema1List[i];
            var ema2 = ema2List[i];
            var ema3 = ema3List[i];
            var ema4 = ema4List[i];
            var ema5 = ema5List[i];
            var ema6 = ema6List[i];
            var ema7 = ema7List[i];
            var ema8 = ema8List[i];

            var prevPema = GetLastOrDefault(pemaList);
            var pema = (8 * ema1) - (28 * ema2) + (56 * ema3) - (70 * ema4) + (56 * ema5) - (28 * ema6) + (8 * ema7) - ema8;
            pemaList.Add(pema);

            var signal = GetCompareSignal(currentValue - pema, prevValue - prevPema);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Pema", pemaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(pemaList);
        stockData.IndicatorName = IndicatorName.PentupleExponentialMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Polynomial Least Squares Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculatePolynomialLeastSquaresMovingAverage(this StockData stockData, int length = 100)
    {
        List<double> sumPow3List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            var prevSumPow3 = GetLastOrDefault(sumPow3List);
            double x1Pow1Sum, x2Pow1Sum, x1Pow2Sum, x2Pow2Sum, x1Pow3Sum, x2Pow3Sum, wPow1, wPow2, wPow3, sumPow1 = 0, sumPow2 = 0, sumPow3 = 0;
            for (var j = 1; j <= length; j++)
            {
                var prevValue = i >= j - 1 ? inputList[i - (j - 1)] : 0;
                var x1 = (double)j / length;
                var x2 = (double)(j - 1) / length;
                var ax1 = x1 * x1;
                var ax2 = x2 * x2;

                double b1Pow1Sum = 0, b2Pow1Sum = 0, b1Pow2Sum = 0, b2Pow2Sum = 0, b1Pow3Sum = 0, b2Pow3Sum = 0;
                for (var k = 1; k <= 3; k++)
                {
                    var b1 = (double)1 / k * Math.Sin(x1 * k * Math.PI);
                    var b2 = (double)1 / k * Math.Sin(x2 * k * Math.PI);

                    b1Pow1Sum += k == 1 ? b1 : 0;
                    b2Pow1Sum += k == 1 ? b2 : 0;
                    b1Pow2Sum += k <= 2 ? b1 : 0;
                    b2Pow2Sum += k <= 2 ? b2 : 0;
                    b1Pow3Sum += k <= 3 ? b1 : 0; //-V3022
                    b2Pow3Sum += k <= 3 ? b2 : 0; //-V3022
                }

                x1Pow1Sum = ax1 + b1Pow1Sum;
                x2Pow1Sum = ax2 + b2Pow1Sum;
                wPow1 = x1Pow1Sum - x2Pow1Sum;
                sumPow1 += prevValue * wPow1;
                x1Pow2Sum = ax1 + b1Pow2Sum;
                x2Pow2Sum = ax2 + b2Pow2Sum;
                wPow2 = x1Pow2Sum - x2Pow2Sum;
                sumPow2 += prevValue * wPow2;
                x1Pow3Sum = ax1 + b1Pow3Sum;
                x2Pow3Sum = ax2 + b2Pow3Sum;
                wPow3 = x1Pow3Sum - x2Pow3Sum;
                sumPow3 += prevValue * wPow3;
            }
            sumPow3List.Add(sumPow3);

            var signal = GetCompareSignal(currentValue - sumPow3, prevVal - prevSumPow3);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Plsma", sumPow3List }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(sumPow3List);
        stockData.IndicatorName = IndicatorName.PolynomialLeastSquaresMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Parametric Corrective Linear Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="alpha"></param>
    /// <param name="per"></param>
    /// <returns></returns>
    public static StockData CalculateParametricCorrectiveLinearMovingAverage(this StockData stockData, int length = 50, double alpha = 1,
        double per = 35)
    {
        List<double> w1List = new(stockData.Count);
        List<double> w2List = new(stockData.Count);
        List<double> vw1List = new(stockData.Count);
        List<double> vw2List = new(stockData.Count);
        List<double> rrma1List = new(stockData.Count);
        List<double> rrma2List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum w1SumWindow = new();
        RollingSum w2SumWindow = new();
        RollingSum vw1SumWindow = new();
        RollingSum vw2SumWindow = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevValue = i >= length ? inputList[i - length] : 0;
            var p1 = i + 1 - (per / 100 * length);
            var p2 = i + 1 - ((100 - per) / 100 * length);

            var w1 = p1 >= 0 ? p1 : alpha * p1;
            w1List.Add(w1);
            w1SumWindow.Add(w1);

            var w2 = p2 >= 0 ? p2 : alpha * p2;
            w2List.Add(w2);
            w2SumWindow.Add(w2);

            var vw1 = prevValue * w1;
            vw1List.Add(vw1);
            vw1SumWindow.Add(vw1);

            var vw2 = prevValue * w2;
            vw2List.Add(vw2);
            vw2SumWindow.Add(vw2);

            var wSum1 = w1SumWindow.Sum(length);
            var wSum2 = w2SumWindow.Sum(length);
            var sum1 = vw1SumWindow.Sum(length);
            var sum2 = vw2SumWindow.Sum(length);

            var prevRrma1 = GetLastOrDefault(rrma1List);
            var rrma1 = wSum1 != 0 ? sum1 / wSum1 : 0;
            rrma1List.Add(rrma1);

            var prevRrma2 = GetLastOrDefault(rrma2List);
            var rrma2 = wSum2 != 0 ? sum2 / wSum2 : 0;
            rrma2List.Add(rrma2);

            var signal = GetCompareSignal(rrma1 - rrma2, prevRrma1 - prevRrma2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Pclma", rrma1List }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(rrma1List);
        stockData.IndicatorName = IndicatorName.ParametricCorrectiveLinearMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Parabolic Weighted Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateParabolicWeightedMovingAverage(this StockData stockData, int length = 14)
    {
        List<double> pwmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            double sum = 0, weightedSum = 0;
            for (var j = 0; j <= length - 1; j++)
            {
                var weight = Pow(length - j, 2);
                var prevValue = i >= j ? inputList[i - j] : 0;

                sum += prevValue * weight;
                weightedSum += weight;
            }

            var prevPwma = GetLastOrDefault(pwmaList);
            var pwma = weightedSum != 0 ? sum / weightedSum : 0;
            pwmaList.Add(pwma);

            var signal = GetCompareSignal(currentValue - pwma, prevVal - prevPwma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Pwma", pwmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(pwmaList);
        stockData.IndicatorName = IndicatorName.ParabolicWeightedMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Parametric Kalman Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateParametricKalmanFilter(this StockData stockData, int length = 50)
    {
        List<double> errList = new(stockData.Count);
        List<double> estList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var priorEst = i >= length ? estList[i - length] : prevValue;
            var errMea = Math.Abs(priorEst - currentValue);
            var errPrv = Math.Abs(MinPastValues(i, 1, currentValue - prevValue) * -1);
            var prevErr = i >= 1 ? errList[i - 1] : errPrv;
            var kg = prevErr != 0 ? prevErr / (prevErr + errMea) : 0;
            var prevEst = i >= 1 ? estList[i - 1] : prevValue;

            var est = prevEst + (kg * (currentValue - prevEst));
            estList.Add(est);

            var err = (1 - kg) * errPrv;
            errList.Add(err);

            var signal = GetCompareSignal(currentValue - est, prevValue - prevEst);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Pkf", estList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(estList);
        stockData.IndicatorName = IndicatorName.ParametricKalmanFilter;

        return stockData;
    }


    /// <summary>
    /// Calculates the R2 Adaptive Regression
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateR2AdaptiveRegression(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 100)
    {
        List<double> outList = new(stockData.Count);
        List<double> tempList = new(stockData.Count);
        List<double> x2List = new(stockData.Count);
        List<double> x2PowList = new(stockData.Count);
        List<double> y1List = new(stockData.Count);
        List<double> y2List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingCorrelation x2CorrWindow = new();
        RollingCorrelation y1CorrWindow = new();
        RollingCorrelation y2CorrWindow = new();
        RollingSum x2SumWindow = new();
        RollingSum x2PowSumWindow = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var linregList = CalculateLinearRegression(stockData, length).CustomValuesList;
        var stdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var stdDev = stdDevList[i];
            var sma = smaList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var currentValue = inputList[i];
            tempList.Add(currentValue);
            var y1 = linregList[i];
            y1List.Add(y1);
            y1CorrWindow.Add(y1, currentValue);

            var x2 = i >= 1 ? outList[i - 1] : currentValue;
            x2List.Add(x2);
            x2SumWindow.Add(x2);
            x2CorrWindow.Add(x2, currentValue);

            var r2x2 = x2CorrWindow.R(length);
            r2x2 = IsValueNullOrInfinity(r2x2) ? 0 : r2x2;
            var x2Avg = x2SumWindow.Average(length);
            var x2Dev = x2 - x2Avg;

            var x2Pow = Pow(x2Dev, 2);
            x2PowList.Add(x2Pow);
            x2PowSumWindow.Add(x2Pow);

            var x2PowAvg = x2PowSumWindow.Average(length);
            var x2StdDev = x2PowAvg >= 0 ? Sqrt(x2PowAvg) : 0;
            var a = x2StdDev != 0 ? stdDev * (double)r2x2 / x2StdDev : 0;
            var b = sma - (a * x2Avg);

            var y2 = (a * x2) + b;
            y2List.Add(y2);
            y2CorrWindow.Add(y2, currentValue);

            var ry1 = Math.Pow(y1CorrWindow.R(length), 2);
            ry1 = IsValueNullOrInfinity(ry1) ? 0 : ry1;
            var ry2 = Math.Pow(y2CorrWindow.R(length), 2);
            ry2 = IsValueNullOrInfinity(ry2) ? 0 : ry2;

            var prevOutVal = GetLastOrDefault(outList);
            var outval = ((double)ry1 * y1) + ((double)ry2 * y2) + ((1 - (double)(ry1 + ry2)) * x2);
            outList.Add(outval);

            var signal = GetCompareSignal(currentValue - outval, prevValue - prevOutVal);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "R2ar", outList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(outList);
        stockData.IndicatorName = IndicatorName.R2AdaptiveRegression;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ratio OCHL Averager
    /// </summary>
    /// <param name="stockData"></param>
    /// <returns></returns>
    public static StockData CalculateRatioOCHLAverager(this StockData stockData)
    {
        List<double> dList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, openList, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentOpen = openList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var b = currentHigh - currentLow != 0 ? Math.Abs(currentValue - currentOpen) / (currentHigh - currentLow) : 0;
            var c = b > 1 ? 1 : b;

            var prevD = i >= 1 ? dList[i - 1] : currentValue;
            var d = (c * currentValue) + ((1 - c) * prevD);
            dList.Add(d);

            var signal = GetCompareSignal(currentValue - d, prevValue - prevD);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Rochla", dList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(dList);
        stockData.IndicatorName = IndicatorName.RatioOCHLAverager;

        return stockData;
    }


    /// <summary>
    /// Calculates the Regularized Exponential Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="lambda"></param>
    /// <returns></returns>
    public static StockData CalculateRegularizedExponentialMovingAverage(this StockData stockData, int length = 14, double lambda = 0.5)
    {
        List<double> remaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var alpha = (double)2 / (length + 1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevRema1 = i >= 1 ? remaList[i - 1] : 0;
            var prevRema2 = i >= 2 ? remaList[i - 2] : 0;

            var rema = (prevRema1 + (alpha * (currentValue - prevRema1)) + (lambda * ((2 * prevRema1) - prevRema2))) / (lambda + 1);
            remaList.Add(rema);

            var signal = GetCompareSignal(currentValue - rema, prevValue - prevRema1);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Rema", remaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(remaList);
        stockData.IndicatorName = IndicatorName.RegularizedExponentialMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Repulsion Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateRepulsionMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 100)
    {
        List<double> maList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var sma1List = GetMovingAverageList(stockData, maType, length, inputList);
        var sma2List = GetMovingAverageList(stockData, maType, length * 2, inputList);
        var sma3List = GetMovingAverageList(stockData, maType, length * 3, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var sma1 = sma1List[i];
            var sma2 = sma2List[i];
            var sma3 = sma3List[i];

            var prevMa = GetLastOrDefault(maList);
            var ma = sma3 + sma2 - sma1;
            maList.Add(ma);

            var signal = GetCompareSignal(currentValue - ma, prevValue - prevMa);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Rma", maList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(maList);
        stockData.IndicatorName = IndicatorName.RepulsionMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Retention Acceleration Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateRetentionAccelerationFilter(this StockData stockData, int length = 50)
    {
        List<double> altmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList1, lowestList1) = GetMaxAndMinValuesList(highList, lowList, length);
        var (highestList2, lowestList2) = GetMaxAndMinValuesList(highList, lowList, length * 2);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var highest1 = highestList1[i];
            var lowest1 = lowestList1[i];
            var highest2 = highestList2[i];
            var lowest2 = lowestList2[i];
            var ar = 2 * (highest1 - lowest1);
            var br = 2 * (highest2 - lowest2);
            var k1 = ar != 0 ? (1 - ar) / ar : 0;
            var k2 = br != 0 ? (1 - br) / br : 0;
            var alpha = k1 != 0 ? k2 / k1 : 0;
            var r1 = alpha != 0 && highest1 >= 0 ? Sqrt(highest1) / 4 * ((alpha - 1) / alpha) * (k2 / (k2 + 1)) : 0;
            var r2 = highest2 >= 0 ? Sqrt(highest2) / 4 * (alpha - 1) * (k1 / (k1 + 1)) : 0;
            var factor = r1 != 0 ? r2 / r1 : 0;
            var altk = Pow(factor >= 1 ? 1 : factor, Sqrt(length)) * ((double)1 / length);

            var prevAltma = i >= 1 ? altmaList[i - 1] : currentValue;
            var altma = (altk * currentValue) + ((1 - altk) * prevAltma);
            altmaList.Add(altma);

            var signal = GetCompareSignal(currentValue - altma, prevValue - prevAltma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Raf", altmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(altmaList);
        stockData.IndicatorName = IndicatorName.RetentionAccelerationFilter;

        return stockData;
    }


    /// <summary>
    /// Calculates the Reverse Engineering Relative Strength Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="rsiLevel"></param>
    /// <returns></returns>
    public static StockData CalculateReverseEngineeringRelativeStrengthIndex(this StockData stockData, int length = 14, double rsiLevel = 50)
    {
        List<double> aucList = new(stockData.Count);
        List<double> adcList = new(stockData.Count);
        List<double> revRsiList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        double expPeriod = (2 * length) - 1;
        var k = 2 / (expPeriod + 1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevAuc = i >= 1 ? aucList[i - 1] : 1;
            var prevAdc = i >= 1 ? adcList[i - 1] : 1;

            var auc = currentValue > prevValue ? (k * MinPastValues(i, 1, currentValue - prevValue)) + ((1 - k) * prevAuc) : (1 - k) * prevAuc;
            aucList.Add(auc);

            var adc = currentValue > prevValue ? ((1 - k) * prevAdc) : (k * MinPastValues(i, 1, prevValue - currentValue)) + ((1 - k) * prevAdc);
            adcList.Add(adc);

            var rsiValue = (length - 1) * ((adc * rsiLevel / (100 - rsiLevel)) - auc);
            var prevRevRsi = GetLastOrDefault(revRsiList);
            var revRsi = rsiValue >= 0 ? currentValue + rsiValue : currentValue + (rsiValue * (100 - rsiLevel) / rsiLevel);
            revRsiList.Add(revRsi);

            var signal = GetCompareSignal(currentValue - revRsi, prevValue - prevRevRsi);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Rersi", revRsiList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(revRsiList);
        stockData.IndicatorName = IndicatorName.ReverseEngineeringRelativeStrengthIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Right Sided Ricker Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="pctWidth"></param>
    /// <returns></returns>
    public static StockData CalculateRightSidedRickerMovingAverage(this StockData stockData, int length = 50, double pctWidth = 60)
    {
        List<double> rrmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var width = pctWidth / 100 * length;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            double w = 0, vw = 0;
            for (var j = 0; j < length; j++)
            {
                var prevV = i >= j ? inputList[i - j] : 0;
                w += (1 - Pow(j / width, 2)) * Exp(-(Pow(j, 2) / (2 * Pow(width, 2))));
                vw += prevV * w;
            }
            
            var prevRrma = GetLastOrDefault(rrmaList);
            var rrma = w != 0 ? vw / w : 0;
            rrmaList.Add(rrma);

            var signal = GetCompareSignal(currentValue - rrma, prevValue - prevRrma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Rsrma", rrmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(rrmaList);
        stockData.IndicatorName = IndicatorName.RightSidedRickerMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Recursive Moving Trend Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateRecursiveMovingTrendAverage(this StockData stockData, int length = 14)
    {
        List<double> botList = new(stockData.Count);
        List<double> nResList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var alpha = (double)2 / (length + 1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevBot = i >= 1 ? botList[i - 1] : currentValue;
            var prevNRes = i >= 1 ? nResList[i - 1] : currentValue;

            var bot = ((1 - alpha) * prevBot) + currentValue;
            botList.Add(bot);

            var nRes = ((1 - alpha) * prevNRes) + (alpha * (currentValue + bot - prevBot));
            nResList.Add(nRes);

            var signal = GetCompareSignal(currentValue - nRes, prevValue - prevNRes);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Rmta", nResList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(nResList);
        stockData.IndicatorName = IndicatorName.RecursiveMovingTrendAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Reverse Moving Average Convergence Divergence
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="signalLength"></param>
    /// <param name="macdLevel"></param>
    /// <returns></returns>
    public static StockData CalculateReverseMovingAverageConvergenceDivergence(this StockData stockData,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int fastLength = 12, int slowLength = 26, int signalLength = 9,
        double macdLevel = 0)
    {
        List<double> pMacdLevelList = new(stockData.Count);
        List<double> pMacdEqList = new(stockData.Count);
        List<double> histogramList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var fastAlpha = (double)2 / (1 + fastLength);
        var slowAlpha = (double)2 / (1 + slowLength);

        var fastEmaList = GetMovingAverageList(stockData, maType, fastLength, inputList);
        var slowEmaList = GetMovingAverageList(stockData, maType, slowLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevFastEma = i >= 1 ? fastEmaList[i - 1] : 0;
            var prevSlowEma = i >= 1 ? slowEmaList[i - 1] : 0;

            var pMacdEq = fastAlpha - slowAlpha != 0 ? ((prevFastEma * fastAlpha) - (prevSlowEma * slowAlpha)) / (fastAlpha - slowAlpha) : 0;
            pMacdEqList.Add(pMacdEq);

            var pMacdLevel = fastAlpha - slowAlpha != 0 ? (macdLevel - (prevFastEma * (1 - fastAlpha)) + (prevSlowEma * (1 - slowAlpha))) /
                                                          (fastAlpha - slowAlpha) : 0;
            pMacdLevelList.Add(pMacdLevel);
        }

        var pMacdEqSignalList = GetMovingAverageList(stockData, maType, signalLength, pMacdEqList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var pMacdEq = pMacdEqList[i];
            var pMacdEqSignal = pMacdEqSignalList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevPMacdEq = i >= 1 ? pMacdEqList[i - 1] : 0;

            var macdHistogram = pMacdEq - pMacdEqSignal;
            histogramList.Add(macdHistogram);

            var signal = GetCompareSignal(currentValue - pMacdEq, prevValue - prevPMacdEq);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Rmacd", pMacdEqList },
            { "Signal", pMacdEqSignalList },
            { "Histogram", histogramList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(pMacdEqList);
        stockData.IndicatorName = IndicatorName.ReverseMovingAverageConvergenceDivergence;

        return stockData;
    }


    /// <summary>
    /// Calculates the Moving Average Adaptive Q
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="fastAlpha"></param>
    /// <param name="slowAlpha"></param>
    /// <returns></returns>
    public static StockData CalculateMovingAverageAdaptiveQ(this StockData stockData, int length = 10, double fastAlpha = 0.667, 
        double slowAlpha = 0.0645)
    {
        List<double> maaqList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var erList = CalculateKaufmanAdaptiveMovingAverage(stockData, length: length).OutputValues["Er"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevMaaq = i >= 1 ? maaqList[i - 1] : currentValue;
            var er = erList[i];
            var temp = (er * fastAlpha) + slowAlpha;

            var maaq = prevMaaq + (Pow(temp, 2) * (currentValue - prevMaaq));
            maaqList.Add(maaq);

            var signal = GetCompareSignal(currentValue - maaq, prevValue - prevMaaq);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Maaq", maaqList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(maaqList);
        stockData.IndicatorName = IndicatorName.MovingAverageAdaptiveQ;

        return stockData;
    }


    /// <summary>
    /// Calculates the McGinley Dynamic Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="k"></param>
    /// <returns></returns>
    public static StockData CalculateMcGinleyDynamicIndicator(this StockData stockData, int length = 14, double k = 0.6)
    {
        List<double> mdiList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevMdi = i >= 1 ? GetLastOrDefault(mdiList) : currentValue;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var ratio = prevMdi != 0 ? currentValue / prevMdi : 0;
            var bottom = k * length * Pow(ratio, 4);

            var mdi = bottom != 0 ? prevMdi + ((currentValue - prevMdi) / Math.Max(bottom, 1)) : currentValue;
            mdiList.Add(mdi);

            var signal = GetCompareSignal(currentValue - mdi, prevValue - prevMdi);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Mdi", mdiList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(mdiList);
        stockData.IndicatorName = IndicatorName.McGinleyDynamicIndicator;

        return stockData;
    }

    public static StockData CalculateMiddleHighLowMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length1 = 14, int length2 = 10)
    {
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var mhlList = CalculateMidpoint(stockData, length2).CustomValuesList;
        var mhlMaList = GetMovingAverageList(stockData, maType, length1, mhlList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentMhlMa = mhlMaList[i];
            var prevMhlma = i >= 1 ? mhlMaList[i - 1] : 0;

            var signal = GetCompareSignal(currentValue - currentMhlMa, prevValue - prevMhlma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Mhlma", mhlMaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(mhlMaList);
        stockData.IndicatorName = IndicatorName.MiddleHighLowMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Moving Average V3
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculateMovingAverageV3(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 14, int length2 = 3)
    {
        List<double> nmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var lamdaRatio = (double)length1 / length2;
        var alpha = length1 - lamdaRatio != 0 ? lamdaRatio * (length1 - 1) / (length1 - lamdaRatio) : 0;

        var ma1List = GetMovingAverageList(stockData, maType, length1, inputList);
        var ma2List = GetMovingAverageList(stockData, maType, length2, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var ma1 = ma1List[i];
            var ma2 = ma2List[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevNma = GetLastOrDefault(nmaList);
            var nma = ((1 + alpha) * ma1) - (alpha * ma2);
            nmaList.Add(nma);

            var signal = GetCompareSignal(currentValue - nma, prevValue - prevNma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Mav3", nmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(nmaList);
        stockData.IndicatorName = IndicatorName.MovingAverageV3;

        return stockData;
    }


    /// <summary>
    /// Calculates the Multi Depth Zero Lag Exponential Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateMultiDepthZeroLagExponentialMovingAverage(this StockData stockData, int length = 50)
    {
        List<double> alpha1List = new(stockData.Count);
        List<double> beta1List = new(stockData.Count);
        List<double> alpha2List = new(stockData.Count);
        List<double> beta2List = new(stockData.Count);
        List<double> alpha3List = new(stockData.Count);
        List<double> beta3List = new(stockData.Count);
        List<double> mda1List = new(stockData.Count);
        List<double> mda2List = new(stockData.Count);
        List<double> mda3List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var a1 = (double)2 / (length + 1);
        var a2 = Exp(-Sqrt(2) * Math.PI / length);
        var a3 = Exp(-Math.PI / length);
        var b2 = 2 * a2 * Math.Cos(Sqrt(2) * Math.PI / length);
        var b3 = 2 * a3 * Math.Cos(Sqrt(3) * Math.PI / length);
        var c = Exp(-2 * Math.PI / length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevAlpha1 = i >= 1 ? alpha1List[i - 1] : currentValue;
            var alpha1 = (a1 * currentValue) + ((1 - a1) * prevAlpha1);
            alpha1List.Add(alpha1);

            var prevAlpha2 = i >= 1 ? alpha2List[i - 1] : currentValue;
            var priorAlpha2 = i >= 2 ? alpha2List[i - 2] : currentValue;
            var alpha2 = (b2 * prevAlpha2) - (a2 * a2 * priorAlpha2) + ((1 - b2 + (a2 * a2)) * currentValue);
            alpha2List.Add(alpha2);

            var prevAlpha3 = i >= 1 ? alpha3List[i - 1] : currentValue;
            var prevAlpha3_2 = i >= 2 ? alpha3List[i - 2] : currentValue;
            var prevAlpha3_3 = i >= 3 ? alpha3List[i - 3] : currentValue;
            var alpha3 = ((b3 + c) * prevAlpha3) - ((c + (b3 * c)) * prevAlpha3_2) + (c * c * prevAlpha3_3) + ((1 - b3 + c) * (1 - c) * currentValue);
            alpha3List.Add(alpha3);

            var detrend1 = currentValue - alpha1;
            var detrend2 = currentValue - alpha2;
            var detrend3 = currentValue - alpha3;

            var prevBeta1 = i >= 1 ? beta1List[i - 1] : 0;
            var beta1 = (a1 * detrend1) + ((1 - a1) * prevBeta1);
            beta1List.Add(beta1);

            var prevBeta2 = i >= 1 ? beta2List[i - 1] : 0;
            var prevBeta2_2 = i >= 2 ? beta2List[i - 2] : 0;
            var beta2 = (b2 * prevBeta2) - (a2 * a2 * prevBeta2_2) + ((1 - b2 + (a2 * a2)) * detrend2);
            beta2List.Add(beta2);

            var prevBeta3_2 = i >= 2 ? beta3List[i - 2] : 0;
            var prevBeta3_3 = i >= 3 ? beta3List[i - 3] : 0;
            var beta3 = ((b3 + c) * prevBeta3_2) - ((c + (b3 * c)) * prevBeta3_2) + (c * c * prevBeta3_3) + ((1 - b3 + c) * (1 - c) * detrend3);
            beta3List.Add(beta3);

            var mda1 = alpha1 + ((double)1 / 1 * beta1);
            mda1List.Add(mda1);

            var prevMda2 = GetLastOrDefault(mda2List);
            var mda2 = alpha2 + ((double)1 / 2 * beta2);
            mda2List.Add(mda2);

            var mda3 = alpha3 + ((double)1 / 3 * beta3);
            mda3List.Add(mda3);

            var signal = GetCompareSignal(currentValue - mda2, prevValue - prevMda2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Md2Pole", mda2List },
            { "Md1Pole", mda1List },
            { "Md3Pole", mda3List }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(mda2List);
        stockData.IndicatorName = IndicatorName.MultiDepthZeroLagExponentialMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Modular Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="beta"></param>
    /// <param name="z"></param>
    /// <returns></returns>
    public static StockData CalculateModularFilter(this StockData stockData, int length = 200, double beta = 0.8, double z = 0.5)
    {
        List<double> b2List = new(stockData.Count);
        List<double> c2List = new(stockData.Count);
        List<double> os2List = new(stockData.Count);
        List<double> ts2List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var alpha = (double)2 / (length + 1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevB2 = i >= 1 ? b2List[i - 1] : currentValue;
            var b2 = currentValue > (alpha * currentValue) + ((1 - alpha) * prevB2) ? currentValue : (alpha * currentValue) + ((1 - alpha) * prevB2);
            b2List.Add(b2);

            var prevC2 = i >= 1 ? c2List[i - 1] : currentValue;
            var c2 = currentValue < (alpha * currentValue) + ((1 - alpha) * prevC2) ? currentValue : (alpha * currentValue) + ((1 - alpha) * prevC2);
            c2List.Add(c2);

            var prevOs2 = GetLastOrDefault(os2List);
            var os2 = currentValue == b2 ? 1 : currentValue == c2 ? 0 : prevOs2;
            os2List.Add(os2);

            var upper2 = (beta * b2) + ((1 - beta) * c2);
            var lower2 = (beta * c2) + ((1 - beta) * b2);

            var prevTs2 = GetLastOrDefault(ts2List);
            var ts2 = (os2 * upper2) + ((1 - os2) * lower2);
            ts2List.Add(ts2);

            var signal = GetCompareSignal(currentValue - ts2, prevValue - prevTs2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Mf", ts2List }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(ts2List);
        stockData.IndicatorName = IndicatorName.ModularFilter;

        return stockData;
    }

}

