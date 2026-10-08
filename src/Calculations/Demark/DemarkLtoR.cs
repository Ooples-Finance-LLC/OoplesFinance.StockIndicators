
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
        List<double> values=new(stockData.Count);var signals=CreateSignalsList(stockData);
        var (input,_,_,_,_)=GetInputValuesList(stockData);var window=new DemarkRangeWindow(length);
        for(var i=0;i<stockData.Count;i++)
        {
            var value=window.Next(stockData.HighPrices[i],stockData.LowPrices[i],input[i],true);
            var previous=i>0?values[i-1]:0;var before=i>1?values[i-2]:0;
            signals?.Add(GetRsiSignal(value-previous,previous-before,value,previous,100,-100));values.Add(value);
        }
        stockData.SetOutputValues(()=>new Dictionary<string,List<double>>{{"Drei",values}});
        stockData.SetSignals(signals);stockData.SetCustomValues(values);stockData.IndicatorName=IndicatorName.DemarkRangeExpansionIndex;return stockData;
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

