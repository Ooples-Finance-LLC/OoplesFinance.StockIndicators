using OoplesFinance.StockIndicators.Compatibility;
using OoplesFinance.StockIndicators.Core;

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
        List<double> cciList = new(stockData.Count);
        List<double> tpDevDiffList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var (inputList, _, _, _, _, _) = GetInputValuesList(inputName, stockData);
        var tpSmaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var tpSma = tpSmaList[i];

            var tpDevDiff = Math.Abs(currentValue - tpSma);
            tpDevDiffList.Add(tpDevDiff);
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
            cciList.Add(cci);

            var signal = GetRsiSignal(cci - prevCci1, prevCci1 - prevCci2, cci, prevCci1, 100, -100);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Cci", cciList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(cciList);
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
        var (inputList, _, _, _, _, _) = GetInputValuesList(inputName, stockData);
        var count = inputList.Count;
        var aoList = new List<double>(count);
        List<Signal>? signalsList = CreateSignalsList(stockData, count);
        var fastSmaList = GetMovingAverageList(stockData, maType, fastLength, inputList);
        var slowSmaList = GetMovingAverageList(stockData, maType, slowLength, inputList);

        double prevAo = 0;
        for (var i = 0; i < count; i++)
        {
            var fastSma = fastSmaList[i];
            var slowSma = slowSmaList[i];

            var ao = fastSma - slowSma;
            aoList.Add(ao);

            var signal = GetCompareSignal(ao, prevAo);
            signalsList?.Add(signal);
            prevAo = ao;
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ao", aoList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(aoList);
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
        var awesomeOscList = CalculateAwesomeOscillator(stockData, maType, inputName, fastLength, slowLength).CustomValuesList;
        var count = awesomeOscList.Count;
        var acList = new List<double>(count);
        List<Signal>? signalsList = CreateSignalsList(stockData, count);
        var awesomeOscMaList = GetMovingAverageList(stockData, maType, smoothLength, awesomeOscList);

        double prevAc = 0;
        for (var i = 0; i < count; i++)
        {
            var ao = awesomeOscList[i];
            var aoSma = awesomeOscMaList[i];

            var ac = ao - aoSma;
            acList.Add(ac);

            var signal = GetCompareSignal(ac, prevAc);
            signalsList?.Add(signal);
            prevAc = ac;
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ac", acList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(acList);
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
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var count = inputList.Count;
        var ulcerIndexList = new List<double>(count);
        List<Signal>? signalsList = CreateSignalsList(stockData, count);
        var (highestList, _) = GetMaxAndMinValuesList(inputList, length);
        var pctDrawdownSquaredSum = new RollingSum();

        double prevUlcerIndex1 = 0;
        double prevUlcerIndex2 = 0;
        for (var i = 0; i < count; i++)
        {
            var currentValue = inputList[i];
            var maxValue = highestList[i];

            var pctDrawdownSquared = maxValue != 0 ? Pow((currentValue - maxValue) / maxValue * 100, 2) : 0;
            pctDrawdownSquaredSum.Add(pctDrawdownSquared);

            var squaredAvg = pctDrawdownSquaredSum.Average(length);

            var ulcerIndex = squaredAvg >= 0 ? Sqrt(squaredAvg) : 0;
            ulcerIndexList.Add(ulcerIndex);

            var signal = GetCompareSignal(ulcerIndex - prevUlcerIndex1, prevUlcerIndex1 - prevUlcerIndex2, true);
            signalsList?.Add(signal);

            prevUlcerIndex2 = prevUlcerIndex1;
            prevUlcerIndex1 = ulcerIndex;
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ui", ulcerIndexList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(ulcerIndexList);
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
        List<double> balanceOfPowerList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, openList, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentClose = inputList[i];
            var currentOpen = openList[i];
            var currentHigh = highList[i];
            var currentLow = lowList[i];

            var balanceOfPower = currentHigh - currentLow != 0 ? (currentClose - currentOpen) / (currentHigh - currentLow) : 0;
            balanceOfPowerList.Add(balanceOfPower);
        }

        var bopSignalList = GetMovingAverageList(stockData, maType, length, balanceOfPowerList);
        for (var i = 0; i < stockData.ClosePrices.Count; i++)
        {
            var bop = balanceOfPowerList[i];
            var bopMa = bopSignalList[i];
            var prevBop = i >= 1 ? balanceOfPowerList[i - 1] : 0;
            var prevBopMa = i >= 1 ? bopSignalList[i - 1] : 0;

            var signal = GetCompareSignal(bop - bopMa, prevBop - prevBopMa);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Bop", balanceOfPowerList },
            { "BopSignal", bopSignalList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(balanceOfPowerList);
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
        List<double> rocList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= length ? inputList[i - length] : 0;
            var prevRoc1 = i >= 1 ? rocList[i - 1] : 0;
            var prevRoc2 = i >= 2 ? rocList[i - 2] : 0;

            var roc = prevValue != 0 ? MinPastValues(i, length, currentValue - prevValue) / prevValue * 100 : 0;
            rocList.Add(roc);

            var signal = GetCompareSignal(roc - prevRoc1, prevRoc1 - prevRoc2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Roc", rocList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(rocList);
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
        List<double> chaikinOscillatorList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var adlList = CalculateAccumulationDistributionLine(stockData, maType, fastLength).CustomValuesList;
        var adl3EmaList = GetMovingAverageList(stockData, maType, fastLength, adlList);
        var adl10EmaList = GetMovingAverageList(stockData, maType, slowLength, adlList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var adl3Ema = adl3EmaList[i];
            var adl10Ema = adl10EmaList[i];

            var prevChaikinOscillator = GetLastOrDefault(chaikinOscillatorList);
            var chaikinOscillator = adl3Ema - adl10Ema;
            chaikinOscillatorList.Add(chaikinOscillator);

            var signal = GetCompareSignal(chaikinOscillator, prevChaikinOscillator);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "ChaikinOsc", chaikinOscillatorList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(chaikinOscillatorList);
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
        List<double> tenkanSenList = new(stockData.Count);
        List<double> kijunSenList = new(stockData.Count);
        List<double> senkouSpanAList = new(stockData.Count);
        List<double> senkouSpanBList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
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

            var prevTenkanSen = GetLastOrDefault(tenkanSenList);
            var tenkanSen = (highest1 + lowest1) / 2;
            tenkanSenList.Add(tenkanSen);

            var prevKijunSen = GetLastOrDefault(kijunSenList);
            var kijunSen = (highest2 + lowest2) / 2;
            kijunSenList.Add(kijunSen);

            var senkouSpanA = (tenkanSen + kijunSen) / 2;
            senkouSpanAList.Add(senkouSpanA);

            var senkouSpanB = (highest3 + lowest3) / 2;
            senkouSpanBList.Add(senkouSpanB);

            var signal = GetCompareSignal(tenkanSen - kijunSen, prevTenkanSen - prevKijunSen);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "TenkanSen", tenkanSenList },
            { "KijunSen", kijunSenList },
            { "SenkouSpanA", senkouSpanAList },
            { "SenkouSpanB", senkouSpanBList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
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
        List<double> displacedJawList = new(stockData.Count);
        List<double> displacedTeethList = new(stockData.Count);
        List<double> displacedLipsList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var jawList = GetMovingAverageList(stockData, maType, jawLength, inputList);
        var teethList = GetMovingAverageList(stockData, maType, teethLength, inputList);
        var lipsList = GetMovingAverageList(stockData, maType, lipsLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevJaw = GetLastOrDefault(displacedJawList);
            var displacedJaw = i >= jawOffset ? jawList[i - jawOffset] : 0;
            displacedJawList.Add(displacedJaw);

            var prevTeeth = GetLastOrDefault(displacedTeethList);
            var displacedTeeth = i >= teethOffset ? teethList[i - teethOffset] : 0;
            displacedTeethList.Add(displacedTeeth);

            var prevLips = GetLastOrDefault(displacedLipsList);
            var displacedLips = i >= lipsOffset ? lipsList[i - lipsOffset] : 0;
            displacedLipsList.Add(displacedLips);

            var signal = GetBullishBearishSignal(displacedLips - Math.Max(displacedJaw, displacedTeeth), prevLips - Math.Max(prevJaw, prevTeeth),
                displacedLips - Math.Min(displacedJaw, displacedTeeth), prevLips - Math.Min(prevJaw, prevTeeth));
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Lips", displacedLipsList },
            { "Teeth", displacedTeethList },
            { "Jaws", displacedJawList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
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
        List<double> topList = new(stockData.Count);
        List<double> bottomList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var alligatorList = CalculateAlligatorIndex(stockData, inputName, maType, jawLength, jawOffset, teethLength, teethOffset, lipsLength, lipsOffset).OutputValues;
        var jawList = alligatorList["Jaws"];
        var teethList = alligatorList["Teeth"];
        var lipsList = alligatorList["Lips"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var jaw = jawList[i];
            var teeth = teethList[i];
            var lips = lipsList[i];

            var prevTop = GetLastOrDefault(topList);
            var top = Math.Abs(jaw - teeth);
            topList.Add(top);

            var prevBottom = GetLastOrDefault(bottomList);
            var bottom = -Math.Abs(teeth - lips);
            bottomList.Add(bottom);

            var signal = GetCompareSignal(top - bottom, prevTop - prevBottom);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Top", topList },
            { "Bottom", bottomList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
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
        List<double> uoList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var bpSumWindow = new RollingSum();
        var trSumWindow = new RollingSum();

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
            bpSumWindow.Add(buyingPressure);

            var trueRange = maxValue - minValue;
            trSumWindow.Add(trueRange);

            var bp7Sum = bpSumWindow.Sum(length1);
            var bp14Sum = bpSumWindow.Sum(length2);
            var bp28Sum = bpSumWindow.Sum(length3);
            var tr7Sum = trSumWindow.Sum(length1);
            var tr14Sum = trSumWindow.Sum(length2);
            var tr28Sum = trSumWindow.Sum(length3);
            var avg7 = tr7Sum != 0 ? bp7Sum / tr7Sum : 0;
            var avg14 = tr14Sum != 0 ? bp14Sum / tr14Sum : 0;
            var avg28 = tr28Sum != 0 ? bp28Sum / tr28Sum : 0;

            var ultimateOscillator = MinOrMax(100 * (((4 * avg7) + (2 * avg14) + avg28) / (4 + 2 + 1)), 100, 0);
            uoList.Add(ultimateOscillator);

            var signal = GetRsiSignal(ultimateOscillator - prevUo1, prevUo1 - prevUo2, ultimateOscillator, prevUo1, 70, 30);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Uo", uoList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(uoList);
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
        List<double> viPlus14List = new(stockData.Count);
        List<double> viMinus14List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var vmPlusSumWindow = new RollingSum();
        var vmMinusSumWindow = new RollingSum();
        var trueRangeSumWindow = new RollingSum();

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var prevClose = i >= 1 ? inputList[i - 1] : 0;
            var prevLow = i >= 1 ? lowList[i - 1] : 0;
            var prevHigh = i >= 1 ? highList[i - 1] : 0;

            var vmPlus = Math.Abs(currentHigh - prevLow);
            vmPlusSumWindow.Add(vmPlus);

            var vmMinus = Math.Abs(currentLow - prevHigh);
            vmMinusSumWindow.Add(vmMinus);

            var trueRange = CalculateTrueRange(currentHigh, currentLow, prevClose);
            trueRangeSumWindow.Add(trueRange);

            var vmPlus14 = vmPlusSumWindow.Sum(length);
            var vmMinus14 = vmMinusSumWindow.Sum(length);
            var trueRange14 = trueRangeSumWindow.Sum(length);

            var prevViPlus14 = GetLastOrDefault(viPlus14List);
            var viPlus14 = trueRange14 != 0 ? vmPlus14 / trueRange14 : 0;
            viPlus14List.Add(viPlus14);

            var prevViMinus14 = GetLastOrDefault(viMinus14List);
            var viMinus14 = trueRange14 != 0 ? vmMinus14 / trueRange14 : 0;
            viMinus14List.Add(viMinus14);

            var signal = GetCompareSignal(viPlus14 - viMinus14, prevViPlus14 - prevViMinus14);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "ViPlus", viPlus14List },
            { "ViMinus", viMinus14List }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
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
        List<double> trixList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var ema1List = GetMovingAverageList(stockData, maType, length, inputList);
        var ema2List = GetMovingAverageList(stockData, maType, length, ema1List);
        var ema3List = GetMovingAverageList(stockData, maType, length, ema2List);

        for (var i = 0; i < stockData.Count; i++)
        {
            var ema3 = ema3List[i];
            var prevEma3 = i >= 1 ? ema3List[i - 1] : 0;

            var trix = CalculatePercentChange(ema3, prevEma3);
            trixList.Add(trix);
        }

        var trixSignalList = GetMovingAverageList(stockData, maType, signalLength, trixList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var trix = trixList[i];
            var trixSignal = trixSignalList[i];
            var prevTrix = i >= 1 ? trixList[i - 1] : 0;
            var prevTrixSignal = i >= 1 ? trixSignalList[i - 1] : 0;

            var signal = GetCompareSignal(trix - trixSignal, prevTrix - prevTrixSignal);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Trix", trixList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(trixList);
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
        List<double> williamsRList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
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
            williamsRList.Add(williamsR);

            var signal = GetRsiSignal(williamsR - prevWilliamsR1, prevWilliamsR1 - prevWilliamsR2, williamsR, prevWilliamsR1, -20, -80);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Williams%R", williamsRList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(williamsRList);
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
        List<double> pcList = new(stockData.Count);
        List<double> absPCList = new(stockData.Count);
        List<double> tsiList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var pc = MinPastValues(i, 1, currentValue - prevValue);
            pcList.Add(pc);

            var absPC = Math.Abs(pc);
            absPCList.Add(absPC);
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
            tsiList.Add(tsi);
        }

        var tsiSignalList = GetMovingAverageList(stockData, maType, signalLength, tsiList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var tsi = tsiList[i];
            var tsiSignal = tsiSignalList[i];
            var prevTsi = i >= 1 ? tsiList[i - 1] : 0;
            var prevTsiSignal = i >= 1 ? tsiSignalList[i - 1] : 0;

            var signal = GetRsiSignal(tsi - tsiSignal, prevTsi - prevTsiSignal, tsi, prevTsi, 25, -25);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Tsi", tsiList },
            { "Signal", tsiSignalList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(tsiList);
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
        List<double> bullPowerList = new(stockData.Count);
        List<double> bearPowerList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        var emaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentEma = emaList[i];

            var prevBullPower = GetLastOrDefault(bullPowerList);
            var bullPower = currentHigh - currentEma;
            bullPowerList.Add(bullPower);

            var prevBearPower = GetLastOrDefault(bearPowerList);
            var bearPower = currentLow - currentEma;
            bearPowerList.Add(bearPower);

            var signal = GetCompareSignal(bullPower - bearPower, prevBullPower - prevBearPower);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "BullPower", bullPowerList },
            { "BearPower", bearPowerList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
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
        List<double> apoList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var fastEmaList = GetMovingAverageList(stockData, maType, fastLength, inputList);
        var slowEmaList = GetMovingAverageList(stockData, maType, slowLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var fastEma = fastEmaList[i];
            var slowEma = slowEmaList[i];

            var prevApo = GetLastOrDefault(apoList);
            var apo = fastEma - slowEma;
            apoList.Add(apo);

            var signal = GetCompareSignal(apo, prevApo);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Apo", apoList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(apoList);
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
        List<double> aroonOscillatorList = new(stockData.Count);
        List<double> tempList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(inputList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentPrice = inputList[i];
            tempList.Add(currentPrice);

            var maxPrice = highestList[i];
            var maxIndex = tempList.LastIndexOf(maxPrice);
            var minPrice = lowestList[i];
            var minIndex = tempList.LastIndexOf(minPrice);
            var daysSinceMax = i - maxIndex;
            var daysSinceMin = i - minIndex;
            var aroonUp = (double)(length - daysSinceMax) / length * 100;
            var aroonDown = (double)(length - daysSinceMin) / length * 100;

            var prevAroonOscillator = GetLastOrDefault(aroonOscillatorList);
            var aroonOscillator = aroonUp - aroonDown;
            aroonOscillatorList.Add(aroonOscillator);

            var signal = GetCompareSignal(aroonOscillator, prevAroonOscillator);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Aroon", aroonOscillatorList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(aroonOscillatorList);
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
}

