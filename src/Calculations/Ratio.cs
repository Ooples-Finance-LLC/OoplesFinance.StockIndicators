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
    /// Calculates the Martin Ratio
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="bmk"></param>
    /// <returns></returns>
    public static StockData CalculateMartinRatio(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 30, double bmk = 0.02)
    {
        List<double> martinList = new();
        List<double> benchList = new();
        List<double> retList = new();

        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        double barMin = 60 * 24;
        double minPerYr = 60 * 24 * 30 * 12;
        var barsPerYr = minPerYr / barMin;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= length ? inputList[i - length] : 0;

            var bench = Pow(1 + bmk, length / barsPerYr) - 1;
            benchList.Add(bench);

            var ret = prevValue != 0 ? (100 * (currentValue / prevValue)) - 1 - (bench * 100) : 0;
            retList.Add(ret);
        }

        var retSmaList = GetMovingAverageList(stockData, maType, length, retList);
        stockData.CustomValuesList = retList;
        var ulcerIndexList = CalculateUlcerIndex(stockData, length).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var ulcerIndex = ulcerIndexList[i];
            var retSma = retSmaList[i];

            var prevMartin = martinList.LastOrDefault();
            var martin = ulcerIndex != 0 ? retSma / ulcerIndex : 0;
            martinList.Add(martin);

            var signal = GetCompareSignal(martin - 2, prevMartin - 2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Mr", martinList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = martinList;
        stockData.IndicatorName = IndicatorName.MartinRatio;

        return stockData;
    }

    /// <summary>
    /// Calculates the Upside Potential Ratio
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="bmk"></param>
    /// <returns></returns>
    public static StockData CalculateUpsidePotentialRatio(this StockData stockData, int length = 30, double bmk = 0.05)
    {
        List<double> retList = new();
        List<double> upsidePotentialList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        double barMin = 60 * 24;
        double minPerYr = 60 * 24 * 30 * 12;
        var barsPerYr = minPerYr / barMin;
        var ratio = (double)1 / length;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= length ? inputList[i - length] : 0;
            var bench = Pow(1 + bmk, length / barsPerYr) - 1;

            var ret = prevValue != 0 ? (currentValue / prevValue) - 1 : 0;
            retList.Add(ret);

            double downSide = 0, upSide = 0;
            for (var j = 0; j < length; j++)
            {
                var iValue = i >= j ? retList[i - j] : 0;
                downSide += iValue < bench ? Pow(iValue - bench, 2) * ratio : 0;
                upSide += iValue > bench ? (iValue - bench) * ratio : 0;
            }

            var prevUpsidePotential = upsidePotentialList.LastOrDefault();
            var upsidePotential = downSide >= 0 ? upSide / Sqrt(downSide) : 0;
            upsidePotentialList.Add(upsidePotential);

            var signal = GetCompareSignal(upsidePotential - 5, prevUpsidePotential - 5);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Upr", upsidePotentialList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = upsidePotentialList;
        stockData.IndicatorName = IndicatorName.UpsidePotentialRatio;

        return stockData;
    }

    /// <summary>
    /// Calculates the Information Ratio
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="bmk"></param>
    /// <returns></returns>
    public static StockData CalculateInformationRatio(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 30, double bmk = 0.05)
    {
        List<double> infoList = new();
        List<double> retList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        double barMin = 60 * 24;
        double minPerYr = 60 * 24 * 30 * 12;
        var barsPerYr = minPerYr / barMin;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= length ? inputList[i - length] : 0;

            var ret = prevValue != 0 ? (currentValue / prevValue) - 1 : 0;
            retList.Add(ret);
        }

        stockData.CustomValuesList = retList;
        var stdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        var retSmaList = GetMovingAverageList(stockData, maType, length, retList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var stdDeviation = stdDevList[i];
            var retSma = retSmaList[i];
            var bench = Pow(1 + bmk, length / barsPerYr) - 1;

            var prevInfo = infoList.LastOrDefault();
            var info = stdDeviation != 0 ? (retSma - bench) / stdDeviation : 0;
            infoList.Add(info);

            var signal = GetCompareSignal(info - 5, prevInfo - 5);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ir", infoList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = infoList;
        stockData.IndicatorName = IndicatorName.InformationRatio;

        return stockData;
    }

    /// <summary>
    /// Calculates the Omega Ratio
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="bmk"></param>
    /// <returns></returns>
    public static StockData CalculateOmegaRatio(this StockData stockData, int length = 30, double bmk = 0.05)
    {
        List<double> omegaList = new();
        List<double> retList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        double barMin = 60 * 24;
        double minPerYr = 60 * 24 * 30 * 12;
        var barsPerYr = minPerYr / barMin;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= length ? inputList[i - length] : 0;
            var bench = Pow(1 + bmk, length / barsPerYr) - 1;

            var ret = prevValue != 0 ? (currentValue / prevValue) - 1 : 0;
            retList.Add(ret);

            double downSide = 0, upSide = 0;
            for (var j = 0; j < length; j++)
            {
                var iValue = i >= j ? retList[i - j] : 0;
                downSide += iValue < bench ? bench - iValue : 0;
                upSide += iValue > bench ? iValue - bench : 0;
            }

            var prevOmega = omegaList.LastOrDefault();
            var omega = downSide != 0 ? upSide / downSide : 0;
            omegaList.Add(omega);

            var signal = GetCompareSignal(omega - 5, prevOmega - 5);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Or", omegaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = omegaList;
        stockData.IndicatorName = IndicatorName.OmegaRatio;

        return stockData;
    }

    /// <summary>
    /// Calculates the Volatility Ratio
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="breakoutLevel"></param>
    /// <returns></returns>
    public static StockData CalculateVolatilityRatio(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 14, double breakoutLevel = 0.5)
    {
        List<double> vrList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length - 1);

        var emaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentEma = emaList[i];
            var prevHighest = i >= 1 ? highestList[i - 1] : 0;
            var prevLowest = i >= 1 ? lowestList[i - 1] : 0;
            var priorValue = i >= length + 1 ? inputList[i - (length + 1)] : 0;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevEma = i >= 1 ? emaList[i - 1] : 0;
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var tr = CalculateTrueRange(currentHigh, currentLow, prevValue);
            var max = priorValue != 0 ? Math.Max(prevHighest, priorValue) : prevHighest;
            var min = priorValue != 0 ? Math.Min(prevLowest, priorValue) : prevLowest;

            var vr = max - min != 0 ? tr / (max - min) : 0;
            vrList.Add(vr);

            var signal = GetVolatilitySignal(currentValue - currentEma, prevValue - prevEma, vr, breakoutLevel);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Vr", vrList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = vrList;
        stockData.IndicatorName = IndicatorName.VolatilityRatio;

        return stockData;
    }

    /// <summary>
    /// Calculates the Calmar Ratio
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateCalmarRatio(this StockData stockData, int length = 30)
    {
        List<double> calmarList = new();
        List<double> ddList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var (highestList, _) = GetMaxAndMinValuesList(inputList, length);

        double barMin = 60 * 24;
        double minPerYr = 60 * 24 * 30 * 12;
        var barsPerYr = minPerYr / barMin;
        var power = barsPerYr / (length * 15);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= length ? inputList[i - length] : 0;
            var maxDn = highestList[i];

            var dd = maxDn != 0 ? (currentValue - maxDn) / maxDn : 0;
            ddList.Add(dd);

            var ret = prevValue != 0 ? (currentValue / prevValue) - 1 : 0;
            var annualReturn = 1 + ret >= 0 ? Pow(1 + ret, power) - 1 : 0;
            var maxDd = ddList.TakeLastExt(length).Min();

            var prevCalmar = calmarList.LastOrDefault();
            var calmar = maxDd != 0 ? annualReturn / Math.Abs(maxDd) : 0;
            calmarList.Add(calmar);

            var signal = GetCompareSignal(calmar - 2, prevCalmar - 2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Cr", calmarList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = calmarList;
        stockData.IndicatorName = IndicatorName.CalmarRatio;

        return stockData;
    }

    /// <summary>
    /// Calculates the Treynor Ratio
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="beta"></param>
    /// <param name="bmk"></param>
    /// <returns></returns>
    public static StockData CalculateTreynorRatio(this StockData stockData, int length = 30, double beta = 1, double bmk = 0.02)
    {
        List<double> treynorList = new();
        List<double> retList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        double barMin = 60 * 24, minPerYr = 60 * 24 * 30 * 12, barsPerYr = minPerYr / barMin;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= length ? inputList[i - length] : 0;
            var bench = Pow(1 + bmk, length / barsPerYr) - 1;

            var ret = prevValue != 0 ? (currentValue / prevValue) - 1 : 0;
            retList.Add(ret);

            var retSma = retList.TakeLastExt(length).Average();
            var prevTreynor = treynorList.LastOrDefault();
            var treynor = beta != 0 ? (retSma - bench) / beta : 0;
            treynorList.Add(treynor);

            var signal = GetCompareSignal(treynor - 2, prevTreynor - 2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Tr", treynorList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = treynorList;
        stockData.IndicatorName = IndicatorName.TreynorRatio;

        return stockData;
    }

    /// <summary>
    /// Calculates the Sortino Ratio
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="bmk"></param>
    /// <returns></returns>
    public static StockData CalculateSortinoRatio(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 30, 
        double bmk = 0.02)
    {
        List<double> sortinoList = new();
        List<double> retList = new();
        List<double> deviationSquaredList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        double minPerYr = 60 * 24 * 30 * 12, barMin = 60 * 24, barsPerYr = minPerYr / barMin;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= length ? inputList[i - length] : 0;
            var bench = Pow(1 + bmk, length / barsPerYr) - 1;

            var ret = prevValue != 0 ? (currentValue / prevValue) - 1 - bench : 0;
            retList.Add(ret);
        }

        var retSmaList = GetMovingAverageList(stockData, maType, length, retList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var ret = retList[i];
            var retSma = retSmaList[i];
            var currentDeviation = Math.Min(ret - retSma, 0);

            var deviationSquared = Pow(currentDeviation, 2);
            deviationSquaredList.Add(deviationSquared);
        }

        var divisionOfSumList = GetMovingAverageList(stockData, maType, length, deviationSquaredList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var divisionOfSum = divisionOfSumList[i];
            var stdDeviation = Sqrt(divisionOfSum);
            var retSma = retSmaList[i];

            var prevSortino = sortinoList.LastOrDefault();
            var sortino = stdDeviation != 0 ? retSma / stdDeviation : 0;
            sortinoList.Add(sortino);

            var signal = GetCompareSignal(sortino - 2, prevSortino - 2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Sr", sortinoList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = sortinoList;
        stockData.IndicatorName = IndicatorName.SortinoRatio;

        return stockData;
    }

    /// <summary>
    /// Calculates the Sharpe Ratio
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="bmk"></param>
    /// <returns></returns>
    public static StockData CalculateSharpeRatio(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 30, 
        double bmk = 0.02)
    {
        List<double> sharpeList = new();
        List<double> retList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        double minPerYr = 60 * 24 * 30 * 12, barMin = 60 * 24, barsPerYr = minPerYr / barMin;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= length ? inputList[i - length] : 0;
            var bench = Pow(1 + bmk, length / barsPerYr) - 1;

            var ret = prevValue != 0 ? (currentValue / prevValue) - 1 - bench : 0;
            retList.Add(ret);
        }

        var retSmaList = GetMovingAverageList(stockData, maType, length, retList);
        stockData.CustomValuesList = retList;
        var stdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var stdDeviation = stdDevList[i];
            var retSma = retSmaList[i];

            var prevSharpe = sharpeList.LastOrDefault();
            var sharpe = stdDeviation != 0 ? retSma / stdDeviation : 0;
            sharpeList.Add(sharpe);

            var signal = GetCompareSignal(sharpe - 2, prevSharpe - 2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Sr", sharpeList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = sharpeList;
        stockData.IndicatorName = IndicatorName.SharpeRatio;

        return stockData;
    }

    /// <summary>
    /// Calculates the Shinohara Intensity Ratio
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateShinoharaIntensityRatio(this StockData stockData, int length = 14)
    {
        List<double> tempOpenList = new();
        List<double> tempLowList = new();
        List<double> tempHighList = new();
        List<double> prevCloseList = new();
        List<double> ratioAList = new();
        List<double> ratioBList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, openList, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var high = highList[i];
            tempHighList.Add(high);

            var low = lowList[i];
            tempLowList.Add(low);

            var open = openList[i];
            tempOpenList.Add(open);

            var prevClose = i >= 1 ? inputList[i - 1] : 0;
            prevCloseList.Add(prevClose);

            var highSum = tempHighList.TakeLastExt(length).Sum();
            var lowSum = tempLowList.TakeLastExt(length).Sum();
            var openSum = openList.TakeLastExt(length).Sum();
            var prevCloseSum = prevCloseList.TakeLastExt(length).Sum();
            var bullA = highSum - openSum;
            var bearA = openSum - lowSum;
            var bullB = highSum - prevCloseSum;
            var bearB = prevCloseSum - lowSum;

            var prevRatioA = ratioAList.LastOrDefault();
            var ratioA = bearA != 0 ? bullA / bearA * 100 : 0;
            ratioAList.Add(ratioA);

            var prevRatioB = ratioBList.LastOrDefault();
            var ratioB = bearB != 0 ? bullB / bearB * 100 : 0;
            ratioBList.Add(ratioB);

            var signal = GetCompareSignal(ratioA - ratioB, prevRatioA - prevRatioB);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "ARatio", ratioAList },
            { "BRatio", ratioBList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.ShinoharaIntensityRatio;

        return stockData;
    }
}