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
        List<double> fastKList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var highestHigh = highestList[i];
            var lowestLow = lowestList[i];

            var fastK = highestHigh - lowestLow != 0 ? MinOrMax((currentValue - lowestLow) / (highestHigh - lowestLow) * 100, 100, 0) : 0;
            fastKList.AddRounded(fastK);
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
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "FastK", fastKList },
            { "FastD", fastDList },
            { "SlowD", slowDList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = fastKList;
        stockData.IndicatorName = IndicatorName.StochasticOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the adaptive stochastic.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <param name="fastLength">Length of the fast.</param>
    /// <param name="slowLength">Length of the slow.</param>
    /// <returns></returns>
    public static StockData CalculateAdaptiveStochastic(this StockData stockData, int length = 50, int fastLength = 50, int slowLength = 200)
    {
        List<double> stcList = new();
        List<Signal> signalsList = new();

        var srcList = CalculateLinearRegression(stockData, Math.Abs(slowLength - fastLength)).CustomValuesList;
        var erList = CalculateKaufmanAdaptiveMovingAverage(stockData, length: length).OutputValues["Er"];
        var (highest1List, lowest1List) = GetMaxAndMinValuesList(srcList, fastLength);
        var (highest2List, lowest2List) = GetMaxAndMinValuesList(srcList, slowLength);

        for (var i = 0; i < stockData.Count; i++)
        {
            var er = erList[i];
            var src = srcList[i];
            var highest1 = highest1List[i];
            var lowest1 = lowest1List[i];
            var highest2 = highest2List[i];
            var lowest2 = lowest2List[i];
            var prevStc1 = i >= 1 ? stcList[i - 1] : 0;
            var prevStc2 = i >= 2 ? stcList[i - 2] : 0;
            var a = (er * highest1) + ((1 - er) * highest2);
            var b = (er * lowest1) + ((1 - er) * lowest2);

            var stc = a - b != 0 ? MinOrMax((src - b) / (a - b), 1, 0) : 0;
            stcList.AddRounded(stc);

            var signal = GetRsiSignal(stc - prevStc1, prevStc1 - prevStc2, stc, prevStc1, 0.8, 0.2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ast", stcList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = stcList;
        stockData.IndicatorName = IndicatorName.AdaptiveStochastic;

        return stockData;
    }

    /// <summary>
    /// Calculates the Bilateral Stochastic Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculateBilateralStochasticOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 100, int signalLength = 20)
    {
        List<double> bullList = new();
        List<double> bearList = new();
        List<double> rangeList = new();
        List<double> maxList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);
        var (highestList, lowestList) = GetMaxAndMinValuesList(smaList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var highest = highestList[i];
            var lowest = lowestList[i];

            var range = highest - lowest;
            rangeList.AddRounded(range);
        }

        var rangeSmaList = GetMovingAverageList(stockData, maType, length, rangeList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var sma = smaList[i];
            var highest = highestList[i];
            var lowest = lowestList[i];
            var rangeSma = rangeSmaList[i];

            var bull = rangeSma != 0 ? (sma / rangeSma) - (lowest / rangeSma) : 0;
            bullList.AddRounded(bull);

            var bear = rangeSma != 0 ? Math.Abs((sma / rangeSma) - (highest / rangeSma)) : 0;
            bearList.AddRounded(bear);

            var max = Math.Max(bull, bear);
            maxList.AddRounded(max);
        }

        var signalList = GetMovingAverageList(stockData, maType, signalLength, maxList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var bull = bullList[i];
            var bear = bearList[i];
            var sig = signalList[i];

            var signal = GetConditionSignal(bull > bear || bull > sig, bear > bull || bull < sig);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Bull", bullList },
            { "Bear", bearList },
            { "Bso", maxList },
            { "Signal", signalList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = maxList;
        stockData.IndicatorName = IndicatorName.BilateralStochasticOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Natural Stochastic Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateNaturalStochasticIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 20, int smoothLength = 10)
    {
        List<double> rawNstList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            double weightSum = 0, denomSum = 0;
            for (var j = 0; j < length; j++)
            {
                var hh = i >= j ? highestList[i - j] : 0;
                var ll = i >= j ? lowestList[i - j] : 0;
                var c = i >= j ? inputList[i - j] : 0;
                var range = hh - ll;
                var frac = range != 0 ? (c - ll) / range : 0;
                var ratio = 1 / Sqrt(j + 1);
                weightSum += frac * ratio;
                denomSum += ratio;
            }

            var rawNst = denomSum != 0 ? (200 * weightSum / denomSum) - 100 : 0;
            rawNstList.AddRounded(rawNst);
        }

        var nstList = GetMovingAverageList(stockData, maType, smoothLength, rawNstList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var nst = nstList[i];
            var prevNst1 = i >= 1 ? nstList[i - 1] : 0;
            var prevNst2 = i >= 2 ? nstList[i - 2] : 0;

            var signal = GetCompareSignal(nst - prevNst1, prevNst1 - prevNst2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Nst", nstList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = nstList;
        stockData.IndicatorName = IndicatorName.NaturalStochasticIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Fisher Transform Stochastic Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="stochLength"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateFisherTransformStochasticOscillator(this StockData stockData,
        MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 2, int stochLength = 30, int smoothLength = 5)
    {
        List<double> rbwList = new();
        List<double> ftsoList = new();
        List<double> numList = new();
        List<double> denomList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var wmaList = GetMovingAverageList(stockData, maType, length, inputList);
        var wma2List = GetMovingAverageList(stockData, maType, length, wmaList);
        var wma3List = GetMovingAverageList(stockData, maType, length, wma2List);
        var wma4List = GetMovingAverageList(stockData, maType, length, wma3List);
        var wma5List = GetMovingAverageList(stockData, maType, length, wma4List);
        var wma6List = GetMovingAverageList(stockData, maType, length, wma5List);
        var wma7List = GetMovingAverageList(stockData, maType, length, wma6List);
        var wma8List = GetMovingAverageList(stockData, maType, length, wma7List);
        var wma9List = GetMovingAverageList(stockData, maType, length, wma8List);
        var wma10List = GetMovingAverageList(stockData, maType, length, wma9List);

        for (var i = 0; i < stockData.Count; i++)
        {
            var wma1 = wmaList[i];
            var wma2 = wma2List[i];
            var wma3 = wma3List[i];
            var wma4 = wma4List[i];
            var wma5 = wma5List[i];
            var wma6 = wma6List[i];
            var wma7 = wma7List[i];
            var wma8 = wma8List[i];
            var wma9 = wma9List[i];
            var wma10 = wma10List[i];

            var rbw = ((wma1 * 5) + (wma2 * 4) + (wma3 * 3) + (wma4 * 2) + wma5 + wma6 + wma7 + wma8 + wma9 + wma10) / 20;
            rbwList.AddRounded(rbw);
        }

        var (highestList, lowestList) = GetMaxAndMinValuesList(rbwList, stochLength);
        for (var i = 0; i < stockData.Count; i++)
        {
            var highest = highestList[i];
            var lowest = lowestList[i];
            var rbw = rbwList[i];
            var prevFtso1 = i >= 1 ? ftsoList[i - 1] : 0;
            var prevFtso2 = i >= 2 ? ftsoList[i - 2] : 0;

            var num = rbw - lowest;
            numList.AddRounded(num);

            var denom = highest - lowest;
            denomList.AddRounded(denom);

            var numSum = numList.TakeLastExt(smoothLength).Sum();
            var denomSum = denomList.TakeLastExt(smoothLength).Sum();
            var rbws = denomSum + 0.0001 != 0 ? MinOrMax(numSum / (denomSum + 0.0001) * 100, 100, 0) : 0;
            var x = 0.1 * (rbws - 50);

            var ftso = MinOrMax((((Exp(2 * x) - 1) / (Exp(2 * x) + 1)) + 1) * 50, 100, 0);
            ftsoList.AddRounded(ftso);

            var signal = GetRsiSignal(ftso - prevFtso1, prevFtso1 - prevFtso2, ftso, prevFtso1, 70, 30);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ftso", ftsoList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = ftsoList;
        stockData.IndicatorName = IndicatorName.FisherTransformStochasticOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Fast and Slow Stochastic Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="length4"></param>
    /// <returns></returns>
    public static StockData CalculateFastandSlowStochasticOscillator(this StockData stockData,
        MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length1 = 3, int length2 = 6, int length3 = 9, int length4 = 9)
    {
        List<double> fsstList = new();
        List<Signal> signalsList = new();

        var fskList = CalculateFastandSlowKurtosisOscillator(stockData, maType, length1).CustomValuesList;
        var v4List = GetMovingAverageList(stockData, maType, length2, fskList);
        var fastKList = CalculateStochasticOscillator(stockData, maType, length: length3).CustomValuesList;
        var slowKList = GetMovingAverageList(stockData, maType, length3, fastKList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var v4 = v4List[i];
            var slowK = slowKList[i];

            var fsst = (500 * v4) + slowK;
            fsstList.AddRounded(fsst);
        }

        var wfsstList = GetMovingAverageList(stockData, maType, length4, fsstList);
        for (var i = 0; i < wfsstList.Count; i++)
        {
            var fsst = fsstList[i];
            var wfsst = wfsstList[i];
            var prevFsst = i >= 1 ? fsstList[i - 1] : 0;
            var prevWfsst = i >= 1 ? wfsstList[i - 1] : 0;

            var signal = GetCompareSignal(fsst - wfsst, prevFsst - prevWfsst);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Fsst", fsstList },
            { "Signal", wfsstList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = fsstList;
        stockData.IndicatorName = IndicatorName.FastandSlowStochasticOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Premier Stochastic Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculatePremierStochasticOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 8, int smoothLength = 25)
    {
        List<double> nskList = new();
        List<double> psoList = new();
        List<Signal> signalsList = new();

        var len = MinOrMax((int)Math.Ceiling(Sqrt(smoothLength)));

        var stochasticRsiList = CalculateStochasticOscillator(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var sk = stochasticRsiList[i];

            var nsk = 0.1 * (sk - 50);
            nskList.AddRounded(nsk);
        }

        var nskEmaList = GetMovingAverageList(stockData, maType, len, nskList);
        var ssList = GetMovingAverageList(stockData, maType, len, nskEmaList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var ss = ssList[i];
            var prevPso1 = i >= 1 ? psoList[i - 1] : 0;
            var prevPso2 = i >= 2 ? psoList[i - 2] : 0;
            var expss = Exp(ss);

            var pso = expss + 1 != 0 ? MinOrMax((expss - 1) / (expss + 1), 1, -1) : 0;
            psoList.AddRounded(pso);

            var signal = GetRsiSignal(pso - prevPso1, prevPso1 - prevPso2, pso, prevPso1, 0.9, -0.9);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Pso", psoList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = psoList;
        stockData.IndicatorName = IndicatorName.PremierStochasticOscillator;

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
        List<Signal> signalsList = new();

        var turbo = turboLength < 0 ? Math.Max(turboLength, length2 * -1) : turboLength > 0 ? Math.Min(turboLength, length2) : 0;

        var fastKList = CalculateStochasticOscillator(stockData, maType, length: length1).CustomValuesList;
        var fastDList = GetMovingAverageList(stockData, maType, length1, fastKList);
        stockData.CustomValuesList = fastKList;
        var tsfKList = CalculateLinearRegression(stockData, length2 + turbo).CustomValuesList;
        stockData.CustomValuesList = fastDList;
        var tsfDList = CalculateLinearRegression(stockData, length2 + turbo).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var tsfD = tsfDList[i];
            var tsfK = tsfKList[i];
            var prevTsfk = i >= 1 ? tsfKList[i - 1] : 0;
            var prevTsfd = i >= 1 ? tsfDList[i - 1] : 0;

            var signal = GetRsiSignal(tsfK - tsfD, prevTsfk - prevTsfd, tsfK, prevTsfk, 70, 30);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Tsf", tsfKList },
            { "Signal", tsfDList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = tsfKList;
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
        List<double> tssDList = new();
        List<double> tssKList = new();
        List<Signal> signalsList = new();

        var turbo = turboLength < 0 ? Math.Max(turboLength, length2 * -1) : turboLength > 0 ? Math.Min(turboLength, length2) : 0;

        var fastKList = CalculateStochasticOscillator(stockData, maType, length: length1).CustomValuesList;
        var slowKList = GetMovingAverageList(stockData, maType, length1, fastKList);
        var slowDList = GetMovingAverageList(stockData, maType, length1, slowKList);
        stockData.CustomValuesList = slowKList;
        var tsfKList = CalculateLinearRegression(stockData, length2 + turbo).CustomValuesList;
        stockData.CustomValuesList = slowDList;
        var tsfDList = CalculateLinearRegression(stockData, length2 + turbo).CustomValuesList;

        for (var i = 0; i < tssDList.Count; i++)
        {
            var tssD = tssDList[i];
            var tssK = tssKList[i];
            var prevTssk = i >= 1 ? tssKList[i - 1] : 0;
            var prevTssd = i >= 1 ? tssDList[i - 1] : 0;

            var signal = GetRsiSignal(tssK - tssD, prevTssk - prevTssd, tssK, prevTssk, 70, 30);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Tsf", tsfKList },
            { "Signal", tsfDList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = tsfKList;
        stockData.IndicatorName = IndicatorName.TurboStochasticsSlow;

        return stockData;
    }

    /// <summary>
    /// Calculates the Recursive Stochastic
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="alpha"></param>
    /// <returns></returns>
    public static StockData CalculateRecursiveStochastic(this StockData stockData, int length = 200, double alpha = 0.1)
    {
        List<double> kList = new();
        List<double> maList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(inputList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var highest = highestList[i];
            var lowest = lowestList[i];
            var stoch = highest - lowest != 0 ? (currentValue - lowest) / (highest - lowest) * 100 : 0;
            var prevK1 = i >= 1 ? kList[i - 1] : 0;
            var prevK2 = i >= 2 ? kList[i - 2] : 0;

            var ma = (alpha * stoch) + ((1 - alpha) * prevK1);
            maList.AddRounded(ma);

            var lbList = maList.TakeLastExt(length).ToList();
            var highestMa = lbList.Max();
            var lowestMa = lbList.Min();

            var k = highestMa - lowestMa != 0 ? MinOrMax((ma - lowestMa) / (highestMa - lowestMa) * 100, 100, 0) : 0;
            kList.AddRounded(k);

            var signal = GetRsiSignal(k - prevK1, prevK1 - prevK2, k, prevK1, 80, 20);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Rsto", kList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = kList;
        stockData.IndicatorName = IndicatorName.RecursiveStochastic;

        return stockData;
    }

    /// <summary>
    /// Calculates the DiNapoli Preferred Stochastic Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <returns></returns>
    public static StockData CalculateDiNapoliPreferredStochasticOscillator(this StockData stockData, int length1 = 8, int length2 = 3, int length3 = 3)
    {
        List<double> rList = new();
        List<double> sList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var max = highestList[i];
            var min = lowestList[i];
            var fast = max - min != 0 ? MinOrMax((currentValue - min) / (max - min) * 100, 100, 0) : 0;

            var prevR = rList.LastOrDefault();
            var r = prevR + ((fast - prevR) / length2);
            rList.AddRounded(r);

            var prevS = sList.LastOrDefault();
            var s = prevS + ((r - prevS) / length3);
            sList.AddRounded(s);

            var signal = GetRsiSignal(r - s, prevR - prevS, r, prevR, 70, 30);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Dpso", rList },
            { "Signal", sList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = rList;
        stockData.IndicatorName = IndicatorName.DiNapoliPreferredStochasticOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Double Smoothed Stochastic
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="length4"></param>
    /// <returns></returns>
    public static StockData CalculateDoubleSmoothedStochastic(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 2,
        int length2 = 3, int length3 = 15, int length4 = 3)
    {
        List<double> dssList = new();
        List<double> numList = new();
        List<double> denomList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var highestHigh = highestList[i];
            var lowestLow = lowestList[i];

            var num = currentValue - lowestLow;
            numList.AddRounded(num);

            var denom = highestHigh - lowestLow;
            denomList.AddRounded(denom);
        }

        var ssNumList = GetMovingAverageList(stockData, maType, length2, numList);
        var ssDenomList = GetMovingAverageList(stockData, maType, length2, denomList);
        var dsNumList = GetMovingAverageList(stockData, maType, length3, ssNumList);
        var dsDenomList = GetMovingAverageList(stockData, maType, length3, ssDenomList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var dsNum = dsNumList[i];
            var dsDenom = dsDenomList[i];

            var dss = dsDenom != 0 ? MinOrMax(100 * dsNum / dsDenom, 100, 0) : 0;
            dssList.AddRounded(dss);
        }

        var sdssList = GetMovingAverageList(stockData, maType, length4, dssList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var dss = dssList[i];
            var sdss = sdssList[i];
            var prevDss = i >= 1 ? dssList[i - 1] : 0;
            var prevSdss = i >= 1 ? sdssList[i - 1] : 0;

            var signal = GetRsiSignal(dss - sdss, prevDss - prevSdss, dss, prevDss, 70, 30);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Dss", dssList },
            { "Signal", sdssList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = dssList;
        stockData.IndicatorName = IndicatorName.DoubleSmoothedStochastic;

        return stockData;
    }

    /// <summary>
    /// Calculates the Double Stochastic Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateDoubleStochasticOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14,
        int smoothLength = 3)
    {
        List<double> doubleKList = new();
        List<Signal> signalsList = new();

        var stochasticList = CalculateStochasticOscillator(stockData, maType, length: length).CustomValuesList;
        var (highestList, lowestList) = GetMaxAndMinValuesList(stochasticList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var slowK = stochasticList[i];
            var highestSlowK = highestList[i];
            var lowestSlowK = lowestList[i];

            var doubleK = highestSlowK - lowestSlowK != 0 ? MinOrMax((slowK - lowestSlowK) / (highestSlowK - lowestSlowK) * 100, 100, 0) : 0;
            doubleKList.AddRounded(doubleK);
        }

        var doubleSlowKList = GetMovingAverageList(stockData, maType, smoothLength, doubleKList);
        var doubleKSignalList = GetMovingAverageList(stockData, maType, smoothLength, doubleSlowKList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var doubleSlowK = doubleSlowKList[i];
            var doubleKSignal = doubleKSignalList[i];
            var prevDoubleslowk = i >= 1 ? doubleSlowKList[i - 1] : 0;
            var prevDoubleKSignal = i >= 1 ? doubleKSignalList[i - 1] : 0;

            var signal = GetRsiSignal(doubleSlowK - doubleKSignal, prevDoubleslowk - prevDoubleKSignal, doubleSlowK, prevDoubleslowk, 80, 20);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Dso", doubleSlowKList },
            { "Signal", doubleKSignalList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = doubleSlowKList;
        stockData.IndicatorName = IndicatorName.DoubleStochasticOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the DMI Stochastic
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="length4"></param>
    /// <returns></returns>
    public static StockData CalculateDMIStochastic(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 10,
        int length2 = 10, int length3 = 3, int length4 = 3)
    {
        List<double> dmiOscillatorList = new();
        List<double> fastKList = new();
        List<Signal> signalsList = new();

        var adxList = CalculateAverageDirectionalIndex(stockData, maType, length1);
        var diPlusList = adxList.OutputValues["DiPlus"];
        var diMinusList = adxList.OutputValues["DiMinus"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var pdi = diPlusList[i];
            var ndi = diMinusList[i];

            var dmiOscillator = ndi - pdi;
            dmiOscillatorList.AddRounded(dmiOscillator);
        }

        var (highestList, lowestList) = GetMaxAndMinValuesList(dmiOscillatorList, length2);
        for (var i = 0; i < stockData.Count; i++)
        {
            var highest = highestList[i];
            var lowest = lowestList[i];
            var dmiOscillator = dmiOscillatorList[i];

            var fastK = highest - lowest != 0 ? MinOrMax((dmiOscillator - lowest) / (highest - lowest) * 100, 100, 0) : 0;
            fastKList.AddRounded(fastK);
        }

        var slowKList = GetMovingAverageList(stockData, maType, length3, fastKList);
        var dmiStochList = GetMovingAverageList(stockData, maType, length4, slowKList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var dmiStoch = dmiStochList[i];
            var prevDmiStoch1 = i >= 1 ? dmiStochList[i - 1] : 0;
            var prevDmiStoch2 = i >= 2 ? dmiStochList[i - 2] : 0;

            var signal = GetRsiSignal(dmiStoch - prevDmiStoch1, prevDmiStoch1 - prevDmiStoch2, dmiStoch, prevDmiStoch1, 90, 10);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "DmiStochastic", dmiStochList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = dmiStochList;
        stockData.IndicatorName = IndicatorName.DMIStochastic;

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
        List<double> dList = new();
        List<double> hlList = new();
        List<double> smiList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var highestHigh = highestList[i];
            var lowestLow = lowestList[i];
            var median = (highestHigh + lowestLow) / 2;

            var diff = currentValue - median;
            dList.AddRounded(diff);

            var highLow = highestHigh - lowestLow;
            hlList.AddRounded(highLow);
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
            smiList.AddRounded(smi);
        }

        var smiSignalList = GetMovingAverageList(stockData, maType, smoothLength2, smiList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var smi = smiList[i];
            var smiSignal = smiSignalList[i];
            var prevSmi = i >= 1 ? smiList[i - 1] : 0;
            var prevSmiSignal = i >= 1 ? smiSignalList[i - 1] : 0;

            var signal = GetCompareSignal(smi - smiSignal, prevSmi - prevSmiSignal);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Smi", smiList },
            { "Signal", smiSignalList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = smiList;
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
        List<Signal> signalsList = new();

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
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Sfo", pkList },
            { "Signal", pdList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = pkList;
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
        List<double> numList = new();
        List<double> denomList = new();
        List<double> sckList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var highestHigh = highestList[i];
            var lowestLow = lowestList[i];

            var num = currentValue - lowestLow;
            numList.AddRounded(num);

            var denom = highestHigh - lowestLow;
            denomList.AddRounded(denom);
        }

        var numSmaList = GetMovingAverageList(stockData, maType, length2, numList);
        var denomSmaList = GetMovingAverageList(stockData, maType, length2, denomList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var numSma = numSmaList[i];
            var denomSma = denomSmaList[i];

            var sck = denomSma != 0 ? MinOrMax(numSma / denomSma * 100, 100, 0) : 0;
            sckList.AddRounded(sck);
        }

        var scdList = GetMovingAverageList(stockData, maType, length3, sckList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var sck = sckList[i];
            var scd = scdList[i];
            var prevSck = i >= 1 ? sckList[i - 1] : 0;
            var prevScd = i >= 1 ? scdList[i - 1] : 0;

            var signal = GetRsiSignal(sck - scd, prevSck - prevScd, sck, prevSck, 70, 30);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Sco", sckList },
            { "Signal", scdList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = sckList;
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
        List<Signal> signalsList = new();

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
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Sco", fastKList },
            { "Signal", skList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = fastKList;
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
        List<double> numList = new();
        List<double> denomList = new();
        List<double> stochList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, slowLength - fastLength);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var highest = highestList[i];
            var lowest = lowestList[i];
            var prevStoch1 = i >= 1 ? stochList[i - 1] : 0;
            var prevStoch2 = i >= 2 ? stochList[i - 2] : 0;

            var pNum = numList.LastOrDefault();
            var num = (currentValue - lowest + pNum) / 2;
            numList.AddRounded(num);

            var pDenom = denomList.LastOrDefault();
            var denom = (highest - lowest + pDenom) / 2;
            denomList.AddRounded(denom);

            var stoch = denom != 0 ? MinOrMax((0.2 * num / denom) + (0.8 * prevStoch1), 1, 0) : 0;
            stochList.AddRounded(stoch);

            var signal = GetRsiSignal(stoch - prevStoch1, prevStoch1 - prevStoch2, stoch, prevStoch1, 0.8, 0.2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ss", stochList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = stochList;
        stockData.IndicatorName = IndicatorName.SwamiStochastics;

        return stockData;
    }
}
