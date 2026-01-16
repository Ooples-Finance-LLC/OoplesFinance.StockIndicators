
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Stochastic Oscillator
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <param name="smoothLength1">Length of the K signal.</param>
    /// <param name="smoothLength2">Length of the D signal.</param>
    /// <returns></returns>
    public static StockData CalculateStochasticOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 14, int smoothLength1 = 3, int smoothLength2 = 3)
    {
        List<double> fastKList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var highestHigh = highestList[i];
            var lowestLow = lowestList[i];

            var fastK = highestHigh - lowestLow != 0 ? MinOrMax((currentValue - lowestLow) / (highestHigh - lowestLow) * 100, 100, 0) : 0;
            fastKList.Add(fastK);
        }

        var fastDList = GetMovingAverageList(stockData, maType, smoothLength1, fastKList);
        var slowDList = GetMovingAverageList(stockData, maType, smoothLength2, fastDList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var slowK = fastDList[i];
            var slowD = slowDList[i];
            var prevSlowk = i >= 1 ? fastDList[i - 1] : 0;
            var prevSlowd = i >= 1 ? slowDList[i - 1] : 0;

            var signal = GetRsiSignal(slowK - slowD, prevSlowk - prevSlowd, slowK, prevSlowk, 80, 20);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "FastK", fastKList },
            { "FastD", fastDList },
            { "SlowD", slowDList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(fastKList);
        stockData.IndicatorName = IndicatorName.StochasticOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Fast Turbo Stochastics
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="turboLength"></param>
    /// <returns></returns>
    public static StockData CalculateTurboStochasticsFast(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length1 = 20, int length2 = 10, int turboLength = 2)
    {
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var turbo = turboLength < 0 ? Math.Max(turboLength, length2 * -1) : turboLength > 0 ? Math.Min(turboLength, length2) : 0;

        var fastKList = CalculateStochasticOscillator(stockData, maType, length: length1).CustomValuesList;
        var fastDList = GetMovingAverageList(stockData, maType, length1, fastKList);
        stockData.SetCustomValues(fastKList);
        var tsfKList = CalculateLinearRegression(stockData, length2 + turbo).CustomValuesList;
        stockData.SetCustomValues(fastDList);
        var tsfDList = CalculateLinearRegression(stockData, length2 + turbo).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var tsfD = tsfDList[i];
            var tsfK = tsfKList[i];
            var prevTsfk = i >= 1 ? tsfKList[i - 1] : 0;
            var prevTsfd = i >= 1 ? tsfDList[i - 1] : 0;

            var signal = GetRsiSignal(tsfK - tsfD, prevTsfk - prevTsfd, tsfK, prevTsfk, 70, 30);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Tsf", tsfKList },
            { "Signal", tsfDList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(tsfKList);
        stockData.IndicatorName = IndicatorName.TurboStochasticsFast;

        return stockData;
    }


    /// <summary>
    /// Calculates the Slow Turbo Stochastics
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="turboLength"></param>
    /// <returns></returns>
    public static StockData CalculateTurboStochasticsSlow(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length1 = 20, int length2 = 10, int turboLength = 2)
    {
        List<double> tssDList = new(stockData.Count);
        List<double> tssKList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var turbo = turboLength < 0 ? Math.Max(turboLength, length2 * -1) : turboLength > 0 ? Math.Min(turboLength, length2) : 0;

        var fastKList = CalculateStochasticOscillator(stockData, maType, length: length1).CustomValuesList;
        var slowKList = GetMovingAverageList(stockData, maType, length1, fastKList);
        var slowDList = GetMovingAverageList(stockData, maType, length1, slowKList);
        stockData.SetCustomValues(slowKList);
        var tsfKList = CalculateLinearRegression(stockData, length2 + turbo).CustomValuesList;
        stockData.SetCustomValues(slowDList);
        var tsfDList = CalculateLinearRegression(stockData, length2 + turbo).CustomValuesList;

        for (var i = 0; i < tssDList.Count; i++)
        {
            var tssD = tssDList[i];
            var tssK = tssKList[i];
            var prevTssk = i >= 1 ? tssKList[i - 1] : 0;
            var prevTssd = i >= 1 ? tssDList[i - 1] : 0;

            var signal = GetRsiSignal(tssK - tssD, prevTssk - prevTssd, tssK, prevTssk, 70, 30);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Tsf", tsfKList },
            { "Signal", tsfDList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(tsfKList);
        stockData.IndicatorName = IndicatorName.TurboStochasticsSlow;

        return stockData;
    }


    /// <summary>
    /// Calculates the Stochastic Momentum Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="smoothLength1"></param>
    /// <param name="smoothLength2"></param>
    /// <returns></returns>
    public static StockData CalculateStochasticMomentumIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length1 = 2, int length2 = 8, int smoothLength1 = 5, int smoothLength2 = 5)
    {
        List<double> dList = new(stockData.Count);
        List<double> hlList = new(stockData.Count);
        List<double> smiList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var highestHigh = highestList[i];
            var lowestLow = lowestList[i];
            var median = (highestHigh + lowestLow) / 2;

            var diff = currentValue - median;
            dList.Add(diff);

            var highLow = highestHigh - lowestLow;
            hlList.Add(highLow);
        }

        var dEmaList = GetMovingAverageList(stockData, maType, length2, dList);
        var hlEmaList = GetMovingAverageList(stockData, maType, length2, hlList);
        var dSmoothEmaList = GetMovingAverageList(stockData, maType, smoothLength1, dEmaList);
        var hlSmoothEmaList = GetMovingAverageList(stockData, maType, smoothLength1, hlEmaList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var hlSmoothEma = hlSmoothEmaList[i];
            var dSmoothEma = dSmoothEmaList[i];
            var hl2 = hlSmoothEma / 2;

            var smi = hl2 != 0 ? MinOrMax(100 * dSmoothEma / hl2, 100, -100) : 0;
            smiList.Add(smi);
        }

        var smiSignalList = GetMovingAverageList(stockData, maType, smoothLength2, smiList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var smi = smiList[i];
            var smiSignal = smiSignalList[i];
            var prevSmi = i >= 1 ? smiList[i - 1] : 0;
            var prevSmiSignal = i >= 1 ? smiSignalList[i - 1] : 0;

            var signal = GetCompareSignal(smi - smiSignal, prevSmi - prevSmiSignal);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Smi", smiList },
            { "Signal", smiSignalList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(smiList);
        stockData.IndicatorName = IndicatorName.StochasticMomentumIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Stochastic Fast Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength1"></param>
    /// <param name="smoothLength2"></param>
    /// <returns></returns>
    public static StockData CalculateStochasticFastOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length = 14, int smoothLength1 = 3, int smoothLength2 = 2)
    {
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var fastKList = CalculateStochasticOscillator(stockData, maType, length, smoothLength1, smoothLength2);
        var pkList = fastKList.OutputValues["FastD"];
        var pdList = fastKList.OutputValues["SlowD"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var pkEma = pkList[i];
            var pdEma = pdList[i];
            var prevPkema = i >= 1 ? pkList[i - 1] : 0;
            var prevPdema = i >= 1 ? pdList[i - 1] : 0;

            var signal = GetRsiSignal(pkEma - pdEma, prevPkema - prevPdema, pkEma, prevPkema, 80, 20);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Sfo", pkList },
            { "Signal", pdList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(pkList);
        stockData.IndicatorName = IndicatorName.StochasticFastOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Stochastic Custom Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <returns></returns>
    public static StockData CalculateStochasticCustomOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 7,
        int length2 = 3, int length3 = 12)
    {
        List<double> numList = new(stockData.Count);
        List<double> denomList = new(stockData.Count);
        List<double> sckList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var highestHigh = highestList[i];
            var lowestLow = lowestList[i];

            var num = currentValue - lowestLow;
            numList.Add(num);

            var denom = highestHigh - lowestLow;
            denomList.Add(denom);
        }

        var numSmaList = GetMovingAverageList(stockData, maType, length2, numList);
        var denomSmaList = GetMovingAverageList(stockData, maType, length2, denomList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var numSma = numSmaList[i];
            var denomSma = denomSmaList[i];

            var sck = denomSma != 0 ? MinOrMax(numSma / denomSma * 100, 100, 0) : 0;
            sckList.Add(sck);
        }

        var scdList = GetMovingAverageList(stockData, maType, length3, sckList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var sck = sckList[i];
            var scd = scdList[i];
            var prevSck = i >= 1 ? sckList[i - 1] : 0;
            var prevScd = i >= 1 ? scdList[i - 1] : 0;

            var signal = GetRsiSignal(sck - scd, prevSck - prevScd, sck, prevSck, 70, 30);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Sco", sckList },
            { "Signal", scdList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(sckList);
        stockData.IndicatorName = IndicatorName.StochasticCustomOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Stochastic Regular
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculateStochasticRegular(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 5, 
        int length2 = 3)
    {
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var stoList = CalculateStochasticOscillator(stockData, maType, length1, length2, length2);
        var fastKList = stoList.CustomValuesList;
        var skList = stoList.OutputValues["FastD"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var fk = fastKList[i];
            var sk = skList[i];
            var prevFk = i >= 1 ? fastKList[i - 1] : 0;
            var prevSk = i >= 1 ? skList[i - 1] : 0;

            var signal = GetRsiSignal(fk - sk, prevFk - prevSk, fk, prevFk, 70, 30);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Sco", fastKList },
            { "Signal", skList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(fastKList);
        stockData.IndicatorName = IndicatorName.StochasticRegular;

        return stockData;
    }


    /// <summary>
    /// Calculates the Swami Stochastics
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <returns></returns>
    public static StockData CalculateSwamiStochastics(this StockData stockData, int fastLength = 12, int slowLength = 48)
    {
        List<double> numList = new(stockData.Count);
        List<double> denomList = new(stockData.Count);
        List<double> stochList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, slowLength - fastLength);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var highest = highestList[i];
            var lowest = lowestList[i];
            var prevStoch1 = i >= 1 ? stochList[i - 1] : 0;
            var prevStoch2 = i >= 2 ? stochList[i - 2] : 0;

            var pNum = GetLastOrDefault(numList);
            var num = (currentValue - lowest + pNum) / 2;
            numList.Add(num);

            var pDenom = GetLastOrDefault(denomList);
            var denom = (highest - lowest + pDenom) / 2;
            denomList.Add(denom);

            var stoch = denom != 0 ? MinOrMax((0.2 * num / denom) + (0.8 * prevStoch1), 1, 0) : 0;
            stochList.Add(stoch);

            var signal = GetRsiSignal(stoch - prevStoch1, prevStoch1 - prevStoch2, stoch, prevStoch1, 0.8, 0.2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ss", stochList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(stochList);
        stockData.IndicatorName = IndicatorName.SwamiStochastics;

        return stockData;
    }
}

