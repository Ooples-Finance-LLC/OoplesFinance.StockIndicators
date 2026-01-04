using OoplesFinance.StockIndicators.Compatibility;
using OoplesFinance.StockIndicators.Core;

namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the simple moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateSimpleMovingAverage(this StockData stockData, int length = 14)
    {
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var count = inputList.Count;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var outputBuffer = SpanCompat.CreateOutputBuffer(count);
        var smaSpan = outputBuffer.Span;
        MovingAverageCore.SimpleMovingAverage(inputSpan, smaSpan, length);
        var smaList = outputBuffer.ToList();

        List<Signal>? signalsList = CreateSignalsList(stockData, count);
        for (var i = 0; i < count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var sma = smaList[i];
            var prevSma = i >= 1 ? smaList[i - 1] : 0;
            var signal = GetCompareSignal(currentValue - sma, prevValue - prevSma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Sma", smaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(smaList);
        stockData.IndicatorName = IndicatorName.SimpleMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the weighted moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateWeightedMovingAverage(this StockData stockData, int length = 14)
    {
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var count = inputList.Count;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var outputBuffer = SpanCompat.CreateOutputBuffer(count);
        var wmaSpan = outputBuffer.Span;
        MovingAverageCore.WeightedMovingAverage(inputSpan, wmaSpan, length);
        var wmaList = outputBuffer.ToList();

        List<Signal>? signalsList = CreateSignalsList(stockData, count);
        for (var i = 0; i < count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;
            var wma = wmaList[i];
            var prevWma = i >= 1 ? wmaList[i - 1] : 0;
            var signal = GetCompareSignal(currentValue - wma, prevVal - prevWma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Wma", wmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(wmaList);
        stockData.IndicatorName = IndicatorName.WeightedMovingAverage;

        return stockData;
    }
}



