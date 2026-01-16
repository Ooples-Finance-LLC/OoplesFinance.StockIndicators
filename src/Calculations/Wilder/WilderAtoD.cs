using OoplesFinance.StockIndicators.Compatibility;
using OoplesFinance.StockIndicators.Core;

namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the average true range.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateAverageTrueRange(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod,
        int length = 14)
    {
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var count = inputList.Count;
        List<double> maList;
        List<double> atrList;
        List<double> atrMaList;
        var trList = GetTrueRangeList(stockData);

        if (maType == MovingAvgType.WildersSmoothingMethod)
        {
            var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
            var maBuffer = SpanCompat.CreateOutputBuffer(count);
            MovingAverageCore.WellesWilderMovingAverage(inputSpan, maBuffer.Span, length);
            maList = maBuffer.ToList();

            var trSpan = SpanCompat.AsReadOnlySpan(trList);
            var atrBuffer = SpanCompat.CreateOutputBuffer(count);
            MovingAverageCore.WellesWilderMovingAverage(trSpan, atrBuffer.Span, length);
            atrList = atrBuffer.ToList();

            var atrMaBuffer = SpanCompat.CreateOutputBuffer(count);
            MovingAverageCore.WellesWilderMovingAverage(atrBuffer.Span, atrMaBuffer.Span, length);
            atrMaList = atrMaBuffer.ToList();
        }
        else
        {
            maList = GetMovingAverageList(stockData, maType, length, inputList);

            atrList = GetMovingAverageList(stockData, maType, length, trList);
            atrMaList = GetMovingAverageList(stockData, maType, length, atrList);
        }

        List<Signal>? signalsList = CreateSignalsList(stockData, count);
        for (var i = 0; i < count; i++)
        {
            var currentValue = inputList[i];
            var atr = atrList[i];
            var currentMa = maList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevMa = i >= 1 ? maList[i - 1] : 0;
            var atrMa = atrMaList[i];

            var signal = GetVolatilitySignal(currentValue - currentMa, prevValue - prevMa, atr, atrMa);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Atr", atrList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(atrList);
        stockData.IndicatorName = IndicatorName.AverageTrueRange;

        return stockData;
    }


    /// <summary>
    /// Calculates the average index of the directional.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateAverageDirectionalIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int length = 14)
    {
        List<double> dmPlusList = new(stockData.Count);
        List<double> dmMinusList = new(stockData.Count);
        List<double> diPlusList = new(stockData.Count);
        List<double> diMinusList = new(stockData.Count);
        List<double> trList = new(stockData.Count);
        List<double> diList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var prevHigh = i >= 1 ? highList[i - 1] : 0;
            var prevLow = i >= 1 ? lowList[i - 1] : 0;
            var prevClose = i >= 1 ? inputList[i - 1] : 0;
            var highDiff = currentHigh - prevHigh;
            var lowDiff = prevLow - currentLow;

            var dmPlus = highDiff > lowDiff ? Math.Max(highDiff, 0) : 0;
            dmPlusList.Add(dmPlus);

            var dmMinus = highDiff < lowDiff ? Math.Max(lowDiff, 0) : 0;
            dmMinusList.Add(dmMinus);

            var tr = CalculateTrueRange(currentHigh, currentLow, prevClose);
            trList.Add(tr);
        }

        var dmPlus14List = GetMovingAverageList(stockData, maType, length, dmPlusList);
        var dmMinus14List = GetMovingAverageList(stockData, maType, length, dmMinusList);
        var tr14List = GetMovingAverageList(stockData, maType, length, trList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var dmPlus14 = dmPlus14List[i];
            var dmMinus14 = dmMinus14List[i];
            var trueRange14 = tr14List[i];

            var diPlus = trueRange14 != 0 ? MinOrMax(100 * dmPlus14 / trueRange14, 100, 0) : 0;
            diPlusList.Add(diPlus);

            var diMinus = trueRange14 != 0 ? MinOrMax(100 * dmMinus14 / trueRange14, 100, 0) : 0;
            diMinusList.Add(diMinus);

            var diDiff = Math.Abs(diPlus - diMinus);
            var diSum = diPlus + diMinus;

            var di = diSum != 0 ? MinOrMax(100 * diDiff / diSum, 100, 0) : 0;
            diList.Add(di);
        }

        var adxList = GetMovingAverageList(stockData, maType, length, diList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var diPlus = diPlusList[i];
            var diMinus = diMinusList[i];
            var prevDiPlus = i >= 1 ? diPlusList[i - 1] : 0;
            var prevDiMinus = i >= 1 ? diMinusList[i - 1] : 0;

            var signal = GetCompareSignal(diPlus - diMinus, prevDiPlus - prevDiMinus);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "DiPlus", diPlusList },
            { "DiMinus", diMinusList },
            { "Adx", adxList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(adxList);
        stockData.IndicatorName = IndicatorName.AverageDirectionalIndex;

        return stockData;
    }

}

