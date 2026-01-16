
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Camarilla Pivot Points
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="inputLength"></param>
    /// <returns></returns>
    public static StockData CalculateCamarillaPivotPoints(this StockData stockData, InputLength inputLength = InputLength.Day)
    {
        List<double> resistanceLevel5List = new(stockData.Count);
        List<double> resistanceLevel4List = new(stockData.Count);
        List<double> resistanceLevel3List = new(stockData.Count);
        List<double> resistanceLevel2List = new(stockData.Count);
        List<double> resistanceLevel1List = new(stockData.Count);
        List<double> supportLevel1List = new(stockData.Count);
        List<double> supportLevel2List = new(stockData.Count);
        List<double> supportLevel3List = new(stockData.Count);
        List<double> supportLevel4List = new(stockData.Count);
        List<double> supportLevel5List = new(stockData.Count);
        List<double> midpoint1List = new(stockData.Count);
        List<double> midpoint2List = new(stockData.Count);
        List<double> midpoint3List = new(stockData.Count);
        List<double> midpoint4List = new(stockData.Count);
        List<double> midpoint5List = new(stockData.Count);
        List<double> midpoint6List = new(stockData.Count);
        List<double> pivotList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData, inputLength);

        for (var i = 0; i < inputList.Count; i++)
        {
            var prevClose = i >= 1 ? inputList[i - 1] : 0;
            var currentClose = i >= 1 ? prevClose : inputList[i];
            var prevHigh = i >= 1 ? highList[i - 1] : 0;
            var currentHigh = i >= 1 ? prevHigh : highList[i];
            var prevLow = i >= 1 ? lowList[i - 1] : 0;
            var currentLow = i >= 1 ? prevLow : lowList[i];
            var range = currentHigh - currentLow;

            var pivot = (prevHigh + prevLow + prevClose) / 3;
            pivotList.Add(pivot);

            var prevSupportLevel1 = GetLastOrDefault(supportLevel1List);
            var supportLevel1 = currentClose - (0.0916 * range);
            supportLevel1List.Add(supportLevel1);

            var supportLevel2 = currentClose - (0.183 * range);
            supportLevel2List.Add(supportLevel2);

            var supportLevel3 = currentClose - (0.275 * range);
            supportLevel3List.Add(supportLevel3);

            var supportLevel4 = currentClose - (0.55 * range);
            supportLevel4List.Add(supportLevel4);

            var prevResistanceLevel1 = GetLastOrDefault(resistanceLevel1List);
            var resistanceLevel1 = currentClose + (0.0916 * range);
            resistanceLevel1List.Add(resistanceLevel1);

            var resistanceLevel2 = currentClose + (0.183 * range);
            resistanceLevel2List.Add(resistanceLevel2);

            var resistanceLevel3 = currentClose + (0.275 * range);
            resistanceLevel3List.Add(resistanceLevel3);

            var resistanceLevel4 = currentClose + (0.55 * range);
            resistanceLevel4List.Add(resistanceLevel4);

            var resistanceLevel5 = currentLow != 0 ? currentHigh / currentLow * currentClose : 0;
            resistanceLevel5List.Add(resistanceLevel5);

            var supportLevel5 = currentClose - (resistanceLevel5 - currentClose);
            supportLevel5List.Add(supportLevel5);

            var midpoint1 = (supportLevel3 + supportLevel2) / 2;
            midpoint1List.Add(midpoint1);

            var midpoint2 = (supportLevel2 + supportLevel1) / 2;
            midpoint2List.Add(midpoint2);

            var midpoint3 = (resistanceLevel2 + resistanceLevel1) / 2;
            midpoint3List.Add(midpoint3);

            var midpoint4 = (resistanceLevel3 + resistanceLevel2) / 2;
            midpoint4List.Add(midpoint4);

            var midpoint5 = (resistanceLevel3 + resistanceLevel4) / 2;
            midpoint5List.Add(midpoint5);

            var midpoint6 = (supportLevel4 + supportLevel3) / 2;
            midpoint6List.Add(midpoint6);

            var signal = GetBullishBearishSignal(currentClose - resistanceLevel1, prevClose - prevResistanceLevel1, currentClose - supportLevel1, 
                prevClose - prevSupportLevel1);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Pivot", pivotList },
            { "S1", supportLevel1List },
            { "S2", supportLevel2List },
            { "S3", supportLevel3List },
            { "S4", supportLevel4List },
            { "S5", supportLevel5List },
            { "R1", resistanceLevel1List },
            { "R2", resistanceLevel2List },
            { "R3", resistanceLevel3List },
            { "R4", resistanceLevel4List },
            { "R5", resistanceLevel5List },
            { "M1", midpoint1List },
            { "M2", midpoint2List },
            { "M3", midpoint3List },
            { "M4", midpoint4List },
            { "M5", midpoint5List },
            { "M6", midpoint6List }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(pivotList);
        stockData.IndicatorName = IndicatorName.CamarillaPivotPoints;

        return stockData;
    }


    /// <summary>
    /// Calculates the Demark Pivot Points
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="inputLength"></param>
    /// <returns></returns>
    public static StockData CalculateDemarkPivotPoints(this StockData stockData, InputLength inputLength = InputLength.Day)
    {
        List<double> pivotList = new(stockData.Count);
        List<double> resistanceLevel1List = new(stockData.Count);
        List<double> supportLevel1List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, openList, _) = GetInputValuesList(stockData, inputLength);

        for (var i = 0; i < inputList.Count; i++)
        {
            var currentClose = inputList[i];
            var prevClose = i >= 1 ? inputList[i - 1] : 0;
            var prevOpen = i >= 1 ? openList[i - 1] : 0;
            var prevLow = i >= 1 ? lowList[i - 1] : 0;
            var prevHigh = i >= 1 ? highList[i - 1] : 0;
            var x = prevClose < prevOpen ? prevHigh + (2 * prevLow) + prevClose : prevClose > prevOpen ? (2 * prevHigh) + prevLow + prevClose :
                prevClose == prevOpen ? prevHigh + prevLow + (2 * prevClose) : prevClose;

            var prevPivot = GetLastOrDefault(pivotList);
            var pivot = x / 4;
            pivotList.Add(pivot);

            var ratio = x / 2;
            var supportLevel1 = ratio - prevHigh;
            supportLevel1List.Add(supportLevel1);

            var resistanceLevel1 = ratio - prevLow;
            resistanceLevel1List.Add(resistanceLevel1);

            var signal = GetCompareSignal(currentClose - pivot, prevClose - prevPivot);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Pivot", pivotList },
            { "S1", supportLevel1List },
            { "R1", resistanceLevel1List }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(pivotList);
        stockData.IndicatorName = IndicatorName.DemarkPivotPoints;

        return stockData;
    }


    /// <summary>
    /// Calculates the Dynamic Pivot Points
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="inputLength"></param>
    /// <returns></returns>
    public static StockData CalculateDynamicPivotPoints(this StockData stockData, InputLength inputLength = InputLength.Day)
    {
        List<double> resistanceLevel1List = new(stockData.Count);
        List<double> supportLevel1List = new(stockData.Count);
        List<double> pivotList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData, inputLength);

        for (var i = 0; i < inputList.Count; i++)
        {
            var currentClose = inputList[i];
            var prevHigh = i >= 1 ? highList[i - 1] : 0;
            var prevLow = i >= 1 ? lowList[i - 1] : 0;
            var prevClose = i >= 1 ? inputList[i - 1] : 0;

            var pivot = (prevHigh + prevLow + prevClose) / 3;
            pivotList.Add(pivot);

            var prevSupportLevel1 = GetLastOrDefault(supportLevel1List);
            var supportLevel1 = pivot - (prevHigh - pivot);
            supportLevel1List.Add(supportLevel1);

            var prevResistanceLevel1 = GetLastOrDefault(resistanceLevel1List);
            var resistanceLevel1 = pivot + (pivot - prevLow);
            resistanceLevel1List.Add(resistanceLevel1);

            var signal = GetBullishBearishSignal(currentClose - resistanceLevel1, prevClose - prevResistanceLevel1, 
                currentClose - supportLevel1, prevClose - prevSupportLevel1);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Pivot", pivotList },
            { "S1", supportLevel1List },
            { "R1", resistanceLevel1List }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(pivotList);
        stockData.IndicatorName = IndicatorName.DynamicPivotPoints;

        return stockData;
    }
}

