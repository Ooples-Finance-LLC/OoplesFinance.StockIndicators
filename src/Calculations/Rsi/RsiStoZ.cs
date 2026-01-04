using OoplesFinance.StockIndicators.Compatibility;
using OoplesFinance.StockIndicators.Core;

namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Volume Weighted Relative Strength Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateVolumeWeightedRelativeStrengthIndex(this StockData stockData,
        MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 10, int smoothLength = 3)
    {
        List<double> maxList = new(stockData.Count);
        List<double> minList = new(stockData.Count);
        List<double> rsiScaledList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var volume = volumeList[i];

            var max = Math.Max(MinPastValues(i, 1, currentValue - prevValue) * volume, 0);
            maxList.Add(max);

            var min = -Math.Min(MinPastValues(i, 1, currentValue - prevValue) * volume, 0);
            minList.Add(min);
        }

        var upList = GetMovingAverageList(stockData, maType, length, maxList);
        var dnList = GetMovingAverageList(stockData, maType, length, minList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var up = upList[i];
            var dn = dnList[i];
            var rsiRaw = dn == 0 ? 100 : up == 0 ? 0 : 100 - (100 / (1 + (up / dn)));

            var rsiScale = (rsiRaw * 2) - 100;
            rsiScaledList.Add(rsiScale);
        }

        var rsiList = GetMovingAverageList(stockData, maType, smoothLength, rsiScaledList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var rsi = rsiList[i];
            var prevRsi1 = i >= 1 ? rsiList[i - 1] : 0;
            var prevRsi2 = i >= 2 ? rsiList[i - 2] : 0;

            var signal = GetCompareSignal(rsi - prevRsi1, prevRsi1 - prevRsi2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Vwrsi", rsiList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(rsiList);
        stockData.IndicatorName = IndicatorName.VolumeWeightedRelativeStrengthIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Self Adjusting Relative Strength Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothingLength"></param>
    /// <param name="mult"></param>
    /// <returns></returns>
    public static StockData CalculateSelfAdjustingRelativeStrengthIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 14, int smoothingLength = 21, double mult = 2)
    {
        List<double> obList = new(stockData.Count);
        List<double> osList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var rsiList = CalculateRelativeStrengthIndex(stockData, maType, length: length).CustomValuesList;
        stockData.SetCustomValues(rsiList);
        var rsiStdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        var rsiSmaList = GetMovingAverageList(stockData, maType, smoothingLength, rsiList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var rsiStdDev = rsiStdDevList[i];
            var rsi = rsiList[i];
            var prevRsi = i >= 1 ? rsiList[i - 1] : 0;
            var adjustingStdDev = mult * rsiStdDev;
            var rsiSma = rsiSmaList[i];
            var prevRsiSma = i >= 1 ? rsiSmaList[i - 1] : 0;

            var obStdDev = 50 + adjustingStdDev;
            obList.Add(obStdDev);

            var osStdDev = 50 - adjustingStdDev;
            osList.Add(osStdDev);

            var signal = GetRsiSignal(rsi - rsiSma, prevRsi - prevRsiSma, rsi, prevRsi, obStdDev, osStdDev);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "SaRsi", rsiList },
            { "Signal", rsiSmaList },
            { "ObLevel", obList },
            { "OsLevel", osList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(rsiList);
        stockData.IndicatorName = IndicatorName.SelfAdjustingRelativeStrengthIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Stochastic Connors Relative Strength Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="smoothLength1"></param>
    /// <param name="smoothLength2"></param>
    /// <returns></returns>
    public static StockData CalculateStochasticConnorsRelativeStrengthIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, 
        int length1 = 2, int length2 = 3, int length3 = 100, int smoothLength1 = 3, int smoothLength2 = 3)
    {
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var connorsRsiList = CalculateConnorsRelativeStrengthIndex(stockData, maType, length1, length2, length3).CustomValuesList;
        stockData.SetCustomValues(connorsRsiList);
        var stochasticList = CalculateStochasticOscillator(stockData, maType, length2, smoothLength1, smoothLength2);
        var fastDList = stochasticList.OutputValues["FastD"];
        var slowDList = stochasticList.OutputValues["SlowD"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var smaK = fastDList[i];
            var smaD = slowDList[i];
            var prevSmak = i >= 1 ? fastDList[i - 1] : 0;
            var prevSmad = i >= 1 ? slowDList[i - 1] : 0;

            var signal = GetRsiSignal(smaK - smaD, prevSmak - prevSmad, smaK, prevSmak, 70, 30);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "SaRsi", fastDList },
            { "Signal", slowDList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(fastDList);
        stockData.IndicatorName = IndicatorName.StochasticConnorsRelativeStrengthIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Stochastic Relative Strength Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength1"></param>
    /// <param name="smoothLength2"></param>
    /// <returns></returns>
    public static StockData CalculateStochasticRelativeStrengthIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, 
        int length = 14, int smoothLength1 = 3, int smoothLength2 = 3)
    {
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var rsiList = CalculateRelativeStrengthIndex(stockData, maType, length: length).CustomValuesList;
        stockData.SetCustomValues(rsiList);
        var stoRsiList = CalculateStochasticOscillator(stockData, maType, length, smoothLength1, smoothLength2);
        var stochRsiList = stoRsiList.OutputValues["FastD"];
        var stochRsiSignalList = stoRsiList.OutputValues["SlowD"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentRSI = stochRsiList[i];
            var prevStochRsi = i >= 1 ? stochRsiList[i - 1] : 0;
            var currentRsiSignal = stochRsiSignalList[i];
            var prevRsiSignal = i >= 1 ? stochRsiSignalList[i - 1] : 0;

            var signal = GetRsiSignal(currentRSI - currentRsiSignal, prevStochRsi - prevRsiSignal, currentRSI, prevStochRsi, 80, 20);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "StochRsi", stochRsiList },
            { "Signal", stochRsiSignalList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(stochRsiList);
        stockData.IndicatorName = IndicatorName.StochasticRelativeStrengthIndex;

        return stockData;
    }
}

