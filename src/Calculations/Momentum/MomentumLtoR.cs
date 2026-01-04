
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
    public static StockData CalculatePriceMomentumOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 35,
        int length2 = 20, int signalLength = 10)
    {
        List<double> pmoList = new(stockData.Count);
        List<double> rocMaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var sc1 = 2 / (double)length1;
        var sc2 = 2 / (double)length2;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var roc = prevValue != 0 ? MinPastValues(i, 1, currentValue - prevValue) / prevValue * 100 : 0;

            var prevRocMa1 = GetLastOrDefault(rocMaList);
            var rocMa = prevRocMa1 + ((roc - prevRocMa1) * sc1);
            rocMaList.Add(rocMa);

            var prevPmo = GetLastOrDefault(pmoList);
            var pmo = prevPmo + (((rocMa * 10) - prevPmo) * sc2);
            pmoList.Add(pmo);
        }

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

            var momentumOscillator = prevPrice != 0 ? currentPrice / prevPrice * 100 : 0;
            momentumOscillatorList.Add(momentumOscillator);
        }

        var emaList = GetMovingAverageList(stockData, maType, length, momentumOscillatorList);
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
    public static StockData CalculateRelativeMomentumIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod,
        int length1 = 14, int length2 = 3)
    {
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

