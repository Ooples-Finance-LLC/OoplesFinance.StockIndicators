using OoplesFinance.StockIndicators.Compatibility;
using OoplesFinance.StockIndicators.Core;

namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the triangular moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateTriangularMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 20)
    {
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var count = inputList.Count;
        List<double> tmaList;

        if (maType == MovingAvgType.SimpleMovingAverage)
        {
            var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
            var smaBuffer = new double[count];
            MovingAverageCore.SimpleMovingAverage(inputSpan, smaBuffer, length);

            var outputBuffer = SpanCompat.CreateOutputBuffer(count);
            MovingAverageCore.SimpleMovingAverage(smaBuffer, outputBuffer.Span, length);
            tmaList = outputBuffer.ToList();
        }
        else
        {
            var sma1List = GetMovingAverageList(stockData, maType, length, inputList);
            tmaList = GetMovingAverageList(stockData, maType, length, sma1List);
        }

        List<Signal>? signalsList = CreateSignalsList(stockData, count);
        for (var i = 0; i < count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var tma = tmaList[i];
            var prevTma = i >= 1 ? tmaList[i - 1] : 0;

            var signal = GetCompareSignal(currentValue - tma, prevValue - prevTma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Tma", tmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(tmaList);
        stockData.IndicatorName = IndicatorName.TriangularMovingAverage;

        return stockData;
    }


    public static StockData CalculateWellesWilderMovingAverage(this StockData stockData, int length = 14)
    {
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var count = inputList.Count;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var outputBuffer = SpanCompat.CreateOutputBuffer(count);
        MovingAverageCore.WellesWilderMovingAverage(inputSpan, outputBuffer.Span, length);
        var wwmaList = outputBuffer.ToList();

        List<Signal>? signalsList = CreateSignalsList(stockData, count);
        for (var i = 0; i < count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var wwma = wwmaList[i];
            var prevWwma = i >= 1 ? wwmaList[i - 1] : 0;
            var signal = GetCompareSignal(currentValue - wwma, prevValue - prevWwma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Wwma", wwmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(wwmaList);
        stockData.IndicatorName = IndicatorName.WellesWilderMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Tillson T3 Moving Average
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <param name="vFactor">The v factor.</param>
    /// <returns></returns>
    public static StockData CalculateTillsonT3MovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length = 5, double vFactor = 0.7)
    {
        List<double> t3List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var c1 = -vFactor * vFactor * vFactor;
        var c2 = (3 * vFactor * vFactor) + (3 * vFactor * vFactor * vFactor);
        var c3 = (-6 * vFactor * vFactor) - (3 * vFactor) - (3 * vFactor * vFactor * vFactor);
        var c4 = 1 + (3 * vFactor) + (vFactor * vFactor * vFactor) + (3 * vFactor * vFactor);

        var ema1List = GetMovingAverageList(stockData, maType, length, inputList);
        var ema2List = GetMovingAverageList(stockData, maType, length, ema1List);
        var ema3List = GetMovingAverageList(stockData, maType, length, ema2List);
        var ema4List = GetMovingAverageList(stockData, maType, length, ema3List);
        var ema5List = GetMovingAverageList(stockData, maType, length, ema4List);
        var ema6List = GetMovingAverageList(stockData, maType, length, ema5List);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var ema6 = ema6List[i];
            var ema5 = ema5List[i];
            var ema4 = ema4List[i];
            var ema3 = ema3List[i];

            var prevT3 = GetLastOrDefault(t3List);
            var t3 = (c1 * ema6) + (c2 * ema5) + (c3 * ema4) + (c4 * ema3);
            t3List.Add(t3);

            var signal = GetCompareSignal(currentValue - t3, prevValue - prevT3);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "T3", t3List }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(t3List);
        stockData.IndicatorName = IndicatorName.TillsonT3MovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the triple exponential moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateTripleExponentialMovingAverage(this StockData stockData, 
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14)
    {
        List<double> temaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var ema1List = GetMovingAverageList(stockData, maType, length, inputList);
        var ema2List = GetMovingAverageList(stockData, maType, length, ema1List);
        var ema3List = GetMovingAverageList(stockData, maType, length, ema2List);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentEma1 = ema1List[i];
            var currentEma2 = ema2List[i];
            var currentEma3 = ema3List[i];

            var prevTema = GetLastOrDefault(temaList);
            var tema = (3 * currentEma1) - (3 * currentEma2) + currentEma3;
            temaList.Add(tema);

            var signal = GetCompareSignal(currentValue - tema, prevValue - prevTema);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Tema", temaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(temaList);
        stockData.IndicatorName = IndicatorName.TripleExponentialMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the volume weighted average price.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="inputName">Name of the input.</param>
    /// <returns></returns>
    public static StockData CalculateVolumeWeightedAveragePrice(this StockData stockData, InputName inputName = InputName.TypicalPrice)
    {
        List<double> vwapList = new(stockData.Count);
        List<double> tempVolList = new(stockData.Count);
        List<double> tempVolPriceList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        double tempVolSum = 0;
        double tempVolPriceSum = 0;
        var (inputList, _, _, _, _, volumeList) = GetInputValuesList(inputName, stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var currentVolume = volumeList[i];
            tempVolList.Add(currentVolume);
            tempVolSum += currentVolume;

            var volumePrice = currentValue * currentVolume;
            tempVolPriceList.Add(volumePrice);
            tempVolPriceSum += volumePrice;

            var volPriceSum = tempVolPriceSum;
            var volSum = tempVolSum;

            var prevVwap = GetLastOrDefault(vwapList);
            var vwap = volSum != 0 ? volPriceSum / volSum : 0;
            vwapList.Add(vwap);

            var signal = GetCompareSignal(currentValue - vwap, prevValue - prevVwap);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Vwap", vwapList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(vwapList);
        stockData.IndicatorName = IndicatorName.VolumeWeightedAveragePrice;

        return stockData;
    }


    /// <summary>
    /// Calculates the volume weighted moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateVolumeWeightedMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 14)
    {
        List<double> volumePriceList = new(stockData.Count);
        List<double> vwmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum volumePriceSum = new();
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);

        var volumeSmaList = GetMovingAverageList(stockData, maType, length, volumeList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentVolume = volumeList[i];
            var currentVolumeSma = volumeSmaList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var volumePrice = currentValue * currentVolume;
            volumePriceList.Add(volumePrice);
            volumePriceSum.Add(volumePrice);

            var volumePriceSma = volumePriceSum.Average(length);

            var prevVwma = GetLastOrDefault(vwmaList);
            var vwma = currentVolumeSma != 0 ? volumePriceSma / currentVolumeSma : 0;
            vwmaList.Add(vwma);

            var signal = GetCompareSignal(currentValue - vwma, prevValue - prevVwma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Vwma", vwmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(vwmaList);
        stockData.IndicatorName = IndicatorName.VolumeWeightedMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the ultimate moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="minLength">The minimum length.</param>
    /// <param name="maxLength">The maximum length.</param>
    /// <param name="acc">The acc.</param>
    /// <returns></returns>
    public static StockData CalculateUltimateMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int minLength = 5, int maxLength = 50, double acc = 1)
    {
        List<double> umaList = new(stockData.Count);
        List<double> posMoneyFlowList = new(stockData.Count);
        List<double> negMoneyFlowList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum posMoneyFlowSum = new();
        RollingSum negMoneyFlowSum = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var lenList = CalculateVariableLengthMovingAverage(stockData, maType, minLength, maxLength).OutputValues["Length"];
        var tpList = CalculateTypicalPrice(stockData).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;
            var currentVolume = stockData.Volumes[i];
            var typicalPrice = tpList[i];
            var prevTypicalPrice = i >= 1 ? tpList[i - 1] : 0;
            var length = MinOrMax(lenList[i], maxLength, minLength);
            var rawMoneyFlow = typicalPrice * currentVolume;

            var posMoneyFlow = i >= 1 && typicalPrice > prevTypicalPrice ? rawMoneyFlow : 0;
            posMoneyFlowList.Add(posMoneyFlow);
            posMoneyFlowSum.Add(posMoneyFlow);

            var negMoneyFlow = i >= 1 && typicalPrice < prevTypicalPrice ? rawMoneyFlow : 0;
            negMoneyFlowList.Add(negMoneyFlow);
            negMoneyFlowSum.Add(negMoneyFlow);

            var len = (int)length;
            var posMoneyFlowTotal = posMoneyFlowSum.Sum(len);
            var negMoneyFlowTotal = negMoneyFlowSum.Sum(len);
            var mfiRatio = negMoneyFlowTotal != 0 ? posMoneyFlowTotal / negMoneyFlowTotal : 0;
            var mfi = negMoneyFlowTotal == 0 ? 100 : posMoneyFlowTotal == 0 ? 0 : MinOrMax(100 - (100 / (1 + mfiRatio)), 100, 0);
            var mfScaled = (mfi * 2) - 100;
            var p = acc + (Math.Abs(mfScaled) / 25);
            double sum = 0, weightedSum = 0;
            for (var j = 0; j <= len - 1; j++)
            {
                var weight = Pow(len - j, p);
                var prevValue = i >= j ? inputList[i - j] : 0;

                sum += prevValue * weight;
                weightedSum += weight;
            }

            var prevUma = GetLastOrDefault(umaList);
            var uma = weightedSum != 0 ? sum / weightedSum : 0;
            umaList.Add(uma);

            var signal = GetCompareSignal(currentValue - uma, prevVal - prevUma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Uma", umaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(umaList);
        stockData.IndicatorName = IndicatorName.UltimateMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the variable length moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="minLength">The minimum length.</param>
    /// <param name="maxLength">The maximum length.</param>
    /// <returns></returns>
    public static StockData CalculateVariableLengthMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int minLength = 5, int maxLength = 50)
    {
        List<double> vlmaList = new(stockData.Count);
        List<double> lengthList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, maxLength, inputList);
        var stdDevList = CalculateStandardDeviationVolatility(stockData, maType, maxLength).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var sma = smaList[i];
            var stdDev = stdDevList[i];
            var a = sma - (1.75 * stdDev);
            var b = sma - (0.25 * stdDev);
            var c = sma + (0.25 * stdDev);
            var d = sma + (1.75 * stdDev);

            var prevLength = i >= 1 ? lengthList[i - 1] : maxLength;
            var length = MinOrMax(currentValue >= b && currentValue <= c ? prevLength + 1 : currentValue < a ||
                currentValue > d ? prevLength - 1 : prevLength, maxLength, minLength);
            lengthList.Add(length);

            var sc = 2 / (length + 1);
            var prevVlma = i >= 1 ? vlmaList[i - 1] : currentValue;
            var vlma = (currentValue * sc) + ((1 - sc) * prevVlma);
            vlmaList.Add(vlma);

            var signal = GetCompareSignal(currentValue - vlma, prevValue - prevVlma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Length", lengthList },
            { "Vlma", vlmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(vlmaList);
        stockData.IndicatorName = IndicatorName.VariableLengthMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Zero Low Lag Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="lag"></param>
    /// <returns></returns>
    public static StockData CalculateZeroLowLagMovingAverage(this StockData stockData, int length = 50, double lag = 1.4)
    {
        List<double> aList = new(stockData.Count);
        List<double> bList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var lbLength = MinOrMax((int)Math.Ceiling((double)length / 2));

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var priorB = i >= lbLength ? bList[i - lbLength] : currentValue;
            var priorA = i >= length ? aList[i - length] : 0;

            var prevA = GetLastOrDefault(aList);
            var a = (lag * currentValue) + ((1 - lag) * priorB) + prevA;
            aList.Add(a);

            var aDiff = a - priorA;
            var prevB = GetLastOrDefault(bList);
            var b = aDiff / length;
            bList.Add(b);

            var signal = GetCompareSignal(currentValue - b, prevValue - prevB);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Zllma", bList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(bList);
        stockData.IndicatorName = IndicatorName.ZeroLowLagMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Zero Lag Exponential Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateZeroLagExponentialMovingAverage(this StockData stockData,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14)
    {
        List<double> zemaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var ema1List = GetMovingAverageList(stockData, maType, length, inputList);
        var ema2List = GetMovingAverageList(stockData, maType, length, ema1List);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var ema1 = ema1List[i];
            var ema2 = ema2List[i];
            var d = ema1 - ema2;

            var prevZema = GetLastOrDefault(zemaList);
            var zema = ema1 + d;
            zemaList.Add(zema);

            var signal = GetCompareSignal(currentValue - zema, prevValue - prevZema);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Zema", zemaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(zemaList);
        stockData.IndicatorName = IndicatorName.ZeroLagExponentialMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Zero Lag Triple Exponential Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateZeroLagTripleExponentialMovingAverage(this StockData stockData,
        MovingAvgType maType = MovingAvgType.TripleExponentialMovingAverage, int length = 14)
    {
        List<double> zlTemaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var tma1List = GetMovingAverageList(stockData, maType, length, inputList);
        var tma2List = GetMovingAverageList(stockData, maType, length, tma1List);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var tma1 = tma1List[i];
            var tma2 = tma2List[i];
            var diff = tma1 - tma2;

            var prevZltema = GetLastOrDefault(zlTemaList);
            var zltema = tma1 + diff;
            zlTemaList.Add(zltema);

            var signal = GetCompareSignal(currentValue - zltema, prevValue - prevZltema);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ztema", zlTemaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(zlTemaList);
        stockData.IndicatorName = IndicatorName.ZeroLagTripleExponentialMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Windowed Volume Weighted Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateWindowedVolumeWeightedMovingAverage(this StockData stockData, int length = 100)
    {
        List<double> bartlettWList = new(stockData.Count);
        List<double> blackmanWList = new(stockData.Count);
        List<double> hanningWList = new(stockData.Count);
        List<double> bartlettVWList = new(stockData.Count);
        List<double> blackmanVWList = new(stockData.Count);
        List<double> hanningVWList = new(stockData.Count);
        List<double> bartlettWvwmaList = new(stockData.Count);
        List<double> blackmanWvwmaList = new(stockData.Count);
        List<double> hanningWvwmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum bartlettWSumWindow = new();
        RollingSum bartlettVWSumWindow = new();
        RollingSum blackmanWSumWindow = new();
        RollingSum blackmanVWSumWindow = new();
        RollingSum hanningWSumWindow = new();
        RollingSum hanningVWSumWindow = new();
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentVolume = volumeList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var iRatio = (double)i / length;
            var bartlett = 1 - (2 * Math.Abs(i - ((double)length / 2)) / length);

            var bartlettW = bartlett * currentVolume;
            bartlettWList.Add(bartlettW);
            bartlettWSumWindow.Add(bartlettW);

            var bartlettWSum = bartlettWSumWindow.Sum(length);
            var bartlettVW = currentValue * bartlettW;
            bartlettVWList.Add(bartlettVW);
            bartlettVWSumWindow.Add(bartlettVW);

            var bartlettVWSum = bartlettVWSumWindow.Sum(length);
            var prevBartlettWvwma = GetLastOrDefault(bartlettWvwmaList);
            var bartlettWvwma = bartlettWSum != 0 ? bartlettVWSum / bartlettWSum : 0;
            bartlettWvwmaList.Add(bartlettWvwma);

            var blackman = 0.42 - (0.5 * Math.Cos(2 * Math.PI * iRatio)) + (0.08 * Math.Cos(4 * Math.PI * iRatio));
            var blackmanW = blackman * currentVolume;
            blackmanWList.Add(blackmanW);
            blackmanWSumWindow.Add(blackmanW);

            var blackmanWSum = blackmanWSumWindow.Sum(length);
            var blackmanVW = currentValue * blackmanW;
            blackmanVWList.Add(blackmanVW);
            blackmanVWSumWindow.Add(blackmanVW);

            var blackmanVWSum = blackmanVWSumWindow.Sum(length);
            var blackmanWvwma = blackmanWSum != 0 ? blackmanVWSum / blackmanWSum : 0;
            blackmanWvwmaList.Add(blackmanWvwma);

            var hanning = 0.5 - (0.5 * Math.Cos(2 * Math.PI * iRatio));
            var hanningW = hanning * currentVolume;
            hanningWList.Add(hanningW);
            hanningWSumWindow.Add(hanningW);

            var hanningWSum = hanningWSumWindow.Sum(length);
            var hanningVW = currentValue * hanningW;
            hanningVWList.Add(hanningVW);
            hanningVWSumWindow.Add(hanningVW);

            var hanningVWSum = hanningVWSumWindow.Sum(length);
            var hanningWvwma = hanningWSum != 0 ? hanningVWSum / hanningWSum : 0;
            hanningWvwmaList.Add(hanningWvwma);

            var signal = GetCompareSignal(currentValue - bartlettWvwma, prevValue - prevBartlettWvwma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Wvwma", bartlettWvwmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(bartlettWvwmaList);
        stockData.IndicatorName = IndicatorName.WindowedVolumeWeightedMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Well Rounded Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateWellRoundedMovingAverage(this StockData stockData, int length = 14)
    {
        List<double> aList = new(stockData.Count);
        List<double> bList = new(stockData.Count);
        List<double> yList = new(stockData.Count);
        List<double> srcYList = new(stockData.Count);
        List<double> srcEmaList = new(stockData.Count);
        List<double> yEmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var alpha = (double)2 / (length + 1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevSrcY = i >= 1 ? srcYList[i - 1] : 0;
            var prevSrcEma = i >= 1 ? srcEmaList[i - 1] : 0;

            var prevA = GetLastOrDefault(aList);
            var a = prevA + (alpha * prevSrcY);
            aList.Add(a);

            var prevB = GetLastOrDefault(bList);
            var b = prevB + (alpha * prevSrcEma);
            bList.Add(b);

            var ab = a + b;
            var prevY = GetLastOrDefault(yList);
            var y = CalculateEMA(ab, prevY, 1);
            yList.Add(y);

            var srcY = currentValue - y;
            srcYList.Add(srcY);

            var prevYEma = GetLastOrDefault(yEmaList);
            var yEma = CalculateEMA(y, prevYEma, length);
            yEmaList.Add(yEma);

            var srcEma = currentValue - yEma;
            srcEmaList.Add(srcEma);

            var signal = GetCompareSignal(currentValue - y, prevValue - prevY);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Wrma", yList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(yList);
        stockData.IndicatorName = IndicatorName.WellRoundedMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Welles Wilder Summation
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateWellesWilderSummation(this StockData stockData, int length = 14)
    {
        List<double> sumList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevSum = GetLastOrDefault(sumList);
            var sum = prevSum - (prevSum / length) + currentValue;
            sumList.Add(sum);

            var signal = GetCompareSignal(currentValue - sum, prevValue - prevSum);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Wws", sumList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(sumList);
        stockData.IndicatorName = IndicatorName.WellesWilderSummation;

        return stockData;
    }


    /// <summary>
    /// Calculates the Trimean
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateTrimean(this StockData stockData, int length = 14)
    {
        List<double> tempList = new(stockData.Count);
        List<double> medianList = new(stockData.Count);
        List<double> q1List = new(stockData.Count);
        List<double> q3List = new(stockData.Count);
        List<double> trimeanList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        using var lookBackOrder = new RollingOrderStatistic(length);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevValue = GetLastOrDefault(tempList);
            var currentValue = inputList[i];
            tempList.Add(currentValue);
            lookBackOrder.Add(currentValue);

            var q1 = lookBackOrder.PercentileNearestRank(25);
            q1List.Add(q1);

            var median = lookBackOrder.PercentileNearestRank(50);
            medianList.Add(median);

            var q3 = lookBackOrder.PercentileNearestRank(75);
            q3List.Add(q3);

            var prevTrimean = GetLastOrDefault(trimeanList);
            var trimean = (q1 + (2 * median) + q3) / 4;
            trimeanList.Add(trimean);

            var signal = GetCompareSignal(currentValue - trimean, prevValue - prevTrimean);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Trimean", trimeanList },
            { "Q1", q1List },
            { "Median", medianList },
            { "Q3", q3List }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(trimeanList);
        stockData.IndicatorName = IndicatorName.Trimean;

        return stockData;
    }


    /// <summary>
    /// Calculates the Variable Index Dynamic Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateVariableIndexDynamicAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 14)
    {
        List<double> vidyaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var alpha = (double)2 / (length + 1);

        var cmoList = CalculateChandeMomentumOscillator(stockData, maType, length: length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentCmo = Math.Abs(cmoList[i] / 100);
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevVidya = GetLastOrDefault(vidyaList);
            var currentVidya = (currentValue * alpha * currentCmo) + (prevVidya * (1 - (alpha * currentCmo)));
            vidyaList.Add(currentVidya);

            var signal = GetCompareSignal(currentValue - currentVidya, prevValue - prevVidya);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Vidya", vidyaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(vidyaList);
        stockData.IndicatorName = IndicatorName.VariableIndexDynamicAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Symmetrically Weighted Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateSymmetricallyWeightedMovingAverage(this StockData stockData, int length = 14)
    {
        List<double> swmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var floorLength = (int)Math.Floor((double)length / 2);
        var roundLength = (int)Math.Round((double)length / 2);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            double nr = 0, nl = 0, sr = 0, sl = 0;
            if (floorLength == roundLength)
            {
                for (var j = 0; j <= floorLength - 1; j++)
                {
                    double wr = (length - (length - 1 - j)) * length;
                    var prevVal = i >= j ? inputList[i - j] : 0;
                    nr += wr;
                    sr += prevVal * wr;
                }

                for (var j = floorLength; j <= length - 1; j++)
                {
                    double wl = (length - j) * length;
                    var prevVal = i >= j ? inputList[i - j] : 0;
                    nl += wl;
                    sl += prevVal * wl;
                }
            }
            else
            {
                for (var j = 0; j <= floorLength; j++)
                {
                    double wr = (length - (length - 1 - j)) * length;
                    var prevVal = i >= j ? inputList[i - j] : 0;
                    nr += wr;
                    sr += prevVal * wr;
                }

                for (var j = roundLength; j <= length - 1; j++)
                {
                    double wl = (length - j) * length;
                    var prevVal = i >= j ? inputList[i - j] : 0;
                    nl += wl;
                    sl += prevVal * wl;
                }
            }

            var prevSwma = GetLastOrDefault(swmaList);
            var swma = nr + nl != 0 ? (sr + sl) / (nr + nl) : 0;
            swmaList.Add(swma);

            var signal = GetCompareSignal(currentValue - swma, prevValue - prevSwma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Swma", swmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(swmaList);
        stockData.IndicatorName = IndicatorName.SymmetricallyWeightedMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Volume Adjusted Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="factor"></param>
    /// <returns></returns>
    public static StockData CalculateVolumeAdjustedMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 14, double factor = 0.67)
    {
        List<double> priceVolumeRatioList = new(stockData.Count);
        List<double> priceVolumeRatioSumList = new(stockData.Count);
        List<double> vamaList = new(stockData.Count);
        List<double> volumeRatioList = new(stockData.Count);
        List<double> volumeRatioSumList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum volumeRatioSumWindow = new();
        RollingSum priceVolumeRatioSumWindow = new();
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);

        var volumeSmaList = GetMovingAverageList(stockData, maType, length, volumeList); ;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentVolume = volumeList[i];
            var currentValue = inputList[i];
            var volumeSma = volumeSmaList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var volumeIncrement = volumeSma * factor;

            var volumeRatio = volumeIncrement != 0 ? currentVolume / volumeIncrement : 0;
            volumeRatioList.Add(volumeRatio);
            volumeRatioSumWindow.Add(volumeRatio);

            var priceVolumeRatio = currentValue * volumeRatio;
            priceVolumeRatioList.Add(priceVolumeRatio);
            priceVolumeRatioSumWindow.Add(priceVolumeRatio);

            var volumeRatioSum = volumeRatioSumWindow.Sum(length);
            volumeRatioSumList.Add(volumeRatioSum);

            var priceVolumeRatioSum = priceVolumeRatioSumWindow.Sum(length);
            priceVolumeRatioSumList.Add(priceVolumeRatioSum);

            var prevVama = GetLastOrDefault(vamaList);
            var vama = volumeRatioSum != 0 ? priceVolumeRatioSum / volumeRatioSum : 0;
            vamaList.Add(vama);

            var signal = GetCompareSignal(currentValue - vama, prevValue - prevVama);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Vama", vamaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(vamaList);
        stockData.IndicatorName = IndicatorName.VolumeAdjustedMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Volatility Wave Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="kf"></param>
    /// <returns></returns>
    public static StockData CalculateVolatilityWaveMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length = 20, double kf = 2.5)
    {
        List<double> zlmapList = new(stockData.Count);
        List<double> pmaList = new(stockData.Count);
        List<double> pList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var s = MinOrMax((int)Math.Ceiling(Sqrt(length)));

        var stdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var stdDev = stdDevList[i];
            var currentValue = inputList[i];
            var sdPct = currentValue != 0 ? stdDev / currentValue * 100 : 0;

            var p = sdPct >= 0 ? MinOrMax(Sqrt(sdPct) * kf, 4, 1) : 1;
            pList.Add(p);
        }

        for (var i = 0; i < stockData.Count; i++)
        {
            var p = pList[i];

            double sum = 0, weightedSum = 0;
            for (var j = 0; j <= length - 1; j++)
            {
                var weight = Pow(length - j, p);
                var prevValue = i >= j ? inputList[i - j] : 0;

                sum += prevValue * weight;
                weightedSum += weight;
            }

            var pma = weightedSum != 0 ? sum / weightedSum : 0;
            pmaList.Add(pma);
        }

        var wmap1List = GetMovingAverageList(stockData, maType, s, pmaList);
        var wmap2List = GetMovingAverageList(stockData, maType, s, wmap1List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var wmap1 = wmap1List[i];
            var wmap2 = wmap2List[i];

            var prevZlmap = GetLastOrDefault(zlmapList);
            var zlmap = (2 * wmap1) - wmap2;
            zlmapList.Add(zlmap);

            var signal = GetCompareSignal(currentValue - zlmap, prevValue - prevZlmap);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Vwma", zlmapList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(zlmapList);
        stockData.IndicatorName = IndicatorName.VolatilityWaveMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Variable Adaptive Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateVariableAdaptiveMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 14)
    {
        List<double> vmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, openList, _) = GetInputValuesList(stockData);

        var cList = GetMovingAverageList(stockData, maType, length, inputList);
        var oList = GetMovingAverageList(stockData, maType, length, openList);
        var hList = GetMovingAverageList(stockData, maType, length, highList);
        var lList = GetMovingAverageList(stockData, maType, length, lowList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var c = cList[i];
            var o = oList[i];
            var h = hList[i];
            var l = lList[i];
            var lv = h - l != 0 ? MinOrMax(Math.Abs(c - o) / (h - l), 0.99, 0.01) : 0;

            var prevVma = i >= 1 ? vmaList[i - 1] : currentValue;
            var vma = (lv * currentValue) + ((1 - lv) * prevVma);
            vmaList.Add(vma);

            var signal = GetCompareSignal(currentValue - vma, prevValue - prevVma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Vama", vmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(vmaList);
        stockData.IndicatorName = IndicatorName.VariableAdaptiveMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Variable Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateVariableMovingAverage(this StockData stockData, int length = 6)
    {
        List<double> vmaList = new(stockData.Count);
        List<double> pdmsList = new(stockData.Count);
        List<double> pdisList = new(stockData.Count);
        List<double> mdmsList = new(stockData.Count);
        List<double> mdisList = new(stockData.Count);
        List<double> isList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingMinMax isWindow = new(length);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var k = (double)1 / length;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var pdm = Math.Max(MinPastValues(i, 1, currentValue - prevValue), 0);
            var mdm = Math.Max(MinPastValues(i, 1, prevValue - currentValue), 0);

            var prevPdms = GetLastOrDefault(pdmsList);
            var pdmS = ((1 - k) * prevPdms) + (k * pdm);
            pdmsList.Add(pdmS);

            var prevMdms = GetLastOrDefault(mdmsList);
            var mdmS = ((1 - k) * prevMdms) + (k * mdm);
            mdmsList.Add(mdmS);

            var s = pdmS + mdmS;
            var pdi = s != 0 ? pdmS / s : 0;
            var mdi = s != 0 ? mdmS / s : 0;

            var prevPdis = GetLastOrDefault(pdisList);
            var pdiS = ((1 - k) * prevPdis) + (k * pdi);
            pdisList.Add(pdiS);

            var prevMdis = GetLastOrDefault(mdisList);
            var mdiS = ((1 - k) * prevMdis) + (k * mdi);
            mdisList.Add(mdiS);

            var d = Math.Abs(pdiS - mdiS);
            var s1 = pdiS + mdiS;
            var dS1 = s1 != 0 ? d / s1 : 0;

            var prevIs = GetLastOrDefault(isList);
            var iS = ((1 - k) * prevIs) + (k * dS1);
            isList.Add(iS);
            isWindow.Add(iS);

            var hhv = isWindow.Max;
            var llv = isWindow.Min;
            var d1 = hhv - llv;
            var vI = d1 != 0 ? (iS - llv) / d1 : 0;

            var prevVma = GetLastOrDefault(vmaList);
            var vma = ((1 - k) * vI * prevVma) + (k * vI * currentValue);
            vmaList.Add(vma);

            var signal = GetCompareSignal(currentValue - vma, prevValue - prevVma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Vma", vmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(vmaList);
        stockData.IndicatorName = IndicatorName.VariableMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Volatility Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="lbLength"></param>
    /// <param name="smoothLength"></param>
    /// <param name="stdDevMult"></param>
    /// <returns></returns>
    public static StockData CalculateVolatilityMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 20, int lbLength = 10, int smoothLength = 3)
    {
        List<double> kList = new(stockData.Count);
        List<double> vma1List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, lbLength, inputList);
        var stdDevList = CalculateStandardDeviationVolatility(stockData, maType, lbLength).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var sma = smaList[i];
            var currentValue = inputList[i];
            var dev = stdDevList[i];
            var upper = sma + dev;
            var lower = sma - dev;

            var k = upper - lower != 0 ? (currentValue - sma) / (upper - lower) * 100 * 2 : 0;
            kList.Add(k);
        }

        var kMaList = GetMovingAverageList(stockData, maType, smoothLength, kList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var kMa = kMaList[i];
            var kNorm = Math.Min(Math.Max(kMa, -100), 100);
            var kAbs = Math.Round(Math.Abs(kNorm) / lbLength);
            var kRescaled = RescaleValue(kAbs, 10, 0, length, 0, true);
            var vLength = (int)Math.Round(Math.Max(kRescaled, 1));

            double sum = 0, weightedSum = 0;
            for (var j = 0; j <= vLength - 1; j++)
            {
                double weight = vLength - j;
                var prevValue = i >= j ? inputList[i - j] : 0;

                sum += prevValue * weight;
                weightedSum += weight;
            }

            var vma1 = weightedSum != 0 ? sum / weightedSum : 0;
            vma1List.Add(vma1);
        }

        var vma2List = GetMovingAverageList(stockData, maType, smoothLength, vma1List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var vma = vma2List[i];
            var prevVma = i >= 1 ? vma2List[i - 1] : 0;

            var signal = GetCompareSignal(currentValue - vma, prevValue - prevVma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Vma", vma2List }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(vma2List);
        stockData.IndicatorName = IndicatorName.VolatilityMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Vertical Horizontal Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateVerticalHorizontalMovingAverage(this StockData stockData, int length = 50)
    {
        List<double> changeList = new(stockData.Count);
        List<double> vhmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum changeSumWindow = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(inputList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var priorValue = i >= length ? inputList[i - length] : 0;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var highest = highestList[i];
            var lowest = lowestList[i];

            var priceChange = Math.Abs(currentValue - priorValue);
            changeList.Add(priceChange);
            changeSumWindow.Add(priceChange);

            var numerator = highest - lowest;
            var denominator = changeSumWindow.Sum(length);
            var vhf = denominator != 0 ? numerator / denominator : 0;

            var prevVhma = GetLastOrDefault(vhmaList);
            var vhma = prevVhma + (Pow(vhf, 2) * (currentValue - prevVhma));
            vhmaList.Add(vhma);

            var signal = GetCompareSignal(currentValue - vhma, prevValue - prevVhma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Vhma", vhmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(vhmaList);
        stockData.IndicatorName = IndicatorName.VerticalHorizontalMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the T Step Least Squares Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="sc"></param>
    /// <returns></returns>
    public static StockData CalculateTStepLeastSquaresMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 100, double sc = 0.5)
    {
        List<double> lsList = new(stockData.Count);
        List<double> bList = new(stockData.Count);
        List<double> chgList = new(stockData.Count);
        List<double> tempList = new(stockData.Count);
        List<double> corrList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingCorrelation corrWindow = new();
        double chgSum = 0;
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var efRatioList = CalculateKaufmanAdaptiveMovingAverage(stockData, length: length).OutputValues["Er"];
        var stdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            tempList.Add(currentValue);

            var efRatio = efRatioList[i];
            var prevB = i >= 1 ? bList[i - 1] : currentValue;
            var er = 1 - efRatio;

            var chg = Math.Abs(currentValue - prevB);
            chgList.Add(chg);
            chgSum += chg;

            var a = chgSum / chgList.Count * (1 + er);
            var b = currentValue > prevB + a ? currentValue : currentValue < prevB - a ? currentValue : prevB;
            bList.Add(b);

            corrWindow.Add(b, currentValue);
            var corr = corrWindow.R(length);
            corr = IsValueNullOrInfinity(corr) ? 0 : corr;
            corrList.Add((double)corr);
        }

        stockData.SetCustomValues(bList);
        var bStdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        var bSmaList = GetMovingAverageList(stockData, maType, length, bList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var corr = corrList[i];
            var stdDev = stdDevList[i];
            var bStdDev = bStdDevList[i];
            var bSma = bSmaList[i];
            var sma = smaList[i];
            var currentValue = inputList[i];
            var prevLs = i >= 1 ? lsList[i - 1] : currentValue;
            var b = bList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var tslsma = (sc * currentValue) + ((1 - sc) * prevLs);
            var alpha = bStdDev != 0 ? corr * stdDev / bStdDev : 0;
            var beta = sma - (alpha * bSma);

            var ls = (alpha * b) + beta;
            lsList.Add(ls);

            var signal = GetCompareSignal(currentValue - ls, prevValue - prevLs);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Tslsma", lsList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(lsList);
        stockData.IndicatorName = IndicatorName.TStepLeastSquaresMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Tillson IE2
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateTillsonIE2(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 15)
    {
        List<double> ie2List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);
        var linRegList = CalculateLinearRegression(stockData, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var sma = smaList[i];
            var a0 = linRegList[i];
            var a1 = i >= 1 ? linRegList[i - 1] : 0;
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var m = a0 - a1 + sma;

            var prevIe2 = GetLastOrDefault(ie2List);
            var ie2 = (m + a0) / 2;
            ie2List.Add(ie2);

            var signal = GetCompareSignal(currentValue - ie2, prevValue - prevIe2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ie2", ie2List }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(ie2List);
        stockData.IndicatorName = IndicatorName.TillsonIE2;

        return stockData;
    }


    /// <summary>
    /// Calculates the Spencer 21 Point Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <returns></returns>
    public static StockData CalculateSpencer21PointMovingAverage(this StockData stockData)
    {
        List<double> spmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            double sum = 0, weightedSum = 0;
            for (var j = 0; j <= 20; j++)
            {
                var weight = j switch
                {
                    0 => -1,
                    1 => -3,
                    2 => -5,
                    3 => -5,
                    4 => -2,
                    5 => 6,
                    6 => 18,
                    7 => 33,
                    8 => 47,
                    9 => 57,
                    10 => 60,
                    11 => 57,
                    12 => 47,
                    13 => 33,
                    14 => 18,
                    15 => 6,
                    16 => -2,
                    17 => -5,
                    18 => -5,
                    19 => -3,
                    20 => -1,
                    _ => 0,
                };
                var prevValue = i >= j ? inputList[i - j] : 0;

                sum += prevValue * weight;
                weightedSum += weight;
            }

            var prevSpma = GetLastOrDefault(spmaList);
            var spma = weightedSum != 0 ? sum / weightedSum : 0;
            spmaList.Add(spma);

            var signal = GetCompareSignal(currentValue - spma, prevVal - prevSpma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "S21ma", spmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(spmaList);
        stockData.IndicatorName = IndicatorName.Spencer21PointMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Spencer 15 Point Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <returns></returns>
    public static StockData CalculateSpencer15PointMovingAverage(this StockData stockData)
    {
        List<double> spmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            double sum = 0, weightedSum = 0;
            for (var j = 0; j <= 14; j++)
            {
                var weight = j switch
                {
                    0 => -3,
                    1 => -6,
                    2 => -5,
                    3 => 3,
                    4 => 21,
                    5 => 46,
                    6 => 67,
                    7 => 74,
                    8 => 67,
                    9 => 46,
                    10 => 21,
                    11 => 3,
                    12 => -5,
                    13 => -6,
                    14 => -3,
                    _ => 0,
                };
                var prevValue = i >= j ? inputList[i - j] : 0;

                sum += prevValue * weight;
                weightedSum += weight;
            }

            var prevSpma = GetLastOrDefault(spmaList);
            var spma = weightedSum != 0 ? sum / weightedSum : 0;
            spmaList.Add(spma);

            var signal = GetCompareSignal(currentValue - spma, prevVal - prevSpma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "S15ma", spmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(spmaList);
        stockData.IndicatorName = IndicatorName.Spencer15PointMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Square Root Weighted Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateSquareRootWeightedMovingAverage(this StockData stockData, int length = 14)
    {
        List<double> srwmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            double sum = 0, weightedSum = 0;
            for (var j = 0; j <= length - 1; j++)
            {
                var weight = Pow(length - j, 0.5);
                var prevValue = i >= j ? inputList[i - j] : 0;

                sum += prevValue * weight;
                weightedSum += weight;
            }

            var prevSrwma = GetLastOrDefault(srwmaList);
            var srwma = weightedSum != 0 ? sum / weightedSum : 0;
            srwmaList.Add(srwma);

            var signal = GetCompareSignal(currentValue - srwma, prevVal - prevSrwma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Srwma", srwmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(srwmaList);
        stockData.IndicatorName = IndicatorName.SquareRootWeightedMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Shapeshifting Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateShapeshiftingMovingAverage(this StockData stockData, int length = 50)
    {
        List<double> filtXList = new(stockData.Count);
        List<double> filtNList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            double sumX = 0, weightedSumX = 0, sumN = 0, weightedSumN = 0;
            for (var j = 0; j <= length - 1; j++)
            {
                var x = (double)j / (length - 1);
                var n = -1 + (x * 2);
                var wx = 1 - (2 * x / (Pow(x, 4) + 1));
                var wn = 1 - (2 * Pow(n, 2) / (Pow(n, 4 - (4 % 2)) + 1));
                var prevValue = i >= j ? inputList[i - j] : 0;

                sumX += prevValue * wx;
                weightedSumX += wx;
                sumN += prevValue * wn;
                weightedSumN += wn;
            }

            var prevFiltX = GetLastOrDefault(filtXList);
            var filtX = weightedSumX != 0 ? sumX / weightedSumX : 0;
            filtXList.Add(filtX);

            var filtN = weightedSumN != 0 ? sumN / weightedSumN : 0;
            filtNList.Add(filtN);

            var signal = GetCompareSignal(currentValue - filtX, prevVal - prevFiltX);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Sma", filtXList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(filtXList);
        stockData.IndicatorName = IndicatorName.ShapeshiftingMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Self Weighted Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateSelfWeightedMovingAverage(this StockData stockData, int length = 14)
    {
        List<double> wmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            double sum = 0, weightSum = 0;
            for (var j = 0; j < length; j++)
            {
                var pValue = i >= j ? inputList[i - j] : 0;
                var weight = i >= length + j ? inputList[i - (length + j)] : 0;
                weightSum += weight;
                sum += weight * pValue;
            }

            var prevWma = GetLastOrDefault(wmaList);
            var wma = weightSum != 0 ? sum / weightSum : 0;
            wmaList.Add(wma);

            var signal = GetCompareSignal(currentValue - wma, prevValue - prevWma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Swma", wmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(wmaList);
        stockData.IndicatorName = IndicatorName.SelfWeightedMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Sine Weighted Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateSineWeightedMovingAverage(this StockData stockData, int length = 14)
    {
        List<double> swmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            double sum = 0, weightedSum = 0;
            for (var j = 0; j <= length - 1; j++)
            {
                var weight = Math.Sin((j + 1) * Math.PI / (length + 1));
                var prevValue = i >= j ? inputList[i - j] : 0;

                sum += prevValue * weight;
                weightedSum += weight;
            }

            var prevSwma = GetLastOrDefault(swmaList);
            var swma = weightedSum != 0 ? sum / weightedSum : 0;
            swmaList.Add(swma);

            var signal = GetCompareSignal(currentValue - swma, prevVal - prevSwma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Swma", swmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(swmaList);
        stockData.IndicatorName = IndicatorName.SineWeightedMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Simplified Weighted Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateSimplifiedWeightedMovingAverage(this StockData stockData, int length = 14)
    {
        List<double> wmaList = new(stockData.Count);
        List<double> cmlList = new(stockData.Count);
        List<double> cmlSumList = new(stockData.Count);
        List<double> tempList = new(stockData.Count);
        List<double> sumList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        double tempSum = 0;
        double cmlSumTotal = 0;
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentValue = inputList[i];
            tempList.Add(currentValue);
            tempSum += currentValue;

            var cml = tempSum;
            cmlList.Add(cml);

            var prevCmlSum = i >= length ? cmlSumList[i - length] : 0;
            cmlSumTotal += cml;
            var cmlSum = cmlSumTotal;
            cmlSumList.Add(cmlSum);

            var prevSum = GetLastOrDefault(sumList);
            var sum = cmlSum - prevCmlSum;
            sumList.Add(sum);

            var prevWma = GetLastOrDefault(wmaList);
            var wma = ((length * cml) - prevSum) / (length * (double)(length + 1) / 2);
            wmaList.Add(wma);

            var signal = GetCompareSignal(currentValue - wma, prevValue - prevWma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Swma", wmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(wmaList);
        stockData.IndicatorName = IndicatorName.SimplifiedWeightedMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Simplified Least Squares Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateSimplifiedLeastSquaresMovingAverage(this StockData stockData, int length = 14)
    {
        List<double> cmlList = new(stockData.Count);
        List<double> cmlSumList = new(stockData.Count);
        List<double> tempList = new(stockData.Count);
        List<double> sumList = new(stockData.Count);
        List<double> lsmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        double tempSum = 0;
        double cmlSumTotal = 0;
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentValue = inputList[i];
            tempList.Add(currentValue);
            tempSum += currentValue;

            var prevCml = i >= length ? cmlList[i - length] : 0;
            var cml = tempSum;
            cmlList.Add(cml);

            var prevCmlSum = i >= length ? cmlSumList[i - length] : 0;
            cmlSumTotal += cml;
            var cmlSum = cmlSumTotal;
            cmlSumList.Add(cmlSum);

            var prevSum = GetLastOrDefault(sumList);
            var sum = cmlSum - prevCmlSum;
            sumList.Add(sum);

            var wma = ((length * cml) - prevSum) / (length * (double)(length + 1) / 2);
            var prevLsma = GetLastOrDefault(lsmaList);
            var lsma = length != 0 ? (3 * wma) - (2 * (cml - prevCml) / length) : 0;
            lsmaList.Add(lsma);

            var signal = GetCompareSignal(currentValue - lsma, prevValue - prevLsma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Slsma", lsmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(lsmaList);
        stockData.IndicatorName = IndicatorName.SimplifiedLeastSquaresMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Sharp Modified Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateSharpModifiedMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14)
    {
        List<double> shmmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentSma = smaList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            double slope = 0;
            for (var j = 1; j <= length; j++)
            {
                var prevValue = i >= j - 1 ? inputList[i - (j - 1)] : 0;
                double factor = 1 + (2 * (j - 1));
                slope += prevValue * (length - factor) / 2;
            }

            var prevShmma = GetLastOrDefault(shmmaList);
            var shmma = currentSma + (6 * slope / ((length + 1) * length));
            shmmaList.Add(shmma);

            var signal = GetCompareSignal(currentValue - shmma, prevVal - prevShmma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Smma", shmmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(shmmaList);
        stockData.IndicatorName = IndicatorName.SharpModifiedMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Slow Smoothed Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateSlowSmoothedMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 15)
    {
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var w2 = MinOrMax((int)Math.Ceiling((double)length / 3));
        var w1 = MinOrMax((int)Math.Ceiling((double)(length - w2) / 2));
        var w3 = MinOrMax((int)Math.Floor((double)(length - w2) / 2));

        var l1List = GetMovingAverageList(stockData, maType, w1, inputList);
        var l2List = GetMovingAverageList(stockData, maType, w2, l1List);
        var l3List = GetMovingAverageList(stockData, maType, w3, l2List);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var l3 = l3List[i];
            var prevL3 = i >= 1 ? l3List[i - 1] : 0;

            var signal = GetCompareSignal(currentValue - l3, prevValue - prevL3);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ssma", l3List }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(l3List);
        stockData.IndicatorName = IndicatorName.SlowSmoothedMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Sequentially Filtered Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateSequentiallyFilteredMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 50)
    {
        List<double> sfmaList = new(stockData.Count);
        List<double> signList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum signSumWindow = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var sma = smaList[i];
            var prevSma = i >= 1 ? smaList[i - 1] : 0;

            double a = Math.Sign(sma - prevSma);
            signList.Add(a);
            signSumWindow.Add(a);

            var sum = signSumWindow.Sum(length);
            double alpha = Math.Abs(sum) == length ? 1 : 0;
            var prevSfma = i >= 1 ? sfmaList[i - 1] : sma;
            var sfma = (alpha * sma) + ((1 - alpha) * prevSfma);
            sfmaList.Add(sfma);

            var signal = GetCompareSignal(currentValue - sfma, prevValue - prevSfma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Sfma", sfmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(sfmaList);
        stockData.IndicatorName = IndicatorName.SequentiallyFilteredMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Svama
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateSvama(this StockData stockData, int length = 14)
    {
        List<double> hList = new(stockData.Count);
        List<double> lList = new(stockData.Count);
        List<double> cMaxList = new(stockData.Count);
        List<double> cMinList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var a = volumeList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevH = i >= 1 ? hList[i - 1] : a;
            var h = a > prevH ? a : prevH;
            hList.Add(h);

            var prevL = i >= 1 ? lList[i - 1] : a;
            var l = a < prevL ? a : prevL;
            lList.Add(l);

            var bMax = h != 0 ? a / h : 0;
            var bMin = a != 0 ? l / a : 0;

            var prevCMax = i >= 1 ? cMaxList[i - 1] : currentValue;
            var cMax = (bMax * currentValue) + ((1 - bMax) * prevCMax);
            cMaxList.Add(cMax);

            var prevCMin = i >= 1 ? cMinList[i - 1] : currentValue;
            var cMin = (bMin * currentValue) + ((1 - bMin) * prevCMin);
            cMinList.Add(cMin);

            var signal = GetCompareSignal(currentValue - cMax, prevValue - prevCMax);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Svama", cMaxList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(cMaxList);
        stockData.IndicatorName = IndicatorName.Svama;

        return stockData;
    }


    /// <summary>
    /// Calculates the Setting Less Trend Step Filtering
    /// </summary>
    /// <param name="stockData"></param>
    /// <returns></returns>
    public static StockData CalculateSettingLessTrendStepFiltering(this StockData stockData)
    {
        List<double> chgList = new(stockData.Count);
        List<double> aList = new(stockData.Count);
        List<double> bList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        double chgSum = 0;
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevB = i >= 1 ? bList[i - 1] : currentValue;
            var prevA = GetLastOrDefault(aList);
            var sc = Math.Abs(currentValue - prevB) + prevA != 0 ? Math.Abs(currentValue - prevB) / (Math.Abs(currentValue - prevB) + prevA) : 0;
            var sltsf = (sc * currentValue) + ((1 - sc) * prevB);

            var chg = Math.Abs(sltsf - prevB);
            chgList.Add(chg);
            chgSum += chg;

            var a = chgSum / chgList.Count * (1 + sc);
            aList.Add(a);

            var b = sltsf > prevB + a ? sltsf : sltsf < prevB - a ? sltsf : prevB;
            bList.Add(b);

            var signal = GetCompareSignal(currentValue - b, prevValue - prevB);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Sltsf", bList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(bList);
        stockData.IndicatorName = IndicatorName.SettingLessTrendStepFiltering;

        return stockData;
    }

}

