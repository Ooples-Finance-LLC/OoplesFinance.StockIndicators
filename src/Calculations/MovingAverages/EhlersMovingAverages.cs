using OoplesFinance.StockIndicators.Compatibility;
using OoplesFinance.StockIndicators.Core;

namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the ehlers mother of adaptive moving averages.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="fastAlpha">The fast alpha.</param>
    /// <param name="slowAlpha">The slow alpha.</param>
    /// <returns></returns>
    public static StockData CalculateEhlersMotherOfAdaptiveMovingAverages(this StockData stockData, double fastAlpha = 0.5, double slowAlpha = 0.05)
    {
        const double HilbertTransformCoeff1 = 0.0962;
        const double HilbertTransformCoeff2 = 0.5769;
        const double PeriodCorrectionFactor = 0.075;
        const double PeriodCorrectionOffset = 0.54;

        List<double> famaList = new(stockData.Count);
        List<double> mamaList = new(stockData.Count);
        List<double> i2List = new(stockData.Count);
        List<double> q2List = new(stockData.Count);
        List<double> reList = new(stockData.Count);
        List<double> imList = new(stockData.Count);
        List<double> sPrdList = new(stockData.Count);
        List<double> phaseList = new(stockData.Count);
        List<double> periodList = new(stockData.Count);
        List<double> smoothList = new(stockData.Count);
        List<double> detList = new(stockData.Count);
        List<double> q1List = new(stockData.Count);
        List<double> i1List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevPrice1 = i >= 1 ? inputList[i - 1] : 0;
            var previ2 = i >= 1 ? i2List[i - 1] : 0;
            var prevq2 = i >= 1 ? q2List[i - 1] : 0;
            var prevRe = i >= 1 ? reList[i - 1] : 0;
            var prevIm = i >= 1 ? imList[i - 1] : 0;
            var prevSprd = i >= 1 ? sPrdList[i - 1] : 0;
            var prevPhase = i >= 1 ? phaseList[i - 1] : 0;
            var prevPeriod = i >= 1 ? periodList[i - 1] : 0;
            var prevPrice2 = i >= 2 ? inputList[i - 2] : 0;
            var prevPrice3 = i >= 3 ? inputList[i - 3] : 0;
            var prevs2 = i >= 2 ? smoothList[i - 2] : 0;
            var prevd2 = i >= 2 ? detList[i - 2] : 0;
            var prevq1x2 = i >= 2 ? q1List[i - 2] : 0;
            var previ1x2 = i >= 2 ? i1List[i - 2] : 0;
            var prevd3 = i >= 3 ? detList[i - 3] : 0;
            var prevs4 = i >= 4 ? smoothList[i - 4] : 0;
            var prevd4 = i >= 4 ? detList[i - 4] : 0;
            var prevq1x4 = i >= 4 ? q1List[i - 4] : 0;
            var previ1x4 = i >= 4 ? i1List[i - 4] : 0;
            var prevs6 = i >= 6 ? smoothList[i - 6] : 0;
            var prevd6 = i >= 6 ? detList[i - 6] : 0;
            var prevq1x6 = i >= 6 ? q1List[i - 6] : 0;
            var previ1x6 = i >= 6 ? i1List[i - 6] : 0;
            var prevMama = i >= 1 ? mamaList[i - 1] : 0;
            var prevFama = i >= 1 ? famaList[i - 1] : 0;

            var smooth = ((4 * currentValue) + (3 * prevPrice1) + (2 * prevPrice2) + prevPrice3) / 10;
            smoothList.Add(smooth);

            var det = ((HilbertTransformCoeff1 * smooth) + (HilbertTransformCoeff2 * prevs2) - (HilbertTransformCoeff2 * prevs4) - (HilbertTransformCoeff1 * prevs6)) * ((PeriodCorrectionFactor * prevPeriod) + PeriodCorrectionOffset);
            detList.Add(det);

            var q1 = ((HilbertTransformCoeff1 * det) + (HilbertTransformCoeff2 * prevd2) - (HilbertTransformCoeff2 * prevd4) - (HilbertTransformCoeff1 * prevd6)) * ((PeriodCorrectionFactor * prevPeriod) + PeriodCorrectionOffset);
            q1List.Add(q1);

            var i1 = prevd3;
            i1List.Add(i1);

            var j1 = ((HilbertTransformCoeff1 * i1) + (HilbertTransformCoeff2 * previ1x2) - (HilbertTransformCoeff2 * previ1x4) - (HilbertTransformCoeff1 * previ1x6)) * ((PeriodCorrectionFactor * prevPeriod) + PeriodCorrectionOffset);
            var jq = ((HilbertTransformCoeff1 * q1) + (HilbertTransformCoeff2 * prevq1x2) - (HilbertTransformCoeff2 * prevq1x4) - (HilbertTransformCoeff1 * prevq1x6)) * ((PeriodCorrectionFactor * prevPeriod) + PeriodCorrectionOffset);

            var i2 = i1 - jq;
            i2 = (0.2 * i2) + (0.8 * previ2);
            i2List.Add(i2);

            var q2 = q1 + j1;
            q2 = (0.2 * q2) + (0.8 * prevq2);
            q2List.Add(q2);

            var re = (i2 * previ2) + (q2 * prevq2);
            re = (0.2 * re) + (0.8 * prevRe);
            reList.Add(re);

            var im = (i2 * prevq2) - (q2 * previ2);
            im = (0.2 * im) + (0.8 * prevIm);
            imList.Add(im);

            var atan = re != 0 ? Math.Atan(im / re) : 0;
            var period = atan != 0 ? 2 * Math.PI / atan : 0;

            if (prevPeriod != 0)
            {
                period = MinOrMax(period, 1.5 * prevPeriod, 0.67 * prevPeriod);
            }

            period = MinOrMax(period, 50, 6);
            period = (0.2 * period) + (0.8 * prevPeriod);
            periodList.Add(period);

            var sPrd = (0.33 * period) + (0.67 * prevSprd);
            sPrdList.Add(sPrd);

            var phase = i1 != 0 ? Math.Atan(q1 / i1).ToDegrees() : 0;
            phaseList.Add(phase);

            var deltaPhase = prevPhase - phase < 1 ? 1 : prevPhase - phase;
            var alpha = deltaPhase != 0 ? fastAlpha / deltaPhase : 0;
            alpha = alpha < slowAlpha ? slowAlpha : alpha;

            var mama = (alpha * currentValue) + ((1 - alpha) * prevMama);
            mamaList.Add(mama);

            var fama = (0.5 * alpha * mama) + ((1 - (0.5 * alpha)) * prevFama);
            famaList.Add(fama);

            var signal = GetCompareSignal(mama - fama, prevMama - prevFama);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Fama", famaList },
            { "Mama", mamaList },
            { "I1", i1List },
            { "Q1", q1List },
            { "SmoothPeriod", sPrdList },
            { "Smooth", smoothList },
            { "Real", reList },
            { "Imag", imList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(mamaList);
        stockData.IndicatorName = IndicatorName.EhlersMotherOfAdaptiveMovingAverages;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Fractal Adaptive Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersFractalAdaptiveMovingAverage(this StockData stockData, int length = 20)
    {
        List<double> filterList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        var halfP = MinOrMax((int)Math.Ceiling((double)length / 2));

        var (highestList1, lowestList1) = GetMaxAndMinValuesList(highList, lowList, length);
        var (highestList2, lowestList2) = GetMaxAndMinValuesList(highList, lowList, halfP);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevFilter = i >= 1 ? GetLastOrDefault(filterList) : currentValue;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var highestHigh1 = highestList1[i];
            var lowestLow1 = lowestList1[i];
            var highestHigh2 = highestList2[i];
            var lowestLow2 = lowestList2[i];
            var lagIndex = Math.Max(i - halfP, 0);
            var highestHigh3 = highestList2[lagIndex];
            var lowestLow3 = lowestList2[lagIndex];
            var n3 = (highestHigh1 - lowestLow1) / length;
            var n1 = (highestHigh2 - lowestLow2) / halfP;
            var n2 = (highestHigh3 - lowestLow3) / halfP;
            var dm = n1 > 0 && n2 > 0 && n3 > 0 ? (Math.Log(n1 + n2) - Math.Log(n3)) / Math.Log(2) : 0;

            var alpha = MinOrMax(Exp(-4.6 * (dm - 1)), 1, 0.01);
            var filter = (alpha * currentValue) + ((1 - alpha) * prevFilter);
            filterList.Add(filter);

            var signal = GetCompareSignal(currentValue - filter, prevValue - prevFilter);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Fama", filterList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(filterList);
        stockData.IndicatorName = IndicatorName.EhlersFractalAdaptiveMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Median Average Adaptive Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="threshold"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersMedianAverageAdaptiveFilter(this StockData stockData, int length = 39, double threshold = 0.002)
    {
        List<double> filterList = new(stockData.Count);
        List<double> value2List = new(stockData.Count);
        List<double> smthList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var windowTree = new OrderStatisticTree();
        var removedValues = new List<double>();

        static double GetMedian(OrderStatisticTree tree, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            if ((count & 1) == 1)
            {
                return tree.SelectByRank((count + 1) / 2);
            }

            var left = tree.SelectByRank(count / 2);
            var right = tree.SelectByRank((count / 2) + 1);
            return (left + right) / 2;
        }

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentPrice = inputList[i];
            var prevP1 = i >= 1 ? inputList[i - 1] : 0;
            var prevP2 = i >= 2 ? inputList[i - 2] : 0;
            var prevP3 = i >= 3 ? inputList[i - 3] : 0;

            var smth = (currentPrice + (2 * prevP1) + (2 * prevP2) + prevP3) / 6;
            smthList.Add(smth);
            windowTree.Insert(smth);
            if (smthList.Count > length)
            {
                windowTree.Remove(smthList[smthList.Count - length - 1]);
            }

            var len = length;
            double value3 = 0.2, value2 = 0, prevV2 = GetLastOrDefault(value2List), alpha;
            var available = Math.Min(length, smthList.Count);
            var windowStart = smthList.Count - available;
            var removedOffset = 0;
            removedValues.Clear();
            while (value3 > threshold && len > 0)
            {
                alpha = (double)2 / (len + 1);
                var value1 = GetMedian(windowTree, Math.Min(len, available));
                value2 = (alpha * smth) + ((1 - alpha) * prevV2);
                value3 = value1 != 0 ? Math.Abs(value1 - value2) / value1 : value3;
                len -= 2;

                if (value3 > threshold && len > 0 && len < available)
                {
                    var firstIndex = windowStart + removedOffset;
                    var secondIndex = firstIndex + 1;
                    var firstValue = smthList[firstIndex];
                    var secondValue = smthList[secondIndex];
                    windowTree.Remove(firstValue);
                    windowTree.Remove(secondValue);
                    removedValues.Add(firstValue);
                    removedValues.Add(secondValue);
                    removedOffset += 2;
                }
            }
            foreach (var removedValue in removedValues)
            {
                windowTree.Insert(removedValue);
            }
            value2List.Add(value2);

            len = len < 3 ? 3 : len;
            alpha = (double)2 / (len + 1);

            var prevFilter = GetLastOrDefault(filterList);
            var filter = (alpha * smth) + ((1 - alpha) * prevFilter);
            filterList.Add(filter);

            var signal = GetCompareSignal(currentPrice - filter, prevP1 - prevFilter);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Maaf", filterList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(filterList);
        stockData.IndicatorName = IndicatorName.EhlersMedianAverageAdaptiveFilter;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers 2 Pole Super Smoother Filter V2
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateEhlers2PoleSuperSmootherFilterV2(this StockData stockData, int length = 10)
    {
        List<double> filtList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var a = Exp(MinOrMax(-MathHelper.Sqrt2 * Math.PI / length, -0.01, -0.99));
        var b = 2 * a * Math.Cos(MinOrMax(MathHelper.Sqrt2 * Math.PI / length, 0.99, 0.01));
        var c2 = b;
        var c3 = -a * a;
        var c1 = 1 - c2 - c3;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevFilter1 = i >= 1 ? filtList[i - 1] : 0;
            var prevFilter2 = i >= 2 ? filtList[i - 2] : 0;

            var filt = (c1 * ((currentValue + prevValue) / 2)) + (c2 * prevFilter1) + (c3 * prevFilter2);
            filtList.Add(filt);

            var signal = GetCompareSignal(currentValue - filt, prevValue - prevFilter1);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "E2ssf", filtList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(filtList);
        stockData.IndicatorName = IndicatorName.Ehlers2PoleSuperSmootherFilterV2;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers 3 Pole Super Smoother Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateEhlers3PoleSuperSmootherFilter(this StockData stockData, int length = 20)
    {
        List<double> filtList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var arg = MinOrMax(Math.PI / length, 0.99, 0.01);
        var a1 = Exp(-arg);
        var b1 = 2 * a1 * Math.Cos(1.738 * arg);
        var c1 = a1 * a1;
        var coef2 = b1 + c1;
        var coef3 = -(c1 + (b1 * c1));
        var coef4 = c1 * c1;
        var coef1 = 1 - coef2 - coef3 - coef4;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevFilter1 = i >= 1 ? filtList[i - 1] : 0;
            var prevFilter2 = i >= 2 ? filtList[i - 2] : 0;
            var prevFilter3 = i >= 3 ? filtList[i - 3] : 0;

            var filt = i < 4 ? currentValue : (coef1 * currentValue) + (coef2 * prevFilter1) + (coef3 * prevFilter2) + (coef4 * prevFilter3);
            filtList.Add(filt);

            var signal = GetCompareSignal(currentValue - filt, prevValue - prevFilter1);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "E3ssf", filtList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(filtList);
        stockData.IndicatorName = IndicatorName.Ehlers3PoleSuperSmootherFilter;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers 2 Pole Butterworth Filter V1
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateEhlers2PoleButterworthFilterV1(this StockData stockData, int length = 10)
    {
        List<double> filtList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var a = Exp(MinOrMax(-MathHelper.Sqrt2 * Math.PI / length, -0.01, -0.99));
        var b = 2 * a * Math.Cos(MinOrMax(MathHelper.Sqrt2 * 1.25 * Math.PI / length, 0.99, 0.01));
        var c2 = b;
        var c3 = -a * a;
        var c1 = 1 - c2 - c3;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevFilter1 = i >= 1 ? filtList[i - 1] : 0;
            var prevFilter2 = i >= 2 ? filtList[i - 2] : 0;

            var filt = (c1 * currentValue) + (c2 * prevFilter1) + (c3 * prevFilter2);
            filtList.Add(filt);

            var signal = GetCompareSignal(currentValue - filt, prevValue - prevFilter1);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "E2bf", filtList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(filtList);
        stockData.IndicatorName = IndicatorName.Ehlers2PoleButterworthFilterV1;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers 2 Pole Butterworth Filter V2
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateEhlers2PoleButterworthFilterV2(this StockData stockData, int length = 15)
    {
        List<double> filtList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var a = Exp(MinOrMax(-MathHelper.Sqrt2 * Math.PI / length, -0.01, -0.99));
        var b = 2 * a * Math.Cos(MinOrMax(MathHelper.Sqrt2 * Math.PI / length, 0.99, 0.01));
        var c2 = b;
        var c3 = -a * a;
        var c1 = (1 - b + Pow(a, 2)) / 4;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevFilter1 = i >= 1 ? filtList[i - 1] : 0;
            var prevFilter2 = i >= 2 ? filtList[i - 2] : 0;
            var prevValue1 = i >= 1 ? inputList[i - 1] : 0;
            var prevValue3 = i >= 3 ? inputList[i - 3] : 0;

            var filt = i < 3 ? currentValue : (c1 * (currentValue + (2 * prevValue1) + prevValue3)) + (c2 * prevFilter1) + (c3 * prevFilter2);
            filtList.Add(filt);

            var signal = GetCompareSignal(currentValue - filt, prevValue1 - prevFilter1);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "E2bf", filtList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(filtList);
        stockData.IndicatorName = IndicatorName.Ehlers2PoleButterworthFilterV2;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers 3 Pole Butterworth Filter V1
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateEhlers3PoleButterworthFilterV1(this StockData stockData, int length = 10)
    {
        List<double> filtList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var a = Exp(MinOrMax(-Math.PI / length, -0.01, -0.99));
        var b = 2 * a * Math.Cos(MinOrMax(1.738 * Math.PI / length, 0.99, 0.01));
        var c = a * a;
        var d2 = b + c;
        var d3 = -(c + (b * c));
        var d4 = c * c;
        var d1 = 1 - d2 - d3 - d4;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevFilter1 = i >= 1 ? filtList[i - 1] : 0;
            var prevFilter2 = i >= 2 ? filtList[i - 2] : 0;
            var prevFilter3 = i >= 3 ? filtList[i - 3] : 0;

            var filt = (d1 * currentValue) + (d2 * prevFilter1) + (d3 * prevFilter2) + (d4 * prevFilter3);
            filtList.Add(filt);

            var signal = GetCompareSignal(currentValue - filt, prevValue - prevFilter1);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "E3bf", filtList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(filtList);
        stockData.IndicatorName = IndicatorName.Ehlers3PoleButterworthFilterV1;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers 3 Pole Butterworth Filter V2
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateEhlers3PoleButterworthFilterV2(this StockData stockData, int length = 15)
    {
        List<double> filtList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var a1 = Exp(MinOrMax(-Math.PI / length, -0.01, -0.99));
        var b1 = 2 * a1 * Math.Cos(MinOrMax(1.738 * Math.PI / length, 0.99, 0.01));
        var c1 = a1 * a1;
        var coef2 = b1 + c1;
        var coef3 = -(c1 + (b1 * c1));
        var coef4 = c1 * c1;
        var coef1 = (1 - b1 + c1) * (1 - c1) / 8;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue1 = i >= 1 ? inputList[i - 1] : 0;
            var prevValue2 = i >= 2 ? inputList[i - 2] : 0;
            var prevValue3 = i >= 3 ? inputList[i - 3] : 0;
            var prevFilter1 = i >= 1 ? filtList[i - 1] : 0;
            var prevFilter2 = i >= 2 ? filtList[i - 2] : 0;
            var prevFilter3 = i >= 3 ? filtList[i - 3] : 0;

            var filt = i < 4 ? currentValue : (coef1 * (currentValue + (3 * prevValue1) + (3 * prevValue2) + prevValue3)) + (coef2 * prevFilter1) +
                                              (coef3 * prevFilter2) + (coef4 * prevFilter3);
            filtList.Add(filt);

            var signal = GetCompareSignal(currentValue - filt, prevValue1 - prevFilter1);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "E3bf", filtList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(filtList);
        stockData.IndicatorName = IndicatorName.Ehlers3PoleButterworthFilterV2;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Gaussian Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersGaussianFilter(this StockData stockData, int length = 14)
    {
        List<double> gf1List = new(stockData.Count);
        List<double> gf2List = new(stockData.Count);
        List<double> gf3List = new(stockData.Count);
        List<double> gf4List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var cosVal = MinOrMax(2 * Math.PI / length, 0.99, 0.01);
        var beta1 = (1 - Math.Cos(cosVal)) / (Pow(2, (double)1 / 1) - 1);
        var beta2 = (1 - Math.Cos(cosVal)) / (Pow(2, (double)1 / 2) - 1);
        var beta3 = (1 - Math.Cos(cosVal)) / (Pow(2, (double)1 / 3) - 1);
        var beta4 = (1 - Math.Cos(cosVal)) / (Pow(2, (double)1 / 4) - 1);
        var alpha1 = -beta1 + Sqrt(Pow(beta1, 2) + (2 * beta1));
        var alpha2 = -beta2 + Sqrt(Pow(beta2, 2) + (2 * beta2));
        var alpha3 = -beta3 + Sqrt(Pow(beta3, 2) + (2 * beta3));
        var alpha4 = -beta4 + Sqrt(Pow(beta4, 2) + (2 * beta4));

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevGf1 = i >= 1 ? gf1List[i - 1] : 0;
            var prevGf2_1 = i >= 1 ? gf2List[i - 1] : 0;
            var prevGf2_2 = i >= 2 ? gf2List[i - 2] : 0;
            var prevGf3_1 = i >= 1 ? gf3List[i - 1] : 0;
            var prevGf4_1 = i >= 1 ? gf4List[i - 1] : 0;
            var prevGf3_2 = i >= 2 ? gf3List[i - 2] : 0;
            var prevGf4_2 = i >= 2 ? gf4List[i - 2] : 0;
            var prevGf3_3 = i >= 3 ? gf3List[i - 3] : 0;
            var prevGf4_3 = i >= 3 ? gf4List[i - 3] : 0;
            var prevGf4_4 = i >= 4 ? gf4List[i - 4] : 0;

            var gf1 = (alpha1 * currentValue) + ((1 - alpha1) * prevGf1);
            gf1List.Add(gf1);

            var gf2 = (Pow(alpha2, 2) * currentValue) + (2 * (1 - alpha2) * prevGf2_1) - (Pow(1 - alpha2, 2) * prevGf2_2);
            gf2List.Add(gf2);

            var gf3 = (Pow(alpha3, 3) * currentValue) + (3 * (1 - alpha3) * prevGf3_1) - (3 * Pow(1 - alpha3, 2) * prevGf3_2) +
                      (Pow(1 - alpha3, 3) * prevGf3_3);
            gf3List.Add(gf3);

            var gf4 = (Pow(alpha4, 4) * currentValue) + (4 * (1 - alpha4) * prevGf4_1) - (6 * Pow(1 - alpha4, 2) * prevGf4_2) +
                (4 * Pow(1 - alpha4, 3) * prevGf4_3) - (Pow(1 - alpha4, 4) * prevGf4_4);
            gf4List.Add(gf4);

            var signal = GetCompareSignal(currentValue - gf4, prevValue - prevGf4_1);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Egf1", gf1List },
            { "Egf2", gf2List },
            { "Egf3", gf3List },
            { "Egf4", gf4List }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(gf4List);
        stockData.IndicatorName = IndicatorName.EhlersGaussianFilter;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Recursive Median Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersRecursiveMedianFilter(this StockData stockData, int length1 = 5, int length2 = 12)
    {
        List<double> tempList = new(stockData.Count);
        List<double> rmfList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        using var tempMedian = new RollingMedian(length1);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var alphaArg = MinOrMax(2 * Math.PI / length2, 0.99, 0.01);
        var alphaArgCos = Math.Cos(alphaArg);
        var alpha = alphaArgCos != 0 ? (alphaArgCos + Math.Sin(alphaArg) - 1) / alphaArgCos : 0;

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevValue = GetLastOrDefault(tempList);
            var currentValue = inputList[i];
            tempList.Add(currentValue);
            tempMedian.Add(currentValue);

            var median = tempMedian.Median;
            var prevRmf = GetLastOrDefault(rmfList);
            var rmf = (alpha * median) + ((1 - alpha) * prevRmf);
            rmfList.Add(rmf);

            var signal = GetCompareSignal(currentValue - rmf, prevValue - prevRmf);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ermf", rmfList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(rmfList);
        stockData.IndicatorName = IndicatorName.EhlersRecursiveMedianFilter;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Super Smoother Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersSuperSmootherFilter(this StockData stockData, int length = 10)
    {
        List<double> filtList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var a1 = Exp(MinOrMax(-MathHelper.Sqrt2 * Math.PI / length, -0.01, -0.99));
        var b1 = 2 * a1 * Math.Cos(MinOrMax(MathHelper.Sqrt2 * Math.PI / length, 0.99, 0.01));
        var coeff2 = b1;
        var coeff3 = -1 * a1 * a1;
        var coeff1 = 1 - coeff2 - coeff3;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevFilter1 = i >= 1 ? filtList[i - 1] : 0;
            var prevFilter2 = i >= 2 ? filtList[i - 2] : 0;

            var prevFilt = GetLastOrDefault(filtList);
            var filt = (coeff1 * ((currentValue + prevValue) / 2)) + (coeff2 * prevFilter1) + (coeff3 * prevFilter2);
            filtList.Add(filt);

            var signal = GetCompareSignal(currentValue - filt, prevValue - prevFilt);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Essf", filtList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(filtList);
        stockData.IndicatorName = IndicatorName.EhlersSuperSmootherFilter;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers 2 Pole Super Smoother Filter V1
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateEhlers2PoleSuperSmootherFilterV1(this StockData stockData, int length = 15)
    {
        List<double> filtList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var a1 = Exp(MinOrMax(-MathHelper.Sqrt2 * Math.PI / length, -0.01, -0.99));
        var b1 = 2 * a1 * Math.Cos(MinOrMax(MathHelper.Sqrt2 * Math.PI / length, 0.99, 0.01));
        var coef2 = b1;
        var coef3 = -a1 * a1;
        var coef1 = 1 - coef2 - coef3;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevFilter1 = i >= 1 ? filtList[i - 1] : 0;
            var prevFilter2 = i >= 2 ? filtList[i - 2] : 0;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var filt = i < 3 ? currentValue : (coef1 * currentValue) + (coef2 * prevFilter1) + (coef3 * prevFilter2);
            filtList.Add(filt);

            var signal = GetCompareSignal(currentValue - filt, prevValue - prevFilter1);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Essf", filtList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(filtList);
        stockData.IndicatorName = IndicatorName.Ehlers2PoleSuperSmootherFilterV1;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Average Error Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersAverageErrorFilter(this StockData stockData, int length = 27)
    {
        List<double> filtList = new(stockData.Count);
        List<double> ssfList = new(stockData.Count);
        List<double> e1List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var a1 = Exp(MinOrMax(-MathHelper.Sqrt2 * Math.PI / length, -0.01, -0.99));
        var b1 = 2 * a1 * Math.Cos(MinOrMax(MathHelper.Sqrt2 * Math.PI / length, 0.99, 0.01));
        var c2 = b1;
        var c3 = -1 * a1 * a1;
        var c1 = 1 - c2 - c3;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevE11 = i >= 1 ? e1List[i - 1] : 0;
            var prevE12 = i >= 2 ? e1List[i - 2] : 0;
            var prevSsf1 = i >= 1 ? ssfList[i - 1] : 0;
            var prevSsf2 = i >= 2 ? ssfList[i - 2] : 0;

            var ssf = i < 3 ? currentValue : (0.5 * c1 * (currentValue + prevValue)) + (c2 * prevSsf1) + (c3 * prevSsf2);
            ssfList.Add(ssf);

            var e1 = i < 3 ? 0 : (c1 * (currentValue - ssf)) + (c2 * prevE11) + (c3 * prevE12);
            e1List.Add(e1);

            var prevFilt = GetLastOrDefault(filtList);
            var filt = ssf + e1;
            filtList.Add(filt);

            var signal = GetCompareSignal(currentValue - filt, prevValue - prevFilt);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Eaef", filtList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(filtList);
        stockData.IndicatorName = IndicatorName.EhlersAverageErrorFilter;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Laguerre Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="alpha"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersLaguerreFilter(this StockData stockData, double alpha = 0.2)
    {
        List<double> filterList = new(stockData.Count);
        List<double> firList = new(stockData.Count);
        List<double> l0List = new(stockData.Count);
        List<double> l1List = new(stockData.Count);
        List<double> l2List = new(stockData.Count);
        List<double> l3List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevP1 = i >= 1 ? inputList[i - 1] : 0;
            var prevP2 = i >= 2 ? inputList[i - 2] : 0;
            var prevP3 = i >= 3 ? inputList[i - 3] : 0;
            var prevL0 = i >= 1 ? GetLastOrDefault(l0List) : currentValue;
            var prevL1 = i >= 1 ? GetLastOrDefault(l1List) : currentValue;
            var prevL2 = i >= 1 ? GetLastOrDefault(l2List) : currentValue;
            var prevL3 = i >= 1 ? GetLastOrDefault(l3List) : currentValue;

            var l0 = (alpha * currentValue) + ((1 - alpha) * prevL0);
            l0List.Add(l0);

            var l1 = (-1 * (1 - alpha) * l0) + prevL0 + ((1 - alpha) * prevL1);
            l1List.Add(l1);

            var l2 = (-1 * (1 - alpha) * l1) + prevL1 + ((1 - alpha) * prevL2);
            l2List.Add(l2);

            var l3 = (-1 * (1 - alpha) * l2) + prevL2 + ((1 - alpha) * prevL3);
            l3List.Add(l3);

            var prevFilter = GetLastOrDefault(filterList);
            var filter = (l0 + (2 * l1) + (2 * l2) + l3) / 6;
            filterList.Add(filter);

            var prevFir = GetLastOrDefault(firList);
            var fir = (currentValue + (2 * prevP1) + (2 * prevP2) + prevP3) / 6;
            firList.Add(fir);

            var signal = GetCompareSignal(filter - fir, prevFilter - prevFir);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Elf", filterList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(filterList);
        stockData.IndicatorName = IndicatorName.EhlersLaguerreFilter;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Adaptive Laguerre Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersAdaptiveLaguerreFilter(this StockData stockData, int length1 = 14, int length2 = 5)
    {
        List<double> filterList = new(stockData.Count);
        List<double> l0List = new(stockData.Count);
        List<double> l1List = new(stockData.Count);
        List<double> l2List = new(stockData.Count);
        List<double> l3List = new(stockData.Count);
        List<double> diffList = new(stockData.Count);
        List<double> midList = new(stockData.Count);
        List<double> alphaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingMinMax diffWindow = new(length1);
        using var midMedian = new RollingMedian(length2);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevL0 = i >= 1 ? GetLastOrDefault(l0List) : currentValue;
            var prevL1 = i >= 1 ? GetLastOrDefault(l1List) : currentValue;
            var prevL2 = i >= 1 ? GetLastOrDefault(l2List) : currentValue;
            var prevL3 = i >= 1 ? GetLastOrDefault(l3List) : currentValue;
            var prevFilter = i >= 1 ? GetLastOrDefault(filterList) : currentValue;

            var diff = Math.Abs(currentValue - prevFilter);
            diffList.Add(diff);
            diffWindow.Add(diff);

            var highestHigh = diffWindow.Max;
            var lowestLow = diffWindow.Min;

            var mid = highestHigh - lowestLow != 0 ? (diff - lowestLow) / (highestHigh - lowestLow) : 0;
            midList.Add(mid);
            midMedian.Add(mid);

            var prevAlpha = i >= 1 ? GetLastOrDefault(alphaList) : (double)2 / (length1 + 1);
            var alpha = mid != 0 ? midMedian.Median : prevAlpha;
            alphaList.Add(alpha);

            var l0 = (alpha * currentValue) + ((1 - alpha) * prevL0);
            l0List.Add(l0);

            var l1 = (-1 * (1 - alpha) * l0) + prevL0 + ((1 - alpha) * prevL1);
            l1List.Add(l1);

            var l2 = (-1 * (1 - alpha) * l1) + prevL1 + ((1 - alpha) * prevL2);
            l2List.Add(l2);

            var l3 = (-1 * (1 - alpha) * l2) + prevL2 + ((1 - alpha) * prevL3);
            l3List.Add(l3);

            var filter = (l0 + (2 * l1) + (2 * l2) + l3) / 6;
            filterList.Add(filter);

            var signal = GetCompareSignal(currentValue - filter, prevValue - prevFilter);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ealf", filterList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(filterList);
        stockData.IndicatorName = IndicatorName.EhlersAdaptiveLaguerreFilter;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Leading Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="alpha1"></param>
    /// <param name="alpha2"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersLeadingIndicator(this StockData stockData, double alpha1 = 0.25, double alpha2 = 0.33)
    {
        List<double> leadList = new(stockData.Count);
        List<double> leadIndicatorList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevLead = GetLastOrDefault(leadList);
            var lead = (2 * currentValue) + ((alpha1 - 2) * prevValue) + ((1 - alpha1) * prevLead);
            leadList.Add(lead);

            var prevLeadIndicator = GetLastOrDefault(leadIndicatorList);
            var leadIndicator = (alpha2 * lead) + ((1 - alpha2) * prevLeadIndicator);
            leadIndicatorList.Add(leadIndicator);

            var signal = GetCompareSignal(currentValue - leadIndicator, prevValue - prevLeadIndicator);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Eli", leadIndicatorList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(leadIndicatorList);
        stockData.IndicatorName = IndicatorName.EhlersLeadingIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Optimum Elliptic Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersOptimumEllipticFilter(this StockData stockData)
    {
        List<double> oefList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue1 = i >= 1 ? inputList[i - 1] : 0;
            var prevValue2 = i >= 2 ? inputList[i - 2] : 0;
            var prevOef1 = i >= 1 ? oefList[i - 1] : 0;
            var prevOef2 = i >= 2 ? oefList[i - 2] : 0;

            var oef = (0.13785 * currentValue) + (0.0007 * prevValue1) + (0.13785 * prevValue2) + (1.2103 * prevOef1) - (0.4867 * prevOef2);
            oefList.Add(oef);

            var signal = GetCompareSignal(currentValue - oef, prevValue1 - prevOef1);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Emoef", oefList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(oefList);
        stockData.IndicatorName = IndicatorName.EhlersOptimumEllipticFilter;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Modified Optimum Elliptic Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersModifiedOptimumEllipticFilter(this StockData stockData)
    {
        List<double> moefList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue1 = i >= 1 ? inputList[i - 1] : currentValue;
            var prevValue2 = i >= 2 ? inputList[i - 2] : prevValue1;
            var prevValue3 = i >= 3 ? inputList[i - 3] : prevValue2;
            var prevMoef1 = i >= 1 ? moefList[i - 1] : currentValue;
            var prevMoef2 = i >= 2 ? moefList[i - 2] : prevMoef1;

            var moef = (0.13785 * ((2 * currentValue) - prevValue1)) + (0.0007 * ((2 * prevValue1) - prevValue2)) +
                (0.13785 * ((2 * prevValue2) - prevValue3)) + (1.2103 * prevMoef1) - (0.4867 * prevMoef2);
            moefList.Add(moef);

            var signal = GetCompareSignal(currentValue - moef, prevValue1 - prevMoef1);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Emoef", moefList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(moefList);
        stockData.IndicatorName = IndicatorName.EhlersModifiedOptimumEllipticFilter;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersFilter(this StockData stockData, int length1 = 15, int length2 = 5)
    {
        List<double> filterList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            double num = 0, sumC = 0;
            for (var j = 0; j <= length1 - 1; j++)
            {
                var currentPrice = i >= j ? inputList[i - j] : 0;
                var prevPrice = i >= j + length2 ? inputList[i - (j + length2)] : 0;
                var priceDiff = Math.Abs(currentPrice - prevPrice);

                num += priceDiff * currentPrice;
                sumC += priceDiff;
            }

            var prevEhlersFilter = GetLastOrDefault(filterList);
            var ehlersFilter = sumC != 0 ? num / sumC : 0;
            filterList.Add(ehlersFilter);

            var signal = GetCompareSignal(currentValue - ehlersFilter, prevValue - prevEhlersFilter);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ef", filterList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(filterList);
        stockData.IndicatorName = IndicatorName.EhlersFilter;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Distance Coefficient Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersDistanceCoefficientFilter(this StockData stockData, int length = 14)
    {
        List<double> filterList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            double srcSum = 0, coefSum = 0;
            for (var count = 0; count <= length - 1; count++)
            {
                var prevCount = i >= count ? inputList[i - count] : 0;

                double distance = 0;
                for (var lookBack = 1; lookBack <= length - 1; lookBack++)
                {
                    var prevCountLookBack = i >= count + lookBack ? inputList[i - (count + lookBack)] : 0;
                    distance += Pow(prevCount - prevCountLookBack, 2);
                }

                srcSum += distance * prevCount;
                coefSum += distance;
            }

            var prevFilter = GetLastOrDefault(filterList);
            var filter = coefSum != 0 ? srcSum / coefSum : 0;
            filterList.Add(filter);

            var signal = GetCompareSignal(currentValue - filter, prevValue - prevFilter);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Edcf", filterList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(filterList);
        stockData.IndicatorName = IndicatorName.EhlersDistanceCoefficientFilter;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Finite Impulse Response Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="coef1"></param>
    /// <param name="coef2"></param>
    /// <param name="coef3"></param>
    /// <param name="coef4"></param>
    /// <param name="coef5"></param>
    /// <param name="coef6"></param>
    /// <param name="coef7"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersFiniteImpulseResponseFilter(this StockData stockData, double coef1 = 1, double coef2 = 3.5, double coef3 = 4.5,
        double coef4 = 3, double coef5 = 0.5, double coef6 = -0.5, double coef7 = -1.5)
    {
        List<double> filterList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var coefSum = coef1 + coef2 + coef3 + coef4 + coef5 + coef6 + coef7;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue1 = i >= 1 ? inputList[i - 1] : 0;
            var prevValue2 = i >= 2 ? inputList[i - 2] : 0;
            var prevValue3 = i >= 3 ? inputList[i - 3] : 0;
            var prevValue4 = i >= 4 ? inputList[i - 4] : 0;
            var prevValue5 = i >= 5 ? inputList[i - 5] : 0;
            var prevValue6 = i >= 6 ? inputList[i - 6] : 0;

            var prevFilter = GetLastOrDefault(filterList);
            var filter = ((coef1 * currentValue) + (coef2 * prevValue1) + (coef3 * prevValue2) + (coef4 * prevValue3) + 
                          (coef5 * prevValue4) + (coef6 * prevValue5) + (coef7 * prevValue6)) / coefSum;
            filterList.Add(filter);

            var signal = GetCompareSignal(currentValue - filter, prevValue1 - prevFilter);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Efirf", filterList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(filterList);
        stockData.IndicatorName = IndicatorName.EhlersFiniteImpulseResponseFilter;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Infinite Impulse Response Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersInfiniteImpulseResponseFilter(this StockData stockData, int length = 14)
    {
        List<double> filterList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var alpha = (double)2 / (length + 1);
        var lag = MinOrMax((int)Math.Ceiling((1 / alpha) - 1));

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= lag ? inputList[i - lag] : 0;
            var prevFilter1 = i >= 1 ? filterList[i - 1] : 0;

            var filter = (alpha * (currentValue + MinPastValues(i, lag, currentValue - prevValue))) + ((1 - alpha) * prevFilter1);
            filterList.Add(filter);

            var signal = GetCompareSignal(currentValue - filter, prevValue - prevFilter1);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Eiirf", filterList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(filterList);
        stockData.IndicatorName = IndicatorName.EhlersInfiniteImpulseResponseFilter;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Deviation Scaled Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersDeviationScaledMovingAverage(this StockData stockData, 
        MovingAvgType maType = MovingAvgType.Ehlers2PoleSuperSmootherFilterV2, int fastLength = 20, int slowLength = 40)
    {
        List<double> edsma2PoleList = new(stockData.Count);
        List<double> zerosList = new(stockData.Count);
        List<double> avgZerosList = new(stockData.Count);
        List<double> scaledFilter2PoleList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 2 ? inputList[i - 2] : 0;

            var prevZeros = GetLastOrDefault(zerosList);
            var zeros = MinPastValues(i, 2, currentValue - prevValue);
            zerosList.Add(zeros);

            var avgZeros = (zeros + prevZeros) / 2;
            avgZerosList.Add(avgZeros);
        }

        var ssf2PoleList = GetMovingAverageList(stockData, maType, fastLength, avgZerosList);
        stockData.SetCustomValues(ssf2PoleList);
        var ssf2PoleStdDevList = CalculateStandardDeviationVolatility(stockData, length: slowLength).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentSsf2Pole = ssf2PoleList[i];
            var currentSsf2PoleStdDev = ssf2PoleStdDevList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevScaledFilter2Pole = GetLastOrDefault(scaledFilter2PoleList);
            var scaledFilter2Pole = currentSsf2PoleStdDev != 0 ? currentSsf2Pole / currentSsf2PoleStdDev : prevScaledFilter2Pole;
            scaledFilter2PoleList.Add(scaledFilter2Pole);

            var alpha2Pole = MinOrMax(5 * Math.Abs(scaledFilter2Pole) / slowLength, 0.99, 0.01);
            var prevEdsma2pole = GetLastOrDefault(edsma2PoleList);
            var edsma2Pole = (alpha2Pole * currentValue) + ((1 - alpha2Pole) * prevEdsma2pole);
            edsma2PoleList.Add(edsma2Pole);

            var signal = GetCompareSignal(currentValue - edsma2Pole, prevValue - prevEdsma2pole);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Edsma", edsma2PoleList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(edsma2PoleList);
        stockData.IndicatorName = IndicatorName.EhlersDeviationScaledMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Hann Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersHannMovingAverage(this StockData stockData, int length = 20)
    {
        List<double> filtList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevFilt = i >= 1 ? filtList[i - 1] : 0;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            double filtSum = 0, coefSum = 0;
            for (var j = 1; j <= length; j++)
            {
                var prevV = i >= j - 1 ? inputList[i - (j - 1)] : 0;
                var cos = 1 - Math.Cos(2 * Math.PI * ((double)j / (length + 1)));
                filtSum += cos * prevV;
                coefSum += cos;
            }

            var filt = coefSum != 0 ? filtSum / coefSum : 0;
            filtList.Add(filt);

            var signal = GetCompareSignal(currentValue - filt, prevValue - prevFilt);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ehma", filtList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(filtList);
        stockData.IndicatorName = IndicatorName.EhlersHannMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Deviation Scaled Super Smoother
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersDeviationScaledSuperSmoother(this StockData stockData, MovingAvgType maType = MovingAvgType.EhlersHannMovingAverage,
        int length1 = 12, int length2 = 50)
    {
        List<double> momList = new(stockData.Count);
        List<double> dsssList = new(stockData.Count);
        List<double> filtPowList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum filtPowSumWindow = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var hannLength = (int)Math.Ceiling(length1 / 1.4m);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var priorValue = i >= length1 ? inputList[i - length1] : 0;

            var mom = currentValue - priorValue;
            momList.Add(mom);
        }

        var filtList = GetMovingAverageList(stockData, maType, hannLength, momList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var filt = filtList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevDsss1 = i >= 1 ? dsssList[i - 1] : 0;
            var prevDsss2 = i >= 2 ? dsssList[i - 2] : 0;

            var filtPow = Pow(filt, 2);
            filtPowList.Add(filtPow);
            filtPowSumWindow.Add(filtPow);

            var filtPowMa = filtPowSumWindow.Average(length2);
            var rms = filtPowMa > 0 ? Sqrt(filtPowMa) : 0;
            var scaledFilt = rms != 0 ? filt / rms : 0;
            var a1 = Exp(-MathHelper.Sqrt2 * Math.PI * Math.Abs(scaledFilt) / length1);
            var b1 = 2 * a1 * Math.Cos(MathHelper.Sqrt2 * Math.PI * Math.Abs(scaledFilt) / length1);
            var c2 = b1;
            var c3 = -a1 * a1;
            var c1 = 1 - c2 - c3;

            var dsss = (c1 * ((currentValue + prevValue) / 2)) + (c2 * prevDsss1) + (c3 * prevDsss2);
            dsssList.Add(dsss);

            var signal = GetCompareSignal(currentValue - dsss, prevValue - prevDsss1);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Edsss", dsssList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(dsssList);
        stockData.IndicatorName = IndicatorName.EhlersDeviationScaledSuperSmoother;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Zero Lag Exponential Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersZeroLagExponentialMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 14)
    {
        List<double> dList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var lag = MinOrMax((int)Math.Floor((double)(length - 1) / 2));

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= lag ? inputList[i - lag] : 0;

            var d = currentValue + MinPastValues(i, lag, currentValue - prevValue);
            dList.Add(d);
        }

        var zemaList = GetMovingAverageList(stockData, maType, length, dList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var zema = zemaList[i];
            var prevZema = i >= 1 ? zemaList[i - 1] : 0;

            var signal = GetCompareSignal(currentValue - zema, prevValue - prevZema);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ezlema", zemaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(zemaList);
        stockData.IndicatorName = IndicatorName.EhlersZeroLagExponentialMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Variable Index Dynamic Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersVariableIndexDynamicAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage, 
        int fastLength = 9, int slowLength = 30)
    {
        List<double> vidyaList = new(stockData.Count);
        List<double> longPowList = new(stockData.Count);
        List<double> shortPowList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum shortPowSumWindow = new();
        RollingSum longPowSumWindow = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var shortAvgList = GetMovingAverageList(stockData, maType, fastLength, inputList);
        var longAvgList = GetMovingAverageList(stockData, maType, slowLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var shortAvg = shortAvgList[i];
            var longAvg = longAvgList[i];

            var shortPow = Pow(currentValue - shortAvg, 2);
            shortPowList.Add(shortPow);
            shortPowSumWindow.Add(shortPow);

            var shortMa = shortPowSumWindow.Average(fastLength);
            var shortRms = shortMa > 0 ? Sqrt(shortMa) : 0;

            var longPow = Pow(currentValue - longAvg, 2);
            longPowList.Add(longPow);
            longPowSumWindow.Add(longPow);

            var longMa = longPowSumWindow.Average(slowLength);
            var longRms = longMa > 0 ? Sqrt(longMa) : 0;
            var kk = longRms != 0 ? MinOrMax(0.2 * shortRms / longRms, 0.99, 0.01) : 0;

            var prevVidya = GetLastOrDefault(vidyaList);
            var vidya = (kk * currentValue) + ((1 - kk) * prevVidya);
            vidyaList.Add(vidya);

            var signal = GetCompareSignal(currentValue - vidya, prevValue - prevVidya);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Evidya", vidyaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(vidyaList);
        stockData.IndicatorName = IndicatorName.EhlersVariableIndexDynamicAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Kaufman Adaptive Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersKaufmanAdaptiveMovingAverage(this StockData stockData, int length = 20)
    {
        List<double> kamaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var priorValue = i >= length - 1 ? inputList[i - (length - 1)] : 0;

            double deltaSum = 0;
            for (var j = 0; j < length; j++)
            {
                var cValue = i >= j ? inputList[i - j] : 0;
                var pValue = i >= j + 1 ? inputList[i - (j + 1)] : 0;
                deltaSum += Math.Abs(cValue - pValue);
            }

            var ef = deltaSum != 0 ? Math.Min(Math.Abs(currentValue - priorValue) / deltaSum, 1) : 0;
            var s = Pow((0.6667 * ef) + 0.0645, 2);

            var prevKama = GetLastOrDefault(kamaList);
            var kama = (s * currentValue) + ((1 - s) * prevKama);
            kamaList.Add(kama);

            var signal = GetCompareSignal(currentValue - kama, prevValue - prevKama);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ekama", kamaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(kamaList);
        stockData.IndicatorName = IndicatorName.EhlersKaufmanAdaptiveMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers All Pass Phase Shifter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="qq"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersAllPassPhaseShifter(this StockData stockData, int length = 20, double qq = 0.5)
    {
        List<double> phaserList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var a2 = qq != 0 && length != 0 ? -2 * Math.Cos(2 * Math.PI / length) / qq : 0;
        var a3 = qq != 0 ? Pow(1 / qq, 2) : 0;
        var b2 = length != 0 ? -2 * qq * Math.Cos(2 * Math.PI / length) : 0;
        var b3 = Pow(qq, 2);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue1 = i >= 1 ? inputList[i - 1] : 0;
            var prevValue2 = i >= 2 ? inputList[i - 2] : 0;
            var prevPhaser1 = i >= 1 ? phaserList[i - 1] : 0;
            var prevPhaser2 = i >= 2 ? phaserList[i - 2] : 0;

            var phaser = (b3 * (currentValue + (a2 * prevValue1) + (a3 * prevValue2))) - (b2 * prevPhaser1) - (b3 * prevPhaser2);
            phaserList.Add(phaser);

            var signal = GetCompareSignal(currentValue - phaser, prevValue1 - prevPhaser1);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Eapps", phaserList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(phaserList);
        stockData.IndicatorName = IndicatorName.EhlersAllPassPhaseShifter;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Chebyshev Low Pass Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersChebyshevLowPassFilter(this StockData stockData)
    {
        List<double> v1Neg2List = new(stockData.Count);
        List<double> waveNeg2List = new(stockData.Count);
        List<double> v1Neg1List = new(stockData.Count);
        List<double> waveNeg1List = new(stockData.Count);
        List<double> v10List = new(stockData.Count);
        List<double> wave0List = new(stockData.Count);
        List<double> v11List = new(stockData.Count);
        List<double> wave1List = new(stockData.Count);
        List<double> v12List = new(stockData.Count);
        List<double> wave2List = new(stockData.Count);
        List<double> v13List = new(stockData.Count);
        List<double> wave3List = new(stockData.Count);
        List<double> v14List = new(stockData.Count);
        List<double> wave4List = new(stockData.Count);
        List<double> v15List = new(stockData.Count);
        List<double> wave5List = new(stockData.Count);
        List<double> v16List = new(stockData.Count);
        List<double> wave6List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue1 = i >= 1 ? inputList[i - 1] : 0;
            var prevValue2 = i >= 2 ? inputList[i - 2] : 0;
            var prevV1Neg2_1 = i >= 1 ? v1Neg2List[i - 1] : 0;
            var prevV1Neg2_2 = i >= 2 ? v1Neg2List[i - 2] : 0;
            var prevWaveNeg2_1 = i >= 1 ? waveNeg2List[i - 1] : 0;
            var prevWaveNeg2_2 = i >= 2 ? waveNeg2List[i - 2] : 0;
            var prevV1Neg1_1 = i >= 1 ? v1Neg1List[i - 1] : 0;
            var prevV1Neg1_2 = i >= 2 ? v1Neg1List[i - 2] : 0;
            var prevWaveNeg1_1 = i >= 1 ? waveNeg1List[i - 1] : 0;
            var prevWaveNeg1_2 = i >= 2 ? waveNeg1List[i - 2] : 0;
            var prevV10_1 = i >= 1 ? v10List[i - 1] : 0;
            var prevV10_2 = i >= 2 ? v10List[i - 2] : 0;
            var prevWave0_1 = i >= 1 ? wave0List[i - 1] : 0;
            var prevWave0_2 = i >= 2 ? wave0List[i - 2] : 0;
            var prevV11_1 = i >= 1 ? v11List[i - 1] : 0;
            var prevV11_2 = i >= 2 ? v11List[i - 2] : 0;
            var prevWave1_1 = i >= 1 ? wave1List[i - 1] : 0;
            var prevWave1_2 = i >= 2 ? wave1List[i - 2] : 0;
            var prevV12_1 = i >= 1 ? v12List[i - 1] : 0;
            var prevV12_2 = i >= 2 ? v12List[i - 2] : 0;
            var prevWave2_1 = i >= 1 ? wave2List[i - 1] : 0;
            var prevWave2_2 = i >= 2 ? wave2List[i - 2] : 0;
            var prevV13_1 = i >= 1 ? v13List[i - 1] : 0;
            var prevV13_2 = i >= 2 ? v13List[i - 2] : 0;
            var prevWave3_1 = i >= 1 ? wave3List[i - 1] : 0;
            var prevWave3_2 = i >= 2 ? wave3List[i - 2] : 0;
            var prevV14_1 = i >= 1 ? v14List[i - 1] : 0;
            var prevV14_2 = i >= 2 ? v14List[i - 2] : 0;
            var prevWave4_1 = i >= 1 ? wave4List[i - 1] : 0;
            var prevWave4_2 = i >= 2 ? wave4List[i - 2] : 0;
            var prevV15_1 = i >= 1 ? v15List[i - 1] : 0;
            var prevV15_2 = i >= 2 ? v15List[i - 2] : 0;
            var prevWave5_1 = i >= 1 ? wave5List[i - 1] : 0;
            var prevWave5_2 = i >= 2 ? wave5List[i - 2] : 0;
            var prevV16_1 = i >= 1 ? v16List[i - 1] : 0;
            var prevV16_2 = i >= 2 ? v16List[i - 2] : 0;
            var prevWave6_1 = i >= 1 ? wave6List[i - 1] : 0;
            var prevWave6_2 = i >= 2 ? wave6List[i - 2] : 0;

            var v1Neg2 = (0.080778 * (currentValue + (1.907 * prevValue1) + prevValue2)) + (0.293 * prevV1Neg2_1) - (0.063 * prevV1Neg2_2);
            v1Neg2List.Add(v1Neg2);

            var waveNeg2 = v1Neg2 + (0.513 * prevV1Neg2_1) + prevV1Neg2_2 + (0.451 * prevWaveNeg2_1) - (0.481 * prevWaveNeg2_2);
            waveNeg2List.Add(waveNeg2);

            var v1Neg1 = (0.021394 * (currentValue + (1.777 * prevValue1) + prevValue2)) + (0.731 * prevV1Neg1_1) - (0.166 * prevV1Neg1_2);
            v1Neg1List.Add(v1Neg1);

            var waveNeg1 = v1Neg1 + (0.977 * prevV1Neg1_1) + prevV1Neg1_2 + (1.008 * prevWaveNeg1_1) - (0.561 * prevWaveNeg1_2);
            waveNeg1List.Add(waveNeg1);

            var v10 = (0.0095822 * (currentValue + (1.572 * prevValue1) + prevValue2)) + (1.026 * prevV10_1) - (0.282 * prevV10_2);
            v10List.Add(v10);

            var wave0 = v10 + (0.356 * prevV10_1) + prevV10_2 + (1.329 * prevWave0_1) - (0.644 * prevWave0_2);
            wave0List.Add(wave0);

            var v11 = (0.00461 * (currentValue + (1.192 * prevValue1) + prevValue2)) + (1.281 * prevV11_1) - (0.426 * prevV11_2);
            v11List.Add(v11);

            var wave1 = v11 - (0.384 * prevV11_1) + prevV11_2 + (1.565 * prevWave1_1) - (0.729 * prevWave1_2);
            wave1List.Add(wave1);

            var v12 = (0.0026947 * (currentValue + (0.681 * prevValue1) + prevValue2)) + (1.46 * prevV12_1) - (0.543 * prevV12_2);
            v12List.Add(v12);

            var wave2 = v12 - (0.966 * prevV12_1) + prevV12_2 + (1.703 * prevWave2_1) - (0.793 * prevWave2_2);
            wave2List.Add(wave2);

            var v13 = (0.0017362 * (currentValue + (0.012 * prevValue1) + prevValue2)) + (1.606 * prevV13_1) - (0.65 * prevV13_2);
            v13List.Add(v13);

            var wave3 = v13 - (1.408 * prevV13_1) + prevV13_2 + (1.801 * prevWave3_1) - (0.848 * prevWave3_2);
            wave3List.Add(wave3);

            var v14 = (0.0013738 * (currentValue - (0.669 * prevValue1) + prevValue2)) + (1.716 * prevV14_1) - (0.74 * prevV14_2);
            v14List.Add(v14);

            var wave4 = v14 - (1.685 * prevV14_1) + prevV14_2 + (1.866 * prevWave4_1) - (0.89 * prevWave4_2);
            wave4List.Add(wave4);

            var v15 = (0.0010794 * (currentValue - (1.226 * prevValue1) + prevValue2)) + (1.8 * prevV15_1) - (0.811 * prevV15_2);
            v15List.Add(v15);

            var wave5 = v15 - (1.842 * prevV15_1) + prevV15_2 + (1.91 * prevWave5_1) - (0.922 * prevWave5_2);
            wave5List.Add(wave5);

            var v16 = (0.001705 * (currentValue - (1.659 * prevValue1) + prevValue2)) + (1.873 * prevV16_1) - (0.878 * prevV16_2);
            v16List.Add(v16);

            var wave6 = v16 - (1.957 * prevV16_1) + prevV16_2 + (1.946 * prevWave6_1) - (0.951 * prevWave6_2);
            wave6List.Add(wave6);

            var signal = GetCompareSignal(currentValue - waveNeg2, prevValue1 - prevWaveNeg2_1);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Eclpf-2", waveNeg2List },
            { "Eclpf-1", waveNeg1List },
            { "Eclpf0", wave0List },
            { "Eclpf1", wave1List },
            { "Eclpf2", wave2List },
            { "Eclpf3", wave3List },
            { "Eclpf4", wave4List },
            { "Eclpf5", wave5List },
            { "Eclpf6", wave6List }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(waveNeg2List);
        stockData.IndicatorName = IndicatorName.EhlersChebyshevLowPassFilter;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Better Exponential Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersBetterExponentialMovingAverage(this StockData stockData, int length = 20)
    {
        List<double> emaList = new(stockData.Count);
        List<double> bEmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var val = length != 0 ? Math.Cos(2 * Math.PI / length) + Math.Sin(2 * Math.PI / length) : 0;
        var alpha = val != 0 ? MinOrMax((val - 1) / val, 0.99, 0.01) : 0.01;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevEma1 = i >= 1 ? emaList[i - 1] : 0;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var ema = (alpha * currentValue) + ((1 - alpha) * prevEma1);
            emaList.Add(ema);

            var prevBEma = GetLastOrDefault(bEmaList);
            var bEma = (alpha * ((currentValue + prevValue) / 2)) + ((1 - alpha) * prevEma1);
            bEmaList.Add(bEma);

            var signal = GetCompareSignal(currentValue - bEma, prevValue - prevBEma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ebema", bEmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(bEmaList);
        stockData.IndicatorName = IndicatorName.EhlersBetterExponentialMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Hamming Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="pedestal"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersHammingMovingAverage(this StockData stockData, int length = 20, double pedestal = 3)
    {
        List<double> filtList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevFilt = i >= 1 ? filtList[i - 1] : 0;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            double filtSum = 0, coefSum = 0;
            for (var j = 0; j < length; j++)
            {
                var prevV = i >= j ? inputList[i - j] : 0;
                var sine = Math.Sin(pedestal + ((Math.PI - (2 * pedestal)) * ((double)j / (length - 1))));
                filtSum += sine * prevV;
                coefSum += sine;
            }

            var filt = coefSum != 0 ? filtSum / coefSum : 0;
            filtList.Add(filt);

            var signal = GetCompareSignal(currentValue - filt, prevValue - prevFilt);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ehma", filtList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(filtList);
        stockData.IndicatorName = IndicatorName.EhlersHammingMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ehlers Triangle Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersTriangleMovingAverage(this StockData stockData, int length = 20)
    {
        List<double> filtList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var l2 = (double)length / 2;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevFilt = i >= 1 ? filtList[i - 1] : 0;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            double filtSum = 0, coefSum = 0;
            for (var j = 1; j <= length; j++)
            {
                var prevV = i >= j - 1 ? inputList[i - (j - 1)] : 0;
                var c = j < l2 ? j : j > l2 ? length + 1 - j : l2;
                filtSum += c * prevV;
                coefSum += c;
            }

            var filt = coefSum != 0 ? filtSum / coefSum : 0;
            filtList.Add(filt);

            var signal = GetCompareSignal(currentValue - filt, prevValue - prevFilt);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Etma", filtList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(filtList);
        stockData.IndicatorName = IndicatorName.EhlersTriangleMovingAverage;

        return stockData;
    }
}

