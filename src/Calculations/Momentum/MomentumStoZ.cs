
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Ultimate Momentum Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="inputName"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="length4"></param>
    /// <param name="length5"></param>
    /// <param name="length6"></param>
    /// <param name="stdDevMult"></param>
    /// <returns></returns>
    public static StockData CalculateUltimateMomentumIndicator(this StockData stockData, InputName inputName = InputName.TypicalPrice,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 13, int length2 = 19, int length3 = 21, int length4 = 39,
        int length5 = 50, int length6 = 200, double stdDevMult = 1.5)
    {
        List<double> utmList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var moVar = CalculateMcClellanOscillator(stockData, maType, fastLength: length2, slowLength: length4);
        var advSumList = moVar.OutputValues["AdvSum"];
        var decSumList = moVar.OutputValues["DecSum"];
        var moList = moVar.OutputValues["Mo"];
        var bbPctList = CalculateBollingerBandsPercentB(stockData, stdDevMult, maType, length5).CustomValuesList;
        var mfi1List = CalculateMoneyFlowIndex(stockData, inputName, length2).CustomValuesList;
        var mfi2List = CalculateMoneyFlowIndex(stockData, inputName, length3).CustomValuesList;
        var mfi3List = CalculateMoneyFlowIndex(stockData, inputName, length4).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var mo = moList[i];
            var bbPct = bbPctList[i];
            var mfi1 = mfi1List[i];
            var mfi2 = mfi2List[i];
            var mfi3 = mfi3List[i];
            var advSum = advSumList[i];
            var decSum = decSumList[i];
            var ratio = decSum != 0 ? advSum / decSum : 0;

            var utm = (200 * bbPct) + (100 * ratio) + (2 * mo) + (1.5 * mfi3) + (3 * mfi2) + (3 * mfi1);
            utmList.Add(utm);
        }

        stockData.SetCustomValues(utmList);
        var utmRsiList = CalculateRelativeStrengthIndex(stockData, maType, length1, length1).CustomValuesList;
        var utmiList = GetMovingAverageList(stockData, maType, length1, utmRsiList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var utmi = utmiList[i];
            var prevUtmi1 = i >= 1 ? utmiList[i - 1] : 0;
            var prevUtmi2 = i >= 2 ? utmiList[i - 2] : 0;

            var signal = GetCompareSignal(utmi - prevUtmi1, prevUtmi1 - prevUtmi2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Utm", utmiList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(utmiList);
        stockData.IndicatorName = IndicatorName.UltimateMomentumIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Tick Line Momentum Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateTickLineMomentumOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 10, int smoothLength = 5)
    {
        List<double> cumoList = new(stockData.Count);
        List<double> cumoSumList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        double cumoSum = 0;
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var maList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevMa = i >= 1 ? maList[i - 1] : 0;

            double cumo = currentValue > prevMa ? 1 : currentValue < prevMa ? -1 : 0;
            cumoList.Add(cumo);

            cumoSum += cumo;
            cumoSumList.Add(cumoSum);
        }

        stockData.SetCustomValues(cumoSumList);
        var rocList = CalculateRateOfChange(stockData, smoothLength).CustomValuesList;
        var tlmoList = GetMovingAverageList(stockData, maType, smoothLength, rocList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var tlmo = tlmoList[i];
            var prevTlmo1 = i >= 1 ? tlmoList[i - 1] : 0;
            var prevTlmo2 = i >= 2 ? tlmoList[i - 2] : 0;

            var signal = GetRsiSignal(tlmo - prevTlmo1, prevTlmo1 - prevTlmo2, tlmo, prevTlmo1, 5, -5);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Tlmo", tlmoList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(tlmoList);
        stockData.IndicatorName = IndicatorName.TickLineMomentumOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Squeeze Momentum Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateSqueezeMomentumIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 20)
    {
        List<double> diffList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var highest = highestList[i];
            var lowest = lowestList[i];
            var midprice = (highest + lowest) / 2;
            var sma = smaList[i];
            var midpriceSmaAvg = (midprice + sma) / 2;

            var diff = currentValue - midpriceSmaAvg;
            diffList.Add(diff);
        }

        stockData.SetCustomValues(diffList);
        var linregList = CalculateLinearRegression(stockData, length).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var predictedToday = linregList[i];
            var prevPredictedToday = i >= 1 ? linregList[i - 1] : 0;

            var signal = GetCompareSignal(predictedToday, prevPredictedToday);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Smi", linregList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(linregList);
        stockData.IndicatorName = IndicatorName.SqueezeMomentumIndicator;

        return stockData;
    }
}

