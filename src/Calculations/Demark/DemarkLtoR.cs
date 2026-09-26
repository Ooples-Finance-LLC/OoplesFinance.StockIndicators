
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Demark Range Expansion Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateDemarkRangeExpansionIndex(this StockData stockData, int length = 5)
    {
        List<double> s2List = new(stockData.Count);
        List<double> s1List = new(stockData.Count);
        List<double> reiList = new(stockData.Count);
        var s1SumWindow = new RollingSum();
        var s2SumWindow = new RollingSum();
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var high = highList[i];
            var prevHigh2 = i >= 2 ? highList[i - 2] : 0;
            var prevHigh5 = i >= 5 ? highList[i - 5] : 0;
            var prevHigh6 = i >= 6 ? highList[i - 6] : 0;
            var low = lowList[i];
            var prevLow2 = i >= 2 ? lowList[i - 2] : 0;
            var prevLow5 = i >= 5 ? lowList[i - 5] : 0;
            var prevLow6 = i >= 6 ? lowList[i - 6] : 0;
            var prevClose7 = i >= 7 ? inputList[i - 7] : 0;
            var prevClose8 = i >= 8 ? inputList[i - 8] : 0;
            var prevRei1 = i >= 1 ? reiList[i - 1] : 0;
            var prevRei2 = i >= 2 ? reiList[i - 2] : 0;
            double n = (high >= prevLow5 || high >= prevLow6) && (low <= prevHigh5 || low <= prevHigh6) ? 0 : 1;
            double m = prevHigh2 >= prevClose8 && (prevLow2 <= prevClose7 || prevLow2 <= prevClose8) ? 0 : 1;
            var s = high - prevHigh2 + (low - prevLow2);

            var s1 = n * m * s;
            s1List.Add(s1);
            s1SumWindow.Add(s1);

            var s2 = Math.Abs(s);
            s2List.Add(s2);
            s2SumWindow.Add(s2);

            var s1Sum = s1SumWindow.Sum(length);
            var s2Sum = s2SumWindow.Sum(length);

            var rei = s2Sum != 0 ? s1Sum / s2Sum * 100 : 0;
            reiList.Add(rei);

            var signal = GetRsiSignal(rei - prevRei1, prevRei1 - prevRei2, rei, prevRei1, 100, -100);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Drei", reiList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(reiList);
        stockData.IndicatorName = IndicatorName.DemarkRangeExpansionIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Demark Pressure Ratio V1
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateDemarkPressureRatioV1(this StockData stockData, int length = 13)
    {
        List<double> output = new(stockData.Count);
        List<Signal>? signals = CreateSignalsList(stockData);
        var (close, _, _, _, _) = GetInputValuesList(stockData);
        var high = stockData.HighPrices; var low = stockData.LowPrices;
        var open = stockData.OpenPrices; var volume = stockData.Volumes;
        using var pressure = new DemarkPressureWindow(length, false);
        for (var i = 0; i < stockData.Count; i++)
        {
            var value = pressure.Next(open[i], high[i], low[i], close[i], volume[i], true);
            var previous = i == 0 ? 0 : output[i - 1];
            var beforePrevious = i < 2 ? 0 : output[i - 2];
            output.Add(value);
            signals?.Add(GetRsiSignal(value - previous, previous - beforePrevious, value, previous, 75, 25));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Dpr", output } });
        stockData.SetSignals(signals); stockData.SetCustomValues(output);
        stockData.IndicatorName = IndicatorName.DemarkPressureRatioV1;
        return stockData;
    }


    /// <summary>
    /// Calculates the Demark Pressure Ratio V2
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateDemarkPressureRatioV2(this StockData stockData, int length = 10)
    {
        List<double> output = new(stockData.Count);
        List<Signal>? signals = CreateSignalsList(stockData);
        var (close, _, _, _, _) = GetInputValuesList(stockData);
        var high = stockData.HighPrices; var low = stockData.LowPrices;
        var open = stockData.OpenPrices; var volume = stockData.Volumes;
        using var pressure = new DemarkPressureWindow(length, true);
        for (var i = 0; i < stockData.Count; i++)
        {
            var value = pressure.Next(open[i], high[i], low[i], close[i], volume[i], true);
            var previous = i == 0 ? 0 : output[i - 1];
            var beforePrevious = i < 2 ? 0 : output[i - 2];
            output.Add(value);
            signals?.Add(GetRsiSignal(value - previous, previous - beforePrevious, value, previous, 75, 25));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Dpr", output } });
        stockData.SetSignals(signals); stockData.SetCustomValues(output);
        stockData.IndicatorName = IndicatorName.DemarkPressureRatioV2;
        return stockData;
    }


    /// <summary>
    /// Calculates the Demark Reversal Points
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateDemarkReversalPoints(this StockData stockData, int length1 = 9, int length2 = 4)
    {
        List<double> drpPriceList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];

            double uCount = 0, dCount = 0;
            for (var j = 0; j < length1; j++)
            {
                var value = i >= j ? inputList[i - j] : 0;
                var prevValue = i >= j + length2 ? inputList[i - (j + length2)] : 0;

                uCount += value > prevValue ? 1 : 0;
                dCount += value < prevValue ? 1 : 0;
            }

            double drp = dCount == length1 ? 1 : uCount == length1 ? -1 : 0;
            var drpPrice = drp != 0 ? currentValue : 0;
            drpPriceList.Add(drpPrice);

            var signal = GetConditionSignal(drp > 0 || uCount > dCount, drp < 0 || dCount > uCount);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Drp", drpPriceList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(drpPriceList);
        stockData.IndicatorName = IndicatorName.DemarkReversalPoints;

        return stockData;
    }

}

