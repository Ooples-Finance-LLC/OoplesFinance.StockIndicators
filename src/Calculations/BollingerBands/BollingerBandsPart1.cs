
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the bollinger bands.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="stdDevMult">The standard dev mult.</param>
    /// <param name="maType">Average type of the moving.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateBollingerBands(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 20, double stdDevMult = 2)
    {
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var count = inputList.Count;
        var upperBandList = new List<double>(count);
        var lowerBandList = new List<double>(count);
        List<Signal>? signalsList = CreateSignalsList(stockData, count);
        var smaList = GetMovingAverageList(stockData, maType, length, inputList);
        var stdDeviationList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;

        double prevUpperBand = 0;
        double prevLowerBand = 0;
        for (var i = 0; i < count; i++)
        {
            var middleBand = smaList[i];
            var currentValue = inputList[i];
            var currentStdDeviation = stdDeviationList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevMiddleBand = i >= 1 ? smaList[i - 1] : 0;

            var upperBand = middleBand + (currentStdDeviation * stdDevMult);
            upperBandList.Add(upperBand);

            var lowerBand = middleBand - (currentStdDeviation * stdDevMult);
            lowerBandList.Add(lowerBand);

            var signal = GetBollingerBandsSignal(currentValue - middleBand, prevValue - prevMiddleBand, currentValue, prevValue, upperBand, prevUpperBand, lowerBand, prevLowerBand);
            signalsList?.Add(signal);

            prevUpperBand = upperBand;
            prevLowerBand = lowerBand;
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", upperBandList },
            { "MiddleBand", smaList },
            { "LowerBand", lowerBandList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.BollingerBands;

        return stockData;
    }


    /// <summary>
    /// Calculates the adaptive price zone indicator.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <param name="pct">The PCT.</param>
    /// <returns></returns>
    public static StockData CalculateAdaptivePriceZoneIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length = 20, double pct = 2)
    {
        List<double> xHLList = new(stockData.Count);
        List<double> outerUpBandList = new(stockData.Count);
        List<double> outerDnBandList = new(stockData.Count);
        List<double> middleBandList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        var nP = MinOrMax((int)Math.Ceiling(Sqrt(length)));

        var ema1List = GetMovingAverageList(stockData, maType, nP, inputList);
        var ema2List = GetMovingAverageList(stockData, maType, nP, ema1List);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];

            var xHL = currentHigh - currentLow;
            xHLList.Add(xHL);
        }

        var xHLEma1List = GetMovingAverageList(stockData, maType, nP, xHLList);
        var xHLEma2List = GetMovingAverageList(stockData, maType, nP, xHLEma1List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var xVal1 = ema2List[i];
            var xVal2 = xHLEma2List[i];

            var prevUpBand = GetLastOrDefault(outerUpBandList);
            var outerUpBand = (pct * xVal2) + xVal1;
            outerUpBandList.Add(outerUpBand);

            var prevDnBand = GetLastOrDefault(outerDnBandList);
            var outerDnBand = xVal1 - (pct * xVal2);
            outerDnBandList.Add(outerDnBand);

            var prevMiddleBand = GetLastOrDefault(middleBandList);
            var middleBand = (outerUpBand + outerDnBand) / 2;
            middleBandList.Add(middleBand);

            var signal = GetBollingerBandsSignal(currentValue - middleBand, prevValue - prevMiddleBand, currentValue, prevValue, outerUpBand,
                prevUpBand, outerDnBand, prevDnBand);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", outerUpBandList },
            { "MiddleBand", middleBandList },
            { "LowerBand", outerDnBandList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.AdaptivePriceZoneIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Auto Dispersion Bands
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <param name="smoothLength">Length of the smooth.</param>
    /// <returns></returns>
    public static StockData CalculateAutoDispersionBands(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage, 
        int length = 90, int smoothLength = 140)
    {
        List<double> middleBandList = new(stockData.Count);
        List<double> aList = new(stockData.Count);
        List<double> bList = new(stockData.Count);
        List<double> aMaxList = new(stockData.Count);
        List<double> bMinList = new(stockData.Count);
        List<double> x2List = new(stockData.Count);
        var x2SumWindow = new RollingSum();
        var aWindow = new RollingMinMax(length);
        var bWindow = new RollingMinMax(length);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= length ? inputList[i - length] : 0;
            var x = MinPastValues(i, length, currentValue - prevValue);

            var x2 = x * x;
            x2List.Add(x2);
            x2SumWindow.Add(x2);

            var x2Sma = x2SumWindow.Average(length);
            var sq = x2Sma >= 0 ? Sqrt(x2Sma) : 0;

            var a = currentValue + sq;
            aList.Add(a);
            aWindow.Add(a);

            var b = currentValue - sq;
            bList.Add(b);
            bWindow.Add(b);

            aMaxList.Add(aWindow.Max);
            bMinList.Add(bWindow.Min);
        }

        var aMaList = GetMovingAverageList(stockData, maType, length, aMaxList);
        var upperBandList = GetMovingAverageList(stockData, maType, smoothLength, aMaList);
        var bMaList = GetMovingAverageList(stockData, maType, length, bMinList);
        var lowerBandList = GetMovingAverageList(stockData, maType, smoothLength, bMaList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var upperBand = upperBandList[i];
            var lowerBand = lowerBandList[i];
            var prevUpperBand = i >= 1 ? upperBandList[i - 1] : 0;
            var prevLowerBand = i >= 1 ? lowerBandList[i - 1] : 0;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevMiddleBand = GetLastOrDefault(middleBandList);
            var middleBand = (upperBand + lowerBand) / 2;
            middleBandList.Add(middleBand);

            var signal = GetBollingerBandsSignal(currentValue - middleBand, prevValue - prevMiddleBand, currentValue, prevValue, upperBand,
                prevUpperBand, lowerBand, prevLowerBand);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", upperBandList },
            { "MiddleBand", middleBandList },
            { "LowerBand", lowerBandList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.AutoDispersionBands;

        return stockData;
    }


    /// <summary>
    /// Calculates the Bollinger Bands Fibonacci Ratios
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="fibRatio1"></param>
    /// <param name="fibRatio2"></param>
    /// <param name="fibRatio3"></param>
    /// <returns></returns>
    public static StockData CalculateBollingerBandsFibonacciRatios(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 20, double fibRatio1 = MathHelper.Phi, double fibRatio2 = MathHelper.Phi + 1, double fibRatio3 = (2 * MathHelper.Phi) + 1)
    {
        List<double> fibTop3List = new(stockData.Count);
        List<double> fibBottom3List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var atrList = CalculateAverageTrueRange(stockData, maType, length).CustomValuesList;
        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var atr = atrList[i];
            var sma = smaList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevSma = i >= 1 ? smaList[i - 1] : 0;
            var r1 = atr * fibRatio1;
            var r2 = atr * fibRatio2;
            var r3 = atr * fibRatio3;

            var prevFibTop3 = GetLastOrDefault(fibTop3List);
            var fibTop3 = sma + r3;
            fibTop3List.Add(fibTop3);

            var fibTop2 = sma + r2;
            var fibTop1 = sma + r1;
            var fibBottom1 = sma - r1;
            var fibBottom2 = sma - r2;

            var prevFibBottom3 = GetLastOrDefault(fibBottom3List);
            var fibBottom3 = sma - r3;
            fibBottom3List.Add(fibBottom3);

            var signal = GetBollingerBandsSignal(currentValue - sma, prevValue - prevSma, currentValue, prevValue, fibTop3, prevFibTop3, 
                fibBottom3, prevFibBottom3);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", fibTop3List },
            { "MiddleBand", smaList },
            { "LowerBand", fibBottom3List }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.BollingerBandsFibonacciRatios;

        return stockData;
    }


    /// <summary>
    /// Calculates the Bollinger Bands Average True Range
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="atrLength"></param>
    /// <param name="length"></param>
    /// <param name="stdDevMult"></param>
    /// <returns></returns>
    public static StockData CalculateBollingerBandsAvgTrueRange(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int atrLength = 22, int length = 55, double stdDevMult = 2)
    {
        List<double> atrDevList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var bollingerBands = CalculateBollingerBands(stockData, maType, length, stdDevMult);
        var upperBandList = bollingerBands.OutputValues["UpperBand"];
        var lowerBandList = bollingerBands.OutputValues["LowerBand"];
        var emaList = GetMovingAverageList(stockData, maType, atrLength, inputList);
        var atrList = CalculateAverageTrueRange(stockData, maType, atrLength).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentEma = emaList[i];
            var currentAtr = atrList[i];
            var upperBand = upperBandList[i];
            var lowerBand = lowerBandList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevEma = i >= 1 ? emaList[i - 1] : 0;
            var bbDiff = upperBand - lowerBand;

            var atrDev = bbDiff != 0 ? currentAtr / bbDiff : 0;
            atrDevList.Add(atrDev);

            var signal = GetVolatilitySignal(currentValue - currentEma, prevValue - prevEma, atrDev, 0.5);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "AtrDev", atrDevList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(atrDevList);
        stockData.IndicatorName = IndicatorName.BollingerBandsAverageTrueRange;

        return stockData;
    }

}

