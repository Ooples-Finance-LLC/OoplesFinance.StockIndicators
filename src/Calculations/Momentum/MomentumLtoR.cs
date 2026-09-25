
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the price momentum oscillator.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length1">The length1.</param>
    /// <param name="length2">The length2.</param>
    /// <param name="signalLength">Length of the signal.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculatePriceMomentumOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 35,
        int length2 = 20, int signalLength = 10)
    {
        if (StrengthWindow.Supports(maType) && !Builder.Compute.ComponentAverage.HasOverrides)
        {
            var (stableInput, _, _, _, _) = GetInputValuesList(stockData);
            var stableLine = new List<double>(stockData.Count);
            var stableSignal = new List<double>(stockData.Count);
            var stableHistogram = new List<double>(stockData.Count);
            var stableSignals = CreateSignalsList(stockData);
            using var stableWindow = new PriceMomentumWindow(maType, length1, length2, signalLength, stockData.Count);
            double previousDifference = 0;
            foreach (var price in stableInput)
            {
                var next = stableWindow.Next(price, true);
                stableLine.Add(next.Value); stableSignal.Add(next.Signal); stableHistogram.Add(next.Histogram);
                stableSignals?.Add(GetCompareSignal(next.Histogram, previousDifference));
                previousDifference = next.Histogram;
            }
            stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Pmo", stableLine }, { "Signal", stableSignal } });
            stockData.SetSignals(stableSignals); stockData.SetCustomValues(stableLine);
            stockData.IndicatorName = IndicatorName.PriceMomentumOscillator;
            return stockData;
        }

        List<double> pmoList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        using var fixedStages = new PriceMomentumWindow(MovingAvgType.ExponentialMovingAverage, length1, length2, 1, stockData.Count);
        foreach (var price in inputList) pmoList.Add(fixedStages.Next(price, true).Value);

        var pmoSignalList = GetMovingAverageList(stockData, maType, signalLength, pmoList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var pmo = pmoList[i];
            var prevPmo = i >= 1 ? pmoList[i - 1] : 0;
            var pmoSignal = pmoSignalList[i];
            var prevPmoSignal = i >= 1 ? pmoSignalList[i - 1] : 0;

            var signal = GetCompareSignal(pmo - pmoSignal, prevPmo - prevPmoSignal);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Pmo", pmoList },
            { "Signal", pmoSignalList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(pmoList);
        stockData.IndicatorName = IndicatorName.PriceMomentumOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Momentum Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateMomentumOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length = 14)
    {
        List<double> momentumOscillatorList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentPrice = inputList[i];
            var prevPrice = i >= length ? inputList[i - length] : 0;

            var momentumOscillator = RoundedMomentumRatio.Of(currentPrice, prevPrice);
            momentumOscillatorList.Add(momentumOscillator);
        }

        var finiteInput = FiniteSignalInput.Create(momentumOscillatorList, out var finiteCount);
        var emaList = GetMovingAverageList(stockData, maType, length, finiteInput);
        if (maType == MovingAvgType.SimpleMovingAverage)
        {
            using var mean = new OoplesFinance.StockIndicators.Streaming.RoundedSimpleMovingAverageSmoother(length);
            for (var i = 0; i < finiteCount; i++) emaList[i] = mean.Next(finiteInput[i], true);
        }
        for (var i = finiteCount; i < emaList.Count; i++) emaList[i] = double.NaN;
        for (var i = 0; i < stockData.Count; i++)
        {
            var momentum = emaList[i];
            var prevMomentum = i >= 1 ? emaList[i - 1] : 0;

            var signal = GetCompareSignal(momentum, prevMomentum);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Mo", momentumOscillatorList },
            { "Signal", emaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(momentumOscillatorList);
        stockData.IndicatorName = IndicatorName.MomentumOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Relative Momentum Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateRelativeMomentumIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod,
        int length1 = 14, int length2 = 3)
    {
        if (StrengthWindow.Supports(maType) && !Builder.Compute.ComponentAverage.HasOverrides)
        {
            var (prices, _, _, _, _) = GetInputValuesList(stockData);
            var line = new List<double>(stockData.Count); var signal = new List<double>(stockData.Count); var histogram = new List<double>(stockData.Count);
            var events = CreateSignalsList(stockData);
            using var window = new RelativeMomentumWindow(maType, length1, length2, stockData.Count);
            double previous = 0, previousHistogram = 0;
            foreach (var price in prices)
            {
                var next = window.Next(price, true);
                line.Add(next.Value); signal.Add(next.Signal); histogram.Add(next.Histogram);
                events?.Add(GetRsiSignal(next.Histogram, previousHistogram, next.Value, previous, 70, 30));
                previous = next.Value; previousHistogram = next.Histogram;
            }
            stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Rmi", line }, { "Signal", signal }, { "Histogram", histogram } });
            stockData.SetSignals(events); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.RelativeMomentumIndex;
            return stockData;
        }

        List<double> rsiList = new(stockData.Count);
        List<double> lossList = new(stockData.Count);
        List<double> gainList = new(stockData.Count);
        List<double> rsiHistogramList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= length2 ? inputList[i - length2] : 0;
            var priceChg = MinPastValues(i, length2, currentValue - prevValue);

            var loss = i >= length2 && priceChg < 0 ? Math.Abs(priceChg) : 0;
            lossList.Add(loss);

            var gain = i >= length2 && priceChg > 0 ? priceChg : 0;
            gainList.Add(gain);
        }

        var avgGainList = GetMovingAverageList(stockData, maType, length1, gainList);
        var avgLossList = GetMovingAverageList(stockData, maType, length1, lossList);
        for (var i = 0; i < inputList.Count; i++)
        {
            var avgGain = avgGainList[i];
            var avgLoss = avgLossList[i];
            var rs = avgLoss != 0 ? avgGain / avgLoss : 0;

            var rsi = avgLoss == 0 ? 100 : avgGain == 0 ? 0 : MinOrMax(100 - (100 / (1 + rs)), 100, 0);
            rsiList.Add(rsi);
        }

        var rsiSignalList = GetMovingAverageList(stockData, maType, length1, rsiList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var rsi = rsiList[i];
            var rsiSignal = rsiSignalList[i];
            var prevRsi = i >= 1 ? rsiList[i - 1] : 0;

            var prevRsiHistogram = GetLastOrDefault(rsiHistogramList);
            var rsiHistogram = rsi - rsiSignal;
            rsiHistogramList.Add(rsiHistogram);

            var signal = GetRsiSignal(rsiHistogram, prevRsiHistogram, rsi, prevRsi, 70, 30);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Rmi", rsiList },
            { "Signal", rsiSignalList },
            { "Histogram", rsiHistogramList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(rsiList);
        stockData.IndicatorName = IndicatorName.RelativeMomentumIndex;

        return stockData;
    }

}

