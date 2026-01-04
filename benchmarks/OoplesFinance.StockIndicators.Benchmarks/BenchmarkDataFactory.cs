#if BASELINE
extern alias Original;
#endif

using System;
using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Models;

namespace OoplesFinance.StockIndicators.Benchmarks;

internal sealed record BenchmarkData(
    List<double> OpenPrices,
    List<double> HighPrices,
    List<double> LowPrices,
    List<double> ClosePrices,
    List<double> Volumes,
    List<DateTime> Dates);

internal static class BenchmarkDataFactory
{
    public static BenchmarkData CreateData(int count, int seed = 42)
    {
        var rand = new Random(seed);
        var openPrices = new List<double>(count);
        var highPrices = new List<double>(count);
        var lowPrices = new List<double>(count);
        var closePrices = new List<double>(count);
        var volumes = new List<double>(count);
        var dates = new List<DateTime>(count);

        var lastClose = 100d;
        var start = new DateTime(2020, 1, 1);
        for (var i = 0; i < count; i++)
        {
            var open = lastClose + ((rand.NextDouble() - 0.5) * 0.5);
            var drift = (rand.NextDouble() - 0.5) * 2;
            var close = Math.Max(1, open + drift);
            var high = Math.Max(open, close) + rand.NextDouble();
            var low = Math.Min(open, close) - rand.NextDouble();
            low = Math.Max(0.01, low);

            openPrices.Add(open);
            highPrices.Add(high);
            lowPrices.Add(low);
            closePrices.Add(close);
            volumes.Add(rand.Next(1000, 500000));
            dates.Add(start.AddMinutes(i));

            lastClose = close;
        }

        return new BenchmarkData(openPrices, highPrices, lowPrices, closePrices, volumes, dates);
    }

    public static StockData CreateStockData(BenchmarkData data)
    {
        return new StockData(
            data.OpenPrices,
            data.HighPrices,
            data.LowPrices,
            data.ClosePrices,
            data.Volumes,
            data.Dates,
            InputName.Close);
    }

    public static void Reset(StockData stockData)
    {
        stockData.CustomValuesList = new List<double>();
        stockData.OutputValues = new Dictionary<string, List<double>>();
        stockData.SignalsList = new List<Signal>();
        stockData.InputValues = stockData.ClosePrices;
        stockData.InputName = InputName.Close;
        stockData.IndicatorName = IndicatorName.None;
    }

#if BASELINE
    public static Original::OoplesFinance.StockIndicators.Models.StockData CreateBaselineStockData(BenchmarkData data)
    {
        return new Original::OoplesFinance.StockIndicators.Models.StockData(
            data.OpenPrices,
            data.HighPrices,
            data.LowPrices,
            data.ClosePrices,
            data.Volumes,
            data.Dates,
            Original::OoplesFinance.StockIndicators.Enums.InputName.Close);
    }

    public static void Reset(Original::OoplesFinance.StockIndicators.Models.StockData stockData)
    {
        stockData.CustomValuesList = new List<double>();
        stockData.OutputValues = new Dictionary<string, List<double>>();
        stockData.SignalsList = new List<Original::OoplesFinance.StockIndicators.Enums.Signal>();
        stockData.InputValues = stockData.ClosePrices;
        stockData.InputName = Original::OoplesFinance.StockIndicators.Enums.InputName.Close;
        stockData.IndicatorName = Original::OoplesFinance.StockIndicators.Enums.IndicatorName.None;
    }
#endif
}
