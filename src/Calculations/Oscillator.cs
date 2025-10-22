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
    /// Calculates the index of the commodity channel.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="inputName">Name of the input.</param>
    /// <param name="constant">The constant.</param>
    /// <returns></returns>
    public static StockData CalculateCommodityChannelIndex(this StockData stockData, InputName inputName = InputName.TypicalPrice,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 20, double constant = 0.015)
    {
        List<double> cciList = new();
        List<double> tpDevDiffList = new();
        List<Signal> signalsList = new();

        var (inputList, _, _, _, _, _) = GetInputValuesList(inputName, stockData);
        var tpSmaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var tpSma = tpSmaList[i];

            var tpDevDiff = Math.Abs(currentValue - tpSma);
            tpDevDiffList.AddRounded(tpDevDiff);
        }

        var tpMeanDevList = GetMovingAverageList(stockData, maType, length, tpDevDiffList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var prevCci1 = i >= 1 ? cciList[i - 1] : 0;
            var prevCci2 = i >= 2 ? cciList[i - 2] : 0;
            var tpMeanDev = tpMeanDevList[i];
            var currentValue = inputList[i];
            var tpSma = tpSmaList[i];

            var cci = tpMeanDev != 0 ? (currentValue - tpSma) / (constant * tpMeanDev) : 0;
            cciList.AddRounded(cci);

            var signal = GetRsiSignal(cci - prevCci1, prevCci1 - prevCci2, cci, prevCci1, 100, -100);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Cci", cciList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = cciList;
        stockData.IndicatorName = IndicatorName.CommodityChannelIndex;

        return stockData;
    }

    /// <summary>
    /// Calculates the Awesome Oscillator
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="inputName">Name of the input.</param>
    /// <param name="fastLength">Length of the fast.</param>
    /// <param name="slowLength">Length of the slow.</param>
    /// <returns></returns>
    public static StockData CalculateAwesomeOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        InputName inputName = InputName.MedianPrice, int fastLength = 5, int slowLength = 34)
    {
        List<double> aoList = new();
        List<Signal> signalsList = new();

        var (inputList, _, _, _, _, _) = GetInputValuesList(inputName, stockData);
        var fastSmaList = GetMovingAverageList(stockData, maType, fastLength, inputList);
        var slowSmaList = GetMovingAverageList(stockData, maType, slowLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var fastSma = fastSmaList[i];
            var slowSma = slowSmaList[i];

            var prevAo = aoList.LastOrDefault();
            var ao = fastSma - slowSma;
            aoList.AddRounded(ao);

            var signal = GetCompareSignal(ao, prevAo);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ao", aoList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = aoList;
        stockData.IndicatorName = IndicatorName.AwesomeOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Accelerator Oscillator
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="inputName">Name of the input.</param>
    /// <param name="fastLength">Length of the fast.</param>
    /// <param name="slowLength">Length of the slow.</param>
    /// <param name="smoothLength">Length of the smooth.</param>
    /// <returns></returns>
    public static StockData CalculateAcceleratorOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, InputName inputName = InputName.MedianPrice,
        int fastLength = 5, int slowLength = 34, int smoothLength = 5)
    {
        List<double> acList = new();
        List<Signal> signalsList = new();

        var awesomeOscList = CalculateAwesomeOscillator(stockData, maType, inputName, fastLength, slowLength).CustomValuesList;
        var awesomeOscMaList = GetMovingAverageList(stockData, maType, smoothLength, awesomeOscList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var ao = awesomeOscList[i];
            var aoSma = awesomeOscMaList[i];

            var prevAc = acList.LastOrDefault();
            var ac = ao - aoSma;
            acList.AddRounded(ac);

            var signal = GetCompareSignal(ac, prevAc);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ac", acList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = acList;
        stockData.IndicatorName = IndicatorName.AcceleratorOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the ulcer index.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateUlcerIndex(this StockData stockData, int length = 14)
    {
        List<double> ulcerIndexList = new();
        List<double> pctDrawdownSquaredList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var (highestList, _) = GetMaxAndMinValuesList(inputList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var maxValue = highestList[i];
            var prevUlcerIndex1 = i >= 1 ? ulcerIndexList[i - 1] : 0;
            var prevUlcerIndex2 = i >= 2 ? ulcerIndexList[i - 2] : 0;

            var pctDrawdownSquared = maxValue != 0 ? Pow((currentValue - maxValue) / maxValue * 100, 2) : 0;
            pctDrawdownSquaredList.AddRounded(pctDrawdownSquared);

            var squaredAvg = pctDrawdownSquaredList.TakeLastExt(length).Average();

            var ulcerIndex = squaredAvg >= 0 ? Sqrt(squaredAvg) : 0;
            ulcerIndexList.AddRounded(ulcerIndex);

            var signal = GetCompareSignal(ulcerIndex - prevUlcerIndex1, prevUlcerIndex1 - prevUlcerIndex2, true);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ui", ulcerIndexList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = ulcerIndexList;
        stockData.IndicatorName = IndicatorName.UlcerIndex;

        return stockData;
    }

    /// <summary>
    /// Calculates the balance of power.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateBalanceOfPower(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14)
    {
        List<double> balanceOfPowerList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, openList, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentClose = inputList[i];
            var currentOpen = openList[i];
            var currentHigh = highList[i];
            var currentLow = lowList[i];

            var balanceOfPower = currentHigh - currentLow != 0 ? (currentClose - currentOpen) / (currentHigh - currentLow) : 0;
            balanceOfPowerList.AddRounded(balanceOfPower);
        }

        var bopSignalList = GetMovingAverageList(stockData, maType, length, balanceOfPowerList);
        for (var i = 0; i < stockData.ClosePrices.Count; i++)
        {
            var bop = balanceOfPowerList[i];
            var bopMa = bopSignalList[i];
            var prevBop = i >= 1 ? balanceOfPowerList[i - 1] : 0;
            var prevBopMa = i >= 1 ? bopSignalList[i - 1] : 0;

            var signal = GetCompareSignal(bop - bopMa, prevBop - prevBopMa);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Bop", balanceOfPowerList },
            { "BopSignal", bopSignalList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = balanceOfPowerList;
        stockData.IndicatorName = IndicatorName.BalanceOfPower;

        return stockData;
    }

    /// <summary>
    /// Calculates the rate of change.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateRateOfChange(this StockData stockData, int length = 12)
    {
        List<double> rocList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= length ? inputList[i - length] : 0;
            var prevRoc1 = i >= 1 ? rocList[i - 1] : 0;
            var prevRoc2 = i >= 2 ? rocList[i - 2] : 0;

            var roc = prevValue != 0 ? MinPastValues(i, length, currentValue - prevValue) / prevValue * 100 : 0;
            rocList.AddRounded(roc);

            var signal = GetCompareSignal(roc - prevRoc1, prevRoc1 - prevRoc2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Roc", rocList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = rocList;
        stockData.IndicatorName = IndicatorName.RateOfChange;

        return stockData;
    }

    /// <summary>
    /// Calculates the Chaikin Oscillator
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="fastLength">Length of the fast.</param>
    /// <param name="slowLength">Length of the slow.</param>
    /// <returns></returns>
    public static StockData CalculateChaikinOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int fastLength = 3, int slowLength = 10)
    {
        List<double> chaikinOscillatorList = new();
        List<Signal> signalsList = new();

        var adlList = CalculateAccumulationDistributionLine(stockData, maType, fastLength).CustomValuesList;
        var adl3EmaList = GetMovingAverageList(stockData, maType, fastLength, adlList);
        var adl10EmaList = GetMovingAverageList(stockData, maType, slowLength, adlList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var adl3Ema = adl3EmaList[i];
            var adl10Ema = adl10EmaList[i];

            var prevChaikinOscillator = chaikinOscillatorList.LastOrDefault();
            var chaikinOscillator = adl3Ema - adl10Ema;
            chaikinOscillatorList.AddRounded(chaikinOscillator);

            var signal = GetCompareSignal(chaikinOscillator, prevChaikinOscillator);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "ChaikinOsc", chaikinOscillatorList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = chaikinOscillatorList;
        stockData.IndicatorName = IndicatorName.ChaikinOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the ichimoku cloud.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="tenkanLength">Length of the tenkan.</param>
    /// <param name="kiiunLength">Length of the kiiun.</param>
    /// <param name="senkouLength">Length of the senkou.</param>
    /// <returns></returns>
    public static StockData CalculateIchimokuCloud(this StockData stockData, int tenkanLength = 9, int kijunLength = 26, int senkouLength = 52)
    {
        List<double> tenkanSenList = new();
        List<double> kijunSenList = new();
        List<double> senkouSpanAList = new();
        List<double> senkouSpanBList = new();
        List<Signal> signalsList = new();
        var (_, highList, lowList, _, _) = GetInputValuesList(stockData);

        var (tenkanHighList, tenkanLowList) = GetMaxAndMinValuesList(highList, lowList, tenkanLength);
        var (kijunHighList, kijunLowList) = GetMaxAndMinValuesList(highList, lowList, kijunLength);
        var (senkouHighList, senkouLowList) = GetMaxAndMinValuesList(highList, lowList, senkouLength);

        for (var i = 0; i < stockData.Count; i++)
        {
            var highest1 = tenkanHighList[i];
            var lowest1 = tenkanLowList[i];
            var highest2 = kijunHighList[i];
            var lowest2 = kijunLowList[i];
            var highest3 = senkouHighList[i];
            var lowest3 = senkouLowList[i];

            var prevTenkanSen = tenkanSenList.LastOrDefault();
            var tenkanSen = (highest1 + lowest1) / 2;
            tenkanSenList.AddRounded(tenkanSen);

            var prevKijunSen = kijunSenList.LastOrDefault();
            var kijunSen = (highest2 + lowest2) / 2;
            kijunSenList.AddRounded(kijunSen);

            var senkouSpanA = (tenkanSen + kijunSen) / 2;
            senkouSpanAList.AddRounded(senkouSpanA);

            var senkouSpanB = (highest3 + lowest3) / 2;
            senkouSpanBList.AddRounded(senkouSpanB);

            var signal = GetCompareSignal(tenkanSen - kijunSen, prevTenkanSen - prevKijunSen);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "TenkanSen", tenkanSenList },
            { "KijunSen", kijunSenList },
            { "SenkouSpanA", senkouSpanAList },
            { "SenkouSpanB", senkouSpanBList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.IchimokuCloud;

        return stockData;
    }

    /// <summary>
    /// Calculates the index of the alligator.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="inputName">Name of the input.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="iawLength">Length of the iaw.</param>
    /// <param name="iawOffset">The iaw offset.</param>
    /// <param name="teethLength">Length of the teeth.</param>
    /// <param name="teethOffset">The teeth offset.</param>
    /// <param name="lipsLength">Length of the lips.</param>
    /// <param name="lipsOffset">The lips offset.</param>
    /// <returns></returns>
    public static StockData CalculateAlligatorIndex(this StockData stockData, InputName inputName = InputName.MedianPrice, 
        MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int jawLength = 13, int jawOffset = 8, int teethLength = 8, int teethOffset = 5, 
        int lipsLength = 5, int lipsOffset = 3)
    {
        List<double> displacedJawList = new();
        List<double> displacedTeethList = new();
        List<double> displacedLipsList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var jawList = GetMovingAverageList(stockData, maType, jawLength, inputList);
        var teethList = GetMovingAverageList(stockData, maType, teethLength, inputList);
        var lipsList = GetMovingAverageList(stockData, maType, lipsLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevJaw = displacedJawList.LastOrDefault();
            var displacedJaw = i >= jawOffset ? jawList[i - jawOffset] : 0;
            displacedJawList.AddRounded(displacedJaw);

            var prevTeeth = displacedTeethList.LastOrDefault();
            var displacedTeeth = i >= teethOffset ? teethList[i - teethOffset] : 0;
            displacedTeethList.AddRounded(displacedTeeth);

            var prevLips = displacedLipsList.LastOrDefault();
            var displacedLips = i >= lipsOffset ? lipsList[i - lipsOffset] : 0;
            displacedLipsList.AddRounded(displacedLips);

            var signal = GetBullishBearishSignal(displacedLips - Math.Max(displacedJaw, displacedTeeth), prevLips - Math.Max(prevJaw, prevTeeth),
                displacedLips - Math.Min(displacedJaw, displacedTeeth), prevLips - Math.Min(prevJaw, prevTeeth));
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Lips", displacedLipsList },
            { "Teeth", displacedTeethList },
            { "Jaws", displacedJawList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.AlligatorIndex;

        return stockData;
    }

    /// <summary>
    /// Calculates the gator oscillator.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="inputName">Name of the input.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="jawLength">Length of the jaw.</param>
    /// <param name="jawOffset">The jaw offset.</param>
    /// <param name="teethLength">Length of the teeth.</param>
    /// <param name="teethOffset">The teeth offset.</param>
    /// <param name="lipsLength">Length of the lips.</param>
    /// <param name="lipsOffset">The lips offset.</param>
    /// <returns></returns>
    public static StockData CalculateGatorOscillator(this StockData stockData, InputName inputName = InputName.MedianPrice, 
        MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int jawLength = 13, int jawOffset = 8, int teethLength = 8, int teethOffset = 5, 
        int lipsLength = 5, int lipsOffset = 3)
    {
        List<double> topList = new();
        List<double> bottomList = new();
        List<Signal> signalsList = new();

        var alligatorList = CalculateAlligatorIndex(stockData, inputName, maType, jawLength, jawOffset, teethLength, teethOffset, lipsLength, lipsOffset).OutputValues;
        var jawList = alligatorList["Jaw"];
        var teethList = alligatorList["Teeth"];
        var lipsList = alligatorList["Lips"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var jaw = jawList[i];
            var teeth = teethList[i];
            var lips = lipsList[i];

            var prevTop = topList.LastOrDefault();
            var top = Math.Abs(jaw - teeth);
            topList.AddRounded(top);

            var prevBottom = bottomList.LastOrDefault();
            var bottom = -Math.Abs(teeth - lips);
            bottomList.AddRounded(bottom);

            var signal = GetCompareSignal(top - bottom, prevTop - prevBottom);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Top", topList },
            { "Bottom", bottomList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.GatorOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the ultimate oscillator.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length1">The length1.</param>
    /// <param name="length2">The length2.</param>
    /// <param name="length3">The length3.</param>
    /// <returns></returns>
    public static StockData CalculateUltimateOscillator(this StockData stockData, int length1 = 7, int length2 = 14, int length3 = 28)
    {
        List<double> uoList = new();
        List<double> bpList = new();
        List<double> trList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentClose = inputList[i];
            var prevClose = i >= 1 ? inputList[i - 1] : 0;
            var minValue = Math.Min(currentLow, prevClose);
            var maxValue = Math.Max(currentHigh, prevClose);
            var prevUo1 = i >= 1 ? uoList[i - 1] : 0;
            var prevUo2 = i >= 2 ? uoList[i - 2] : 0;

            var buyingPressure = currentClose - minValue;
            bpList.AddRounded(buyingPressure);

            var trueRange = maxValue - minValue;
            trList.AddRounded(trueRange);

            var bp7Sum = bpList.TakeLastExt(length1).Sum();
            var bp14Sum = bpList.TakeLastExt(length2).Sum();
            var bp28Sum = bpList.TakeLastExt(length3).Sum();
            var tr7Sum = trList.TakeLastExt(length1).Sum();
            var tr14Sum = trList.TakeLastExt(length2).Sum();
            var tr28Sum = trList.TakeLastExt(length3).Sum();
            var avg7 = tr7Sum != 0 ? bp7Sum / tr7Sum : 0;
            var avg14 = tr14Sum != 0 ? bp14Sum / tr14Sum : 0;
            var avg28 = tr28Sum != 0 ? bp28Sum / tr28Sum : 0;

            var ultimateOscillator = MinOrMax(100 * (((4 * avg7) + (2 * avg14) + avg28) / (4 + 2 + 1)), 100, 0);
            uoList.AddRounded(ultimateOscillator);

            var signal = GetRsiSignal(ultimateOscillator - prevUo1, prevUo1 - prevUo2, ultimateOscillator, prevUo1, 70, 30);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Uo", uoList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = uoList;
        stockData.IndicatorName = IndicatorName.UltimateOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the vortex indicator.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateVortexIndicator(this StockData stockData, int length = 14)
    {
        List<double> vmPlusList = new();
        List<double> trueRangeList = new();
        List<double> vmMinusList = new();
        List<double> viPlus14List = new();
        List<double> viMinus14List = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var prevClose = i >= 1 ? inputList[i - 1] : 0;
            var prevLow = i >= 1 ? lowList[i - 1] : 0;
            var prevHigh = i >= 1 ? highList[i - 1] : 0;

            var vmPlus = Math.Abs(currentHigh - prevLow);
            vmPlusList.AddRounded(vmPlus);

            var vmMinus = Math.Abs(currentLow - prevHigh);
            vmMinusList.AddRounded(vmMinus);

            var trueRange = CalculateTrueRange(currentHigh, currentLow, prevClose);
            trueRangeList.AddRounded(trueRange);

            var vmPlus14 = vmPlusList.TakeLastExt(length).Sum();
            var vmMinus14 = vmMinusList.TakeLastExt(length).Sum();
            var trueRange14 = trueRangeList.TakeLastExt(length).Sum();

            var prevViPlus14 = viPlus14List.LastOrDefault();
            var viPlus14 = trueRange14 != 0 ? vmPlus14 / trueRange14 : 0;
            viPlus14List.AddRounded(viPlus14);

            var prevViMinus14 = viMinus14List.LastOrDefault();
            var viMinus14 = trueRange14 != 0 ? vmMinus14 / trueRange14 : 0;
            viMinus14List.AddRounded(viMinus14);

            var signal = GetCompareSignal(viPlus14 - viMinus14, prevViPlus14 - prevViMinus14);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "ViPlus", viPlus14List },
            { "ViMinus", viMinus14List }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.VortexIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Trix Indicator
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <param name="signalLength">Length of the signal.</param>
    /// <returns></returns>
    public static StockData CalculateTrix(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length = 15, int signalLength = 9)
    {
        List<double> trixList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var ema1List = GetMovingAverageList(stockData, maType, length, inputList);
        var ema2List = GetMovingAverageList(stockData, maType, length, ema1List);
        var ema3List = GetMovingAverageList(stockData, maType, length, ema2List);

        for (var i = 0; i < stockData.Count; i++)
        {
            var ema3 = ema3List[i];
            var prevEma3 = i >= 1 ? ema3List[i - 1] : 0;

            var trix = CalculatePercentChange(ema3, prevEma3);
            trixList.AddRounded(trix);
        }

        var trixSignalList = GetMovingAverageList(stockData, maType, signalLength, trixList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var trix = trixList[i];
            var trixSignal = trixSignalList[i];
            var prevTrix = i >= 1 ? trixList[i - 1] : 0;
            var prevTrixSignal = i >= 1 ? trixSignalList[i - 1] : 0;

            var signal = GetCompareSignal(trix - trixSignal, prevTrix - prevTrixSignal);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Trix", trixList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = trixList;
        stockData.IndicatorName = IndicatorName.Trix;

        return stockData;
    }

    /// <summary>
    /// Calculates the williams r.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateWilliamsR(this StockData stockData, int length = 14)
    {
        List<double> williamsRList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentClose = inputList[i];
            var highestHigh = highestList[i];
            var lowestLow = lowestList[i];
            var prevWilliamsR1 = i >= 1 ? williamsRList[i - 1] : 0;
            var prevWilliamsR2 = i >= 2 ? williamsRList[i - 2] : 0;

            var williamsR = highestHigh - lowestLow != 0 ? -100 * (highestHigh - currentClose) / (highestHigh - lowestLow) : -100;
            williamsRList.AddRounded(williamsR);

            var signal = GetRsiSignal(williamsR - prevWilliamsR1, prevWilliamsR1 - prevWilliamsR2, williamsR, prevWilliamsR1, -20, -80);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Williams%R", williamsRList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = williamsRList;
        stockData.IndicatorName = IndicatorName.WilliamsR;

        return stockData;
    }

    /// <summary>
    /// Calculates the True Strength Index
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length1">The length1.</param>
    /// <param name="length2">The length2.</param>
    /// <param name="signalLength">Length of the signal.</param>
    /// <returns></returns>
    public static StockData CalculateTrueStrengthIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length1 = 25, int length2 = 13, int signalLength = 7)
    {
        List<double> pcList = new();
        List<double> absPCList = new();
        List<double> tsiList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var pc = MinPastValues(i, 1, currentValue - prevValue);
            pcList.AddRounded(pc);

            var absPC = Math.Abs(pc);
            absPCList.AddRounded(absPC);
        }

        var pcSmooth1List = GetMovingAverageList(stockData, maType, length1, pcList);
        var pcSmooth2List = GetMovingAverageList(stockData, maType, length2, pcSmooth1List);
        var absPCSmooth1List = GetMovingAverageList(stockData, maType, length1, absPCList);
        var absPCSmooth2List = GetMovingAverageList(stockData, maType, length2, absPCSmooth1List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var absSmooth2PC = absPCSmooth2List[i];
            var smooth2PC = pcSmooth2List[i];

            var tsi = absSmooth2PC != 0 ? MinOrMax(100 * smooth2PC / absSmooth2PC, 100, -100) : 0;
            tsiList.AddRounded(tsi);
        }

        var tsiSignalList = GetMovingAverageList(stockData, maType, signalLength, tsiList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var tsi = tsiList[i];
            var tsiSignal = tsiSignalList[i];
            var prevTsi = i >= 1 ? tsiList[i - 1] : 0;
            var prevTsiSignal = i >= 1 ? tsiSignalList[i - 1] : 0;

            var signal = GetRsiSignal(tsi - tsiSignal, prevTsi - prevTsiSignal, tsi, prevTsi, 25, -25);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Tsi", tsiList },
            { "Signal", tsiSignalList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = tsiList;
        stockData.IndicatorName = IndicatorName.TrueStrengthIndex;

        return stockData;
    }

    /// <summary>
    /// Calculates the index of the elder ray.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateElderRayIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length = 13)
    {
        List<double> bullPowerList = new();
        List<double> bearPowerList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        var emaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentEma = emaList[i];

            var prevBullPower = bullPowerList.LastOrDefault();
            var bullPower = currentHigh - currentEma;
            bullPowerList.AddRounded(bullPower);

            var prevBearPower = bearPowerList.LastOrDefault();
            var bearPower = currentLow - currentEma;
            bearPowerList.AddRounded(bearPower);

            var signal = GetCompareSignal(bullPower - bearPower, prevBullPower - prevBearPower);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "BullPower", bullPowerList },
            { "BearPower", bearPowerList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.ElderRayIndex;

        return stockData;
    }

    /// <summary>
    /// Calculates the Absolute Price Oscillator
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="fastLength">Length of the fast.</param>
    /// <param name="slowLength">Length of the slow.</param>
    /// <returns></returns>
    public static StockData CalculateAbsolutePriceOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int fastLength = 10, int slowLength = 20)
    {
        List<double> apoList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var fastEmaList = GetMovingAverageList(stockData, maType, fastLength, inputList);
        var slowEmaList = GetMovingAverageList(stockData, maType, slowLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var fastEma = fastEmaList[i];
            var slowEma = slowEmaList[i];

            var prevApo = apoList.LastOrDefault();
            var apo = fastEma - slowEma;
            apoList.AddRounded(apo);

            var signal = GetCompareSignal(apo, prevApo);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Apo", apoList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = apoList;
        stockData.IndicatorName = IndicatorName.AbsolutePriceOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the aroon oscillator.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateAroonOscillator(this StockData stockData, int length = 25)
    {
        List<double> aroonOscillatorList = new();
        List<double> tempList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(inputList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentPrice = inputList[i];
            tempList.AddRounded(currentPrice);

            var maxPrice = highestList[i];
            var maxIndex = tempList.LastIndexOf(maxPrice);
            var minPrice = lowestList[i];
            var minIndex = tempList.LastIndexOf(minPrice);
            var daysSinceMax = i - maxIndex;
            var daysSinceMin = i - minIndex;
            var aroonUp = (double)(length - daysSinceMax) / length * 100;
            var aroonDown = (double)(length - daysSinceMin) / length * 100;

            var prevAroonOscillator = aroonOscillatorList.LastOrDefault();
            var aroonOscillator = aroonUp - aroonDown;
            aroonOscillatorList.AddRounded(aroonOscillator);

            var signal = GetCompareSignal(aroonOscillator, prevAroonOscillator);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Aroon", aroonOscillatorList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = aroonOscillatorList;
        stockData.IndicatorName = IndicatorName.AroonOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the index of the absolute strength.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <param name="maLength">Length of the ma.</param>
    /// <param name="signalLength">Length of the signal.</param>
    /// <returns></returns>
    public static StockData CalculateAbsoluteStrengthIndex(this StockData stockData, int length = 10, int maLength = 21, int signalLength = 34)
    {
        List<double> AList = new();
        List<double> MList = new();
        List<double> DList = new();
        List<double> mtList = new();
        List<double> utList = new();
        List<double> abssiEmaList = new();
        List<double> dList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var alp = (double)2 / (signalLength + 1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevA = AList.LastOrDefault();
            var A = currentValue > prevValue && prevValue != 0 ? prevA + ((currentValue / prevValue) - 1) : prevA;
            AList.AddRounded(A);

            var prevM = MList.LastOrDefault();
            var M = currentValue == prevValue ? prevM + ((double)1 / length) : prevM;
            MList.AddRounded(M);

            var prevD = DList.LastOrDefault();
            var D = currentValue < prevValue && currentValue != 0 ? prevD + ((prevValue / currentValue) - 1) : prevD;
            DList.AddRounded(D);

            var abssi = (D + M) / 2 != 0 ? 1 - (1 / (1 + ((A + M) / 2 / ((D + M) / 2)))) : 1;
            var abssiEma = CalculateEMA(abssi, abssiEmaList.LastOrDefault(), maLength);
            abssiEmaList.AddRounded(abssiEma);

            var abssio = abssi - abssiEma;
            var prevMt = mtList.LastOrDefault();
            var mt = (alp * abssio) + ((1 - alp) * prevMt);
            mtList.AddRounded(mt);

            var prevUt = utList.LastOrDefault();
            var ut = (alp * mt) + ((1 - alp) * prevUt);
            utList.AddRounded(ut);

            var s = (2 - alp) * (mt - ut) / (1 - alp);
            var prevd = dList.LastOrDefault();
            var d = abssio - s;
            dList.AddRounded(d);

            var signal = GetCompareSignal(d, prevd);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Asi", dList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = dList;
        stockData.IndicatorName = IndicatorName.AbsoluteStrengthIndex;

        return stockData;
    }

    /// <summary>
    /// Calculates the Accumulative Swing Index
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateAccumulativeSwingIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14)
    {
        List<double> accumulativeSwingIndexList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, openList, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentClose = inputList[i];
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentOpen = openList[i];
            var prevClose = i >= 1 ? inputList[i - 1] : 0;
            var prevOpen = i >= 1 ? openList[i - 1] : 0;
            var prevHigh = i >= 1 ? highList[i - 1] : 0;
            var prevLow = i >= 1 ? lowList[i - 1] : 0;
            var prevHighCurrentClose = prevHigh - currentClose;
            var prevLowCurrentClose = prevLow - currentClose;
            var prevClosePrevOpen = prevClose - prevOpen;
            var currentHighPrevClose = currentHigh - prevClose;
            var currentLowPrevClose = currentLow - prevClose;
            var t = currentHigh - currentLow;
            var k = Math.Max(Math.Abs(prevHighCurrentClose), Math.Abs(prevLowCurrentClose));
            var r = currentHighPrevClose > Math.Max(currentLowPrevClose, t) ? currentHighPrevClose - (0.5 * currentLowPrevClose) + (0.25 * prevClosePrevOpen) :
                currentLowPrevClose > Math.Max(currentHighPrevClose, t) ? currentLowPrevClose - (0.5 * currentHighPrevClose) + (0.25 * prevClosePrevOpen) :
                t > Math.Max(currentHighPrevClose, currentLowPrevClose) ? t + (0.25 * prevClosePrevOpen) : 0;
            var swingIndex = r != 0 && t != 0 ? 50 * ((prevClose - currentClose + (0.5 * prevClosePrevOpen) +
                                                       (0.25 * (currentClose - currentOpen))) / r) * (k / t) : 0;

            var prevSwingIndex = accumulativeSwingIndexList.LastOrDefault();
            var accumulativeSwingIndex = prevSwingIndex + swingIndex;
            accumulativeSwingIndexList.AddRounded(accumulativeSwingIndex);
        }

        var asiOscillatorList = GetMovingAverageList(stockData, maType, length, accumulativeSwingIndexList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var asi = accumulativeSwingIndexList[i];
            var prevAsi = i >= 1 ? accumulativeSwingIndexList[i - 1] : 0;

            var signal = GetCompareSignal(asi, prevAsi);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Asi", accumulativeSwingIndexList },
            { "Signal", asiOscillatorList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = accumulativeSwingIndexList;
        stockData.IndicatorName = IndicatorName.AccumulativeSwingIndex;

        return stockData;
    }

    /// <summary>
    /// Calculates the Adaptive Ergodic Candlestick Oscillator
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="smoothLength">Length of the smooth.</param>
    /// <param name="stochLength">Length of the stoch.</param>
    /// <param name="signalLength">Length of the signal.</param>
    /// <returns></returns>
    public static StockData CalculateAdaptiveErgodicCandlestickOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int smoothLength = 5, int stochLength = 14, int signalLength = 9)
    {
        List<double> came1List = new();
        List<double> came2List = new();
        List<double> came11List = new();
        List<double> came22List = new();
        List<double> ecoList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, openList, _) = GetInputValuesList(stockData);

        var mep = (double)2 / (smoothLength + 1);
        double ce = (stochLength + smoothLength) * 2;

        var stochList = CalculateStochasticOscillator(stockData, maType, length: stochLength).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var stoch = stochList[i];
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentOpen = openList[i];
            var currentClose = inputList[i];
            var vrb = Math.Abs(stoch - 50) / 50;

            var prevCame1 = came1List.LastOrDefault();
            var came1 = i < ce ? currentClose - currentOpen : prevCame1 + (mep * vrb * (currentClose - currentOpen - prevCame1));
            came1List.AddRounded(came1);

            var prevCame2 = came2List.LastOrDefault();
            var came2 = i < ce ? currentHigh - currentLow : prevCame2 + (mep * vrb * (currentHigh - currentLow - prevCame2));
            came2List.AddRounded(came2);

            var prevCame11 = came11List.LastOrDefault();
            var came11 = i < ce ? came1 : prevCame11 + (mep * vrb * (came1 - prevCame11));
            came11List.AddRounded(came11);

            var prevCame22 = came22List.LastOrDefault();
            var came22 = i < ce ? came2 : prevCame22 + (mep * vrb * (came2 - prevCame22));
            came22List.AddRounded(came22);

            var eco = came22 != 0 ? came11 / came22 * 100 : 0;
            ecoList.AddRounded(eco);
        }

        var seList = GetMovingAverageList(stockData, maType, signalLength, ecoList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var eco = ecoList[i];
            var se = seList[i];
            var prevEco = i >= 1 ? ecoList[i - 1] : 0;
            var prevSe = i >= 1 ? seList[i - 1] : 0;

            var signal = GetCompareSignal(eco - se, prevEco - prevSe);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Eco", ecoList },
            { "Signal", seList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = ecoList;
        stockData.IndicatorName = IndicatorName.AdaptiveErgodicCandlestickOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Absolute Strength MTF Indicator
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <param name="smoothLength">Length of the smooth.</param>
    /// <returns></returns>
    public static StockData CalculateAbsoluteStrengthMTFIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 50, int smoothLength = 25)
    {
        List<double> prevValuesList = new();
        List<double> bulls0List = new();
        List<double> bears0List = new();
        List<double> bulls1List = new();
        List<double> bears1List = new();
        List<double> bulls2List = new();
        List<double> bears2List = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            prevValuesList.AddRounded(prevValue);
        }

        var price1List = GetMovingAverageList(stockData, maType, length, inputList);
        var price2List = GetMovingAverageList(stockData, maType, length, prevValuesList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var price1 = price1List[i];
            var price2 = price2List[i];
            var highest = highestList[i];
            var lowest = lowestList[i];
            var high = highList[i];
            var low = lowList[i];
            var prevHigh = i >= 1 ? highList[i - 1] : 0;
            var prevLow = i >= 1 ? lowList[i - 1] : 0;

            var bulls0 = 0.5 * (Math.Abs(price1 - price2) + (price1 - price2));
            bulls0List.AddRounded(bulls0);

            var bears0 = 0.5 * (Math.Abs(price1 - price2) - (price1 - price2));
            bears0List.AddRounded(bears0);

            var bulls1 = price1 - lowest;
            bulls1List.AddRounded(bulls1);

            var bears1 = highest - price1;
            bears1List.AddRounded(bears1);

            var bulls2 = 0.5 * (Math.Abs(high - prevHigh) + (high - prevHigh));
            bulls2List.AddRounded(bulls2);

            var bears2 = 0.5 * (Math.Abs(prevLow - low) + (prevLow - low));
            bears2List.AddRounded(bears2);
        }

        var smthBulls0List = GetMovingAverageList(stockData, maType, smoothLength, bulls0List);
        var smthBears0List = GetMovingAverageList(stockData, maType, smoothLength, bears0List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var bulls = smthBulls0List[i];
            var bears = smthBears0List[i];
            var prevBulls = i >= 1 ? smthBulls0List[i - 1] : 0;
            var prevBears = i >= 1 ? smthBears0List[i - 1] : 0;

            var signal = GetCompareSignal(bulls - bears, prevBulls - prevBears);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Bulls", smthBulls0List },
            { "Bears", smthBears0List }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.AbsoluteStrengthMTFIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Japanese Correlation Coefficient
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateJapaneseCorrelationCoefficient(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 50)
    {
        List<double> joList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        var length1 = MinOrMax((int)Math.Ceiling((double)length / 2));

        var hList = GetMovingAverageList(stockData, maType, length1, highList);
        var lList = GetMovingAverageList(stockData, maType, length1, lowList);
        var cList = GetMovingAverageList(stockData, maType, length1, inputList);
        var highestList = GetMaxAndMinValuesList(hList, length1).Item1;
        var lowestList = GetMaxAndMinValuesList(lList, length1).Item2;

        for (var i = 0; i < stockData.Count; i++)
        {
            var c = cList[i];
            var prevC = i >= length ? cList[i - length] : 0;
            var highest = highestList[i];
            var lowest = lowestList[i];
            var prevJo1 = i >= 1 ? joList[i - 1] : 0;
            var prevJo2 = i >= 2 ? joList[i - 2] : 0;
            var cChg = c - prevC;

            var jo = highest - lowest != 0 ? cChg / (highest - lowest) : 0;
            joList.AddRounded(jo);

            var signal = GetCompareSignal(jo - prevJo1, prevJo1 - prevJo2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Jo", joList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = joList;
        stockData.IndicatorName = IndicatorName.JapaneseCorrelationCoefficient;

        return stockData;
    }

    /// <summary>
    /// Calculates the Jma Rsx Clone
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateJmaRsxClone(this StockData stockData, int length = 14)
    {
        List<double> rsxList = new();
        List<double> f8List = new();
        List<double> f28List = new();
        List<double> f30List = new();
        List<double> f38List = new();
        List<double> f40List = new();
        List<double> f48List = new();
        List<double> f50List = new();
        List<double> f58List = new();
        List<double> f60List = new();
        List<double> f68List = new();
        List<double> f70List = new();
        List<double> f78List = new();
        List<double> f80List = new();
        List<double> f88List = new();
        List<double> f90_List = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var f18 = (double)3 / (length + 2);
        var f20 = 1 - f18;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevRsx1 = i >= 1 ? rsxList[i - 1] : 0;
            var prevRsx2 = i >= 2 ? rsxList[i - 2] : 0;

            var prevF8 = f8List.LastOrDefault();
            var f8 = 100 * currentValue;
            f8List.AddRounded(f8);

            var f10 = prevF8;
            var v8 = f8 - f10;

            var prevF28 = f28List.LastOrDefault();
            var f28 = (f20 * prevF28) + (f18 * v8);
            f28List.AddRounded(f28);

            var prevF30 = f30List.LastOrDefault();
            var f30 = (f18 * f28) + (f20 * prevF30);
            f30List.AddRounded(f30);

            var vC = (f28 * 1.5) - (f30 * 0.5);
            var prevF38 = f38List.LastOrDefault();
            var f38 = (f20 * prevF38) + (f18 * vC);
            f38List.AddRounded(f38);

            var prevF40 = f40List.LastOrDefault();
            var f40 = (f18 * f38) + (f20 * prevF40);
            f40List.AddRounded(f40);

            var v10 = (f38 * 1.5) - (f40 * 0.5);
            var prevF48 = f48List.LastOrDefault();
            var f48 = (f20 * prevF48) + (f18 * v10);
            f48List.AddRounded(f48);

            var prevF50 = f50List.LastOrDefault();
            var f50 = (f18 * f48) + (f20 * prevF50);
            f50List.AddRounded(f50);

            var v14 = (f48 * 1.5) - (f50 * 0.5);
            var prevF58 = f58List.LastOrDefault();
            var f58 = (f20 * prevF58) + (f18 * Math.Abs(v8));
            f58List.AddRounded(f58);

            var prevF60 = f60List.LastOrDefault();
            var f60 = (f18 * f58) + (f20 * prevF60);
            f60List.AddRounded(f60);

            var v18 = (f58 * 1.5) - (f60 * 0.5);
            var prevF68 = f68List.LastOrDefault();
            var f68 = (f20 * prevF68) + (f18 * v18);
            f68List.AddRounded(f68);

            var prevF70 = f70List.LastOrDefault();
            var f70 = (f18 * f68) + (f20 * prevF70);
            f70List.AddRounded(f70);

            var v1C = (f68 * 1.5) - (f70 * 0.5);
            var prevF78 = f78List.LastOrDefault();
            var f78 = (f20 * prevF78) + (f18 * v1C);
            f78List.AddRounded(f78);

            var prevF80 = f80List.LastOrDefault();
            var f80 = (f18 * f78) + (f20 * prevF80);
            f80List.AddRounded(f80);

            var v20 = (f78 * 1.5) - (f80 * 0.5);
            var prevF88 = f88List.LastOrDefault();
            var prevF90_ = f90_List.LastOrDefault();
            var f90_ = prevF90_ == 0 ? 1 : prevF88 <= prevF90_ ? prevF88 + 1 : prevF90_ + 1;
            f90_List.AddRounded(f90_);

            double f88 = prevF90_ == 0 && length - 1 >= 5 ? length - 1 : 5;
            double f0 = f88 >= f90_ && f8 != f10 ? 1 : 0;
            var f90 = f88 == f90_ && f0 == 0 ? 0 : f90_;
            var v4_ = f88 < f90 && v20 > 0 ? MinOrMax(((v14 / v20) + 1) * 50, 100, 0) : 50;
            var rsx = v4_ > 100 ? 100 : v4_ < 0 ? 0 : v4_;
            rsxList.AddRounded(rsx);

            var signal = GetRsiSignal(rsx - prevRsx1, prevRsx1 - prevRsx2, rsx, prevRsx1, 70, 30);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Rsx", rsxList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = rsxList;
        stockData.IndicatorName = IndicatorName.JmaRsxClone;

        return stockData;
    }

    /// <summary>
    /// Calculates the Jrc Fractal Dimension
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateJrcFractalDimension(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length1 = 20, int length2 = 5, int smoothLength = 5)
    {
        List<double> smallSumList = new();
        List<double> smallRangeList = new();
        List<double> fdList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        var wind1 = MinOrMax((length2 - 1) * length1);
        var wind2 = MinOrMax(length2 * length1);
        var nLog = Math.Log(length2);

        var (highest1List, lowest1List) = GetMaxAndMinValuesList(highList, lowList, length1);
        var (highest2List, lowest2List) = GetMaxAndMinValuesList(highList, lowList, wind2);

        for (var i = 0; i < stockData.Count; i++)
        {
            var highest1 = highest1List[i];
            var lowest1 = lowest1List[i];
            var prevValue1 = i >= length1 ? inputList[i - length1] : 0;
            var highest2 = highest2List[i];
            var lowest2 = lowest2List[i];
            var prevValue2 = i >= wind2 ? inputList[i - wind2] : 0;
            var bigRange = Math.Max(prevValue2, highest2) - Math.Min(prevValue2, lowest2);

            var prevSmallRange = i >= wind1 ? smallRangeList[i - wind1] : 0;
            var smallRange = Math.Max(prevValue1, highest1) - Math.Min(prevValue1, lowest1);
            smallRangeList.AddRounded(smallRange);

            var prevSmallSum = i >= 1 ? smallSumList.LastOrDefault() : smallRange;
            var smallSum = prevSmallSum + smallRange - prevSmallRange;
            smallSumList.AddRounded(smallSum);

            var value1 = wind1 != 0 ? smallSum / wind1 : 0;
            var value2 = value1 != 0 ? bigRange / value1 : 0;
            var temp = value2 > 0 ? Math.Log(value2) : 0;

            var fd = nLog != 0 ? 2 - (temp / nLog) : 0;
            fdList.AddRounded(fd);
        }

        var jrcfdList = GetMovingAverageList(stockData, maType, smoothLength, fdList);
        var jrcfdSignalList = GetMovingAverageList(stockData, maType, smoothLength, jrcfdList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var jrcfd = jrcfdList[i];
            var jrcfdSignal = jrcfdSignalList[i];
            var prevJrcfd = i >= 1 ? jrcfdList[i - 1] : 0;
            var prevJrcfdSignal = i >= 1 ? jrcfdSignalList[i - 1] : 0;

            var signal = GetCompareSignal(jrcfd - jrcfdSignal, prevJrcfd - prevJrcfdSignal, true);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Jrcfd", jrcfdList },
            { "Signal", jrcfdSignalList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = jrcfdList;
        stockData.IndicatorName = IndicatorName.JrcFractalDimension;

        return stockData;
    }

    /// <summary>
    /// Calculates the Zweig Market Breadth Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateZweigMarketBreadthIndicator(this StockData stockData,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 10)
    {
        List<double> advDiffList = new();
        List<double> advancesList = new();
        List<double> declinesList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            double advance = currentValue > prevValue ? 1 : 0;
            advancesList.AddRounded(advance);

            double decline = currentValue < prevValue ? 1 : 0;
            declinesList.AddRounded(decline);

            var advSum = advancesList.TakeLastExt(length).Sum();
            var decSum = declinesList.TakeLastExt(length).Sum();

            var advDiff = advSum + decSum != 0 ? advSum / (advSum + decSum) : 0;
            advDiffList.AddRounded(advDiff);
        }

        var zmbtiList = GetMovingAverageList(stockData, maType, length, advDiffList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var prevZmbti1 = i >= 1 ? zmbtiList[i - 1] : 0;
            var prevZmbti2 = i >= 2 ? zmbtiList[i - 2] : 0;
            var zmbti = zmbtiList[i];

            var signal = GetRsiSignal(zmbti - prevZmbti1, prevZmbti1 - prevZmbti2, zmbti, prevZmbti1, 0.615, 0.4);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Zmbti", zmbtiList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = zmbtiList;
        stockData.IndicatorName = IndicatorName.ZweigMarketBreadthIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Z Distance From Vwap Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateZDistanceFromVwapIndicator(this StockData stockData,
        MovingAvgType maType = MovingAvgType.VolumeWeightedAveragePrice, int length = 20)
    {
        List<double> zscoreList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var vwapList = GetMovingAverageList(stockData, maType, length, inputList);
        stockData.CustomValuesList = vwapList;
        var vwapSdList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevZScore1 = i >= 1 ? zscoreList[i - 1] : 0;
            var prevZScore2 = i >= 2 ? zscoreList[i - 2] : 0;
            var mean = vwapList[i];
            var vwapsd = vwapSdList[i];

            var zscore = vwapsd != 0 ? (currentValue - mean) / vwapsd : 0;
            zscoreList.AddRounded(zscore);

            var signal = GetRsiSignal(zscore - prevZScore1, prevZScore1 - prevZScore2, zscore, prevZScore1, 2, -2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Zscore", zscoreList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = zscoreList;
        stockData.IndicatorName = IndicatorName.ZDistanceFromVwap;

        return stockData;
    }

    /// <summary>
    /// Calculates the Z Score
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="matype"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateZScore(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14)
    {
        List<double> zScorePopulationList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);
        var stdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var sma = smaList[i];
            var dev = currentValue - sma;
            var stdDevPopulation = stdDevList[i];

            var prevZScorePopulation = zScorePopulationList.LastOrDefault();
            var zScorePopulation = stdDevPopulation != 0 ? dev / stdDevPopulation : 0;
            zScorePopulationList.AddRounded(zScorePopulation);

            var signal = GetCompareSignal(zScorePopulation, prevZScorePopulation);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Zscore", zScorePopulationList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = zScorePopulationList;
        stockData.IndicatorName = IndicatorName.ZScore;

        return stockData;
    }

    /// <summary>
    /// Calculates the Zero Lag Smoothed Cycle
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateZeroLagSmoothedCycle(this StockData stockData, int length = 100)
    {
        List<double> ax1List = new();
        List<double> lx1List = new();
        List<double> ax2List = new();
        List<double> lx2List = new();
        List<double> ax3List = new();
        List<double> lcoList = new();
        List<double> filterList = new();
        List<double> lcoSma1List = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var length1 = MinOrMax((int)Math.Ceiling((double)length / 2));

        var linregList = CalculateLinearRegression(stockData, length).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var linreg = linregList[i];

            var ax1 = currentValue - linreg;
            ax1List.AddRounded(ax1);
        }

        stockData.CustomValuesList = ax1List;
        var ax1LinregList = CalculateLinearRegression(stockData, length).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var ax1 = ax1List[i];
            var ax1Linreg = ax1LinregList[i];

            var lx1 = ax1 + (ax1 - ax1Linreg);
            lx1List.AddRounded(lx1);
        }

        stockData.CustomValuesList = lx1List;
        var lx1LinregList = CalculateLinearRegression(stockData, length).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var lx1 = lx1List[i];
            var lx1Linreg = lx1LinregList[i];

            var ax2 = lx1 - lx1Linreg;
            ax2List.AddRounded(ax2);
        }

        stockData.CustomValuesList = ax2List;
        var ax2LinregList = CalculateLinearRegression(stockData, length).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var ax2 = ax2List[i];
            var ax2Linreg = ax2LinregList[i];

            var lx2 = ax2 + (ax2 - ax2Linreg);
            lx2List.AddRounded(lx2);
        }

        stockData.CustomValuesList = lx2List;
        var lx2LinregList = CalculateLinearRegression(stockData, length).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var lx2 = lx2List[i];
            var lx2Linreg = lx2LinregList[i];

            var ax3 = lx2 - lx2Linreg;
            ax3List.AddRounded(ax3);
        }

        stockData.CustomValuesList = ax3List;
        var ax3LinregList = CalculateLinearRegression(stockData, length).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var ax3 = ax3List[i];
            var ax3Linreg = ax3LinregList[i];

            var prevLco = lcoList.LastOrDefault();
            var lco = ax3 + (ax3 - ax3Linreg);
            lcoList.AddRounded(lco);

            var lcoSma1 = lcoList.TakeLastExt(length1).Average();
            lcoSma1List.AddRounded(lcoSma1);

            var lcoSma2 = lcoSma1List.TakeLastExt(length1).Average();
            var prevFilter = filterList.LastOrDefault();
            var filter = -lcoSma2 * 2;
            filterList.AddRounded(filter);

            var signal = GetCompareSignal(lco - filter, prevLco - prevFilter);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Lco", lcoList },
            { "Filter", filterList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = filterList;
        stockData.IndicatorName = IndicatorName.ZeroLagSmoothedCycle;

        return stockData;
    }

    /// <summary>
    /// Calculates the Bayesian Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="stdDevMult"></param>
    /// <param name="lowerThreshold"></param>
    /// <returns></returns>
    public static StockData CalculateBayesianOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 20, double stdDevMult = 2.5, double lowerThreshold = 15)
    {
        List<double> probBbUpperUpSeqList = new();
        List<double> probBbUpperDownSeqList = new();
        List<double> probBbBasisUpSeqList = new();
        List<double> probBbBasisUpList = new();
        List<double> probBbBasisDownSeqList = new();
        List<double> probBbBasisDownList = new();
        List<double> sigmaProbsDownList = new();
        List<double> sigmaProbsUpList = new();
        List<double> probPrimeList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var bbList = CalculateBollingerBands(stockData, maType, length, stdDevMult);
        var upperBbList = bbList.OutputValues["UpperBand"];
        var basisList = bbList.OutputValues["MiddleBand"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var upperBb = upperBbList[i];
            var basis = basisList[i];

            double probBbUpperUpSeq = currentValue > upperBb ? 1 : 0;
            probBbUpperUpSeqList.AddRounded(probBbUpperUpSeq);

            var probBbUpperUp = probBbUpperUpSeqList.TakeLastExt(length).Average();

            double probBbUpperDownSeq = currentValue < upperBb ? 1 : 0;
            probBbUpperDownSeqList.AddRounded(probBbUpperDownSeq);

            var probBbUpperDown = probBbUpperDownSeqList.TakeLastExt(length).Average();
            var probUpBbUpper = probBbUpperUp + probBbUpperDown != 0 ? probBbUpperUp / (probBbUpperUp + probBbUpperDown) : 0;
            var probDownBbUpper = probBbUpperUp + probBbUpperDown != 0 ? probBbUpperDown / (probBbUpperUp + probBbUpperDown) : 0;

            double probBbBasisUpSeq = currentValue > basis ? 1 : 0;
            probBbBasisUpSeqList.AddRounded(probBbBasisUpSeq);

            var probBbBasisUp = probBbBasisUpSeqList.TakeLastExt(length).Average();
            probBbBasisUpList.AddRounded(probBbBasisUp);

            double probBbBasisDownSeq = currentValue < basis ? 1 : 0;
            probBbBasisDownSeqList.AddRounded(probBbBasisDownSeq);

            var probBbBasisDown = probBbBasisDownSeqList.TakeLastExt(length).Average();
            probBbBasisDownList.AddRounded(probBbBasisDown);

            var probUpBbBasis = probBbBasisUp + probBbBasisDown != 0 ? probBbBasisUp / (probBbBasisUp + probBbBasisDown) : 0;
            var probDownBbBasis = probBbBasisUp + probBbBasisDown != 0 ? probBbBasisDown / (probBbBasisUp + probBbBasisDown) : 0;

            var prevSigmaProbsDown = sigmaProbsDownList.LastOrDefault();
            var sigmaProbsDown = probUpBbUpper != 0 && probUpBbBasis != 0 ? ((probUpBbUpper * probUpBbBasis) / (probUpBbUpper * probUpBbBasis)) +
                                                                            ((1 - probUpBbUpper) * (1 - probUpBbBasis)) : 0;
            sigmaProbsDownList.AddRounded(sigmaProbsDown);

            var prevSigmaProbsUp = sigmaProbsUpList.LastOrDefault();
            var sigmaProbsUp = probDownBbUpper != 0 && probDownBbBasis != 0 ? ((probDownBbUpper * probDownBbBasis) / (probDownBbUpper * probDownBbBasis)) +
                                                                              ((1 - probDownBbUpper) * (1 - probDownBbBasis)) : 0;
            sigmaProbsUpList.AddRounded(sigmaProbsUp);

            var prevProbPrime = probPrimeList.LastOrDefault();
            var probPrime = sigmaProbsDown != 0 && sigmaProbsUp != 0 ? ((sigmaProbsDown * sigmaProbsUp) / (sigmaProbsDown * sigmaProbsUp)) +
                                                                       ((1 - sigmaProbsDown) * (1 - sigmaProbsUp)) : 0;
            probPrimeList.AddRounded(probPrime);

            var longUsingProbPrime = probPrime > lowerThreshold / 100 && prevProbPrime == 0;
            var longUsingSigmaProbsUp = sigmaProbsUp < 1 && prevSigmaProbsUp == 1;
            var shortUsingProbPrime = probPrime == 0 && prevProbPrime > lowerThreshold / 100;
            var shortUsingSigmaProbsDown = sigmaProbsDown < 1 && prevSigmaProbsDown == 1;

            var signal = GetConditionSignal(longUsingProbPrime || longUsingSigmaProbsUp, shortUsingProbPrime || shortUsingSigmaProbsDown);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "SigmaProbsDown", sigmaProbsDownList },
            { "SigmaProbsUp", sigmaProbsUpList },
            { "ProbPrime", probPrimeList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.BayesianOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Bear Power Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateBearPowerIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14)
    {
        List<double> bpiList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, openList, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var close = inputList[i];
            var prevClose = i >= 1 ? inputList[i - 1] : 0;
            var open = openList[i];
            var high = highList[i];
            var low = lowList[i];

            var bpi = close < open ? high - low : prevClose > open ? Math.Max(close - open, high - low) :
                close > open ? Math.Max(open - low, high - close) : prevClose > open ? Math.Max(prevClose - low, high - close) :
                high - close > close - low ? high - low : prevClose > open ? Math.Max(prevClose - open, high - low) :
                high - close < close - low ? open - low : close > open ? Math.Max(close - low, high - close) :
                close > open ? Math.Max(prevClose - open, high - close) : prevClose < open ? Math.Max(open - low, high - close) : high - low;
            bpiList.AddRounded(bpi);
        }

        var bpiEmaList = GetMovingAverageList(stockData, maType, length, bpiList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var bpi = bpiList[i];
            var bpiEma = bpiEmaList[i];
            var prevBpi = i >= 1 ? bpiList[i - 1] : 0;
            var prevBpiEma = i >= 1 ? bpiEmaList[i - 1] : 0;

            var signal = GetCompareSignal(bpi - bpiEma, prevBpi - prevBpiEma, true);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "BearPower", bpiList },
            { "Signal", bpiEmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = bpiList;
        stockData.IndicatorName = IndicatorName.BearPowerIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Bull Power Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateBullPowerIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14)
    {
        List<double> bpiList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, openList, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var close = inputList[i];
            var prevClose = i >= 1 ? inputList[i - 1] : 0;
            var open = openList[i];
            var high = highList[i];
            var low = lowList[i];

            var bpi = close < open ? Math.Max(high - open, close - low) : prevClose < open ? Math.Max(high - prevClose, close - low) :
                close > open ? Math.Max(open - prevClose, high - low) : prevClose > open ? high - low :
                high - close > close - low ? high - open : prevClose < open ? Math.Max(high - prevClose, close - low) :
                high - close < close - low ? Math.Max(open - close, high - low) : prevClose > open ? high - low :
                prevClose > open ? Math.Max(high - open, close - low) : prevClose < open ? Math.Max(open - close, high - low) : high - low;
            bpiList.AddRounded(bpi);
        }

        var bpiEmaList = GetMovingAverageList(stockData, maType, length, bpiList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var bpi = bpiList[i];
            var bpiEma = bpiEmaList[i];
            var prevBpi = i >= 1 ? bpiList[i - 1] : 0;
            var prevBpiEma = i >= 1 ? bpiEmaList[i - 1] : 0;

            var signal = GetCompareSignal(bpi - bpiEma, prevBpi - prevBpiEma, true);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "BullPower", bpiList },
            { "Signal", bpiEmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = bpiList;
        stockData.IndicatorName = IndicatorName.BullPowerIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Belkhayate Timing
    /// </summary>
    /// <param name="stockData"></param>
    /// <returns></returns>
    public static StockData CalculateBelkhayateTiming(this StockData stockData)
    {
        List<double> bList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var prevHigh1 = i >= 1 ? highList[i - 1] : 0;
            var prevLow1 = i >= 1 ? lowList[i - 1] : 0;
            var prevHigh2 = i >= 2 ? highList[i - 2] : 0;
            var prevLow2 = i >= 2 ? lowList[i - 2] : 0;
            var prevHigh3 = i >= 3 ? highList[i - 3] : 0;
            var prevLow3 = i >= 3 ? lowList[i - 3] : 0;
            var prevHigh4 = i >= 4 ? highList[i - 4] : 0;
            var prevLow4 = i >= 4 ? lowList[i - 4] : 0;
            var prevB1 = i >= 1 ? bList[i - 1] : 0;
            var prevB2 = i >= 2 ? bList[i - 2] : 0;
            var middle = (((currentHigh + currentLow) / 2) + ((prevHigh1 + prevLow1) / 2) + ((prevHigh2 + prevLow2) / 2) +
                          ((prevHigh3 + prevLow3) / 2) + ((prevHigh4 + prevLow4) / 2)) / 5;
            var scale = ((currentHigh - currentLow + (prevHigh1 - prevLow1) + (prevHigh2 - prevLow2) + (prevHigh3 - prevLow3) +
                          (prevHigh4 - prevLow4)) / 5) * 0.2;

            var b = scale != 0 ? (currentValue - middle) / scale : 0;
            bList.AddRounded(b);

            var signal = GetRsiSignal(b - prevB1, prevB1 - prevB2, b, prevB1, 4, -4);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Belkhayate", bList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = bList;
        stockData.IndicatorName = IndicatorName.BelkhayateTiming;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ultimate Trader Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="lbLength"></param>
    /// <param name="smoothLength"></param>
    /// <param name="rangeLength"></param>
    /// <returns></returns>
    public static StockData CalculateUltimateTraderOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length = 10, int lbLength = 5, int smoothLength = 4, int rangeLength = 2)
    {
        List<double> dxList = new();
        List<double> dxiList = new();
        List<double> trList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, openList, volumeList) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, rangeLength);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var prevClose = i >= 1 ? inputList[i - 1] : 0;

            var tr = CalculateTrueRange(currentHigh, currentLow, prevClose);
            trList.AddRounded(tr);
        }

        stockData.CustomValuesList = trList;
        var trStoList = CalculateStochasticOscillator(stockData, maType, length: lbLength).CustomValuesList;
        stockData.CustomValuesList = volumeList;
        var vStoList = CalculateStochasticOscillator(stockData, maType, length: lbLength).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var close = inputList[i];
            var body = close - openList[i];
            var high = highList[i];
            var low = lowList[i];
            var range = high - low;
            var prevClose = i >= 1 ? inputList[i - 1] : 0;
            var c = close - prevClose;
            double sign = Math.Sign(c);
            var highest = highestList[i];
            var lowest = lowestList[i];
            var vSto = vStoList[i];
            var trSto = trStoList[i];
            var k1 = range != 0 ? body / range * 100 : 0;
            var k2 = range == 0 ? 0 : ((close - low) / range * 100 * 2) - 100;
            var k3 = c == 0 || highest - lowest == 0 ? 0 : ((close - lowest) / (highest - lowest) * 100 * 2) - 100;
            var k4 = highest - lowest != 0 ? c / (highest - lowest) * 100 : 0;
            var k5 = sign * trSto;
            var k6 = sign * vSto;
            var bullScore = Math.Max(0, k1) + Math.Max(0, k2) + Math.Max(0, k3) + Math.Max(0, k4) + Math.Max(0, k5) + Math.Max(0, k6);
            var bearScore = -1 * (Math.Min(0, k1) + Math.Min(0, k2) + Math.Min(0, k3) + Math.Min(0, k4) + Math.Min(0, k5) + Math.Min(0, k6));

            var dx = bearScore != 0 ? bullScore / bearScore : 0;
            dxList.AddRounded(dx);

            var dxi = (2 * (100 - (100 / (1 + dx)))) - 100;
            dxiList.AddRounded(dxi);
        }

        var dxiavgList = GetMovingAverageList(stockData, maType, lbLength, dxiList);
        var dxisList = GetMovingAverageList(stockData, maType, smoothLength, dxiavgList);
        var dxissList = GetMovingAverageList(stockData, maType, smoothLength, dxisList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var dxis = dxisList[i];
            var dxiss = dxissList[i];
            var prevDxis = i >= 1 ? dxisList[i - 1] : 0;
            var prevDxiss = i >= 1 ? dxissList[i - 1] : 0;

            var signal = GetCompareSignal(dxis - dxiss, prevDxis - prevDxiss);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Uto", dxisList },
            { "Signal", dxissList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = dxisList;
        stockData.IndicatorName = IndicatorName.UltimateTraderOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Uhl Ma Crossover System
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateUhlMaCrossoverSystem(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 100)
    {
        List<double> cmaList = new();
        List<double> ctsList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);
        var varList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var sma = smaList[i];
            var prevVar = i >= length ? varList[i - length] : 0;
            var prevCma = i >= 1 ? cmaList.LastOrDefault() : currentValue;
            var prevCts = i >= 1 ? ctsList.LastOrDefault() : currentValue;
            var secma = Pow(sma - prevCma, 2);
            var sects = Pow(currentValue - prevCts, 2);
            var ka = prevVar < secma && secma != 0 ? 1 - (prevVar / secma) : 0;
            var kb = prevVar < sects && sects != 0 ? 1 - (prevVar / sects) : 0;

            var cma = (ka * sma) + ((1 - ka) * prevCma);
            cmaList.AddRounded(cma);

            var cts = (kb * currentValue) + ((1 - kb) * prevCts);
            ctsList.AddRounded(cts);

            var signal = GetCompareSignal(cts - cma, prevCts - prevCma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Cts", ctsList },
            { "Cma", cmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.UhlMaCrossoverSystem;

        return stockData;
    }

    /// <summary>
    /// Calculates the McClellan Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="signalLength"></param>
    /// <param name="mult"></param>
    /// <returns></returns>
    public static StockData CalculateMcClellanOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int fastLength = 19, int slowLength = 39, int signalLength = 9, double mult = 1000)
    {
        List<double> advancesList = new();
        List<double> declinesList = new();
        List<double> advancesSumList = new();
        List<double> declinesSumList = new();
        List<double> ranaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            double advance = currentValue > prevValue ? 1 : 0;
            advancesList.AddRounded(advance);

            double decline = currentValue < prevValue ? 1 : 0;
            declinesList.AddRounded(decline);

            var advanceSum = advancesList.TakeLastExt(fastLength).Sum();
            advancesSumList.AddRounded(advanceSum);

            var declineSum = declinesList.TakeLastExt(fastLength).Sum();
            declinesSumList.AddRounded(declineSum);

            var rana = advanceSum + declineSum != 0 ? mult * (advanceSum - declineSum) / (advanceSum + declineSum) : 0;
            ranaList.AddRounded(rana);
        }

        stockData.CustomValuesList = ranaList;
        var moList = CalculateMovingAverageConvergenceDivergence(stockData, maType, fastLength, slowLength, signalLength);
        var mcclellanOscillatorList = moList.OutputValues["Macd"];
        var mcclellanSignalLineList = moList.OutputValues["Signal"];
        var mcclellanHistogramList = moList.OutputValues["Histogram"];
        for (var i = 0; i < stockData.Count; i++)
        {
            var mcclellanHistogram = mcclellanHistogramList[i];
            var prevMcclellanHistogram = i >= 1 ? mcclellanHistogramList[i - 1] : 0;

            var signal = GetCompareSignal(mcclellanHistogram, prevMcclellanHistogram);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "AdvSum", advancesSumList },
            { "DecSum", declinesSumList },
            { "Mo", mcclellanOscillatorList },
            { "Signal", mcclellanSignalLineList },
            { "Histogram", mcclellanHistogramList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = mcclellanOscillatorList;
        stockData.IndicatorName = IndicatorName.McClellanOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Woodie Commodity Channel Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <returns></returns>
    public static StockData CalculateWoodieCommodityChannelIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int fastLength = 6, int slowLength = 14)
    {
        List<double> histogramList = new();
        List<Signal> signalsList = new();

        var cciList = CalculateCommodityChannelIndex(stockData, maType: maType, length: slowLength).CustomValuesList;
        var turboCciList = CalculateCommodityChannelIndex(stockData, maType: maType, length: fastLength).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var cci = cciList[i];
            var cciTurbo = turboCciList[i];

            var prevCciHistogram = histogramList.LastOrDefault();
            var cciHistogram = cciTurbo - cci;
            histogramList.AddRounded(cciHistogram);

            var signal = GetCompareSignal(cciHistogram, prevCciHistogram);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "FastCci", turboCciList },
            { "SlowCci", cciList },
            { "Histogram", histogramList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.WoodieCommodityChannelIndex;

        return stockData;
    }

    /// <summary>
    /// Calculates the Williams Fractals
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateWilliamsFractals(this StockData stockData, int length = 2)
    {
        List<double> upFractalList = new();
        List<double> dnFractalList = new();
        List<Signal> signalsList = new();
        var (_, highList, lowList, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevHigh = i >= length - 2 ? highList[i - (length - 2)] : 0;
            var prevHigh1 = i >= length - 1 ? highList[i - (length - 1)] : 0;
            var prevHigh2 = i >= length ? highList[i - length] : 0;
            var prevHigh3 = i >= length + 1 ? highList[i - (length + 1)] : 0;
            var prevHigh4 = i >= length + 2 ? highList[i - (length + 2)] : 0;
            var prevHigh5 = i >= length + 3 ? highList[i - (length + 3)] : 0;
            var prevHigh6 = i >= length + 4 ? highList[i - (length + 4)] : 0;
            var prevHigh7 = i >= length + 5 ? highList[i - (length + 5)] : 0;
            var prevHigh8 = i >= length + 8 ? highList[i - (length + 6)] : 0;
            var prevLow = i >= length - 2 ? lowList[i - (length - 2)] : 0;
            var prevLow1 = i >= length - 1 ? lowList[i - (length - 1)] : 0;
            var prevLow2 = i >= length ? lowList[i - length] : 0;
            var prevLow3 = i >= length + 1 ? lowList[i - (length + 1)] : 0;
            var prevLow4 = i >= length + 2 ? lowList[i - (length + 2)] : 0;
            var prevLow5 = i >= length + 3 ? lowList[i - (length + 3)] : 0;
            var prevLow6 = i >= length + 4 ? lowList[i - (length + 4)] : 0;
            var prevLow7 = i >= length + 5 ? lowList[i - (length + 5)] : 0;
            var prevLow8 = i >= length + 8 ? lowList[i - (length + 6)] : 0;

            var prevUpFractal = upFractalList.LastOrDefault();
            double upFractal = (prevHigh4 < prevHigh2 && prevHigh3 < prevHigh2 && prevHigh1 < prevHigh2 && prevHigh < prevHigh2) ||
                (prevHigh5 < prevHigh2 && prevHigh4 < prevHigh2 && prevHigh3 == prevHigh2 && prevHigh1 < prevHigh2) ||
                (prevHigh6 < prevHigh2 && prevHigh5 < prevHigh2 && prevHigh4 == prevHigh2 && prevHigh3 <= prevHigh2 && prevHigh1 < prevHigh2 &&
                prevHigh < prevHigh2) || (prevHigh7 < prevHigh2 && prevHigh6 < prevHigh2 && prevHigh5 == prevHigh2 && prevHigh4 == prevHigh2 &&
                prevHigh3 <= prevHigh2 && prevHigh1 < prevHigh2 && prevHigh < prevHigh2) || (prevHigh8 < prevHigh2 && prevHigh7 < prevHigh2 &&
                prevHigh6 == prevHigh2 && prevHigh5 <= prevHigh2 && prevHigh4 == prevHigh2 && prevHigh3 <= prevHigh2 && prevHigh1 < prevHigh2 &&
                prevHigh < prevHigh2) ? 1 : 0;
            upFractalList.AddRounded(upFractal);

            var prevDnFractal = dnFractalList.LastOrDefault();
            double dnFractal = (prevLow4 > prevLow2 && prevLow3 > prevLow2 && prevLow1 > prevLow2 && prevLow > prevLow2) || (prevLow5 > prevLow2 &&
                prevLow4 > prevLow2 && prevLow3 == prevLow2 && prevLow1 > prevLow2 && prevLow > prevLow2) || (prevLow6 > prevLow2 &&
                prevLow5 > prevLow2 && prevLow4 == prevLow2 && prevLow3 >= prevLow2 && prevLow1 > prevLow2 && prevLow > prevLow2) ||
                (prevLow7 > prevLow2 && prevLow6 > prevLow2 && prevLow5 == prevLow2 && prevLow4 == prevLow2 && prevLow3 >= prevLow2 &&
                prevLow1 > prevLow2 && prevLow > prevLow2) || (prevLow8 > prevLow2 && prevLow7 > prevLow2 && prevLow6 == prevLow2 &&
                prevLow5 >= prevLow2 && prevLow4 == prevLow2 && prevLow3 >= prevLow2 && prevLow1 > prevLow2 && prevLow > prevLow2) ? 1 : 0;
            dnFractalList.AddRounded(dnFractal);

            var signal = GetCompareSignal(upFractal - dnFractal, prevUpFractal - prevDnFractal);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpFractal", upFractalList },
            { "DnFractal", dnFractalList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.WilliamsFractals;

        return stockData;
    }

    /// <summary>
    /// Calculates the Williams Accumulation Distribution
    /// </summary>
    /// <param name="stockData"></param>
    /// <returns></returns>
    public static StockData CalculateWilliamsAccumulationDistribution(this StockData stockData)
    {
        List<double> wadList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var close = inputList[i];
            var prevClose = i >= 1 ? inputList[i - 1] : 0;
            var prevLow = i >= 1 ? lowList[i - 1] : 0;
            var prevHigh = i >= 1 ? highList[i - 1] : 0;

            var prevWad = wadList.LastOrDefault();
            var wad = close > prevClose ? prevWad + close - prevLow : close < prevClose ? prevWad + close - prevHigh : 0;
            wadList.AddRounded(wad);

            var signal = GetCompareSignal(wad, prevWad);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Wad", wadList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = wadList;
        stockData.IndicatorName = IndicatorName.WilliamsAccumulationDistribution;

        return stockData;
    }

    /// <summary>
    /// Calculates the Wami Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculateWamiOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 13, int length2 = 4)
    {
        List<double> diffList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var diff = MinPastValues(i, 1, currentValue - prevValue);
            diffList.AddRounded(diff);
        }

        var wma1List = GetMovingAverageList(stockData, MovingAvgType.WeightedMovingAverage, length2, diffList);
        var ema2List = GetMovingAverageList(stockData, maType, length1, wma1List);
        var wamiList = GetMovingAverageList(stockData, maType, length1, ema2List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var wami = wamiList[i];
            var prevWami = i >= 1 ? wamiList[i - 1] : 0;

            var signal = GetCompareSignal(wami, prevWami);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Wami", wamiList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = wamiList;
        stockData.IndicatorName = IndicatorName.WamiOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Waddah Attar Explosion
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="sensitivity"></param>
    /// <returns></returns>
    public static StockData CalculateWaddahAttarExplosion(this StockData stockData, int fastLength = 20, int slowLength = 40, double sensitivity = 150)
    {
        List<double> t1List = new();
        List<double> t2List = new();
        List<double> e1List = new();
        List<double> temp1List = new();
        List<double> temp2List = new();
        List<double> temp3List = new();
        List<double> trendUpList = new();
        List<double> trendDnList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var macd1List = CalculateMovingAverageConvergenceDivergence(stockData, fastLength: fastLength, slowLength: slowLength).CustomValuesList;
        var bbList = CalculateBollingerBands(stockData, length: fastLength);
        var upperBollingerBandList = bbList.OutputValues["UpperBand"];
        var lowerBollingerBandList = bbList.OutputValues["LowerBand"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevValue1 = i >= 1 ? inputList[i - 1] : 0;
            temp1List.AddRounded(prevValue1);

            var prevValue2 = i >= 2 ? inputList[i - 2] : 0;
            temp2List.AddRounded(prevValue2);

            var prevValue3 = i >= 3 ? inputList[i - 3] : 0;
            temp3List.AddRounded(prevValue3);
        }

        stockData.CustomValuesList = temp1List;
        var macd2List = CalculateMovingAverageConvergenceDivergence(stockData, fastLength: fastLength, slowLength: slowLength).CustomValuesList;
        stockData.CustomValuesList = temp2List;
        var macd3List = CalculateMovingAverageConvergenceDivergence(stockData, fastLength: fastLength, slowLength: slowLength).CustomValuesList;
        stockData.CustomValuesList = temp3List;
        var macd4List = CalculateMovingAverageConvergenceDivergence(stockData, fastLength: fastLength, slowLength: slowLength).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentMacd1 = macd1List[i];
            var currentMacd2 = macd2List[i];
            var currentMacd3 = macd3List[i];
            var currentMacd4 = macd4List[i];
            var currentUpperBB = upperBollingerBandList[i];
            var currentLowerBB = lowerBollingerBandList[i];

            var t1 = (currentMacd1 - currentMacd2) * sensitivity;
            t1List.AddRounded(t1);

            var t2 = (currentMacd3 - currentMacd4) * sensitivity;
            t2List.AddRounded(t2);

            var prevE1 = e1List.LastOrDefault();
            var e1 = currentUpperBB - currentLowerBB;
            e1List.AddRounded(e1);

            var prevTrendUp = trendUpList.LastOrDefault();
            var trendUp = (t1 >= 0) ? t1 : 0;
            trendUpList.AddRounded(trendUp);

            var trendDown = (t1 < 0) ? (-1 * t1) : 0;
            trendDnList.AddRounded(trendDown);

            var signal = GetConditionSignal(trendUp > prevTrendUp && trendUp > e1 && e1 > prevE1 && trendUp > fastLength && e1 > fastLength,
                trendUp < e1);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "T1", t1List },
            { "T2", t2List },
            { "E1", e1List },
            { "TrendUp", trendUpList },
            { "TrendDn", trendDnList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.WaddahAttarExplosion;

        return stockData;
    }

    /// <summary>
    /// Calculates the Quantitative Qualitative Estimation
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <param name="fastFactor"></param>
    /// <param name="slowFactor"></param>
    /// <returns></returns>
    public static StockData CalculateQuantitativeQualitativeEstimation(this StockData stockData,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14, int smoothLength = 5, double fastFactor = 2.618,
        double slowFactor = 4.236)
    {
        List<double> atrRsiList = new();
        List<double> fastAtrRsiList = new();
        List<double> slowAtrRsiList = new();
        List<Signal> signalsList = new();

        var wildersLength = (length * 2) - 1;

        var rsiValueList = CalculateRelativeStrengthIndex(stockData, maType, length, smoothLength);
        var rsiEmaList = rsiValueList.OutputValues["Signal"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentRsiEma = rsiEmaList[i];
            var prevRsiEma = i >= 1 ? rsiEmaList[i - 1] : 0;

            var atrRsi = Math.Abs(currentRsiEma - prevRsiEma);
            atrRsiList.AddRounded(atrRsi);
        }

        var atrRsiEmaList = GetMovingAverageList(stockData, maType, wildersLength, atrRsiList);
        var atrRsiEmaSmoothList = GetMovingAverageList(stockData, maType, wildersLength, atrRsiEmaList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var atrRsiEmaSmooth = atrRsiEmaSmoothList[i];
            var prevAtrRsiEmaSmooth = i >= 1 ? atrRsiEmaSmoothList[i - 1] : 0;

            var prevFastTl = fastAtrRsiList.LastOrDefault();
            var fastTl = atrRsiEmaSmooth * fastFactor;
            fastAtrRsiList.AddRounded(fastTl);

            var prevSlowTl = slowAtrRsiList.LastOrDefault();
            var slowTl = atrRsiEmaSmooth * slowFactor;
            slowAtrRsiList.AddRounded(slowTl);

            var signal = GetBullishBearishSignal(atrRsiEmaSmooth - Math.Max(fastTl, slowTl), prevAtrRsiEmaSmooth - Math.Max(prevFastTl, prevSlowTl),
                atrRsiEmaSmooth - Math.Min(fastTl, slowTl), prevAtrRsiEmaSmooth - Math.Min(prevFastTl, prevSlowTl));
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "FastAtrRsi", fastAtrRsiList },
            { "SlowAtrRsi", slowAtrRsiList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.QuantitativeQualitativeEstimation;

        return stockData;
    }

    /// <summary>
    /// Calculates the Quasi White Noise
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="noiseLength"></param>
    /// <param name="divisor"></param>
    /// <returns></returns>
    public static StockData CalculateQuasiWhiteNoise(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod,
        int length = 20, int noiseLength = 500, double divisor = 40)
    {
        List<double> whiteNoiseList = new();
        List<double> whiteNoiseVarianceList = new();
        List<Signal> signalsList = new();

        var connorsRsiList = CalculateConnorsRelativeStrengthIndex(stockData, maType, noiseLength, noiseLength, length).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var connorsRsi = connorsRsiList[i];
            var prevConnorsRsi1 = i >= 1 ? connorsRsiList[i - 1] : 0;
            var prevConnorsRsi2 = i >= 2 ? connorsRsiList[i - 2] : 0;

            var whiteNoise = (connorsRsi - 50) * (1 / divisor);
            whiteNoiseList.AddRounded(whiteNoise);

            var signal = GetRsiSignal(connorsRsi - prevConnorsRsi1, prevConnorsRsi1 - prevConnorsRsi2, connorsRsi, prevConnorsRsi1, 70, 30);
            signalsList.Add(signal);
        }

        var whiteNoiseSmaList = GetMovingAverageList(stockData, maType, noiseLength, whiteNoiseList);
        stockData.CustomValuesList = whiteNoiseList;
        var whiteNoiseStdDevList = CalculateStandardDeviationVolatility(stockData, maType, noiseLength).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var whiteNoiseStdDev = whiteNoiseStdDevList[i];

            var whiteNoiseVariance = Pow(whiteNoiseStdDev, 2);
            whiteNoiseVarianceList.AddRounded(whiteNoiseVariance);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "WhiteNoise", whiteNoiseList },
            { "WhiteNoiseMa", whiteNoiseSmaList },
            { "WhiteNoiseStdDev", whiteNoiseStdDevList },
            { "WhiteNoiseVariance", whiteNoiseVarianceList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = whiteNoiseList;
        stockData.IndicatorName = IndicatorName.QuasiWhiteNoise;

        return stockData;
    }

    /// <summary>
    /// Calculates the LBR Paint Bars
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="lbLength"></param>
    /// <param name="atrMult"></param>
    /// <returns></returns>
    public static StockData CalculateLBRPaintBars(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 9,
        int lbLength = 16, double atrMult = 2.5)
    {
        List<double> upperBandList = new();
        List<double> lowerBandList = new();
        List<double> aatrList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, lbLength);

        var atrList = CalculateAverageTrueRange(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var highest = highestList[i];
            var lowest = lowestList[i];
            var currentAtr = atrList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var aatr = atrMult * currentAtr;
            aatrList.AddRounded(aatr);

            var prevLowerBand = lowerBandList.LastOrDefault();
            var lowerBand = lowest + aatr;
            lowerBandList.AddRounded(lowerBand);

            var prevUpperBand = upperBandList.LastOrDefault();
            var upperBand = highest - aatr;
            upperBandList.AddRounded(upperBand);

            var signal = GetBullishBearishSignal(currentValue - Math.Max(lowerBand, upperBand), prevValue - Math.Max(prevLowerBand, prevUpperBand),
                currentValue - Math.Min(lowerBand, upperBand), prevValue - Math.Min(prevLowerBand, prevUpperBand));
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", upperBandList },
            { "LowerBand", lowerBandList },
            { "MiddleBand", aatrList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.LBRPaintBars;

        return stockData;
    }

    /// <summary>
    /// Calculates the Linear Quadratic Convergence Divergence Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculateLinearQuadraticConvergenceDivergenceOscillator(this StockData stockData,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 50, int signalLength = 25)
    {
        List<double> lqcdList = new();
        List<double> histList = new();
        List<Signal> signalsList = new();

        var linregList = CalculateLinearRegression(stockData, length).CustomValuesList;
        var yList = CalculateQuadraticRegression(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var linreg = linregList[i];
            var y = yList[i];

            var lqcd = y - linreg;
            lqcdList.AddRounded(lqcd);
        }

        var signList = GetMovingAverageList(stockData, maType, signalLength, lqcdList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var sign = signList[i];
            var lqcd = lqcdList[i];
            var osc = lqcd - sign;

            var prevHist = histList.LastOrDefault();
            var hist = osc - sign;
            histList.AddRounded(hist);

            var signal = GetCompareSignal(hist, prevHist);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Lqcdo", histList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = histList;
        stockData.IndicatorName = IndicatorName.LinearQuadraticConvergenceDivergenceOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Logistic Correlation
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="k"></param>
    /// <returns></returns>
    public static StockData CalculateLogisticCorrelation(this StockData stockData, int length = 100, double k = 10)
    {
        List<double> tempList = new();
        List<double> indexList = new();
        List<double> logList = new();
        List<double> corrList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            tempList.AddRounded(currentValue);

            double index = i;
            indexList.AddRounded(index);

            var corr = GoodnessOfFit.R(indexList.TakeLastExt(length).Select(x => (double)x), tempList.TakeLastExt(length).Select(x => (double)x));
            corr = IsValueNullOrInfinity(corr) ? 0 : corr;
            corrList.AddRounded((double)corr);
        }

        for (var i = 0; i < stockData.Count; i++)
        {
            var corr = corrList[i];
            var prevLog1 = i >= 1 ? logList[i - 1] : 0;
            var prevLog2 = i >= 2 ? logList[i - 2] : 0;

            var log = 1 / (1 + Exp(k * -corr));
            logList.AddRounded(log);

            var signal = GetCompareSignal(log - prevLog1, prevLog1 - prevLog2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "LogCorr", logList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = logList;
        stockData.IndicatorName = IndicatorName.LogisticCorrelation;

        return stockData;
    }

    /// <summary>
    /// Calculates the Linda Raschke 3/10 Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateLindaRaschke3_10Oscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int fastLength = 3, int slowLength = 10, int smoothLength = 16)
    {
        List<double> macdList = new();
        List<double> macdHistogramList = new();
        List<double> ppoList = new();
        List<double> ppoHistogramList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var fastSmaList = GetMovingAverageList(stockData, maType, fastLength, inputList);
        var slowSmaList = GetMovingAverageList(stockData, maType, slowLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var sma3 = fastSmaList[i];
            var sma10 = slowSmaList[i];

            var ppo = sma10 != 0 ? (sma3 - sma10) / sma10 * 100 : 0;
            ppoList.AddRounded(ppo);

            var macd = sma3 - sma10;
            macdList.AddRounded(macd);
        }

        var macdSignalLineList = GetMovingAverageList(stockData, maType, smoothLength, macdList);
        var ppoSignalLineList = GetMovingAverageList(stockData, maType, smoothLength, ppoList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var ppo = ppoList[i];
            var ppoSignalLine = ppoSignalLineList[i];
            var macd = macdList[i];
            var macdSignalLine = macdSignalLineList[i];

            var ppoHistogram = ppo - ppoSignalLine;
            ppoHistogramList.AddRounded(ppoHistogram);

            var prevMacdHistogram = macdHistogramList.LastOrDefault();
            var macdHistogram = macd - macdSignalLine;
            macdHistogramList.AddRounded(macdHistogram);

            var signal = GetCompareSignal(macdHistogram, prevMacdHistogram);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "LindaMacd", macdList },
            { "LindaMacdSignal", macdSignalLineList },
            { "LindaMacdHistogram", macdHistogramList },
            { "LindaPpo", ppoList },
            { "LindaPpoSignal", ppoSignalLineList },
            { "LindaPpoHistogram", ppoHistogramList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = macdList;
        stockData.IndicatorName = IndicatorName.LindaRaschke3_10Oscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Relative Volatility Index V1
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateRelativeVolatilityIndexV1(this StockData stockData,
        MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int length = 10, int smoothLength = 14)
    {
        List<double> upList = new();
        List<double> downList = new();
        List<double> rviOriginalList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var stdDeviationList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentStdDeviation = stdDeviationList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var up = currentValue > prevValue ? currentStdDeviation : 0;
            upList.AddRounded(up);

            var down = currentValue < prevValue ? currentStdDeviation : 0;
            downList.AddRounded(down);
        }

        var upAvgList = GetMovingAverageList(stockData, maType, smoothLength, upList);
        var downAvgList = GetMovingAverageList(stockData, maType, smoothLength, downList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var avgUp = upAvgList[i];
            var avgDown = downAvgList[i];
            var prevRvi1 = i >= 1 ? rviOriginalList[i - 1] : 0;
            var prevRvi2 = i >= 2 ? rviOriginalList[i - 2] : 0;
            var rs = avgDown != 0 ? avgUp / avgDown : 0;

            var rvi = avgDown == 0 ? 100 : avgUp == 0 ? 0 : MinOrMax(100 - (100 / (1 + rs)), 100, 0);
            rviOriginalList.AddRounded(rvi);

            var signal = GetRsiSignal(rvi - prevRvi1, prevRvi1 - prevRvi2, rvi, prevRvi1, 70, 30);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Rvi", rviOriginalList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = rviOriginalList;
        stockData.IndicatorName = IndicatorName.RelativeVolatilityIndexV1;

        return stockData;
    }

    /// <summary>
    /// Calculates the Relative Volatility Index V2
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateRelativeVolatilityIndexV2(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod,
        int length = 10, int smoothLength = 14)
    {
        List<double> rviList = new();
        List<Signal> signalsList = new();
        var (_, highList, lowList, _, _) = GetInputValuesList(stockData);

        stockData.CustomValuesList = highList;
        var rviHighList = CalculateRelativeVolatilityIndexV1(stockData, maType, length, smoothLength).CustomValuesList;
        stockData.CustomValuesList = lowList;
        var rviLowList = CalculateRelativeVolatilityIndexV1(stockData, maType, length, smoothLength).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var rviOriginalHigh = rviHighList[i];
            var rviOriginalLow = rviLowList[i];
            var prevRvi1 = i >= 1 ? rviList[i - 1] : 0;
            var prevRvi2 = i >= 2 ? rviList[i - 2] : 0;

            var rvi = (rviOriginalHigh + rviOriginalLow) / 2;
            rviList.AddRounded(rvi);

            var signal = GetRsiSignal(rvi - prevRvi1, prevRvi1 - prevRvi2, rvi, prevRvi1, 70, 30);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Rvi", rviList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = rviList;
        stockData.IndicatorName = IndicatorName.RelativeVolatilityIndexV2;

        return stockData;
    }

    /// <summary>
    /// Calculates the Inertia Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateInertiaIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.LinearRegression,
        int length = 20)
    {
        List<Signal> signalsList = new();

        var rviList = CalculateRelativeVolatilityIndexV2(stockData).CustomValuesList;
        var inertiaList = GetMovingAverageList(stockData, maType, length, rviList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var inertiaIndicator = inertiaList[i];
            var prevInertiaIndicator1 = i >= 1 ? inertiaList[i - 1] : 0;
            var prevInertiaIndicator2 = i >= 2 ? inertiaList[i - 2] : 0;

            var signal = GetCompareSignal(inertiaIndicator - prevInertiaIndicator1, prevInertiaIndicator1 - prevInertiaIndicator2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Inertia", inertiaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = inertiaList;
        stockData.IndicatorName = IndicatorName.InertiaIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Internal Bar Strength Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateInternalBarStrengthIndicator(this StockData stockData, int length = 14, int smoothLength = 3)
    {
        List<double> ibsList = new();
        List<double> ibsiList = new();
        List<double> ibsEmaList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var close = inputList[i];
            var high = highList[i];
            var low = lowList[i];

            var ibs = high - low != 0 ? (close - low) / (high - low) * 100 : 0;
            ibsList.AddRounded(ibs);

            var prevIbsi = ibsiList.LastOrDefault();
            var ibsi = ibsList.TakeLastExt(length).Average();
            ibsiList.AddRounded(ibsi);

            var prevIbsiEma = ibsEmaList.LastOrDefault();
            var ibsiEma = CalculateEMA(ibsi, prevIbsiEma, smoothLength);
            ibsEmaList.AddRounded(ibsiEma);

            var signal = GetRsiSignal(ibsi - ibsiEma, prevIbsi - prevIbsiEma, ibsi, prevIbsi, 70, 30);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ibs", ibsiList },
            { "Signal", ibsEmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = ibsiList;
        stockData.IndicatorName = IndicatorName.InternalBarStrengthIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Inverse Fisher Fast Z Score
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateInverseFisherFastZScore(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 50)
    {
        List<double> ifzList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var length1 = MinOrMax((int)Math.Ceiling((double)length / 2));

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);
        stockData.CustomValuesList = smaList;
        var stdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        stockData.CustomValuesList = smaList;
        var linreg1List = CalculateLinearRegression(stockData, length).CustomValuesList;
        stockData.CustomValuesList = smaList;
        var linreg2List = CalculateLinearRegression(stockData, length1).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var linreg1 = linreg1List[i];
            var linreg2 = linreg2List[i];
            var stdDev = stdDevList[i];
            var fz = stdDev != 0 ? (linreg2 - linreg1) / stdDev / 2 : 0;
            var prevIfz1 = i >= 1 ? ifzList[i - 1] : 0;
            var prevIfz2 = i >= 2 ? ifzList[i - 2] : 0;

            var ifz = Exp(10 * fz) + 1 != 0 ? (Exp(10 * fz) - 1) / (Exp(10 * fz) + 1) : 0;
            ifzList.AddRounded(ifz);

            var signal = GetCompareSignal(ifz - prevIfz1, prevIfz1 - prevIfz2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Iffzs", ifzList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = ifzList;
        stockData.IndicatorName = IndicatorName.InverseFisherFastZScore;

        return stockData;
    }

    /// <summary>
    /// Calculates the Inverse Fisher Z Score
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateInverseFisherZScore(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 100)
    {
        List<double> fList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);
        var stdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var sma = smaList[i];
            var stdDev = stdDevList[i];
            var prevF1 = i >= 1 ? fList[i - 1] : 0;
            var prevF2 = i >= 2 ? fList[i - 2] : 0;
            var z = stdDev != 0 ? (currentValue - sma) / stdDev : 0;
            var expZ = Exp(2 * z);

            var f = expZ + 1 != 0 ? MinOrMax((((expZ - 1) / (expZ + 1)) + 1) * 50, 100, 0) : 0;
            fList.AddRounded(f);

            var signal = GetRsiSignal(f - prevF1, prevF1 - prevF2, f, prevF1, 80, 20);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ifzs", fList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = fList;
        stockData.IndicatorName = IndicatorName.InverseFisherZScore;

        return stockData;
    }

    /// <summary>
    /// Calculates the Insync Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="signalLength"></param>
    /// <param name="emoLength"></param>
    /// <param name="mfiLength"></param>
    /// <param name="bbLength"></param>
    /// <param name="cciLength"></param>
    /// <param name="dpoLength"></param>
    /// <param name="rocLength"></param>
    /// <param name="rsiLength"></param>
    /// <param name="stochLength"></param>
    /// <param name="stochKLength"></param>
    /// <param name="stochDLength"></param>
    /// <param name="smaLength"></param>
    /// <param name="stdDevMult"></param>
    /// <param name="divisor"></param>
    /// <returns></returns>
    public static StockData CalculateInsyncIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int fastLength = 12, int slowLength = 26, int signalLength = 9, int emoLength = 14, int mfiLength = 20, int bbLength = 20,
        int cciLength = 14, int dpoLength = 18, int rocLength = 10, int rsiLength = 14, int stochLength = 14, int stochKLength = 1,
        int stochDLength = 3, int smaLength = 10, double stdDevMult = 2, double divisor = 10000)
    {
        List<double> iidxList = new();
        List<double> tempMacdList = new();
        List<double> tempDpoList = new();
        List<double> tempRocList = new();
        List<double> pdoinsbList = new();
        List<double> pdoinssList = new();
        List<double> emoList = new();
        List<double> emoSmaList = new();
        List<Signal> signalsList = new();

        var rsiList = CalculateRelativeStrengthIndex(stockData, length: rsiLength).CustomValuesList;
        var cciList = CalculateCommodityChannelIndex(stockData, length: cciLength).CustomValuesList;
        var mfiList = CalculateMoneyFlowIndex(stockData, length: mfiLength).CustomValuesList;
        var macdList = CalculateMovingAverageConvergenceDivergence(stockData, fastLength: fastLength, slowLength: slowLength,
            signalLength: signalLength).CustomValuesList;
        var bbIndicatorList = CalculateBollingerBandsPercentB(stockData, stdDevMult: stdDevMult, length: bbLength).CustomValuesList;
        var dpoList = CalculateDetrendedPriceOscillator(stockData, length: dpoLength).CustomValuesList;
        var rocList = CalculateRateOfChange(stockData, length: rocLength).CustomValuesList;
        var stochasticList = CalculateStochasticOscillator(stockData, length: stochLength, smoothLength1: stochKLength, smoothLength2: stochDLength);
        var stochKList = stochasticList.OutputValues["FastD"];
        var stochDList = stochasticList.OutputValues["SlowD"];
        var emvList = CalculateEaseOfMovement(stockData, length: emoLength, divisor: divisor).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var bolins2 = bbIndicatorList[i];
            var prevPdoinss10 = i >= smaLength ? pdoinssList[i - smaLength] : 0;
            var prevPdoinsb10 = i >= smaLength ? pdoinsbList[i - smaLength] : 0;
            var cci = cciList[i];
            var mfi = mfiList[i];
            var rsi = rsiList[i];
            var stochD = stochDList[i];
            var stochK = stochKList[i];
            var prevIidx1 = i >= 1 ? iidxList[i - 1] : 0;
            var prevIidx2 = i >= 2 ? iidxList[i - 2] : 0;
            double bolinsll = bolins2 < 0.05 ? -5 : bolins2 > 0.95 ? 5 : 0;
            double cciins = cci > 100 ? 5 : cci < -100 ? -5 : 0;

            var emo = emvList[i];
            emoList.AddRounded(emo);

            var emoSma = emoList.TakeLastExt(smaLength).Average();
            emoSmaList.AddRounded(emoSma);

            var emvins2 = emo - emoSma;
            double emvinsb = emvins2 < 0 ? emoSma < 0 ? -5 : 0 : emoSma > 0 ? 5 : 0;

            var macd = macdList[i];
            tempMacdList.AddRounded(macd);

            var macdSma = tempMacdList.TakeLastExt(smaLength).Average();
            var macdins2 = macd - macdSma;
            double macdinsb = macdins2 < 0 ? macdSma < 0 ? -5 : 0 : macdSma > 0 ? 5 : 0;
            double mfiins = mfi > 80 ? 5 : mfi < 20 ? -5 : 0;

            var dpo = dpoList[i];
            tempDpoList.AddRounded(dpo);

            var dpoSma = tempDpoList.TakeLastExt(smaLength).Average();
            var pdoins2 = dpo - dpoSma;
            double pdoinsb = pdoins2 < 0 ? dpoSma < 0 ? -5 : 0 : dpoSma > 0 ? 5 : 0;
            pdoinsbList.AddRounded(pdoinsb);

            double pdoinss = pdoins2 > 0 ? dpoSma > 0 ? 5 : 0 : dpoSma < 0 ? -5 : 0;
            pdoinssList.AddRounded(pdoinss);

            var roc = rocList[i];
            tempRocList.AddRounded(roc);

            var rocSma = tempRocList.TakeLastExt(smaLength).Average();
            var rocins2 = roc - rocSma;
            double rocinsb = rocins2 < 0 ? rocSma < 0 ? -5 : 0 : rocSma > 0 ? 5 : 0;
            double rsiins = rsi > 70 ? 5 : rsi < 30 ? -5 : 0;
            double stopdins = stochD > 80 ? 5 : stochD < 20 ? -5 : 0;
            double stopkins = stochK > 80 ? 5 : stochK < 20 ? -5 : 0;

            var iidx = 50 + cciins + bolinsll + rsiins + stopkins + stopdins + mfiins + emvinsb + rocinsb + prevPdoinss10 + prevPdoinsb10 + macdinsb;
            iidxList.AddRounded(iidx);

            var signal = GetRsiSignal(iidx - prevIidx1, prevIidx1 - prevIidx2, iidx, prevIidx1, 95, 5);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Iidx", iidxList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = iidxList;
        stockData.IndicatorName = IndicatorName.InsyncIndex;

        return stockData;
    }

    /// <summary>
    /// Calculates the Detrended Price Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateDetrendedPriceOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 20)
    {
        List<double> dpoList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var prevPeriods = MinOrMax((int)Math.Ceiling(((double)length / 2) + 1));

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentSma = smaList[i];
            var prevValue = i >= prevPeriods ? inputList[i - prevPeriods] : 0;

            var prevDpo = dpoList.LastOrDefault();
            var dpo = prevValue - currentSma;
            dpoList.AddRounded(dpo);

            var signal = GetCompareSignal(dpo, prevDpo);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Dpo", dpoList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = dpoList;
        stockData.IndicatorName = IndicatorName.DetrendedPriceOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ocean Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateOceanIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 14)
    {
        List<double> lnList = new();
        List<double> oiList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevLn = i >= length ? lnList[i - length] : 0;

            var ln = currentValue > 0 ? Math.Log(currentValue) * 1000 : 0;
            lnList.AddRounded(ln);

            var oi = (ln - prevLn) / Sqrt(length) * 100;
            oiList.AddRounded(oi);
        }

        var oiEmaList = GetMovingAverageList(stockData, maType, length, oiList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var oiEma = oiEmaList[i];
            var prevOiEma1 = i >= 1 ? oiEmaList[i - 1] : 0;
            var prevOiEma2 = i >= 2 ? oiEmaList[i - 2] : 0;

            var signal = GetCompareSignal(oiEma - prevOiEma1, prevOiEma1 - prevOiEma2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Oi", oiList },
            { "Signal", oiEmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = oiList;
        stockData.IndicatorName = IndicatorName.OceanIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Oscar Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateOscarIndicator(this StockData stockData, int length = 8)
    {
        List<double> oscarList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var highest = highestList[i];
            var lowest = lowestList[i];
            var rough = highest - lowest != 0 ? MinOrMax((currentValue - lowest) / (highest - lowest) * 100, 100, 0) : 0;
            var prevOscar1 = i >= 1 ? oscarList[i - 1] : 0;
            var prevOscar2 = i >= 2 ? oscarList[i - 2] : 0;

            var oscar = (prevOscar1 / 6) + (rough / 3);
            oscarList.AddRounded(oscar);

            var signal = GetRsiSignal(oscar - prevOscar1, prevOscar1 - prevOscar2, oscar, prevOscar1, 80, 20);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Oscar", oscarList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = oscarList;
        stockData.IndicatorName = IndicatorName.OscarIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the OC Histogram
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateOCHistogram(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 10)
    {
        List<double> ocHistogramList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, openList, _) = GetInputValuesList(stockData);

        var openEmaList = GetMovingAverageList(stockData, maType, length, openList);
        var closeEmaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentCloseEma = closeEmaList[i];
            var currentOpenEma = openEmaList[i];

            var prevOcHistogram = ocHistogramList.LastOrDefault();
            var ocHistogram = currentCloseEma - currentOpenEma;
            ocHistogramList.AddRounded(ocHistogram);

            var signal = GetCompareSignal(ocHistogram, prevOcHistogram);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "OcHistogram", ocHistogramList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = ocHistogramList;
        stockData.IndicatorName = IndicatorName.OCHistogram;

        return stockData;
    }

    /// <summary>
    /// Calculates the Osc Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <returns></returns>
    public static StockData CalculateOscOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int fastLength = 7, int slowLength = 14)
    {
        List<double> oscList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var fastSmaList = GetMovingAverageList(stockData, maType, fastLength, inputList);
        var slowSmaList = GetMovingAverageList(stockData, maType, slowLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var fastSma = fastSmaList[i];
            var slowSma = slowSmaList[i];
            var prevOsc1 = i >= 1 ? oscList[i - 1] : 0;
            var prevOsc2 = i >= 2 ? oscList[i - 2] : 0;

            var osc = slowSma - fastSma;
            oscList.AddRounded(osc);

            var signal = GetCompareSignal(osc - prevOsc1, prevOsc1 - prevOsc2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "OscOscillator", oscList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = oscList;
        stockData.IndicatorName = IndicatorName.OscOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Natural Directional Combo
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateNaturalDirectionalCombo(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 40, int smoothLength = 20)
    {
        List<double> nxcList = new();
        List<Signal> signalsList = new();

        var ndxList = CalculateNaturalDirectionalIndex(stockData, maType, length, smoothLength).CustomValuesList;
        var nstList = CalculateNaturalStochasticIndicator(stockData, maType, length, smoothLength).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var ndx = ndxList[i];
            var nst = nstList[i];
            var prevNxc1 = i >= 1 ? nxcList[i - 1] : 0;
            var prevNxc2 = i >= 2 ? nxcList[i - 2] : 0;
            var v3 = Math.Sign(ndx) != Math.Sign(nst) ? ndx * nst : ((Math.Abs(ndx) * nst) + (Math.Abs(nst) * ndx)) / 2;

            var nxc = Math.Sign(v3) * Sqrt(Math.Abs(v3));
            nxcList.AddRounded(nxc);

            var signal = GetCompareSignal(nxc - prevNxc1, prevNxc1 - prevNxc2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Nxc", nxcList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = nxcList;
        stockData.IndicatorName = IndicatorName.NaturalDirectionalCombo;

        return stockData;
    }

    /// <summary>
    /// Calculates the Natural Directional Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateNaturalDirectionalIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 40, int smoothLength = 20)
    {
        List<double> lnList = new();
        List<double> rawNdxList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];

            var ln = currentValue > 0 ? Math.Log(currentValue) * 1000 : 0;
            lnList.AddRounded(ln);

            double weightSum = 0, denomSum = 0, absSum = 0;
            for (var j = 0; j < length; j++)
            {
                var prevLn = i >= j + 1 ? lnList[i - (j + 1)] : 0;
                var currLn = i >= j ? lnList[i - j] : 0;
                var diff = prevLn - currLn;
                absSum += Math.Abs(diff);
                var frac = absSum != 0 ? (ln - currLn) / absSum : 0;
                var ratio = 1 / Sqrt(j + 1);
                weightSum += frac * ratio;
                denomSum += ratio;
            }

            var rawNdx = denomSum != 0 ? weightSum / denomSum * 100 : 0;
            rawNdxList.AddRounded(rawNdx);
        }

        var ndxList = GetMovingAverageList(stockData, maType, smoothLength, rawNdxList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var ndx = ndxList[i];
            var prevNdx1 = i >= 1 ? ndxList[i - 1] : 0;
            var prevNdx2 = i >= 2 ? ndxList[i - 2] : 0;

            var signal = GetCompareSignal(ndx - prevNdx1, prevNdx1 - prevNdx2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ndx", ndxList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = ndxList;
        stockData.IndicatorName = IndicatorName.NaturalDirectionalIndex;

        return stockData;
    }

    /// <summary>
    /// Calculates the Natural Market Mirror
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateNaturalMarketMirror(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 40)
    {
        List<double> lnList = new();
        List<double> oiAvgList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];

            var ln = currentValue > 0 ? Math.Log(currentValue) * 1000 : 0;
            lnList.AddRounded(ln);

            double oiSum = 0;
            for (var j = 1; j <= length; j++)
            {
                var prevLn = i >= j ? lnList[i - j] : 0;
                oiSum += (ln - prevLn) / Sqrt(j) * 100;
            }

            var oiAvg = oiSum / length;
            oiAvgList.AddRounded(oiAvg);
        }

        var nmmList = GetMovingAverageList(stockData, maType, length, oiAvgList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var nmm = nmmList[i];
            var prevNmm1 = i >= 1 ? nmmList[i - 1] : 0;
            var prevNmm2 = i >= 2 ? nmmList[i - 2] : 0;

            var signal = GetCompareSignal(nmm - prevNmm1, prevNmm1 - prevNmm2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Nmm", nmmList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = nmmList;
        stockData.IndicatorName = IndicatorName.NaturalMarketMirror;

        return stockData;
    }

    /// <summary>
    /// Calculates the Natural Market River
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateNaturalMarketRiver(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 40)
    {
        List<double> lnList = new();
        List<double> oiSumList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];

            var ln = currentValue > 0 ? Math.Log(currentValue) * 1000 : 0;
            lnList.AddRounded(ln);

            double oiSum = 0;
            for (var j = 0; j < length; j++)
            {
                var currentLn = i >= j ? lnList[i - j] : 0;
                var prevLn = i >= j + 1 ? lnList[i - (j + 1)] : 0;

                oiSum += (prevLn - currentLn) * (Sqrt(j) - Sqrt(j + 1));
            }
            oiSumList.AddRounded(oiSum);
        }

        var nmrList = GetMovingAverageList(stockData, maType, length, oiSumList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var nmr = nmrList[i];
            var prevNmr1 = i >= 1 ? nmrList[i - 1] : 0;
            var prevNmr2 = i >= 2 ? nmrList[i - 2] : 0;

            var signal = GetCompareSignal(nmr - prevNmr1, prevNmr1 - prevNmr2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Nmr", nmrList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = nmrList;
        stockData.IndicatorName = IndicatorName.NaturalMarketRiver;

        return stockData;
    }

    /// <summary>
    /// Calculates the Natural Market Combo
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateNaturalMarketCombo(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length = 40, int smoothLength = 20)
    {
        List<double> nmcList = new();
        List<Signal> signalsList = new();

        var nmrList = CalculateNaturalMarketRiver(stockData, maType, length).CustomValuesList;
        var nmmList = CalculateNaturalMarketMirror(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var nmr = nmrList[i];
            var nmm = nmmList[i];
            var v3 = Math.Sign(nmm) != Math.Sign(nmr) ? nmm * nmr : ((Math.Abs(nmm) * nmr) + (Math.Abs(nmr) * nmm)) / 2;

            var nmc = Math.Sign(v3) * Sqrt(Math.Abs(v3));
            nmcList.AddRounded(nmc);
        }

        var nmcMaList = GetMovingAverageList(stockData, maType, smoothLength, nmcList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var nmc = nmcMaList[i];
            var prevNmc1 = i >= 1 ? nmcMaList[i - 1] : 0;
            var prevNmc2 = i >= 2 ? nmcMaList[i - 2] : 0;

            var signal = GetCompareSignal(nmc - prevNmc1, prevNmc1 - prevNmc2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Nmc", nmcList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = nmcList;
        stockData.IndicatorName = IndicatorName.NaturalMarketCombo;

        return stockData;
    }

    /// <summary>
    /// Calculates the Natural Market Slope
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateNaturalMarketSlope(this StockData stockData, int length = 40)
    {
        List<double> lnList = new();
        List<double> nmsList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];

            var ln = currentValue > 0 ? Math.Log(currentValue) * 1000 : 0;
            lnList.AddRounded(ln);
        }

        stockData.CustomValuesList = lnList;
        var linRegList = CalculateLinearRegression(stockData, length).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var linReg = linRegList[i];
            var prevLinReg = i >= 1 ? linRegList[i - 1] : 0;
            var prevNms1 = i >= 1 ? nmsList[i - 1] : 0;
            var prevNms2 = i >= 2 ? nmsList[i - 2] : 0;

            var nms = (linReg - prevLinReg) * Math.Log(length);
            nmsList.AddRounded(nms);

            var signal = GetCompareSignal(nms - prevNms1, prevNms1 - prevNms2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Nms", nmsList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = nmsList;
        stockData.IndicatorName = IndicatorName.NaturalMarketSlope;

        return stockData;
    }

    /// <summary>
    /// Calculates the Narrow Bandpass Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateNarrowBandpassFilter(this StockData stockData, int length = 50)
    {
        List<double> sumList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevSum1 = i >= 1 ? sumList[i - 1] : 0;
            var prevSum2 = i >= 2 ? sumList[i - 2] : 0;

            double sum = 0;
            for (var j = 0; j <= length - 1; j++)
            {
                var prevValue = i >= j ? inputList[i - j] : 0;
                var x = j / (double)(length - 1);
                var win = 0.42 - (0.5 * Math.Cos(2 * Math.PI * x)) + (0.08 * Math.Cos(4 * Math.PI * x));
                var w = Math.Sin(2 * Math.PI * j / length) * win;
                sum += prevValue * w;
            }
            sumList.AddRounded(sum);

            var signal = GetCompareSignal(sum - prevSum1, prevSum1 - prevSum2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Nbpf", sumList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = sumList;
        stockData.IndicatorName = IndicatorName.NarrowBandpassFilter;

        return stockData;
    }

    /// <summary>
    /// Calculates the Nth Order Differencing Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="lbLength"></param>
    /// <returns></returns>
    public static StockData CalculateNthOrderDifferencingOscillator(this StockData stockData, int length = 14, int lbLength = 2)
    {
        List<double> nodoList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];

            double sum = 0, w = 1;
            for (var j = 0; j <= lbLength; j++)
            {
                var prevValue = i >= length * (j + 1) ? inputList[i - (length * (j + 1))] : 0;
                double x = Math.Sign(((j + 1) % 2) - 0.5);
                w *= (lbLength - j) / (double)(j + 1);
                sum += prevValue * w * x;
            }

            var prevNodo = nodoList.LastOrDefault();
            var nodo = currentValue - sum;
            nodoList.AddRounded(nodo);

            var signal = GetCompareSignal(nodo, prevNodo);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Nodo", nodoList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = nodoList;
        stockData.IndicatorName = IndicatorName.NthOrderDifferencingOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Normalized Relative Vigor Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateNormalizedRelativeVigorIndex(this StockData stockData,
        MovingAvgType maType = MovingAvgType.SymmetricallyWeightedMovingAverage, int length = 10)
    {
        List<double> closeOpenList = new();
        List<double> highLowList = new();
        List<double> tempCloseOpenList = new();
        List<double> tempHighLowList = new();
        List<double> swmaCloseOpenSumList = new();
        List<double> swmaHighLowSumList = new();
        List<double> rvgiList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, openList, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentClose = inputList[i];
            var currentOpen = openList[i];
            var currentHigh = highList[i];
            var currentLow = lowList[i];

            var closeOpen = currentClose - currentOpen;
            closeOpenList.AddRounded(closeOpen);

            var highLow = currentHigh - currentLow;
            highLowList.AddRounded(highLow);
        }

        var swmaCloseOpenList = GetMovingAverageList(stockData, maType, length, closeOpenList);
        var swmaHighLowList = GetMovingAverageList(stockData, maType, length, highLowList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var swmaCloseOpen = swmaCloseOpenList[i];
            tempCloseOpenList.AddRounded(swmaCloseOpen);

            var closeOpenSum = tempCloseOpenList.TakeLastExt(length).Sum();
            swmaCloseOpenSumList.AddRounded(closeOpenSum);

            var swmaHighLow = swmaHighLowList[i];
            tempHighLowList.AddRounded(swmaHighLow);

            var highLowSum = tempHighLowList.TakeLastExt(length).Sum();
            swmaHighLowSumList.AddRounded(highLowSum);

            var rvgi = highLowSum != 0 ? closeOpenSum / highLowSum * 100 : 0;
            rvgiList.AddRounded(rvgi);
        }

        var rvgiSignalList = GetMovingAverageList(stockData, maType, length, rvgiList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var rvgi = rvgiList[i];
            var rvgiSig = rvgiSignalList[i];
            var prevRvgi = i >= 1 ? rvgiList[i - 1] : 0;
            var prevRvgiSig = i >= 1 ? rvgiSignalList[i - 1] : 0;

            var signal = GetCompareSignal(rvgi - rvgiSig, prevRvgi - prevRvgiSig);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Nrvi", rvgiList },
            { "Signal", rvgiSignalList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = rvgiList;
        stockData.IndicatorName = IndicatorName.NormalizedRelativeVigorIndex;

        return stockData;
    }

    /// <summary>
    /// Calculates the Gann Swing Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateGannSwingOscillator(this StockData stockData, int length = 5)
    {
        List<double> gannSwingOscillatorList = new();
        List<Signal> signalsList = new();
        var (_, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var highestHigh = highestList[i];
            var lowestLow = lowestList[i];
            var prevHighest1 = i >= 1 ? highestList[i - 1] : 0;
            var prevLowest1 = i >= 1 ? lowestList[i - 1] : 0;
            var prevHighest2 = i >= 2 ? highestList[i - 2] : 0;
            var prevLowest2 = i >= 2 ? lowestList[i - 2] : 0;

            var prevGso = gannSwingOscillatorList.LastOrDefault();
            var gso = prevHighest2 > prevHighest1 && highestHigh > prevHighest1 ? 1 :
                prevLowest2 < prevLowest1 && lowestLow < prevLowest1 ? -1 : prevGso;
            gannSwingOscillatorList.AddRounded(gso);

            var signal = GetCompareSignal(gso, prevGso);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Gso", gannSwingOscillatorList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = gannSwingOscillatorList;
        stockData.IndicatorName = IndicatorName.GannSwingOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Gann HiLo Activator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateGannHiLoActivator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 3)
    {
        List<double> ghlaList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        var highMaList = GetMovingAverageList(stockData, maType, length, highList);
        var lowMaList = GetMovingAverageList(stockData, maType, length, lowList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var highMa = highMaList[i];
            var lowMa = lowMaList[i];
            var prevHighMa = i >= 1 ? highMaList[i - 1] : 0;
            var prevLowMa = i >= 1 ? lowMaList[i - 1] : 0;
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevGhla = ghlaList.LastOrDefault();
            var ghla = currentValue > prevHighMa ? lowMa : currentValue < prevLowMa ? highMa : prevGhla;
            ghlaList.AddRounded(ghla);

            var signal = GetCompareSignal(currentValue - ghla, prevValue - prevGhla);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ghla", ghlaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = ghlaList;
        stockData.IndicatorName = IndicatorName.GannHiLoActivator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Grover Llorens Cycle Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <param name="mult"></param>
    /// <returns></returns>
    public static StockData CalculateGroverLlorensCycleOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod,
        int length = 100, int smoothLength = 20, double mult = 10)
    {
        List<double> tsList = new();
        List<double> oscList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var atrList = CalculateAverageTrueRange(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var atr = atrList[i];
            var prevTs = i >= 1 ? tsList[i - 1] : currentValue;
            var diff = currentValue - prevTs;

            var ts = diff > 0 ? prevTs - (atr * mult) : diff < 0 ? prevTs + (atr * mult) : prevTs;
            tsList.AddRounded(ts);

            var osc = currentValue - ts;
            oscList.AddRounded(osc);
        }

        var smoList = GetMovingAverageList(stockData, maType, smoothLength, oscList);
        stockData.CustomValuesList = smoList;
        var rsiList = CalculateRelativeStrengthIndex(stockData, maType, length: smoothLength).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var rsi = rsiList[i];
            var prevRsi1 = i >= 1 ? rsiList[i - 1] : 0;
            var prevRsi2 = i >= 2 ? rsiList[i - 2] : 0;

            var signal = GetRsiSignal(rsi - prevRsi1, prevRsi1 - prevRsi2, rsi, prevRsi1, 80, 20);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Glco", rsiList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = rsiList;
        stockData.IndicatorName = IndicatorName.GroverLlorensCycleOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Grover Llorens Activator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="mult"></param>
    /// <returns></returns>
    public static StockData CalculateGroverLlorensActivator(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod,
        int length = 100, double mult = 5)
    {
        List<double> tsList = new();
        List<double> diffList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var atrList = CalculateAverageTrueRange(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var atr = atrList[i];
            var prevTs = i >= 1 ? tsList[i - 1] : currentValue;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            prevTs = prevTs == 0 ? prevValue : prevTs;

            var prevDiff = diffList.LastOrDefault();
            var diff = currentValue - prevTs;
            diffList.AddRounded(diff);

            var ts = diff > 0 ? prevTs - (atr * mult) : diff < 0 ? prevTs + (atr * mult) : prevTs;
            tsList.AddRounded(ts);

            var signal = GetCompareSignal(diff, prevDiff);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Gla", tsList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = tsList;
        stockData.IndicatorName = IndicatorName.GroverLlorensActivator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Guppy Count Back Line
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateGuppyCountBackLine(this StockData stockData, int length = 21)
    {
        List<double> cblList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var hh = highestList[i];
            var ll = lowestList[i];

            var prevCbl = cblList.LastOrDefault();
            int hCount = 0, lCount = 0;
            var cbl = currentValue;
            for (var j = 0; j <= length; j++)
            {
                var currentLow = i >= j ? lowList[i - j] : 0;
                var currentHigh = i >= j ? highList[i - j] : 0;

                if (currentLow == ll)
                {
                    for (var k = j + 1; k <= j + length; k++)
                    {
                        var prevHigh = i >= k ? highList[i - k] : 0;
                        lCount += prevHigh > currentHigh ? 1 : 0;
                        if (lCount == 2)
                        {
                            cbl = prevHigh;
                            break;
                        }
                    }
                }

                if (currentHigh == hh)
                {
                    for (var k = j + 1; k <= j + length; k++)
                    {
                        var prevLow = i >= k ? lowList[i - k] : 0;
                        hCount += prevLow > currentLow ? 1 : 0;
                        if (hCount == 2)
                        {
                            cbl = prevLow;
                            break;
                        }
                    }
                }
            }
            cblList.AddRounded(cbl);

            var signal = GetCompareSignal(currentValue - cbl, prevValue - prevCbl);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Cbl", cblList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = cblList;
        stockData.IndicatorName = IndicatorName.GuppyCountBackLine;

        return stockData;
    }

    /// <summary>
    /// Calculates the Guppy Multiple Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="length4"></param>
    /// <param name="length5"></param>
    /// <param name="length6"></param>
    /// <param name="length7"></param>
    /// <param name="length8"></param>
    /// <param name="length9"></param>
    /// <param name="length10"></param>
    /// <param name="length11"></param>
    /// <param name="length12"></param>
    /// <param name="length13"></param>
    /// <param name="length14"></param>
    /// <param name="length15"></param>
    /// <param name="length16"></param>
    /// <param name="length17"></param>
    /// <param name="length18"></param>
    /// <param name="length19"></param>
    /// <param name="length20"></param>
    /// <param name="length21"></param>
    /// <param name="length22"></param>
    /// <param name="length23"></param>
    /// <param name="length24"></param>
    /// <param name="length25"></param>
    /// <param name="length26"></param>
    /// <param name="length27"></param>
    /// <param name="length28"></param>
    /// <param name="length29"></param>
    /// <param name="length30"></param>
    /// <param name="length31"></param>
    /// <param name="length32"></param>
    /// <param name="length33"></param>
    /// <param name="length34"></param>
    /// <param name="length35"></param>
    /// <param name="smoothLength"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculateGuppyMultipleMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 3, int length2 = 5, int length3 = 7, int length4 = 8, int length5 = 9, int length6 = 10, int length7 = 11, int length8 = 12,
        int length9 = 13, int length10 = 15, int length11 = 17, int length12 = 19, int length13 = 21, int length14 = 23, int length15 = 25,
        int length16 = 28, int length17 = 30, int length18 = 31, int length19 = 34, int length20 = 35, int length21 = 37, int length22 = 40,
        int length23 = 43, int length24 = 45, int length25 = 46, int length26 = 49, int length27 = 50, int length28 = 52, int length29 = 55,
        int length30 = 58, int length31 = 60, int length32 = 61, int length33 = 64, int length34 = 67, int length35 = 70, int smoothLength = 1,
        int signalLength = 13)
    {
        List<double> superGmmaFastList = new();
        List<double> superGmmaSlowList = new();
        List<double> superGmmaOscRawList = new();
        List<double> superGmmaOscList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var ema3List = GetMovingAverageList(stockData, maType, length1, inputList);
        var ema5List = GetMovingAverageList(stockData, maType, length2, inputList);
        var ema7List = GetMovingAverageList(stockData, maType, length3, inputList);
        var ema9List = GetMovingAverageList(stockData, maType, length5, inputList);
        var ema11List = GetMovingAverageList(stockData, maType, length7, inputList);
        var ema13List = GetMovingAverageList(stockData, maType, length9, inputList);
        var ema15List = GetMovingAverageList(stockData, maType, length10, inputList);
        var ema17List = GetMovingAverageList(stockData, maType, length11, inputList);
        var ema19List = GetMovingAverageList(stockData, maType, length12, inputList);
        var ema21List = GetMovingAverageList(stockData, maType, length13, inputList);
        var ema23List = GetMovingAverageList(stockData, maType, length14, inputList);
        var ema25List = GetMovingAverageList(stockData, maType, length15, inputList);
        var ema28List = GetMovingAverageList(stockData, maType, length16, inputList);
        var ema31List = GetMovingAverageList(stockData, maType, length18, inputList);
        var ema34List = GetMovingAverageList(stockData, maType, length19, inputList);
        var ema37List = GetMovingAverageList(stockData, maType, length21, inputList);
        var ema40List = GetMovingAverageList(stockData, maType, length22, inputList);
        var ema43List = GetMovingAverageList(stockData, maType, length23, inputList);
        var ema46List = GetMovingAverageList(stockData, maType, length25, inputList);
        var ema49List = GetMovingAverageList(stockData, maType, length26, inputList);
        var ema52List = GetMovingAverageList(stockData, maType, length28, inputList);
        var ema55List = GetMovingAverageList(stockData, maType, length29, inputList);
        var ema58List = GetMovingAverageList(stockData, maType, length30, inputList);
        var ema61List = GetMovingAverageList(stockData, maType, length32, inputList);
        var ema64List = GetMovingAverageList(stockData, maType, length33, inputList);
        var ema67List = GetMovingAverageList(stockData, maType, length34, inputList);
        var ema70List = GetMovingAverageList(stockData, maType, length35, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var emaF1 = ema3List[i];
            var emaF2 = ema5List[i];
            var emaF3 = ema7List[i];
            var emaF4 = ema9List[i];
            var emaF5 = ema11List[i];
            var emaF6 = ema13List[i];
            var emaF7 = ema15List[i];
            var emaF8 = ema17List[i];
            var emaF9 = ema19List[i];
            var emaF10 = ema21List[i];
            var emaF11 = ema23List[i];
            var emaS1 = ema25List[i];
            var emaS2 = ema28List[i];
            var emaS3 = ema31List[i];
            var emaS4 = ema34List[i];
            var emaS5 = ema37List[i];
            var emaS6 = ema40List[i];
            var emaS7 = ema43List[i];
            var emaS8 = ema46List[i];
            var emaS9 = ema49List[i];
            var emaS10 = ema52List[i];
            var emaS11 = ema55List[i];
            var emaS12 = ema58List[i];
            var emaS13 = ema61List[i];
            var emaS14 = ema64List[i];
            var emaS15 = ema67List[i];
            var emaS16 = ema70List[i];

            var superGmmaFast = (emaF1 + emaF2 + emaF3 + emaF4 + emaF5 + emaF6 + emaF7 + emaF8 + emaF9 + emaF10 + emaF11) / 11;
            superGmmaFastList.AddRounded(superGmmaFast);

            var superGmmaSlow = (emaS1 + emaS2 + emaS3 + emaS4 + emaS5 + emaS6 + emaS7 + emaS8 + emaS9 + emaS10 + emaS11 + emaS12 + emaS13 +
                                 emaS14 + emaS15 + emaS16) / 16;
            superGmmaSlowList.AddRounded(superGmmaSlow);

            var superGmmaOscRaw = superGmmaSlow != 0 ? (superGmmaFast - superGmmaSlow) / superGmmaSlow * 100 : 0;
            superGmmaOscRawList.AddRounded(superGmmaOscRaw);

            var superGmmaOsc = superGmmaOscRawList.TakeLastExt(smoothLength).Average();
            superGmmaOscList.AddRounded(superGmmaOsc);
        }

        var superGmmaSignalList = GetMovingAverageList(stockData, maType, signalLength, superGmmaOscRawList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var superGmmaOsc = superGmmaOscList[i];
            var superGmmaSignal = superGmmaSignalList[i];
            var prevSuperGmmaOsc = i >= 1 ? superGmmaOscList[i - 1] : 0;
            var prevSuperGmmaSignal = i >= 1 ? superGmmaSignalList[i - 1] : 0;

            var signal = GetCompareSignal(superGmmaOsc - superGmmaSignal, prevSuperGmmaOsc - prevSuperGmmaSignal);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "SuperGmmaOsc", superGmmaOscList },
            { "SuperGmmaSignal", superGmmaSignalList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = superGmmaOscList;
        stockData.IndicatorName = IndicatorName.GuppyMultipleMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Guppy Distance Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="length4"></param>
    /// <param name="length5"></param>
    /// <param name="length6"></param>
    /// <param name="length7"></param>
    /// <param name="length8"></param>
    /// <param name="length9"></param>
    /// <param name="length10"></param>
    /// <param name="length11"></param>
    /// <param name="length12"></param>
    /// <returns></returns>
    public static StockData CalculateGuppyDistanceIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 3, int length2 = 5, int length3 = 8, int length4 = 10, int length5 = 12, int length6 = 15, int length7 = 30, int length8 = 35,
        int length9 = 40, int length10 = 45, int length11 = 11, int length12 = 60)
    {
        List<double> fastDistanceList = new();
        List<double> slowDistanceList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var ema3List = GetMovingAverageList(stockData, maType, length1, inputList);
        var ema5List = GetMovingAverageList(stockData, maType, length2, inputList);
        var ema8List = GetMovingAverageList(stockData, maType, length3, inputList);
        var ema10List = GetMovingAverageList(stockData, maType, length4, inputList);
        var ema12List = GetMovingAverageList(stockData, maType, length5, inputList);
        var ema15List = GetMovingAverageList(stockData, maType, length6, inputList);
        var ema30List = GetMovingAverageList(stockData, maType, length7, inputList);
        var ema35List = GetMovingAverageList(stockData, maType, length8, inputList);
        var ema40List = GetMovingAverageList(stockData, maType, length9, inputList);
        var ema45List = GetMovingAverageList(stockData, maType, length10, inputList);
        var ema50List = GetMovingAverageList(stockData, maType, length11, inputList);
        var ema60List = GetMovingAverageList(stockData, maType, length12, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var ema1 = ema3List[i];
            var ema2 = ema5List[i];
            var ema3 = ema8List[i];
            var ema4 = ema10List[i];
            var ema5 = ema12List[i];
            var ema6 = ema15List[i];
            var ema7 = ema30List[i];
            var ema8 = ema35List[i];
            var ema9 = ema40List[i];
            var ema10 = ema45List[i];
            var ema11 = ema50List[i];
            var ema12 = ema60List[i];
            var diff12 = Math.Abs(ema1 - ema2);
            var diff23 = Math.Abs(ema2 - ema3);
            var diff34 = Math.Abs(ema3 - ema4);
            var diff45 = Math.Abs(ema4 - ema5);
            var diff56 = Math.Abs(ema5 - ema6);
            var diff78 = Math.Abs(ema7 - ema8);
            var diff89 = Math.Abs(ema8 - ema9);
            var diff910 = Math.Abs(ema9 - ema10);
            var diff1011 = Math.Abs(ema10 - ema11);
            var diff1112 = Math.Abs(ema11 - ema12);

            var fastDistance = diff12 + diff23 + diff34 + diff45 + diff56;
            fastDistanceList.AddRounded(fastDistance);

            var slowDistance = diff78 + diff89 + diff910 + diff1011 + diff1112;
            slowDistanceList.AddRounded(slowDistance);

            var colFastL = ema1 > ema2 && ema2 > ema3 && ema3 > ema4 && ema4 > ema5 && ema5 > ema6;
            var colFastS = ema1 < ema2 && ema2 < ema3 && ema3 < ema4 && ema4 < ema5 && ema5 < ema6;
            var colSlowL = ema7 > ema8 && ema8 > ema9 && ema9 > ema10 && ema10 > ema11 && ema11 > ema12;
            var colSlowS = ema7 < ema8 && ema8 < ema9 && ema9 < ema10 && ema10 < ema11 && ema11 < ema12;

            var signal = GetConditionSignal(colSlowL || colFastL, colSlowS || colFastS);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "FastDistance", fastDistanceList },
            { "SlowDistance", slowDistanceList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.GuppyDistanceIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the G Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateGOscillator(this StockData stockData, int length = 14)
    {
        List<double> bList = new();
        List<double> bSumList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevBSum1 = i >= 1 ? bSumList[i - 1] : 0;
            var prevBSum2 = i >= 2 ? bSumList[i - 2] : 0;

            var b = currentValue > prevValue ? (double)100 / length : 0;
            bList.AddRounded(b);

            var bSum = bList.TakeLastExt(length).Sum();
            bSumList.AddRounded(bSum);

            var signal = GetRsiSignal(bSum - prevBSum1, prevBSum1 - prevBSum2, bSum, prevBSum1, 80, 20);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "GOsc", bSumList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = bSumList;
        stockData.IndicatorName = IndicatorName.GOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Gain Loss Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculateGainLossMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod,
        int length = 14, int signalLength = 7)
    {
        List<double> gainLossList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var gainLoss = currentValue + prevValue != 0 ? MinPastValues(i, 1, currentValue - prevValue) / ((currentValue + prevValue) / 2) * 100 : 0;
            gainLossList.AddRounded(gainLoss);
        }

        var gainLossAvgList = GetMovingAverageList(stockData, maType, length, gainLossList);
        var gainLossAvgSignalList = GetMovingAverageList(stockData, maType, signalLength, gainLossAvgList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var gainLossSignal = gainLossAvgSignalList[i];
            var prevGainLossSignal1 = i >= 1 ? gainLossAvgSignalList[i - 1] : 0;
            var prevGainLossSignal2 = i >= 2 ? gainLossAvgSignalList[i - 2] : 0;

            var signal = GetCompareSignal(gainLossSignal - prevGainLossSignal1, prevGainLossSignal1 - prevGainLossSignal2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Glma", gainLossAvgList },
            { "Signal", gainLossAvgSignalList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = gainLossAvgList;
        stockData.IndicatorName = IndicatorName.GainLossMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the High Low Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateHighLowIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 10)
    {
        List<double> hliList = new();
        List<double> advList = new();
        List<double> loList = new();
        List<double> advDiffList = new();
        List<Signal> signalsList = new();
        var (_, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevHighest = i >= 1 ? highestList[i - 1] : 0;
            var prevLowest = i >= 1 ? lowestList[i - 1] : 0;
            var highest = highestList[i];
            var lowest = lowestList[i];

            double adv = highest > prevHighest ? 1 : 0;
            advList.AddRounded(adv);

            double lo = lowest < prevLowest ? 1 : 0;
            loList.AddRounded(lo);

            var advSum = advList.TakeLastExt(length).Sum();
            var loSum = loList.TakeLastExt(length).Sum();

            var advDiff = advSum + loSum != 0 ? MinOrMax(advSum / (advSum + loSum) * 100, 100, 0) : 0;
            advDiffList.AddRounded(advDiff);
        }

        var zmbtiList = GetMovingAverageList(stockData, maType, length, advDiffList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var zmbti = zmbtiList[i];
            var prevZmbti1 = i >= 1 ? hliList[i - 1] : 0;
            var prevZmbti2 = i >= 2 ? hliList[i - 2] : 0;

            var signal = GetRsiSignal(zmbti - prevZmbti1, prevZmbti1 - prevZmbti2, zmbti, prevZmbti1, 70, 30);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Zmbti", zmbtiList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = zmbtiList;
        stockData.IndicatorName = IndicatorName.HighLowIndex;

        return stockData;
    }

    /// <summary>
    /// Calculates the Forecast Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateForecastOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 3)
    {
        List<double> pfList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var pf = currentValue != 0 ? 100 * MinPastValues(i, 1, currentValue - prevValue) / currentValue : 0;
            pfList.AddRounded(pf);
        }

        var pfSmaList = GetMovingAverageList(stockData, maType, length, pfList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var pfSma = pfSmaList[i];
            var prevPfSma = i >= 1 ? pfSmaList[i - 1] : 0;

            var signal = GetCompareSignal(pfSma, prevPfSma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Fo", pfList },
            { "Signal", pfSmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = pfList;
        stockData.IndicatorName = IndicatorName.ForecastOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Fast and Slow Kurtosis Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="ratio"></param>
    /// <returns></returns>
    public static StockData CalculateFastandSlowKurtosisOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length = 3, double ratio = 0.03)
    {
        List<double> fskList = new();
        List<double> momentumList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= length ? inputList[i - length] : 0;

            var prevMomentum = momentumList.LastOrDefault();
            var momentum = MinPastValues(i, length, currentValue - prevValue);
            momentumList.AddRounded(momentum);

            var prevFsk = fskList.LastOrDefault();
            var fsk = (ratio * (momentum - prevMomentum)) + ((1 - ratio) * prevFsk);
            fskList.AddRounded(fsk);
        }

        var fskSignalList = GetMovingAverageList(stockData, maType, length, fskList);
        for (var i = 0; i < fskSignalList.Count; i++)
        {
            var fsk = fskList[i];
            var fskSignal = fskSignalList[i];
            var prevFsk = i >= 1 ? fskList[i - 1] : 0;
            var prevFskSignal = i >= 1 ? fskSignalList[i - 1] : 0;

            var signal = GetCompareSignal(fsk - fskSignal, prevFsk - prevFskSignal);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Fsk", fskList },
            { "Signal", fskSignalList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = fskList;
        stockData.IndicatorName = IndicatorName.FastandSlowKurtosisOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Fast and Slow Relative Strength Index Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="length4"></param>
    /// <returns></returns>
    public static StockData CalculateFastandSlowRelativeStrengthIndexOscillator(this StockData stockData,
        MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length1 = 3, int length2 = 6, int length3 = 9, int length4 = 6)
    {
        List<double> fsrsiList = new();
        List<Signal> signalsList = new();

        var rsiList = CalculateRelativeStrengthIndex(stockData, maType, length: length3).CustomValuesList;
        var fskList = CalculateFastandSlowKurtosisOscillator(stockData, maType, length: length1).CustomValuesList;
        var v4List = GetMovingAverageList(stockData, maType, length2, fskList);

        for (var i = 0; i < v4List.Count; i++)
        {
            var rsi = rsiList[i];
            var v4 = v4List[i];

            var fsrsi = (10000 * v4) + rsi;
            fsrsiList.AddRounded(fsrsi);
        }

        var fsrsiSignalList = GetMovingAverageList(stockData, maType, length4, fsrsiList);
        for (var i = 0; i < fsrsiSignalList.Count; i++)
        {
            var fsrsi = fsrsiList[i];
            var fsrsiSignal = fsrsiSignalList[i];
            var prevFsrsi = i >= 1 ? fsrsiList[i - 1] : 0;
            var prevFsrsiSignal = i >= 1 ? fsrsiSignalList[i - 1] : 0;

            var signal = GetCompareSignal(fsrsi - fsrsiSignal, prevFsrsi - prevFsrsiSignal);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Fsrsi", fsrsiList },
            { "Signal", fsrsiSignalList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = fsrsiList;
        stockData.IndicatorName = IndicatorName.FastandSlowRelativeStrengthIndexOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Fast Slow Degree Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculateFastSlowDegreeOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 100, int fastLength = 3, int slowLength = 2, int signalLength = 14)
    {
        List<double> fastF1bList = new();
        List<double> fastF2bList = new();
        List<double> fastVWList = new();
        List<double> slowF1bList = new();
        List<double> slowF2bList = new();
        List<double> slowVWList = new();
        List<double> slowVWSumList = new();
        List<double> osList = new();
        List<double> histList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var fastF1x = (double)(i + 1) / length;
            var fastF1b = (double)1 / (i + 1) * Math.Sin(fastF1x * (i + 1) * Math.PI);
            fastF1bList.AddRounded(fastF1b);

            var fastF1bSum = fastF1bList.TakeLastExt(fastLength).Sum();
            var fastF1pol = (fastF1x * fastF1x) + fastF1bSum;
            var fastF2x = length != 0 ? (double)i / length : 0;
            var fastF2b = (double)1 / (i + 1) * Math.Sin(fastF2x * (i + 1) * Math.PI);
            fastF2bList.AddRounded(fastF2b);

            var fastF2bSum = fastF2bList.TakeLastExt(fastLength).Sum();
            var fastF2pol = (fastF2x * fastF2x) + fastF2bSum;
            var fastW = fastF1pol - fastF2pol;
            var fastVW = prevValue * fastW;
            fastVWList.AddRounded(fastVW);

            var fastVWSum = fastVWList.TakeLastExt(length).Sum();
            var slowF1x = length != 0 ? (double)(i + 1) / length : 0;
            var slowF1b = (double)1 / (i + 1) * Math.Sin(slowF1x * (i + 1) * Math.PI);
            slowF1bList.AddRounded(slowF1b);

            var slowF1bSum = slowF1bList.TakeLastExt(slowLength).Sum();
            var slowF1pol = (slowF1x * slowF1x) + slowF1bSum;
            var slowF2x = length != 0 ? (double)i / length : 0;
            var slowF2b = (double)1 / (i + 1) * Math.Sin(slowF2x * (i + 1) * Math.PI);
            slowF2bList.AddRounded(slowF2b);

            var slowF2bSum = slowF2bList.TakeLastExt(slowLength).Sum();
            var slowF2pol = (slowF2x * slowF2x) + slowF2bSum;
            var slowW = slowF1pol - slowF2pol;
            var slowVW = prevValue * slowW;
            slowVWList.AddRounded(slowVW);

            var slowVWSum = slowVWList.TakeLastExt(length).Sum();
            slowVWSumList.AddRounded(slowVWSum);

            var os = fastVWSum - slowVWSum;
            osList.AddRounded(os);
        }

        var osSignalList = GetMovingAverageList(stockData, maType, signalLength, osList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var os = osList[i];
            var osSignal = osSignalList[i];

            var prevHist = histList.LastOrDefault();
            var hist = os - osSignal;
            histList.AddRounded(hist);

            var signal = GetCompareSignal(hist, prevHist);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Fsdo", osList },
            { "Signal", osSignalList },
            { "Histogram", histList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = osList;
        stockData.IndicatorName = IndicatorName.FastSlowDegreeOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Fractal Chaos Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <returns></returns>
    public static StockData CalculateFractalChaosOscillator(this StockData stockData)
    {
        List<double> fcoList = new();
        List<Signal> signalsList = new();

        var fractalChaosBandsList = CalculateFractalChaosBands(stockData);
        var upperBandList = fractalChaosBandsList.OutputValues["UpperBand"];
        var lowerBandList = fractalChaosBandsList.OutputValues["LowerBand"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var upperBand = upperBandList[i];
            var prevUpperBand = i >= 1 ? upperBandList[i - 1] : 0;
            var lowerBand = lowerBandList[i];
            var prevLowerBand = i >= 1 ? lowerBandList[i - 1] : 0;

            var prevFco = fcoList.LastOrDefault();
            double fco = upperBand != prevUpperBand ? 1 : lowerBand != prevLowerBand ? -1 : 0;
            fcoList.AddRounded(fco);

            var signal = GetCompareSignal(fco, prevFco);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Fco", fcoList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = fcoList;
        stockData.IndicatorName = IndicatorName.FractalChaosOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Firefly Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateFireflyOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ZeroLagExponentialMovingAverage,
        int length = 10, int smoothLength = 3)
    {
        List<double> v2List = new();
        List<double> v5List = new();
        List<double> wwList = new();
        List<double> mmList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentClose = inputList[i];

            var v2 = (currentHigh + currentLow + (currentClose * 2)) / 4;
            v2List.AddRounded(v2);
        }

        var v3List = GetMovingAverageList(stockData, maType, length, v2List);
        stockData.CustomValuesList = v2List;
        var v4List = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var v2 = v2List[i];
            var v3 = v3List[i];
            var v4 = v4List[i];

            var v5 = v4 == 0 ? (v2 - v3) * 100 : (v2 - v3) * 100 / v4;
            v5List.AddRounded(v5);
        }

        var v6List = GetMovingAverageList(stockData, maType, smoothLength, v5List);
        var v7List = GetMovingAverageList(stockData, maType, smoothLength, v6List);
        var wwZLagEmaList = GetMovingAverageList(stockData, maType, length, v7List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var wwZlagEma = wwZLagEmaList[i];
            var prevWw1 = i >= 1 ? wwList[i - 1] : 0;
            var prevWw2 = i >= 2 ? wwList[i - 2] : 0;

            var ww = ((wwZlagEma + 100) / 2) - 4;
            wwList.AddRounded(ww);

            var mm = wwList.TakeLastExt(smoothLength).Max();
            mmList.AddRounded(mm);

            var signal = GetRsiSignal(ww - prevWw1, prevWw1 - prevWw2, ww, prevWw1, 80, 20);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Fo", wwList },
            { "Signal", mmList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = wwList;
        stockData.IndicatorName = IndicatorName.FireflyOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Fibonacci Retrace
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="factor"></param>
    /// <returns></returns>
    public static StockData CalculateFibonacciRetrace(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length1 = 15, int length2 = 50, double factor = 0.382)
    {
        List<double> hretList = new();
        List<double> lretList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length2);

        var wmaList = GetMovingAverageList(stockData, maType, length1, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var wma = wmaList[i];
            var highest = highestList[i];
            var lowest = lowestList[i];
            var prevWma = i >= 1 ? wmaList[i - 1] : 0;
            var retrace = (highest - lowest) * factor;

            var prevHret = hretList.LastOrDefault();
            var hret = highest - retrace;
            hretList.AddRounded(hret);

            var prevLret = lretList.LastOrDefault();
            var lret = lowest + retrace;
            lretList.AddRounded(lret);

            var signal = GetBullishBearishSignal(wma - hret, prevWma - prevHret, wma - lret, prevWma - prevLret);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", hretList },
            { "LowerBand", lretList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.FibonacciRetrace;

        return stockData;
    }

    /// <summary>
    /// Calculates the FX Sniper Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="cciLength"></param>
    /// <param name="t3Length"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    public static StockData CalculateFXSniperIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int cciLength = 14, int t3Length = 5, double b = 0.618)
    {
        List<double> e1List = new();
        List<double> e2List = new();
        List<double> e3List = new();
        List<double> e4List = new();
        List<double> e5List = new();
        List<double> e6List = new();
        List<double> fxSniperList = new();
        List<Signal> signalsList = new();

        var b2 = b * b;
        var b3 = b2 * b;
        var c1 = -b3;
        var c2 = 3 * (b2 + b3);
        var c3 = -3 * ((2 * b2) + b + b3);
        var c4 = 1 + (3 * b) + b3 + (3 * b2);
        var nr = 1 + (0.5 * (t3Length - 1));
        var w1 = 2 / (nr + 1);
        var w2 = 1 - w1;

        var cciList = CalculateCommodityChannelIndex(stockData, maType: maType, length: cciLength).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var cci = cciList[i];

            var prevE1 = e1List.LastOrDefault();
            var e1 = (w1 * cci) + (w2 * prevE1);
            e1List.AddRounded(e1);

            var prevE2 = e2List.LastOrDefault();
            var e2 = (w1 * e1) + (w2 * prevE2);
            e2List.AddRounded(e2);

            var prevE3 = e3List.LastOrDefault();
            var e3 = (w1 * e2) + (w2 * prevE3);
            e3List.AddRounded(e3);

            var prevE4 = e4List.LastOrDefault();
            var e4 = (w1 * e3) + (w2 * prevE4);
            e4List.AddRounded(e4);

            var prevE5 = e5List.LastOrDefault();
            var e5 = (w1 * e4) + (w2 * prevE5);
            e5List.AddRounded(e5);

            var prevE6 = e6List.LastOrDefault();
            var e6 = (w1 * e5) + (w2 * prevE6);
            e6List.AddRounded(e6);

            var prevFxSniper = fxSniperList.LastOrDefault();
            var fxsniper = (c1 * e6) + (c2 * e5) + (c3 * e4) + (c4 * e3);
            fxSniperList.AddRounded(fxsniper);

            var signal = GetCompareSignal(fxsniper, prevFxSniper);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "FXSniper", fxSniperList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = fxSniperList;
        stockData.IndicatorName = IndicatorName.FXSniperIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Fear and Greed Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateFearAndGreedIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int fastLength = 10, int slowLength = 30, int smoothLength = 2)
    {
        List<double> trUpList = new();
        List<double> trDnList = new();
        List<double> fgiList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var tr = CalculateTrueRange(currentHigh, currentLow, prevValue);

            var trUp = currentValue > prevValue ? tr : 0;
            trUpList.AddRounded(trUp);

            var trDn = currentValue < prevValue ? tr : 0;
            trDnList.AddRounded(trDn);
        }

        var fastTrUpList = GetMovingAverageList(stockData, maType, fastLength, trUpList);
        var fastTrDnList = GetMovingAverageList(stockData, maType, fastLength, trDnList);
        var slowTrUpList = GetMovingAverageList(stockData, maType, slowLength, trUpList);
        var slowTrDnList = GetMovingAverageList(stockData, maType, slowLength, trDnList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var fastTrUp = fastTrUpList[i];
            var fastTrDn = fastTrDnList[i];
            var slowTrUp = slowTrUpList[i];
            var slowTrDn = slowTrDnList[i];
            var fastDiff = fastTrUp - fastTrDn;
            var slowDiff = slowTrUp - slowTrDn;

            var fgi = fastDiff - slowDiff;
            fgiList.AddRounded(fgi);
        }

        var fgiEmaList = GetMovingAverageList(stockData, maType, smoothLength, fgiList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var fgiEma = fgiEmaList[i];
            var prevFgiEma = i >= 1 ? fgiEmaList[i - 1] : 0;

            var signal = GetCompareSignal(fgiEma, prevFgiEma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Fgi", fgiList },
            { "Signal", fgiEmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = fgiList;
        stockData.IndicatorName = IndicatorName.FearAndGreedIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Function To Candles
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateFunctionToCandles(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod,
        int length = 14)
    {
        List<double> tpList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, openList, _) = GetInputValuesList(stockData);

        stockData.CustomValuesList = inputList;
        var rsiCList = CalculateRelativeStrengthIndex(stockData, maType, length: length).CustomValuesList;
        stockData.CustomValuesList = openList;
        var rsiOList = CalculateRelativeStrengthIndex(stockData, maType, length: length).CustomValuesList;
        stockData.CustomValuesList = highList;
        var rsiHList = CalculateRelativeStrengthIndex(stockData, maType, length: length).CustomValuesList;
        stockData.CustomValuesList = lowList;
        var rsiLList = CalculateRelativeStrengthIndex(stockData, maType, length: length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var rsiC = rsiCList[i];
            var rsiO = rsiOList[i];
            var rsiH = rsiHList[i];
            var rsiL = rsiLList[i];
            var prevTp1 = i >= 1 ? tpList[i - 1] : 0;
            var prevTp2 = i >= 2 ? tpList[i - 2] : 0;

            var tp = (rsiC + rsiO + rsiH + rsiL) / 4;
            tpList.AddRounded(tp);

            var signal = GetCompareSignal(tp - prevTp1, prevTp1 - prevTp2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Close", rsiCList },
            { "Open", rsiOList },
            { "High", rsiHList },
            { "Low", rsiLList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.FunctionToCandles;

        return stockData;
    }

    /// <summary>
    /// Calculates the Karobein Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateKarobeinOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 50)
    {
        List<double> aList = new();
        List<double> bList = new();
        List<double> dList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var emaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var ema = emaList[i];
            var prevEma = i >= 1 ? emaList[i - 1] : 0;

            var a = ema < prevEma && prevEma != 0 ? ema / prevEma : 0;
            aList.AddRounded(a);

            var b = ema > prevEma && prevEma != 0 ? ema / prevEma : 0;
            bList.AddRounded(b);
        }

        var aEmaList = GetMovingAverageList(stockData, maType, length, aList);
        var bEmaList = GetMovingAverageList(stockData, maType, length, bList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var ema = emaList[i];
            var prevEma = i >= 1 ? emaList[i - 1] : 0;
            var a = aEmaList[i];
            var b = bEmaList[i];
            var prevD1 = i >= 1 ? dList[i - 1] : 0;
            var prevD2 = i >= 2 ? dList[i - 2] : 0;
            var c = prevEma != 0 && ema != 0 ? MinOrMax(ema / prevEma / ((ema / prevEma) + b), 1, 0) : 0;

            var d = prevEma != 0 && ema != 0 ? MinOrMax((2 * (ema / prevEma / ((ema / prevEma) + (c * a)))) - 1, 1, 0) : 0;
            dList.AddRounded(d);

            var signal = GetRsiSignal(d - prevD1, prevD1 - prevD2, d, prevD1, 0.8, 0.2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ko", dList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = dList;
        stockData.IndicatorName = IndicatorName.KarobeinOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Kase Peak Oscillator V1
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateKasePeakOscillatorV1(this StockData stockData, int length = 30, int smoothLength = 3)
    {
        List<double> diffList = new();
        List<double> lnList = new();
        List<Signal> signalsList = new();
        var (_, highList, lowList, _, _) = GetInputValuesList(stockData);

        var sqrt = Sqrt(length);

        var atrList = CalculateAverageTrueRange(stockData, length: length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentAtr = atrList[i];
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var prevLow = i >= length ? lowList[i - length] : 0;
            var prevHigh = i >= length ? highList[i - length] : 0;
            var rwh = currentAtr != 0 ? (currentHigh - prevLow) / currentAtr * sqrt : 0;
            var rwl = currentAtr != 0 ? (prevHigh - currentLow) / currentAtr * sqrt : 0;

            var diff = rwh - rwl;
            diffList.AddRounded(diff);
        }

        var pkList = GetMovingAverageList(stockData, MovingAvgType.WeightedMovingAverage, smoothLength, diffList);
        var mnList = GetMovingAverageList(stockData, MovingAvgType.SimpleMovingAverage, length, pkList);
        stockData.CustomValuesList = pkList;
        var sdList = CalculateStandardDeviationVolatility(stockData, length: length).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var pk = pkList[i];
            var mn = mnList[i];
            var sd = sdList[i];
            var prevPk = i >= 1 ? pkList[i - 1] : 0;
            var v1 = mn + (1.33 * sd) > 2.08 ? mn + (1.33 * sd) : 2.08;
            var v2 = mn - (1.33 * sd) < -1.92 ? mn - (1.33 * sd) : -1.92;

            var prevLn = lnList.LastOrDefault();
            var ln = prevPk >= 0 && pk > 0 ? v1 : prevPk <= 0 && pk < 0 ? v2 : 0;
            lnList.AddRounded(ln);

            var signal = GetCompareSignal(ln, prevLn);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Kpo", lnList },
            { "Pk", pkList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = lnList;
        stockData.IndicatorName = IndicatorName.KasePeakOscillatorV1;

        return stockData;
    }

    /// <summary>
    /// Calculates the Kase Peak Oscillator V2
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="smoothLength"></param>
    /// <param name="devFactor"></param>
    /// <param name="sensitivity"></param>
    /// <returns></returns>
    public static StockData CalculateKasePeakOscillatorV2(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int fastLength = 8, int slowLength = 65, int length1 = 9, int length2 = 30, int length3 = 50, int smoothLength = 3, double devFactor = 2,
        double sensitivity = 40)
    {
        List<double> ccLogList = new();
        List<double> xpAbsAvgList = new();
        List<double> kpoBufferList = new();
        List<double> x1List = new();
        List<double> x2List = new();
        List<double> xpList = new();
        List<double> xpAbsList = new();
        List<double> kppBufferList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var temp = prevValue != 0 ? currentValue / prevValue : 0;

            var ccLog = temp > 0 ? Math.Log(temp) : 0;
            ccLogList.AddRounded(ccLog);
        }

        stockData.CustomValuesList = ccLogList;
        var ccDevList = CalculateStandardDeviationVolatility(stockData, maType, length1).CustomValuesList;
        var ccDevAvgList = GetMovingAverageList(stockData, maType, length2, ccDevList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var avg = ccDevAvgList[i];
            var currentLow = lowList[i];
            var currentHigh = highList[i];

            double max1 = 0, max2 = 0;
            for (var j = fastLength; j < slowLength; j++)
            {
                var sqrtK = Sqrt(j);
                var prevLow = i >= j ? lowList[i - j] : 0;
                var prevHigh = i >= j ? highList[i - j] : 0;
                var temp1 = prevLow != 0 ? currentHigh / prevLow : 0;
                var log1 = temp1 > 0 ? Math.Log(temp1) : 0;
                max1 = Math.Max(log1 / sqrtK, max1);
                var temp2 = currentLow != 0 ? prevHigh / currentLow : 0;
                var log2 = temp2 > 0 ? Math.Log(temp2) : 0;
                max2 = Math.Max(log2 / sqrtK, max2);
            }

            var x1 = avg != 0 ? max1 / avg : 0;
            x1List.AddRounded(x1);

            var x2 = avg != 0 ? max2 / avg : 0;
            x2List.AddRounded(x2);

            var xp = sensitivity * (x1List.TakeLastExt(smoothLength).Average() - x2List.TakeLastExt(smoothLength).Average());
            xpList.AddRounded(xp);

            var xpAbs = Math.Abs(xp);
            xpAbsList.AddRounded(xpAbs);

            var xpAbsAvg = xpAbsList.TakeLastExt(length3).Average();
            xpAbsAvgList.AddRounded(xpAbsAvg);
        }

        stockData.CustomValuesList = xpAbsList;
        var xpAbsStdDevList = CalculateStandardDeviationVolatility(stockData, maType, length3).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var xpAbsAvg = xpAbsAvgList[i];
            var xpAbsStdDev = xpAbsStdDevList[i];
            var prevKpoBuffer1 = i >= 1 ? kpoBufferList[i - 1] : 0;
            var prevKpoBuffer2 = i >= 2 ? kpoBufferList[i - 2] : 0;

            var tmpVal = xpAbsAvg + (devFactor * xpAbsStdDev);
            var maxVal = Math.Max(90, tmpVal);

            var prevKpoBuffer = kpoBufferList.LastOrDefault();
            var kpoBuffer = xpList[i];
            kpoBufferList.AddRounded(kpoBuffer);

            var kppBuffer = prevKpoBuffer1 > 0 && prevKpoBuffer1 > kpoBuffer && prevKpoBuffer1 >= prevKpoBuffer2 &&
                            prevKpoBuffer1 >= maxVal ? prevKpoBuffer1 : prevKpoBuffer1 < 0 && prevKpoBuffer1 < kpoBuffer &&
                                                                        prevKpoBuffer1 <= prevKpoBuffer2 && prevKpoBuffer1 <= maxVal * -1 ? prevKpoBuffer1 :
                prevKpoBuffer1 > 0 && prevKpoBuffer1 > kpoBuffer && prevKpoBuffer1 >= prevKpoBuffer2 ? prevKpoBuffer1 :
                prevKpoBuffer1 < 0 && prevKpoBuffer1 < kpoBuffer && prevKpoBuffer1 <= prevKpoBuffer2 ? prevKpoBuffer1 : 0;
            kppBufferList.AddRounded(kppBuffer);

            var signal = GetCompareSignal(kpoBuffer, prevKpoBuffer);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Kpo", xpList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = xpList;
        stockData.IndicatorName = IndicatorName.KasePeakOscillatorV2;

        return stockData;
    }

    /// <summary>
    /// Calculates the Kase Serial Dependency Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateKaseSerialDependencyIndex(this StockData stockData, int length = 14)
    {
        List<double> ksdiUpList = new();
        List<double> ksdiDownList = new();
        List<double> tempList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var temp = prevValue != 0 ? currentValue / prevValue : 0;

            var tempLog = temp > 0 ? Math.Log(temp) : 0;
            tempList.AddRounded(tempLog);
        }

        stockData.CustomValuesList = tempList;
        var stdDevList = CalculateStandardDeviationVolatility(stockData, length: length).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var volatility = stdDevList[i];
            var prevHigh = i >= length ? highList[i - length] : 0;
            var prevLow = i >= length ? lowList[i - length] : 0;
            var ksdiUpTemp = prevLow != 0 ? currentHigh / prevLow : 0;
            var ksdiDownTemp = prevHigh != 0 ? currentLow / prevHigh : 0;
            var ksdiUpLog = ksdiUpTemp > 0 ? Math.Log(ksdiUpTemp) : 0;
            var ksdiDownLog = ksdiDownTemp > 0 ? Math.Log(ksdiDownTemp) : 0;

            var prevKsdiUp = ksdiUpList.LastOrDefault();
            var ksdiUp = volatility != 0 ? ksdiUpLog / volatility : 0;
            ksdiUpList.AddRounded(ksdiUp);

            var prevKsdiDown = ksdiDownList.LastOrDefault();
            var ksdiDown = volatility != 0 ? ksdiDownLog / volatility : 0;
            ksdiDownList.AddRounded(ksdiDown);

            var signal = GetCompareSignal(ksdiUp - ksdiDown, prevKsdiUp - prevKsdiDown);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "KsdiUp", ksdiUpList },
            { "KsdiDn", ksdiDownList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.KaseSerialDependencyIndex;

        return stockData;
    }

    /// <summary>
    /// Calculates the Kaufman Binary Wave
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="fastSc"></param>
    /// <param name="slowSc"></param>
    /// <param name="filterPct"></param>
    /// <returns></returns>
    public static StockData CalculateKaufmanBinaryWave(this StockData stockData, int length = 20, double fastSc = 0.6022, double slowSc = 0.0645,
        double filterPct = 10)
    {
        List<double> amaList = new();
        List<double> diffList = new();
        List<double> amaLowList = new();
        List<double> amaHighList = new();
        List<double> bwList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var efRatioList = CalculateKaufmanAdaptiveMovingAverage(stockData, length: length).OutputValues["Er"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var efRatio = efRatioList[i];
            var prevAma = i >= 1 ? amaList[i - 1] : currentValue;
            var smooth = Pow((efRatio * fastSc) + slowSc, 2);

            var ama = prevAma + (smooth * (currentValue - prevAma));
            amaList.AddRounded(ama);

            var diff = ama - prevAma;
            diffList.AddRounded(diff);
        }

        stockData.CustomValuesList = diffList;
        var diffStdDevList = CalculateStandardDeviationVolatility(stockData, length: length).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var ama = amaList[i];
            var diffStdDev = diffStdDevList[i];
            var prevAma = i >= 1 ? amaList[i - 1] : currentValue;
            var filter = filterPct / 100 * diffStdDev;

            var prevAmaLow = amaLowList.LastOrDefault();
            var amaLow = ama < prevAma ? ama : prevAmaLow;
            amaLowList.AddRounded(amaLow);

            var prevAmaHigh = amaHighList.LastOrDefault();
            var amaHigh = ama > prevAma ? ama : prevAmaHigh;
            amaHighList.AddRounded(amaHigh);

            var prevBw = bwList.LastOrDefault();
            double bw = ama - amaLow > filter ? 1 : amaHigh - ama > filter ? -1 : 0;
            bwList.AddRounded(bw);

            var signal = GetCompareSignal(bw, prevBw);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Kbw", bwList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = bwList;
        stockData.IndicatorName = IndicatorName.KaufmanBinaryWave;

        return stockData;
    }

    /// <summary>
    /// Calculates the Kurtosis Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <returns></returns>
    public static StockData CalculateKurtosisIndicator(this StockData stockData, int length1 = 3, int length2 = 1, int fastLength = 3,
        int slowLength = 65)
    {
        List<double> diffList = new();
        List<double> kList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= length1 ? inputList[i - length1] : 0;
            var prevDiff = i >= length2 ? diffList[i - length2] : 0;

            var diff = MinPastValues(i, length1, currentValue - prevValue);
            diffList.AddRounded(diff);

            var k = MinPastValues(i, length2, diff - prevDiff);
            kList.AddRounded(k);
        }

        var fkList = GetMovingAverageList(stockData, MovingAvgType.ExponentialMovingAverage, slowLength, kList);
        var fskList = GetMovingAverageList(stockData, MovingAvgType.WeightedMovingAverage, fastLength, fkList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var fsk = fskList[i];
            var prevFsk = i >= 1 ? fskList[i - 1] : 0;

            var signal = GetCompareSignal(fsk, prevFsk);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Fk", fkList },
            { "Signal", fskList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = fkList;
        stockData.IndicatorName = IndicatorName.KurtosisIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Kaufman Adaptive Correlation Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateKaufmanAdaptiveCorrelationOscillator(this StockData stockData,
        MovingAvgType maType = MovingAvgType.KaufmanAdaptiveMovingAverage, int length = 14)
    {
        List<double> indexList = new();
        List<double> index2List = new();
        List<double> src2List = new();
        List<double> srcStList = new();
        List<double> indexStList = new();
        List<double> indexSrcList = new();
        List<double> rList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var kamaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];

            double index = i;
            indexList.AddRounded(index);

            var indexSrc = i * currentValue;
            indexSrcList.AddRounded(indexSrc);

            var srcSrc = currentValue * currentValue;
            src2List.AddRounded(srcSrc);

            var indexIndex = index * index;
            index2List.AddRounded(indexIndex);
        }

        var indexMaList = GetMovingAverageList(stockData, maType, length, indexList);
        var indexSrcMaList = GetMovingAverageList(stockData, maType, length, indexSrcList);
        var index2MaList = GetMovingAverageList(stockData, maType, length, index2List);
        var src2MaList = GetMovingAverageList(stockData, maType, length, src2List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var srcMa = kamaList[i];
            var indexMa = indexMaList[i];
            var indexSrcMa = indexSrcMaList[i];
            var index2Ma = index2MaList[i];
            var src2Ma = src2MaList[i];
            var prevR1 = i >= 1 ? rList[i - 1] : 0;
            var prevR2 = i >= 2 ? rList[i - 2] : 0;

            var indexSqrt = index2Ma - Pow(indexMa, 2);
            var indexSt = indexSqrt >= 0 ? Sqrt(indexSqrt) : 0;
            indexStList.AddRounded(indexSt);

            var srcSqrt = src2Ma - Pow(srcMa, 2);
            var srcSt = srcSqrt >= 0 ? Sqrt(srcSqrt) : 0;
            srcStList.AddRounded(srcSt);

            var a = indexSrcMa - (indexMa * srcMa);
            var b = indexSt * srcSt;

            var r = b != 0 ? a / b : 0;
            rList.AddRounded(r);

            var signal = GetRsiSignal(r - prevR1, prevR1 - prevR2, r, prevR1, 0.5, -0.5);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "IndexSt", indexStList },
            { "SrcSt", srcStList },
            { "Kaco", rList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = rList;
        stockData.IndicatorName = IndicatorName.KaufmanAdaptiveCorrelationOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Know Sure Thing Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="length4"></param>
    /// <param name="rocLength1"></param>
    /// <param name="rocLength2"></param>
    /// <param name="rocLength3"></param>
    /// <param name="rocLength4"></param>
    /// <param name="signalLength"></param>
    /// <param name="weight1"></param>
    /// <param name="weight2"></param>
    /// <param name="weight3"></param>
    /// <param name="weight4"></param>
    /// <returns></returns>
    public static StockData CalculateKnowSureThing(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 10,
        int length2 = 10, int length3 = 10, int length4 = 15, int rocLength1 = 10, int rocLength2 = 15, int rocLength3 = 20, int rocLength4 = 30,
        int signalLength = 9, double weight1 = 1, double weight2 = 2, double weight3 = 3, double weight4 = 4)
    {
        List<double> kstList = new();
        List<Signal> signalsList = new();

        var roc1List = CalculateRateOfChange(stockData, rocLength1).CustomValuesList;
        var roc2List = CalculateRateOfChange(stockData, rocLength2).CustomValuesList;
        var roc3List = CalculateRateOfChange(stockData, rocLength3).CustomValuesList;
        var roc4List = CalculateRateOfChange(stockData, rocLength4).CustomValuesList;
        var roc1SmaList = GetMovingAverageList(stockData, maType, length1, roc1List);
        var roc2SmaList = GetMovingAverageList(stockData, maType, length2, roc2List);
        var roc3SmaList = GetMovingAverageList(stockData, maType, length3, roc3List);
        var roc4SmaList = GetMovingAverageList(stockData, maType, length4, roc4List);

        for (var i = 0; i < stockData.Count; i++)
        {
            var roc1 = roc1SmaList[i];
            var roc2 = roc2SmaList[i];
            var roc3 = roc3SmaList[i];
            var roc4 = roc4SmaList[i];

            var kst = (roc1 * weight1) + (roc2 * weight2) + (roc3 * weight3) + (roc4 * weight4);
            kstList.AddRounded(kst);
        }

        var kstSignalList = GetMovingAverageList(stockData, maType, signalLength, kstList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var kst = kstList[i];
            var kstSignal = kstSignalList[i];
            var prevKst = i >= 1 ? kstList[i - 1] : 0;
            var prevKstSignal = i >= 1 ? kstSignalList[i - 1] : 0;

            var signal = GetCompareSignal(kst - kstSignal, prevKst - prevKstSignal);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Kst", kstList },
            { "Signal", kstSignalList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = kstList;
        stockData.IndicatorName = IndicatorName.KnowSureThing;

        return stockData;
    }

    /// <summary>
    /// Calculates the Kase Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateKaseIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 10)
    {
        List<double> kUpList = new();
        List<double> kDownList = new();
        List<Signal> signalsList = new();
        var (_, highList, lowList, _, volumeList) = GetInputValuesList(stockData);

        var sqrtPeriod = Sqrt(length);

        var volumeSmaList = GetMovingAverageList(stockData, maType, length, volumeList);
        var atrList = CalculateAverageTrueRange(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var avgTrueRange = atrList[i];
            var avgVolSma = volumeSmaList[i];
            var prevHigh = i >= 1 ? highList[i - 1] : 0;
            var prevLow = i >= 1 ? lowList[i - 1] : 0;
            var ratio = avgVolSma * sqrtPeriod;

            var prevKUp = kUpList.LastOrDefault();
            var kUp = avgTrueRange > 0 && ratio != 0 && currentLow != 0 ? prevHigh / currentLow / ratio : prevKUp;
            kUpList.AddRounded(kUp);

            var prevKDown = kDownList.LastOrDefault();
            var kDown = avgTrueRange > 0 && ratio != 0 && prevLow != 0 ? currentHigh / prevLow / ratio : prevKDown;
            kDownList.AddRounded(kDown);

            var signal = GetCompareSignal(kUp - kDown, prevKUp - prevKDown);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "KaseUp", kUpList },
            { "KaseDn", kDownList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.KaseIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Kendall Rank Correlation Coefficient
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateKendallRankCorrelationCoefficient(this StockData stockData, int length = 20)
    {
        List<double> tempList = new();
        List<double> numeratorList = new();
        List<double> tempLinRegList = new();
        List<double> pearsonCorrelationList = new();
        List<double> kendallCorrelationList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var linRegList = CalculateLinearRegression(stockData, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevKendall1 = i >= 1 ? kendallCorrelationList[i - 1] : 0;
            var prevKendall2 = i >= 2 ? kendallCorrelationList[i - 2] : 0;

            var currentValue = inputList[i];
            tempList.AddRounded(currentValue);

            var linReg = linRegList[i];
            tempLinRegList.AddRounded(linReg);

            var pearsonCorrelation = Correlation.Pearson(tempLinRegList.TakeLastExt(length).Select(x => (double)x),
                tempList.TakeLastExt(length).Select(x => (double)x));
            pearsonCorrelation = IsValueNullOrInfinity(pearsonCorrelation) ? 0 : pearsonCorrelation;
            pearsonCorrelationList.AddRounded((double)pearsonCorrelation);

            var totalPairs = length * (double)(length - 1) / 2;
            double numerator = 0;
            for (var j = 0; j <= length - 1; j++)
            {
                for (var k = 0; k <= j; k++)
                {
                    var prevValueJ = i >= j ? inputList[i - j] : 0;
                    var prevValueK = i >= k ? inputList[i - k] : 0;
                    var prevLinRegJ = i >= j ? linRegList[i - j] : 0;
                    var prevLinRegK = i >= k ? linRegList[i - k] : 0;
                    numerator += Math.Sign(prevLinRegJ - prevLinRegK) * Math.Sign(prevValueJ - prevValueK);
                }
            }

            var kendallCorrelation = numerator / totalPairs;
            kendallCorrelationList.AddRounded(kendallCorrelation);

            var signal = GetCompareSignal(kendallCorrelation - prevKendall1, prevKendall1 - prevKendall2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Krcc", kendallCorrelationList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = kendallCorrelationList;
        stockData.IndicatorName = IndicatorName.KendallRankCorrelationCoefficient;

        return stockData;
    }

    /// <summary>
    /// Calculates the Kwan Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateKwanIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int length = 9, 
        int smoothLength = 2)
    {
        List<double> vrList = new();
        List<double> prevList = new();
        List<double> knrpList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        var rsiList = CalculateRelativeStrengthIndex(stockData, maType, length: length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentClose = inputList[i];
            var priorClose = i >= length ? inputList[i - length] : 0;
            var mom = priorClose != 0 ? currentClose / priorClose * 100 : 0;
            var rsi = rsiList[i];
            var hh = highestList[i];
            var ll = lowestList[i];
            var sto = hh - ll != 0 ? (currentClose - ll) / (hh - ll) * 100 : 0;
            var prevVr = i >= smoothLength ? vrList[i - smoothLength] : 0;
            var prevKnrp1 = i >= 1 ? knrpList[i - 1] : 0;
            var prevKnrp2 = i >= 2 ? knrpList[i - 2] : 0;

            var vr = mom != 0 ? sto * rsi / mom : 0;
            vrList.AddRounded(vr);

            var prev = prevVr;
            prevList.AddRounded(prev);

            var vrSum = prevList.Sum();
            var knrp = vrSum / smoothLength;
            knrpList.AddRounded(knrp);

            var signal = GetCompareSignal(knrp - prevKnrp1, prevKnrp1 - prevKnrp2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ki", knrpList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = knrpList;
        stockData.IndicatorName = IndicatorName.KwanIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Kaufman Stress Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="marketData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateKaufmanStressIndicator(this StockData stockData, StockData marketData, int length = 60)
    {
        List<double> svList = new();
        List<double> dList = new();
        List<Signal> signalsList = new();
        var (inputList1, highList1, lowList1, _, _) = GetInputValuesList(stockData);
        var (inputList2, highList2, lowList2, _, _) = GetInputValuesList(marketData);
        var (highestList1, lowestList1) = GetMaxAndMinValuesList(highList1, lowList1, length);
        var (highestList2, lowestList2) = GetMaxAndMinValuesList(highList2, lowList2, length);

        if (stockData.Count == marketData.Count)
        {
            for (var i = 0; i < stockData.Count; i++)
            {
                var highestHigh1 = highestList1[i];
                var lowestLow1 = lowestList1[i];
                var highestHigh2 = highestList2[i];
                var lowestLow2 = lowestList2[i];
                var currentValue1 = inputList1[i];
                var currentValue2 = inputList2[i];
                var prevSv1 = i >= 1 ? svList[i - 1] : 0;
                var prevSv2 = i >= 2 ? svList[i - 2] : 0;
                var r1 = highestHigh1 - lowestLow1;
                var r2 = highestHigh2 - lowestLow2;
                var s1 = r1 != 0 ? (currentValue1 - lowestLow1) / r1 : 50;
                var s2 = r2 != 0 ? (currentValue2 - lowestLow2) / r2 : 50;

                var d = s1 - s2;
                dList.AddRounded(d);

                var list = dList.TakeLastExt(length).ToList();
                var highestD = list.Max();
                var lowestD = list.Min();
                var r11 = highestD - lowestD;

                var sv = r11 != 0 ? MinOrMax(100 * (d - lowestD) / r11, 100, 0) : 50;
                svList.AddRounded(sv);

                var signal = GetRsiSignal(sv - prevSv1, prevSv1 - prevSv2, sv, prevSv1, 90, 10);
                signalsList.Add(signal);
            }
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ksi", svList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = svList;
        stockData.IndicatorName = IndicatorName.KaufmanStressIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Vostro Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="level"></param>
    /// <returns></returns>
    public static StockData CalculateVostroIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length1 = 5, int length2 = 100, double level = 8)
    {
        List<double> tempList = new();
        List<double> rangeList = new();
        List<double> iBuff116List = new();
        List<double> iBuff112List = new();
        List<double> iBuff109List = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        var wmaList = GetMovingAverageList(stockData, maType, length2, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var wma = wmaList[i];
            var prevBuff109_1 = i >= 1 ? iBuff109List[i - 1] : 0;
            var prevBuff109_2 = i >= 2 ? iBuff109List[i - 2] : 0;

            var medianPrice = inputList[i];
            tempList.AddRounded(medianPrice);

            var range = currentHigh - currentLow;
            rangeList.AddRounded(range);

            var gd120 = tempList.TakeLastExt(length1).Sum();
            var gd128 = gd120 * 0.2;
            var gd121 = rangeList.TakeLastExt(length1).Sum();
            var gd136 = gd121 * 0.2 * 0.2;

            var prevIBuff116 = iBuff116List.LastOrDefault();
            var iBuff116 = gd136 != 0 ? (currentLow - gd128) / gd136 : 0;
            iBuff116List.AddRounded(iBuff116);

            var prevIBuff112 = iBuff112List.LastOrDefault();
            var iBuff112 = gd136 != 0 ? (currentHigh - gd128) / gd136 : 0;
            iBuff112List.AddRounded(iBuff112);

            double iBuff108 = iBuff112 > level && currentHigh > wma ? 90 : iBuff116 < -level && currentLow < wma ? -90 : 0;
            var iBuff109 = (iBuff112 > level && prevIBuff112 > level) || (iBuff116 < -level && prevIBuff116 < -level) ? 0 : iBuff108;
            iBuff109List.AddRounded(iBuff109);

            var signal = GetRsiSignal(iBuff109 - prevBuff109_1, prevBuff109_1 - prevBuff109_2, iBuff109, prevBuff109_1, 80, -80);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Vi", iBuff109List }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = iBuff109List;
        stockData.IndicatorName = IndicatorName.VostroIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Value Chart Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="inputName"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateValueChartIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
       InputName inputName = InputName.MedianPrice, int length = 5)
    {
        List<double> vOpenList = new();
        List<double> vHighList = new();
        List<double> vLowList = new();
        List<double> vCloseList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, openList, closeList, _) = GetInputValuesList(inputName, stockData);

        var varp = MinOrMax((int)Math.Ceiling((double)length / 5));

        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, varp);
        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentClose = closeList[i];
            var prevClose1 = i >= 1 ? closeList[i - 1] : 0;
            var prevHighest1 = i >= 1 ? highestList[i - 1] : 0;
            var prevLowest1 = i >= 1 ? lowestList[i - 1] : 0;
            var prevClose2 = i >= 2 ? closeList[i - 2] : 0;
            var prevHighest2 = i >= 2 ? highestList[i - 2] : 0;
            var prevLowest2 = i >= 2 ? lowestList[i - 2] : 0;
            var prevClose3 = i >= 3 ? closeList[i - 3] : 0;
            var prevHighest3 = i >= 3 ? highestList[i - 3] : 0;
            var prevLowest3 = i >= 3 ? lowestList[i - 3] : 0;
            var prevClose4 = i >= 4 ? closeList[i - 4] : 0;
            var prevHighest4 = i >= 4 ? highestList[i - 4] : 0;
            var prevLowest4 = i >= 4 ? lowestList[i - 4] : 0;
            var prevClose5 = i >= 5 ? closeList[i - 5] : 0;
            var mba = smaList[i];
            var highest = highestList[i];
            var lowest = lowestList[i];
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentOpen = openList[i];
            var vara = highest - lowest;
            var varr1 = vara == 0 && varp == 1 ? Math.Abs(currentClose - prevClose1) : vara;
            var varb = prevHighest1 - prevLowest1;
            var varr2 = varb == 0 && varp == 1 ? Math.Abs(prevClose1 - prevClose2) : varb;
            var varc = prevHighest2 - prevLowest2;
            var varr3 = varc == 0 && varp == 1 ? Math.Abs(prevClose2 - prevClose3) : varc;
            var vard = prevHighest3 - prevLowest3;
            var varr4 = vard == 0 && varp == 1 ? Math.Abs(prevClose3 - prevClose4) : vard;
            var vare = prevHighest4 - prevLowest4;
            var varr5 = vare == 0 && varp == 1 ? Math.Abs(prevClose4 - prevClose5) : vare;
            var cdelta = Math.Abs(currentClose - prevClose1);
            var var0 = cdelta > currentHigh - currentLow || currentHigh == currentLow ? cdelta : currentHigh - currentLow;
            var lRange = (varr1 + varr2 + varr3 + varr4 + varr5) / 5 * 0.2;

            var vClose = lRange != 0 ? (currentClose - mba) / lRange : 0;
            vCloseList.AddRounded(vClose);

            var vOpen = lRange != 0 ? (currentOpen - mba) / lRange : 0;
            vOpenList.AddRounded(vOpen);

            var vHigh = lRange != 0 ? (currentHigh - mba) / lRange : 0;
            vHighList.AddRounded(vHigh);

            var vLow = lRange != 0 ? (currentLow - mba) / lRange : 0;
            vLowList.AddRounded(vLow);
        }

        var vValueEmaList = GetMovingAverageList(stockData, maType, length, vCloseList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var vValue = vCloseList[i];
            var vValueEma = vValueEmaList[i];
            var prevVvalue = i >= 1 ? vCloseList[i - 1] : 0;
            var prevVValueEma = i >= 1 ? vValueEmaList[i - 1] : 0;

            var signal = GetRsiSignal(vValue - vValueEma, prevVvalue - prevVValueEma, vValue, prevVvalue, 4, -4);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "vClose", vCloseList },
            { "vOpen", vOpenList },
            { "vHigh", vHighList },
            { "vLow", vLowList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.ValueChartIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Vervoort Smoothed Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="inputName"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="smoothLength"></param>
    /// <param name="stdDevMult"></param>
    /// <returns></returns>
    public static StockData CalculateVervoortSmoothedOscillator(this StockData stockData, InputName inputName = InputName.TypicalPrice,
        int length1 = 18, int length2 = 30, int length3 = 2, int smoothLength = 3, double stdDevMult = 2)
    {
        List<double> rainbowList = new();
        List<double> zlrbList = new();
        List<double> zlrbpercbList = new();
        List<double> rbcList = new();
        List<double> fastKList = new();
        List<double> skList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, closeList, _) = GetInputValuesList(inputName, stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length2);

        var r1List = GetMovingAverageList(stockData, MovingAvgType.SimpleMovingAverage, length3, closeList);
        var r2List = GetMovingAverageList(stockData, MovingAvgType.SimpleMovingAverage, length3, r1List);
        var r3List = GetMovingAverageList(stockData, MovingAvgType.SimpleMovingAverage, length3, r2List);
        var r4List = GetMovingAverageList(stockData, MovingAvgType.SimpleMovingAverage, length3, r3List);
        var r5List = GetMovingAverageList(stockData, MovingAvgType.SimpleMovingAverage, length3, r4List);
        var r6List = GetMovingAverageList(stockData, MovingAvgType.SimpleMovingAverage, length3, r5List);
        var r7List = GetMovingAverageList(stockData, MovingAvgType.SimpleMovingAverage, length3, r6List);
        var r8List = GetMovingAverageList(stockData, MovingAvgType.SimpleMovingAverage, length3, r7List);
        var r9List = GetMovingAverageList(stockData, MovingAvgType.SimpleMovingAverage, length3, r8List);
        var r10List = GetMovingAverageList(stockData, MovingAvgType.SimpleMovingAverage, length3, r9List);

        for (var i = 0; i < stockData.Count; i++)
        {
            var r1 = r1List[i];
            var r2 = r2List[i];
            var r3 = r3List[i];
            var r4 = r4List[i];
            var r5 = r5List[i];
            var r6 = r6List[i];
            var r7 = r7List[i];
            var r8 = r8List[i];
            var r9 = r9List[i];
            var r10 = r10List[i];

            var rainbow = ((5 * r1) + (4 * r2) + (3 * r3) + (2 * r4) + r5 + r6 + r7 + r8 + r9 + r10) / 20;
            rainbowList.AddRounded(rainbow);
        }

        var ema1List = GetMovingAverageList(stockData, MovingAvgType.ExponentialMovingAverage, smoothLength, rainbowList);
        var ema2List = GetMovingAverageList(stockData, MovingAvgType.ExponentialMovingAverage, smoothLength, ema1List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var ema1 = ema1List[i];
            var ema2 = ema2List[i];

            var zlrb = (2 * ema1) - ema2;
            zlrbList.AddRounded(zlrb);
        }

        var tzList = GetMovingAverageList(stockData, MovingAvgType.TripleExponentialMovingAverage, smoothLength, zlrbList);
        stockData.CustomValuesList = tzList;
        var hwidthList = CalculateStandardDeviationVolatility(stockData, length: length1).CustomValuesList;
        var wmatzList = GetMovingAverageList(stockData, MovingAvgType.WeightedMovingAverage, length1, tzList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentTypicalPrice = inputList[i];
            var rainbow = rainbowList[i];
            var tz = tzList[i];
            var hwidth = hwidthList[i];
            var wmatz = wmatzList[i];
            var highest = highestList[i];
            var lowest = lowestList[i];

            var prevZlrbpercb = zlrbpercbList.LastOrDefault();
            var zlrbpercb = hwidth != 0 ? (tz + (stdDevMult * hwidth) - wmatz) / (2 * stdDevMult * hwidth * 100) : 0;
            zlrbpercbList.AddRounded(zlrbpercb);

            var rbc = (rainbow + currentTypicalPrice) / 2;
            rbcList.AddRounded(rbc);

            var lowestRbc = rbcList.TakeLastExt(length2).Min();
            var nom = rbc - lowest;
            var den = highest - lowestRbc;

            var fastK = den != 0 ? MinOrMax(100 * nom / den, 100, 0) : 0;
            fastKList.AddRounded(fastK);

            var prevSk = skList.LastOrDefault();
            var sk = fastKList.TakeLastExt(smoothLength).Average();
            skList.AddRounded(sk);

            var signal = GetConditionSignal(sk > prevSk && zlrbpercb > prevZlrbpercb, sk < prevSk && zlrbpercb < prevZlrbpercb);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Vso", zlrbpercbList },
            { "Sk", skList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = zlrbpercbList;
        stockData.IndicatorName = IndicatorName.VervoortSmoothedOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Vervoort Heiken Ashi Long Term Candlestick Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="inputName"></param>
    /// <param name="length"></param>
    /// <param name="factor"></param>
    /// <returns></returns>
    public static StockData CalculateVervoortHeikenAshiLongTermCandlestickOscillator(this StockData stockData,
        MovingAvgType maType = MovingAvgType.TripleExponentialMovingAverage, InputName inputName = InputName.FullTypicalPrice, int length = 55,
        double factor = 1.1)
    {
        List<double> haoList = new();
        List<double> hacList = new();
        List<double> medianPriceList = new();
        List<bool> keepN1List = new();
        List<bool> keepAll1List = new();
        List<bool> keepN2List = new();
        List<bool> keepAll2List = new();
        List<bool> utrList = new();
        List<bool> dtrList = new();
        List<double> hacoList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, openList, closeList, _) = GetInputValuesList(inputName, stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevHao = haoList.LastOrDefault();
            var hao = (prevValue + prevHao) / 2;
            haoList.AddRounded(hao);

            var hac = (currentValue + hao + Math.Max(currentHigh, hao) + Math.Min(currentLow, hao)) / 4;
            hacList.AddRounded(hac);

            var medianPrice = (currentHigh + currentLow) / 2;
            medianPriceList.AddRounded(medianPrice);
        }

        var tacList = GetMovingAverageList(stockData, maType, length, hacList);
        var thl2List = GetMovingAverageList(stockData, maType, length, medianPriceList);
        var tacTemaList = GetMovingAverageList(stockData, maType, length, tacList);
        var thl2TemaList = GetMovingAverageList(stockData, maType, length, thl2List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var tac = tacList[i];
            var tacTema = tacTemaList[i];
            var thl2 = thl2List[i];
            var thl2Tema = thl2TemaList[i];
            var currentOpen = openList[i];
            var currentClose = closeList[i];
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var hac = hacList[i];
            var hao = haoList[i];
            var prevHac = i >= 1 ? hacList[i - 1] : 0;
            var prevHao = i >= 1 ? haoList[i - 1] : 0;
            var prevHigh = i >= 1 ? highList[i - 1] : 0;
            var prevLow = i >= 1 ? lowList[i - 1] : 0;
            var prevClose = i >= 1 ? closeList[i - 1] : 0;
            var hacSmooth = (2 * tac) - tacTema;
            var hl2Smooth = (2 * thl2) - thl2Tema;

            var shortCandle = Math.Abs(currentClose - currentOpen) < (currentHigh - currentLow) * factor;
            var prevKeepN1 = keepN1List.LastOrDefault();
            var keepN1 = ((hac >= hao) && (prevHac >= prevHao)) || currentClose >= hac || currentHigh > prevHigh || currentLow > prevLow || hl2Smooth >= hacSmooth;
            keepN1List.Add(keepN1);

            var prevKeepAll1 = keepAll1List.LastOrDefault();
            var keepAll1 = keepN1 || (prevKeepN1 && (currentClose >= currentOpen || currentClose >= prevClose));
            keepAll1List.Add(keepAll1);

            var keep13 = shortCandle && currentHigh >= prevLow;
            var prevUtr = utrList.LastOrDefault();
            var utr = keepAll1 || (prevKeepAll1 && keep13);
            utrList.Add(utr);

            var prevKeepN2 = keepN2List.LastOrDefault();
            var keepN2 = (hac < hao && prevHac < prevHao) || hl2Smooth < hacSmooth;
            keepN2List.Add(keepN2);

            var keep23 = shortCandle && currentLow <= prevHigh;
            var prevKeepAll2 = keepAll2List.LastOrDefault();
            var keepAll2 = keepN2 || (prevKeepN2 && (currentClose < currentOpen || currentClose < prevClose));
            keepAll2List.Add(keepAll2);

            var prevDtr = dtrList.LastOrDefault();
            var dtr = (keepAll2 || prevKeepAll2) && keep23;
            dtrList.Add(dtr);

            var upw = dtr == false && prevDtr && utr;
            var dnw = utr == false && prevUtr && dtr;
            var prevHaco = hacoList.LastOrDefault();
            var haco = upw ? 1 : dnw ? -1 : prevHaco;
            hacoList.AddRounded(haco);

            var signal = GetCompareSignal(haco, prevHaco);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Vhaltco", hacoList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = hacoList;
        stockData.IndicatorName = IndicatorName.VervoortHeikenAshiLongTermCandlestickOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Vix Trading System
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="maxCount"></param>
    /// <param name="minCount"></param>
    /// <returns></returns>
    public static StockData CalculateVixTradingSystem(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 50, double maxCount = 11, double minCount = -11)
    {
        List<double> countList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var vixts = smaList[i];

            var prevCount = countList.LastOrDefault();
            var count = currentValue > vixts && prevCount >= 0 ? prevCount + 1 : currentValue <= vixts && prevCount <= 0 ?
                prevCount - 1 : prevCount;
            countList.AddRounded(count);

            var signal = GetBullishBearishSignal(count - maxCount - 1, prevCount - maxCount - 1, count - minCount + 1,
                prevCount - minCount + 1, true);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Vix", countList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = countList;
        stockData.IndicatorName = IndicatorName.VixTradingSystem;

        return stockData;
    }

    /// <summary>
    /// Calculates the Varadi Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateVaradiOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 14)
    {
        List<double> dvoList = new();
        List<double> ratioList = new();
        List<double> tempList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var median = (currentHigh + currentLow) / 2;

            var ratio = median != 0 ? currentValue / median : 0;
            ratioList.AddRounded(ratio);
        }

        var aList = GetMovingAverageList(stockData, maType, length, ratioList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var a = aList[i];
            var prevDvo1 = i >= 1 ? dvoList[i - 1] : 0;
            var prevDvo2 = i >= 2 ? dvoList[i - 2] : 0;

            var prevA = i >= 1 ? aList[i - 1] : 0;
            tempList.AddRounded(prevA);

            var dvo = MinOrMax((double)tempList.TakeLastExt(length).Where(i => i <= a).Count() / length * 100, 100, 0);
            dvoList.AddRounded(dvo);

            var signal = GetRsiSignal(dvo - prevDvo1, prevDvo1 - prevDvo2, dvo, prevDvo1, 80, 20);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Vo", dvoList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = dvoList;
        stockData.IndicatorName = IndicatorName.VaradiOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Vanilla ABCD Pattern
    /// </summary>
    /// <param name="stockData"></param>
    /// <returns></returns>
    public static StockData CalculateVanillaABCDPattern(this StockData stockData)
    {
        List<double> osList = new();
        List<double> fList = new();
        List<double> dosList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, openList, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentOpen = openList[i];
            var currentValue = inputList[i];
            var prevValue1 = i >= 1 ? inputList[i - 1] : 0;
            var prevValue2 = i >= 2 ? inputList[i - 2] : 0;
            var prevValue3 = i >= 3 ? inputList[i - 3] : 0;
            double up = prevValue3 > prevValue2 && prevValue1 > prevValue2 && currentValue < prevValue2 ? 1 : 0;
            double dn = prevValue3 < prevValue2 && prevValue1 < prevValue2 && currentValue > prevValue2 ? 1 : 0;

            var prevOs = osList.LastOrDefault();
            var os = up == 1 ? 1 : dn == 1 ? 0 : prevOs;
            osList.AddRounded(os);

            var prevF = fList.LastOrDefault();
            var f = os == 1 && currentValue > currentOpen ? 1 : os == 0 && currentValue < currentOpen ? 0 : prevF;
            fList.AddRounded(f);

            var prevDos = dosList.LastOrDefault();
            var dos = os - prevOs;
            dosList.AddRounded(dos);

            var signal = GetCompareSignal(dos, prevDos);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Vabcd", dosList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = dosList;
        stockData.IndicatorName = IndicatorName.VanillaABCDPattern;

        return stockData;
    }

    /// <summary>
    /// Calculates the Vervoort Heiken Ashi Candlestick Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="inputName"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateVervoortHeikenAshiCandlestickOscillator(this StockData stockData,
        MovingAvgType maType = MovingAvgType.ZeroLagTripleExponentialMovingAverage, InputName inputName = InputName.FullTypicalPrice, int length = 34)
    {
        List<double> haoList = new();
        List<double> hacList = new();
        List<double> medianPriceList = new();
        List<bool> dnKeepingList = new();
        List<bool> dnKeepAllList = new();
        List<bool> dnTrendList = new();
        List<bool> upKeepingList = new();
        List<bool> upKeepAllList = new();
        List<bool> upTrendList = new();
        List<double> hacoList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, openList, closeList, _) = GetInputValuesList(inputName, stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevHao = haoList.LastOrDefault();
            var hao = (prevValue + prevHao) / 2;
            haoList.AddRounded(hao);

            var hac = (currentValue + hao + Math.Max(currentHigh, hao) + Math.Min(currentLow, hao)) / 4;
            hacList.AddRounded(hac);

            var medianPrice = (currentHigh + currentLow) / 2;
            medianPriceList.AddRounded(medianPrice);
        }

        var tma1List = GetMovingAverageList(stockData, maType, length, hacList);
        var tma2List = GetMovingAverageList(stockData, maType, length, tma1List);
        var tma12List = GetMovingAverageList(stockData, maType, length, medianPriceList);
        var tma22List = GetMovingAverageList(stockData, maType, length, tma12List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var tma1 = tma1List[i];
            var tma2 = tma2List[i];
            var tma12 = tma12List[i];
            var tma22 = tma22List[i];
            var hao = haoList[i];
            var hac = hacList[i];
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentOpen = openList[i];
            var currentClose = closeList[i];
            var prevHao = i >= 1 ? haoList[i - 1] : 0;
            var prevHac = i >= 1 ? hacList[i - 1] : 0;
            var prevHigh = i >= 1 ? highList[i - 1] : 0;
            var prevLow = i >= 1 ? lowList[i - 1] : 0;
            var prevClose = i >= 1 ? closeList[i - 1] : 0;
            var diff = tma1 - tma2;
            var zlHa = tma1 + diff;
            var diff2 = tma12 - tma22;
            var zlCl = tma12 + diff2;
            var zlDiff = zlCl - zlHa;
            var dnKeep1 = hac < hao && prevHac < prevHao;
            var dnKeep2 = zlDiff < 0;
            var dnKeep3 = Math.Abs(currentClose - currentOpen) < (currentHigh - currentLow) * 0.35 && currentLow <= prevHigh;

            var prevDnKeeping = dnKeepingList.LastOrDefault();
            var dnKeeping = dnKeep1 || dnKeep2;
            dnKeepingList.Add(dnKeeping);

            var prevDnKeepAll = dnKeepAllList.LastOrDefault();
            var dnKeepAll = (dnKeeping || prevDnKeeping) && ((currentClose < currentOpen) || (currentClose < prevClose));
            dnKeepAllList.Add(dnKeepAll);

            var prevDnTrend = dnTrendList.LastOrDefault();
            var dnTrend = dnKeepAll || (prevDnKeepAll && dnKeep3);
            dnTrendList.Add(dnTrend);

            var upKeep1 = hac >= hao && prevHac >= prevHao;
            var upKeep2 = zlDiff >= 0;
            var upKeep3 = Math.Abs(currentClose - currentOpen) < (currentHigh - currentLow) * 0.35 && currentHigh >= prevLow;

            var prevUpKeeping = upKeepingList.LastOrDefault();
            var upKeeping = upKeep1 || upKeep2;
            upKeepingList.Add(upKeeping);

            var prevUpKeepAll = upKeepAllList.LastOrDefault();
            var upKeepAll = (upKeeping || prevUpKeeping) && ((currentClose >= currentOpen) || (currentClose >= prevClose));
            upKeepAllList.Add(upKeepAll);

            var prevUpTrend = upTrendList.LastOrDefault();
            var upTrend = upKeepAll || (prevUpKeepAll && upKeep3);
            upTrendList.Add(upTrend);

            var upw = dnTrend == false && prevDnTrend && upTrend;
            var dnw = upTrend == false && prevUpTrend && dnTrend;

            var prevHaco = hacoList.LastOrDefault();
            var haco = upw ? 1 : dnw ? -1 : prevHaco;
            hacoList.AddRounded(haco);

            var signal = GetCompareSignal(haco, prevHaco);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Vhaco", hacoList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = hacoList;
        stockData.IndicatorName = IndicatorName.VervoortHeikenAshiCandlestickOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Chartmill Value Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="inputName"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateChartmillValueIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        InputName inputName = InputName.MedianPrice, int length = 5)
    {
        List<double> cmvCList = new();
        List<double> cmvOList = new();
        List<double> cmvHList = new();
        List<double> cmvLList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, openList, closeList, _) = GetInputValuesList(inputName, stockData);

        var atrList = CalculateAverageTrueRange(stockData, maType, length).CustomValuesList;
        var fList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var v = atrList[i];
            var f = fList[i];
            var prevCmvc1 = i >= 1 ? cmvCList[i - 1] : 0;
            var prevCmvc2 = i >= 2 ? cmvCList[i - 2] : 0;
            var currentClose = closeList[i];
            var currentOpen = openList[i];

            var cmvC = v != 0 ? MinOrMax((currentClose - f) / (v * Pow(length, 0.5)), 1, -1) : 0;
            cmvCList.AddRounded(cmvC);

            var cmvO = v != 0 ? MinOrMax((currentOpen - f) / (v * Pow(length, 0.5)), 1, -1) : 0;
            cmvOList.AddRounded(cmvO);

            var cmvH = v != 0 ? MinOrMax((currentHigh - f) / (v * Pow(length, 0.5)), 1, -1) : 0;
            cmvHList.AddRounded(cmvH);

            var cmvL = v != 0 ? MinOrMax((currentLow - f) / (v * Pow(length, 0.5)), 1, -1) : 0;
            cmvLList.AddRounded(cmvL);

            var signal = GetRsiSignal(cmvC - prevCmvc1, prevCmvc1 - prevCmvc2, cmvC, prevCmvc1, 0.5, -0.5);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Cmvc", cmvCList },
            { "Cmvo", cmvOList },
            { "Cmvh", cmvHList },
            { "Cmvl", cmvLList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.ChartmillValueIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Conditional Accumulator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="increment"></param>
    /// <returns></returns>
    public static StockData CalculateConditionalAccumulator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 14, double increment = 1)
    {
        List<double> valueList = new();
        List<Signal> signalsList = new();
        var (_, highList, lowList, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var prevHigh = i >= 1 ? highList[i - 1] : 0;
            var prevLow = i >= 1 ? lowList[i - 1] : 0;

            var prevValue = valueList.LastOrDefault();
            var value = currentLow >= prevHigh ? prevValue + increment : currentHigh <= prevLow ? prevValue - increment : prevValue;
            valueList.AddRounded(value);
        }

        var valueEmaList = GetMovingAverageList(stockData, maType, length, valueList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var value = valueList[i];
            var valueEma = valueEmaList[i];
            var prevValue = i >= 1 ? valueList[i - 1] : 0;
            var prevValueEma = i >= 1 ? valueEmaList[i - 1] : 0;

            var signal = GetCompareSignal(value - valueEma, prevValue - prevValueEma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ca", valueList },
            { "Signal", valueEmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = valueList;
        stockData.IndicatorName = IndicatorName.ConditionalAccumulator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Contract High Low Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <returns></returns>
    public static StockData CalculateContractHighLow(this StockData stockData)
    {
        List<double> conHiList = new();
        List<double> conLowList = new();
        List<Signal> signalsList = new();
        var (_, highList, lowList, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];

            var prevConHi = conHiList.LastOrDefault();
            var conHi = i >= 1 ? Math.Max(prevConHi, currentHigh) : currentHigh;
            conHiList.AddRounded(conHi);

            var prevConLow = conLowList.LastOrDefault();
            var conLow = i >= 1 ? Math.Min(prevConLow, currentLow) : currentLow;
            conLowList.AddRounded(conLow);

            var signal = GetConditionSignal(conHi > prevConHi, conLow < prevConLow);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ch", conHiList },
            { "Cl", conLowList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.ContractHighLow;

        return stockData;
    }

    /// <summary>
    /// Calculates the Chop Zone Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="inputName"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculateChopZone(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        InputName inputName = InputName.TypicalPrice, int length1 = 30, int length2 = 34)
    {
        List<double> emaAngleList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, closeList, _) = GetInputValuesList(inputName, stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length1);

        var emaList = GetMovingAverageList(stockData, maType, length2, closeList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var highest = highestList[i];
            var lowest = lowestList[i];
            var ema = emaList[i];
            var prevEma = i >= 1 ? emaList[i - 1] : 0;
            var range = highest - lowest != 0 ? 25 / (highest - lowest) * lowest : 0;
            var avg = inputList[i];
            var y = avg != 0 && range != 0 ? (prevEma - ema) / avg * range : 0;
            var c = Sqrt(1 + (y * y));
            var emaAngle1 = c != 0 ? Math.Round(Math.Acos(1 / c).ToDegrees()) : 0;

            var prevEmaAngle = emaAngleList.LastOrDefault();
            var emaAngle = y > 0 ? -emaAngle1 : emaAngle1;
            emaAngleList.AddRounded(emaAngle);

            var signal = GetCompareSignal(emaAngle, prevEmaAngle);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Cz", emaAngleList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = emaAngleList;
        stockData.IndicatorName = IndicatorName.ChopZone;

        return stockData;
    }

    /// <summary>
    /// Calculates the Center of Linearity
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateCenterOfLinearity(this StockData stockData, int length = 14)
    {
        List<double> aList = new();
        List<double> colList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var priorValue = i >= length ? inputList[i - length] : 0;

            var a = (i + 1) * (priorValue - prevValue);
            aList.AddRounded(a);

            var prevCol = colList.LastOrDefault();
            var col = aList.TakeLastExt(length).Sum();
            colList.AddRounded(col);

            var signal = GetCompareSignal(col, prevCol);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Col", colList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = colList;
        stockData.IndicatorName = IndicatorName.CenterOfLinearity;

        return stockData;
    }

    /// <summary>
    /// Calculates the Chaikin Volatility
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculateChaikinVolatility(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length1 = 10, int length2 = 12)
    {
        List<double> chaikinVolatilityList = new();
        List<double> highLowList = new();
        List<Signal> signalsList = new();
        var (_, highList, lowList, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];

            var highLow = currentHigh - currentLow;
            highLowList.AddRounded(highLow);
        }

        var highLowEmaList = GetMovingAverageList(stockData, maType, length1, highLowList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var highLowEma = highLowEmaList[i];
            var prevHighLowEma = i >= length2 ? highLowEmaList[i - length2] : 0;

            var prevChaikinVolatility = chaikinVolatilityList.LastOrDefault();
            var chaikinVolatility = prevHighLowEma != 0 ? (highLowEma - prevHighLowEma) / prevHighLowEma * 100 : 0;
            chaikinVolatilityList.AddRounded(chaikinVolatility);

            var signal = GetCompareSignal(chaikinVolatility, prevChaikinVolatility, true);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Cv", chaikinVolatilityList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = chaikinVolatilityList;
        stockData.IndicatorName = IndicatorName.ChaikinVolatility;

        return stockData;
    }

    /// <summary>
    /// Calculates the Confluence Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="inputName"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateConfluenceIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        InputName inputName = InputName.FullTypicalPrice, int length = 10)
    {
        List<double> value5List = new();
        List<double> value6List = new();
        List<double> value7List = new();
        List<double> momList = new();
        List<double> sumList = new();
        List<double> errSumList = new();
        List<double> value70List = new();
        List<double> confluenceList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, closeList, _) = GetInputValuesList(inputName, stockData);

        var stl = (int)Math.Ceiling((length * 2) - 1 - 0.5m);
        var itl = (int)Math.Ceiling((stl * 2) - 1 - 0.5m);
        var ltl = (int)Math.Ceiling((itl * 2) - 1 - 0.5m);
        var hoff = (int)Math.Ceiling(((double)length / 2) - 0.5);
        var soff = (int)Math.Ceiling(((double)stl / 2) - 0.5);
        var ioff = (int)Math.Ceiling(((double)itl / 2) - 0.5);
        var hLength = MinOrMax(length - 1);
        var sLength = stl - 1;
        var iLength = itl - 1;
        var lLength = ltl - 1;

        var hAvgList = GetMovingAverageList(stockData, maType, length, closeList);
        var sAvgList = GetMovingAverageList(stockData, maType, stl, closeList);
        var iAvgList = GetMovingAverageList(stockData, maType, itl, closeList);
        var lAvgList = GetMovingAverageList(stockData, maType, ltl, closeList);
        var h2AvgList = GetMovingAverageList(stockData, maType, hLength, closeList);
        var s2AvgList = GetMovingAverageList(stockData, maType, sLength, closeList);
        var i2AvgList = GetMovingAverageList(stockData, maType, iLength, closeList);
        var l2AvgList = GetMovingAverageList(stockData, maType, lLength, closeList);
        var ftpAvgList = GetMovingAverageList(stockData, maType, lLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var sAvg = sAvgList[i];
            var priorSAvg = i >= soff ? sAvgList[i - soff] : 0;
            var priorHAvg = i >= hoff ? hAvgList[i - hoff] : 0;
            var iAvg = iAvgList[i];
            var priorIAvg = i >= ioff ? iAvgList[i - ioff] : 0;
            var lAvg = lAvgList[i];
            var hAvg = hAvgList[i];
            var prevSAvg = i >= 1 ? sAvgList[i - 1] : 0;
            var prevHAvg = i >= 1 ? hAvgList[i - 1] : 0;
            var prevIAvg = i >= 1 ? iAvgList[i - 1] : 0;
            var prevLAvg = i >= 1 ? lAvgList[i - 1] : 0;
            var h2 = h2AvgList[i];
            var s2 = s2AvgList[i];
            var i2 = i2AvgList[i];
            var l2 = l2AvgList[i];
            var ftpAvg = ftpAvgList[i];
            var priorValue5 = i >= hoff ? value5List[i - hoff] : 0;
            var priorValue6 = i >= soff ? value6List[i - soff] : 0;
            var priorValue7 = i >= ioff ? value7List[i - ioff] : 0;
            var priorSum = i >= soff ? sumList[i - soff] : 0;
            var priorHAvg2 = i >= soff ? hAvgList[i - soff] : 0;
            var prevErrSum = i >= 1 ? errSumList[i - 1] : 0;
            var prevMom = i >= 1 ? momList[i - 1] : 0;
            var prevValue70 = i >= 1 ? value70List[i - 1] : 0;
            var prevConfluence1 = i >= 1 ? confluenceList[i - 1] : 0;
            var prevConfluence2 = i >= 2 ? confluenceList[i - 2] : 0;
            var value2 = sAvg - priorHAvg;
            var value3 = iAvg - priorSAvg;
            var value12 = lAvg - priorIAvg;
            var momSig = value2 + value3 + value12;
            var derivH = (hAvg * 2) - prevHAvg;
            var derivS = (sAvg * 2) - prevSAvg;
            var derivI = (iAvg * 2) - prevIAvg;
            var derivL = (lAvg * 2) - prevLAvg;
            var sumDH = length * derivH;
            var sumDS = stl * derivS;
            var sumDI = itl * derivI;
            var sumDL = ltl * derivL;
            var n1h = h2 * hLength;
            var n1s = s2 * sLength;
            var n1i = i2 * iLength;
            var n1l = l2 * lLength;
            var drh = sumDH - n1h;
            var drs = sumDS - n1s;
            var dri = sumDI - n1i;
            var drl = sumDL - n1l;
            var hSum = h2 * (length - 1);
            var sSum = s2 * (stl - 1);
            var iSum = i2 * (itl - 1);
            var lSum = ftpAvg * (ltl - 1);

            var value5 = (hSum + drh) / length;
            value5List.AddRounded(value5);

            var value6 = (sSum + drs) / stl;
            value6List.AddRounded(value6);

            var value7 = (iSum + dri) / itl;
            value7List.AddRounded(value7);

            var value13 = (lSum + drl) / ltl;
            var value9 = value6 - priorValue5;
            var value10 = value7 - priorValue6;
            var value14 = value13 - priorValue7;

            var mom = value9 + value10 + value14;
            momList.AddRounded(mom);

            var ht = Math.Sin(value5 * 2 * Math.PI / 360) + Math.Cos(value5 * 2 * Math.PI / 360);
            var hta = Math.Sin(hAvg * 2 * Math.PI / 360) + Math.Cos(hAvg * 2 * Math.PI / 360);
            var st = Math.Sin(value6 * 2 * Math.PI / 360) + Math.Cos(value6 * 2 * Math.PI / 360);
            var sta = Math.Sin(sAvg * 2 * Math.PI / 360) + Math.Cos(sAvg * 2 * Math.PI / 360);
            var it = Math.Sin(value7 * 2 * Math.PI / 360) + Math.Cos(value7 * 2 * Math.PI / 360);
            var ita = Math.Sin(iAvg * 2 * Math.PI / 360) + Math.Cos(iAvg * 2 * Math.PI / 360);

            var sum = ht + st + it;
            sumList.AddRounded(sum);

            var err = hta + sta + ita;
            double cond2 = (sum > priorSum && hAvg < priorHAvg2) || (sum < priorSum && hAvg > priorHAvg2) ? 1 : 0;
            double phase = cond2 == 1 ? -1 : 1;

            var errSum = (sum - err) * phase;
            errSumList.AddRounded(errSum);

            var value70 = value5 - value13;
            value70List.AddRounded(value70);

            var errSig = errSumList.TakeLastExt(soff).Average();
            var value71 = value70List.TakeLastExt(length).Average();
            double errNum = errSum > 0 && errSum < prevErrSum && errSum < errSig ? 1 : errSum > 0 && errSum < prevErrSum && errSum > errSig ? 2 :
                errSum > 0 && errSum > prevErrSum && errSum < errSig ? 2 : errSum > 0 && errSum > prevErrSum && errSum > errSig ? 3 :
                errSum < 0 && errSum > prevErrSum && errSum > errSig ? -1 : errSum < 0 && errSum < prevErrSum && errSum > errSig ? -2 :
                errSum < 0 && errSum > prevErrSum && errSum < errSig ? -2 : errSum < 0 && errSum < prevErrSum && errSum < errSig ? -3 : 0;
            double momNum = mom > 0 && mom < prevMom && mom < momSig ? 1 : mom > 0 && mom < prevMom && mom > momSig ? 2 :
                mom > 0 && mom > prevMom && mom < momSig ? 2 : mom > 0 && mom > prevMom && mom > momSig ? 3 :
                mom < 0 && mom > prevMom && mom > momSig ? -1 : mom < 0 && mom < prevMom && mom > momSig ? -2 :
                mom < 0 && mom > prevMom && mom < momSig ? -2 : mom < 0 && mom < prevMom && mom < momSig ? -3 : 0;
            double tcNum = value70 > 0 && value70 < prevValue70 && value70 < value71 ? 1 : value70 > 0 && value70 < prevValue70 && value70 > value71 ? 2 :
                value70 > 0 && value70 > prevValue70 && value70 < value71 ? 2 : value70 > 0 && value70 > prevValue70 && value70 > value71 ? 3 :
                value70 < 0 && value70 > prevValue70 && value70 > value71 ? -1 : value70 < 0 && value70 < prevValue70 && value70 > value71 ? -2 :
                value70 < 0 && value70 > prevValue70 && value70 < value71 ? -2 : value70 < 0 && value70 < prevValue70 && value70 < value71 ? -3 : 0;
            var value42 = errNum + momNum + tcNum;

            var confluence = value42 > 0 && value70 > 0 ? value42 : value42 < 0 && value70 < 0 ? value42 :
                (value42 > 0 && value70 < 0) || (value42 < 0 && value70 > 0) ? value42 / 10 : 0;
            confluenceList.AddRounded(confluence);

            var res1 = confluence >= 1 ? confluence : 0;
            var res2 = confluence <= -1 ? confluence : 0;
            var res3 = confluence == 0 ? 0 : confluence > -1 && confluence < 1 ? 10 * confluence : 0;

            var signal = GetCompareSignal(confluence - prevConfluence1, prevConfluence1 - prevConfluence2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ci", confluenceList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = confluenceList;
        stockData.IndicatorName = IndicatorName.ConfluenceIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Coppock Curve
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <returns></returns>
    public static StockData CalculateCoppockCurve(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 10,
        int fastLength = 11, int slowLength = 14)
    {
        List<double> rocTotalList = new();
        List<Signal> signalsList = new();

        var roc11List = CalculateRateOfChange(stockData, fastLength).CustomValuesList;
        var roc14List = CalculateRateOfChange(stockData, slowLength).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentRoc11 = roc11List[i];
            var currentRoc14 = roc14List[i];

            var rocTotal = currentRoc11 + currentRoc14;
            rocTotalList.AddRounded(rocTotal);
        }

        var coppockCurveList = GetMovingAverageList(stockData, maType, length, rocTotalList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var coppockCurve = coppockCurveList[i];
            var prevCoppockCurve = i >= 1 ? coppockCurveList[i - 1] : 0;

            var signal = GetCompareSignal(coppockCurve, prevCoppockCurve);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Cc", coppockCurveList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = coppockCurveList;
        stockData.IndicatorName = IndicatorName.CoppockCurve;

        return stockData;
    }

    /// <summary>
    /// Calculates the Constance Brown Composite Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateConstanceBrownCompositeIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int fastLength = 13, int slowLength = 33, int length1 = 14, int length2 = 9, int smoothLength = 3)
    {
        List<double> sList = new();
        List<double> bullSlopeList = new();
        List<double> bearSlopeList = new();
        List<Signal> signalsList = new();

        var rsi1List = CalculateRelativeStrengthIndex(stockData, length: length1).CustomValuesList;
        var rsi2List = CalculateRelativeStrengthIndex(stockData, length: smoothLength).CustomValuesList;
        var rsiSmaList = GetMovingAverageList(stockData, maType, smoothLength, rsi2List);

        for (var i = 0; i < stockData.Count; i++)
        {
            var rsiSma = rsiSmaList[i];
            var rsiDelta = i >= length2 ? rsi1List[i - length2] : 0;

            var s = rsiDelta + rsiSma;
            sList.AddRounded(s);
        }

        var sFastSmaList = GetMovingAverageList(stockData, maType, fastLength, sList);
        var sSlowSmaList = GetMovingAverageList(stockData, maType, slowLength, sList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var s = sList[i];
            var sFastSma = sFastSmaList[i];
            var sSlowSma = sSlowSmaList[i];

            var prevBullSlope = bullSlopeList.LastOrDefault();
            var bullSlope = s - Math.Max(sFastSma, sSlowSma);
            bullSlopeList.AddRounded(bullSlope);

            var prevBearSlope = bearSlopeList.LastOrDefault();
            var bearSlope = s - Math.Min(sFastSma, sSlowSma);
            bearSlopeList.AddRounded(bearSlope);

            var signal = GetBullishBearishSignal(bullSlope, prevBullSlope, bearSlope, prevBearSlope);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Cbci", sList },
            { "FastSignal", sFastSmaList },
            { "SlowSignal", sSlowSmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = sList;
        stockData.IndicatorName = IndicatorName.ConstanceBrownCompositeIndex;

        return stockData;
    }

    /// <summary>
    /// Calculates the Commodity Selection Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="pointValue"></param>
    /// <param name="margin"></param>
    /// <param name="commission"></param>
    /// <returns></returns>
    public static StockData CalculateCommoditySelectionIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, 
        int length = 14, double pointValue = 50, double margin = 3000, double commission = 10)
    {
        List<double> csiList = new();
        List<double> csiSmaList = new();
        List<Signal> signalsList = new();

        var k = 100 * (pointValue / Sqrt(margin) / (150 + commission));

        var atrList = CalculateAverageTrueRange(stockData, maType, length).CustomValuesList;
        var adxList = CalculateAverageDirectionalIndex(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var atr = atrList[i];
            var adxRating = adxList[i];

            var prevCsi = csiList.LastOrDefault();
            var csi = k * atr * adxRating;
            csiList.AddRounded(csi);

            var prevCsiSma = csiSmaList.LastOrDefault();
            var csiSma = csiList.TakeLastExt(length).Average();
            csiSmaList.AddRounded(csiSma);

            var signal = GetCompareSignal(csi - csiSma, prevCsi - prevCsiSma, true);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Csi", csiList },
            { "Signal", csiSmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = csiList;
        stockData.IndicatorName = IndicatorName.CommoditySelectionIndex;

        return stockData;
    }

    /// <summary>
    /// Calculates the Math.PIvot Detector Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculatePivotDetectorOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length1 = 200, int length2 = 14)
    {
        List<double> pdoList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var rsiList = CalculateRelativeStrengthIndex(stockData, maType, length2).CustomValuesList;
        var smaList = GetMovingAverageList(stockData, maType, length1, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var sma = smaList[i];
            var rsi = rsiList[i];
            var prevPdo1 = i >= 1 ? pdoList[i - 1] : 0;
            var prevPdo2 = i >= 2 ? pdoList[i - 2] : 0;

            var pdo = currentValue > sma ? (rsi - 35) / (85 - 35) * 100 : currentValue <= sma ? (rsi - 20) / (70 - 20) * 100 : 0;
            pdoList.AddRounded(pdo);

            var signal = GetCompareSignal(pdo - prevPdo1, prevPdo1 - prevPdo2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Pdo", pdoList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = pdoList;
        stockData.IndicatorName = IndicatorName.PivotDetectorOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Percent Change Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculatePercentChangeOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length = 14)
    {
        List<double> percentChangeList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevPcc = percentChangeList.LastOrDefault();
            var pcc = prevValue - 1 != 0 ? prevPcc + (currentValue / (prevValue - 1)) : 0;
            percentChangeList.AddRounded(pcc);
        }

        var pctChgWmaList = GetMovingAverageList(stockData, maType, length, percentChangeList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var pcc = percentChangeList[i];
            var pccWma = pctChgWmaList[i];
            var prevPcc = i >= 1 ? percentChangeList[i - 1] : 0;
            var prevPccWma = i >= 1 ? pctChgWmaList[i - 1] : 0;

            var signal = GetCompareSignal(pcc - pccWma, prevPcc - prevPccWma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Pcco", percentChangeList },
            { "Signal", pctChgWmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = percentChangeList;
        stockData.IndicatorName = IndicatorName.PercentChangeOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Prime Number Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculatePrimeNumberOscillator(this StockData stockData, int length = 5)
    {
        List<double> pnoList = new();
        List<double> pno1List = new();
        List<double> pno2List = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var ratio = currentValue * length / 100;
            var convertedValue = (long)Math.Round(currentValue);
            var sqrtValue = currentValue >= 0 ? (long)Math.Round(Sqrt(currentValue)) : 0;
            var maxValue = (long)Math.Round(currentValue + ratio);
            var minValue = (long)Math.Round(currentValue - ratio);

            double pno1 = 0, pno2 = 0;
            for (var j = convertedValue; j <= maxValue; j++)
            {
                pno1 = j;
                for (var k = 2; k <= sqrtValue; k++)
                {
                    pno1 = j % k == 0 ? 0 : j;
                    if (pno1 == 0)
                    {
                        break;
                    }
                }

                if (pno1 > 0)
                {
                    break;
                }
            }
            pno1 = pno1 == 0 ? pno1List.LastOrDefault() : pno1;
            pno1List.AddRounded(pno1);

            for (var l = convertedValue; l >= minValue; l--)
            {
                pno2 = l;
                for (var m = 2; m <= sqrtValue; m++)
                {
                    pno2 = l % m == 0 ? 0 : l;
                    if (pno2 == 0)
                    {
                        break;
                    }
                }

                if (pno2 > 0)
                {
                    break;
                }
            }
            pno2 = pno2 == 0 ? pno2List.LastOrDefault() : pno2;
            pno2List.AddRounded(pno2);

            var prevPno = pnoList.LastOrDefault();
            var pno = pno1 - currentValue < currentValue - pno2 ? pno1 - currentValue : pno2 - currentValue;
            pno = pno == 0 ? prevPno : pno;
            pnoList.AddRounded(pno);

            var signal = GetCompareSignal(pno, prevPno);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Pno", pnoList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = pnoList;
        stockData.IndicatorName = IndicatorName.PrimeNumberOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Pring Special K
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="length4"></param>
    /// <param name="length5"></param>
    /// <param name="length6"></param>
    /// <param name="length7"></param>
    /// <param name="length8"></param>
    /// <param name="length9"></param>
    /// <param name="length10"></param>
    /// <param name="length11"></param>
    /// <param name="length12"></param>
    /// <param name="length13"></param>
    /// <param name="length14"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculatePringSpecialK(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 10,
        int length2 = 15, int length3 = 20, int length4 = 30, int length5 = 40, int length6 = 50, int length7 = 65, int length8 = 75, int length9 = 100,
        int length10 = 130, int length11 = 195, int length12 = 265, int length13 = 390, int length14 = 530, int smoothLength = 10)
    {
        List<double> specialKList = new();
        List<Signal> signalsList = new();

        var rocList = CalculateRateOfChange(stockData, length1).CustomValuesList;
        var roc15List = CalculateRateOfChange(stockData, length2).CustomValuesList;
        var roc20List = CalculateRateOfChange(stockData, length3).CustomValuesList;
        var roc30List = CalculateRateOfChange(stockData, length4).CustomValuesList;
        var roc40List = CalculateRateOfChange(stockData, length5).CustomValuesList;
        var roc65List = CalculateRateOfChange(stockData, length7).CustomValuesList;
        var roc75List = CalculateRateOfChange(stockData, length8).CustomValuesList;
        var roc100List = CalculateRateOfChange(stockData, length9).CustomValuesList;
        var roc195List = CalculateRateOfChange(stockData, length11).CustomValuesList;
        var roc265List = CalculateRateOfChange(stockData, length12).CustomValuesList;
        var roc390List = CalculateRateOfChange(stockData, length13).CustomValuesList;
        var roc530List = CalculateRateOfChange(stockData, length14).CustomValuesList;
        var roc10SmaList = GetMovingAverageList(stockData, maType, length1, rocList);
        var roc15SmaList = GetMovingAverageList(stockData, maType, length1, roc15List);
        var roc20SmaList = GetMovingAverageList(stockData, maType, length1, roc20List);
        var roc30SmaList = GetMovingAverageList(stockData, maType, length2, roc30List);
        var roc40SmaList = GetMovingAverageList(stockData, maType, length6, roc40List);
        var roc65SmaList = GetMovingAverageList(stockData, maType, length7, roc65List);
        var roc75SmaList = GetMovingAverageList(stockData, maType, length8, roc75List);
        var roc100SmaList = GetMovingAverageList(stockData, maType, length9, roc100List);
        var roc195SmaList = GetMovingAverageList(stockData, maType, length10, roc195List);
        var roc265SmaList = GetMovingAverageList(stockData, maType, length10, roc265List);
        var roc390SmaList = GetMovingAverageList(stockData, maType, length10, roc390List);
        var roc530SmaList = GetMovingAverageList(stockData, maType, length11, roc530List);

        for (var i = 0; i < stockData.Count; i++)
        {
            var roc10Sma = roc10SmaList[i];
            var roc15Sma = roc15SmaList[i];
            var roc20Sma = roc20SmaList[i];
            var roc30Sma = roc30SmaList[i];
            var roc40Sma = roc40SmaList[i];
            var roc65Sma = roc65SmaList[i];
            var roc75Sma = roc75SmaList[i];
            var roc100Sma = roc100SmaList[i];
            var roc195Sma = roc195SmaList[i];
            var roc265Sma = roc265SmaList[i];
            var roc390Sma = roc390SmaList[i];
            var roc530Sma = roc530SmaList[i];

            var specialK = (roc10Sma * 1) + (roc15Sma * 2) + (roc20Sma * 3) + (roc30Sma * 4) + (roc40Sma * 1) + (roc65Sma * 2) + (roc75Sma * 3) +
                           (roc100Sma * 4) + (roc195Sma * 1) + (roc265Sma * 2) + (roc390Sma * 3) + (roc530Sma * 4);
            specialKList.AddRounded(specialK);
        }

        var specialKSignalList = GetMovingAverageList(stockData, maType, smoothLength, specialKList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var specialK = specialKList[i];
            var specialKSignal = specialKSignalList[i];
            var prevSpecialK = i >= 1 ? specialKList[i - 1] : 0;
            var prevSpecialKSignal = i >= 1 ? specialKSignalList[i - 1] : 0;

            var signal = GetCompareSignal(specialK - specialKSignal, prevSpecialK - prevSpecialKSignal);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "PringSpecialK", specialKList },
            { "Signal", specialKSignalList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = specialKList;
        stockData.IndicatorName = IndicatorName.PringSpecialK;

        return stockData;
    }

    /// <summary>
    /// Calculates the Price Zone Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculatePriceZoneOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length = 20)
    {
        List<double> pzoList = new();
        List<double> dvolList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var emaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var dvol = Math.Sign(MinPastValues(i, 1, currentValue - prevValue)) * currentValue;
            dvolList.AddRounded(dvol);
        }

        var dvmaList = GetMovingAverageList(stockData, maType, length, dvolList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var vma = emaList[i];
            var dvma = dvmaList[i];
            var prevPzo1 = i >= 1 ? pzoList[i - 1] : 0;
            var prevPzo2 = i >= 2 ? pzoList[i - 2] : 0;

            var pzo = vma != 0 ? MinOrMax(100 * dvma / vma, 100, -100) : 0;
            pzoList.AddRounded(pzo);

            var signal = GetRsiSignal(pzo - prevPzo1, prevPzo1 - prevPzo2, pzo, prevPzo1, 40, -40);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Pzo", pzoList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = pzoList;
        stockData.IndicatorName = IndicatorName.PriceZoneOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Performance Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculatePerformanceIndex(this StockData stockData, int length = 14)
    {
        List<double> kpiList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= length ? inputList[i - length] : 0;

            var prevKpi = kpiList.LastOrDefault();
            var kpi = prevValue != 0 ? MinPastValues(i, length, currentValue - prevValue) * 100 / prevValue : 0;
            kpiList.AddRounded(kpi);

            var signal = GetCompareSignal(kpi, prevKpi);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Math.PI", kpiList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = kpiList;
        stockData.IndicatorName = IndicatorName.PerformanceIndex;

        return stockData;
    }

    /// <summary>
    /// Calculates the Polarized Fractal Efficiency
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculatePolarizedFractalEfficiency(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 9, int smoothLength = 5)
    {
        List<double> c2cList = new();
        List<double> fracEffList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var priorValue = i >= length ? inputList[i - length] : 0;
            var pfe = Sqrt(Pow(MinPastValues(i, length, currentValue - priorValue), 2) + 100);

            var c2c = Sqrt(Pow(MinPastValues(i, 1, currentValue - prevValue), 2) + 1);
            c2cList.AddRounded(c2c);

            var c2cSum = c2cList.TakeLastExt(length).Sum();
            var efRatio = c2cSum != 0 ? pfe / c2cSum * 100 : 0;

            var fracEff = i >= length && currentValue - priorValue > 0 ? efRatio : -efRatio;
            fracEffList.AddRounded(fracEff);
        }

        var emaList = GetMovingAverageList(stockData, maType, smoothLength, fracEffList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var ema = emaList[i];
            var prevEma = i >= 1 ? emaList[i - 1] : 0;

            var signal = GetCompareSignal(ema, prevEma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Pfe", emaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = emaList;
        stockData.IndicatorName = IndicatorName.PolarizedFractalEfficiency;

        return stockData;
    }

    /// <summary>
    /// Calculates the Pretty Good Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculatePrettyGoodOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 14)
    {
        List<double> pgoList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);
        var atrList = CalculateAverageTrueRange(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var sma = smaList[i];
            var atr = atrList[i];

            var prevPgo = pgoList.LastOrDefault();
            var pgo = atr != 0 ? (currentValue - sma) / atr : 0;
            pgoList.AddRounded(pgo);

            var signal = GetCompareSignal(pgo, prevPgo);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Pgo", pgoList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = pgoList;
        stockData.IndicatorName = IndicatorName.PrettyGoodOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Price Cycle Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculatePriceCycleOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 22)
    {
        List<double> pcoList = new();
        List<double> diffList = new();
        List<Signal> signalsList = new();
        var (inputList, _, lowList, _, _) = GetInputValuesList(stockData);

        var atrList = CalculateAverageTrueRange(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentClose = inputList[i];
            var currentLow = lowList[i];
            
            var diff = currentClose - currentLow;
            diffList.AddRounded(diff);
        }

        var diffSmaList = GetMovingAverageList(stockData, maType, length, diffList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentAtr = atrList[i];
            var prevPco1 = i >= 1 ? pcoList[i - 1] : 0;
            var prevPco2 = i >= 2 ? pcoList[i - 2] : 0;
            var diffSma = diffSmaList[i];

            var pco = currentAtr != 0 ? diffSma / currentAtr * 100 : 0;
            pcoList.AddRounded(pco);

            var signal = GetCompareSignal(pco - prevPco1, prevPco1 - prevPco2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Pco", pcoList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = pcoList;
        stockData.IndicatorName = IndicatorName.PriceCycleOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Phase Change Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculatePhaseChangeIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 35, int smoothLength = 3)
    {
        List<double> pciList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= length ? inputList[i - length] : 0;
            var mom = MinPastValues(i, length, currentValue - prevValue);

            double positiveSum = 0, negativeSum = 0;
            for (var j = 0; j <= length - 1; j++)
            {
                var prevValue2 = i >= length - j ? inputList[i - (length - j)] : 0;
                var gradient = prevValue + (mom * (length - j) / (length - 1));
                var deviation = prevValue2 - gradient;
                positiveSum = deviation > 0 ? positiveSum + deviation : positiveSum + 0;
                negativeSum = deviation < 0 ? negativeSum - deviation : negativeSum + 0;
            }
            var sum = positiveSum + negativeSum;

            var pci = sum != 0 ? MinOrMax(100 * positiveSum / sum, 100, 0) : 0;
            pciList.AddRounded(pci);
        }

        var pciSmoothedList = GetMovingAverageList(stockData, maType, smoothLength, pciList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var pciSmoothed = pciSmoothedList[i];
            var prevPciSmoothed1 = i >= 1 ? pciSmoothedList[i - 1] : 0;
            var prevPciSmoothed2 = i >= 2 ? pciSmoothedList[i - 2] : 0;

            var signal = GetRsiSignal(pciSmoothed - prevPciSmoothed1, prevPciSmoothed1 - prevPciSmoothed2, pciSmoothed, prevPciSmoothed1, 80, 20);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Pci", pciList },
            { "Signal", pciSmoothedList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = pciList;
        stockData.IndicatorName = IndicatorName.PhaseChangeIndex;

        return stockData;
    }

    /// <summary>
    /// Calculates the Peak Valley Estimation
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculatePeakValleyEstimation(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 500, int smoothLength = 100)
    {
        List<double> sign1List = new();
        List<double> sign2List = new();
        List<double> sign3List = new();
        List<double> absOsList = new();
        List<double> osList = new();
        List<double> hList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var sma = smaList[i];

            var os = currentValue - sma;
            osList.AddRounded(os);

            var absOs = Math.Abs(os);
            absOsList.AddRounded(absOs);
        }

        stockData.CustomValuesList = absOsList;
        var pList = CalculateLinearRegression(stockData, smoothLength).CustomValuesList;
        var (highestList, _) = GetMaxAndMinValuesList(pList, length);
        for (var i = 0; i < stockData.Count; i++)
        {
            var os = osList[i];
            var p = pList[i];
            var highest = highestList[i];

            var prevH = i >= 1 ? hList[i - 1] : 0;
            var h = highest != 0 ? p / highest : 0;
            hList.AddRounded(h);

            double mod1 = h == 1 && prevH != 1 ? 1 : 0;
            double mod2 = h < 0.8 ? 1 : 0;
            double mod3 = prevH == 1 && h < prevH ? 1 : 0;

            double sign1 = mod1 == 1 && os < 0 ? 1 : mod1 == 1 && os > 0 ? -1 : 0;
            sign1List.AddRounded(sign1);

            double sign2 = mod2 == 1 && os < 0 ? 1 : mod2 == 1 && os > 0 ? -1 : 0;
            sign2List.AddRounded(sign2);

            double sign3 = mod3 == 1 && os < 0 ? 1 : mod3 == 1 && os > 0 ? -1 : 0;
            sign3List.AddRounded(sign3);

            var signal = GetConditionSignal(sign1 > 0, sign1 < 0);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Sign1", sign1List },
            { "Sign2", sign2List },
            { "Sign3", sign3List }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = sign1List;
        stockData.IndicatorName = IndicatorName.PeakValleyEstimation;

        return stockData;
    }

    /// <summary>
    /// Calculates the Psychological Line
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculatePsychologicalLine(this StockData stockData, int length = 20)
    {
        List<double> condList = new();
        List<double> psyList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevPsy1 = i >= 1 ? psyList[i - 1] : 0;
            var prevPsy2 = i >= 2 ? psyList[i - 2] : 0;

            double cond = currentValue > prevValue ? 1 : 0;
            condList.AddRounded(cond);

            var condSum = condList.TakeLastExt(length).Sum();
            var psy = length != 0 ? condSum / length * 100 : 0;
            psyList.AddRounded(psy);

            var signal = GetCompareSignal(psy - prevPsy1, prevPsy1 - prevPsy2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Pl", psyList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = psyList;
        stockData.IndicatorName = IndicatorName.PsychologicalLine;

        return stockData;
    }

    /// <summary>
    /// Calculates the Turbo Trigger
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateTurboTrigger(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 100,
        int smoothLength = 2)
    {
        List<double> avgList = new();
        List<double> hyList = new();
        List<double> ylList = new();
        List<double> abList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, openList, _) = GetInputValuesList(stockData);

        var cList = GetMovingAverageList(stockData, maType, smoothLength, inputList);
        var oList = GetMovingAverageList(stockData, maType, smoothLength, openList);
        var hList = GetMovingAverageList(stockData, maType, smoothLength, highList);
        var lList = GetMovingAverageList(stockData, maType, smoothLength, lowList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var c = cList[i];
            var o = oList[i];

            var avg = (c + o) / 2;
            avgList.AddRounded(avg);
        }

        var yList = GetMovingAverageList(stockData, maType, length, avgList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var y = yList[i];
            var h = hList[i];
            var l = lList[i];

            var hy = h - y;
            hyList.AddRounded(hy);

            var yl = y - l;
            ylList.AddRounded(yl);
        }

        var aList = GetMovingAverageList(stockData, maType, length, hyList);
        var bList = GetMovingAverageList(stockData, maType, length, ylList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var a = aList[i];
            var b = bList[i];

            var ab = a - b;
            abList.AddRounded(ab);
        }

        var oscList = GetMovingAverageList(stockData, maType, length, abList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var osc = oscList[i];
            var prevOsc = i >= 1 ? oscList[i - 1] : 0;
            var a = aList[i];
            var prevA = i >= 1 ? aList[i - 1] : 0;

            var signal = GetCompareSignal(osc - a, prevOsc - prevA);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "BullLine", aList },
            { "Trigger", oscList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.TurboTrigger;

        return stockData;
    }

    /// <summary>
    /// Calculates the Total Power Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculateTotalPowerIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length1 = 45, int length2 = 10)
    {
        List<double> bullCountList = new();
        List<double> bearCountList = new();
        List<double> totalPowerList = new();
        List<double> adjBullCountList = new();
        List<double> adjBearCountList = new();
        List<Signal> signalsList = new();

        var elderPowerList = CalculateElderRayIndex(stockData, maType, length2);
        var bullPowerList = elderPowerList.OutputValues["BullPower"];
        var bearPowerList = elderPowerList.OutputValues["BearPower"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var bullPower = bullPowerList[i];
            var bearPower = bearPowerList[i];

            double bullCount = bullPower > 0 ? 1 : 0;
            bullCountList.AddRounded(bullCount);

            double bearCount = bearPower < 0 ? 1 : 0;
            bearCountList.AddRounded(bearCount);

            var bullCountSum = bullCountList.TakeLastExt(length1).Sum();
            var bearCountSum = bearCountList.TakeLastExt(length1).Sum();

            var totalPower = length1 != 0 ? 100 * Math.Abs(bullCountSum - bearCountSum) / length1 : 0;
            totalPowerList.AddRounded(totalPower);

            var prevAdjBullCount = adjBullCountList.LastOrDefault();
            var adjBullCount = length1 != 0 ? 100 * bullCountSum / length1 : 0;
            adjBullCountList.AddRounded(adjBullCount);

            var prevAdjBearCount = adjBearCountList.LastOrDefault();
            var adjBearCount = length1 != 0 ? 100 * bearCountSum / length1 : 0;
            adjBearCountList.AddRounded(adjBearCount);

            var signal = GetCompareSignal(adjBullCount - adjBearCount, prevAdjBullCount - prevAdjBearCount);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "TotalPower", totalPowerList },
            { "BullCount", adjBullCountList },
            { "BearCount", adjBearCountList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = totalPowerList;
        stockData.IndicatorName = IndicatorName.TotalPowerIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Turbo Scaler
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="alpha"></param>
    /// <returns></returns>
    public static StockData CalculateTurboScaler(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 50, 
        double alpha = 0.5)
    {
        List<double> aList = new();
        List<double> bList = new();
        List<double> smoList = new();
        List<double> smoSmaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);
        var sma2List = GetMovingAverageList(stockData, maType, length, smaList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var sma = smaList[i];
            var sma2 = sma2List[i];

            var smoSma = (alpha * sma) + ((1 - alpha) * sma2);
            smoSmaList.AddRounded(smoSma);

            var smo = (alpha * currentValue) + ((1 - alpha) * sma);
            smoList.AddRounded(smo);

            var smoSmaHighest = smoSmaList.TakeLastExt(length).Max();
            var smoSmaLowest = smoSmaList.TakeLastExt(length).Min();
            var smoHighest = smoList.TakeLastExt(length).Max();
            var smoLowest = smoList.TakeLastExt(length).Min();

            var a = smoHighest - smoLowest != 0 ? (currentValue - smoLowest) / (smoHighest - smoLowest) : 0;
            aList.AddRounded(a);

            var b = smoSmaHighest - smoSmaLowest != 0 ? (sma - smoSmaLowest) / (smoSmaHighest - smoSmaLowest) : 0;
            bList.AddRounded(b);
        }

        var aSmaList = GetMovingAverageList(stockData, maType, length, aList);
        var bSmaList = GetMovingAverageList(stockData, maType, length, bList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var a = aSmaList[i];
            var b = bSmaList[i];
            var prevA = i >= 1 ? aSmaList[i - 1] : 0;
            var prevB = i >= 1 ? bSmaList[i - 1] : 0;

            var signal = GetCompareSignal(a - b, prevA - prevB);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ts", aList },
            { "Trigger", bList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = aList;
        stockData.IndicatorName = IndicatorName.TurboScaler;

        return stockData;
    }

    /// <summary>
    /// Calculates the Technical Rank
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="length4"></param>
    /// <param name="length5"></param>
    /// <param name="length6"></param>
    /// <param name="length7"></param>
    /// <param name="length8"></param>
    /// <param name="length9"></param>
    /// <returns></returns>
    public static StockData CalculateTechnicalRank(this StockData stockData, int length1 = 200, int length2 = 125, int length3 = 50, int length4 = 20,
        int length5 = 12, int length6 = 26, int length7 = 9, int length8 = 3, int length9 = 14)
    {
        List<double> trList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var ma1List = GetMovingAverageList(stockData, MovingAvgType.SimpleMovingAverage, length1, inputList);
        var ma2List = GetMovingAverageList(stockData, MovingAvgType.SimpleMovingAverage, length3, inputList);
        var ltRocList = CalculateRateOfChange(stockData, length2).CustomValuesList;
        var mtRocList = CalculateRateOfChange(stockData, length4).CustomValuesList;
        var rsiList = CalculateRelativeStrengthIndex(stockData, length: length9).CustomValuesList;
        var ppoHistList = CalculatePercentagePriceOscillator(stockData, MovingAvgType.ExponentialMovingAverage, length5, length6, length7).
            OutputValues["Histogram"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentEma200 = ma1List[i];
            var currentEma50 = ma2List[i];
            var currentRoc125 = ltRocList[i];
            var currentRoc20 = mtRocList[i];
            var currentPpoHistogram = ppoHistList[i];
            var currentRsi = rsiList[i];
            var currentPrice = inputList[i];
            var prevTr1 = i >= 1 ? trList[i - 1] : 0;
            var prevTr2 = i >= 2 ? trList[i - 2] : 0;
            var ltMa = currentEma200 != 0 ? 0.3 * 100 * (currentPrice - currentEma200) / currentEma200 : 0;
            var ltRoc = 0.3 * 100 * currentRoc125;
            var mtMa = currentEma50 != 0 ? 0.15 * 100 * (currentPrice - currentEma50) / currentEma50 : 0;
            var mtRoc = 0.15 * 100 * currentRoc20;
            var currentValue = currentPpoHistogram;
            var prevValue = i >= length8 ? ppoHistList[i - length8] : 0;
            var slope = length8 != 0 ? MinPastValues(i, length8, currentValue - prevValue) / length8 : 0;
            var stPpo = 0.05 * 100 * slope;
            var stRsi = 0.05 * currentRsi;

            var tr = Math.Min(100, Math.Max(0, ltMa + ltRoc + mtMa + mtRoc + stPpo + stRsi));
            trList.AddRounded(tr);

            var signal = GetCompareSignal(tr - prevTr1, prevTr1 - prevTr2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Tr", trList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = trList;
        stockData.IndicatorName = IndicatorName.TechnicalRank;

        return stockData;
    }

    /// <summary>
    /// Calculates the Trigonometric Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateTrigonometricOscillator(this StockData stockData, int length = 200)
    {
        List<double> uList = new();
        List<double> oList = new();
        List<Signal> signalsList = new();

        var sList = CalculateLinearRegression(stockData, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var s = sList[i];
            var prevS = i >= 1 ? sList[i - 1] : 0;
            var wa = Math.Asin(Math.Sign(s - prevS)) * 2;
            var wb = Math.Asin(Math.Sign(1)) * 2;

            var u = wa + (2 * Math.PI * Math.Round((wa - wb) / (2 * Math.PI)));
            uList.AddRounded(u);
        }

        stockData.CustomValuesList = uList;
        var uLinregList = CalculateLinearRegression(stockData, length).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var u = uLinregList[i];
            var prevO1 = i >= 1 ? oList[i - 1] : 0;
            var prevO2 = i >= 2 ? oList[i - 2] : 0;

            var o = Math.Atan(u);
            oList.AddRounded(o);

            var signal = GetRsiSignal(o - prevO1, prevO1 - prevO2, o, prevO1, 1, -1);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "To", oList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = oList;
        stockData.IndicatorName = IndicatorName.TrigonometricOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Trading Made More Simpler Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="smoothLength"></param>
    /// <param name="threshold"></param>
    /// <param name="limit"></param>
    /// <returns></returns>
    public static StockData CalculateTradingMadeMoreSimplerOscillator(this StockData stockData, 
        MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length1 = 14, int length2 = 8, int length3 = 12, int smoothLength = 3,
        double threshold = 50, double limit = 0)
    {
        List<double> bufHistNoList = new();
        List<Signal> signalsList = new();

        var rsiList = CalculateRelativeStrengthIndex(stockData, maType, length: length1).CustomValuesList;
        var stochastic1List = CalculateStochasticOscillator(stockData, maType, length: length2, smoothLength, smoothLength).OutputValues["FastD"];
        var stochastic2List = CalculateStochasticOscillator(stockData, maType, length: length1, smoothLength, smoothLength).OutputValues["FastD"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var rsi = rsiList[i];
            var stoch1 = stochastic1List[i];
            var stoch2 = stochastic2List[i];
            var bufRsi = rsi - threshold;
            var bufStoch1 = stoch1 - threshold;
            var bufStoch2 = stoch2 - threshold;
            var bufHistUp = bufRsi > limit && bufStoch1 > limit && bufStoch2 > limit ? bufStoch2 : 0;
            var bufHistDn = bufRsi < limit && bufStoch1 < limit && bufStoch2 < limit ? bufStoch2 : 0;

            var prevBufHistNo = bufHistNoList.LastOrDefault();
            var bufHistNo = bufHistUp - bufHistDn;
            bufHistNoList.AddRounded(bufHistNo);

            var signal = GetCompareSignal(bufHistNo, prevBufHistNo);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Tmmso", bufHistNoList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = bufHistNoList;
        stockData.IndicatorName = IndicatorName.TradingMadeMoreSimplerOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Traders Dynamic Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="length4"></param>
    /// <returns></returns>
    public static StockData CalculateTradersDynamicIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length1 = 13, int length2 = 34, int length3 = 2, int length4 = 7)
    {
        List<double> upList = new();
        List<double> dnList = new();
        List<double> midList = new();
        List<Signal> signalsList = new();

        var rsiList = CalculateRelativeStrengthIndex(stockData, maType, length1, length2);
        var rList = rsiList.CustomValuesList;
        var maList = rsiList.OutputValues["Signal"];
        stockData.CustomValuesList = rList;
        var stdDevList = CalculateStandardDeviationVolatility(stockData, maType, length2).CustomValuesList;
        var mabList = GetMovingAverageList(stockData, maType, length3, rList);
        var mbbList = GetMovingAverageList(stockData, maType, length4, rList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var rsiSma = maList[i];
            var stdDev = stdDevList[i];
            var mab = mabList[i];
            var mbb = mbbList[i];
            var prevMab = i >= 1 ? mabList[i - 1] : 0;
            var prevMbb = i >= 1 ? mbbList[i - 1] : 0;
            var offs = 1.6185 * stdDev;

            var prevUp = upList.LastOrDefault();
            var up = rsiSma + offs;
            upList.AddRounded(up);

            var prevDn = dnList.LastOrDefault();
            var dn = rsiSma - offs;
            dnList.AddRounded(dn);

            var mid = (up + dn) / 2;
            midList.AddRounded(mid);

            var signal = GetBollingerBandsSignal(mab - mbb, prevMab - prevMbb, mab, prevMab, up, prevUp, dn, prevDn);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", upList },
            { "MiddleBand", midList },
            { "LowerBand", dnList },
            { "Tdi", mabList },
            { "Signal", mbbList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = mabList;
        stockData.IndicatorName = IndicatorName.TradersDynamicIndex;

        return stockData;
    }

    /// <summary>
    /// Calculates the Tops and Bottoms Finder
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateTopsAndBottomsFinder(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length = 50)
    {
        List<double> bList = new();
        List<double> cList = new();
        List<double> upList = new();
        List<double> dnList = new();
        List<double> osList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var emaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var a = emaList[i];
            var prevA = i >= 1 ? emaList[i - 1] : 0;

            var b = a > prevA ? a : 0;
            bList.AddRounded(b);

            var c = a < prevA ? a : 0;
            cList.AddRounded(c);
        }

        stockData.CustomValuesList = bList;
        var bStdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        stockData.CustomValuesList = cList;
        var cStdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var a = emaList[i];
            var b = bStdDevList[i];
            var c = cStdDevList[i];

            var prevUp = upList.LastOrDefault();
            var up = a + b != 0 ? a / (a + b) : 0;
            upList.AddRounded(up);

            var prevDn = dnList.LastOrDefault();
            var dn = a + c != 0 ? a / (a + c) : 0;
            dnList.AddRounded(dn);

            double os = prevUp == 1 && up != 1 ? 1 : prevDn == 1 && dn != 1 ? -1 : 0;
            osList.AddRounded(os);

            var signal = GetConditionSignal(os > 0, os < 0);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Tabf", osList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = osList;
        stockData.IndicatorName = IndicatorName.TopsAndBottomsFinder;

        return stockData;
    }

    /// <summary>
    /// Calculates the Trader Pressure Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateTraderPressureIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage, 
        int length1 = 7, int length2 = 2, int smoothLength = 3)
    {
        List<double> bullsList = new();
        List<double> bearsList = new();
        List<double> netList = new();
        List<Signal> signalsList = new();
        var (_, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length2);

        for (var i = 0; i < stockData.Count; i++)
        {
            var high = highList[i];
            var low = lowList[i];
            var prevHigh = i >= 1 ? highList[i - 1] : 0;
            var prevLow = i >= 1 ? lowList[i - 1] : 0;
            var hiup = Math.Max(high - prevHigh, 0);
            var loup = Math.Max(low - prevLow, 0);
            var hidn = Math.Min(high - prevHigh, 0);
            var lodn = Math.Min(low - prevLow, 0);
            var highest = highestList[i];
            var lowest = lowestList[i];
            var range = highest - lowest;

            var bulls = range != 0 ? Math.Min((hiup + loup) / range, 1) * 100 : 0;
            bullsList.AddRounded(bulls);

            var bears = range != 0 ? Math.Max((hidn + lodn) / range, -1) * -100 : 0;
            bearsList.AddRounded(bears);
        }

        var avgBullsList = GetMovingAverageList(stockData, maType, length1, bullsList);
        var avgBearsList = GetMovingAverageList(stockData, maType, length1, bearsList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var avgBulls = avgBullsList[i];
            var avgBears = avgBearsList[i];

            var net = avgBulls - avgBears;
            netList.AddRounded(net);
        }

        var tpxList = GetMovingAverageList(stockData, maType, smoothLength, netList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var tpx = tpxList[i];
            var prevTpx = i >= 1 ? tpxList[i - 1] : 0;

            var signal = GetCompareSignal(tpx, prevTpx);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Tpx", tpxList },
            { "Bulls", avgBullsList },
            { "Bears", avgBearsList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = tpxList;
        stockData.IndicatorName = IndicatorName.TraderPressureIndex;

        return stockData;
    }

    /// <summary>
    /// Calculates the Technical Ratings
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="aoLength1"></param>
    /// <param name="aoLength2"></param>
    /// <param name="rsiLength"></param>
    /// <param name="stochLength1"></param>
    /// <param name="stochLength2"></param>
    /// <param name="stochLength3"></param>
    /// <param name="ultOscLength1"></param>
    /// <param name="ultOscLength2"></param>
    /// <param name="ultOscLength3"></param>
    /// <param name="ichiLength1"></param>
    /// <param name="ichiLength2"></param>
    /// <param name="ichiLength3"></param>
    /// <param name="vwmaLength"></param>
    /// <param name="cciLength"></param>
    /// <param name="adxLength"></param>
    /// <param name="momLength"></param>
    /// <param name="macdLength1"></param>
    /// <param name="macdLength2"></param>
    /// <param name="macdLength3"></param>
    /// <param name="bullBearLength"></param>
    /// <param name="williamRLength"></param>
    /// <param name="maLength1"></param>
    /// <param name="maLength2"></param>
    /// <param name="maLength3"></param>
    /// <param name="maLength4"></param>
    /// <param name="maLength5"></param>
    /// <param name="maLength6"></param>
    /// <param name="hullMaLength"></param>
    /// <returns></returns>
    public static StockData CalculateTechnicalRatings(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int aoLength1 = 55, int aoLength2 = 34, int rsiLength = 14, int stochLength1 = 14, int stochLength2 = 3, int stochLength3 = 3, 
        int ultOscLength1 = 7, int ultOscLength2 = 14, int ultOscLength3 = 28, int ichiLength1 = 9, int ichiLength2 = 26, int ichiLength3 = 52, 
        int vwmaLength = 20, int cciLength = 20, int adxLength = 14, int momLength = 10, int macdLength1 = 12, int macdLength2 = 26, 
        int macdLength3 = 9, int bullBearLength = 13, int williamRLength = 14, int maLength1 = 10, int maLength2 = 20, int maLength3 = 30, 
        int maLength4 = 50, int maLength5 = 100, int maLength6 = 200, int hullMaLength = 9)
    {
        List<double> maRatingList = new();
        List<double> oscRatingList = new();
        List<double> totalRatingList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var rsiList = CalculateRelativeStrengthIndex(stockData, length: rsiLength).CustomValuesList;
        var aoList = CalculateAwesomeOscillator(stockData, fastLength: aoLength1, slowLength: aoLength2).CustomValuesList;
        var macdItemsList = CalculateMovingAverageConvergenceDivergence(stockData, fastLength: macdLength1, slowLength: macdLength2, 
            signalLength: macdLength3);
        var macdList = macdItemsList.CustomValuesList;
        var macdSignalList = macdItemsList.OutputValues["Signal"];
        var uoList = CalculateUltimateOscillator(stockData, ultOscLength1, ultOscLength2, ultOscLength3).CustomValuesList;
        var ichiMokuList = CalculateIchimokuCloud(stockData, tenkanLength: ichiLength1, kijunLength: ichiLength2, senkouLength: ichiLength3);
        var tenkanList = ichiMokuList.OutputValues["TenkanSen"];
        var kijunList = ichiMokuList.OutputValues["KijunSen"];
        var senkouAList = ichiMokuList.OutputValues["SenkouSpanA"];
        var senkouBList = ichiMokuList.OutputValues["SenkouSpanB"];
        var adxItemsList = CalculateAverageDirectionalIndex(stockData, length: adxLength);
        var adxList = adxItemsList.CustomValuesList;
        var adxPlusList = adxItemsList.OutputValues["DiPlus"];
        var adxMinusList = adxItemsList.OutputValues["DiMinus"];
        var cciList = CalculateCommodityChannelIndex(stockData, length: cciLength).CustomValuesList;
        var bullBearPowerList = CalculateElderRayIndex(stockData, length: bullBearLength);
        var bullPowerList = bullBearPowerList.OutputValues["BullPower"];
        var bearPowerList = bullBearPowerList.OutputValues["BearPower"];
        var hullMaList = CalculateHullMovingAverage(stockData, length: hullMaLength).CustomValuesList;
        var williamsPctList = CalculateWilliamsR(stockData, length: williamRLength).CustomValuesList;
        var vwmaList = CalculateVolumeWeightedMovingAverage(stockData, length: vwmaLength).CustomValuesList;
        var stoList = CalculateStochasticOscillator(stockData, length: stochLength1, smoothLength1: stochLength2, smoothLength2: stochLength3);
        var stoKList = stoList.CustomValuesList;
        var stoDList = stoList.OutputValues["FastD"];
        var ma10List = GetMovingAverageList(stockData, maType, maLength1, inputList);
        var ma20List = GetMovingAverageList(stockData, maType, maLength2, inputList);
        var ma30List = GetMovingAverageList(stockData, maType, maLength3, inputList);
        var ma50List = GetMovingAverageList(stockData, maType, maLength4, inputList);
        var ma100List = GetMovingAverageList(stockData, maType, maLength5, inputList);
        var ma200List = GetMovingAverageList(stockData, maType, maLength6, inputList);
        var momentumList = CalculateMomentumOscillator(stockData, length: momLength).CustomValuesList;
        stockData.CustomValuesList = rsiList;
        var stoRsiList = CalculateStochasticOscillator(stockData, length: stochLength1, smoothLength1: stochLength2, smoothLength2: stochLength3);
        var stoRsiKList = stoRsiList.CustomValuesList;
        var stoRsiDList = stoRsiList.OutputValues["FastD"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var rsi = rsiList[i];
            var prevRsi = i >= 1 ? rsiList[i - 1] : 0;
            var ma10 = ma10List[i];
            var ma20 = ma20List[i];
            var ma30 = ma30List[i];
            var ma50 = ma50List[i];
            var ma100 = ma100List[i];
            var ma200 = ma200List[i];
            var hullMa = hullMaList[i];
            var vwma = vwmaList[i];
            var conLine = tenkanList[i];
            var baseLine = kijunList[i];
            var leadLine1 = senkouAList[i];
            var leadLine2 = senkouBList[i];
            var kSto = stoKList[i];
            var prevKSto = i >= 1 ? stoKList[i - 1] : 0;
            var dSto = stoDList[i];
            var prevDSto = i >= 1 ? stoDList[i - 1] : 0;
            var cci = cciList[i];
            var prevCci = i >= 1 ? cciList[i - 1] : 0;
            var adx = adxList[i];
            var adxPlus = adxPlusList[i];
            var prevAdxPlus = i >= 1 ? adxPlusList[i - 1] : 0;
            var adxMinus = adxMinusList[i];
            var prevAdxMinus = i >= 1 ? adxMinusList[i - 1] : 0;
            var ao = aoList[i];
            var prevAo1 = i >= 1 ? aoList[i - 1] : 0;
            var prevAo2 = i >= 2 ? aoList[i - 2] : 0;
            var mom = momentumList[i];
            var prevMom = i >= 1 ? momentumList[i - 1] : 0;
            var macd = macdList[i];
            var macdSig = macdSignalList[i];
            var kStoRsi = stoRsiKList[i];
            var prevKStoRsi = i >= 1 ? stoRsiKList[i - 1] : 0;
            var dStoRsi = stoRsiDList[i];
            var prevDStoRsi = i >= 1 ? stoRsiDList[i - 1] : 0;
            var upTrend = currentValue > ma50;
            var dnTrend = currentValue < ma50;
            var wr = williamsPctList[i];
            var prevWr = i >= 1 ? williamsPctList[i - 1] : 0;
            var bullPower = bullPowerList[i];
            var prevBullPower = i >= 1 ? bullPowerList[i - 1] : 0;
            var bearPower = bearPowerList[i];
            var prevBearPower = i >= 1 ? bearPowerList[i - 1] : 0;
            var uo = uoList[i];

            double maRating = 0;
            maRating += currentValue > ma10 ? 1 : currentValue < ma10 ? -1 : 0;
            maRating += currentValue > ma20 ? 1 : currentValue < ma20 ? -1 : 0;
            maRating += currentValue > ma30 ? 1 : currentValue < ma30 ? -1 : 0;
            maRating += currentValue > ma50 ? 1 : currentValue < ma50 ? -1 : 0;
            maRating += currentValue > ma100 ? 1 : currentValue < ma100 ? -1 : 0;
            maRating += currentValue > ma200 ? 1 : currentValue < ma200 ? -1 : 0;
            maRating += currentValue > hullMa ? 1 : currentValue < hullMa ? -1 : 0;
            maRating += currentValue > vwma ? 1 : currentValue < vwma ? -1 : 0;
            maRating += leadLine1 > leadLine2 && currentValue > leadLine1 && currentValue < baseLine && prevValue < conLine && 
                currentValue > conLine ? 1 : leadLine2 > leadLine1 &&
                currentValue < leadLine2 && currentValue > baseLine && prevValue > conLine && currentValue < conLine ? -1 : 0;
            maRating /= 9;
            maRatingList.AddRounded(maRating);

            double oscRating = 0;
            oscRating += rsi < 30 && prevRsi < rsi ? 1 : rsi > 70 && prevRsi > rsi ? -1 : 0;
            oscRating += kSto < 20 && dSto < 20 && kSto > dSto && prevKSto < prevDSto ? 1 : kSto > 80 && dSto > 80 && kSto < dSto && 
                prevKSto > prevDSto ? -1 : 0;
            oscRating += cci < -100 && cci > prevCci ? 1 : cci > 100 && cci < prevCci ? -1 : 0;
            oscRating += adx > 20 && prevAdxPlus < prevAdxMinus && adxPlus > adxMinus ? 1 : adx > 20 && prevAdxPlus > prevAdxMinus && 
                adxPlus < adxMinus ? -1 : 0;
            oscRating += (ao > 0 && prevAo1 < 0) || (ao > 0 && prevAo1 > 0 && ao > prevAo1 && prevAo2 > prevAo1) ? 1 : 
                (ao < 0 && prevAo1 > 0) || (ao < 0 && prevAo1 < 0 && ao < prevAo1 && prevAo2 < prevAo1) ? -1 : 0;
            oscRating += mom > prevMom ? 1 : mom < prevMom ? -1 : 0;
            oscRating += macd > macdSig ? 1 : macd < macdSig ? -1 : 0;
            oscRating += dnTrend && kStoRsi < 20 && dStoRsi < 20 && kStoRsi > dStoRsi && prevKStoRsi < prevDStoRsi ? 1 : upTrend && 
                kStoRsi > 80 && dStoRsi > 80 && kStoRsi < dStoRsi && prevKStoRsi > prevDStoRsi ? -1 : 0;
            oscRating += wr < -80 && wr > prevWr ? 1 : wr > -20 && wr < prevWr ? -1 : 0;
            oscRating += upTrend && bearPower < 0 && bearPower > prevBearPower ? 1 : dnTrend && bullPower > 0 && bullPower < prevBullPower ? -1 : 0;
            oscRating += uo > 70 ? 1 : uo < 30 ? -1 : 0;
            oscRating /= 11;
            oscRatingList.AddRounded(oscRating);

            var totalRating = (maRating + oscRating) / 2;
            totalRatingList.AddRounded(totalRating);

            var signal = GetConditionSignal(totalRating > 0.1, totalRating < -0.1);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Tr", totalRatingList },
            { "Or", oscRatingList },
            { "Mr", maRatingList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = totalRatingList;
        stockData.IndicatorName = IndicatorName.TechnicalRatings;

        return stockData;
    }

    /// <summary>
    /// Calculates the TTM Scalper Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <returns></returns>
    public static StockData CalculateTTMScalperIndicator(this StockData stockData)
    {
        List<double> buySellSwitchList = new();
        List<double> sbsList = new();
        List<double> clrsList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var close = inputList[i];
            var prevClose1 = i >= 1 ? inputList[i - 1] : 0;
            var prevClose2 = i >= 2 ? inputList[i - 2] : 0;
            var prevClose3 = i >= 3 ? inputList[i - 3] : 0;
            var high = highList[i];
            var low = lowList[i];
            double triggerSell = prevClose1 < close && (prevClose2 < prevClose1 || prevClose3 < prevClose1) ? 1 : 0;
            double triggerBuy = prevClose1 > close && (prevClose2 > prevClose1 || prevClose3 > prevClose1) ? 1 : 0;

            var prevBuySellSwitch = buySellSwitchList.LastOrDefault();
            var buySellSwitch = triggerSell == 1 ? 1 : triggerBuy == 1 ? 0 : prevBuySellSwitch;
            buySellSwitchList.AddRounded(buySellSwitch);

            var prevSbs = sbsList.LastOrDefault();
            var sbs = triggerSell == 1 && prevBuySellSwitch == 0 ? high : triggerBuy == 1 && prevBuySellSwitch == 1 ? low : prevSbs;
            sbsList.AddRounded(sbs);

            var prevClrs = clrsList.LastOrDefault();
            var clrs = triggerSell == 1 && prevBuySellSwitch == 0 ? 1 : triggerBuy == 1 && prevBuySellSwitch == 1 ? -1 : prevClrs;
            clrsList.AddRounded(clrs);

            var signal = GetCompareSignal(clrs, prevClrs);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Sbs", sbsList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = sbsList;
        stockData.IndicatorName = IndicatorName.TTMScalperIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the TFS Tether Line Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateTFSTetherLineIndicator(this StockData stockData, int length = 50)
    {
        List<double> tetherLineList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var highest = highestList[i];
            var lowest = lowestList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevTetherLine = tetherLineList.LastOrDefault();
            var tetherLine = (highest + lowest) / 2;
            tetherLineList.AddRounded(tetherLine);

            var signal = GetCompareSignal(currentValue - tetherLine, prevValue - prevTetherLine);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Tether", tetherLineList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = tetherLineList;
        stockData.IndicatorName = IndicatorName.TFSTetherLineIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates The Range Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateTheRangeIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage
        , int length = 10, int smoothLength = 3)
    {
        List<double> v1List = new();
        List<double> stochList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var tr = CalculateTrueRange(currentHigh, currentLow, prevValue);

            var v1 = i >= 1 && currentValue > prevValue ? tr / MinPastValues(i, 1, currentValue - prevValue) : tr;
            v1List.AddRounded(v1);

            var lbList = v1List.TakeLastExt(length).ToList();
            var v2 = lbList.Min();
            var v3 = lbList.Max();

            var stoch = v3 - v2 != 0 ? MinOrMax(100 * (v1 - v2) / (v3 - v2), 100, 0) : MinOrMax(100 * (v1 - v2), 100, 0);
            stochList.AddRounded(stoch);
        }

        var triList = GetMovingAverageList(stockData, maType, length, stochList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var tri = triList[i];
            var prevTri1 = i >= 1 ? triList[i - 1] : 0;
            var prevTri2 = i >= 2 ? triList[i - 2] : 0;

            var signal = GetRsiSignal(tri - prevTri1, prevTri1 - prevTri2, tri, prevTri1, 80, 20);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Tri", triList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = triList;
        stockData.IndicatorName = IndicatorName.TheRangeIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Time Price Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateTimePriceIndicator(this StockData stockData, int length = 50)
    {
        List<double> upperList = new();
        List<double> lowerList = new();
        List<double> risingList = new();
        List<double> fallingList = new();
        List<Signal> signalsList = new();
        var (_, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var highest = i >= 1 ? highestList[i - 1] : 0;
            var lowest = i >= 1 ? lowestList[i - 1] : 0;

            double rising = currentHigh > highest ? 1 : 0;
            risingList.AddRounded(rising);

            double falling = currentLow < lowest ? 1 : 0;
            fallingList.AddRounded(falling);

            double a = i - risingList.LastIndexOf(1);
            double b = i - fallingList.LastIndexOf(1);

            var prevUpper = upperList.LastOrDefault();
            var upper = length != 0 ? ((a > length ? length : a) / length) - 0.5 : 0;
            upperList.AddRounded(upper);

            var prevLower = lowerList.LastOrDefault();
            var lower = length != 0 ? ((b > length ? length : b) / length) - 0.5 : 0;
            lowerList.AddRounded(lower);

            var signal = GetCompareSignal((lower * -1) - (upper * -1), (prevLower * -1) - (prevUpper * -1));
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", upperList },
            { "LowerBand", lowerList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.TimePriceIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Rahul Mohindar Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="length4"></param>
    /// <returns></returns>
    public static StockData CalculateRahulMohindarOscillator(this StockData stockData, int length1 = 2, int length2 = 10, int length3 = 30, 
        int length4 = 81)
    {
        List<double> swingTrd1List = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(inputList, length2);

        var r1List = GetMovingAverageList(stockData, MovingAvgType.SimpleMovingAverage, length1, inputList);
        var r2List = GetMovingAverageList(stockData, MovingAvgType.SimpleMovingAverage, length1, r1List); //-V3056
        var r3List = GetMovingAverageList(stockData, MovingAvgType.SimpleMovingAverage, length1, r2List);
        var r4List = GetMovingAverageList(stockData, MovingAvgType.SimpleMovingAverage, length1, r3List);
        var r5List = GetMovingAverageList(stockData, MovingAvgType.SimpleMovingAverage, length1, r4List);
        var r6List = GetMovingAverageList(stockData, MovingAvgType.SimpleMovingAverage, length1, r5List);
        var r7List = GetMovingAverageList(stockData, MovingAvgType.SimpleMovingAverage, length1, r6List);
        var r8List = GetMovingAverageList(stockData, MovingAvgType.SimpleMovingAverage, length1, r7List);
        var r9List = GetMovingAverageList(stockData, MovingAvgType.SimpleMovingAverage, length1, r8List);
        var r10List = GetMovingAverageList(stockData, MovingAvgType.SimpleMovingAverage, length1, r9List);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var highest = highestList[i];
            var lowest = lowestList[i];
            var r1 = r1List[i];
            var r2 = r2List[i];
            var r3 = r3List[i];
            var r4 = r4List[i];
            var r5 = r5List[i];
            var r6 = r6List[i];
            var r7 = r7List[i];
            var r8 = r8List[i];
            var r9 = r9List[i];
            var r10 = r10List[i];

            var swingTrd1 = highest - lowest != 0 ? 100 * (currentValue - ((r1 + r2 + r3 + r4 + r5 + r6 + r7 + r8 + r9 + r10) / 10)) / 
                                                    (highest - lowest) : 0;
            swingTrd1List.AddRounded(swingTrd1);
        }

        var swingTrd2List = GetMovingAverageList(stockData, MovingAvgType.ExponentialMovingAverage, length3, swingTrd1List);
        var swingTrd3List = GetMovingAverageList(stockData, MovingAvgType.ExponentialMovingAverage, length3, swingTrd2List);
        var rmoList = GetMovingAverageList(stockData, MovingAvgType.ExponentialMovingAverage, length4, swingTrd1List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var rmo = rmoList[i];
            var prevRmo = i >= 1 ? rmoList[i - 1] : 0;

            var signal = GetCompareSignal(rmo, prevRmo);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Rmo", rmoList },
            { "SwingTrade1", swingTrd1List },
            { "SwingTrade2", swingTrd2List },
            { "SwingTrade3", swingTrd3List }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = rmoList;
        stockData.IndicatorName = IndicatorName.RahulMohindarOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Rainbow Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculateRainbowOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length1 = 2, int length2 = 10)
    {
        List<double> rainbowOscillatorList = new();
        List<double> upperBandList = new();
        List<double> lowerBandList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(inputList, length2);

        var r1List = GetMovingAverageList(stockData, maType, length1, inputList);
        var r2List = GetMovingAverageList(stockData, maType, length1, r1List); //-V3056
        var r3List = GetMovingAverageList(stockData, maType, length1, r2List);
        var r4List = GetMovingAverageList(stockData, maType, length1, r3List);
        var r5List = GetMovingAverageList(stockData, maType, length1, r4List);
        var r6List = GetMovingAverageList(stockData, maType, length1, r5List);
        var r7List = GetMovingAverageList(stockData, maType, length1, r6List);
        var r8List = GetMovingAverageList(stockData, maType, length1, r7List);
        var r9List = GetMovingAverageList(stockData, maType, length1, r8List);
        var r10List = GetMovingAverageList(stockData, maType, length1, r9List);

        for (var i = 0; i < stockData.Count; i++)
        {
            var highest = highestList[i];
            var lowest = lowestList[i];
            var currentValue = inputList[i];
            var r1 = r1List[i];
            var r2 = r2List[i];
            var r3 = r3List[i];
            var r4 = r4List[i];
            var r5 = r5List[i];
            var r6 = r6List[i];
            var r7 = r7List[i];
            var r8 = r8List[i];
            var r9 = r9List[i];
            var r10 = r10List[i];
            var highestRainbow = Math.Max(r1, Math.Max(r2, Math.Max(r3, Math.Max(r4, Math.Max(r5, Math.Max(r6, Math.Max(r7, Math.Max(r8, 
                Math.Max(r9, r10)))))))));
            var lowestRainbow = Math.Min(r1, Math.Min(r2, Math.Min(r3, Math.Min(r4, Math.Min(r5, Math.Min(r6, Math.Min(r7, Math.Min(r8, 
                Math.Min(r9, r10)))))))));

            var prevRainbowOscillator = rainbowOscillatorList.LastOrDefault();
            var rainbowOscillator = highest - lowest != 0 ? 100 * ((currentValue - ((r1 + r2 + r3 + r4 + r5 + r6 + r7 + r8 + r9 + r10) / 10)) / 
                                                                   (highest - lowest)) : 0;
            rainbowOscillatorList.AddRounded(rainbowOscillator);

            var upperBand = highest - lowest != 0 ? 100 * ((highestRainbow - lowestRainbow) / (highest - lowest)) : 0;
            upperBandList.AddRounded(upperBand);

            var lowerBand = -upperBand;
            lowerBandList.AddRounded(lowerBand);

            var signal = GetCompareSignal(rainbowOscillator, prevRainbowOscillator);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ro", rainbowOscillatorList },
            { "UpperBand", upperBandList },
            { "LowerBand", lowerBandList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = rainbowOscillatorList;
        stockData.IndicatorName = IndicatorName.RainbowOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Random Walk Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateRandomWalkIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, 
        int length = 14)
    {
        List<double> rwiLowList = new();
        List<double> rwiHighList = new();
        List<Signal> signalsList = new();
        var (_, highList, lowList, _, _) = GetInputValuesList(stockData);

        var atrList = CalculateAverageTrueRange(stockData, maType, length).CustomValuesList;
        var sqrt = Sqrt(length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentAtr = atrList[i];
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var prevHigh = i >= length ? highList[i - length] : 0;
            var prevLow = i >= length ? lowList[i - length] : 0;
            var bottom = currentAtr * sqrt;

            var prevRwiLow = rwiLowList.LastOrDefault();
            var rwiLow = bottom != 0 ? (prevHigh - currentLow) / bottom : 0;
            rwiLowList.AddRounded(rwiLow);

            var prevRwiHigh = rwiHighList.LastOrDefault();
            var rwiHigh = bottom != 0 ? (currentHigh - prevLow) / bottom : 0;
            rwiHighList.AddRounded(rwiHigh);

            var signal = GetCompareSignal(rwiHigh - rwiLow, prevRwiHigh - prevRwiLow);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "RwiHigh", rwiHighList },
            { "RwiLow", rwiLowList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.RandomWalkIndex;

        return stockData;
    }

    /// <summary>
    /// Calculates the Range Action Verification Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <returns></returns>
    public static StockData CalculateRangeActionVerificationIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int fastLength = 7, int slowLength = 65)
    {
        List<double> raviList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaFastList = GetMovingAverageList(stockData, maType, fastLength, inputList);
        var smaSlowList = GetMovingAverageList(stockData, maType, slowLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var fastMA = smaFastList[i];
            var slowMA = smaSlowList[i];
            var prevRavi1 = i >= 1 ? raviList[i - 1] : 0;
            var prevRavi2 = i >= 2 ? raviList[i - 2] : 0;

            var ravi = slowMA != 0 ? (fastMA - slowMA) / slowMA * 100 : 0;
            raviList.AddRounded(ravi);

            var signal = GetCompareSignal(ravi - prevRavi1, prevRavi1 - prevRavi2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ravi", raviList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = raviList;
        stockData.IndicatorName = IndicatorName.RangeActionVerificationIndex;

        return stockData;
    }

    /// <summary>
    /// Calculates the Really Simple Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateReallySimpleIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length = 21, int smoothLength = 10)
    {
        List<double> rsiList = new();
        List<Signal> signalsList = new();
        var (inputList, _, lowList, _, _) = GetInputValuesList(stockData);

        var maList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentLow = lowList[i];
            var currentMa = maList[i];

            var rsi = currentValue != 0 ? (currentLow - currentMa) / currentValue * 100 : 0;
            rsiList.AddRounded(rsi);
        }

        var rsiMaList = GetMovingAverageList(stockData, maType, smoothLength, rsiList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var rsi = rsiMaList[i];
            var prevRsiMa = i >= 1 ? rsiMaList[i - 1] : 0;
            var prevRsi = i >= 1 ? rsiList[i - 1] : 0;
            var rsiMa = rsiMaList[i];

            var signal = GetCompareSignal(rsi - rsiMa, prevRsi - prevRsiMa);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Rsi", rsiList },
            { "Signal", rsiMaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = rsiList;
        stockData.IndicatorName = IndicatorName.ReallySimpleIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Recursive Differenciator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="alpha"></param>
    /// <returns></returns>
    public static StockData CalculateRecursiveDifferenciator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length = 14, double alpha = 0.6)
    {
        List<double> bList = new();
        List<double> bChgList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var emaList = GetMovingAverageList(stockData, maType, length, inputList);
        stockData.CustomValuesList = emaList;
        var rsiList = CalculateRelativeStrengthIndex(stockData, length: length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var rsi = rsiList[i];
            var priorB = i >= length ? bList[i - length] : 0;
            var a = rsi / 100;
            var prevBChg1 = i >= 1 ? bChgList[i - 1] : a;
            var prevBChg2 = i >= 2 ? bChgList[i - 2] : 0;

            var b = (alpha * a) + ((1 - alpha) * prevBChg1);
            bList.AddRounded(b);

            var bChg = b - priorB;
            bChgList.AddRounded(bChg);

            var signal = GetCompareSignal(bChg - prevBChg1, prevBChg1 - prevBChg2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Rd", bList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = bList;
        stockData.IndicatorName = IndicatorName.RecursiveDifferenciator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Regression Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateRegressionOscillator(this StockData stockData, int length = 63)
    {
        List<double> roscList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var linRegList = CalculateLinearRegression(stockData, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentLinReg = linRegList[i];

            var prevRosc = roscList.LastOrDefault();
            var rosc = currentLinReg != 0 ? 100 * ((currentValue / currentLinReg) - 1) : 0;
            roscList.AddRounded(rosc);

            var signal = GetCompareSignal(rosc, prevRosc);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Rosc", roscList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = roscList;
        stockData.IndicatorName = IndicatorName.RegressionOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Relative Difference Of Squares Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateRelativeDifferenceOfSquaresOscillator(this StockData stockData, int length = 20)
    {
        List<double> rdosList = new();
        List<double> aList = new();
        List<double> dList = new();
        List<double> nList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            double a = currentValue > prevValue ? 1 : 0;
            aList.AddRounded(a);

            double d = currentValue < prevValue ? 1 : 0;
            dList.AddRounded(d);

            double n = currentValue == prevValue ? 1 : 0;
            nList.AddRounded(n);

            var prevRdos = rdosList.LastOrDefault();
            var aSum = aList.TakeLastExt(length).Sum();
            var dSum = dList.TakeLastExt(length).Sum();
            var nSum = nList.TakeLastExt(length).Sum();
            var rdos = aSum > 0 || dSum > 0 || nSum > 0 ? (Pow(aSum, 2) - Pow(dSum, 2)) / Pow(aSum + nSum + dSum, 2) : 0;
            rdosList.AddRounded(rdos);

            var signal = GetCompareSignal(rdos, prevRdos);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Rdos", rdosList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = rdosList;
        stockData.IndicatorName = IndicatorName.RelativeDifferenceOfSquaresOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Relative Spread Strength
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateRelativeSpreadStrength(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int fastLength = 10, int slowLength = 40, int length = 14, int smoothLength = 5)
    {
        List<double> spreadList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var fastEmaList = GetMovingAverageList(stockData, maType, fastLength, inputList);
        var slowEmaList = GetMovingAverageList(stockData, maType, slowLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var fastEma = fastEmaList[i];
            var slowEma = slowEmaList[i];

            var spread = fastEma - slowEma;
            spreadList.AddRounded(spread);
        }

        stockData.CustomValuesList = spreadList;
        var rsList = CalculateRelativeStrengthIndex(stockData, length: length).CustomValuesList;
        var rssList = GetMovingAverageList(stockData, maType, smoothLength, rsList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var rss = rssList[i];
            var prevRss1 = i >= 1 ? rssList[i - 1] : 0;
            var prevRss2 = i >= 2 ? rssList[i - 2] : 0;

            var signal = GetRsiSignal(rss - prevRss1, prevRss1 - prevRss2, rss, prevRss1, 80, 20);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Rss", rssList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = rssList;
        stockData.IndicatorName = IndicatorName.RelativeSpreadStrength;

        return stockData;
    }

    /// <summary>
    /// Calculates the Relative Strength 3D Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="marketData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="length4"></param>
    /// <param name="length5"></param>
    /// <returns></returns>
    public static StockData CalculateRelativeStrength3DIndicator(this StockData stockData,
            StockData marketData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 4, int length2 = 7, int length3 = 10,
            int length4 = 15, int length5 = 30)
    {
        List<double> r1List = new();
        List<double> rs3List = new();
        List<double> rs2List = new();
        List<double> xList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var (spInputList, _, _, _, _) = GetInputValuesList(marketData);

        if (stockData.Count == marketData.Count)
        {
            for (var i = 0; i < stockData.Count; i++)
            {
                var currentValue = inputList[i];
                var currentSp = spInputList[i];

                var prevR1 = r1List.LastOrDefault();
                var r1 = currentSp != 0 ? currentValue / currentSp * 100 : prevR1;
                r1List.AddRounded(r1);
            }

            var fastMaList = GetMovingAverageList(stockData, maType, length3, r1List);
            var medMaList = GetMovingAverageList(stockData, maType, length2, fastMaList);
            var slowMaList = GetMovingAverageList(stockData, maType, length4, fastMaList);
            var vSlowMaList = GetMovingAverageList(stockData, maType, length5, slowMaList);
            for (var i = 0; i < stockData.Count; i++)
            {
                var fastMa = fastMaList[i];
                var medMa = medMaList[i];
                var slowMa = slowMaList[i];
                var vSlowMa = vSlowMaList[i];
                double t1 = fastMa >= medMa && medMa >= slowMa && slowMa >= vSlowMa ? 10 : 0;
                double t2 = fastMa >= medMa && medMa >= slowMa && slowMa < vSlowMa ? 9 : 0;
                double t3 = fastMa < medMa && medMa >= slowMa && slowMa >= vSlowMa ? 9 : 0;
                double t4 = fastMa < medMa && medMa >= slowMa && slowMa < vSlowMa ? 5 : 0;

                var rs2 = t1 + t2 + t3 + t4;
                rs2List.AddRounded(rs2);
            }

            var rs2MaList = GetMovingAverageList(stockData, maType, length1, rs2List);
            for (var i = 0; i < stockData.Count; i++)
            {
                var rs2 = rs2List[i];
                var rs2Ma = rs2MaList[i];
                var prevRs3_1 = i >= 1 ? rs3List[i - 1] : 0;
                var prevRs3_2 = i >= 2 ? rs3List[i - 1] : 0;

                double x = rs2 >= 5 ? 1 : 0;
                xList.AddRounded(x);

                var rs3 = rs2 >= 5 || rs2 > rs2Ma ? xList.TakeLastExt(length4).Sum() / length4 * 100 : 0;
                rs3List.AddRounded(rs3);

                var signal = GetCompareSignal(rs3 - prevRs3_1, prevRs3_1 - prevRs3_2);
                signalsList.Add(signal);
            }
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Rs3d", rs3List }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = rs3List;
        stockData.IndicatorName = IndicatorName.RelativeStrength3DIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Relative Vigor Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateRelativeVigorIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 14)
    {
        List<double> rviList = new();
        List<double> numeratorList = new();
        List<double> denominatorList = new();
        List<double> signalLineList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, openList, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentClose = inputList[i];
            var currentOpen = openList[i];
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var prevOpen1 = i >= 1 ? openList[i - 1] : 0;
            var prevClose1 = i >= 1 ? inputList[i - 1] : 0;
            var prevHigh1 = i >= 1 ? highList[i - 1] : 0;
            var prevOpen2 = i >= 2 ? openList[i - 2] : 0;
            var prevClose2 = i >= 2 ? inputList[i - 2] : 0;
            var prevHigh2 = i >= 2 ? highList[i - 2] : 0;
            var prevOpen3 = i >= 3 ? openList[i - 3] : 0;
            var prevClose3 = i >= 3 ? inputList[i - 3] : 0;
            var prevHigh3 = i >= 3 ? highList[i - 3] : 0;
            var a = currentClose - currentOpen;
            var b = prevClose1 - prevOpen1;
            var c = prevClose2 - prevOpen2;
            var d = prevClose3 - prevOpen3;
            var e = currentHigh - currentLow;
            var f = prevHigh1 - prevOpen1;
            var g = prevHigh2 - prevOpen2;
            var h = prevHigh3 - prevOpen3;

            var numerator = (a + (2 * b) + (2 * c) + d) / 6;
            numeratorList.AddRounded(numerator);

            var denominator = (e + (2 * f) + (2 * g) + h) / 6;
            denominatorList.AddRounded(denominator);
        }

        var numeratorAvgList = GetMovingAverageList(stockData, maType, length, numeratorList);
        var denominatorAvgList = GetMovingAverageList(stockData, maType, length, denominatorList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var numeratorAvg = numeratorAvgList[i];
            var denominatorAvg = denominatorAvgList[i];
            var k = i >= 1 ? rviList[i - 1] : 0;
            var l = i >= 2 ? rviList[i - 2] : 0;
            var m = i >= 3 ? rviList[i - 3] : 0;

            var rvi = denominatorAvg != 0 ? numeratorAvg / denominatorAvg : 0;
            rviList.AddRounded(rvi);

            var prevSignalLine = signalLineList.LastOrDefault();
            var signalLine = (rvi + (2 * k) + (2 * l) + m) / 6;
            signalLineList.AddRounded(signalLine);

            var signal = GetCompareSignal(rvi - signalLine, k - prevSignalLine);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Rvi", rviList },
            { "Signal", signalLineList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = rviList;
        stockData.IndicatorName = IndicatorName.RelativeVigorIndex;

        return stockData;
    }

    /// <summary>
    /// Calculates the Repulse
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateRepulse(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 5)
    {
        List<double> bullPowerList = new();
        List<double> bearPowerList = new();
        List<double> repulseList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, openList, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentClose = inputList[i];
            var lowestLow = lowestList[i];
            var highestHigh = highestList[i];
            var prevOpen = i >= 1 ? openList[i - 1] : 0;

            var bullPower = currentClose != 0 ? 100 * ((3 * currentClose) - (2 * lowestLow) - prevOpen) / currentClose : 0;
            bullPowerList.AddRounded(bullPower);

            var bearPower = currentClose != 0 ? 100 * (prevOpen + (2 * highestHigh) - (3 * currentClose)) / currentClose : 0;
            bearPowerList.AddRounded(bearPower);
        }

        var bullPowerEmaList = GetMovingAverageList(stockData, maType, length * 5, bullPowerList);
        var bearPowerEmaList = GetMovingAverageList(stockData, maType, length * 5, bearPowerList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var bullPowerEma = bullPowerEmaList[i];
            var bearPowerEma = bearPowerEmaList[i];

            var repulse = bullPowerEma - bearPowerEma;
            repulseList.AddRounded(repulse);
        }

        var repulseEmaList = GetMovingAverageList(stockData, maType, length, repulseList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var repulse = repulseList[i];
            var prevRepulse = i >= 1 ? repulseList[i - 1] : 0;
            var repulseEma = repulseEmaList[i];
            var prevRepulseEma = i >= 1 ? repulseEmaList[i - 1] : 0;

            var signal = GetCompareSignal(repulse - repulseEma, prevRepulse - prevRepulseEma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Repulse", repulseList },
            { "Signal", repulseEmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = repulseList;
        stockData.IndicatorName = IndicatorName.Repulse;

        return stockData;
    }

    /// <summary>
    /// Calculates the Retrospective Candlestick Chart
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateRetrospectiveCandlestickChart(this StockData stockData, int length = 100)
    {
        List<double> absChgList = new();
        List<double> kList = new();
        List<double> cList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, openList, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentClose = inputList[i];
            var prevClose = i >= 1 ? inputList[i - 1] : 0;
            var currentOpen = openList[i];
            var prevK1 = i >= 1 ? kList[i - 1] : 0;
            var prevK2 = i >= 2 ? kList[i - 2] : 0;

            var absChg = Math.Abs(currentClose - prevClose);
            absChgList.AddRounded(absChg);

            var lbList = absChgList.TakeLastExt(length).ToList();
            var highest = lbList.Max();
            var lowest = lbList.Min();
            var s = highest - lowest != 0 ? (absChg - lowest) / (highest - lowest) * 100 : 0;
            var weight = s / 100;

            var prevC = i >= 1 ? cList[i - 1] : currentClose;
            var c = (weight * currentClose) + ((1 - weight) * prevC);
            cList.AddRounded(c);

            var prevH = i >= 1 ? prevC : currentHigh;
            var h = (weight * currentHigh) + ((1 - weight) * prevH);
            var prevL = i >= 1 ? prevC : currentLow;
            var l = (weight * currentLow) + ((1 - weight) * prevL);
            var prevO = i >= 1 ? prevC : currentOpen;
            var o = (weight * currentOpen) + ((1 - weight) * prevO);

            var k = (c + h + l + o) / 4;
            kList.AddRounded(k);

            var signal = GetCompareSignal(k - prevK1, prevK1 - prevK2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Rcc", kList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = kList;
        stockData.IndicatorName = IndicatorName.RetrospectiveCandlestickChart;

        return stockData;
    }

    /// <summary>
    /// Calculates the Rex Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateRexOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length = 14)
    {
        List<double> tvbList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, openList, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var close = inputList[i];
            var open = openList[i];
            var high = highList[i];
            var low = lowList[i];

            var tvb = (3 * close) - (low + open + high);
            tvbList.AddRounded(tvb);
        }

        var roList = GetMovingAverageList(stockData, maType, length, tvbList);
        var roEmaList = GetMovingAverageList(stockData, maType, length, roList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var ro = roList[i];
            var roEma = roEmaList[i];
            var prevRo = i >= 1 ? roList[i - 1] : 0;
            var prevRoEma = i >= 1 ? roEmaList[i - 1] : 0;

            var signal = GetCompareSignal(ro - roEma, prevRo - prevRoEma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ro", roList },
            { "Signal", roEmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = roList;
        stockData.IndicatorName = IndicatorName.RexOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Robust Weighting Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateRobustWeightingOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 200)
    {
        List<double> indexList = new();
        List<double> tempList = new();
        List<double> corrList = new();
        List<double> lList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            double index = i;
            indexList.AddRounded(index);

            var currentValue = inputList[i];
            tempList.AddRounded(currentValue);

            var corr = GoodnessOfFit.R(indexList.TakeLastExt(length).Select(x => (double)x), tempList.TakeLastExt(length).Select(x => (double)x));
            corr = IsValueNullOrInfinity(corr) ? 0 : corr;
            corrList.AddRounded((double)corr);
        }

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);
        var indexSmaList = GetMovingAverageList(stockData, maType, length, indexList);
        var stdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        stockData.CustomValuesList = indexList;
        var indexStdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var corr = corrList[i];
            var stdDev = stdDevList[i];
            var indexStdDev = indexStdDevList[i];
            var sma = smaList[i];
            var indexSma = indexSmaList[i];
            var a = indexStdDev != 0 ? corr * (stdDev / indexStdDev) : 0;
            var b = sma - (a * indexSma);

            var l = currentValue - a - (b * currentValue);
            lList.AddRounded(l);
        }

        var lSmaList = GetMovingAverageList(stockData, maType, length, lList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var l = lSmaList[i];
            var prevL1 = i >= 1 ? lSmaList[i - 1] : 0;
            var prevL2 = i >= 2 ? lSmaList[i - 2] : 0;

            var signal = GetCompareSignal(l - prevL1, prevL1 - prevL2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Rwo", lSmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = lSmaList;
        stockData.IndicatorName = IndicatorName.RobustWeightingOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the RSING Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateRSINGIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 20)
    {
        List<double> rsingList = new();
        List<double> upList = new();
        List<double> dnList = new();
        List<double> rangeList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, volumeList) = GetInputValuesList(stockData);

        var maList = GetMovingAverageList(stockData, maType, length, volumeList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var high = highList[i];
            var low = lowList[i];

            var range = high - low;
            rangeList.AddRounded(range);
        }

        stockData.CustomValuesList = rangeList;
        var stdevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentVolume = volumeList[i];
            var ma = maList[i];
            var stdev = stdevList[i];
            var range = rangeList[i];
            var currentValue = inputList[i];
            var prevValue = i >= length ? inputList[i - length] : 0;
            var vwr = ma != 0 ? currentVolume / ma : 0;
            var blr = stdev != 0 ? range / stdev : 0;
            var isUp = currentValue > prevValue;
            var isDn = currentValue < prevValue;
            var isEq = currentValue == prevValue;

            var prevUpCount = upList.LastOrDefault();
            var upCount = isEq ? 0 : isUp ? (prevUpCount <= 0 ? 1 : prevUpCount + 1) : (prevUpCount >= 0 ? -1 : prevUpCount - 1);
            upList.AddRounded(upCount);

            var prevDnCount = dnList.LastOrDefault();
            var dnCount = isEq ? 0 : isDn ? (prevDnCount <= 0 ? 1 : prevDnCount + 1) : (prevDnCount >= 0 ? -1 : prevDnCount - 1);
            dnList.AddRounded(dnCount);

            var pmo = MinPastValues(i, length, currentValue - prevValue);
            var rsing = vwr * blr * pmo;
            rsingList.AddRounded(rsing);
        }

        var rsingMaList = GetMovingAverageList(stockData, maType, length, rsingList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var rsing = rsingMaList[i];
            var prevRsing1 = i >= 1 ? rsingMaList[i - 1] : 0;
            var prevRsing2 = i >= 2 ? rsingMaList[i - 2] : 0;

            var signal = GetCompareSignal(rsing - prevRsing1, prevRsing1 - prevRsing2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Rsing", rsingList },
            { "Signal", rsingMaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = rsingList;
        stockData.IndicatorName = IndicatorName.RSINGIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the RSMK Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="marketData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateRSMKIndicator(this StockData stockData, StockData marketData, 
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 90, int smoothLength = 3)
    {
        List<double> rsmkList = new();
        List<double> logRatioList = new();
        List<double> logDiffList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var (spInputList, _, _, _, _) = GetInputValuesList(marketData);

        if (stockData.Count == marketData.Count)
        {
            for (var i = 0; i < stockData.Count; i++)
            {
                var currentValue = inputList[i];
                var spValue = spInputList[i];
                var prevLogRatio = i >= length ? logRatioList[i - length] : 0;

                var logRatio = spValue != 0 ? currentValue / spValue : 0;
                logRatioList.AddRounded(logRatio);

                var logDiff = logRatio - prevLogRatio;
                logDiffList.AddRounded(logDiff);
            }

            var logDiffEmaList = GetMovingAverageList(stockData, maType, smoothLength, logDiffList);
            for (var i = 0; i < stockData.Count; i++)
            {
                var logDiffEma = logDiffEmaList[i];

                var prevRsmk = rsmkList.LastOrDefault();
                var rsmk = logDiffEma * 100;
                rsmkList.AddRounded(rsmk);

                var signal = GetCompareSignal(rsmk, prevRsmk);
                signalsList.Add(signal);
            }
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Rsmk", rsmkList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = rsmkList;
        stockData.IndicatorName = IndicatorName.RSMKIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Running Equity
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateRunningEquity(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 100)
    {
        List<double> chgXList = new();
        List<double> reqList = new();
        List<double> xList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var sma = smaList[i];

            var prevX = xList.LastOrDefault();
            double x = Math.Sign(currentValue - sma);
            xList.AddRounded(x);

            var chgX = MinPastValues(i, 1, currentValue - prevValue) * prevX;
            chgXList.AddRounded(chgX);

            var prevReq = reqList.LastOrDefault();
            var req = chgXList.TakeLastExt(length).Sum();
            reqList.AddRounded(req);

            var signal = GetCompareSignal(req, prevReq);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Req", reqList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = reqList;
        stockData.IndicatorName = IndicatorName.RunningEquity;

        return stockData;
    }

    /// <summary>
    /// Calculates the Mass Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculateMassIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length1 = 21, int length2 = 21, int length3 = 25, int signalLength = 9)
    {
        List<double> highLowList = new();
        List<double> ratioList = new();
        List<double> massIndexList = new();
        List<Signal> signalsList = new();
        var (_, highList, lowList, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];

            var highLow = currentHigh - currentLow;
            highLowList.AddRounded(highLow);
        }

        var firstEmaList = GetMovingAverageList(stockData, maType, length1, highLowList);
        var secondEmaList = GetMovingAverageList(stockData, maType, length2, firstEmaList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var firstEma = firstEmaList[i];
            var secondEma = secondEmaList[i];

            var ratio = secondEma != 0 ? firstEma / secondEma : 0;
            ratioList.AddRounded(ratio);

            var massIndex = ratioList.TakeLastExt(length3).Sum();
            massIndexList.AddRounded(massIndex);
        }

        var massIndexSignalList = GetMovingAverageList(stockData, maType, signalLength, massIndexList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var massIndex = massIndexList[i];
            var massIndexEma = massIndexSignalList[i];
            var prevMassIndex = i >= 1 ? massIndexList[i - 1] : 0;
            var prevMassIndexEma = i >= 1 ? massIndexSignalList[i - 1] : 0;

            var signal = GetCompareSignal(massIndex - massIndexEma, prevMassIndex - prevMassIndexEma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Mi", massIndexList },
            { "Signal", massIndexSignalList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = massIndexList;
        stockData.IndicatorName = IndicatorName.MassIndex;

        return stockData;
    }

    /// <summary>
    /// Calculates the Mass Thrust Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateMassThrustOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length = 14)
    {
        List<double> topList = new();
        List<double> botList = new();
        List<double> mtoList = new();
        List<double> advList = new();
        List<double> decList = new();
        List<double> advVolList = new();
        List<double> decVolList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentVolume = volumeList[i];

            var adv = i >= 1 && currentValue > prevValue ? MinPastValues(i, 1, currentValue - prevValue) : 0;
            advList.AddRounded(adv);

            var dec = i >= 1 && currentValue < prevValue ? MinPastValues(i, 1, prevValue - currentValue) : 0;
            decList.AddRounded(dec);

            var advSum = advList.TakeLastExt(length).Sum();
            var decSum = decList.TakeLastExt(length).Sum();

            var advVol = currentValue > prevValue && advSum != 0 ? currentVolume / advSum : 0;
            advVolList.AddRounded(advVol);

            var decVol = currentValue < prevValue && decSum != 0 ? currentVolume / decSum : 0;
            decVolList.AddRounded(decVol);

            var advVolSum = advVolList.TakeLastExt(length).Sum();
            var decVolSum = decVolList.TakeLastExt(length).Sum();

            var top = (advSum * advVolSum) - (decSum * decVolSum);
            topList.AddRounded(top);

            var bot = (advSum * advVolSum) + (decSum * decVolSum);
            botList.AddRounded(bot);

            var mto = bot != 0 ? 100 * top / bot : 0;
            mtoList.AddRounded(mto);
        }

        var mtoEmaList = GetMovingAverageList(stockData, maType, length, mtoList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var mto = mtoList[i];
            var mtoEma = mtoEmaList[i];
            var prevMto = i >= 1 ? mtoList[i - 1] : 0;
            var prevMtoEma = i >= 1 ? mtoEmaList[i - 1] : 0;

            var signal = GetRsiSignal(mto - mtoEma, prevMto - prevMtoEma, mto, prevMto, 50, -50);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Mto", mtoList },
            { "Signal", mtoEmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = mtoList;
        stockData.IndicatorName = IndicatorName.MassThrustOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Midpoint Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculateMidpointOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 26, int signalLength = 9)
    {
        List<double> moList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var hh = highestList[i];
            var ll = lowestList[i];

            var mo = hh - ll != 0 ? MinOrMax(100 * ((2 * currentValue) - hh - ll) / (hh - ll), 100, -100) : 0;
            moList.AddRounded(mo);
        }

        var moEmaList = GetMovingAverageList(stockData, maType, signalLength, moList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var mo = moList[i];
            var moEma = moEmaList[i];
            var prevMo = i >= 1 ? moList[i - 1] : 0;
            var prevMoEma = i >= 1 ? moEmaList[i - 1] : 0;

            var signal = GetRsiSignal(mo - moEma, prevMo - prevMoEma, mo, prevMo, 70, -70);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Mo", moList },
            { "Signal", moEmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = moList;
        stockData.IndicatorName = IndicatorName.MidpointOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Morphed Sine Wave
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="power"></param>
    /// <returns></returns>
    public static StockData CalculateMorphedSineWave(this StockData stockData, int length = 14, double power = 100)
    {
        List<double> sList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var p = length / (2 * Math.PI);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevS1 = i >= 1 ? sList[i - 1] : 0;
            var prevS2 = i >= 2 ? sList[i - 2] : 0;
            var c = (currentValue * power) + Math.Sin(i / p);

            var s = c / power;
            sList.AddRounded(s);

            var signal = GetCompareSignal(s - prevS1, prevS1 - prevS2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Msw", sList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = sList;
        stockData.IndicatorName = IndicatorName.MorphedSineWave;

        return stockData;
    }

    /// <summary>
    /// Calculates the Move Tracker
    /// </summary>
    /// <param name="stockData"></param>
    /// <returns></returns>
    public static StockData CalculateMoveTracker(this StockData stockData)
    {
        List<double> mtList = new();
        List<double> mtSignalList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevMt = mtList.LastOrDefault();
            var mt = MinPastValues(i, 1, currentValue - prevValue);
            mtList.AddRounded(mt);

            var prevMtSignal = mtSignalList.LastOrDefault();
            var mtSignal = mt - prevMt;
            mtSignalList.AddRounded(mtSignal);

            var signal = GetCompareSignal(mt - mtSignal, prevMt - prevMtSignal);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Mt", mtList },
            { "Signal", mtSignalList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = mtList;
        stockData.IndicatorName = IndicatorName.MoveTracker;

        return stockData;
    }

    /// <summary>
    /// Calculates the Multi Level Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="factor"></param>
    /// <returns></returns>
    public static StockData CalculateMultiLevelIndicator(this StockData stockData, int length = 14, double factor = 10000)
    {
        List<double> zList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, openList, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevOpen = i >= length ? openList[i - length] : 0;
            var currentOpen = openList[i];
            var currentClose = inputList[i];
            var prevZ1 = i >= 1 ? zList[i - 1] : 0;
            var prevZ2 = i >= 2 ? zList[i - 2] : 0;

            var z = (currentClose - currentOpen - (currentClose - prevOpen)) * factor;
            zList.AddRounded(z);

            var signal = GetRsiSignal(z - prevZ1, prevZ1 - prevZ2, z, prevZ1, 5, -5);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Mli", zList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = zList;
        stockData.IndicatorName = IndicatorName.MultiLevelIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Modified Gann Hilo Activator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="mult"></param>
    /// <returns></returns>
    public static StockData CalculateModifiedGannHiloActivator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 50, double mult = 1)
    {
        List<double> gannHiloList = new();
        List<double> cList = new();
        List<double> dList = new();
        List<double> gList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, openList, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var highestHigh = highestList[i];
            var lowestLow = lowestList[i];
            var currentClose = inputList[i];
            var currentOpen = openList[i];
            var max = Math.Max(currentClose, currentOpen);
            var min = Math.Min(currentClose, currentOpen);
            var a = highestHigh - max;
            var b = min - lowestLow;

            var c = max + (a * mult);
            cList.AddRounded(c);

            var d = min - (b * mult);
            dList.AddRounded(d);
        }

        var eList = GetMovingAverageList(stockData, maType, length, cList);
        var fList = GetMovingAverageList(stockData, maType, length, dList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentClose = inputList[i];
            var prevClose = i >= 1 ? inputList[i - 1] : 0;
            var f = fList[i];
            var e = eList[i];

            var prevG = gList.LastOrDefault();
            var g = currentClose > e ? 1 : currentClose > f ? 0 : prevG;
            gList.AddRounded(g);

            var prevGannHilo = gannHiloList.LastOrDefault();
            var gannHilo = (g * f) + ((1 - g) * e);
            gannHiloList.AddRounded(gannHilo);

            var signal = GetCompareSignal(currentClose - gannHilo, prevClose - prevGannHilo);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ghla", gannHiloList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = gannHiloList;
        stockData.IndicatorName = IndicatorName.ModifiedGannHiloActivator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Market Direction Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <returns></returns>
    public static StockData CalculateMarketDirectionIndicator(this StockData stockData, int fastLength = 13, int slowLength = 55)
    {
        List<double> mdiList = new();
        List<double> cp2List = new();
        List<double> tempList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            tempList.AddRounded(currentValue);

            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var len1Sum = tempList.TakeLastExt(fastLength - 1).Sum();
            var len2Sum = tempList.TakeLastExt(slowLength - 1).Sum();

            var prevCp2 = cp2List.LastOrDefault();
            var cp2 = ((fastLength * len2Sum) - (slowLength * len1Sum)) / (slowLength - fastLength);
            cp2List.AddRounded(cp2);

            var prevMdi = mdiList.LastOrDefault();
            var mdi = currentValue + prevValue != 0 ? 100 * (prevCp2 - cp2) / ((currentValue + prevValue) / 2) : 0;
            mdiList.AddRounded(mdi);

            var signal = GetCompareSignal(mdi, prevMdi);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Mdi", mdiList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = mdiList;
        stockData.IndicatorName = IndicatorName.MarketDirectionIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Mobility Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculateMobilityOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length1 = 10, int length2 = 14, int signalLength = 7)
    {
        List<double> moList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length2);

        for (var i = 0; i < stockData.Count; i++)
        {
            var hMax = highestList[i];
            var lMin = lowestList[i];
            var prevC = i >= length2 ? inputList[i - length2] : 0;
            var rx = length1 != 0 ? (hMax - lMin) / length1 : 0;

            var imx = 1;
            double pdfmx = 0, pdfc = 0, rx1, bu, bl, bu1, bl1, pdf;
            for (var j = 1; j <= length1; j++)
            {
                bu = lMin + (j * rx);
                bl = bu - rx;

                var currHigh = i >= j ? highList[i - j] : 0;
                var currLow = i >= j ? lowList[i - j] : 0;
                double hMax1 = currHigh, lMin1 = currLow;
                for (var k = 2; k < length2; k++)
                {
                    var high = i >= j + k ? highList[i - (j + k)] : 0;
                    var low = i >= j + k ? lowList[i - (j + k)] : 0;
                    hMax1 = Math.Max(high, hMax1);
                    lMin1 = Math.Min(low, lMin1);
                }

                rx1 = length1 != 0 ? (hMax1 - lMin1) / length1 : 0; //-V3022
                bl1 = lMin1 + ((j - 1) * rx1);
                bu1 = lMin1 + (j * rx1);

                pdf = 0;
                for (var k = 1; k <= length2; k++)
                {
                    var high = i >= j + k ? highList[i - (j + k)] : 0;
                    var low = i >= j + k ? lowList[i - (j + k)] : 0;

                    if (high <= bu1)
                    {
                        pdf += 1;
                    }
                    if (high <= bu1 || low >= bu1)
                    {
                        if (high <= bl1)
                        {
                            pdf -= 1;
                        }
                        if (high <= bl || low >= bl1)
                        {
                            continue;
                        }
                        else
                        {
                            pdf -= high - low != 0 ? (bl1 - low) / (high - low) : 0;
                        }
                    }
                    else
                    {
                        pdf += high - low != 0 ? (bu1 - low) / (high - low) : 0;
                    }
                }

                pdf = length2 != 0 ? pdf / length2 : 0;
                pdfmx = j == 1 ? pdf : pdfmx;
                imx = j == 1 ? j : imx;
                pdfmx = Math.Max(pdf, pdfmx);
                pdfc = j == 1 ? pdf : pdfc;
                pdfc = prevC > bl && prevC <= bu ? pdf : pdfc;
            }

            var pmo = lMin + ((imx - 0.5) * rx);
            var mo = pdfmx != 0 ? 100 * (1 - (pdfc / pdfmx)) : 0;
            mo = prevC < pmo ? -mo : mo;
            moList.AddRounded(-mo);
        }

        var moWmaList = GetMovingAverageList(stockData, maType, signalLength, moList);
        var moSigList = GetMovingAverageList(stockData, maType, signalLength, moWmaList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var mo = moWmaList[i];
            var moSig = moSigList[i];
            var prevMo = i >= 1 ? moWmaList[i - 1] : 0;
            var prevMoSig = i >= 1 ? moSigList[i - 1] : 0;

            var signal = GetCompareSignal(mo - moSig, prevMo - prevMoSig);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Mo", moWmaList },
            { "Signal", moSigList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = moWmaList;
        stockData.IndicatorName = IndicatorName.MobilityOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Mass Thrust Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateMassThrustIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 14)
    {
        List<double> mtiList = new();
        List<double> advList = new();
        List<double> decList = new();
        List<double> advVolList = new();
        List<double> decVolList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentVolume = volumeList[i];

            var adv = i >= 1 && currentValue > prevValue ? MinPastValues(i, 1, currentValue - prevValue) : 0;
            advList.AddRounded(adv);

            var dec = i >= 1 && currentValue < prevValue ? MinPastValues(i, 1, prevValue - currentValue) : 0;
            decList.AddRounded(dec);

            var advSum = advList.TakeLastExt(length).Sum();
            var decSum = decList.TakeLastExt(length).Sum();

            var advVol = currentValue > prevValue && advSum != 0 ? currentVolume / advSum : 0;
            advVolList.AddRounded(advVol);

            var decVol = currentValue < prevValue && decSum != 0 ? currentVolume / decSum : 0;
            decVolList.AddRounded(decVol);

            var advVolSum = advVolList.TakeLastExt(length).Sum();
            var decVolSum = decVolList.TakeLastExt(length).Sum();

            var mti = ((advSum * advVolSum) - (decSum * decVolSum)) / 1000000;
            mtiList.AddRounded(mti);
        }

        var mtiEmaList = GetMovingAverageList(stockData, maType, length, mtiList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var mtiEma = mtiEmaList[i];
            var prevMtiEma = i >= 1 ? mtiEmaList[i - 1] : 0;

            var signal = GetCompareSignal(mtiEma, prevMtiEma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Mti", mtiList },
            { "Signal", mtiEmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = mtiList;
        stockData.IndicatorName = IndicatorName.MassThrustIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Decision Point Breadth Swenlin Trading Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateDecisionPointBreadthSwenlinTradingOscillator(this StockData stockData, 
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 5)
    {
        List<double> iList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            double advance = currentValue > prevValue ? 1 : 0;
            double decline = currentValue < prevValue ? 1 : 0;

            var iVal = advance + decline != 0 ? 1000 * (advance - decline) / (advance + decline) : 0;
            iList.AddRounded(iVal);
        }

        var ivalEmaList = GetMovingAverageList(stockData, maType, length, iList);
        var stoList = GetMovingAverageList(stockData, maType, length, ivalEmaList);
        var stoEmaList = GetMovingAverageList(stockData, maType, length, stoList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var sto = stoList[i];
            var stoEma = stoEmaList[i];
            var prevSto = i >= 1 ? stoList[i - 1] : 0;
            var prevStoEma = i >= 1 ? stoEmaList[i - 1] : 0;

            var signal = GetCompareSignal(sto - stoEma, prevSto - prevStoEma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Dpbsto", stoList },
            { "Signal", stoEmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = stoList;
        stockData.IndicatorName = IndicatorName.DecisionPointBreadthSwenlinTradingOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Delta Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculateDeltaMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length1 = 10, int length2 = 5)
    {
        List<double> deltaList = new();
        List<double> deltaHistogramList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, openList, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentClose = inputList[i];
            var prevOpen = i >= length2 ? openList[i - length2] : 0;

            var delta = currentClose - prevOpen;
            deltaList.AddRounded(delta);
        }

        var deltaSmaList = GetMovingAverageList(stockData, maType, length1, deltaList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var delta = deltaList[i];
            var deltaSma = deltaSmaList[i];

            var prevDeltaHistogram = deltaHistogramList.LastOrDefault();
            var deltaHistogram = delta - deltaSma;
            deltaHistogramList.AddRounded(deltaHistogram);

            var signal = GetCompareSignal(deltaHistogram, prevDeltaHistogram);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Delta", deltaList },
            { "Signal", deltaSmaList },
            { "Histogram", deltaHistogramList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = deltaList;
        stockData.IndicatorName = IndicatorName.DeltaMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Detrended Synthetic Price
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateDetrendedSyntheticPrice(this StockData stockData, int length = 14)
    {
        List<double> dspList = new();
        List<double> ema1List = new();
        List<double> ema2List = new();
        List<Signal> signalsList = new();
        var (_, highList, lowList, _, _) = GetInputValuesList(stockData);

        var alpha = length > 2 ? (double)2 / (length + 1) : 0.67;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var prevHigh = i >= 1 ? highList[i - 1] : 0;
            var prevLow = i >= 1 ? lowList[i - 1] : 0;
            var high = Math.Max(currentHigh, prevHigh);
            var low = Math.Min(currentLow, prevLow);
            var price = (high + low) / 2;
            var prevEma1 = i >= 1 ? ema1List[i - 1] : price;
            var prevEma2 = i >= 1 ? ema2List[i - 1] : price;

            var ema1 = (alpha * price) + ((1 - alpha) * prevEma1);
            ema1List.AddRounded(ema1);

            var ema2 = (alpha / 2 * price) + ((1 - (alpha / 2)) * prevEma2);
            ema2List.AddRounded(ema2);

            var prevDsp = dspList.LastOrDefault();
            var dsp = ema1 - ema2;
            dspList.AddRounded(dsp);

            var signal = GetCompareSignal(dsp, prevDsp);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Dsp", dspList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = dspList;
        stockData.IndicatorName = IndicatorName.DetrendedSyntheticPrice;

        return stockData;
    }

    /// <summary>
    /// Calculates the Derivative Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="length4"></param>
    /// <returns></returns>
    public static StockData CalculateDerivativeOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 14,
        int length2 = 9, int length3 = 5, int length4 = 3)
    {
        List<double> s1List = new();
        List<double> s2List = new();
        List<double> s1SmaList = new();
        List<Signal> signalsList = new();

        var rsiList = CalculateRelativeStrengthIndex(stockData, maType, length: length1).CustomValuesList;
        var rsiEma1List = GetMovingAverageList(stockData, maType, length3, rsiList);
        var rsiEma2List = GetMovingAverageList(stockData, maType, length4, rsiEma1List);

        for (var i = 0; i < rsiList.Count; i++)
        {
            var prevS1 = s1List.LastOrDefault();
            var s1 = rsiEma2List[i];
            s1List.AddRounded(s1);

            var prevS1Sma = s1SmaList.LastOrDefault();
            var s1Sma = s1List.TakeLastExt(length2).Average();
            s1SmaList.AddRounded(s1Sma);

            var s2 = s1 - s1Sma;
            s2List.AddRounded(s2);

            var signal = GetCompareSignal(s1 - s1Sma, prevS1 - prevS1Sma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Do", s2List }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = s2List;
        stockData.IndicatorName = IndicatorName.DerivativeOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Demand Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <returns></returns>
    public static StockData CalculateDemandOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length1 = 10, int length2 = 2, int length3 = 20)
    {
        List<double> rangeList = new();
        List<double> doList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length2);

        for (var i = 0; i < stockData.Count; i++)
        {
            var highest = highestList[i];
            var lowest = lowestList[i];

            var range = highest - lowest;
            rangeList.AddRounded(range);
        }

        var vaList = GetMovingAverageList(stockData, maType, length1, rangeList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var va = vaList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var pctChg = prevValue != 0 ? MinPastValues(i, 1, currentValue - prevValue) / Math.Abs(prevValue) * 100 : 0;
            var currentVolume = stockData.Volumes[i];
            var k = va != 0 ? (3 * currentValue) / va : 0;
            var pctK = pctChg * k;
            var volPctK = pctK != 0 ? currentVolume / pctK : 0;
            var bp = currentValue > prevValue ? currentVolume : volPctK;
            var sp = currentValue > prevValue ? volPctK : currentVolume;

            var dosc = bp - sp;
            doList.AddRounded(dosc);
        }

        var doEmaList = GetMovingAverageList(stockData, maType, length3, doList);
        var doSigList = GetMovingAverageList(stockData, maType, length1, doEmaList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var doSig = doSigList[i];
            var prevSig1 = i >= 1 ? doSigList[i - 1] : 0;
            var prevSig2 = i >= 2 ? doSigList[i - 1] : 0;

            var signal = GetCompareSignal(doSig - prevSig1, prevSig1 - prevSig2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Do", doEmaList },
            { "Signal", doSigList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = doEmaList;
        stockData.IndicatorName = IndicatorName.DemandOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Double Smoothed Momenta
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <returns></returns>
    public static StockData CalculateDoubleSmoothedMomenta(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 2,
        int length2 = 5, int length3 = 25)
    {
        List<double> momList = new();
        List<double> srcLcList = new();
        List<double> hcLcList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(inputList, length1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var hc = highestList[i];
            var lc = lowestList[i];

            var srcLc = currentValue - lc;
            srcLcList.AddRounded(srcLc);

            var hcLc = hc - lc;
            hcLcList.AddRounded(hcLc);
        }

        var topEma1List = GetMovingAverageList(stockData, maType, length2, srcLcList);
        var topEma2List = GetMovingAverageList(stockData, maType, length3, topEma1List);
        var botEma1List = GetMovingAverageList(stockData, maType, length2, hcLcList);
        var botEma2List = GetMovingAverageList(stockData, maType, length3, botEma1List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var top = topEma2List[i];
            var bot = botEma2List[i];

            var mom = bot != 0 ? MinOrMax(100 * top / bot, 100, 0) : 0;
            momList.AddRounded(mom);
        }

        var momEmaList = GetMovingAverageList(stockData, maType, length3, momList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var mom = momList[i];
            var momEma = momEmaList[i];
            var prevMom = i >= 1 ? momList[i - 1] : 0;
            var prevMomEma = i >= 1 ? momEmaList[i - 1] : 0;

            var signal = GetCompareSignal(mom - momEma, prevMom - prevMomEma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Dsm", momList },
            { "Signal", momEmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = momList;
        stockData.IndicatorName = IndicatorName.DoubleSmoothedMomenta;

        return stockData;
    }

    /// <summary>
    /// Calculates the Didi Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <returns></returns>
    public static StockData CalculateDidiIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 3, int length2 = 8,
        int length3 = 20)
    {
        List<double> curtaList = new();
        List<double> longaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var mediumSmaList = GetMovingAverageList(stockData, maType, length2, inputList);
        var shortSmaList = GetMovingAverageList(stockData, maType, length1, inputList);
        var longSmaList = GetMovingAverageList(stockData, maType, length3, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var mediumSma = mediumSmaList[i];
            var shortSma = shortSmaList[i];
            var longSma = longSmaList[i];

            var prevCurta = curtaList.LastOrDefault();
            var curta = mediumSma != 0 ? shortSma / mediumSma : 0;
            curtaList.AddRounded(curta);

            var prevLonga = longaList.LastOrDefault();
            var longa = mediumSma != 0 ? longSma / mediumSma : 0;
            longaList.AddRounded(longa);

            var signal = GetCompareSignal(curta - longa, prevCurta - prevLonga);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Curta", curtaList },
            { "Media", mediumSmaList },
            { "Longa", longSmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.DidiIndex;

        return stockData;
    }

    /// <summary>
    /// Calculates the Disparity Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateDisparityIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14)
    {
        List<double> disparityIndexList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentSma = smaList[i];

            var prevDisparityIndex = disparityIndexList.LastOrDefault();
            var disparityIndex = currentSma != 0 ? (currentValue - currentSma) / currentSma * 100 : 0;
            disparityIndexList.AddRounded(disparityIndex);

            var signal = GetCompareSignal(disparityIndex, prevDisparityIndex);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Di", disparityIndexList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = disparityIndexList;
        stockData.IndicatorName = IndicatorName.DisparityIndex;

        return stockData;
    }

    /// <summary>
    /// Calculates the Damping Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="threshold"></param>
    /// <returns></returns>
    public static StockData CalculateDampingIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 5, 
        double threshold = 1.5)
    {
        List<double> rangeList = new();
        List<double> diList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            
            var range = currentHigh - currentLow;
            rangeList.AddRounded(range);
        }

        var rangeSmaList = GetMovingAverageList(stockData, maType, length, rangeList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevSma1 = i >= 1 ? rangeSmaList[i - 1] : 0;
            var prevSma6 = i >= 6 ? rangeSmaList[i - 6] : 0;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevSma = i >= 1 ? smaList[i - 1] : 0;
            var currentSma = smaList[i];

            var di = prevSma6 != 0 ? prevSma1 / prevSma6 : 0;
            diList.AddRounded(di);

            var signal = GetVolatilitySignal(currentValue - currentSma, prevValue - prevSma, di, threshold);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Di", diList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = diList;
        stockData.IndicatorName = IndicatorName.DampingIndex;

        return stockData;
    }

    /// <summary>
    /// Calculates the Directional Trend Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <returns></returns>
    public static StockData CalculateDirectionalTrendIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 14,
        int length2 = 10, int length3 = 5)
    {
        List<double> dtiList = new();
        List<double> diffList = new();
        List<double> absDiffList = new();
        List<Signal> signalsList = new();
        var (_, highList, lowList, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var prevHigh = i >= 1 ? highList[i - 1] : 0;
            var prevLow = i >= 1 ? lowList[i - 1] : 0;
            var hmu = currentHigh - prevHigh > 0 ? currentHigh - prevHigh : 0;
            var lmd = currentLow - prevLow < 0 ? (currentLow - prevLow) * -1 : 0;

            var diff = hmu - lmd;
            diffList.AddRounded(diff);

            var absDiff = Math.Abs(diff);
            absDiffList.AddRounded(absDiff);
        }
        
        var diffEma1List = GetMovingAverageList(stockData, maType, length1, diffList);
        var absDiffEma1List = GetMovingAverageList(stockData, maType, length1, absDiffList);
        var diffEma2List = GetMovingAverageList(stockData, maType, length2, diffEma1List);
        var absDiffEma2List = GetMovingAverageList(stockData, maType, length2, absDiffEma1List);
        var diffEma3List = GetMovingAverageList(stockData, maType, length3, diffEma2List);
        var absDiffEma3List = GetMovingAverageList(stockData, maType, length3, absDiffEma2List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var diffEma3 = diffEma3List[i];
            var absDiffEma3 = absDiffEma3List[i];
            var prevDti1 = i >= 1 ? dtiList[i - 1] : 0;
            var prevDti2 = i >= 2 ? dtiList[i - 2] : 0;

            var dti = absDiffEma3 != 0 ? MinOrMax(100 * diffEma3 / absDiffEma3, 100, -100) : 0;
            dtiList.AddRounded(dti);

            var signal = GetRsiSignal(dti - prevDti1, prevDti1 - prevDti2, dti, prevDti1, 25, -25);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Dti", dtiList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = dtiList;
        stockData.IndicatorName = IndicatorName.DirectionalTrendIndex;

        return stockData;
    }

    /// <summary>
    /// Calculates the Drunkard Walk
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculateDrunkardWalk(this StockData stockData, int length1 = 80, int length2 = 14)
    {
        List<double> tempHighList = new();
        List<double> tempLowList = new();
        List<double> upAtrList = new();
        List<double> dnAtrList = new();
        List<double> upwalkList = new();
        List<double> dnwalkList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var highestHigh = highestList[i];
            var lowestLow = lowestList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var currentHigh = highList[i];
            tempHighList.AddRounded(currentHigh);

            var currentLow = lowList[i];
            tempLowList.AddRounded(currentLow);

            var tr = CalculateTrueRange(currentHigh, currentLow, prevValue);
            var maxIndex = tempHighList.LastIndexOf(highestHigh);
            var minIndex = tempLowList.LastIndexOf(lowestLow);
            var dnRun = i - maxIndex;
            var upRun = i - minIndex;

            var prevAtrUp = upAtrList.LastOrDefault();
            var upK = upRun != 0 ? (double)1 / upRun : 0;
            var atrUp = (tr * upK) + (prevAtrUp * (1 - upK));
            upAtrList.AddRounded(atrUp);

            var prevAtrDn = dnAtrList.LastOrDefault();
            var dnK = dnRun != 0 ? (double)1 / dnRun : 0;
            var atrDn = (tr * dnK) + (prevAtrDn * (1 - dnK));
            dnAtrList.AddRounded(atrDn);

            var upDen = atrUp > 0 ? atrUp : 1;
            var prevUpWalk = upwalkList.LastOrDefault();
            var upWalk = upRun > 0 ? (currentHigh - lowestLow) / (Sqrt(upRun) * upDen) : 0;
            upwalkList.AddRounded(upWalk);

            var dnDen = atrDn > 0 ? atrDn : 1;
            var prevDnWalk = dnwalkList.LastOrDefault();
            var dnWalk = dnRun > 0 ? (highestHigh - currentLow) / (Sqrt(dnRun) * dnDen) : 0;
            dnwalkList.AddRounded(dnWalk);

            var signal = GetCompareSignal(upWalk - dnWalk, prevUpWalk - prevDnWalk);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpWalk", upwalkList },
            { "DnWalk", dnwalkList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.DrunkardWalk;

        return stockData;
    }

    /// <summary>
    /// Calculates the DT Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="length4"></param>
    /// <returns></returns>
    public static StockData CalculateDTOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int length1 = 13, 
        int length2 = 8, int length3 = 5, int length4 = 3)
    {
        List<double> stoRsiList = new();
        List<double> skList = new();
        List<double> sdList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var wilderMovingAvgList = GetMovingAverageList(stockData, maType, length1, inputList);
        var (highestList, lowestList) = GetMaxAndMinValuesList(wilderMovingAvgList, length2);

        for (var i = 0; i < stockData.Count; i++)
        {
            var wima = wilderMovingAvgList[i];
            var highest = highestList[i];
            var lowest = lowestList[i];
            var prevSd1 = i >= 1 ? sdList[i - 1] : 0;
            var prevSd2 = i >= 2 ? sdList[i - 2] : 0;

            var stoRsi = highest - lowest != 0 ? MinOrMax(100 * (wima - lowest) / (highest - lowest), 100, 0) : 0;
            stoRsiList.AddRounded(stoRsi);

            var sk = stoRsiList.TakeLastExt(length3).Average();
            skList.AddRounded(sk);

            var sd = skList.TakeLastExt(length4).Average();
            sdList.AddRounded(sd);

            var signal = GetRsiSignal(sd - prevSd1, prevSd1 - prevSd2, sd, prevSd1, 70, 30);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Dto", skList },
            { "Signal", sdList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = skList;
        stockData.IndicatorName = IndicatorName.DTOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Smoothed Delta Ratio Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateSmoothedDeltaRatioOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 100)
    {
        List<double> bList = new();
        List<double> cList = new();
        List<double> absChgList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= length ? inputList[i - length] : 0;
            var sma = smaList[i];
            var prevSma = i >= length ? smaList[i - length] : 0;

            var absChg = Math.Abs(MinPastValues(i, length, currentValue - prevValue));
            absChgList.AddRounded(absChg);

            var b = MinPastValues(i, length, sma - prevSma);
            bList.AddRounded(b);
        }

        var aList = GetMovingAverageList(stockData, maType, length, absChgList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var a = aList[i];
            var b = bList[i];
            var prevC1 = i >= 1 ? cList[i - 1] : 0;
            var prevC2 = i >= 2 ? cList[i - 2] : 0;

            var c = a != 0 ? MinOrMax(b / a, 1, 0) : 0;
            cList.AddRounded(c);

            var signal = GetRsiSignal(c - prevC1, prevC1 - prevC2, c, prevC1, 0.8, 0.2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Sdro", cList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = cList;
        stockData.IndicatorName = IndicatorName.SmoothedDeltaRatioOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Support and Resistance Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <returns></returns>
    public static StockData CalculateSupportAndResistanceOscillator(this StockData stockData)
    {
        List<double> sroList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, openList, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentOpen = openList[i];
            var currentClose = inputList[i];
            var prevClose = i >= 1 ? inputList[i - 1] : 0;
            var tr = CalculateTrueRange(currentHigh, currentLow, prevClose);
            var prevSro1 = i >= 1 ? sroList[i - 1] : 0;
            var prevSro2 = i >= 2 ? sroList[i - 2] : 0;

            var sro = tr != 0 ? MinOrMax((currentHigh - currentOpen + (currentClose - currentLow)) / (2 * tr), 1, 0) : 0;
            sroList.AddRounded(sro);

            var signal = GetRsiSignal(sro - prevSro1, prevSro1 - prevSro2, sro, prevSro1, 0.7, 0.3);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Sro", sroList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = sroList;
        stockData.IndicatorName = IndicatorName.SupportAndResistanceOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Stationary Extrapolated Levels Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateStationaryExtrapolatedLevelsOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 200)
    {
        List<double> extList = new();
        List<double> yList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var sma = smaList[i];
            var prevY = i >= length ? yList[i - length] : 0;
            var prevY2 = i >= length * 2 ? yList[i - (length * 2)] : 0;

            var y = currentValue - sma;
            yList.AddRounded(y);

            var ext = ((2 * prevY) - prevY2) / 2;
            extList.AddRounded(ext);
        }

        stockData.CustomValuesList = extList;
        var oscList = CalculateStochasticOscillator(stockData, maType, length: length * 2).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var osc = oscList[i];
            var prevOsc1 = i >= 1 ? oscList[i - 1] : 0;
            var prevOsc2 = i >= 2 ? oscList[i - 2] : 0;

            var signal = GetRsiSignal(osc - prevOsc1, prevOsc1 - prevOsc2, osc, prevOsc1, 80, 20);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Selo", oscList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = oscList;
        stockData.IndicatorName = IndicatorName.StationaryExtrapolatedLevelsOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Sell Gravitation Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateSellGravitationIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 20)
    {
        List<double> v3List = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, openList, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentClose = inputList[i];
            var currentOpen = openList[i];
            var v1 = currentClose - currentOpen;
            var v2 = currentHigh - currentLow;

            var v3 = v2 != 0 ? v1 / v2 : 0;
            v3List.AddRounded(v3);
        }

        var sgiList = GetMovingAverageList(stockData, maType, length, v3List);
        var sgiEmaList = GetMovingAverageList(stockData, maType, length, sgiList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var sgi = sgiList[i];
            var sgiEma = sgiEmaList[i];
            var prevSgi = i >= 1 ? sgiList[i - 1] : 0;
            var prevSgiEma = i >= 1 ? sgiEmaList[i - 1] : 0;

            var signal = GetCompareSignal(sgi - sgiEma, prevSgi - prevSgiEma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Sgi", sgiList },
            { "Signal", sgiEmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = sgiList;
        stockData.IndicatorName = IndicatorName.SellGravitationIndex;

        return stockData;
    }

    /// <summary>
    /// Calculates the Strength of Movement
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="smoothingLength"></param>
    /// <returns></returns>
    public static StockData CalculateStrengthOfMovement(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length1 = 10, 
        int length2 = 3, int smoothingLength = 3)
    {
        List<double> aaSeList = new();
        List<double> sSeList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= length2 - 1 ? inputList[i - (length2 - 1)] : 0;
            var moveSe = MinPastValues(i, length2 - 1, currentValue - prevValue);
            var avgMoveSe = moveSe / (length2 - 1);

            var aaSe = prevValue != 0 ? avgMoveSe / prevValue : 0;
            aaSeList.AddRounded(aaSe);
        }

        var bList = GetMovingAverageList(stockData, maType, length1, aaSeList);
        stockData.CustomValuesList = bList;
        var stoList = CalculateStochasticOscillator(stockData, maType, length: length1).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var bSto = stoList[i];

            var sSe = (bSto * 2) - 100;
            sSeList.AddRounded(sSe);
        }

        var ssSeList = GetMovingAverageList(stockData, maType, smoothingLength, sSeList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var ssSe = ssSeList[i];
            var prevSsse = i >= 1 ? ssSeList[i - 1] : 0;

            var signal = GetCompareSignal(ssSe, prevSsse);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Som", ssSeList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = ssSeList;
        stockData.IndicatorName = IndicatorName.StrengthOfMovement;

        return stockData;
    }
  
    /// <summary>
    /// Calculates the Spearman Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculateSpearmanIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 10, 
        int signalLength = 3)
    {
        List<double> coefCorrList = new();
        List<double> tempList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            tempList.AddRounded(currentValue);

            var enumerableList = tempList.TakeLastExt(length).Select(x => (double)x);
            var orderedList = enumerableList.AsQueryExpr().OrderBy(j => j).Run();

            var sc = Correlation.Spearman(enumerableList, orderedList);
            sc = IsValueNullOrInfinity(sc) ? 0 : sc;
            coefCorrList.AddRounded((double)sc * 100);
        }

        var sigList = GetMovingAverageList(stockData, maType, signalLength, coefCorrList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var sc = coefCorrList[i];
            var prevSc = i >= 1 ? coefCorrList[i - 1] : 0;
            var sig = sigList[i];
            var prevSig = i >= 1 ? sigList[i - 1] : 0;

            var signal = GetCompareSignal(sc - sig, prevSc - prevSig);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Si", coefCorrList },
            { "Signal", sigList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = coefCorrList;
        stockData.IndicatorName = IndicatorName.SpearmanIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Smoothed Williams Accumulation Distribution
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateSmoothedWilliamsAccumulationDistribution(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 14)
    {
        List<Signal> signalsList = new();

        var wadList = CalculateWilliamsAccumulationDistribution(stockData).CustomValuesList;
        var wadSignalList = GetMovingAverageList(stockData, maType, length, wadList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var wad = wadList[i];
            var wadSma = wadSignalList[i];
            var prevWad = i >= 1 ? wadList[i - 1] : 0;
            var prevWadSma = i >= 1 ? wadSignalList[i - 1] : 0;

            var signal = GetCompareSignal(wad - wadSma, prevWad - prevWadSma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Swad", wadList },
            { "Signal", wadSignalList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = wadList;
        stockData.IndicatorName = IndicatorName.SmoothedWilliamsAccumulationDistribution;

        return stockData;
    }

    /// <summary>
    /// Calculates the Smoothed Rate of Change
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothingLength"></param>
    /// <returns></returns>
    public static StockData CalculateSmoothedRateOfChange(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 21, 
        int smoothingLength = 13)
    {
        List<double> srocList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var maList = GetMovingAverageList(stockData, maType, smoothingLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentMa = maList[i];
            var prevMa = i >= length ? maList[i - length] : 0;
            var mom = currentMa - prevMa;

            var prevSroc = srocList.LastOrDefault();
            var sroc = prevMa != 0 ? 100 * mom / prevMa : 100;
            srocList.AddRounded(sroc);

            var signal = GetCompareSignal(sroc, prevSroc);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Sroc", srocList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = srocList;
        stockData.IndicatorName = IndicatorName.SmoothedRateOfChange;

        return stockData;
    }

    /// <summary>
    /// Calculates the Sentiment Zone Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="factor"></param>
    /// <returns></returns>
    public static StockData CalculateSentimentZoneOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.TripleExponentialMovingAverage,
        int fastLength = 14, int slowLength = 30, double factor = 0.95)
    {
        List<double> rList = new();
        List<double> szoList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            double r = currentValue > prevValue ? 1 : -1;
            rList.AddRounded(r);
        }

        var spList = GetMovingAverageList(stockData, maType, fastLength, rList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var sp = spList[i];

            var szo = fastLength != 0 ? 100 * sp / fastLength : 0;
            szoList.AddRounded(szo);
        }

        var (highestList, lowestList) = GetMaxAndMinValuesList(szoList, slowLength);
        for (var i = 0; i < stockData.Count; i++)
        {
            var highest = highestList[i];
            var lowest = lowestList[i];
            var range = highest - lowest;
            var ob = lowest + (range * factor);
            var os = highest - (range * factor);
            var szo = szoList[i];
            var prevSzo1 = i >= 1 ? szoList[i - 1] : 0;
            var prevSzo2 = i >= 2 ? szoList[i - 2] : 0;

            var signal = GetRsiSignal(szo - prevSzo1, prevSzo1 - prevSzo2, szo, prevSzo1, ob, os, true);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Szo", szoList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = szoList;
        stockData.IndicatorName = IndicatorName.SentimentZoneOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Simple Cycle
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateSimpleCycle(this StockData stockData, int length = 50)
    {
        List<double> srcList = new();
        List<double> cEmaList = new();
        List<double> cList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var a = (double)1 / length;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevC1 = i >= 1 ? cList[i - 1] : 0;
            var prevC2 = i >= 2 ? cList[i - 2] : 0;
            var prevSrc = i >= length ? srcList[i - length] : 0;

            var src = currentValue + prevC1;
            srcList.AddRounded(src);

            var cEma = CalculateEMA(prevC1, cEmaList.LastOrDefault(), length);
            cEmaList.AddRounded(cEma);

            var b = prevC1 - cEma;
            var c = (a * (src - prevSrc)) + ((1 - a) * b);
            cList.AddRounded(c);

            var signal = GetCompareSignal(c - prevC1, prevC1 - prevC2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Sc", cList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = cList;
        stockData.IndicatorName = IndicatorName.SimpleCycle;

        return stockData;
    }

    /// <summary>
    /// Calculates the Stiffness Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="smoothingLength"></param>
    /// <param name="threshold"></param>
    /// <returns></returns>
    public static StockData CalculateStiffnessIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length1 = 100, int length2 = 60, int smoothingLength = 3, double threshold = 90)
    {
        List<double> aboveList = new();
        List<double> stiffValueList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length1, inputList);
        var stdDevList = CalculateStandardDeviationVolatility(stockData, maType, length1).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var sma = smaList[i];
            var stdDev = stdDevList[i];
            var bound = sma - (0.2 * stdDev);

            double above = currentValue > bound ? 1 : 0;
            aboveList.AddRounded(above);

            var aboveSum = aboveList.TakeLastExt(length2).Sum();
            var stiffValue = length2 != 0 ? aboveSum * 100 / length2 : 0;
            stiffValueList.AddRounded(stiffValue);
        }

        var stiffnessList = GetMovingAverageList(stockData, MovingAvgType.ExponentialMovingAverage, smoothingLength, stiffValueList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var stiffness = stiffnessList[i];
            var prevStiffness = i >= 1 ? stiffnessList[i - 1] : 0;

            var signal = GetCompareSignal(stiffness - threshold, prevStiffness - threshold);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Si", stiffnessList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = stiffnessList;
        stockData.IndicatorName = IndicatorName.StiffnessIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Super Trend Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="factor"></param>
    /// <returns></returns>
    public static StockData CalculateSuperTrendFilter(this StockData stockData, int length = 200, double factor = 0.9)
    {
        List<double> tList = new();
        List<double> srcList = new();
        List<double> trendUpList = new();
        List<double> trendDnList = new();
        List<double> trendList = new();
        List<double> tslList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        double p = Pow(length, 2), a = 2 / (p + 1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevTsl1 = i >= 1 ? tslList[i - 1] : currentValue;
            var prevTsl2 = i >= 2 ? tslList[i - 2] : 0;
            var d = Math.Abs(currentValue - prevTsl1);

            var prevT = i >= 1 ? tList[i - 1] : d;
            var t = (a * d) + ((1 - a) * prevT);
            tList.AddRounded(t);

            var prevSrc = srcList.LastOrDefault();
            var src = (factor * prevTsl1) + ((1 - factor) * currentValue);
            srcList.AddRounded(src);

            var up = prevTsl1 - t;
            var dn = prevTsl1 + t;

            var prevTrendUp = trendUpList.LastOrDefault();
            var trendUp = prevSrc > prevTrendUp ? Math.Max(up, prevTrendUp) : up;
            trendUpList.AddRounded(trendUp);

            var prevTrendDn = trendDnList.LastOrDefault();
            var trendDn = prevSrc < prevTrendDn ? Math.Min(dn, prevTrendDn) : dn;
            trendDnList.AddRounded(trendDn);

            var prevTrend = i >= 1 ? trendList[i - 1] : 1;
            var trend = src > prevTrendDn ? 1 : src < prevTrendUp ? -1 : prevTrend;
            trendList.AddRounded(trend);

            var tsl = trend == 1 ? trendDn : trendUp;
            tslList.AddRounded(tsl);

            var signal = GetCompareSignal(tsl - prevTsl1, prevTsl1 - prevTsl2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Stf", tslList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = tslList;
        stockData.IndicatorName = IndicatorName.SuperTrendFilter;

        return stockData;
    }

    /// <summary>
    /// Calculates the SMI Ergodic Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculateSMIErgodicIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int fastLength = 5,
        int slowLength = 20, int signalLength = 5)
    {
        List<double> pcList = new();
        List<double> absPCList = new();
        List<double> smiList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var pc = MinPastValues(i, 1, currentValue - prevValue);
            pcList.AddRounded(pc);

            var absPC = Math.Abs(pc);
            absPCList.AddRounded(absPC);
        }

        var pcSmooth1List = GetMovingAverageList(stockData, maType, fastLength, pcList); 
        var pcSmooth2List = GetMovingAverageList(stockData, maType, slowLength, pcSmooth1List);
        var absPCSmooth1List = GetMovingAverageList(stockData, maType, fastLength, absPCList);
        var absPCSmooth2List = GetMovingAverageList(stockData, maType, slowLength, absPCSmooth1List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var absSmooth2PC = absPCSmooth2List[i];
            var smooth2PC = pcSmooth2List[i];

            var smi = absSmooth2PC != 0 ? MinOrMax(100 * smooth2PC / absSmooth2PC, 100, -100) : 0;
            smiList.AddRounded(smi);
        }

        var smiSignalList = GetMovingAverageList(stockData, maType, signalLength, smiList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var smi = smiList[i];
            var smiSignal = smiSignalList[i];
            var prevSmi = i >= 1 ? smiList[i - 1] : 0;
            var prevSmiSignal = i >= 1 ? smiSignalList[i - 1] : 0;

            var signal = GetRsiSignal(smi - smiSignal, prevSmi - prevSmiSignal, smi, prevSmi, 10, -10);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Smi", smiList },
            { "Signal", smiSignalList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = smiList;
        stockData.IndicatorName = IndicatorName.SMIErgodicIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Simple Lines
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="mult"></param>
    /// <returns></returns>
    public static StockData CalculateSimpleLines(this StockData stockData, int length = 10, double mult = 10)
    {
        List<double> aList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var s = 0.01 * 100 * ((double)1 / length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevA = i >= 1 ? aList[i - 1] : currentValue;
            var prevA2 = i >= 2 ? aList[i - 2] : currentValue;
            var x = currentValue + ((prevA - prevA2) * mult);

            prevA = i >= 1 ? aList[i - 1] : x;
            var a = x > prevA + s ? prevA + s : x < prevA - s ? prevA - s : prevA;
            aList.AddRounded(a);

            var signal = GetCompareSignal(a - prevA, prevA - prevA2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Sl", aList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = aList;
        stockData.IndicatorName = IndicatorName.SimpleLines;

        return stockData;
    }

    /// <summary>
    /// Calculates the Sector Rotation Model
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="marketData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculateSectorRotationModel(this StockData stockData, StockData marketData, 
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 25, int length2 = 75)
    {
        List<double> oscList = new();
        List<Signal> signalsList = new();

        if (stockData.Count == marketData.Count)
        {
            var bull1List = CalculateRateOfChange(stockData, length1).CustomValuesList;
            var bull2List = CalculateRateOfChange(stockData, length2).CustomValuesList;
            var bear1List = CalculateRateOfChange(marketData, length1).CustomValuesList;
            var bear2List = CalculateRateOfChange(marketData, length2).CustomValuesList;

            for (var i = 0; i < stockData.Count; i++)
            {
                var bull1 = bull1List[i];
                var bull2 = bull2List[i];
                var bear1 = bear1List[i];
                var bear2 = bear2List[i];
                var bull = (bull1 + bull2) / 2;
                var bear = (bear1 + bear2) / 2;

                var osc = 100 * (bull - bear);
                oscList.AddRounded(osc);
            }

            var oscEmaList = GetMovingAverageList(stockData, maType, length1, oscList);
            for (var i = 0; i < stockData.Count; i++)
            {
                var oscEma = oscEmaList[i];
                var prevOscEma1 = i >= 1 ? oscEmaList[i - 1] : 0;
                var prevOscEma2 = i >= 2 ? oscEmaList[i - 2] : 0;

                var signal = GetCompareSignal(oscEma - prevOscEma1, prevOscEma1 - prevOscEma2);
                signalsList.Add(signal);
            }

            stockData.OutputValues = new Dictionary<string, List<double>>
            {
                { "Srm", oscList },
                { "Signal", oscEmaList }
            };
            stockData.SignalsList = signalsList;
            stockData.CustomValuesList = oscList;
            stockData.IndicatorName = IndicatorName.SectorRotationModel;
        }

        return stockData;
    }

    /// <summary>
    /// Calculates the Enhanced Williams R
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculateEnhancedWilliamsR(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14, 
        int signalLength = 5)
    {
        List<double> ewrList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);
        var (highestList1, lowestList1) = GetMaxAndMinValuesList(inputList, length);
        var (highestList2, lowestList2) = GetMaxAndMinValuesList(volumeList, length);

        var af = length < 10 ? 0.25 : ((double)length / 32) - 0.0625;
        var smaLength = MinOrMax((int)Math.Ceiling((double)length / 2));

        var srcSmaList = GetMovingAverageList(stockData, maType, smaLength, inputList);
        var volSmaList = GetMovingAverageList(stockData, maType, smaLength, volumeList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var maxVol = highestList2[i];
            var minVol = lowestList2[i];
            var maxSrc = highestList1[i];
            var minSrc = lowestList1[i];
            var srcSma = srcSmaList[i];
            var volSma = volSmaList[i];
            var volume = volumeList[i];
            var volWr = maxVol - minVol != 0 ? 2 * ((volume - volSma) / (maxVol - minVol)) : 0;
            var srcWr = maxSrc - minSrc != 0 ? 2 * ((currentValue - srcSma) / (maxSrc - minSrc)) : 0;
            var srcSwr = maxSrc - minSrc != 0 ? 2 * (MinPastValues(i, 1, currentValue - prevValue) / (maxSrc - minSrc)) : 0;

            var ewr = ((volWr > 0 && srcWr > 0 && currentValue > prevValue) || (volWr > 0 && srcWr < 0 && currentValue < prevValue)) && srcSwr + af != 0 ?
                ((50 * (srcWr * (srcSwr + af) * volWr)) + srcSwr + af) / (srcSwr + af) : 25 * ((srcWr * (volWr + 1)) + 2);
            ewrList.AddRounded(ewr);
        }

        var ewrSignalList = GetMovingAverageList(stockData, maType, signalLength, ewrList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var ewr = ewrList[i];
            var ewrSignal = ewrSignalList[i];
            var prevEwr = i >= 1 ? ewrList[i - 1] : 0;
            var prevEwrSignal = i >= 1 ? ewrSignalList[i - 1] : 0;

            var signal = GetRsiSignal(ewr - ewrSignal, prevEwr - prevEwrSignal, ewr, prevEwr, 100, -100);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ewr", ewrList },
            { "Signal", ewrSignalList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = ewrList;
        stockData.IndicatorName = IndicatorName.EnhancedWilliamsR;

        return stockData;
    }

    /// <summary>
    /// Calculates the Earning Support Resistance Levels
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="inputName"></param>
    /// <returns></returns>
    public static StockData CalculateEarningSupportResistanceLevels(this StockData stockData, InputName inputName = InputName.MedianPrice)
    {
        List<double> mode1List = new();
        List<double> mode2List = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, closeList, _) = GetInputValuesList(inputName, stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentHigh = highList[i];
            var prevClose = i >= 1 ? closeList[i - 1] : 0;
            var prevLow = i >= 2 ? lowList[i - 2] : 0;
            var prevValue2 = i >= 2 ? inputList[i - 2] : 0;
            var prevValue1 = i >= 1 ? inputList[i - 1] : 0;

            var prevMode1 = mode1List.LastOrDefault();
            var mode1 = (prevLow + currentHigh) / 2;
            mode1List.AddRounded(mode1);

            var prevMode2 = mode2List.LastOrDefault();
            var mode2 = (prevValue2 + currentValue + prevClose) / 3;
            mode2List.AddRounded(mode2);

            var signal = GetBullishBearishSignal(currentValue - Math.Max(mode1, mode2), prevValue1 - Math.Max(prevMode1, prevMode2),
                currentValue - Math.Min(mode1, mode2), prevValue1 - Math.Min(prevMode1, prevMode2));
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Esr", mode1List }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = mode1List;
        stockData.IndicatorName = IndicatorName.EarningSupportResistanceLevels;

        return stockData;
    }

    /// <summary>
    /// Calculates the Elder Market Thermometer
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateElderMarketThermometer(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 22)
    {
        List<double> emtList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        var emaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var prevHigh = i >= 1 ? highList[i - 1] : 0;
            var prevLow = i >= 1 ? lowList[i - 1] : 0;

            var emt = currentHigh < prevHigh && currentLow > prevLow ? 0 : currentHigh - prevHigh > prevLow - currentLow ? Math.Abs(currentHigh - prevHigh) :
                Math.Abs(prevLow - currentLow);
            emtList.AddRounded(emt);
        }

        var aemtList = GetMovingAverageList(stockData, maType, length, emtList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentEma = emaList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevEma = i >= 1 ? emaList[i - 1] : 0;
            var emt = emtList[i];
            var emtEma = aemtList[i];

            var signal = GetVolatilitySignal(currentValue - currentEma, prevValue - prevEma, emt, emtEma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Emt", emtList },
            { "Signal", aemtList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = emtList;
        stockData.IndicatorName = IndicatorName.ElderMarketThermometer;

        return stockData;
    }

    /// <summary>
    /// Calculates the Elliott Wave Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <returns></returns>
    public static StockData CalculateElliottWaveOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int fastLength = 5,
        int slowLength = 34)
    {
        List<double> ewoList = new();
        List<double> ewoHistogramList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, fastLength, inputList);
        var sma34List = GetMovingAverageList(stockData, maType, slowLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentSma5 = smaList[i];
            var currentSma34 = sma34List[i];

            var ewo = currentSma5 - currentSma34;
            ewoList.AddRounded(ewo);
        }

        var ewoSignalLineList = GetMovingAverageList(stockData, maType, fastLength, ewoList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var ewo = ewoList[i];
            var ewoSignalLine = ewoSignalLineList[i];

            var prevEwoHistogram = ewoHistogramList.LastOrDefault();
            var ewoHistogram = ewo - ewoSignalLine;
            ewoHistogramList.AddRounded(ewoHistogram);

            var signal = GetCompareSignal(ewoHistogram, prevEwoHistogram);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ewo", ewoList },
            { "Signal", ewoSignalLineList },
            { "Histogram", ewoHistogramList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = ewoList;
        stockData.IndicatorName = IndicatorName.ElliottWaveOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ergodic Candlestick Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculateErgodicCandlestickOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length1 = 32, int length2 = 12)
    {
        List<double> xcoList = new();
        List<double> xhlList = new();
        List<double> ecoList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, openList, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentOpen = openList[i];
            var currentClose = inputList[i];

            var xco = currentClose - currentOpen;
            xcoList.AddRounded(xco);

            var xhl = currentHigh - currentLow;
            xhlList.AddRounded(xhl);
        }

        var xcoEma1List = GetMovingAverageList(stockData, maType, length1, xcoList);
        var xcoEma2List = GetMovingAverageList(stockData, maType, length2, xcoEma1List);
        var xhlEma1List = GetMovingAverageList(stockData, maType, length1, xhlList);
        var xhlEma2List = GetMovingAverageList(stockData, maType, length2, xhlEma1List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var xhlEma2 = xhlEma2List[i];
            var xcoEma2 = xcoEma2List[i];

            var eco = xhlEma2 != 0 ? 100 * xcoEma2 / xhlEma2 : 0;
            ecoList.AddRounded(eco);
        }

        var ecoSignalList = GetMovingAverageList(stockData, maType, length2, ecoList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var eco = ecoList[i];
            var ecoEma = ecoSignalList[i];
            var prevEco = i >= 1 ? ecoList[i - 1] : 0;
            var prevEcoEma = i >= 1 ? ecoSignalList[i - 1] : 0;

            var signal = GetCompareSignal(eco - ecoEma, prevEco - prevEcoEma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Eco", ecoList },
            { "Signal", ecoSignalList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = ecoList;
        stockData.IndicatorName = IndicatorName.ErgodicCandlestickOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ergodic True Strength Index V1
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculateErgodicTrueStrengthIndexV1(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length1 = 4, int length2 = 8, int length3 = 6, int signalLength = 3)
    {
        List<double> etsiList = new();
        List<double> priceDiffList = new();
        List<double> absPriceDiffList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var priceDiff = MinPastValues(i, 1, currentValue - prevValue);
            priceDiffList.AddRounded(priceDiff);

            var absPriceDiff = Math.Abs(priceDiff);
            absPriceDiffList.AddRounded(absPriceDiff);
        }

        var diffEma1List = GetMovingAverageList(stockData, maType, length1, priceDiffList);
        var absDiffEma1List = GetMovingAverageList(stockData, maType, length1, absPriceDiffList);
        var diffEma2List = GetMovingAverageList(stockData, maType, length2, diffEma1List);
        var absDiffEma2List = GetMovingAverageList(stockData, maType, length2, absDiffEma1List);
        var diffEma3List = GetMovingAverageList(stockData, maType, length3, diffEma2List);
        var absDiffEma3List = GetMovingAverageList(stockData, maType, length3, absDiffEma2List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var diffEma3 = diffEma3List[i];
            var absDiffEma3 = absDiffEma3List[i];

            var etsi = absDiffEma3 != 0 ? MinOrMax(100 * diffEma3 / absDiffEma3, 100, -100) : 0;
            etsiList.AddRounded(etsi);
        }

        var etsiSignalList = GetMovingAverageList(stockData, maType, signalLength, etsiList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var etsi = etsiList[i];
            var etsiSignal = etsiSignalList[i];
            var prevEtsi = i >= 1 ? etsiList[i - 1] : 0;
            var prevEtsiSignal = i >= 1 ? etsiSignalList[i - 1] : 0;

            var signal = GetCompareSignal(etsi - etsiSignal, prevEtsi - prevEtsiSignal);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Etsi", etsiList },
            { "Signal", etsiSignalList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = etsiList;
        stockData.IndicatorName = IndicatorName.ErgodicTrueStrengthIndexV1;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ergodic True Strength Index V2
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="length4"></param>
    /// <param name="length5"></param>
    /// <param name="length6"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculateErgodicTrueStrengthIndexV2(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length1 = 21, int length2 = 9, int length3 = 9, int length4 = 17, int length5 = 6, int length6 = 2, int signalLength = 2)
    {
        List<double> etsi2List = new();
        List<double> etsi1List = new();
        List<double> priceDiffList = new();
        List<double> absPriceDiffList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var priceDiff = MinPastValues(i, 1, currentValue - prevValue);
            priceDiffList.AddRounded(priceDiff);

            var absPriceDiff = Math.Abs(priceDiff);
            absPriceDiffList.AddRounded(absPriceDiff);
        }

        var diffEma1List = GetMovingAverageList(stockData, maType, length1, priceDiffList);
        var absDiffEma1List = GetMovingAverageList(stockData, maType, length1, absPriceDiffList);
        var diffEma4List = GetMovingAverageList(stockData, maType, length4, priceDiffList);
        var absDiffEma4List = GetMovingAverageList(stockData, maType, length4, absPriceDiffList);
        var diffEma2List = GetMovingAverageList(stockData, maType, length2, diffEma1List);
        var absDiffEma2List = GetMovingAverageList(stockData, maType, length2, absDiffEma1List);
        var diffEma5List = GetMovingAverageList(stockData, maType, length5, diffEma4List);
        var absDiffEma5List = GetMovingAverageList(stockData, maType, length5, absDiffEma4List);
        var diffEma3List = GetMovingAverageList(stockData, maType, length3, diffEma2List);
        var absDiffEma3List = GetMovingAverageList(stockData, maType, length3, absDiffEma2List);
        var diffEma6List = GetMovingAverageList(stockData, maType, length6, diffEma5List);
        var absDiffEma6List = GetMovingAverageList(stockData, maType, length6, absDiffEma5List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var diffEma6 = diffEma6List[i];
            var absDiffEma6 = absDiffEma6List[i];
            var diffEma3 = diffEma3List[i];
            var absDiffEma3 = absDiffEma3List[i];

            var etsi1 = absDiffEma3 != 0 ? MinOrMax(diffEma3 / absDiffEma3 * 100, 100, -100) : 0;
            etsi1List.AddRounded(etsi1);

            var etsi2 = absDiffEma6 != 0 ? MinOrMax(diffEma6 / absDiffEma6 * 100, 100, -100) : 0;
            etsi2List.AddRounded(etsi2);
        }

        var etsi2SignalList = GetMovingAverageList(stockData, maType, signalLength, etsi2List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var etsi2 = etsi2List[i];
            var etsi2Signal = etsi2SignalList[i];
            var prevEtsi2 = i >= 1 ? etsi2List[i - 1] : 0;
            var prevEtsi2Signal = i >= 1 ? etsi2SignalList[i - 1] : 0;

            var signal = GetCompareSignal(etsi2 - etsi2Signal, prevEtsi2 - prevEtsi2Signal);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Etsi1", etsi1List },
            { "Etsi2", etsi2List },
            { "Signal", etsi2SignalList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = etsi2List;
        stockData.IndicatorName = IndicatorName.ErgodicTrueStrengthIndexV2;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ergodic Commodity Selection Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <param name="pointValue"></param>
    /// <returns></returns>
    public static StockData CalculateErgodicCommoditySelectionIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, 
        int length = 32, int smoothLength = 5, double pointValue = 1)
    {
        List<double> ergodicCsiList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        var k = 100 * (pointValue / Sqrt(length) / (150 + smoothLength));

        var adxList = CalculateAverageDirectionalIndex(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentValue = inputList[i];
            var adx = adxList[i];
            var prevAdx = i >= 1 ? adxList[i - 1] : 0;
            var adxR = (adx + prevAdx) * 0.5;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var tr = CalculateTrueRange(currentHigh, currentLow, prevValue);
            var csi = length + tr > 0 ? k * adxR * tr / length : 0;

            var ergodicCsi = currentValue > 0 ? csi / currentValue : 0;
            ergodicCsiList.AddRounded(ergodicCsi);
        }

        var ergodicCsiSmaList = GetMovingAverageList(stockData, maType, smoothLength, ergodicCsiList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var ergodicCsiSma = ergodicCsiSmaList[i];
            var prevErgodicCsiSma1 = i >= 1 ? ergodicCsiSmaList[i - 1] : 0;
            var prevErgodicCsiSma2 = i >= 2 ? ergodicCsiSmaList[i - 2] : 0;

            var signal = GetCompareSignal(ergodicCsiSma - prevErgodicCsiSma1, prevErgodicCsiSma1 - prevErgodicCsiSma2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ecsi", ergodicCsiList },
            { "Signal", ergodicCsiSmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = ergodicCsiList;
        stockData.IndicatorName = IndicatorName.ErgodicCommoditySelectionIndex;

        return stockData;
    }

    /// <summary>
    /// Calculates the Enhanced Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculateEnhancedIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14, 
        int signalLength = 8)
    {
        List<double> closewrList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        var smaLength = MinOrMax((int)Math.Ceiling((double)length / 2));

        var smaList = GetMovingAverageList(stockData, maType, smaLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var highest = highestList[i];
            var lowest = lowestList[i];
            var dnm = highest - lowest;
            var sma = smaList[i];

            var closewr = dnm != 0 ? 2 * (currentValue - sma) / dnm : 0;
            closewrList.AddRounded(closewr);
        }

        var closewrSmaList = GetMovingAverageList(stockData, maType, signalLength, closewrList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var closewr = closewrList[i];
            var closewrSma = closewrSmaList[i];
            var prevCloseWr = i >= 1 ? closewrList[i - 1] : 0;
            var prevCloseWrSma = i >= 1 ? closewrSmaList[i - 1] : 0;

            var signal = GetCompareSignal(closewr - closewrSma, prevCloseWr - prevCloseWrSma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ei", closewrList },
            { "Signal", closewrSmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = closewrList;
        stockData.IndicatorName = IndicatorName.EnhancedIndex;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ema Wave Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateEmaWaveIndicator(this StockData stockData, int length1 = 5, int length2 = 25, int length3 = 50, int smoothLength = 4)
    {
        List<double> emaADiffList = new();
        List<double> emaBDiffList = new();
        List<double> emaCDiffList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var emaAList = GetMovingAverageList(stockData, MovingAvgType.ExponentialMovingAverage, length1, inputList);
        var emaBList = GetMovingAverageList(stockData, MovingAvgType.ExponentialMovingAverage, length2, inputList);
        var emaCList = GetMovingAverageList(stockData, MovingAvgType.ExponentialMovingAverage, length3, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var emaA = emaAList[i];
            var emaB = emaBList[i];
            var emaC = emaCList[i];

            var emaADiff = currentValue - emaA;
            emaADiffList.AddRounded(emaADiff);

            var emaBDiff = currentValue - emaB;
            emaBDiffList.AddRounded(emaBDiff);

            var emaCDiff = currentValue - emaC;
            emaCDiffList.AddRounded(emaCDiff);
        }

        var waList = GetMovingAverageList(stockData, MovingAvgType.SimpleMovingAverage, smoothLength, emaADiffList);
        var wbList = GetMovingAverageList(stockData, MovingAvgType.SimpleMovingAverage, smoothLength, emaBDiffList);
        var wcList = GetMovingAverageList(stockData, MovingAvgType.SimpleMovingAverage, smoothLength, emaCDiffList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var wa = waList[i];
            var wb = wbList[i];
            var wc = wcList[i];

            var signal = GetConditionSignal(wa > 0 && wb > 0 && wc > 0, wa < 0 && wb < 0 && wc < 0);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Wa", waList },
            { "Wb", wbList },
            { "Wc", wcList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.EmaWaveIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ergodic Mean Deviation Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculateErgodicMeanDeviationIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length1 = 32, int length2 = 5, int length3 = 5, int signalLength = 5)
    {
        List<double> ma1List = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var emaList = GetMovingAverageList(stockData, maType, length1, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentEma = emaList[i];

            var ma1 = currentValue - currentEma;
            ma1List.AddRounded(ma1);
        }

        var ma1EmaList = GetMovingAverageList(stockData, maType, length2, ma1List);
        var emdiList = GetMovingAverageList(stockData, maType, length3, ma1EmaList);
        var emdiSignalList = GetMovingAverageList(stockData, maType, signalLength, emdiList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var emdi = emdiList[i];
            var emdiSignal = emdiSignalList[i];
            var prevEmdi = i >= 1 ? emdiList[i - 1] : 0;
            var prevEmdiSignal = i >= 1 ? emdiSignalList[i - 1] : 0;

            var signal = GetCompareSignal(emdi - emdiSignal, prevEmdi - prevEmdiSignal);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Emdi", emdiList },
            { "Signal", emdiSignalList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = emdiList;
        stockData.IndicatorName = IndicatorName.ErgodicMeanDeviationIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Efficient Price
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateEfficientPrice(this StockData stockData, int length = 50)
    {
        List<double> epList = new();
        List<double> chgErList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var erList = CalculateKaufmanAdaptiveMovingAverage(stockData, length: length).OutputValues["Er"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var er = erList[i];
            var prevValue = i >= length ? inputList[i - length] : 0;
            var prevEp1 = i >= 1 ? epList[i - 1] : 0;
            var prevEp2 = i >= 2 ? epList[i - 2] : 0;

            var chgEr = MinPastValues(i, length, currentValue - prevValue) * er;
            chgErList.AddRounded(chgEr);

            var ep = chgErList.Sum();
            epList.AddRounded(ep);

            var signal = GetCompareSignal(ep - prevEp1, prevEp1 - prevEp2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ep", epList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = epList;
        stockData.IndicatorName = IndicatorName.EfficientPrice;

        return stockData;
    }

    /// <summary>
    /// Calculates the Efficient Auto Line
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="fastAlpha"></param>
    /// <param name="slowAlpha"></param>
    /// <returns></returns>
    public static StockData CalculateEfficientAutoLine(this StockData stockData, int length = 19, double fastAlpha = 0.0001, double slowAlpha = 0.005)
    {
        List<double> aList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var erList = CalculateKaufmanAdaptiveMovingAverage(stockData, length: length).OutputValues["Er"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var er = erList[i];
            var dev = (er * fastAlpha) + ((1 - er) * slowAlpha);

            var prevA = aList.LastOrDefault();
            var a = i < 9 ? currentValue : currentValue > prevA + dev ? currentValue : currentValue < prevA - dev ? currentValue : prevA;
            aList.AddRounded(a);

            var signal = GetCompareSignal(currentValue - a, prevValue - prevA);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Eal", aList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = aList;
        stockData.IndicatorName = IndicatorName.EfficientAutoLine;

        return stockData;
    }
}