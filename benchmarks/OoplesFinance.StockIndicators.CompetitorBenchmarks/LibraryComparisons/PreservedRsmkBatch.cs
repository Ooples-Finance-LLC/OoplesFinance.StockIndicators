using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Models;
using OoplesFinance.StockIndicators.Helpers;
using static OoplesFinance.StockIndicators.Helpers.CalculationsHelper;
using static OoplesFinance.StockIndicators.Helpers.SignalHelper;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Exact original batch from 71c31b97; only method name/access changed.
internal static class PreservedRsmkBatch
{
    internal static StockData Run(StockData stockData, StockData marketData,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 90, int smoothLength = 3)
    {
        if (stockData.Count != marketData.Count)
            throw new ArgumentException("Primary and benchmark batches must have equal counts.", nameof(marketData));
        length = Math.Max(1, length);
        smoothLength = Math.Max(1, smoothLength);
        List<double> rsmkList = new(stockData.Count);
        List<double> logRatioList = new(stockData.Count);
        List<double> logDiffList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var (spInputList, _, _, _, _) = GetInputValuesList(marketData);

        for (var i = 0; i < stockData.Count; i++)
        {
            if (stockData.Dates[i] != marketData.Dates[i] || stockData.Dates[i].Kind != marketData.Dates[i].Kind
                || i > 0 && (stockData.Dates[i] <= stockData.Dates[i - 1] || stockData.Dates[i].Kind != stockData.Dates[i - 1].Kind))
                throw new ArgumentException("RSMK requires aligned, strictly increasing timestamps with a consistent kind.", nameof(marketData));
            if (!(inputList[i] > 0) || double.IsInfinity(inputList[i]) || !(spInputList[i] > 0) || double.IsInfinity(spInputList[i]))
                throw new ArgumentOutOfRangeException(nameof(stockData), "RSMK requires positive finite primary and benchmark inputs.");
        }

        if (stockData.Count == marketData.Count)
        {
            for (var i = 0; i < stockData.Count; i++)
            {
                var currentValue = inputList[i];
                var spValue = spInputList[i];
                var prevLogRatio = i >= length ? logRatioList[i - length] : 0;

                var logRatio = Math.Log(currentValue)-Math.Log(spValue);
                logRatioList.Add(logRatio);

                var logDiff = i < length ? 0 : logRatio - prevLogRatio;
                logDiffList.Add(logDiff);
            }

            var logDiffEmaList = GetMovingAverageList(stockData, maType, smoothLength, logDiffList);
            for (var i = 0; i < stockData.Count; i++)
            {
                var logDiffEma = logDiffEmaList[i];

                var prevRsmk = GetLastOrDefault(rsmkList);
                var rsmk = logDiffEma * 100;
                rsmkList.Add(rsmk);

                var signal = GetCompareSignal(rsmk, prevRsmk);
                signalsList?.Add(signal);
            }
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Rsmk", rsmkList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(rsmkList);
        stockData.IndicatorName = IndicatorName.RSMKIndicator;

        return stockData;
    }
}
