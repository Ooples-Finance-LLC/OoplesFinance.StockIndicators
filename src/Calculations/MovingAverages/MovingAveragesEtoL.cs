using OoplesFinance.StockIndicators.Compatibility;
using OoplesFinance.StockIndicators.Core;

namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the exponential moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateExponentialMovingAverage(this StockData stockData, int length = 14)
    {
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var count = inputList.Count;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var outputBuffer = SpanCompat.CreateOutputBuffer(count);
        var emaSpan = outputBuffer.Span;
        MovingAverageCore.ExponentialMovingAverage(inputSpan, emaSpan, length);
        var emaList = outputBuffer.ToList();

        List<Signal>? signalsList = CreateSignalsList(stockData, count);
        for (var i = 0; i < count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var ema = emaList[i];
            var prevEma = i >= 1 ? emaList[i - 1] : 0;
            var signal = GetCompareSignal(currentValue - ema, prevValue - prevEma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ema", emaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(emaList);
        stockData.IndicatorName = IndicatorName.ExponentialMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the hull moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateHullMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length = 20)
    {
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var count = inputList.Count;

        var length2 = MinOrMax((int)Math.Round((double)length / 2));
        var sqrtLength = MinOrMax((int)Math.Round(Sqrt(length)));

        List<double> hullMAList;
        if (maType == MovingAvgType.WeightedMovingAverage)
        {
            var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
            var wma1 = new double[count];
            var wma2 = new double[count];
            MovingAverageCore.WeightedMovingAverage(inputSpan, wma1, length);
            MovingAverageCore.WeightedMovingAverage(inputSpan, wma2, length2);

            var totalWeighted = new double[count];
            for (var i = 0; i < count; i++)
            {
                totalWeighted[i] = (2 * wma2[i]) - wma1[i];
            }

            var outputBuffer = SpanCompat.CreateOutputBuffer(count);
            MovingAverageCore.WeightedMovingAverage(totalWeighted, outputBuffer.Span, sqrtLength);
            hullMAList = outputBuffer.ToList();
        }
        else
        {
            var totalWeightedMAList = new List<double>(count);
            var wma1List = GetMovingAverageList(stockData, maType, length, inputList);
            var wma2List = GetMovingAverageList(stockData, maType, length2, inputList);

            for (var i = 0; i < count; i++)
            {
                var currentWMA1 = wma1List[i];
                var currentWMA2 = wma2List[i];

                var totalWeightedMA = (2 * currentWMA2) - currentWMA1;
                totalWeightedMAList.Add(totalWeightedMA);
            }

            hullMAList = GetMovingAverageList(stockData, maType, sqrtLength, totalWeightedMAList);
        }

        List<Signal>? signalsList = CreateSignalsList(stockData, count);
        for (var i = 0; i < count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var hullMa = hullMAList[i];
            var prevHullMa = i >= 1 ? inputList[i - 1] : 0;

            var signal = GetCompareSignal(currentValue - hullMa, prevValue - prevHullMa);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Hma", hullMAList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(hullMAList);
        stockData.IndicatorName = IndicatorName.HullMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the kaufman adaptive moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <param name="fastLength">Length of the fast.</param>
    /// <param name="slowLength">Length of the slow.</param>
    /// <returns></returns>
    public static StockData CalculateKaufmanAdaptiveMovingAverage(this StockData stockData, int length = 10, int fastLength = 2, int slowLength = 30)
    {
        List<double> volatilityList = new(stockData.Count);
        List<double> erList = new(stockData.Count);
        List<double> kamaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum volatilitySumWindow = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var fastAlpha = (double)2 / (fastLength + 1);
        var slowAlpha = (double)2 / (slowLength + 1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var priorValue = i >= length ? inputList[i - length] : 0;

            var volatility = Math.Abs(MinPastValues(i, 1, currentValue - prevValue));
            volatilityList.Add(volatility);
            volatilitySumWindow.Add(volatility);

            var volatilitySum = volatilitySumWindow.Sum(length);
            var momentum = Math.Abs(MinPastValues(i, length, currentValue - priorValue));

            var efficiencyRatio = volatilitySum != 0 ? momentum / volatilitySum : 0;
            erList.Add(efficiencyRatio);

            var sc = Pow((efficiencyRatio * (fastAlpha - slowAlpha)) + slowAlpha, 2);
            var prevKama = GetLastOrDefault(kamaList);
            var currentKAMA = (sc * currentValue) + ((1 - sc) * prevKama);
            kamaList.Add(currentKAMA);

            var signal = GetCompareSignal(currentValue - currentKAMA, prevValue - prevKama);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Er", erList },
            { "Kama", kamaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(kamaList);
        stockData.IndicatorName = IndicatorName.KaufmanAdaptiveMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the end point moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <param name="offset">The offset.</param>
    /// <returns></returns>
    public static StockData CalculateEndPointMovingAverage(this StockData stockData, int length = 11, int offset = 4)
    {
        List<double> epmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            double sum = 0, weightedSum = 0;
            for (var j = 0; j <= length - 1; j++)
            {
                double weight = length - j - offset;
                var prevValue = i >= j ? inputList[i - j] : 0;

                sum += prevValue * weight;
                weightedSum += weight;
            }

            var prevEpma = GetLastOrDefault(epmaList);
            var epma = weightedSum != 0 ? 1 / weightedSum * sum : 0;
            epmaList.Add(epma);

            var signal = GetCompareSignal(currentValue - epma, prevVal - prevEpma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Epma", epmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(epmaList);
        stockData.IndicatorName = IndicatorName.EndPointMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the least squares moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateLeastSquaresMovingAverage(this StockData stockData, int length = 25)
    {
        List<double> lsmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var wmaList = CalculateWeightedMovingAverage(stockData, length).CustomValuesList;
        var smaList = CalculateSimpleMovingAverage(stockData, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentWma = wmaList[i];
            var currentSma = smaList[i];

            var prevLsma = GetLastOrDefault(lsmaList);
            var lsma = (3 * currentWma) - (2 * currentSma);
            lsmaList.Add(lsma);

            var signal = GetCompareSignal(currentValue - lsma, prevValue - prevLsma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Lsma", lsmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(lsmaList);
        stockData.IndicatorName = IndicatorName.LeastSquaresMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Jsa Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateJsaMovingAverage(this StockData stockData, int length = 14)
    {
        List<double> jmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var priorValue = i >= length ? inputList[i - length] : 0;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevJma = GetLastOrDefault(jmaList);
            var jma = (currentValue + priorValue) / 2;
            jmaList.Add(jma);

            var signal = GetCompareSignal(currentValue - jma, prevValue - prevJma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Jma", jmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(jmaList);
        stockData.IndicatorName = IndicatorName.JsaMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Jurik Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="phase"></param>
    /// <param name="power"></param>
    /// <returns></returns>
    public static StockData CalculateJurikMovingAverage(this StockData stockData, int length = 7, double phase = 50, double power = 2)
    {
        List<double> e0List = new(stockData.Count);
        List<double> e1List = new(stockData.Count);
        List<double> e2List = new(stockData.Count);
        List<double> jmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var phaseRatio = phase < -100 ? 0.5 : phase > 100 ? 2.5 : ((double)phase / 100) + 1.5;
        var ratio = 0.45 * (length - 1);
        var beta = ratio / (ratio + 2);
        var alpha = Pow(beta, power);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevJma = GetLastOrDefault(jmaList);

            var prevE0 = GetLastOrDefault(e0List);
            var e0 = ((1 - alpha) * currentValue) + (alpha * prevE0);
            e0List.Add(e0);

            var prevE1 = GetLastOrDefault(e1List);
            var e1 = ((currentValue - e0) * (1 - beta)) + (beta * prevE1);
            e1List.Add(e1);

            var prevE2 = GetLastOrDefault(e2List);
            var e2 = ((e0 + (phaseRatio * e1) - prevJma) * Pow(1 - alpha, 2)) + (Pow(alpha, 2) * prevE2);
            e2List.Add(e2);

            var jma = e2 + prevJma;
            jmaList.Add(jma);

            var signal = GetCompareSignal(currentValue - jma, prevValue - prevJma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Jma", jmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(jmaList);
        stockData.IndicatorName = IndicatorName.JurikMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Linear Weighted Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateLinearWeightedMovingAverage(this StockData stockData, int length = 14)
    {
        List<double> lwmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            double sum = 0, weightedSum = 0;
            for (var j = 0; j <= length - 1; j++)
            {
                double weight = length - j;
                var prevValue = i >= j ? inputList[i - j] : 0;

                sum += prevValue * weight;
                weightedSum += weight;
            }

            var prevLwma = GetLastOrDefault(lwmaList);
            var lwma = weightedSum != 0 ? sum / weightedSum : 0;
            lwmaList.Add(lwma);

            var signal = GetCompareSignal(currentValue - lwma, prevVal - prevLwma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Lwma", lwmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(lwmaList);
        stockData.IndicatorName = IndicatorName.LinearWeightedMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Leo Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateLeoMovingAverage(this StockData stockData, int length = 14)
    {
        List<double> lmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var wmaList = CalculateWeightedMovingAverage(stockData, length).CustomValuesList;
        var smaList = CalculateSimpleMovingAverage(stockData, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentWma = wmaList[i];
            var currentSma = smaList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevLma = GetLastOrDefault(lmaList);
            var lma = (2 * currentWma) - currentSma;
            lmaList.Add(lma);

            var signal = GetCompareSignal(currentValue - lma, prevValue - prevLma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Lma", lmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(lmaList);
        stockData.IndicatorName = IndicatorName.LeoMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Light Least Squares Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateLightLeastSquaresMovingAverage(this StockData stockData,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 250)
    {
        List<double> yList = new(stockData.Count);
        List<double> indexList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var length1 = MinOrMax((int)Math.Ceiling((double)length / 2));

        for (var i = 0; i < stockData.Count; i++)
        {
            double index = i;
            indexList.Add(index);
        }

        var sma1List = GetMovingAverageList(stockData, maType, length, inputList);
        var sma2List = GetMovingAverageList(stockData, maType, length1, inputList);
        var stdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        stockData.SetCustomValues(indexList);
        var indexStdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        var indexSmaList = GetMovingAverageList(stockData, maType, length, indexList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var sma1 = sma1List[i];
            var sma2 = sma2List[i];
            var stdDev = stdDevList[i];
            var indexStdDev = indexStdDevList[i];
            var indexSma = indexSmaList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var c = stdDev != 0 ? (sma2 - sma1) / stdDev : 0;
            var z = indexStdDev != 0 && c != 0 ? (i - indexSma) / indexStdDev * c : 0;

            var prevY = GetLastOrDefault(yList);
            var y = sma1 + (z * stdDev);
            yList.Add(y);

            var signal = GetCompareSignal(currentValue - y, prevValue - prevY);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Llsma", yList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(yList);
        stockData.IndicatorName = IndicatorName.LightLeastSquaresMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Linear Extrapolation
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateLinearExtrapolation(this StockData stockData, int length = 500)
    {
        List<double> extList = new(stockData.Count);
        List<double> xList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevY = i >= 1 ? inputList[i - 1] : 0;
            var priorY = i >= length ? inputList[i - length] : 0;
            var priorY2 = i >= length * 2 ? inputList[i - (length * 2)] : 0;
            var priorX = i >= length ? xList[i - length] : 0;
            var priorX2 = i >= length * 2 ? xList[i - (length * 2)] : 0;

            double x = i;
            xList.Add(i);

            var prevExt = GetLastOrDefault(extList);
            var ext = priorX2 - priorX != 0 && priorY2 - priorY != 0 ? priorY + ((x - priorX) / (priorX2 - priorX) * (priorY2 - priorY)) : priorY;
            extList.Add(ext);

            var signal = GetCompareSignal(currentValue - ext, prevY - prevExt);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "LinExt", extList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(extList);
        stockData.IndicatorName = IndicatorName.LinearExtrapolation;

        return stockData;
    }


    /// <summary>
    /// Calculates the Linear Regression Line
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateLinearRegressionLine(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 14)
    {
        List<double> regList = new(stockData.Count);
        List<double> corrList = new(stockData.Count);
        List<double> yList = new(stockData.Count);
        List<double> xList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingCorrelation corrWindow = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var yMaList = GetMovingAverageList(stockData, maType, length, inputList);
        var myList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            yList.Add(currentValue);

            double x = i;
            xList.Add(x);

            corrWindow.Add(currentValue, x);
            var corr = corrWindow.R(length);
            corr = IsValueNullOrInfinity(corr) ? 0 : corr;
            corrList.Add((double)corr);
        }

        var xMaList = GetMovingAverageList(stockData, maType, length, xList);
        stockData.SetCustomValues(xList);
        var mxList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList; ;
        for (var i = 0; i < stockData.Count; i++)
        {
            var my = myList[i];
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
            { "LinReg", regList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(regList);
        stockData.IndicatorName = IndicatorName.LinearRegressionLine;

        return stockData;
    }


    /// <summary>
    /// Calculates the IIR Least Squares Estimate
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateIIRLeastSquaresEstimate(this StockData stockData, int length = 100)
    {
        List<double> sList = new(stockData.Count);
        List<double> sEmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var a = (double)4 / (length + 2);
        var halfLength = MinOrMax((int)Math.Ceiling((double)length / 2));

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevS = i >= 1 ? sList[i - 1] : currentValue;
            var prevSEma = GetLastOrDefault(sEmaList);
            var sEma = CalculateEMA(prevS, prevSEma, halfLength);
            sEmaList.Add(prevSEma);

            var s = (a * currentValue) + prevS - (a * sEma);
            sList.Add(s);

            var signal = GetCompareSignal(currentValue - s, prevValue - prevS);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "IIRLse", sList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(sList);
        stockData.IndicatorName = IndicatorName.IIRLeastSquaresEstimate;

        return stockData;
    }


    /// <summary>
    /// Calculates the Inverse Distance Weighted Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateInverseDistanceWeightedMovingAverage(this StockData stockData, int length = 14)
    {
        List<double> idwmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            double sum = 0, weightedSum = 0;
            for (var j = 0; j <= length - 1; j++)
            {
                var prevValue = i >= j ? inputList[i - j] : 0;

                double weight = 0;
                for (var k = 0; k <= length - 1; k++)
                {
                    var prevValue2 = i >= k ? inputList[i - k] : 0;
                    weight += Math.Abs(prevValue - prevValue2);
                }

                sum += prevValue * weight;
                weightedSum += weight;
            }

            var prevIdwma = GetLastOrDefault(idwmaList);
            var idwma = weightedSum != 0 ? sum / weightedSum : 0;
            idwmaList.Add(idwma);

            var signal = GetCompareSignal(currentValue - idwma, prevVal - prevIdwma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Idwma", idwmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(idwmaList);
        stockData.IndicatorName = IndicatorName.InverseDistanceWeightedMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Generalized Double Exponential Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="factor"></param>
    /// <returns></returns>
    public static StockData CalculateGeneralizedDoubleExponentialMovingAverage(this StockData stockData,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 5, double factor = 0.7)
    {
        List<double> gdList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var ema1List = GetMovingAverageList(stockData, maType, length, inputList);
        var ema2List = GetMovingAverageList(stockData, maType, length, ema1List);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentEma1 = ema1List[i];
            var currentEma2 = ema2List[i];

            var prevGd = GetLastOrDefault(gdList);
            var gd = (currentEma1 * (1 + factor)) - (currentEma2 * factor);
            gdList.Add(gd);

            var signal = GetCompareSignal(currentValue - gd, prevValue - prevGd);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Gdema", gdList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(gdList);
        stockData.IndicatorName = IndicatorName.GeneralizedDoubleExponentialMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the General Filter Estimator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="beta"></param>
    /// <param name="gamma"></param>
    /// <param name="zeta"></param>
    /// <returns></returns>
    public static StockData CalculateGeneralFilterEstimator(this StockData stockData, int length = 100, double beta = 5.25, double gamma = 1,
        double zeta = 1)
    {
        List<double> dList = new(stockData.Count);
        List<double> bList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var p = beta != 0 ? (int)Math.Ceiling(length / beta) : 0;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var priorB = i >= p ? bList[i - p] : currentValue;
            var a = currentValue - priorB;

            var prevB = i >= 1 ? bList[i - 1] : currentValue;
            var b = prevB + (a / p * gamma);
            bList.Add(b);

            var priorD = i >= p ? dList[i - p] : b;
            var c = b - priorD;

            var prevD = i >= 1 ? dList[i - 1] : currentValue;
            var d = prevD + (((zeta * a) + ((1 - zeta) * c)) / p * gamma);
            dList.Add(d);

            var signal = GetCompareSignal(currentValue - d, prevValue - prevD);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Gfe", dList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(dList);
        stockData.IndicatorName = IndicatorName.GeneralFilterEstimator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Henderson Weighted Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateHendersonWeightedMovingAverage(this StockData stockData, int length = 7)
    {
        List<double> hwmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var termMult = MinOrMax((int)Math.Floor((double)(length - 1) / 2));

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            double sum = 0, weightedSum = 0;
            for (var j = 0; j <= length - 1; j++)
            {
                var m = termMult;
                var n = j - termMult;
                var numerator = 315 * (Pow(m + 1, 2) - Pow(n, 2)) * (Pow(m + 2, 2) - Pow(n, 2)) * (Pow(m + 3, 2) -
                    Pow(n, 2)) * ((3 * Pow(m + 2, 2)) - (11 * Pow(n, 2)) - 16);
                var denominator = 8 * (m + 2) * (Pow(m + 2, 2) - 1) * ((4 * Pow(m + 2, 2)) - 1) * ((4 * Pow(m + 2, 2)) - 9) *
                                  ((4 * Pow(m + 2, 2)) - 25);
                var weight = denominator != 0 ? numerator / denominator : 0;
                var prevValue = i >= j ? inputList[i - j] : 0;

                sum += prevValue * weight;
                weightedSum += weight;
            }

            var prevHwma = GetLastOrDefault(hwmaList);
            var hwma = weightedSum != 0 ? sum / weightedSum : 0;
            hwmaList.Add(hwma);

            var signal = GetCompareSignal(currentValue - hwma, prevVal - prevHwma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Hwma", hwmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(hwmaList);
        stockData.IndicatorName = IndicatorName.HendersonWeightedMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Holt Exponential Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="alphaLength"></param>
    /// <param name="gammaLength"></param>
    /// <returns></returns>
    public static StockData CalculateHoltExponentialMovingAverage(this StockData stockData, int alphaLength = 20, int gammaLength = 20)
    {
        List<double> hemaList = new(stockData.Count);
        List<double> bList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var alpha = (double)2 / (alphaLength + 1);
        var gamma = (double)2 / (gammaLength + 1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevB = i >= 1 ? bList[i - 1] : currentValue;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevHema = GetLastOrDefault(hemaList);
            var hema = ((1 - alpha) * (prevHema + prevB)) + (alpha * currentValue);
            hemaList.Add(hema);

            var b = ((1 - gamma) * prevB) + (gamma * (hema - prevHema));
            bList.Add(b);

            var signal = GetCompareSignal(currentValue - hema, prevValue - prevHema);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Hema", hemaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(hemaList);
        stockData.IndicatorName = IndicatorName.HoltExponentialMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Hull Estimate
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateHullEstimate(this StockData stockData, int length = 50)
    {
        List<double> hemaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var maLength = MinOrMax((int)Math.Ceiling((double)length / 2));

        var wmaList = GetMovingAverageList(stockData, MovingAvgType.WeightedMovingAverage, maLength, inputList);
        var emaList = GetMovingAverageList(stockData, MovingAvgType.ExponentialMovingAverage, maLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentWma = wmaList[i];
            var currentEma = emaList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevHema = GetLastOrDefault(hemaList);
            var hema = (3 * currentWma) - (2 * currentEma);
            hemaList.Add(hema);

            var signal = GetCompareSignal(currentValue - hema, prevValue - prevHema);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "He", hemaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(hemaList);
        stockData.IndicatorName = IndicatorName.HullEstimate;

        return stockData;
    }


    /// <summary>
    /// Calculates the Hampel Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="scalingFactor"></param>
    /// <returns></returns>
    public static StockData CalculateHampelFilter(this StockData stockData, int length = 14, double scalingFactor = 3)
    {
        List<double> tempList = new(stockData.Count);
        List<double> absDiffList = new(stockData.Count);
        List<double> hfList = new(stockData.Count);
        List<double> hfEmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        using var tempMedian = new RollingMedian(length);
        using var absDiffMedian = new RollingMedian(length);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var alpha = (double)2 / (length + 1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevValue = GetLastOrDefault(tempList);
            var currentValue = inputList[i];
            tempList.Add(currentValue);

            tempMedian.Add(currentValue);
            var sampleMedian = tempMedian.Median;
            var absDiff = Math.Abs(currentValue - sampleMedian);
            absDiffList.Add(absDiff);

            absDiffMedian.Add(absDiff);
            var mad = absDiffMedian.Median;
            var hf = absDiff <= scalingFactor * mad ? currentValue : sampleMedian;
            hfList.Add(hf);

            var prevHfEma = GetLastOrDefault(hfEmaList);
            var hfEma = (alpha * hf) + ((1 - alpha) * prevHfEma);
            hfEmaList.Add(hfEma);

            var signal = GetCompareSignal(currentValue - hfEma, prevValue - prevHfEma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Hf", hfEmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(hfEmaList);
        stockData.IndicatorName = IndicatorName.HampelFilter;

        return stockData;
    }


    /// <summary>
    /// Calculates the Hybrid Convolution Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateHybridConvolutionFilter(this StockData stockData, int length = 14)
    {
        List<double> outputList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            var prevOutput = i >= 1 ? outputList[i - 1] : currentValue;
            double output = 0;
            for (var j = 1; j <= length; j++)
            {
                var sign = 0.5 * (1 - Math.Cos(MinOrMax((double)j / length * Math.PI, 0.99, 0.01)));
                var d = sign - (0.5 * (1 - Math.Cos(MinOrMax((double)(j - 1) / length, 0.99, 0.01))));
                var prevValue = i >= j - 1 ? inputList[i - (j - 1)] : 0;
                output += ((sign * prevOutput) + ((1 - sign) * prevValue)) * d;
            }
            outputList.Add(output);

            var signal = GetCompareSignal(currentValue - output, prevVal - prevOutput);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Hcf", outputList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(outputList);
        stockData.IndicatorName = IndicatorName.HybridConvolutionFilter;

        return stockData;
    }


    /// <summary>
    /// Calculates the Fibonacci Weighted Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateFibonacciWeightedMovingAverage(this StockData stockData, int length = 14)
    {
        List<double> fibonacciWmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var phi = (1 + Sqrt(5)) / 2;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            double sum = 0, weightedSum = 0;
            for (var j = 0; j <= length - 1; j++)
            {
                var pow = Pow(phi, length - j);
                var weight = (pow - (Pow(-1, j) / pow)) / Sqrt(5);
                var prevValue = i >= j ? inputList[i - j] : 0;

                sum += prevValue * weight;
                weightedSum += weight;
            }

            var prevFwma = GetLastOrDefault(fibonacciWmaList);
            var fwma = weightedSum != 0 ? sum / weightedSum : 0;
            fibonacciWmaList.Add(fwma);

            var signal = GetCompareSignal(currentValue - fwma, prevVal - prevFwma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Fwma", fibonacciWmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(fibonacciWmaList);
        stockData.IndicatorName = IndicatorName.FibonacciWeightedMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Farey Sequence Weighted Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateFareySequenceWeightedMovingAverage(this StockData stockData, int length = 5)
    {
        List<double> fswmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var array = new double[4] { 0, 1, 1, length };
        List<double> resList = new(stockData.Count);

        while (array[2] <= length)
        {
            var a = array[0];
            var b = array[1];
            var c = array[2];
            var d = array[3];
            var k = Math.Floor((length + b) / array[3]);

            array[0] = c;
            array[1] = d;
            array[2] = (k * c) - a;
            array[3] = (k * d) - b;

            var res = array[1] != 0 ? Math.Round(array[0] / array[1], 3) : 0;
            resList.Insert(0, res);
        }

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            double sum = 0, weightedSum = 0;
            for (var j = 0; j < resList.Count; j++)
            {
                var prevValue = i >= j ? inputList[i - j] : 0;
                var weight = resList[j];

                sum += prevValue * weight;
                weightedSum += weight;
            }

            var prevFswma = GetLastOrDefault(fswmaList);
            var fswma = weightedSum != 0 ? sum / weightedSum : 0;
            fswmaList.Add(fswma);

            var signal = GetCompareSignal(currentValue - fswma, prevVal - prevFswma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Fswma", fswmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(fswmaList);
        stockData.IndicatorName = IndicatorName.FareySequenceWeightedMovingAverage;

        return stockData;
    }

    public static StockData CalculateFallingRisingFilter(this StockData stockData, int length = 14)
    {
        List<double> tempList = new(stockData.Count);
        List<double> aList = new(stockData.Count);
        List<double> errorList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingMinMax tempWindow = new(length);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var alpha = (double)2 / (length + 1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevA = i >= 1 ? aList[i - 1] : 0;
            var prevError = i >= 1 ? errorList[i - 1] : 0;

            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            tempList.Add(prevValue);
            tempWindow.Add(prevValue);

            var beta = currentValue > tempWindow.Max || currentValue < tempWindow.Min ? 1 : alpha;
            var a = prevA + (alpha * prevError) + (beta * prevError);
            aList.Add(a);

            var error = currentValue - a;
            errorList.Add(error);

            var signal = GetCompareSignal(error, prevError);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Frf", aList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(aList);
        stockData.IndicatorName = IndicatorName.FallingRisingFilter;

        return stockData;
    }


    /// <summary>
    /// Calculates the Fisher Least Squares Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateFisherLeastSquaresMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 100)
    {
        List<double> bList = new(stockData.Count);
        List<double> indexList = new(stockData.Count);
        List<double> diffList = new(stockData.Count);
        List<double> absDiffList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum absDiffSum = new();
        RollingSum diffSum = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var stdDevSrcList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        var smaSrcList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            double index = i;
            indexList.Add(index);
        }

        stockData.SetCustomValues(indexList);
        var indexStdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        var indexSmaList = GetMovingAverageList(stockData, maType, length, indexList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var stdDevSrc = stdDevSrcList[i];
            var indexStdDev = indexStdDevList[i];
            var currentValue = inputList[i];
            var prevB = i >= 1 ? bList[i - 1] : currentValue;
            var indexSma = indexSmaList[i];
            var sma = smaSrcList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var diff = currentValue - prevB;
            diffList.Add(diff);
            diffSum.Add(diff);

            var absDiff = Math.Abs(diff);
            absDiffList.Add(absDiff);
            absDiffSum.Add(absDiff);

            var e = absDiffSum.Average(length);
            var z = e != 0 ? diffSum.Average(length) / e : 0;
            var r = Exp(2 * z) + 1 != 0 ? (Exp(2 * z) - 1) / (Exp(2 * z) + 1) : 0;
            var a = indexStdDev != 0 && r != 0 ? (i - indexSma) / indexStdDev * r : 0;

            var b = sma + (a * stdDevSrc);
            bList.Add(b);

            var signal = GetCompareSignal(currentValue - b, prevValue - prevB);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Flsma", bList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(bList);
        stockData.IndicatorName = IndicatorName.FisherLeastSquaresMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Kaufman Adaptive Least Squares Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateKaufmanAdaptiveLeastSquaresMovingAverage(this StockData stockData,
        MovingAvgType maType = MovingAvgType.KaufmanAdaptiveMovingAverage, int length = 100)
    {
        List<double> kalsmaList = new(stockData.Count);
        List<double> indexList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var kamaList = CalculateKaufmanAdaptiveCorrelationOscillator(stockData, maType, length);
        var indexStList = kamaList.OutputValues["IndexSt"];
        var srcStList = kamaList.OutputValues["SrcSt"];
        var rList = kamaList.OutputValues["Kaco"];
        var srcMaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            double index = i;
            indexList.Add(index);
        }

        var indexMaList = GetMovingAverageList(stockData, maType, length, indexList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var indexSt = indexStList[i];
            var srcSt = srcStList[i];
            var srcMa = srcMaList[i];
            var indexMa = indexMaList[i];
            var r = rList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var alpha = indexSt != 0 ? srcSt / indexSt * r : 0;
            var beta = srcMa - (alpha * indexMa);

            var prevKalsma = GetLastOrDefault(kalsmaList);
            var kalsma = (alpha * i) + beta;
            kalsmaList.Add(kalsma);

            var signal = GetCompareSignal(currentValue - kalsma, prevValue - prevKalsma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Kalsma", kalsmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(kalsmaList);
        stockData.IndicatorName = IndicatorName.KaufmanAdaptiveLeastSquaresMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Kalman Smoother
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateKalmanSmoother(this StockData stockData, int length = 200)
    {
        List<double> veloList = new(stockData.Count);
        List<double> kfList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevKf = i >= 1 ? kfList[i - 1] : currentValue;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var dk = currentValue - prevKf;
            var smooth = prevKf + (dk * Sqrt((double)length / 10000 * 2));

            var prevVelo = i >= 1 ? veloList[i - 1] : 0;
            var velo = prevVelo + ((double)length / 10000 * dk);
            veloList.Add(velo);

            var kf = smooth + velo;
            kfList.Add(kf);

            var signal = GetCompareSignal(currentValue - kf, prevValue - prevKf);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ks", kfList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(kfList);
        stockData.IndicatorName = IndicatorName.KalmanSmoother;

        return stockData;
    }


    /// <summary>
    /// Calculates the linear regression.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateLinearRegression(this StockData stockData, int length = 14)
    {
        List<double> slopeList = new(stockData.Count);
        List<double> interceptList = new(stockData.Count);
        List<double> predictedTomorrowList = new(stockData.Count);
        List<double> predictedTodayList = new(stockData.Count);
        List<double> xList = new(stockData.Count);
        List<double> yList = new(stockData.Count);
        List<double> xyList = new(stockData.Count);
        List<double> x2List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum xSumWindow = new();
        RollingSum ySumWindow = new();
        RollingSum xySumWindow = new();
        RollingSum x2SumWindow = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevValue = GetLastOrDefault(yList);
            var currentValue = inputList[i];
            yList.Add(currentValue);
            ySumWindow.Add(currentValue);

            double x = i;
            xList.Add(x);
            xSumWindow.Add(x);

            var x2 = x * x;
            x2List.Add(x2);
            x2SumWindow.Add(x2);

            var xy = x * currentValue;
            xyList.Add(xy);
            xySumWindow.Add(xy);

            var sumX = xSumWindow.Sum(length);
            var sumY = ySumWindow.Sum(length);
            var sumXY = xySumWindow.Sum(length);
            var sumX2 = x2SumWindow.Sum(length);
            var top = (length * sumXY) - (sumX * sumY);
            var bottom = (length * sumX2) - Pow(sumX, 2);

            var b = bottom != 0 ? top / bottom : 0;
            slopeList.Add(b);

            var a = length != 0 ? (sumY - (b * sumX)) / length : 0;
            interceptList.Add(a);

            var predictedToday = a + (b * x);
            predictedTodayList.Add(predictedToday);

            var prevPredictedNextDay = GetLastOrDefault(predictedTomorrowList);
            var predictedNextDay = a + (b * (x + 1));
            predictedTomorrowList.Add(predictedNextDay);

            var signal = GetCompareSignal(currentValue - predictedNextDay, prevValue - prevPredictedNextDay, true);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "LinearRegression", predictedTodayList },
            { "PredictedTomorrow", predictedTomorrowList },
            { "Slope", slopeList },
            { "Intercept", interceptList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(predictedTodayList);
        stockData.IndicatorName = IndicatorName.LinearRegression;

        return stockData;
    }


    /// <summary>
    /// Calculates the Elastic Volume Weighted Moving Average V1
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="mult"></param>
    /// <returns></returns>
    public static StockData CalculateElasticVolumeWeightedMovingAverageV1(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 40, double mult = 20)
    {
        List<double> evwmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);

        var volumeSmaList = GetMovingAverageList(stockData, maType, length, volumeList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentAvgVolume = volumeSmaList[i];
            var currentVolume = volumeList[i];
            var n = currentAvgVolume * mult;

            var prevEVWMA = i >= 1 ? GetLastOrDefault(evwmaList) : currentValue;
            var evwma = n > 0 ? (((n - currentVolume) * prevEVWMA) + (currentVolume * currentValue)) / n : 0; ;
            evwmaList.Add(evwma);

            var signal = GetCompareSignal(currentValue - evwma, prevValue - prevEVWMA);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Evwma", evwmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(evwmaList);
        stockData.IndicatorName = IndicatorName.ElasticVolumeWeightedMovingAverageV1;

        return stockData;
    }


    /// <summary>
    /// Calculates the Elastic Volume Weighted Moving Average V2
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateElasticVolumeWeightedMovingAverageV2(this StockData stockData, int length = 14)
    {
        List<double> tempList = new(stockData.Count);
        List<double> evwmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum volumeSumWindow = new();
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var currentVolume = volumeList[i];
            tempList.Add(currentVolume);
            volumeSumWindow.Add(currentVolume);

            var volumeSum = volumeSumWindow.Sum(length);
            var prevEvwma = GetLastOrDefault(evwmaList);
            var evwma = volumeSum != 0 ? (((volumeSum - currentVolume) * prevEvwma) + (currentVolume * currentValue)) / volumeSum : 0;
            evwmaList.Add(evwma);

            var signal = GetCompareSignal(currentValue - evwma, prevValue - prevEvwma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Evwma", evwmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(evwmaList);
        stockData.IndicatorName = IndicatorName.ElasticVolumeWeightedMovingAverageV2;

        return stockData;
    }


    /// <summary>
    /// Calculates the Equity Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateEquityMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14)
    {
        List<double> chgXList = new(stockData.Count);
        List<double> chgXCumList = new(stockData.Count);
        List<double> xList = new(stockData.Count);
        List<double> eqmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum chgXSumWindow = new();
        double chgXCumSum = 0;
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var sma = smaList[i];
            var prevEqma = i >= 1 ? eqmaList[i - 1] : currentValue;

            var prevX = GetLastOrDefault(xList);
            double x = Math.Sign(currentValue - sma);
            xList.Add(x);

            var chgX = MinPastValues(i, 1, currentValue - prevValue) * prevX;
            chgXList.Add(chgX);
            chgXSumWindow.Add(chgX);

            var chgXCum = MinPastValues(i, 1, currentValue - prevValue) * x;    
            chgXCumList.Add(chgXCum);
            chgXCumSum += chgXCum;

            var opteq = chgXCumSum;
            var req = chgXSumWindow.Sum(length);
            var alpha = opteq != 0 ? MinOrMax(req / opteq, 0.99, 0.01) : 0.99;  

            var eqma = (alpha * currentValue) + ((1 - alpha) * prevEqma);
            eqmaList.Add(eqma);

            var signal = GetCompareSignal(currentValue - eqma, prevValue - prevEqma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Eqma", eqmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(eqmaList);
        stockData.IndicatorName = IndicatorName.EquityMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Edge Preserving Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateEdgePreservingFilter(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 200, 
        int smoothLength = 50)
    {
        List<double> osList = new(stockData.Count);
        List<double> absOsList = new(stockData.Count);
        List<double> hList = new(stockData.Count);
        List<double> aList = new(stockData.Count);
        List<double> bList = new(stockData.Count);
        List<double> cList = new(stockData.Count);
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
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var p = pList[i];
            var highest = highestList[i];
            var os = osList[i];

            var prevH = GetLastOrDefault(hList);
            var h = highest != 0 ? p / highest : 0;
            hList.Add(h);

            double cnd = h == 1 && prevH != 1 ? 1 : 0;
            double sign = cnd == 1 && os < 0 ? 1 : cnd == 1 && os > 0 ? -1 : 0;
            var condition = sign != 0;

            var prevA = i >= 1 ? aList[i - 1] : 1;
            var a = condition ? 1 : prevA + 1;
            aList.Add(a);

            var prevB = i >= 1 ? bList[i - 1] : currentValue;
            var b = a == 1 ? currentValue : prevB + currentValue;
            bList.Add(b);

            var prevC = GetLastOrDefault(cList);
            var c = a != 0 ? b / a : 0;
            cList.Add(c);

            var signal = GetCompareSignal(currentValue - c, prevValue - prevC);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Epf", cList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(cList);
        stockData.IndicatorName = IndicatorName.EdgePreservingFilter;

        return stockData;
    }
}

