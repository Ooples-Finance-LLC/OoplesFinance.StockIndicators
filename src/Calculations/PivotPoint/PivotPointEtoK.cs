
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Floor Pivot Points
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="inputLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateFloorPivotPoints(this StockData stockData, InputLength inputLength = InputLength.Day)
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
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData, inputLength);

        for (var i = 0; i < inputList.Count; i++)
        {
            var currentClose = inputList[i];
            var prevHigh = i >= 1 ? highList[i] : 0;
            var prevLow = i >= 1 ? lowList[i] : 0;
            var prevClose = i >= 1 ? inputList[i] : 0;

            var range = prevHigh - prevLow;
            var pivot = (prevHigh + prevLow + prevClose) / 3;
            pivotList.Add(pivot);

            var prevSupportLevel1 = GetLastOrDefault(supportLevel1List);
            var supportLevel1 = (pivot * 2) - prevHigh;
            supportLevel1List.Add(supportLevel1);

            var prevResistanceLevel1 = GetLastOrDefault(resistanceLevel1List);
            var resistanceLevel1 = (pivot * 2) - prevLow;
            resistanceLevel1List.Add(resistanceLevel1);

            var supportLevel2 = pivot - range;
            supportLevel2List.Add(supportLevel2);

            var resistanceLevel2 = pivot + range;
            resistanceLevel2List.Add(resistanceLevel2);

            var supportLevel3 = supportLevel1 - range;
            supportLevel3List.Add(supportLevel3);

            var resistanceLevel3 = resistanceLevel1 + range;
            resistanceLevel3List.Add(resistanceLevel3);

            var midpoint1 = (supportLevel3 + supportLevel2) / 2;
            midpoint1List.Add(midpoint1);

            var midpoint2 = (supportLevel2 + supportLevel1) / 2;
            midpoint2List.Add(midpoint2);

            var midpoint3 = (supportLevel1 + pivot) / 2;
            midpoint3List.Add(midpoint3);

            var midpoint4 = (resistanceLevel1 + pivot) / 2;
            midpoint4List.Add(midpoint4);

            var midpoint5 = (resistanceLevel2 + resistanceLevel1) / 2;
            midpoint5List.Add(midpoint5);

            var midpoint6 = (resistanceLevel3 + resistanceLevel2) / 2;
            midpoint6List.Add(midpoint6);

            var signal = GetBullishBearishSignal(currentClose - resistanceLevel1, prevClose - prevResistanceLevel1,
                currentClose - supportLevel1, prevClose - prevSupportLevel1);
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
        stockData.IndicatorName = IndicatorName.FloorPivotPoints;

        return stockData;
    }


    /// <summary>
    /// Calculates the Fibonacci Pivot Points
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="inputLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateFibonacciPivotPoints(this StockData stockData, InputLength inputLength = InputLength.Day)
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
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData, inputLength);

        for (var i = 0; i < inputList.Count; i++)
        {
            var currentClose = inputList[i];
            var prevClose = i >= 1 ? inputList[i - 1] : 0;
            var prevLow = i >= 1 ? lowList[i - 1] : 0;
            var prevHigh = i >= 1 ? highList[i - 1] : 0;

            var prevPivot = GetLastOrDefault(pivotList);
            var range = prevHigh - prevLow;
            var pivot = (prevHigh + prevLow + prevClose) / 3;
            pivotList.Add(pivot);

            var supportLevel1 = pivot - (range * 0.382);
            supportLevel1List.Add(supportLevel1);

            var supportLevel2 = pivot - (range * MathHelper.InversePhi);
            supportLevel2List.Add(supportLevel2);

            var supportLevel3 = pivot - (range * 1);
            supportLevel3List.Add(supportLevel3);

            var resistanceLevel1 = pivot + (range * 0.382);
            resistanceLevel1List.Add(resistanceLevel1);

            var resistanceLevel2 = pivot + (range * MathHelper.InversePhi);
            resistanceLevel2List.Add(resistanceLevel2);

            var resistanceLevel3 = pivot + (range * 1);
            resistanceLevel3List.Add(resistanceLevel3);

            var midpoint1 = (supportLevel3 + supportLevel2) / 2;
            midpoint1List.Add(midpoint1);

            var midpoint2 = (supportLevel2 + supportLevel1) / 2;
            midpoint2List.Add(midpoint2);

            var midpoint3 = (supportLevel1 + pivot) / 2;
            midpoint3List.Add(midpoint3);

            var midpoint4 = (resistanceLevel1 + pivot) / 2;
            midpoint4List.Add(midpoint4);

            var midpoint5 = (resistanceLevel2 + resistanceLevel1) / 2;
            midpoint5List.Add(midpoint5);

            var midpoint6 = (resistanceLevel3 + resistanceLevel2) / 2;
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
        stockData.IndicatorName = IndicatorName.FibonacciPivotPoints;

        return stockData;
    }

}

