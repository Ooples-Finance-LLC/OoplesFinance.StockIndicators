
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Standard Pivot Points
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="inputLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateStandardPivotPoints(this StockData stockData, InputLength inputLength = InputLength.Day)
    {
        List<double> pivotList = new(stockData.Count);
        List<double> resistanceLevel3List = new(stockData.Count);
        List<double> resistanceLevel2List = new(stockData.Count);
        List<double> resistanceLevel1List = new(stockData.Count);
        List<double> supportLevel1List = new(stockData.Count);
        List<double> supportLevel2List = new(stockData.Count);
        List<double> supportLevel3List = new(stockData.Count);
        List<double> midpoint1List = new(stockData.Count);
        List<double> midpoint2List = new(stockData.Count);
        List<double> midpoint3List = new(stockData.Count);
        List<double> midpoint4List = new(stockData.Count);
        List<double> midpoint5List = new(stockData.Count);
        List<double> midpoint6List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, openList, _) = PivotPeriodInputs.Read(stockData, inputLength);

        for (var i = 0; i < inputList.Count; i++)
        {
            var currentClose = inputList[i];
            var prevClose = i >= 1 ? inputList[i - 1] : 0;
            var prevLow = i >= 1 ? lowList[i - 1] : 0;
            var prevHigh = i >= 1 ? highList[i - 1] : 0;
            var prevOpen = i >= 1 ? openList[i - 1] : 0;

            var prevPivot = GetLastOrDefault(pivotList);
            var levels = DailyPivotMath.Levels(prevOpen,prevHigh,prevLow,prevClose,true);
            var pivot = levels[0];
            pivotList.Add(pivot);

            var supportLevel1 = levels[1];
            supportLevel1List.Add(supportLevel1);

            var resistanceLevel1 = levels[4];
            resistanceLevel1List.Add(resistanceLevel1);

            var supportLevel2 = levels[2];
            supportLevel2List.Add(supportLevel2);

            var resistanceLevel2 = levels[5];
            resistanceLevel2List.Add(resistanceLevel2);

            var supportLevel3 = levels[3];
            supportLevel3List.Add(supportLevel3);

            var resistanceLevel3 = levels[6];
            resistanceLevel3List.Add(resistanceLevel3);

            var midpoint1 = levels[7];
            midpoint1List.Add(midpoint1);

            var midpoint2 = levels[8];
            midpoint2List.Add(midpoint2);

            var midpoint3 = levels[9];
            midpoint3List.Add(midpoint3);

            var midpoint4 = levels[10];
            midpoint4List.Add(midpoint4);

            var midpoint5 = levels[11];
            midpoint5List.Add(midpoint5);

            var midpoint6 = levels[12];
            midpoint6List.Add(midpoint6);

            var signal = GetCompareSignal(currentClose - pivot, prevClose - prevPivot);
            signalsList?.Add(signal);
        }


        // BAR ALIGNMENT. Everything above is computed per PERIOD; the StockData this is stored on is
        // per BAR, and callers index the two in parallel. Project each series onto the bars of its own
        // period before storing. Causal: a period's level derives from the PRECEDING period, so it is
        // already known when its own period opens.
        var barGroupIndexes = GetInputLengthGroupIndexes(stockData, inputLength);
        pivotList = ExpandPeriodValuesToBars(pivotList, barGroupIndexes);
        resistanceLevel3List = ExpandPeriodValuesToBars(resistanceLevel3List, barGroupIndexes);
        resistanceLevel2List = ExpandPeriodValuesToBars(resistanceLevel2List, barGroupIndexes);
        resistanceLevel1List = ExpandPeriodValuesToBars(resistanceLevel1List, barGroupIndexes);
        supportLevel1List = ExpandPeriodValuesToBars(supportLevel1List, barGroupIndexes);
        supportLevel2List = ExpandPeriodValuesToBars(supportLevel2List, barGroupIndexes);
        supportLevel3List = ExpandPeriodValuesToBars(supportLevel3List, barGroupIndexes);
        midpoint1List = ExpandPeriodValuesToBars(midpoint1List, barGroupIndexes);
        midpoint2List = ExpandPeriodValuesToBars(midpoint2List, barGroupIndexes);
        midpoint3List = ExpandPeriodValuesToBars(midpoint3List, barGroupIndexes);
        midpoint4List = ExpandPeriodValuesToBars(midpoint4List, barGroupIndexes);
        midpoint5List = ExpandPeriodValuesToBars(midpoint5List, barGroupIndexes);
        midpoint6List = ExpandPeriodValuesToBars(midpoint6List, barGroupIndexes);

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Pivot", pivotList },
            { "S1", supportLevel1List },
            { "S2", supportLevel2List },
            { "S3", supportLevel3List },
            { "R1", resistanceLevel1List },
            { "R2", resistanceLevel2List },
            { "R3", resistanceLevel3List },
            { "M1", midpoint1List },
            { "M2", midpoint2List },
            { "M3", midpoint3List },
            { "M4", midpoint4List },
            { "M5", midpoint5List },
            { "M6", midpoint6List }
        });
        // Signals are produced per PERIOD in the loop above, exactly like the levels, so they need
        // the same projection. Leaving them period-length puts every signal on the wrong bar and
        // reintroduces on SignalsList the misalignment this method just removed from its levels.
        if (signalsList is not null)
        {
            var barAlignedSignals = ExpandPeriodItemsToBars(signalsList, barGroupIndexes, Signal.None);
            signalsList.Clear();
            signalsList.AddRange(barAlignedSignals);
        }

        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(pivotList);
        stockData.IndicatorName = IndicatorName.StandardPivotPoints;

        return stockData;
    }


    /// <summary>
    /// Calculates the Woodie Pivot Points
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="inputLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateWoodiePivotPoints(this StockData stockData, InputLength inputLength = InputLength.Day)
    {
        List<double> pivotList = new(stockData.Count);
        List<double> resistanceLevel1List = new(stockData.Count);
        List<double> resistanceLevel2List = new(stockData.Count);
        List<double> resistanceLevel3List = new(stockData.Count);
        List<double> resistanceLevel4List = new(stockData.Count);
        List<double> supportLevel1List = new(stockData.Count);
        List<double> supportLevel2List = new(stockData.Count);
        List<double> supportLevel3List = new(stockData.Count);
        List<double> supportLevel4List = new(stockData.Count);
        List<double> midpoint1List = new(stockData.Count);
        List<double> midpoint2List = new(stockData.Count);
        List<double> midpoint3List = new(stockData.Count);
        List<double> midpoint4List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = PivotPeriodInputs.Read(stockData, inputLength);

        for (var i = 0; i < inputList.Count; i++)
        {
            var currentClose = inputList[i];
            var prevHigh = i >= 1 ? highList[i - 1] : 0;
            var prevLow = i >= 1 ? lowList[i - 1] : 0;
            var prevClose = i >= 1 ? inputList[i - 1] : 0;

            var prevPivot = GetLastOrDefault(pivotList);
            var levels = WoodiePivotMath.Levels(prevHigh,prevLow,prevClose);
            var pivot = levels[0];
            pivotList.Add(pivot);

            var supportLevel1 = levels[1];
            supportLevel1List.Add(supportLevel1);

            var resistanceLevel1 = levels[5];
            resistanceLevel1List.Add(resistanceLevel1);

            var supportLevel2 = levels[2];
            supportLevel2List.Add(supportLevel2);

            var resistanceLevel2 = levels[6];
            resistanceLevel2List.Add(resistanceLevel2);

            var supportLevel3 = levels[3];
            supportLevel3List.Add(supportLevel3);

            var resistanceLevel3 = levels[7];
            resistanceLevel3List.Add(resistanceLevel3);

            var supportLevel4 = levels[4];
            supportLevel4List.Add(supportLevel4);

            var resistanceLevel4 = levels[8];
            resistanceLevel4List.Add(resistanceLevel4);

            var midpoint1 = levels[9];
            midpoint1List.Add(midpoint1);

            var midpoint2 = levels[10];
            midpoint2List.Add(midpoint2);

            var midpoint3 = levels[11];
            midpoint3List.Add(midpoint3);

            var midpoint4 = levels[12];
            midpoint4List.Add(midpoint4);

            var signal = GetCompareSignal(currentClose - pivot, prevClose - prevPivot);
            signalsList?.Add(signal);
        }


        // BAR ALIGNMENT. Everything above is computed per PERIOD; the StockData this is stored on is
        // per BAR, and callers index the two in parallel. Project each series onto the bars of its own
        // period before storing. Causal: a period's level derives from the PRECEDING period, so it is
        // already known when its own period opens.
        var barGroupIndexes = GetInputLengthGroupIndexes(stockData, inputLength);
        pivotList = ExpandPeriodValuesToBars(pivotList, barGroupIndexes);
        resistanceLevel1List = ExpandPeriodValuesToBars(resistanceLevel1List, barGroupIndexes);
        resistanceLevel2List = ExpandPeriodValuesToBars(resistanceLevel2List, barGroupIndexes);
        resistanceLevel3List = ExpandPeriodValuesToBars(resistanceLevel3List, barGroupIndexes);
        resistanceLevel4List = ExpandPeriodValuesToBars(resistanceLevel4List, barGroupIndexes);
        supportLevel1List = ExpandPeriodValuesToBars(supportLevel1List, barGroupIndexes);
        supportLevel2List = ExpandPeriodValuesToBars(supportLevel2List, barGroupIndexes);
        supportLevel3List = ExpandPeriodValuesToBars(supportLevel3List, barGroupIndexes);
        supportLevel4List = ExpandPeriodValuesToBars(supportLevel4List, barGroupIndexes);
        midpoint1List = ExpandPeriodValuesToBars(midpoint1List, barGroupIndexes);
        midpoint2List = ExpandPeriodValuesToBars(midpoint2List, barGroupIndexes);
        midpoint3List = ExpandPeriodValuesToBars(midpoint3List, barGroupIndexes);
        midpoint4List = ExpandPeriodValuesToBars(midpoint4List, barGroupIndexes);

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Pivot", pivotList },
            { "S1", supportLevel1List },
            { "S2", supportLevel2List },
            { "S3", supportLevel3List },
            { "S4", supportLevel4List },
            { "R1", resistanceLevel1List },
            { "R2", resistanceLevel2List },
            { "R3", resistanceLevel3List },
            { "R4", resistanceLevel4List },
            { "M1", midpoint1List },
            { "M2", midpoint2List },
            { "M3", midpoint3List },
            { "M4", midpoint4List }
        });
        // Signals are produced per PERIOD in the loop above, exactly like the levels, so they need
        // the same projection. Leaving them period-length puts every signal on the wrong bar and
        // reintroduces on SignalsList the misalignment this method just removed from its levels.
        if (signalsList is not null)
        {
            var barAlignedSignals = ExpandPeriodItemsToBars(signalsList, barGroupIndexes, Signal.None);
            signalsList.Clear();
            signalsList.AddRange(barAlignedSignals);
        }

        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(pivotList);
        stockData.IndicatorName = IndicatorName.WoodiePivotPoints;

        return stockData;
    }

}

