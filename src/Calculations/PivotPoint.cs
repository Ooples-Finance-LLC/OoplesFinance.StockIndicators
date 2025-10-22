//     Ooples Finance Stock Indicator Library
//     https://ooples.github.io/OoplesFinance.StockIndicators/
//
//     Copyright © Franklin Moormann, 2020-2022
//     cheatcountry@gmail.com
//
//     This library is free software and it uses the Apache 2.0 license
//     so if you are going to re-use or modify my code then I just ask
//     that you include my copyright info and my contact info in a comment

namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Standard Pivot Points
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="inputLength"></param>
    /// <returns></returns>
    public static StockData CalculateStandardPivotPoints(this StockData stockData, InputLength inputLength = InputLength.Day)
    {
        List<double> pivotList = new();
        List<double> resistanceLevel3List = new();
        List<double> resistanceLevel2List = new();
        List<double> resistanceLevel1List = new();
        List<double> supportLevel1List = new();
        List<double> supportLevel2List = new();
        List<double> supportLevel3List = new();
        List<double> midpoint1List = new();
        List<double> midpoint2List = new();
        List<double> midpoint3List = new();
        List<double> midpoint4List = new();
        List<double> midpoint5List = new();
        List<double> midpoint6List = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, openList, _) = GetInputValuesList(stockData, inputLength);

        for (var i = 0; i < inputList.Count; i++)
        {
            var currentClose = inputList[i];
            var prevClose = i >= 1 ? inputList[i - 1] : 0;
            var prevLow = i >= 1 ? lowList[i - 1] : 0;
            var prevHigh = i >= 1 ? highList[i - 1] : 0;
            var prevOpen = i >= 1 ? openList[i - 1] : 0;

            var prevPivot = pivotList.LastOrDefault();
            var range = prevHigh - prevLow;
            var pivot = (prevHigh + prevLow + prevClose + prevOpen) / 4;
            pivotList.AddRounded(pivot);

            var supportLevel1 = (pivot * 2) - prevHigh;
            supportLevel1List.AddRounded(supportLevel1);

            var resistanceLevel1 = (pivot * 2) - prevLow;
            resistanceLevel1List.AddRounded(resistanceLevel1);

            var range2 = resistanceLevel1 - supportLevel1;
            var supportLevel2 = pivot - range;
            supportLevel2List.AddRounded(supportLevel2);

            var resistanceLevel2 = pivot + range;
            resistanceLevel2List.AddRounded(resistanceLevel2);

            var supportLevel3 = pivot - range2;
            supportLevel3List.AddRounded(supportLevel3);

            var resistanceLevel3 = pivot + range2;
            resistanceLevel3List.AddRounded(resistanceLevel3);

            var midpoint1 = (supportLevel3 + supportLevel2) / 2;
            midpoint1List.AddRounded(midpoint1);

            var midpoint2 = (supportLevel2 + supportLevel1) / 2;
            midpoint2List.AddRounded(midpoint2);

            var midpoint3 = (supportLevel1 + pivot) / 2;
            midpoint3List.AddRounded(midpoint3);

            var midpoint4 = (resistanceLevel1 + pivot) / 2;
            midpoint4List.AddRounded(midpoint4);

            var midpoint5 = (resistanceLevel2 + resistanceLevel1) / 2;
            midpoint5List.AddRounded(midpoint5);

            var midpoint6 = (resistanceLevel3 + resistanceLevel2) / 2;
            midpoint6List.AddRounded(midpoint6);

            var signal = GetCompareSignal(currentClose - pivot, prevClose - prevPivot);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
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
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = pivotList;
        stockData.IndicatorName = IndicatorName.StandardPivotPoints;

        return stockData;
    }

    /// <summary>
    /// Calculates the Woodie Pivot Points
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="inputLength"></param>
    /// <returns></returns>
    public static StockData CalculateWoodiePivotPoints(this StockData stockData, InputLength inputLength = InputLength.Day)
    {
        List<double> pivotList = new();
        List<double> resistanceLevel1List = new();
        List<double> resistanceLevel2List = new();
        List<double> resistanceLevel3List = new();
        List<double> resistanceLevel4List = new();
        List<double> supportLevel1List = new();
        List<double> supportLevel2List = new();
        List<double> supportLevel3List = new();
        List<double> supportLevel4List = new();
        List<double> midpoint1List = new();
        List<double> midpoint2List = new();
        List<double> midpoint3List = new();
        List<double> midpoint4List = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData, inputLength);

        for (var i = 0; i < inputList.Count; i++)
        {
            var currentClose = inputList[i];
            var prevHigh = i >= 1 ? highList[i - 1] : 0;
            var prevLow = i >= 1 ? lowList[i - 1] : 0;
            var prevClose = i >= 1 ? inputList[i - 1] : 0;

            var prevPivot = pivotList.LastOrDefault();
            var range = prevHigh - prevLow;
            var pivot = (prevHigh + prevLow + (prevClose * 2)) / 4;
            pivotList.AddRounded(pivot);

            var supportLevel1 = (pivot * 2) - prevHigh;
            supportLevel1List.AddRounded(supportLevel1);

            var resistanceLevel1 = (pivot * 2) - prevLow;
            resistanceLevel1List.AddRounded(resistanceLevel1);

            var supportLevel2 = pivot - range;
            supportLevel2List.AddRounded(supportLevel2);

            var resistanceLevel2 = pivot + range;
            resistanceLevel2List.AddRounded(resistanceLevel2);

            var supportLevel3 = prevLow - (2 * (prevHigh - pivot));
            supportLevel3List.AddRounded(supportLevel3);

            var resistanceLevel3 = prevHigh + (2 * (pivot - prevLow));
            resistanceLevel3List.AddRounded(resistanceLevel3);

            var supportLevel4 = supportLevel3 - range;
            supportLevel4List.AddRounded(supportLevel4);

            var resistanceLevel4 = resistanceLevel3 + range;
            resistanceLevel4List.AddRounded(resistanceLevel4);

            var midpoint1 = (supportLevel1 + supportLevel2) / 2;
            midpoint1List.AddRounded(midpoint1);

            var midpoint2 = (pivot + supportLevel1) / 2;
            midpoint2List.AddRounded(midpoint2);

            var midpoint3 = (resistanceLevel1 + pivot) / 2;
            midpoint3List.AddRounded(midpoint3);

            var midpoint4 = (resistanceLevel1 + resistanceLevel2) / 2;
            midpoint4List.AddRounded(midpoint4);

            var signal = GetCompareSignal(currentClose - pivot, prevClose - prevPivot);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
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
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = pivotList;
        stockData.IndicatorName = IndicatorName.WoodiePivotPoints;

        return stockData;
    }

    /// <summary>
    /// Calculates the Floor Pivot Points
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="inputLength"></param>
    /// <returns></returns>
    public static StockData CalculateFloorPivotPoints(this StockData stockData, InputLength inputLength = InputLength.Day)
    {
        List<double> pivotList = new();
        List<double> resistanceLevel3List = new();
        List<double> resistanceLevel2List = new();
        List<double> resistanceLevel1List = new();
        List<double> supportLevel1List = new();
        List<double> supportLevel2List = new();
        List<double> supportLevel3List = new();
        List<double> midpoint1List = new();
        List<double> midpoint2List = new();
        List<double> midpoint3List = new();
        List<double> midpoint4List = new();
        List<double> midpoint5List = new();
        List<double> midpoint6List = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData, inputLength);

        for (var i = 0; i < inputList.Count; i++)
        {
            var currentClose = inputList[i];
            var prevHigh = i >= 1 ? highList[i] : 0;
            var prevLow = i >= 1 ? lowList[i] : 0;
            var prevClose = i >= 1 ? inputList[i] : 0;

            var range = prevHigh - prevLow;
            var pivot = (prevHigh + prevLow + prevClose) / 3;
            pivotList.AddRounded(pivot);

            var prevSupportLevel1 = supportLevel1List.LastOrDefault();
            var supportLevel1 = (pivot * 2) - prevHigh;
            supportLevel1List.AddRounded(supportLevel1);

            var prevResistanceLevel1 = resistanceLevel1List.LastOrDefault();
            var resistanceLevel1 = (pivot * 2) - prevLow;
            resistanceLevel1List.AddRounded(resistanceLevel1);

            var supportLevel2 = pivot - range;
            supportLevel2List.AddRounded(supportLevel2);

            var resistanceLevel2 = pivot + range;
            resistanceLevel2List.AddRounded(resistanceLevel2);

            var supportLevel3 = supportLevel1 - range;
            supportLevel3List.AddRounded(supportLevel3);

            var resistanceLevel3 = resistanceLevel1 + range;
            resistanceLevel3List.AddRounded(resistanceLevel3);

            var midpoint1 = (supportLevel3 + supportLevel2) / 2;
            midpoint1List.AddRounded(midpoint1);

            var midpoint2 = (supportLevel2 + supportLevel1) / 2;
            midpoint2List.AddRounded(midpoint2);

            var midpoint3 = (supportLevel1 + pivot) / 2;
            midpoint3List.AddRounded(midpoint3);

            var midpoint4 = (resistanceLevel1 + pivot) / 2;
            midpoint4List.AddRounded(midpoint4);

            var midpoint5 = (resistanceLevel2 + resistanceLevel1) / 2;
            midpoint5List.AddRounded(midpoint5);

            var midpoint6 = (resistanceLevel3 + resistanceLevel2) / 2;
            midpoint6List.AddRounded(midpoint6);

            var signal = GetBullishBearishSignal(currentClose - resistanceLevel1, prevClose - prevResistanceLevel1,
                currentClose - supportLevel1, prevClose - prevSupportLevel1);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
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
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = pivotList;
        stockData.IndicatorName = IndicatorName.FloorPivotPoints;

        return stockData;
    }

    /// <summary>
    /// Calculates the Fibonacci Pivot Points
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="inputLength"></param>
    /// <returns></returns>
    public static StockData CalculateFibonacciPivotPoints(this StockData stockData, InputLength inputLength = InputLength.Day)
    {
        List<double> pivotList = new();
        List<double> resistanceLevel3List = new();
        List<double> resistanceLevel2List = new();
        List<double> resistanceLevel1List = new();
        List<double> supportLevel1List = new();
        List<double> supportLevel2List = new();
        List<double> supportLevel3List = new();
        List<double> midpoint1List = new();
        List<double> midpoint2List = new();
        List<double> midpoint3List = new();
        List<double> midpoint4List = new();
        List<double> midpoint5List = new();
        List<double> midpoint6List = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData, inputLength);

        for (var i = 0; i < inputList.Count; i++)
        {
            var currentClose = inputList[i];
            var prevClose = i >= 1 ? inputList[i - 1] : 0;
            var prevLow = i >= 1 ? lowList[i - 1] : 0;
            var prevHigh = i >= 1 ? highList[i - 1] : 0;

            var prevPivot = pivotList.LastOrDefault();
            var range = prevHigh - prevLow;
            var pivot = (prevHigh + prevLow + prevClose) / 3;
            pivotList.AddRounded(pivot);

            var supportLevel1 = pivot - (range * 0.382);
            supportLevel1List.AddRounded(supportLevel1);

            var supportLevel2 = pivot - (range * 0.618);
            supportLevel2List.AddRounded(supportLevel2);

            var supportLevel3 = pivot - (range * 1);
            supportLevel3List.AddRounded(supportLevel3);

            var resistanceLevel1 = pivot + (range * 0.382);
            resistanceLevel1List.AddRounded(resistanceLevel1);

            var resistanceLevel2 = pivot + (range * 0.618);
            resistanceLevel2List.AddRounded(resistanceLevel2);

            var resistanceLevel3 = pivot + (range * 1);
            resistanceLevel3List.AddRounded(resistanceLevel3);

            var midpoint1 = (supportLevel3 + supportLevel2) / 2;
            midpoint1List.AddRounded(midpoint1);

            var midpoint2 = (supportLevel2 + supportLevel1) / 2;
            midpoint2List.AddRounded(midpoint2);

            var midpoint3 = (supportLevel1 + pivot) / 2;
            midpoint3List.AddRounded(midpoint3);

            var midpoint4 = (resistanceLevel1 + pivot) / 2;
            midpoint4List.AddRounded(midpoint4);

            var midpoint5 = (resistanceLevel2 + resistanceLevel1) / 2;
            midpoint5List.AddRounded(midpoint5);

            var midpoint6 = (resistanceLevel3 + resistanceLevel2) / 2;
            midpoint6List.AddRounded(midpoint6);

            var signal = GetCompareSignal(currentClose - pivot, prevClose - prevPivot);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
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
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = pivotList;
        stockData.IndicatorName = IndicatorName.FibonacciPivotPoints;

        return stockData;
    }

    /// <summary>
    /// Calculates the Camarilla Pivot Points
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="inputLength"></param>
    /// <returns></returns>
    public static StockData CalculateCamarillaPivotPoints(this StockData stockData, InputLength inputLength = InputLength.Day)
    {
        List<double> resistanceLevel5List = new();
        List<double> resistanceLevel4List = new();
        List<double> resistanceLevel3List = new();
        List<double> resistanceLevel2List = new();
        List<double> resistanceLevel1List = new();
        List<double> supportLevel1List = new();
        List<double> supportLevel2List = new();
        List<double> supportLevel3List = new();
        List<double> supportLevel4List = new();
        List<double> supportLevel5List = new();
        List<double> midpoint1List = new();
        List<double> midpoint2List = new();
        List<double> midpoint3List = new();
        List<double> midpoint4List = new();
        List<double> midpoint5List = new();
        List<double> midpoint6List = new();
        List<double> pivotList = new();
        List<Signal> signalsList = new();
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
            pivotList.AddRounded(pivot);

            var prevSupportLevel1 = supportLevel1List.LastOrDefault();
            var supportLevel1 = currentClose - (0.0916 * range);
            supportLevel1List.AddRounded(supportLevel1);

            var supportLevel2 = currentClose - (0.183 * range);
            supportLevel2List.AddRounded(supportLevel2);

            var supportLevel3 = currentClose - (0.275 * range);
            supportLevel3List.AddRounded(supportLevel3);

            var supportLevel4 = currentClose - (0.55 * range);
            supportLevel4List.AddRounded(supportLevel4);

            var prevResistanceLevel1 = resistanceLevel1List.LastOrDefault();
            var resistanceLevel1 = currentClose + (0.0916 * range);
            resistanceLevel1List.AddRounded(resistanceLevel1);

            var resistanceLevel2 = currentClose + (0.183 * range);
            resistanceLevel2List.AddRounded(resistanceLevel2);

            var resistanceLevel3 = currentClose + (0.275 * range);
            resistanceLevel3List.AddRounded(resistanceLevel3);

            var resistanceLevel4 = currentClose + (0.55 * range);
            resistanceLevel4List.AddRounded(resistanceLevel4);

            var resistanceLevel5 = currentLow != 0 ? currentHigh / currentLow * currentClose : 0;
            resistanceLevel5List.AddRounded(resistanceLevel5);

            var supportLevel5 = currentClose - (resistanceLevel5 - currentClose);
            supportLevel5List.AddRounded(supportLevel5);

            var midpoint1 = (supportLevel3 + supportLevel2) / 2;
            midpoint1List.AddRounded(midpoint1);

            var midpoint2 = (supportLevel2 + supportLevel1) / 2;
            midpoint2List.AddRounded(midpoint2);

            var midpoint3 = (resistanceLevel2 + resistanceLevel1) / 2;
            midpoint3List.AddRounded(midpoint3);

            var midpoint4 = (resistanceLevel3 + resistanceLevel2) / 2;
            midpoint4List.AddRounded(midpoint4);

            var midpoint5 = (resistanceLevel3 + resistanceLevel4) / 2;
            midpoint5List.AddRounded(midpoint5);

            var midpoint6 = (supportLevel4 + supportLevel3) / 2;
            midpoint6List.AddRounded(midpoint6);

            var signal = GetBullishBearishSignal(currentClose - resistanceLevel1, prevClose - prevResistanceLevel1, currentClose - supportLevel1, 
                prevClose - prevSupportLevel1);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
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
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = pivotList;
        stockData.IndicatorName = IndicatorName.CamarillaPivotPoints;

        return stockData;
    }

    /// <summary>
    /// Calculates the Pivot Point Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="inputLength"></param>
    /// <returns></returns>
    public static StockData CalculatePivotPointAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 3, InputLength inputLength = InputLength.Day)
    {
        List<double> pp1List = new();
        List<double> pp2List = new();
        List<double> pp3List = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, openList, _) = GetInputValuesList(stockData, inputLength);

        for (var i = 0; i < inputList.Count; i++)
        {
            var currentOpen = openList[i];
            var prevHigh = i >= 1 ? highList[i - 1] : 0;
            var prevLow = i >= 1 ? lowList[i - 1] : 0;
            var prevClose = i >= 1 ? inputList[i - 1] : 0;

            var pp1 = (prevHigh + prevLow + prevClose) / 3;
            pp1List.AddRounded(pp1);

            var pp2 = (prevHigh + prevLow + prevClose + currentOpen) / 4;
            pp2List.AddRounded(pp2);

            var pp3 = (prevHigh + prevLow + currentOpen) / 3;
            pp3List.AddRounded(pp3);
        }

        var ppav1List = GetMovingAverageList(stockData, maType, length, pp1List);
        var ppav2List = GetMovingAverageList(stockData, maType, length, pp2List);
        var ppav3List = GetMovingAverageList(stockData, maType, length, pp3List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var pp1 = pp1List[i];
            var ppav1 = ppav1List[i];
            var prevPp1 = i >= 1 ? pp1List[i - 1] : 0;
            var prevPpav1 = i >= 1 ? ppav1List[i - 1] : 0;

            var signal = GetCompareSignal(pp1 - ppav1, prevPp1 - prevPpav1);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Pivot1", pp1List },
            { "Signal1", ppav1List },
            { "Pivot2", pp2List },
            { "Signal2", ppav2List },
            { "Pivot3", pp3List },
            { "Signal3", ppav3List }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = pp1List;
        stockData.IndicatorName = IndicatorName.PivotPointAverage;

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
        List<double> pivotList = new();
        List<double> resistanceLevel1List = new();
        List<double> supportLevel1List = new();
        List<Signal> signalsList = new();
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

            var prevPivot = pivotList.LastOrDefault();
            var pivot = x / 4;
            pivotList.AddRounded(pivot);

            var ratio = x / 2;
            var supportLevel1 = ratio - prevHigh;
            supportLevel1List.AddRounded(supportLevel1);

            var resistanceLevel1 = ratio - prevLow;
            resistanceLevel1List.AddRounded(resistanceLevel1);

            var signal = GetCompareSignal(currentClose - pivot, prevClose - prevPivot);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Pivot", pivotList },
            { "S1", supportLevel1List },
            { "R1", resistanceLevel1List }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = pivotList;
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
        List<double> resistanceLevel1List = new();
        List<double> supportLevel1List = new();
        List<double> pivotList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData, inputLength);

        for (var i = 0; i < inputList.Count; i++)
        {
            var currentClose = inputList[i];
            var prevHigh = i >= 1 ? highList[i - 1] : 0;
            var prevLow = i >= 1 ? lowList[i - 1] : 0;
            var prevClose = i >= 1 ? inputList[i - 1] : 0;

            var pivot = (prevHigh + prevLow + prevClose) / 3;
            pivotList.AddRounded(pivot);

            var prevSupportLevel1 = supportLevel1List.LastOrDefault();
            var supportLevel1 = pivot - (prevHigh - pivot);
            supportLevel1List.AddRounded(supportLevel1);

            var prevResistanceLevel1 = resistanceLevel1List.LastOrDefault();
            var resistanceLevel1 = pivot + (pivot - prevLow);
            resistanceLevel1List.AddRounded(resistanceLevel1);

            var signal = GetBullishBearishSignal(currentClose - resistanceLevel1, prevClose - prevResistanceLevel1, 
                currentClose - supportLevel1, prevClose - prevSupportLevel1);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Pivot", pivotList },
            { "S1", supportLevel1List },
            { "R1", resistanceLevel1List }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = pivotList;
        stockData.IndicatorName = IndicatorName.DynamicPivotPoints;

        return stockData;
    }
}