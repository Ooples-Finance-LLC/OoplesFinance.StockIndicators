using OoplesFinance.StockIndicators.Compatibility;
using OoplesFinance.StockIndicators.Core;

namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the relative strength index.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="movingAvgType">Average type of the moving.</param>
    /// <param name="length">The length.</param>
    /// <param name="signalLength">Length of the signal.</param>
    /// <returns></returns>
    public static StockData CalculateRelativeStrengthIndex(this StockData stockData, MovingAvgType movingAvgType = MovingAvgType.WildersSmoothingMethod,
        int length = 14, int signalLength = 3)
    {
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var count = inputList.Count;
        List<double> rsiList;
        List<double> rsiSignalList;

        if (movingAvgType == MovingAvgType.WildersSmoothingMethod)
        {
            var gainValues = new double[count];
            var lossValues = new double[count];

            for (var i = 0; i < count; i++)
            {
                var currentValue = inputList[i];
                var prevValue = i >= 1 ? inputList[i - 1] : 0;
                var priceChg = MinPastValues(i, 1, currentValue - prevValue);

                lossValues[i] = priceChg < 0 ? Math.Abs(priceChg) : 0;
                gainValues[i] = priceChg > 0 ? priceChg : 0;
            }

            var avgGainBuffer = SpanCompat.CreateOutputBuffer(count);
            var avgLossBuffer = SpanCompat.CreateOutputBuffer(count);
            MovingAverageCore.WellesWilderMovingAverage(gainValues, avgGainBuffer.Span, length);
            MovingAverageCore.WellesWilderMovingAverage(lossValues, avgLossBuffer.Span, length);

            var rsiBuffer = SpanCompat.CreateOutputBuffer(count);
            var rsiSpan = rsiBuffer.Span;
            for (var i = 0; i < count; i++)
            {
                var avgGain = avgGainBuffer.Span[i];
                var avgLoss = avgLossBuffer.Span[i];
                var rs = avgLoss != 0 ? avgGain / avgLoss : 0;

                rsiSpan[i] = avgLoss == 0 ? 100 : avgGain == 0 ? 0 : MinOrMax(100 - (100 / (1 + rs)), 100, 0);
            }

            rsiList = rsiBuffer.ToList();
            var rsiSignalBuffer = SpanCompat.CreateOutputBuffer(count);
            MovingAverageCore.WellesWilderMovingAverage(rsiSpan, rsiSignalBuffer.Span, signalLength);
            rsiSignalList = rsiSignalBuffer.ToList();
        }
        else
        {
            var lossList = new List<double>(count);
            var gainList = new List<double>(count);

            for (var i = 0; i < count; i++)
            {
                var currentValue = inputList[i];
                var prevValue = i >= 1 ? inputList[i - 1] : 0;
                var priceChg = MinPastValues(i, 1, currentValue - prevValue);

                var loss = priceChg < 0 ? Math.Abs(priceChg) : 0;
                lossList.Add(loss);

                var gain = priceChg > 0 ? priceChg : 0;
                gainList.Add(gain);
            }

            var avgGainList = GetMovingAverageList(stockData, movingAvgType, length, gainList);
            var avgLossList = GetMovingAverageList(stockData, movingAvgType, length, lossList);
            rsiList = new List<double>(count);
            for (var i = 0; i < count; i++)
            {
                var avgGain = avgGainList[i];
                var avgLoss = avgLossList[i];
                var rs = avgLoss != 0 ? avgGain / avgLoss : 0;

                var rsi = avgLoss == 0 ? 100 : avgGain == 0 ? 0 : MinOrMax(100 - (100 / (1 + rs)), 100, 0);
                rsiList.Add(rsi);
            }

            rsiSignalList = GetMovingAverageList(stockData, movingAvgType, signalLength, rsiList);
        }

        var rsiHistogramList = new List<double>(count);
        List<Signal>? signalsList = CreateSignalsList(stockData, count);
        double prevRsiHistogram = 0;
        for (var i = 0; i < count; i++)
        {
            var rsi = rsiList[i];
            var prevRsi = i >= 1 ? rsiList[i - 1] : 0;
            var rsiSignal = rsiSignalList[i];

            var rsiHistogram = rsi - rsiSignal;
            rsiHistogramList.Add(rsiHistogram);

            var signal = GetRsiSignal(rsiHistogram, prevRsiHistogram, rsi, prevRsi, 70, 30);
            signalsList?.Add(signal);
            prevRsiHistogram = rsiHistogram;
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Rsi", rsiList },
            { "Signal", rsiSignalList },
            { "Histogram", rsiHistogramList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(rsiList);
        stockData.IndicatorName = IndicatorName.RelativeStrengthIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Liquid Relative Strength Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateLiquidRelativeStrengthIndex(this StockData stockData, int length = 14)
    {
        List<double> numEmaList = new(stockData.Count);
        List<double> denEmaList = new(stockData.Count);
        List<double> cList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);

        var k = (double)1 / length;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentVolume = volumeList[i];
            var prevVolume = i >= 1 ? volumeList[i - 1] : 0;
            var a = MinPastValues(i, 1, currentValue - prevValue);
            var b = MinPastValues(i, 1, currentVolume - prevVolume);
            var prevC1 = i >= 1 ? cList[i - 1] : 0;
            var prevC2 = i >= 2 ? cList[i - 2] : 0;
            var num = Math.Max(a, 0) * Math.Max(b, 0);
            var den = Math.Abs(a) * Math.Abs(b);

            var prevNumEma = GetLastOrDefault(numEmaList);
            var numEma = (num * k) + (prevNumEma * (1 - k));
            numEmaList.Add(numEma);

            var prevDenEma = GetLastOrDefault(denEmaList);
            var denEma = (den * k) + (prevDenEma * (1 - k));
            denEmaList.Add(denEma);

            var c = denEma != 0 ? MinOrMax(100 * numEma / denEma, 100, 0) : 0;
            cList.Add(c);

            var signal = GetRsiSignal(c - prevC1, prevC1 - prevC2, c, prevC1, 80, 20);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Lrsi", cList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(cList);
        stockData.IndicatorName = IndicatorName.LiquidRelativeStrengthIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Rapid Relative Strength Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateRapidRelativeStrengthIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 14)
    {
        List<double> upChgList = new(stockData.Count);
        List<double> downChgList = new(stockData.Count);
        List<double> rapidRsiList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum upChgSumWindow = new();
        RollingSum downChgSumWindow = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var chg = MinPastValues(i, 1, currentValue - prevValue);

            var upChg = i >= 1 && chg > 0 ? chg : 0;
            upChgList.Add(upChg);
            upChgSumWindow.Add(upChg);

            var downChg = i >= 1 && chg < 0 ? Math.Abs(chg) : 0;
            downChgList.Add(downChg);
            downChgSumWindow.Add(downChg);

            var upChgSum = upChgSumWindow.Sum(length);
            var downChgSum = downChgSumWindow.Sum(length);
            var rs = downChgSum != 0 ? upChgSum / downChgSum : 0;

            var rapidRsi = downChgSum == 0 ? 100 : upChgSum == 0 ? 0 : MinOrMax(100 - (100 / (1 + rs)), 100, 0);
            rapidRsiList.Add(rapidRsi);
        }

        var rrsiEmaList = GetMovingAverageList(stockData, maType, length, rapidRsiList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var rapidRsi = rrsiEmaList[i];
            var prevRapidRsi1 = i >= 1 ? rrsiEmaList[i - 1] : 0;
            var prevRapidRsi2 = i >= 2 ? rrsiEmaList[i - 2] : 0;

            var signal = GetRsiSignal(rapidRsi - prevRapidRsi1, prevRapidRsi1 - prevRapidRsi2, rapidRsi, prevRapidRsi1, 70, 30);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Rrsi", rapidRsiList },
            { "Signal", rrsiEmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(rapidRsiList);
        stockData.IndicatorName = IndicatorName.RapidRelativeStrengthIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Recursive Relative Strength Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateRecursiveRelativeStrengthIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 14)
    {
        List<double> chgList = new(stockData.Count);
        List<double> bList = new(stockData.Count);
        List<double> avgRsiList = new(stockData.Count);
        List<double> avgList = new(stockData.Count);
        List<double> gainList = new(stockData.Count);
        List<double> lossList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum avgRsiSum = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= length ? inputList[i - length] : 0;

            var chg = MinPastValues(i, length, currentValue - prevValue);
            chgList.Add(chg);
        }

        var srcList = GetMovingAverageList(stockData, maType, length, chgList);
        stockData.SetCustomValues(srcList);
        var rsiList = CalculateRelativeStrengthIndex(stockData, length: length).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var rsi = rsiList[i];
            var src = srcList[i];
            var prevB1 = i >= 1 ? bList[i - 1] : 0;
            var prevB2 = i >= 2 ? bList[i - 2] : 0;

            double b = 0, avg = 0, gain = 0, loss = 0, avgRsi = 0;
            for (var j = 1; j <= length; j++)
            {
                var prevB = i >= j ? bList[i - j] : src;
                var prevAvg = i >= j ? avgList[i - j] : 0;
                var prevGain = i >= j ? gainList[i - j] : 0;
                var prevLoss = i >= j ? lossList[i - j] : 0;
                var k = (double)j / length;
                var a = rsi * ((double)length / j);
                avg = (a + prevB) / 2;
                var avgChg = avg - prevAvg;
                gain = avgChg > 0 ? avgChg : 0;
                loss = avgChg < 0 ? Math.Abs(avgChg) : 0;
                var avgGain = (gain * k) + (prevGain * (1 - k));
                var avgLoss = (loss * k) + (prevLoss * (1 - k));
                var rs = avgLoss != 0 ? avgGain / avgLoss : 0;
                avgRsi = avgLoss == 0 ? 100 : avgGain == 0 ? 0 : MinOrMax(100 - (100 / (1 + rs)), 1, 0);
                b = avgRsiList.Count >= length ? avgRsiSum.Average(length) : avgRsi;
            }
            bList.Add(b);
            avgList.Add(avg);
            gainList.Add(gain);
            lossList.Add(loss);
            avgRsiList.Add(avgRsi);
            avgRsiSum.Add(avgRsi);

            var signal = GetRsiSignal(b - prevB1, prevB1 - prevB2, b, prevB1, 0.8, 0.2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Rrsi", bList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(bList);
        stockData.IndicatorName = IndicatorName.RecursiveRelativeStrengthIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Momenta Relative Strength Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculateMomentaRelativeStrengthIndex(this StockData stockData, 
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 2, int length2 = 14)
    {
        List<double> rsiList = new(stockData.Count);
        List<double> srcLcList = new(stockData.Count);
        List<double> hcSrcList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(inputList, length1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var hc = highestList[i];
            var lc = lowestList[i];

            var srcLc = currentValue - lc;
            srcLcList.Add(srcLc);

            var hcSrc = hc - currentValue;
            hcSrcList.Add(hcSrc);
        }

        var topList = GetMovingAverageList(stockData, maType, length2, srcLcList);
        var botList = GetMovingAverageList(stockData, maType, length2, hcSrcList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var top = topList[i];
            var bot = botList[i];
            var rs = bot != 0 ? MinOrMax(top / bot, 1, 0) : 0;

            var rsi = bot == 0 ? 100 : top == 0 ? 0 : MinOrMax(100 - (100 / (1 + rs)), 100, 0);
            rsiList.Add(rsi);
        }

        var rsiEmaList = GetMovingAverageList(stockData, maType, length2, rsiList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var rsi = rsiList[i];
            var rsiEma = rsiEmaList[i];
            var prevRsi = i >= 1 ? rsiList[i - 1] : 0;
            var prevRsiEma = i >= 1 ? rsiEmaList[i - 1] : 0;

            var signal = GetRsiSignal(rsi - rsiEma, prevRsi - prevRsiEma, rsi, prevRsi, 80, 20);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Mrsi", rsiList },
            { "Signal", rsiEmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(rsiList);
        stockData.IndicatorName = IndicatorName.MomentaRelativeStrengthIndex;

        return stockData;
    }

}

