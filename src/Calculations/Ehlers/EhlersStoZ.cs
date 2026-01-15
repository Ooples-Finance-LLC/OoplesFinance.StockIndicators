
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Ehlers Simple Decycler
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="upperPct"></param>
    /// <param name="lowerPct"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersSimpleDecycler(this StockData stockData, int length = 125, double upperPct = 0.5, double lowerPct = 0.5)
    {
        length = Math.Max(length, 1);
        List<double> decyclerList = new(stockData.Count);
        List<double> upperBandList = new(stockData.Count);
        List<double> lowerBandList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var hpList = GetCustomValuesListInternal(stockData,
            data => CalculateEhlersHighPassFilterV1(data, length, 1));

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var hp = hpList[i];

            var prevDecycler = GetLastOrDefault(decyclerList);
            var decycler = currentValue - hp;
            decyclerList.Add(decycler);

            var upperBand = (1 + (upperPct / 100)) * decycler;
            upperBandList.Add(upperBand);

            var lowerBand = (1 - (lowerPct / 100)) * decycler;
            lowerBandList.Add(lowerBand);

            var signal = GetCompareSignal(currentValue - decycler, prevValue - prevDecycler);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", upperBandList },
            { "MiddleBand", decyclerList },
            { "LowerBand", lowerBandList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(decyclerList);
        stockData.IndicatorName = IndicatorName.EhlersSimpleDecycler;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Smoothed Adaptive Momentum
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersSmoothedAdaptiveMomentum(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length1 = 5, int length2 = 8)
    {
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 2);
        List<double> f3List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var a1 = Exp(-Math.PI / length2);
        var b1 = 2 * a1 * Math.Cos(1.738 * Math.PI / length2);
        var c1 = Pow(a1, 2);
        var coef2 = b1 + c1;
        var coef3 = -1 * (c1 + (b1 * c1));
        var coef4 = c1 * c1;
        var coef1 = 1 - coef2 - coef3 - coef4;

        var pList = GetOutputValuesInternal(stockData,
            data => CalculateEhlersAdaptiveCyberCycle(data, length1))["Period"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var p = pList[i];
            var prevF3_1 = i >= 1 ? f3List[i - 1] : 0;
            var prevF3_2 = i >= 2 ? f3List[i - 2] : 0;
            var prevF3_3 = i >= 3 ? f3List[i - 3] : 0;
            var pr = (int)Math.Ceiling(Math.Abs(p - 1));
            var prevValue = i >= pr ? inputList[i - pr] : 0;
            var v1 = MinPastValues(i, pr, currentValue - prevValue);

            var f3 = (coef1 * v1) + (coef2 * prevF3_1) + (coef3 * prevF3_2) + (coef4 * prevF3_3);
            f3List.Add(f3);
        }

        var f3EmaList = GetMovingAverageList(stockData, maType, length2, f3List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var f3 = f3List[i];
            var f3Ema = f3EmaList[i];
            var prevF3 = i >= 1 ? f3List[i - 1] : 0;
            var prevF3Ema = i >= 1 ? f3EmaList[i - 1] : 0;

            var signal = GetCompareSignal(f3 - f3Ema, prevF3 - prevF3Ema);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Esam", f3List },
            { "Signal", f3EmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(f3List);
        stockData.IndicatorName = IndicatorName.EhlersSmoothedAdaptiveMomentumIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Stochastic Center Of Gravity Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersStochasticCenterOfGravityOscillator(this StockData stockData, int length = 8)
    {
        length = Math.Max(length, 1);
        List<double> v1List = new(stockData.Count);
        List<double> v2List = new(stockData.Count);
        List<double> tList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var ehlersCGOscillatorList = GetCustomValuesListInternal(stockData,
            data => CalculateEhlersCenterofGravityOscillator(data, length));
        var (highestList, lowestList) = GetMaxAndMinValuesList(ehlersCGOscillatorList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var cg = ehlersCGOscillatorList[i];
            var maxc = highestList[i];
            var minc = lowestList[i];
            var prevV1_1 = i >= 1 ? v1List[i - 1] : 0;
            var prevV1_2 = i >= 2 ? v1List[i - 2] : 0;
            var prevV1_3 = i >= 3 ? v1List[i - 3] : 0;
            var prevV2_1 = i >= 1 ? v2List[i - 1] : 0;
            var prevT1 = i >= 1 ? tList[i - 1] : 0;
            var prevT2 = i >= 2 ? tList[i - 2] : 0;

            var v1 = maxc - minc != 0 ? (cg - minc) / (maxc - minc) : 0;
            v1List.Add(v1);

            var v2_ = ((4 * v1) + (3 * prevV1_1) + (2 * prevV1_2) + prevV1_3) / 10;
            var v2 = 2 * (v2_ - 0.5);
            v2List.Add(v2);

            var t = MinOrMax(0.96 * (prevV2_1 + 0.02), 1, 0);
            tList.Add(t);

            var signal = GetRsiSignal(t - prevT1, prevT1 - prevT2, t, prevT1, 0.8, 0.2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Escog", tList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(tList);
        stockData.IndicatorName = IndicatorName.EhlersStochasticCenterOfGravityOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Simple Cycle Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="alpha"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersSimpleCycleIndicator(this StockData stockData, double alpha = 0.07)
    {
        List<double> smoothList = new(stockData.Count);
        List<double> cycleList = new(stockData.Count);
        List<double> cycle_List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentMedianPrice = inputList[i];
            var prevMedianPrice1 = i >= 1 ? inputList[i - 1] : 0;
            var prevMedianPrice2 = i >= 2 ? inputList[i - 2] : 0;
            var prevMedianPrice3 = i >= 3 ? inputList[i - 3] : 0;
            var prevSmooth1 = GetLastOrDefault(smoothList);
            var prevCycle1 = GetLastOrDefault(cycle_List);
            var prevSmooth2 = i >= 2 ? smoothList[i - 2] : 0;
            var prevCycle2 = i >= 2 ? cycle_List[i - 2] : 0;
            var prevCyc1 = i >= 1 ? cycleList[i - 1] : 0;
            var prevCyc2 = i >= 2 ? cycleList[i - 2] : 0;

            var smooth = (currentMedianPrice + (2 * prevMedianPrice1) + (2 * prevMedianPrice2) + prevMedianPrice3) / 6;
            smoothList.Add(smooth);

            var cycle_ = ((1 - (0.5 * alpha)) * (1 - (0.5 * alpha)) * (smooth - (2 * prevSmooth1) + prevSmooth2)) + (2 * (1 - alpha) * prevCycle1) -
                         ((1 - alpha) * (1 - alpha) * prevCycle2);
            cycle_List.Add(cycle_);

            var cycle = i < 7 ? (currentMedianPrice - (2 * prevMedianPrice1) + prevMedianPrice2) / 4 : cycle_;
            cycleList.Add(cycle);

            var signal = GetCompareSignal(cycle - prevCyc1, prevCyc1 - prevCyc2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Esci", cycleList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(cycleList);
        stockData.IndicatorName = IndicatorName.EhlersSimpleCycleIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Zero Mean Roofing Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersZeroMeanRoofingFilter(this StockData stockData, int length1 = 48, int length2 = 10)
    {
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        List<double> zmrFilterList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var alphaArg = Math.Min(2 * Math.PI / length1, 0.99);
        var alphaCos = Math.Cos(alphaArg);
        var alpha1 = alphaCos != 0 ? (alphaCos + Math.Sin(alphaArg) - 1) / alphaCos : 0;

        var roofingFilterList = GetCustomValuesListInternal(stockData,
            data => CalculateEhlersHpLpRoofingFilter(data, length1, length2));

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentRf = roofingFilterList[i];
            var prevRf = i >= 1 ? roofingFilterList[i - 1] : 0;
            var prevZmrFilt1 = i >= 1 ? zmrFilterList[i - 1] : 0;
            var prevZmrFilt2 = i >= 2 ? zmrFilterList[i - 2] : 0;

            var zmrFilt = ((1 - (alpha1 / 2)) * (currentRf - prevRf)) + ((1 - alpha1) * prevZmrFilt1);
            zmrFilterList.Add(zmrFilt);

            var signal = GetCompareSignal(zmrFilt - prevZmrFilt1, prevZmrFilt1 - prevZmrFilt2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ezmrf", zmrFilterList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(zmrFilterList);
        stockData.IndicatorName = IndicatorName.EhlersZeroMeanRoofingFilter;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Spectrum Derived Filter Bank
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="minLength"></param>
    /// <param name="maxLength"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersSpectrumDerivedFilterBank(this StockData stockData, int minLength = 8, int maxLength = 50, 
        int length1 = 40, int length2 = 10)
    {
        minLength = Math.Max(minLength, 1);
        maxLength = Math.Max(maxLength, minLength);
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        List<double> dcList = new(stockData.Count);
        List<double> domCycList = new(stockData.Count);
        List<double> realList = new(stockData.Count);
        List<double> imagList = new(stockData.Count);
        List<double> q1List = new(stockData.Count);
        List<double> hpList = new(stockData.Count);
        List<double> smoothHpList = new(stockData.Count);
        using var domCycMedian = new RollingMedian(length2);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var twoPiPer = MinOrMax(2 * Math.PI / length1, 0.99, 0.01);
        var alpha1 = (1 - Math.Sin(twoPiPer)) / Math.Cos(twoPiPer);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var delta = Math.Max((-0.015 * i) + 0.5, 0.15);
            var prevHp1 = i >= 1 ? hpList[i - 1] : 0;
            var prevHp2 = i >= 2 ? hpList[i - 2] : 0;
            var prevHp3 = i >= 3 ? hpList[i - 3] : 0;
            var prevHp4 = i >= 4 ? hpList[i - 4] : 0;
            var prevHp5 = i >= 5 ? hpList[i - 5] : 0;

            var hp = i < 7 ? currentValue : (0.5 * (1 + alpha1) * (currentValue - prevValue)) + (alpha1 * prevHp1);
            hpList.Add(hp);

            var prevSmoothHp = GetLastOrDefault(smoothHpList);
            var smoothHp = i < 7 ? currentValue - prevValue : (hp + (2 * prevHp1) + (3 * prevHp2) + (3 * prevHp3) + (2 * prevHp4) + prevHp5) / 12;
            smoothHpList.Add(smoothHp);

            double num = 0, denom = 0, dc = 0, real = 0, imag = 0, q1 = 0, maxAmpl = 0;
            for (var j = minLength; j <= maxLength; j++)
            {
                var beta = Math.Cos(MinOrMax(2 * Math.PI / j, 0.99, 0.01));
                var gamma = 1 / Math.Cos(MinOrMax(4 * Math.PI * delta / j, 0.99, 0.01));
                var alpha = gamma - Sqrt((gamma * gamma) - 1);
                var priorSmoothHp = i >= j ? smoothHpList[i - j] : 0;
                var prevReal = i >= j ? realList[i - j] : 0;
                var priorReal = i >= j * 2 ? realList[i - (j * 2)] : 0;
                var prevImag = i >= j ? imagList[i - j] : 0;
                var priorImag = i >= j * 2 ? imagList[i - (j * 2)] : 0;
                var prevQ1 = i >= j ? q1List[i - j] : 0;

                q1 = j / Math.PI * 2 * (smoothHp - prevSmoothHp);
                real = (0.5 * (1 - alpha) * (smoothHp - priorSmoothHp)) + (beta * (1 + alpha) * prevReal) - (alpha * priorReal);
                imag = (0.5 * (1 - alpha) * (q1 - prevQ1)) + (beta * (1 + alpha) * prevImag) - (alpha * priorImag);
                var ampl = (real * real) + (imag * imag);
                maxAmpl = ampl > maxAmpl ? ampl : maxAmpl;
                var db = maxAmpl != 0 && ampl / maxAmpl > 0 ? -length2 * Math.Log(0.01 / (1 - (0.99 * ampl / maxAmpl))) / Math.Log(length2) : 0;
                db = db > maxLength ? maxLength : db;
                num += db <= 3 ? j * (maxLength - db) : 0;
                denom += db <= 3 ? maxLength - db : 0;
                dc = denom != 0 ? num / denom : 0;
            }
            q1List.Add(q1);
            realList.Add(real);
            imagList.Add(imag);
            dcList.Add(dc);
            domCycMedian.Add(dc);

            var domCyc = domCycMedian.Median;
            domCycList.Add(domCyc);

            var signal = GetCompareSignal(smoothHp, prevSmoothHp);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Esdfb", domCycList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(domCycList);
        stockData.IndicatorName = IndicatorName.EhlersSpectrumDerivedFilterBank;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Trendflex Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersTrendflexIndicator(this StockData stockData, int length = 20)
    {
        length = Math.Max(length, 1);
        List<double> filterList = new(stockData.Count);
        List<double> msList = new(stockData.Count);
        List<double> trendflexList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var period = 0.5 * length;
        var a1 = Exp(-MathHelper.Sqrt2 * Math.PI / period);
        var b1 = 2 * a1 * Math.Cos(MathHelper.Sqrt2 * Math.PI / period);  
        var c2 = b1;
        var c3 = -a1 * a1;
        var c1 = 1 - c2 - c3;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevFilter1 = i >= 1 ? filterList[i - 1] : 0;
            var prevFilter2 = i >= 2 ? filterList[i - 2] : 0;

            var filter = (c1 * ((currentValue + prevValue) / 2)) + (c2 * prevFilter1) + (c3 * prevFilter2);
            filterList.Add(filter);

            double sum = 0;
            for (var j = 1; j <= length; j++)
            {
                var prevFilterCount = i >= j ? filterList[i - j] : 0;
                sum += filter - prevFilterCount;
            }
            sum /= length;

            var prevMs = GetLastOrDefault(msList);
            var ms = (0.04 * Pow(sum, 2)) + (0.96 * prevMs);
            msList.Add(ms);

            var prevTrendflex = GetLastOrDefault(trendflexList);
            var trendflex = ms > 0 ? sum / Sqrt(ms) : 0;
            trendflexList.Add(trendflex);

            var signal = GetCompareSignal(trendflex, prevTrendflex);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Eti", trendflexList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(trendflexList);
        stockData.IndicatorName = IndicatorName.EhlersTrendflexIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Trend Extraction
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="delta"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersTrendExtraction(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 20, double delta = 0.1)
    {
        length = Math.Max(length, 1);
        List<double> bpList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var beta = Math.Cos(MinOrMax(2 * Math.PI / length, 0.99, 0.01));
        var gamma = 1 / Math.Cos(MinOrMax(4 * Math.PI * delta / length, 0.99, 0.01));
        var alpha = MinOrMax(gamma - Sqrt((gamma * gamma) - 1), 0.99, 0.01);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 2 ? inputList[i - 2] : 0;
            var prevBp1 = i >= 1 ? bpList[i - 1] : 0;
            var prevBp2 = i >= 2 ? bpList[i - 2] : 0;

            var bp = (0.5 * (1 - alpha) * MinPastValues(i, 2, currentValue - prevValue)) + (beta * (1 + alpha) * prevBp1) - (alpha * prevBp2);
            bpList.Add(bp);
        }

        var trendList = GetMovingAverageList(stockData, maType, length * 2, bpList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var trend = trendList[i];
            var prevTrend = i >= 1 ? trendList[i - 1] : 0;

            var signal = GetCompareSignal(trend, prevTrend);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Trend", trendList },
            { "Bp", bpList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(trendList);
        stockData.IndicatorName = IndicatorName.EhlersTrendExtraction;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Snake Universal Trading Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="bw"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersSnakeUniversalTradingFilter(this StockData stockData, MovingAvgType maType = MovingAvgType.EhlersHannMovingAverage,
        int length1 = 23, int length2 = 50, double bw = 1.4)
    {
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        List<double> bpList = new(stockData.Count);
        List<double> negRmsList = new(stockData.Count);
        List<double> filtPowList = new(stockData.Count);
        List<double> rmsList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum filtPowSum = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var l1 = Math.Cos(MinOrMax(2 * Math.PI / (2 * length1), 0.99, 0.01));
        var g1 = Math.Cos(MinOrMax(bw * 2 * Math.PI / (2 * length1), 0.99, 0.01));
        var s1 = (1 / g1) - Sqrt(1 / Pow(g1, 2) - 1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 2 ? inputList[i - 2] : 0;
            var prevBp1 = i >= 1 ? bpList[i - 1] : 0;
            var prevBp2 = i >= 2 ? bpList[i - 2] : 0;

            var bp = i < 3 ? 0 : (0.5 * (1 - s1) * (currentValue - prevValue)) + (l1 * (1 + s1) * prevBp1) - (s1 * prevBp2);
            bpList.Add(bp);
        }

        var filtList = GetMovingAverageList(stockData, maType, length1, bpList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var filt = filtList[i];
            var prevFilt1 = i >= 1 ? filtList[i - 1] : 0;
            var prevFilt2 = i >= 2 ? filtList[i - 2] : 0;

            var filtPow = Pow(filt, 2);
            filtPowList.Add(filtPow);
            filtPowSum.Add(filtPow);

            var filtPowMa = filtPowSum.Average(length2);
            var rms = Sqrt(filtPowMa);
            rmsList.Add(rms);

            var negRms = -rms;
            negRmsList.Add(negRms);

            var signal = GetCompareSignal(filt - prevFilt1, prevFilt1 - prevFilt2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", rmsList },
            { "Erf", filtList },
            { "LowerBand", negRmsList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(filtList);
        stockData.IndicatorName = IndicatorName.EhlersSnakeUniversalTradingFilter;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Universal Trading Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="mult"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersUniversalTradingFilter(this StockData stockData, MovingAvgType maType = MovingAvgType.EhlersHannMovingAverage,
        int length1 = 16, int length2 = 50, double mult = 2)
    {
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        List<double> momList = new(stockData.Count);
        List<double> negRmsList = new(stockData.Count);
        List<double> filtPowList = new(stockData.Count);
        List<double> rmsList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum filtPowSum = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var hannLength = (int)Math.Ceiling(mult * length1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var priorValue = i >= hannLength ? inputList[i - hannLength] : 0;

            var mom = currentValue - priorValue;
            momList.Add(mom);
        }

        var filtList = GetMovingAverageList(stockData, maType, length1, momList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var filt = filtList[i];
            var prevFilt1 = i >= 1 ? filtList[i - 1] : 0;
            var prevFilt2 = i >= 2 ? filtList[i - 2] : 0;

            var filtPow = Pow(filt, 2);
            filtPowList.Add(filtPow);
            filtPowSum.Add(filtPow);

            var filtPowMa = filtPowSum.Average(length2);
            var rms = filtPowMa > 0 ? Sqrt(filtPowMa) : 0;
            rmsList.Add(rms);

            var negRms = -rms;
            negRmsList.Add(negRms);

            var signal = GetCompareSignal(filt - prevFilt1, prevFilt1 - prevFilt2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Eutf", filtList },
            { "UpperBand", rmsList },
            { "LowerBand", negRmsList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(filtList);
        stockData.IndicatorName = IndicatorName.EhlersUniversalTradingFilter;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Super Passband Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersSuperPassbandFilter(this StockData stockData, int fastLength = 40, int slowLength = 60, int length1 = 5, int length2 = 50)
    {
        fastLength = Math.Max(fastLength, 1);
        slowLength = Math.Max(slowLength, 1);
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        List<double> espfList = new(stockData.Count);
        List<double> squareList = new(stockData.Count);
        List<double> rmsList = new(stockData.Count);
        List<double> negRmsList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum squareSum = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var a1 = MinOrMax((double)length1 / fastLength, 0.99, 0.01);
        var a2 = MinOrMax((double)length1 / slowLength, 0.99, 0.01);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue1 = i >= 1 ? inputList[i - 1] : 0;
            var prevEspf1 = i >= 1 ? espfList[i - 1] : 0;
            var prevEspf2 = i >= 2 ? espfList[i - 2] : 0;

            var espf = ((a1 - a2) * currentValue) + (((a2 * (1 - a1)) - (a1 * (1 - a2))) * prevValue1) + ((1 - a1 + (1 - a2)) * prevEspf1) - 
                       ((1 - a1) * (1 - a2) * prevEspf2);
            espfList.Add(espf);

            var espfPow = Pow(espf, 2);
            squareList.Add(espfPow);
            squareSum.Add(espfPow);

            var squareAvg = squareSum.Average(length2);
            var prevRms = GetLastOrDefault(rmsList);
            var rms = Sqrt(squareAvg);
            rmsList.Add(rms);

            var prevNegRms = GetLastOrDefault(negRmsList);
            var negRms = -rms;
            negRmsList.Add(negRms);

            var signal = GetBullishBearishSignal(espf - rms, prevEspf1 - prevRms, espf - negRms, prevEspf1 - prevNegRms);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Espf", espfList },
            { "UpperBand", rmsList },
            { "LowerBand", negRmsList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(espfList);
        stockData.IndicatorName = IndicatorName.EhlersSuperPassbandFilter;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Simple Deriv Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersSimpleDerivIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length = 2, int signalLength = 8)
    {
        length = Math.Max(length, 1);
        signalLength = Math.Max(signalLength, 1);
        List<double> derivList = new(stockData.Count);
        List<double> z3List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= length ? inputList[i - length] : 0;
            var prevDeriv1 = i >= 1 ? derivList[i - 1] : 0;
            var prevDeriv2 = i >= 2 ? derivList[i - 2] : 0;
            var prevDeriv3 = i >= 3 ? derivList[i - 3] : 0;

            var deriv = MinPastValues(i, length, currentValue - prevValue);
            derivList.Add(deriv);

            var z3 = deriv + prevDeriv1 + prevDeriv2 + prevDeriv3;
            z3List.Add(z3);
        }

        var z3EmaList = GetMovingAverageList(stockData, maType, signalLength, z3List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var z3Ema = z3EmaList[i];
            var prevZ3Ema = i >= 1 ? z3EmaList[i - 1] : 0;

            var signal = GetCompareSignal(z3Ema, prevZ3Ema);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Esdi", z3List },
            { "Signal", z3EmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(z3List);
        stockData.IndicatorName = IndicatorName.EhlersSimpleDerivIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Simple Clip Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersSimpleClipIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length1 = 2, int length2 = 10, int length3 = 50, int signalLength = 22)
    {
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        length3 = Math.Max(length3, 1);
        signalLength = Math.Max(signalLength, 1);
        List<double> derivList = new(stockData.Count);
        List<double> clipList = new(stockData.Count);
        List<double> z3List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= length1 ? inputList[i - length1] : 0;
            var prevClip1 = i >= 1 ? clipList[i - 1] : 0;
            var prevClip2 = i >= 2 ? clipList[i - 2] : 0;
            var prevClip3 = i >= 3 ? clipList[i - 3] : 0;

            var deriv = MinPastValues(i, length1, currentValue - prevValue);
            derivList.Add(deriv);

            double rms = 0;
            for (var j = 0; j < length3; j++)
            {
                var prevDeriv = i >= j ? derivList[i - j] : 0;
                rms += Pow(prevDeriv, 2);
            }

            var clip = rms != 0 ? MinOrMax(2 * deriv / Sqrt(rms / length3), 1, -1) : 0;
            clipList.Add(clip);

            var z3 = clip + prevClip1 + prevClip2 + prevClip3;
            z3List.Add(z3);
        }

        var z3EmaList = GetMovingAverageList(stockData, maType, signalLength, z3List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var z3Ema = z3EmaList[i];
            var prevZ3Ema = i >= 1 ? z3EmaList[i - 1] : 0;

            var signal = GetCompareSignal(z3Ema, prevZ3Ema);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Esci", z3List },
            { "Signal", z3EmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(z3List);
        stockData.IndicatorName = IndicatorName.EhlersSimpleClipIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Spearman Rank Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersSpearmanRankIndicator(this StockData stockData, int length = 20)
    {
        length = Math.Max(length, 1);
        List<double> sriList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var priceArray = new double[length + 1];
            var rankArray = new double[length + 1];
            for (var j = 1; j <= length; j++)
            {
                var prevPrice = i >= j - 1 ? inputList[i - (j - 1)] : 0;
                priceArray[j] = prevPrice;
                rankArray[j] = j;
            }

            for (var j = 1; j <= length; j++)
            {
                var count = length + 1 - j;

                for (var k = 1; k <= length - count; k++)
                {
                    var array1 = priceArray[k + 1];

                    if (array1 < priceArray[k])
                    {
                        var tempPrice = priceArray[k];
                        var tempRank = rankArray[k];

                        priceArray[k] = array1;
                        rankArray[k] = rankArray[k + 1];
                        priceArray[k + 1] = tempPrice;
                        rankArray[k + 1] = tempRank;
                    }
                }
            }

            double sum = 0;
            for (var j = 1; j <= length; j++)
            {
                sum += Pow(j - rankArray[j], 2);
            }

            var prevSri = GetLastOrDefault(sriList);
            var sri = 2 * (0.5 - (1 - (6 * sum / (length * (Pow(length, 2) - 1)))));
            sriList.Add(sri);

            var signal = GetCompareSignal(sri, prevSri);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Esri", sriList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(sriList);
        stockData.IndicatorName = IndicatorName.EhlersSpearmanRankIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Truncated Bandpass Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="bw"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersTruncatedBandPassFilter(this StockData stockData, int length1 = 20, int length2 = 10, double bw = 0.1)
    {
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        List<double> bptList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var l1 = Math.Cos(MinOrMax(2 * Math.PI / length1, 0.99, 0.01));
        var g1 = Math.Cos(bw * 2 * Math.PI / length1);
        var s1 = (1 / g1) - Sqrt((1 / Pow(g1, 2)) - 1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var trunArray = new double[length2 + 3];
            for (var j = length2; j > 0; j--)
            {
                var prevValue1 = i >= j - 1 ? inputList[i - (j - 1)] : 0;
                var prevValue2 = i >= j + 1 ? inputList[i - (j + 1)] : 0;
                trunArray[j] = (0.5 * (1 - s1) * (prevValue1 - prevValue2)) + (l1 * (1 + s1) * trunArray[j + 1]) - (s1 * trunArray[j + 2]);
            }

            var prevBpt = GetLastOrDefault(bptList);
            var bpt = trunArray[1];
            bptList.Add(bpt);

            var signal = GetCompareSignal(bpt, prevBpt);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Etbpf", bptList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(bptList);
        stockData.IndicatorName = IndicatorName.EhlersTruncatedBandPassFilter;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Squelch Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersSquelchIndicator(this StockData stockData, int length1 = 6, int length2 = 20, int length3 = 40)
    {
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        length3 = Math.Max(length3, 1);
        List<double> phaseList = new(stockData.Count);
        List<double> dPhaseList = new(stockData.Count);
        List<double> dcPeriodList = new(stockData.Count);
        List<double> v1List = new(stockData.Count);
        List<double> ipList = new(stockData.Count);
        List<double> quList = new(stockData.Count);
        List<double> siList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= length1 ? inputList[i - length1] : 0;
            var priorV1 = i >= length1 ? v1List[i - length1] : 0;
            var prevV12 = i >= 2 ? v1List[i - 2] : 0;
            var prevV14 = i >= 4 ? v1List[i - 4] : 0;

            var v1 = MinPastValues(i, length1, currentValue - prevValue);
            v1List.Add(v1);

            var v2 = i >= 3 ? v1List[i - 3] : 0;
            var v3 = (0.75 * (v1 - priorV1)) + (0.25 * (prevV12 - prevV14));
            var prevIp = GetLastOrDefault(ipList);
            var ip = (0.33 * v2) + (0.67 * prevIp);
            ipList.Add(ip);

            var prevQu = GetLastOrDefault(quList);
            var qu = (0.2 * v3) + (0.8 * prevQu);
            quList.Add(qu);

            var prevPhase = GetLastOrDefault(phaseList);
            var phase = Math.Abs(ip + prevIp) > 0 ? Math.Atan(Math.Abs((qu + prevQu) / (ip + prevIp))).ToDegrees() : 0;
            phase = ip < 0 && qu > 0 ? 180 - phase : phase;
            phase = ip < 0 && qu < 0 ? 180 + phase : phase;
            phase = ip > 0 && qu < 0 ? 360 - phase : phase;
            phaseList.Add(phase);

            var dPhase = prevPhase - phase;
            dPhase = prevPhase < 90 && phase > 270 ? 360 + prevPhase - phase : dPhase;
            dPhase = MinOrMax(dPhase, 60, 1);
            dPhaseList.Add(dPhase);

            double instPeriod = 0, v4 = 0;
            for (var j = 0; j <= length3; j++)
            {
                var prevDPhase = i >= j ? dPhaseList[i - j] : 0;
                v4 += prevDPhase;
                instPeriod = v4 > 360 && instPeriod == 0 ? j : instPeriod;
            }

            var prevDcPeriod = GetLastOrDefault(dcPeriodList);
            var dcPeriod = (0.25 * instPeriod) + (0.75 * prevDcPeriod);
            dcPeriodList.Add(dcPeriod);

            double si = dcPeriod < length2 ? 0 : 1;
            siList.Add(si);

            var signal = GetCompareSignal(qu - (-1 * ip), prevQu - (-1 * prevIp));
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Esi", siList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(siList);
        stockData.IndicatorName = IndicatorName.EhlersSquelchIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Signal To Noise Ratio V1
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersSignalToNoiseRatioV1(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length = 7)
    {
        length = Math.Max(length, 1);
        List<double> ampList = new(stockData.Count);
        List<double> v2List = new(stockData.Count);
        List<double> rangeList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        var hilbertOutputs = GetOutputValuesInternal(stockData,
            data => CalculateEhlersHilbertTransformIndicator(data, length: length));
        var inPhaseList = hilbertOutputs["Inphase"];
        var quadList = hilbertOutputs["Quad"];
        var emaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentEma = emaList[i];
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var inPhase = inPhaseList[i];
            var quad = quadList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevEma = i >= 1 ? emaList[i - 1] : 0;

            var prevV2 = GetLastOrDefault(v2List);
            var v2 = (0.2 * ((inPhase * inPhase) + (quad * quad))) + (0.8 * prevV2);
            v2List.Add(v2);

            var prevRange = GetLastOrDefault(rangeList);
            var range = (0.2 * (currentHigh - currentLow)) + (0.8 * prevRange);
            rangeList.Add(range);

            var prevAmp = GetLastOrDefault(ampList);
            var temp = range != 0 ? v2 / (range * range) : 0;
            var logTemp = temp > 0 ? Math.Log10(temp) : 0;
            var amp = range != 0 ? (0.25 * ((10 * logTemp) + 1.9)) + (0.75 * prevAmp) : 0;
            ampList.Add(amp);

            var signal = GetVolatilitySignal(currentValue - currentEma, prevValue - prevEma, amp, 1.9);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Esnr", ampList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(ampList);
        stockData.IndicatorName = IndicatorName.EhlersSignalToNoiseRatioV1;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Triangle Window Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersTriangleWindowIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.EhlersTriangleMovingAverage,
        int length = 20)
    {
        length = Math.Max(length, 1);
        List<double> rocList = new(stockData.Count);
        List<double> derivList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, openList, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentOpen = openList[i];
            var currentValue = inputList[i];
            
            var deriv = currentValue - currentOpen;
            derivList.Add(deriv);
        }

        var filtList = GetMovingAverageList(stockData, maType, length, derivList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var filt = filtList[i];
            var prevFilt1 = i >= 1 ? filtList[i - 1] : 0;
            var prevFilt2 = i >= 2 ? filtList[i - 2] : 0;

            var roc = length / 2 * Math.PI * (filt - prevFilt1);
            rocList.Add(roc);

            var signal = GetCompareSignal(filt - prevFilt1, prevFilt1 - prevFilt2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Etwi", filtList },
            { "Roc", rocList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(filtList);
        stockData.IndicatorName = IndicatorName.EhlersSimpleWindowIndicator;  

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Simple Window Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersSimpleWindowIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 20)
    {
        length = Math.Max(length, 1);
        List<double> rocList = new(stockData.Count);
        List<double> derivList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, openList, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentOpen = openList[i];
            var currentValue = inputList[i];

            var deriv = currentValue - currentOpen;
            derivList.Add(deriv);
        }

        var filtList = GetMovingAverageList(stockData, maType, length, derivList);
        var filtMa1List = GetMovingAverageList(stockData, maType, length, filtList);
        var filtMa2List = GetMovingAverageList(stockData, maType, length, filtMa1List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var filt = filtMa2List[i];
            var prevFilt1 = i >= 1 ? filtMa2List[i - 1] : 0;
            var prevFilt2 = i >= 2 ? filtMa2List[i - 2] : 0;

            var roc = length / 2 * Math.PI * (filt - prevFilt1);
            rocList.Add(roc);

            var signal = GetCompareSignal(filt - prevFilt1, prevFilt1 - prevFilt2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Etwi", filtList },
            { "Roc", rocList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(filtList);
        stockData.IndicatorName = IndicatorName.EhlersTriangleWindowIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Signal To Noise Ratio V2
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersSignalToNoiseRatioV2(this StockData stockData, int length = 6)
    {
        length = Math.Max(length, 1);
        List<double> snrList = new(stockData.Count);
        List<double> rangeList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        var ehlersMamaOutputs = GetOutputValuesInternal(stockData,
            data => CalculateEhlersMotherOfAdaptiveMovingAverages(data));
        var i1List = ehlersMamaOutputs["I1"];
        var q1List = ehlersMamaOutputs["Q1"];
        var mamaList = GetCustomValuesListInternal(stockData,
            data => CalculateEhlersMotherOfAdaptiveMovingAverages(data));

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var prevMama = i >= 1 ? mamaList[i - 1] : 0;
            var i1 = i1List[i];
            var q1 = q1List[i];
            var mama = mamaList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevRange = GetLastOrDefault(rangeList);
            var range = (0.1 * (currentHigh - currentLow)) + (0.9 * prevRange);
            rangeList.Add(range);

            var temp = range != 0 ? ((i1 * i1) + (q1 * q1)) / (range * range) : 0;
            var logTemp = temp > 0 ? Math.Log10(temp) : 0;
            var prevSnr = GetLastOrDefault(snrList);
            var snr = range > 0 ? (0.25 * ((10 * logTemp) + length)) + (0.75 * prevSnr) : 0;
            snrList.Add(snr);

            var signal = GetVolatilitySignal(currentValue - mama, prevValue - prevMama, snr, length);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Esnr", snrList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(snrList);
        stockData.IndicatorName = IndicatorName.EhlersSignalToNoiseRatioV2;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Voss Predictive Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="predict"></param>
    /// <param name="bw"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersVossPredictiveFilter(this StockData stockData, int length = 20, double predict = 3, double bw = 0.25)
    {
        length = Math.Max(length, 1);
        List<double> filtList = new(stockData.Count);
        List<double> vossList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var order = MinOrMax((int)Math.Ceiling(3 * predict));
        var f1 = Math.Cos(2 * Math.PI / length);
        var g1 = Math.Cos(bw * 2 * Math.PI / length);
        var s1 = (1 / g1) - Sqrt((1 / (g1 * g1)) - 1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevFilt1 = i >= 1 ? filtList[i - 1] : 0;
            var prevFilt2 = i >= 2 ? filtList[i - 2] : 0;
            var prevValue = i >= 2 ? inputList[i - 2] : 0;

            var filt = i <= 5 ? 0 : (0.5 * (1 - s1) * (currentValue - prevValue)) + (f1 * (1 + s1) * prevFilt1) - (s1 * prevFilt2);
            filtList.Add(filt);

            double sumC = 0;
            for (var j = 0; j <= order - 1; j++)
            {
                var prevVoss = i >= order - j ? vossList[i - (order - j)] : 0;
                sumC += (double)(j + 1) / order * prevVoss;
            }

            var prevvoss = GetLastOrDefault(vossList);
            var voss = ((double)(3 + order) / 2 * filt) - sumC;
            vossList.Add(voss);

            var signal = GetCompareSignal(voss - filt, prevvoss - prevFilt1);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Voss", vossList },
            { "Filt", filtList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.EhlersVossPredictiveFilter;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Swiss Army Knife Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="delta"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersSwissArmyKnifeIndicator(this StockData stockData, int length = 20, double delta = 0.1)
    {
        length = Math.Max(length, 1);
        List<double> emaFilterList = new(stockData.Count);
        List<double> smaFilterList = new(stockData.Count);
        List<double> gaussFilterList = new(stockData.Count);
        List<double> butterFilterList = new(stockData.Count);
        List<double> smoothFilterList = new(stockData.Count);
        List<double> hpFilterList = new(stockData.Count);
        List<double> php2FilterList = new(stockData.Count);
        List<double> bpFilterList = new(stockData.Count);
        List<double> bsFilterList = new(stockData.Count);
        List<double> filterAvgList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var twoPiPrd = MinOrMax(2 * Math.PI / length, 0.99, 0.01);
        var deltaPrd = MinOrMax(2 * Math.PI * 2 * delta / length, 0.99, 0.01);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevPrice1 = i >= 1 ? inputList[i - 1] : 0;
            var prevPrice2 = i >= 2 ? inputList[i - 2] : 0;
            var prevPrice = i >= length ? inputList[i - length] : 0;
            var prevEmaFilter1 = GetLastOrDefault(emaFilterList);
            var prevSmaFilter1 = GetLastOrDefault(smaFilterList);
            var prevGaussFilter1 = GetLastOrDefault(gaussFilterList);
            var prevButterFilter1 = GetLastOrDefault(butterFilterList);
            var prevSmoothFilter1 = GetLastOrDefault(smoothFilterList);
            var prevHpFilter1 = GetLastOrDefault(hpFilterList);
            var prevPhp2Filter1 = GetLastOrDefault(php2FilterList);
            var prevBpFilter1 = GetLastOrDefault(bpFilterList);
            var prevBsFilter1 = GetLastOrDefault(bsFilterList);
            var prevEmaFilter2 = i >= 2 ? emaFilterList[i - 2] : 0;
            var prevSmaFilter2 = i >= 2 ? smaFilterList[i - 2] : 0;
            var prevGaussFilter2 = i >= 2 ? gaussFilterList[i - 2] : 0;
            var prevButterFilter2 = i >= 2 ? butterFilterList[i - 2] : 0;
            var prevSmoothFilter2 = i >= 2 ? smoothFilterList[i - 2] : 0;
            var prevHpFilter2 = i >= 2 ? hpFilterList[i - 2] : 0;
            var prevPhp2Filter2 = i >= 2 ? php2FilterList[i - 2] : 0;
            var prevBpFilter2 = i >= 2 ? bpFilterList[i - 2] : 0;
            var prevBsFilter2 = i >= 2 ? bsFilterList[i - 2] : 0;
            double alpha = (Math.Cos(twoPiPrd) + Math.Sin(twoPiPrd) - 1) / Math.Cos(twoPiPrd), c0 = 1, c1 = 0, b0 = alpha, b1 = 0, b2 = 0, a1 = 1 - alpha, a2 = 0;

            var emaFilter = i <= length ? currentValue :
                (c0 * ((b0 * currentValue) + (b1 * prevPrice1) + (b2 * prevPrice2))) + (a1 * prevEmaFilter1) + (a2 * prevEmaFilter2) - (c1 * prevPrice);
            emaFilterList.Add(emaFilter);

            var n = length; c0 = 1; c1 = (double)1 / n; b0 = (double)1 / n; b1 = 0; b2 = 0; a1 = 1; a2 = 0;
            var smaFilter = i <= length ? currentValue :
                (c0 * ((b0 * currentValue) + (b1 * prevPrice1) + (b2 * prevPrice2))) + (a1 * prevSmaFilter1) + (a2 * prevSmaFilter2) - (c1 * prevPrice);
            smaFilterList.Add(smaFilter);

            double beta = 2.415 * (1 - Math.Cos(twoPiPrd)), sqrtData = Pow(beta, 2) + (2 * beta), sqrt = Sqrt(sqrtData); alpha = (-1 * beta) + sqrt;
            c0 = Pow(alpha, 2); c1 = 0; b0 = 1; b1 = 0; b2 = 0; a1 = 2 * (1 - alpha); a2 = -(1 - alpha) * (1 - alpha);
            var gaussFilter = i <= length ? currentValue :
                (c0 * ((b0 * currentValue) + (b1 * prevPrice1) + (b2 * prevPrice2))) + (a1 * prevGaussFilter1) + (a2 * prevGaussFilter2) - (c1 * prevPrice);
            gaussFilterList.Add(gaussFilter);

            beta = 2.415 * (1 - Math.Cos(twoPiPrd)); sqrtData = (beta * beta) + (2 * beta); sqrt = sqrtData >= 0 ? Sqrt(sqrtData) : 0; alpha = (-1 * beta) + sqrt;
            c0 = Pow(alpha, 2) / 4; c1 = 0; b0 = 1; b1 = 2; b2 = 1; a1 = 2 * (1 - alpha); a2 = -(1 - alpha) * (1 - alpha);
            var butterFilter = i <= length ? currentValue :
                (c0 * ((b0 * currentValue) + (b1 * prevPrice1) + (b2 * prevPrice2))) + (a1 * prevButterFilter1) + (a2 * prevButterFilter2) - (c1 * prevPrice);
            butterFilterList.Add(butterFilter);

            c0 = (double)1 / 4; c1 = 0; b0 = 1; b1 = 2; b2 = 1; a1 = 0; a2 = 0;
            var smoothFilter = (c0 * ((b0 * currentValue) + (b1 * prevPrice1) + (b2 * prevPrice2))) + (a1 * prevSmoothFilter1) + 
                (a2 * prevSmoothFilter2) - (c1 * prevPrice);
            smoothFilterList.Add(smoothFilter);

            alpha = (Math.Cos(twoPiPrd) + Math.Sin(twoPiPrd) - 1) / Math.Cos(twoPiPrd); c0 = 1 - (alpha / 2); c1 = 0; b0 = 1; b1 = -1; b2 = 0; a1 = 1 - alpha; a2 = 0;
            var hpFilter = i <= length ? 0 :
                (c0 * ((b0 * currentValue) + (b1 * prevPrice1) + (b2 * prevPrice2))) + (a1 * prevHpFilter1) + (a2 * prevHpFilter2) - (c1 * prevPrice);
            hpFilterList.Add(hpFilter);

            beta = 2.415 * (1 - Math.Cos(twoPiPrd)); sqrtData = Pow(beta, 2) + (2 * beta); sqrt = sqrtData >= 0 ? Sqrt(sqrtData) : 0; alpha = (-1 * beta) + sqrt; 
            c0 = (1 - (alpha / 2)) * (1 - (alpha / 2)); c1 = 0; b0 = 1; b1 = -2; b2 = 1; a1 = 2 * (1 - alpha); a2 = -(1 - alpha) * (1 - alpha);
            var php2Filter = i <= length ? 0 :
                (c0 * ((b0 * currentValue) + (b1 * prevPrice1) + (b2 * prevPrice2))) + (a1 * prevPhp2Filter1) + (a2 * prevPhp2Filter2) - (c1 * prevPrice);
            php2FilterList.Add(php2Filter);

            beta = Math.Cos(twoPiPrd); var gamma = 1 / Math.Cos(deltaPrd); sqrtData = Pow(gamma, 2) - 1; sqrt = Sqrt(sqrtData);
            alpha = gamma - sqrt; c0 = (1 - alpha) / 2; c1 = 0; b0 = 1; b1 = 0; b2 = -1; a1 = beta * (1 + alpha); a2 = alpha * -1;
            var bpFilter = i <= length ? currentValue :
                (c0 * ((b0 * currentValue) + (b1 * prevPrice1) + (b2 * prevPrice2))) + (a1 * prevBpFilter1) + (a2 * prevBpFilter2) - (c1 * prevPrice);
            bpFilterList.Add(bpFilter);

            beta = Math.Cos(twoPiPrd); gamma = 1 / Math.Cos(deltaPrd); sqrtData = Pow(gamma, 2) - 1; sqrt = sqrtData >= 0 ? Sqrt(sqrtData) : 0;
            alpha = gamma - sqrt; c0 = (1 + alpha) / 2; c1 = 0; b0 = 1; b1 = -2 * beta; b2 = 1; a1 = beta * (1 + alpha); a2 = alpha * -1;
            var bsFilter = i <= length ? currentValue :
                (c0 * ((b0 * currentValue) + (b1 * prevPrice1) + (b2 * prevPrice2))) + (a1 * prevBsFilter1) + (a2 * prevBsFilter2) - (c1 * prevPrice);
            bsFilterList.Add(bsFilter);

            var signal = GetCompareSignal(smaFilter - prevSmaFilter1, prevSmaFilter1 - prevSmaFilter2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "EmaFilter", emaFilterList },
            { "SmaFilter", smaFilterList },
            { "GaussFilter", gaussFilterList },
            { "ButterFilter", butterFilterList },
            { "SmoothFilter", smoothFilterList },
            { "HpFilter", hpFilterList },
            { "PhpFilter", php2FilterList },
            { "BpFilter", bpFilterList },
            { "BsFilter", bsFilterList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(smaFilterList);
        stockData.IndicatorName = IndicatorName.EhlersSwissArmyKnifeIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Universal Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersUniversalOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 20, int signalLength = 9)
    {
        length = Math.Max(length, 1);
        signalLength = Math.Max(signalLength, 1);
        List<double> euoList = new(stockData.Count);
        List<double> whitenoiseList = new(stockData.Count);
        List<double> filtList = new(stockData.Count);
        List<double> pkList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var a1 = Exp(-MinOrMax(1.414 * Math.PI / length, 0.99, 0.01));
        var b1 = 2 * a1 * Math.Cos(1.414 * Math.PI / length);
        var c2 = b1;
        var c3 = -a1 * a1;
        var c1 = 1 - c2 - c3;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 2 ? inputList[i - 2] : 0;
            var prevFilt2 = i >= 2 ? filtList[i - 2] : 0;

            var prevWhitenoise = GetLastOrDefault(whitenoiseList);
            var whitenoise = MinPastValues(i, 2, currentValue - prevValue) / 2;
            whitenoiseList.Add(whitenoise);

            var prevFilt1 = GetLastOrDefault(filtList);
            var filt = (c1 * ((whitenoise + prevWhitenoise) / 2)) + (c2 * prevFilt1) + (c3 * prevFilt2);
            filtList.Add(filt);

            var prevPk = GetLastOrDefault(pkList);
            var pk = Math.Abs(filt) > prevPk ? Math.Abs(filt) : 0.991 * prevPk;
            pkList.Add(pk);

            var denom = pk == 0 ? -1 : pk;
            var prevEuo = GetLastOrDefault(euoList);
            var euo = denom == -1 ? prevEuo : pk != 0 ? filt / pk : 0;
            euoList.Add(euo);
        }

        var euoMaList = GetMovingAverageList(stockData, maType, signalLength, euoList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var euo = euoList[i];
            var euoMa = euoMaList[i];
            var prevEuo = i >= 1 ? euoList[i - 1] : 0;
            var prevEuoMa = i >= 1 ? euoMaList[i - 1] : 0;

            var signal = GetCompareSignal(euo - euoMa, prevEuo - prevEuoMa);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Euo", euoList },
            { "Signal", euoMaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(euoList);
        stockData.IndicatorName = IndicatorName.EhlersUniversalOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Zero Crossings Dominant Cycle
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="bw"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersZeroCrossingsDominantCycle(this StockData stockData, int length = 20, double bw = 0.7)
    {
        length = Math.Max(length, 1);
        List<double> dcList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var counter = 0;

        var ebpfOutputs = GetOutputValuesInternal(stockData,
            data => CalculateEhlersBandPassFilterV1(data, length, bw));
        var realList = ebpfOutputs["Ebpf"];
        var triggerList = ebpfOutputs["Signal"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var real = realList[i];
            var trigger = triggerList[i];
            var prevReal = i >= 1 ? realList[i - 1] : 0;
            var prevTrigger = i >= 1 ? triggerList[i - 1] : 0;

            var prevDc = GetLastOrDefault(dcList);
            var dc = Math.Max(prevDc, 6);
            counter += 1;
            if ((real > 0 && prevReal <= 0) || (real < 0 && prevReal >= 0))
            {
                dc = MinOrMax(2 * counter, 1.25 * prevDc, 0.8 * prevDc);
                counter = 0;
            }
            dcList.Add(dc);

            var signal = GetCompareSignal(real - trigger, prevReal - prevTrigger);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ezcdc", dcList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(dcList);
        stockData.IndicatorName = IndicatorName.EhlersZeroCrossingsDominantCycle;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Stochastic Cyber Cycle
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="alpha"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersStochasticCyberCycle(this StockData stockData, int length = 14, double alpha = 0.7)
    {
        length = Math.Max(length, 1);
        List<double> stochList = new(stockData.Count);
        List<double> stochCCList = new(stockData.Count);
        List<double> triggerList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var cyberCycleList = GetCustomValuesListInternal(stockData,
            data => CalculateEhlersCyberCycle(data, alpha));
        var (maxCycleList, minCycleList) = GetMaxAndMinValuesList(cyberCycleList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevStoch1 = i >= 1 ? stochList[i - 1] : 0;
            var prevStoch2 = i >= 2 ? stochList[i - 2] : 0;
            var prevStoch3 = i >= 3 ? stochList[i - 3] : 0;
            var cycle = cyberCycleList[i];
            var maxCycle = maxCycleList[i];
            var minCycle = minCycleList[i];

            var stoch = maxCycle - minCycle != 0 ? MinOrMax((cycle - minCycle) / (maxCycle - minCycle), 1, 0) : 0;
            stochList.Add(stoch);

            var prevStochCC = GetLastOrDefault(stochCCList);
            var stochCC = MinOrMax(2 * ((((4 * stoch) + (3 * prevStoch1) + (2 * prevStoch2) + prevStoch3) / 10) - 0.5), 1, -1);
            stochCCList.Add(stochCC);

            var prevTrigger = GetLastOrDefault(triggerList);
            var trigger = MinOrMax(0.96 * (prevStochCC + 0.02), 1, -1);
            triggerList.Add(trigger);

            var signal = GetRsiSignal(stochCC - trigger, prevStochCC - prevTrigger, stochCC, prevStochCC, 0.5, -0.5);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Escc", stochCCList },
            { "Signal", triggerList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(stochCCList);
        stockData.IndicatorName = IndicatorName.EhlersStochasticCyberCycle;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Stochastic
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersStochastic(this StockData stockData, MovingAvgType maType = MovingAvgType.Ehlers2PoleSuperSmootherFilterV1, 
        int length1 = 48, int length2 = 20, int length3 = 10)
    {
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        length3 = Math.Max(length3, 1);
        List<double> stoch2PoleList = new(stockData.Count);
        List<double> arg2PoleList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var roofingFilter2PoleList = GetCustomValuesListInternal(stockData,
            data => CalculateEhlersRoofingFilterV1(data, maType, length1, length3));
        var (max2PoleList, min2PoleList) = GetMaxAndMinValuesList(roofingFilter2PoleList, length2);

        for (var i = 0; i < stockData.Count; i++)
        {
            var rf2Pole = roofingFilter2PoleList[i];
            var min2Pole = min2PoleList[i];
            var max2Pole = max2PoleList[i];

            var prevStoch2Pole = GetLastOrDefault(stoch2PoleList);
            var stoch2Pole = max2Pole - min2Pole != 0 ? MinOrMax((rf2Pole - min2Pole) / (max2Pole - min2Pole), 1, 0) : 0;
            stoch2PoleList.Add(stoch2Pole);

            var arg2Pole = (stoch2Pole + prevStoch2Pole) / 2;
            arg2PoleList.Add(arg2Pole);
        }

        var estoch2PoleList = GetMovingAverageList(stockData, maType, length2, arg2PoleList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var estoch2Pole = estoch2PoleList[i];
            var prevEstoch2Pole1 = i >= 1 ? estoch2PoleList[i - 1] : 0;
            var prevEstoch2Pole2 = i >= 2 ? estoch2PoleList[i - 2] : 0;

            var signal = GetRsiSignal(estoch2Pole - prevEstoch2Pole1, prevEstoch2Pole1 - prevEstoch2Pole2, estoch2Pole, prevEstoch2Pole1, 0.8, 0.2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Es", estoch2PoleList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(estoch2PoleList);
        stockData.IndicatorName = IndicatorName.EhlersStochastic;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Triple Delay Line Detrender
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersTripleDelayLineDetrender(this StockData stockData, MovingAvgType maType = MovingAvgType.EhlersModifiedOptimumEllipticFilter, 
        int length = 14)
    {
        length = Math.Max(length, 1);
        List<double> tmp1List = new(stockData.Count);
        List<double> tmp2List = new(stockData.Count);
        List<double> detrenderList = new(stockData.Count);
        List<double> histList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevTmp1_6 = i >= 6 ? tmp1List[i - 6] : 0;
            var prevTmp2_6 = i >= 6 ? tmp2List[i - 6] : 0;
            var prevTmp2_12 = i >= 12 ? tmp2List[i - 12] : 0;

            var tmp1 = currentValue + (0.088 * prevTmp1_6);
            tmp1List.Add(tmp1);

            var tmp2 = tmp1 - prevTmp1_6 + (1.2 * prevTmp2_6) - (0.7 * prevTmp2_12);
            tmp2List.Add(tmp2);

            var detrender = prevTmp2_12 - (2 * prevTmp2_6) + tmp2;
            detrenderList.Add(detrender);
        }

        var tdldList = GetMovingAverageList(stockData, maType, length, detrenderList);
        var tdldSignalList = GetMovingAverageList(stockData, maType, length, tdldList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var tdld = tdldList[i];
            var tdldSignal = tdldSignalList[i];

            var prevHist = GetLastOrDefault(histList);
            var hist = tdld - tdldSignal;
            histList.Add(hist);

            var signal = GetCompareSignal(hist, prevHist);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Etdld", tdldList },
            { "Signal", tdldSignalList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(tdldList);
        stockData.IndicatorName = IndicatorName.EhlersTripleDelayLineDetrender;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Sine Wave Indicator V1
    /// </summary>
    /// <param name="stockData"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersSineWaveIndicatorV1(this StockData stockData)
    {
        List<double> sineList = new(stockData.Count);
        List<double> leadSineList = new(stockData.Count);
        List<double> dcPhaseList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var ehlersMamaOutputs = GetOutputValuesInternal(stockData,
            data => CalculateEhlersMotherOfAdaptiveMovingAverages(data));
        var spList = ehlersMamaOutputs["SmoothPeriod"];
        var smoothList = ehlersMamaOutputs["Smooth"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var sp = spList[i];
            var dcPeriod = (int)Math.Ceiling(sp + 0.5);

            double realPart = 0, imagPart = 0;
            for (var j = 0; j <= dcPeriod - 1; j++)
            {
                var prevSmooth = i >= j ? smoothList[i - j] : 0;
                realPart += Math.Sin(MinOrMax(2 * Math.PI * ((double)j / dcPeriod), 0.99, 0.01)) * prevSmooth;
                imagPart += Math.Cos(MinOrMax(2 * Math.PI * ((double)j / dcPeriod), 0.99, 0.01)) * prevSmooth;
            }

            var dcPhase = Math.Abs(imagPart) > 0.001 ? Math.Atan(realPart / imagPart).ToDegrees() : 90 * Math.Sign(realPart);
            dcPhase += 90;
            dcPhase += sp != 0 ? 360 / sp : 0;
            dcPhase += imagPart < 0 ? 180 : 0;
            dcPhase -= dcPhase > 315 ? 360 : 0;
            dcPhaseList.Add(dcPhase);

            var prevSine = GetLastOrDefault(sineList);
            var sine = Math.Sin(dcPhase.ToRadians());
            sineList.Add(sine);

            var prevLeadSine = GetLastOrDefault(leadSineList);
            var leadSine = Math.Sin((dcPhase + 45).ToRadians());
            leadSineList.Add(leadSine);

            var signal = GetCompareSignal(sine - leadSine, prevSine - prevLeadSine);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Sine", sineList },
            { "LeadSine", leadSineList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(sineList);
        stockData.IndicatorName = IndicatorName.EhlersSineWaveIndicatorV1;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ehlers Sine Wave Indicator V2
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="alpha"></param>
    /// <returns></returns>
    public static StockData CalculateEhlersSineWaveIndicatorV2(this StockData stockData, int length = 5, double alpha = 0.07)
    {
        length = Math.Max(length, 1);
        List<double> sineList = new(stockData.Count);
        List<double> leadSineList = new(stockData.Count);
        List<double> dcPhaseList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var periodList = GetOutputValuesInternal(stockData,
            data => CalculateEhlersAdaptiveCyberCycle(data, length, alpha))["Period"];
        var cycleList = GetCustomValuesListInternal(stockData,
            data => CalculateEhlersCyberCycle(data));

        for (var i = 0; i < stockData.Count; i++)
        {
            var period = periodList[i];
            var dcPeriod = (int)Math.Ceiling(period);

            double realPart = 0, imagPart = 0;
            for (var j = 0; j <= dcPeriod - 1; j++)
            {
                var prevCycle = i >= j ? cycleList[i - j] : 0;
                realPart += Math.Sin(MinOrMax(2 * Math.PI * ((double)j / dcPeriod), 0.99, 0.01)) * prevCycle;
                imagPart += Math.Cos(MinOrMax(2 * Math.PI * ((double)j / dcPeriod), 0.99, 0.01)) * prevCycle;
            }

            var dcPhase = Math.Abs(imagPart) > 0.001 ? Math.Atan(realPart / imagPart).ToDegrees() : 90 * Math.Sign(realPart);
            dcPhase += 90;
            dcPhase += imagPart < 0 ? 180 : 0;
            dcPhase -= dcPhase > 315 ? 360 : 0;
            dcPhaseList.Add(dcPhase);

            var prevSine = GetLastOrDefault(sineList);
            var sine = Math.Sin(dcPhase.ToRadians());
            sineList.Add(sine);

            var prevLeadSine = GetLastOrDefault(leadSineList);
            var leadSine = Math.Sin((dcPhase + 45).ToRadians());
            leadSineList.Add(leadSine);

            var signal = GetCompareSignal(sine - leadSine, prevSine - prevLeadSine);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Sine", sineList },
            { "LeadSine", leadSineList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(sineList);
        stockData.IndicatorName = IndicatorName.EhlersSineWaveIndicatorV2;

        return stockData;
    }

}

