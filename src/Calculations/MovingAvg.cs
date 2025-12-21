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
    /// Calculates the simple moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateSimpleMovingAverage(this StockData stockData, int length = 14)
    {
        List<double> smaList = new();
        List<double> tempList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentValue = inputList[i];
            tempList.AddRounded(currentValue);

            var prevSma = smaList.LastOrDefault();
            var sma = tempList.Count >= length ? tempList.TakeLastExt(length).Average() : 0;
            smaList.AddRounded(sma);

            var signal = GetCompareSignal(currentValue - sma, prevValue - prevSma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Sma", smaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = smaList;
        stockData.IndicatorName = IndicatorName.SimpleMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the weighted moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateWeightedMovingAverage(this StockData stockData, int length = 14)
    {
        List<double> wmaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            double sum = 0, weightedSum = 0;
            for (var j = 0; j < length; j++)
            {
                double weight = length - j;
                var prevValue = i >= j ? inputList[i - j] : 0;

                sum += prevValue * weight;
                weightedSum += weight;
            }

            var prevWma = wmaList.LastOrDefault();
            var wma = weightedSum != 0 ? sum / weightedSum : 0;
            wmaList.AddRounded(wma);

            var signal = GetCompareSignal(currentValue - wma, prevVal - prevWma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Wma", wmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = wmaList;
        stockData.IndicatorName = IndicatorName.WeightedMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the exponential moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateExponentialMovingAverage(this StockData stockData, int length = 14)
    {
        List<double> emaList = new();
        List<double> tempList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            tempList.AddRounded(currentValue);
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevEma = emaList.LastOrDefault();
            var ema = i < length ? tempList.Average() : CalculateEMA(currentValue, prevEma, length);
            emaList.AddRounded(ema);

            var signal = GetCompareSignal(currentValue - ema, prevValue - prevEma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ema", emaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = emaList;
        stockData.IndicatorName = IndicatorName.ExponentialMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the triangular moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateTriangularMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 20)
    {
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var sma1List = GetMovingAverageList(stockData, maType, length, inputList);
        var tmaList = GetMovingAverageList(stockData, maType, length, sma1List);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var tma = tmaList[i];
            var prevTma = i >= 1 ? tmaList[i - 1] : 0;

            var signal = GetCompareSignal(currentValue - tma, prevValue - prevTma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Tma", tmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = tmaList;
        stockData.IndicatorName = IndicatorName.TriangularMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the hull moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateHullMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length = 20)
    {
        List<double> totalWeightedMAList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var length2 = MinOrMax((int)Math.Round((double)length / 2));
        var sqrtLength = MinOrMax((int)Math.Round(Sqrt(length)));

        var wma1List = GetMovingAverageList(stockData, maType, length, inputList);
        var wma2List = GetMovingAverageList(stockData, maType, length2, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentWMA1 = wma1List[i];
            var currentWMA2 = wma2List[i];

            var totalWeightedMA = (2 * currentWMA2) - currentWMA1;
            totalWeightedMAList.AddRounded(totalWeightedMA);
        }

        var hullMAList = GetMovingAverageList(stockData, maType, sqrtLength, totalWeightedMAList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var hullMa = hullMAList[i];
            var prevHullMa = i >= 1 ? inputList[i - 1] : 0;

            var signal = GetCompareSignal(currentValue - hullMa, prevValue - prevHullMa);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Hma", hullMAList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = hullMAList;
        stockData.IndicatorName = IndicatorName.HullMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the kaufman adaptive moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <param name="fastLength">Length of the fast.</param>
    /// <param name="slowLength">Length of the slow.</param>
    /// <returns></returns>
    public static StockData CalculateKaufmanAdaptiveMovingAverage(this StockData stockData, int length = 10, int fastLength = 2, int slowLength = 30)
    {
        List<double> volatilityList = new();
        List<double> erList = new();
        List<double> kamaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var fastAlpha = (double)2 / (fastLength + 1);
        var slowAlpha = (double)2 / (slowLength + 1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var priorValue = i >= length ? inputList[i - length] : 0;

            var volatility = Math.Abs(MinPastValues(i, 1, currentValue - prevValue));
            volatilityList.AddRounded(volatility);

            var volatilitySum = volatilityList.TakeLastExt(length).Sum();
            var momentum = Math.Abs(MinPastValues(i, length, currentValue - priorValue));

            var efficiencyRatio = volatilitySum != 0 ? momentum / volatilitySum : 0;
            erList.AddRounded(efficiencyRatio);

            var sc = Pow((efficiencyRatio * (fastAlpha - slowAlpha)) + slowAlpha, 2);
            var prevKama = kamaList.LastOrDefault();
            var currentKAMA = (sc * currentValue) + ((1 - sc) * prevKama);
            kamaList.AddRounded(currentKAMA);

            var signal = GetCompareSignal(currentValue - currentKAMA, prevValue - prevKama);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Er", erList },
            { "Kama", kamaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = kamaList;
        stockData.IndicatorName = IndicatorName.KaufmanAdaptiveMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the arnaud legoux moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="sigma">The sigma.</param>
    /// <returns></returns>
    public static StockData CalculateArnaudLegouxMovingAverage(this StockData stockData, int length = 9, double offset = 0.85, int sigma = 6)
    {
        List<double> almaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var m = offset * (length - 1);
        var s = (double)length / sigma;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            double sum = 0, weightedSum = 0;
            for (var j = 0; j <= length - 1; j++)
            {
                var weight = s != 0 ? Exp(-1 * Pow(j - m, 2) / (2 * Pow(s, 2))) : 0;
                var prevValue = i >= length - 1 - j ? inputList[i - (length - 1 - j)] : 0;

                sum += prevValue * weight;
                weightedSum += weight;
            }

            var prevAlma = almaList.LastOrDefault();
            var alma = weightedSum != 0 ? sum / weightedSum : 0;
            almaList.AddRounded(alma);

            var signal = GetCompareSignal(currentValue - alma, prevVal - prevAlma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Alma", almaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = almaList;
        stockData.IndicatorName = IndicatorName.ArnaudLegouxMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the end point moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <param name="offset">The offset.</param>
    /// <returns></returns>
    public static StockData CalculateEndPointMovingAverage(this StockData stockData, int length = 11, int offset = 4)
    {
        List<double> epmaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            double sum = 0, weightedSum = 0;
            for (var j = 0; j <= length - 1; j++)
            {
                double weight = length - j - offset;
                var prevValue = i >= j ? inputList[i - j] : 0;

                sum += prevValue * weight;
                weightedSum += weight;
            }

            var prevEpma = epmaList.LastOrDefault();
            var epma = weightedSum != 0 ? 1 / weightedSum * sum : 0;
            epmaList.AddRounded(epma);

            var signal = GetCompareSignal(currentValue - epma, prevVal - prevEpma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Epma", epmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = epmaList;
        stockData.IndicatorName = IndicatorName.EndPointMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the least squares moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateLeastSquaresMovingAverage(this StockData stockData, int length = 25)
    {
        List<double> lsmaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var wmaList = CalculateWeightedMovingAverage(stockData, length).CustomValuesList;
        var smaList = CalculateSimpleMovingAverage(stockData, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentWma = wmaList[i];
            var currentSma = smaList[i];

            var prevLsma = lsmaList.LastOrDefault();
            var lsma = (3 * currentWma) - (2 * currentSma);
            lsmaList.AddRounded(lsma);

            var signal = GetCompareSignal(currentValue - lsma, prevValue - prevLsma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Lsma", lsmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = lsmaList;
        stockData.IndicatorName = IndicatorName.LeastSquaresMovingAverage;

        return stockData;
    }

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

        List<double> famaList = new();
        List<double> mamaList = new();
        List<double> i2List = new();
        List<double> q2List = new();
        List<double> reList = new();
        List<double> imList = new();
        List<double> sPrdList = new();
        List<double> phaseList = new();
        List<double> periodList = new();
        List<double> smoothList = new();
        List<double> detList = new();
        List<double> q1List = new();
        List<double> i1List = new();
        List<Signal> signalsList = new();
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
            smoothList.AddRounded(smooth);

            var det = ((HilbertTransformCoeff1 * smooth) + (HilbertTransformCoeff2 * prevs2) - (HilbertTransformCoeff2 * prevs4) - (HilbertTransformCoeff1 * prevs6)) * ((PeriodCorrectionFactor * prevPeriod) + PeriodCorrectionOffset);
            detList.AddRounded(det);

            var q1 = ((HilbertTransformCoeff1 * det) + (HilbertTransformCoeff2 * prevd2) - (HilbertTransformCoeff2 * prevd4) - (HilbertTransformCoeff1 * prevd6)) * ((PeriodCorrectionFactor * prevPeriod) + PeriodCorrectionOffset);
            q1List.AddRounded(q1);

            var i1 = prevd3;
            i1List.AddRounded(i1);

            var j1 = ((HilbertTransformCoeff1 * i1) + (HilbertTransformCoeff2 * previ1x2) - (HilbertTransformCoeff2 * previ1x4) - (HilbertTransformCoeff1 * previ1x6)) * ((PeriodCorrectionFactor * prevPeriod) + PeriodCorrectionOffset);
            var jq = ((HilbertTransformCoeff1 * q1) + (HilbertTransformCoeff2 * prevq1x2) - (HilbertTransformCoeff2 * prevq1x4) - (HilbertTransformCoeff1 * prevq1x6)) * ((PeriodCorrectionFactor * prevPeriod) + PeriodCorrectionOffset);

            var i2 = i1 - jq;
            i2 = (0.2 * i2) + (0.8 * previ2);
            i2List.AddRounded(i2);

            var q2 = q1 + j1;
            q2 = (0.2 * q2) + (0.8 * prevq2);
            q2List.AddRounded(q2);

            var re = (i2 * previ2) + (q2 * prevq2);
            re = (0.2 * re) + (0.8 * prevRe);
            reList.AddRounded(re);

            var im = (i2 * prevq2) - (q2 * previ2);
            im = (0.2 * im) + (0.8 * prevIm);
            imList.AddRounded(im);

            var atan = re != 0 ? Math.Atan(im / re) : 0;
            var period = atan != 0 ? 2 * Math.PI / atan : 0;

            if (prevPeriod != 0)
            {
                period = MinOrMax(period, 1.5 * prevPeriod, 0.67 * prevPeriod);
            }

            period = MinOrMax(period, 50, 6);
            period = (0.2 * period) + (0.8 * prevPeriod);
            periodList.AddRounded(period);

            var sPrd = (0.33 * period) + (0.67 * prevSprd);
            sPrdList.AddRounded(sPrd);

            var phase = i1 != 0 ? Math.Atan(q1 / i1).ToDegrees() : 0;
            phaseList.AddRounded(phase);

            var deltaPhase = prevPhase - phase < 1 ? 1 : prevPhase - phase;
            var alpha = deltaPhase != 0 ? fastAlpha / deltaPhase : 0;
            alpha = alpha < slowAlpha ? slowAlpha : alpha;

            var mama = (alpha * currentValue) + ((1 - alpha) * prevMama);
            mamaList.AddRounded(mama);

            var fama = (0.5 * alpha * mama) + ((1 - (0.5 * alpha)) * prevFama);
            famaList.AddRounded(fama);

            var signal = GetCompareSignal(mama - fama, prevMama - prevFama);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Fama", famaList },
            { "Mama", mamaList },
            { "I1", i1List },
            { "Q1", q1List },
            { "SmoothPeriod", sPrdList },
            { "Smooth", smoothList },
            { "Real", reList },
            { "Imag", imList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = mamaList;
        stockData.IndicatorName = IndicatorName.EhlersMotherOfAdaptiveMovingAverages;

        return stockData;
    }

    /// <summary>
    /// Calculates the welles wilder moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateWellesWilderMovingAverage(this StockData stockData, int length = 14)
    {
        List<double> wwmaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var k = (double)1 / length;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevWwma = wwmaList.LastOrDefault();
            var wwma = (currentValue * k) + (prevWwma * (1 - k));
            wwmaList.AddRounded(wwma);

            var signal = GetCompareSignal(currentValue - wwma, prevValue - prevWwma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Wwma", wwmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = wwmaList;
        stockData.IndicatorName = IndicatorName.WellesWilderMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Tillson T3 Moving Average
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <param name="vFactor">The v factor.</param>
    /// <returns></returns>
    public static StockData CalculateTillsonT3MovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length = 5, double vFactor = 0.7)
    {
        List<double> t3List = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var c1 = -vFactor * vFactor * vFactor;
        var c2 = (3 * vFactor * vFactor) + (3 * vFactor * vFactor * vFactor);
        var c3 = (-6 * vFactor * vFactor) - (3 * vFactor) - (3 * vFactor * vFactor * vFactor);
        var c4 = 1 + (3 * vFactor) + (vFactor * vFactor * vFactor) + (3 * vFactor * vFactor);

        var ema1List = GetMovingAverageList(stockData, maType, length, inputList);
        var ema2List = GetMovingAverageList(stockData, maType, length, ema1List);
        var ema3List = GetMovingAverageList(stockData, maType, length, ema2List);
        var ema4List = GetMovingAverageList(stockData, maType, length, ema3List);
        var ema5List = GetMovingAverageList(stockData, maType, length, ema4List);
        var ema6List = GetMovingAverageList(stockData, maType, length, ema5List);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var ema6 = ema6List[i];
            var ema5 = ema5List[i];
            var ema4 = ema4List[i];
            var ema3 = ema3List[i];

            var prevT3 = t3List.LastOrDefault();
            var t3 = (c1 * ema6) + (c2 * ema5) + (c3 * ema4) + (c4 * ema3);
            t3List.AddRounded(t3);

            var signal = GetCompareSignal(currentValue - t3, prevValue - prevT3);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "T3", t3List }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = t3List;
        stockData.IndicatorName = IndicatorName.TillsonT3MovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the triple exponential moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateTripleExponentialMovingAverage(this StockData stockData, 
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14)
    {
        List<double> temaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var ema1List = GetMovingAverageList(stockData, maType, length, inputList);
        var ema2List = GetMovingAverageList(stockData, maType, length, ema1List);
        var ema3List = GetMovingAverageList(stockData, maType, length, ema2List);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentEma1 = ema1List[i];
            var currentEma2 = ema2List[i];
            var currentEma3 = ema3List[i];

            var prevTema = temaList.LastOrDefault();
            var tema = (3 * currentEma1) - (3 * currentEma2) + currentEma3;
            temaList.AddRounded(tema);

            var signal = GetCompareSignal(currentValue - tema, prevValue - prevTema);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Tema", temaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = temaList;
        stockData.IndicatorName = IndicatorName.TripleExponentialMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the volume weighted average price.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="inputName">Name of the input.</param>
    /// <returns></returns>
    public static StockData CalculateVolumeWeightedAveragePrice(this StockData stockData, InputName inputName = InputName.TypicalPrice)
    {
        List<double> vwapList = new();
        List<double> tempVolList = new();
        List<double> tempVolPriceList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _, volumeList) = GetInputValuesList(inputName, stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var currentVolume = volumeList[i];
            tempVolList.AddRounded(currentVolume);

            var volumePrice = currentValue * currentVolume;
            tempVolPriceList.AddRounded(volumePrice);

            var volPriceSum = tempVolPriceList.Sum();
            var volSum = tempVolList.Sum();

            var prevVwap = vwapList.LastOrDefault();
            var vwap = volSum != 0 ? volPriceSum / volSum : 0;
            vwapList.AddRounded(vwap);

            var signal = GetCompareSignal(currentValue - vwap, prevValue - prevVwap);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Vwap", vwapList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = vwapList;
        stockData.IndicatorName = IndicatorName.VolumeWeightedAveragePrice;

        return stockData;
    }

    /// <summary>
    /// Calculates the volume weighted moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateVolumeWeightedMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 14)
    {
        List<double> volumePriceList = new();
        List<double> vwmaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);

        var volumeSmaList = GetMovingAverageList(stockData, maType, length, volumeList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentVolume = volumeList[i];
            var currentVolumeSma = volumeSmaList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var volumePrice = currentValue * currentVolume;
            volumePriceList.AddRounded(volumePrice);

            var volumePriceSma = volumePriceList.TakeLastExt(length).Average();

            var prevVwma = vwmaList.LastOrDefault();
            var vwma = currentVolumeSma != 0 ? volumePriceSma / currentVolumeSma : 0;
            vwmaList.AddRounded(vwma);

            var signal = GetCompareSignal(currentValue - vwma, prevValue - prevVwma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Vwma", vwmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = vwmaList;
        stockData.IndicatorName = IndicatorName.VolumeWeightedMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the ultimate moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="minLength">The minimum length.</param>
    /// <param name="maxLength">The maximum length.</param>
    /// <param name="acc">The acc.</param>
    /// <returns></returns>
    public static StockData CalculateUltimateMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int minLength = 5, int maxLength = 50, double acc = 1)
    {
        List<double> umaList = new();
        List<double> posMoneyFlowList = new();
        List<double> negMoneyFlowList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var lenList = CalculateVariableLengthMovingAverage(stockData, maType, minLength, maxLength).OutputValues["Length"];
        var tpList = CalculateTypicalPrice(stockData).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;
            var currentVolume = stockData.Volumes[i];
            var typicalPrice = tpList[i];
            var prevTypicalPrice = i >= 1 ? tpList[i - 1] : 0;
            var length = MinOrMax(lenList[i], maxLength, minLength);
            var rawMoneyFlow = typicalPrice * currentVolume;

            var posMoneyFlow = i >= 1 && typicalPrice > prevTypicalPrice ? rawMoneyFlow : 0;
            posMoneyFlowList.AddRounded(posMoneyFlow);

            var negMoneyFlow = i >= 1 && typicalPrice < prevTypicalPrice ? rawMoneyFlow : 0;
            negMoneyFlowList.AddRounded(negMoneyFlow);

            var len = (int)length;
            var posMoneyFlowTotal = posMoneyFlowList.TakeLastExt(len).Sum();
            var negMoneyFlowTotal = negMoneyFlowList.TakeLastExt(len).Sum();
            var mfiRatio = negMoneyFlowTotal != 0 ? posMoneyFlowTotal / negMoneyFlowTotal : 0;
            var mfi = negMoneyFlowTotal == 0 ? 100 : posMoneyFlowTotal == 0 ? 0 : MinOrMax(100 - (100 / (1 + mfiRatio)), 100, 0);
            var mfScaled = (mfi * 2) - 100;
            var p = acc + (Math.Abs(mfScaled) / 25);
            double sum = 0, weightedSum = 0;
            for (var j = 0; j <= len - 1; j++)
            {
                var weight = Pow(len - j, p);
                var prevValue = i >= j ? inputList[i - j] : 0;

                sum += prevValue * weight;
                weightedSum += weight;
            }

            var prevUma = umaList.LastOrDefault();
            var uma = weightedSum != 0 ? sum / weightedSum : 0;
            umaList.AddRounded(uma);

            var signal = GetCompareSignal(currentValue - uma, prevVal - prevUma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Uma", umaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = umaList;
        stockData.IndicatorName = IndicatorName.UltimateMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the variable length moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="minLength">The minimum length.</param>
    /// <param name="maxLength">The maximum length.</param>
    /// <returns></returns>
    public static StockData CalculateVariableLengthMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int minLength = 5, int maxLength = 50)
    {
        List<double> vlmaList = new();
        List<double> lengthList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, maxLength, inputList);
        var stdDevList = CalculateStandardDeviationVolatility(stockData, maType, maxLength).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var sma = smaList[i];
            var stdDev = stdDevList[i];
            var a = sma - (1.75 * stdDev);
            var b = sma - (0.25 * stdDev);
            var c = sma + (0.25 * stdDev);
            var d = sma + (1.75 * stdDev);

            var prevLength = i >= 1 ? lengthList[i - 1] : maxLength;
            var length = MinOrMax(currentValue >= b && currentValue <= c ? prevLength + 1 : currentValue < a ||
                currentValue > d ? prevLength - 1 : prevLength, maxLength, minLength);
            lengthList.AddRounded(length);

            var sc = 2 / (length + 1);
            var prevVlma = i >= 1 ? vlmaList[i - 1] : currentValue;
            var vlma = (currentValue * sc) + ((1 - sc) * prevVlma);
            vlmaList.AddRounded(vlma);

            var signal = GetCompareSignal(currentValue - vlma, prevValue - prevVlma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Length", lengthList },
            { "Vlma", vlmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = vlmaList;
        stockData.IndicatorName = IndicatorName.VariableLengthMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the ahrens moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateAhrensMovingAverage(this StockData stockData, int length = 9)
    {
        List<double> ahmaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var priorAhma = i >= length ? ahmaList[i - length] : currentValue;

            var prevAhma = ahmaList.LastOrDefault();
            var ahma = prevAhma + ((currentValue - ((prevAhma + priorAhma) / 2)) / length);
            ahmaList.AddRounded(ahma);

            var signal = GetCompareSignal(currentValue - ahma, prevValue - prevAhma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ahma", ahmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = ahmaList;
        stockData.IndicatorName = IndicatorName.AhrensMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the adaptive moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="fastLength">Length of the fast.</param>
    /// <param name="slowLength">Length of the slow.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateAdaptiveMovingAverage(this StockData stockData, int fastLength = 2, int slowLength = 14, int length = 14)
    {
        List<double> amaList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length + 1);

        var fastAlpha = (double)2 / (fastLength + 1);
        var slowAlpha = (double)2 / (slowLength + 1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var hh = highestList[i];
            var ll = lowestList[i];
            var mltp = hh - ll != 0 ? MinOrMax(Math.Abs((2 * currentValue) - ll - hh) / (hh - ll), 1, 0) : 0;
            var ssc = (mltp * (fastAlpha - slowAlpha)) + slowAlpha;

            var prevAma = amaList.LastOrDefault();
            var ama = prevAma + (Pow(ssc, 2) * (currentValue - prevAma));
            amaList.AddRounded(ama);

            var signal = GetCompareSignal(currentValue - ama, prevValue - prevAma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ama", amaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = amaList;
        stockData.IndicatorName = IndicatorName.AdaptiveMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the adaptive exponential moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateAdaptiveExponentialMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 10)
    {
        List<double> aemaList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        var mltp1 = (double)2 / (length + 1);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var hh = highestList[i];
            var ll = lowestList[i];
            var sma = smaList[i];
            var mltp2 = hh - ll != 0 ? MinOrMax(Math.Abs((2 * currentValue) - ll - hh) / (hh - ll), 1, 0) : 0;
            var rate = mltp1 * (1 + mltp2);

            var prevAema = i >= 1 ? aemaList.LastOrDefault() : currentValue;
            var aema = i <= length ? sma : prevAema + (rate * (currentValue - prevAema));
            aemaList.AddRounded(aema);

            var signal = GetCompareSignal(currentValue - aema, prevValue - prevAema);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Aema", aemaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = aemaList;
        stockData.IndicatorName = IndicatorName.AdaptiveExponentialMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the adaptive autonomous recursive moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <param name="gamma">The gamma.</param>
    /// <returns></returns>
    public static StockData CalculateAdaptiveAutonomousRecursiveMovingAverage(this StockData stockData, int length = 14, double gamma = 3)
    {
        List<double> ma1List = new();
        List<double> ma2List = new();
        List<double> absDiffList = new();
        List<double> dList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var erList = CalculateKaufmanAdaptiveMovingAverage(stockData, length: length).OutputValues["Er"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var er = erList[i];
            var prevMa2 = i >= 1 ? ma2List[i - 1] : currentValue;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var absDiff = Math.Abs(currentValue - prevMa2);
            absDiffList.AddRounded(absDiff);

            var d = i != 0 ? absDiffList.Sum() / i * gamma : 0;
            dList.AddRounded(d);

            var c = currentValue > prevMa2 + d ? currentValue + d : currentValue < prevMa2 - d ? currentValue - d : prevMa2;
            var prevMa1 = i >= 1 ? ma1List[i - 1] : currentValue;
            var ma1 = (er * c) + ((1 - er) * prevMa1);
            ma1List.AddRounded(ma1);

            var ma2 = (er * ma1) + ((1 - er) * prevMa2);
            ma2List.AddRounded(ma2);

            var signal = GetCompareSignal(currentValue - ma2, prevValue - prevMa2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "D", dList },
            { "Aarma", ma2List }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = ma2List;
        stockData.IndicatorName = IndicatorName.AdaptiveAutonomousRecursiveMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the autonomous recursive moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <param name="momLength">Length of the mom.</param>
    /// <param name="gamma">The gamma.</param>
    /// <returns></returns>
    public static StockData CalculateAutonomousRecursiveMovingAverage(this StockData stockData, int length = 14, int momLength = 7, double gamma = 3)
    {
        List<double> madList = new();
        List<double> ma1List = new();
        List<double> absDiffList = new();
        List<double> cList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var priorValue = i >= length ? inputList[i - momLength] : 0;
            var prevMad = i >= 1 ? madList[i - 1] : currentValue;

            var absDiff = Math.Abs(priorValue - prevMad);
            absDiffList.AddRounded(absDiff);

            var d = i != 0 ? absDiffList.Sum() / i * gamma : 0;
            var c = currentValue > prevMad + d ? currentValue + d : currentValue < prevMad - d ? currentValue - d : prevMad;
            cList.AddRounded(c);

            var ma1 = cList.TakeLastExt(length).Average();
            ma1List.AddRounded(ma1);

            var mad = ma1List.TakeLastExt(length).Average();
            madList.AddRounded(mad);

            var signal = GetCompareSignal(currentValue - mad, prevValue - prevMad);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Arma", madList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = madList;
        stockData.IndicatorName = IndicatorName.AutonomousRecursiveMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the atr filtered exponential moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <param name="atrLength">Length of the atr.</param>
    /// <param name="stdDevLength">Length of the standard dev.</param>
    /// <param name="lbLength">Length of the lb.</param>
    /// <param name="min">The minimum.</param>
    /// <returns></returns>
    public static StockData CalculateAtrFilteredExponentialMovingAverage(this StockData stockData, 
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 45, int atrLength = 20, int stdDevLength = 10, int lbLength = 20, 
        double min = 5)
    {
        List<double> trValList = new();
        List<double> atrValPowList = new();
        List<double> tempList = new();
        List<double> stdDevList = new();
        List<double> emaAFPList = new();
        List<double> emaCTPList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var tr = CalculateTrueRange(currentHigh, currentLow, prevValue);

            var trVal = currentValue != 0 ? tr / currentValue : tr;
            trValList.AddRounded(trVal);
        }

        var atrValList = GetMovingAverageList(stockData, maType, atrLength, trValList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var atrVal = atrValList[i];

            var atrValPow = Pow(atrVal, 2);
            atrValPowList.AddRounded(atrValPow);
        }

        var stdDevAList = GetMovingAverageList(stockData, maType, stdDevLength, atrValPowList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var stdDevA = stdDevAList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var atrVal = atrValList[i];
            tempList.AddRounded(atrVal);

            var atrValSum = tempList.TakeLastExt(stdDevLength).Sum();
            var stdDevB = Pow(atrValSum, 2) / Pow(stdDevLength, 2);

            var stdDev = stdDevA - stdDevB >= 0 ? Sqrt(stdDevA - stdDevB) : 0;
            stdDevList.AddRounded(stdDev);

            var stdDevLow = stdDevList.TakeLastExt(lbLength).Min();
            var stdDevFactorAFP = stdDev != 0 ? stdDevLow / stdDev : 0;
            var stdDevFactorCTP = stdDevLow != 0 ? stdDev / stdDevLow : 0;
            var stdDevFactorAFPLow = Math.Min(stdDevFactorAFP, min);
            var stdDevFactorCTPLow = Math.Min(stdDevFactorCTP, min);
            var alphaAfp = (2 * stdDevFactorAFPLow) / (length + 1);
            var alphaCtp = (2 * stdDevFactorCTPLow) / (length + 1);

            var prevEmaAfp = emaAFPList.LastOrDefault();
            var emaAfp = (alphaAfp * currentValue) + ((1 - alphaAfp) * prevEmaAfp);
            emaAFPList.AddRounded(emaAfp);

            var prevEmaCtp = emaCTPList.LastOrDefault();
            var emaCtp = (alphaCtp * currentValue) + ((1 - alphaCtp) * prevEmaCtp);
            emaCTPList.AddRounded(emaCtp);

            var signal = GetCompareSignal(currentValue - emaAfp, prevValue - prevEmaAfp);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Afp", emaAFPList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = emaAFPList;
        stockData.IndicatorName = IndicatorName.AtrFilteredExponentialMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the adaptive least squares.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <param name="smooth">The smooth.</param>
    /// <returns></returns>
    public static StockData CalculateAdaptiveLeastSquares(this StockData stockData, int length = 500, double smooth = 1.5)
    {
        List<double> xList = new();
        List<double> yList = new();
        List<double> mxList = new();
        List<double> myList = new();
        List<double> regList = new();
        List<double> tempList = new();
        List<double> mxxList = new();
        List<double> myyList = new();
        List<double> mxyList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            double index = i;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentHigh = highList[i];
            var currentLow = lowList[i];

            var tr = CalculateTrueRange(currentHigh, currentLow, prevValue);
            tempList.AddRounded(tr);

            var highest = tempList.TakeLastExt(length).Max();
            var alpha = highest != 0 ? MinOrMax(Pow(tr / highest, smooth), 0.99, 0.01) : 0.01;
            var xx = index * index;
            var yy = currentValue * currentValue;
            var xy = index * currentValue;

            var prevX = i >= 1 ? xList[i - 1] : index;
            var x = (alpha * index) + ((1 - alpha) * prevX);
            xList.AddRounded(x);

            var prevY = i >= 1 ? yList[i - 1] : currentValue;
            var y = (alpha * currentValue) + ((1 - alpha) * prevY);
            yList.AddRounded(y);

            var dx = Math.Abs(index - x);
            var dy = Math.Abs(currentValue - y);

            var prevMx = i >= 1 ? mxList[i - 1] : dx;
            var mx = (alpha * dx) + ((1 - alpha) * prevMx);
            mxList.AddRounded(mx);

            var prevMy = i >= 1 ? myList[i - 1] : dy;
            var my = (alpha * dy) + ((1 - alpha) * prevMy);
            myList.AddRounded(my);

            var prevMxx = i >= 1 ? mxxList[i - 1] : xx;
            var mxx = (alpha * xx) + ((1 - alpha) * prevMxx);
            mxxList.AddRounded(mxx);

            var prevMyy = i >= 1 ? myyList[i - 1] : yy;
            var myy = (alpha * yy) + ((1 - alpha) * prevMyy);
            myyList.AddRounded(myy);

            var prevMxy = i >= 1 ? mxyList[i - 1] : xy;
            var mxy = (alpha * xy) + ((1 - alpha) * prevMxy);
            mxyList.AddRounded(mxy);

            var alphaVal = (2 / alpha) + 1;
            var a1 = alpha != 0 ? (Pow(alphaVal, 2) * mxy) - (alphaVal * mx * alphaVal * my) : 0;
            var tempVal = ((Pow(alphaVal, 2) * mxx) - Pow(alphaVal * mx, 2)) * ((Pow(alphaVal, 2) * myy) - Pow(alphaVal * my, 2));
            var b1 = tempVal >= 0 ? Sqrt(tempVal) : 0;
            var r = b1 != 0 ? a1 / b1 : 0;
            var a = mx != 0 ? r * (my / mx) : 0;
            var b = y - (a * x);

            var prevReg = regList.LastOrDefault();
            var reg = (x * a) + b;
            regList.AddRounded(reg);

            var signal = GetCompareSignal(currentValue - reg, prevValue - prevReg);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Als", regList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = regList;
        stockData.IndicatorName = IndicatorName.AdaptiveLeastSquares;

        return stockData;
    }

    /// <summary>
    /// Calculates the alpha decreasing exponential moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <returns></returns>
    public static StockData CalculateAlphaDecreasingExponentialMovingAverage(this StockData stockData)
    {
        List<double> emaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var alpha = (double)2 / (i + 1);

            var prevEma = emaList.LastOrDefault();
            var ema = (alpha * currentValue) + ((1 - alpha) * prevEma);
            emaList.AddRounded(ema);

            var signal = GetCompareSignal(currentValue - ema, prevValue - prevEma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ema", emaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = emaList;
        stockData.IndicatorName = IndicatorName.AlphaDecreasingExponentialMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the powered kaufman adaptive moving average.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <param name="factor">The factor.</param>
    /// <returns></returns>
    public static StockData CalculatePoweredKaufmanAdaptiveMovingAverage(this StockData stockData, int length = 100, double factor = 3)
    {
        List<double> aList = new();
        List<double> aSpList = new();
        List<double> perList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var erList = CalculateKaufmanAdaptiveMovingAverage(stockData, length: length).OutputValues["Er"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var er = erList[i];
            var powSp = er != 0 ? 1 / er : factor;
            var perSp = Pow(er, powSp);

            var per = Pow(er, factor);
            perList.AddRounded(per);

            var prevA = i >= 1 ? aList.LastOrDefault() : currentValue;
            var a = (per * currentValue) + ((1 - per) * prevA);
            aList.AddRounded(a);

            var prevASp = i >= 1 ? aSpList.LastOrDefault() : currentValue;
            var aSp = (perSp * currentValue) + ((1 - perSp) * prevASp);
            aSpList.AddRounded(aSp);

            var signal = GetCompareSignal(currentValue - a, prevValue - prevA);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Per", perList },
            { "Pkama", aList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = aList;
        stockData.IndicatorName = IndicatorName.PoweredKaufmanAdaptiveMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the automatic filter.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateAutoFilter(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 500)
    {
        List<double> regList = new();
        List<double> corrList = new();
        List<double> interList = new();
        List<double> slopeList = new();
        List<double> tempList = new();
        List<double> xList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var yMaList = GetMovingAverageList(stockData, maType, length, inputList);
        var devList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var dev = devList[i];

            var currentValue = inputList[i];
            tempList.AddRounded(currentValue);

            var prevX = i >= 1 ? xList[i - 1] : currentValue;
            var x = currentValue > prevX + dev ? currentValue : currentValue < prevX - dev ? currentValue : prevX;
            xList.AddRounded(x);

            var corr = GoodnessOfFit.R(tempList.TakeLastExt(length).Select(x => (double)x), xList.TakeLastExt(length).Select(x => (double)x));
            corr = IsValueNullOrInfinity(corr) ? 0 : corr;
            corrList.AddRounded((double)corr);
        }

        var xMaList = GetMovingAverageList(stockData, maType, length, xList);
        stockData.CustomValuesList = xList;
        var mxList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var my = devList[i];
            var mx = mxList[i];
            var corr = corrList[i];
            var yMa = yMaList[i];
            var xMa = xMaList[i];
            var x = xList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var slope = mx != 0 ? corr * (my / mx) : 0;
            var inter = yMa - (slope * xMa);

            var prevReg = regList.LastOrDefault();
            var reg = (x * slope) + inter;
            regList.AddRounded(reg);

            var signal = GetCompareSignal(currentValue - reg, prevValue - prevReg);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Af", regList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = regList;
        stockData.IndicatorName = IndicatorName.AutoFilter;

        return stockData;
    }

    /// <summary>
    /// Calculates the automatic line.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateAutoLine(this StockData stockData, int length = 500)
    {
        List<double> xList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var devList = CalculateStandardDeviationVolatility(stockData, length: length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var dev = devList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevX = i >= 1 ? xList[i - 1] : currentValue;
            var x = currentValue > prevX + dev ? currentValue : currentValue < prevX - dev ? currentValue : prevX;
            xList.AddRounded(x);

            var signal = GetCompareSignal(currentValue - x, prevValue - prevX);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Al", xList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = xList;
        stockData.IndicatorName = IndicatorName.AutoLine;

        return stockData;
    }

    /// <summary>
    /// Calculates the automatic line with drift.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateAutoLineWithDrift(this StockData stockData, int length = 500)
    {
        List<double> aList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var stdDevList = CalculateStandardDeviationVolatility(stockData, length: length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var dev = stdDevList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var r = Math.Round(currentValue);

            var prevA = i >= 1 ? aList[i - 1] : r;
            var priorA = i >= length + 1 ? aList[i - (length + 1)] : r;
            var a = currentValue > prevA + dev ? currentValue : currentValue < prevA - dev ? currentValue :
                prevA + ((double)1 / (length * 2) * (prevA - priorA));
            aList.AddRounded(a);

            var signal = GetCompareSignal(currentValue - a, prevValue - prevA);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Alwd", aList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = aList;
        stockData.IndicatorName = IndicatorName.AutoLineWithDrift;

        return stockData;
    }

    /// <summary>
    /// Calculates the 1LC Least Squares Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData Calculate1LCLeastSquaresMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 14)
    {
        List<double> yList = new();
        List<double> tempList = new();
        List<double> corrList = new();
        List<double> indexList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);
        var stdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;

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
            var sma = smaList[i];
            var corr = corrList[i];
            var stdDev = stdDevList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevY = yList.LastOrDefault();
            var y = sma + (corr * stdDev * 1.7);
            yList.AddRounded(y);

            var signal = GetCompareSignal(currentValue - y, prevValue - prevY);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "1lsma", yList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = yList;
        stockData.IndicatorName = IndicatorName._1LCLeastSquaresMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the 3HMA
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData Calculate3HMA(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 50)
    {
        List<double> midList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var p = MinOrMax((int)Math.Ceiling((double)length / 2));
        var p1 = MinOrMax((int)Math.Ceiling((double)p / 3));
        var p2 = MinOrMax((int)Math.Ceiling((double)p / 2));

        var wma1List = GetMovingAverageList(stockData, maType, p1, inputList);
        var wma2List = GetMovingAverageList(stockData, maType, p2, inputList);
        var wma3List = GetMovingAverageList(stockData, maType, p, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var wma1 = wma1List[i];
            var wma2 = wma2List[i];
            var wma3 = wma3List[i];

            var mid = (wma1 * 3) - wma2 - wma3;
            midList.AddRounded(mid);
        }

        var aList = GetMovingAverageList(stockData, maType, p, midList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var a = aList[i];
            var prevA = i >= 1 ? aList[i - 1] : 0;
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var signal = GetCompareSignal(currentValue - a, prevValue - prevA);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "3hma", aList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = aList;
        stockData.IndicatorName = IndicatorName._3HMA;

        return stockData;
    }

    /// <summary>
    /// Calculates the Jsa Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateJsaMovingAverage(this StockData stockData, int length = 14)
    {
        List<double> jmaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var priorValue = i >= length ? inputList[i - length] : 0;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevJma = jmaList.LastOrDefault();
            var jma = (currentValue + priorValue) / 2;
            jmaList.AddRounded(jma);

            var signal = GetCompareSignal(currentValue - jma, prevValue - prevJma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Jma", jmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = jmaList;
        stockData.IndicatorName = IndicatorName.JsaMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Jurik Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="phase"></param>
    /// <param name="power"></param>
    /// <returns></returns>
    public static StockData CalculateJurikMovingAverage(this StockData stockData, int length = 7, double phase = 50, double power = 2)
    {
        List<double> e0List = new();
        List<double> e1List = new();
        List<double> e2List = new();
        List<double> jmaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var phaseRatio = phase < -100 ? 0.5 : phase > 100 ? 2.5 : ((double)phase / 100) + 1.5;
        var ratio = 0.45 * (length - 1);
        var beta = ratio / (ratio + 2);
        var alpha = Pow(beta, power);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevJma = jmaList.LastOrDefault();

            var prevE0 = e0List.LastOrDefault();
            var e0 = ((1 - alpha) * currentValue) + (alpha * prevE0);
            e0List.AddRounded(e0);

            var prevE1 = e1List.LastOrDefault();
            var e1 = ((currentValue - e0) * (1 - beta)) + (beta * prevE1);
            e1List.AddRounded(e1);

            var prevE2 = e2List.LastOrDefault();
            var e2 = ((e0 + (phaseRatio * e1) - prevJma) * Pow(1 - alpha, 2)) + (Pow(alpha, 2) * prevE2);
            e2List.AddRounded(e2);

            var jma = e2 + prevJma;
            jmaList.AddRounded(jma);

            var signal = GetCompareSignal(currentValue - jma, prevValue - prevJma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Jma", jmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = jmaList;
        stockData.IndicatorName = IndicatorName.JurikMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Zero Low Lag Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="lag"></param>
    /// <returns></returns>
    public static StockData CalculateZeroLowLagMovingAverage(this StockData stockData, int length = 50, double lag = 1.4)
    {
        List<double> aList = new();
        List<double> bList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var lbLength = MinOrMax((int)Math.Ceiling((double)length / 2));

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var priorB = i >= lbLength ? bList[i - lbLength] : currentValue;
            var priorA = i >= length ? aList[i - length] : 0;

            var prevA = aList.LastOrDefault();
            var a = (lag * currentValue) + ((1 - lag) * priorB) + prevA;
            aList.AddRounded(a);

            var aDiff = a - priorA;
            var prevB = bList.LastOrDefault();
            var b = aDiff / length;
            bList.AddRounded(b);

            var signal = GetCompareSignal(currentValue - b, prevValue - prevB);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Zllma", bList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = bList;
        stockData.IndicatorName = IndicatorName.ZeroLowLagMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Zero Lag Exponential Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateZeroLagExponentialMovingAverage(this StockData stockData,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14)
    {
        List<double> zemaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var ema1List = GetMovingAverageList(stockData, maType, length, inputList);
        var ema2List = GetMovingAverageList(stockData, maType, length, ema1List);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var ema1 = ema1List[i];
            var ema2 = ema2List[i];
            var d = ema1 - ema2;

            var prevZema = zemaList.LastOrDefault();
            var zema = ema1 + d;
            zemaList.AddRounded(zema);

            var signal = GetCompareSignal(currentValue - zema, prevValue - prevZema);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Zema", zemaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = zemaList;
        stockData.IndicatorName = IndicatorName.ZeroLagExponentialMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Zero Lag Triple Exponential Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateZeroLagTripleExponentialMovingAverage(this StockData stockData,
        MovingAvgType maType = MovingAvgType.TripleExponentialMovingAverage, int length = 14)
    {
        List<double> zlTemaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var tma1List = GetMovingAverageList(stockData, maType, length, inputList);
        var tma2List = GetMovingAverageList(stockData, maType, length, tma1List);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var tma1 = tma1List[i];
            var tma2 = tma2List[i];
            var diff = tma1 - tma2;

            var prevZltema = zlTemaList.LastOrDefault();
            var zltema = tma1 + diff;
            zlTemaList.AddRounded(zltema);

            var signal = GetCompareSignal(currentValue - zltema, prevValue - prevZltema);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ztema", zlTemaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = zlTemaList;
        stockData.IndicatorName = IndicatorName.ZeroLagTripleExponentialMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Bryant Adaptive Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="maxLength"></param>
    /// <param name="trend"></param>
    /// <returns></returns>
    public static StockData CalculateBryantAdaptiveMovingAverage(this StockData stockData, int length = 14, int maxLength = 100, double trend = -1)
    {
        List<double> bamaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var erList = CalculateKaufmanAdaptiveMovingAverage(stockData, length: length).OutputValues["Er"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var er = erList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var ver = Pow(er - (((2 * er) - 1) / 2 * (1 - trend)) + 0.5, 2);
            var vLength = ver != 0 ? (length - ver + 1) / ver : 0;
            vLength = Math.Min(vLength, maxLength);
            var vAlpha = 2 / (vLength + 1);

            var prevBama = bamaList.LastOrDefault();
            var bama = (vAlpha * currentValue) + ((1 - vAlpha) * prevBama);
            bamaList.AddRounded(bama);

            var signal = GetCompareSignal(currentValue - bama, prevValue - prevBama);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Bama", bamaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = bamaList;
        stockData.IndicatorName = IndicatorName.BryantAdaptiveMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Windowed Volume Weighted Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateWindowedVolumeWeightedMovingAverage(this StockData stockData, int length = 100)
    {
        List<double> bartlettWList = new();
        List<double> blackmanWList = new();
        List<double> hanningWList = new();
        List<double> bartlettVWList = new();
        List<double> blackmanVWList = new();
        List<double> hanningVWList = new();
        List<double> bartlettWvwmaList = new();
        List<double> blackmanWvwmaList = new();
        List<double> hanningWvwmaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentVolume = volumeList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var iRatio = (double)i / length;
            var bartlett = 1 - (2 * Math.Abs(i - ((double)length / 2)) / length);

            var bartlettW = bartlett * currentVolume;
            bartlettWList.AddRounded(bartlettW);

            var bartlettWSum = bartlettWList.TakeLastExt(length).Sum();
            var bartlettVW = currentValue * bartlettW;
            bartlettVWList.AddRounded(bartlettVW);

            var bartlettVWSum = bartlettVWList.TakeLastExt(length).Sum();
            var prevBartlettWvwma = bartlettWvwmaList.LastOrDefault();
            var bartlettWvwma = bartlettWSum != 0 ? bartlettVWSum / bartlettWSum : 0;
            bartlettWvwmaList.AddRounded(bartlettWvwma);

            var blackman = 0.42 - (0.5 * Math.Cos(2 * Math.PI * iRatio)) + (0.08 * Math.Cos(4 * Math.PI * iRatio));
            var blackmanW = blackman * currentVolume;
            blackmanWList.AddRounded(blackmanW);

            var blackmanWSum = blackmanWList.TakeLastExt(length).Sum();
            var blackmanVW = currentValue * blackmanW;
            blackmanVWList.AddRounded(blackmanVW);

            var blackmanVWSum = blackmanVWList.TakeLastExt(length).Sum();
            var blackmanWvwma = blackmanWSum != 0 ? blackmanVWSum / blackmanWSum : 0;
            blackmanWvwmaList.AddRounded(blackmanWvwma);

            var hanning = 0.5 - (0.5 * Math.Cos(2 * Math.PI * iRatio));
            var hanningW = hanning * currentVolume;
            hanningWList.AddRounded(hanningW);

            var hanningWSum = hanningWList.TakeLastExt(length).Sum();
            var hanningVW = currentValue * hanningW;
            hanningVWList.AddRounded(hanningVW);

            var hanningVWSum = hanningVWList.TakeLastExt(length).Sum();
            var hanningWvwma = hanningWSum != 0 ? hanningVWSum / hanningWSum : 0;
            hanningWvwmaList.AddRounded(hanningWvwma);

            var signal = GetCompareSignal(currentValue - bartlettWvwma, prevValue - prevBartlettWvwma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Wvwma", bartlettWvwmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = bartlettWvwmaList;
        stockData.IndicatorName = IndicatorName.WindowedVolumeWeightedMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Well Rounded Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateWellRoundedMovingAverage(this StockData stockData, int length = 14)
    {
        List<double> aList = new();
        List<double> bList = new();
        List<double> yList = new();
        List<double> srcYList = new();
        List<double> srcEmaList = new();
        List<double> yEmaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var alpha = (double)2 / (length + 1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevSrcY = i >= 1 ? srcYList[i - 1] : 0;
            var prevSrcEma = i >= 1 ? srcEmaList[i - 1] : 0;

            var prevA = aList.LastOrDefault();
            var a = prevA + (alpha * prevSrcY);
            aList.AddRounded(a);

            var prevB = bList.LastOrDefault();
            var b = prevB + (alpha * prevSrcEma);
            bList.AddRounded(b);

            var ab = a + b;
            var prevY = yList.LastOrDefault();
            var y = CalculateEMA(ab, prevY, 1);
            yList.AddRounded(y);

            var srcY = currentValue - y;
            srcYList.AddRounded(srcY);

            var prevYEma = yEmaList.LastOrDefault();
            var yEma = CalculateEMA(y, prevYEma, length);
            yEmaList.AddRounded(yEma);

            var srcEma = currentValue - yEma;
            srcEmaList.AddRounded(srcEma);

            var signal = GetCompareSignal(currentValue - y, prevValue - prevY);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Wrma", yList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = yList;
        stockData.IndicatorName = IndicatorName.WellRoundedMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Welles Wilder Summation
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateWellesWilderSummation(this StockData stockData, int length = 14)
    {
        List<double> sumList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevSum = sumList.LastOrDefault();
            var sum = prevSum - (prevSum / length) + currentValue;
            sumList.AddRounded(sum);

            var signal = GetCompareSignal(currentValue - sum, prevValue - prevSum);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Wws", sumList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = sumList;
        stockData.IndicatorName = IndicatorName.WellesWilderSummation;

        return stockData;
    }

    /// <summary>
    /// Calculates the Quick Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateQuickMovingAverage(this StockData stockData, int length = 14)
    {
        List<double> qmaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var peak = MinOrMax((int)Math.Ceiling((double)length / 3));

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            double num = 0, denom = 0;
            for (var j = 1; j <= length + 1; j++)
            {
                var mult = j <= peak ? (double)j / peak : (double)(length + 1 - j) / (length + 1 - peak);
                var prevValue = i >= j - 1 ? inputList[i - (j - 1)] : 0;

                num += prevValue * mult;
                denom += mult;
            }

            var prevQma = qmaList.LastOrDefault();
            var qma = denom != 0 ? num / denom : 0;
            qmaList.AddRounded(qma);

            var signal = GetCompareSignal(currentValue - qma, prevVal - prevQma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Qma", qmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = qmaList;
        stockData.IndicatorName = IndicatorName.QuickMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Quadratic Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateQuadraticMovingAverage(this StockData stockData, int length = 14)
    {
        List<double> qmaList = new();
        List<double> powList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var pow = Pow(currentValue, 2);
            powList.AddRounded(pow);

            var prevQma = qmaList.LastOrDefault();
            var powSma = powList.TakeLastExt(length).Average();
            var qma = powSma >= 0 ? Sqrt(powSma) : 0;
            qmaList.AddRounded(qma);

            var signal = GetCompareSignal(currentValue - qma, prevValue - prevQma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Qma", qmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = qmaList;
        stockData.IndicatorName = IndicatorName.QuadraticMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Quadruple Exponential Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateQuadrupleExponentialMovingAverage(this StockData stockData,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 20)
    {
        List<double> qemaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var ema1List = GetMovingAverageList(stockData, maType, length, inputList);
        var ema2List = GetMovingAverageList(stockData, maType, length, ema1List);
        var ema3List = GetMovingAverageList(stockData, maType, length, ema2List);
        var ema4List = GetMovingAverageList(stockData, maType, length, ema3List);
        var ema5List = GetMovingAverageList(stockData, maType, length, ema4List);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var ema1 = ema1List[i];
            var ema2 = ema2List[i];
            var ema3 = ema3List[i];
            var ema4 = ema4List[i];
            var ema5 = ema5List[i];

            var prevQema = qemaList.LastOrDefault();
            var qema = (5 * ema1) - (10 * ema2) + (10 * ema3) - (5 * ema4) + ema5;
            qemaList.AddRounded(qema);

            var signal = GetCompareSignal(currentValue - qema, prevValue - prevQema);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Qema", qemaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = qemaList;
        stockData.IndicatorName = IndicatorName.QuadrupleExponentialMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Quadratic Least Squares Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="forecastLength"></param>
    /// <returns></returns>
    public static StockData CalculateQuadraticLeastSquaresMovingAverage(this StockData stockData,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 50, int forecastLength = 14)
    {
        List<double> nList = new();
        List<double> n2List = new();
        List<double> nn2List = new();
        List<double> nn2CovList = new();
        List<double> n2vList = new();
        List<double> n2vCovList = new();
        List<double> nvList = new();
        List<double> nvCovList = new();
        List<double> qlsmaList = new();
        List<double> fcastList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];

            double n = i;
            nList.AddRounded(n);

            var n2 = Pow(n, 2);
            n2List.AddRounded(n2);

            var nn2 = n * n2;
            nn2List.AddRounded(nn2);

            var n2v = n2 * currentValue;
            n2vList.AddRounded(n2v);

            var nv = n * currentValue;
            nvList.AddRounded(nv);
        }

        var nSmaList = GetMovingAverageList(stockData, maType, length, nList);
        var n2SmaList = GetMovingAverageList(stockData, maType, length, n2List);
        var n2vSmaList = GetMovingAverageList(stockData, maType, length, n2vList);
        var nvSmaList = GetMovingAverageList(stockData, maType, length, nvList);
        var nn2SmaList = GetMovingAverageList(stockData, maType, length, nn2List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var nSma = nSmaList[i];
            var n2Sma = n2SmaList[i];
            var n2vSma = n2vSmaList[i];
            var nvSma = nvSmaList[i];
            var nn2Sma = nn2SmaList[i];
            var sma = smaList[i];

            var nn2Cov = nn2Sma - (nSma * n2Sma);
            nn2CovList.AddRounded(nn2Cov);

            var n2vCov = n2vSma - (n2Sma * sma);
            n2vCovList.AddRounded(n2vCov);

            var nvCov = nvSma - (nSma * sma);
            nvCovList.AddRounded(nvCov);
        }

        stockData.CustomValuesList = nList;
        var nVarianceList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        stockData.CustomValuesList = n2List;
        var n2VarianceList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var n2Variance = n2VarianceList[i];
            var nVariance = nVarianceList[i];
            var nn2Cov = nn2CovList[i];
            var n2vCov = n2vCovList[i];
            var nvCov = nvCovList[i];
            var sma = smaList[i];
            var n2Sma = n2SmaList[i];
            var nSma = nSmaList[i];
            var n2 = n2List[i];
            var norm = (n2Variance * nVariance) - Pow(nn2Cov, 2);
            var a = norm != 0 ? ((n2vCov * nVariance) - (nvCov * nn2Cov)) / norm : 0;
            var b = norm != 0 ? ((nvCov * n2Variance) - (n2vCov * nn2Cov)) / norm : 0;
            var c = sma - (a * n2Sma) - (b * nSma);

            var prevQlsma = qlsmaList.LastOrDefault();
            var qlsma = (a * n2) + (b * i) + c;
            qlsmaList.AddRounded(qlsma);

            var fcast = (a * Pow(i + forecastLength, 2)) + (b * (i + forecastLength)) + c;
            fcastList.AddRounded(fcast);

            var signal = GetCompareSignal(currentValue - qlsma, prevValue - prevQlsma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Qlma", qlsmaList },
            { "Forecast", fcastList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = qlsmaList;
        stockData.IndicatorName = IndicatorName.QuadraticLeastSquaresMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Quadratic Regression
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateQuadraticRegression(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 500)
    {
        List<double> tempList = new();
        List<double> x1List = new();
        List<double> x2List = new();
        List<double> x1SumList = new();
        List<double> x2SumList = new();
        List<double> x1x2List = new();
        List<double> x1x2SumList = new();
        List<double> x2PowList = new();
        List<double> x2PowSumList = new();
        List<double> ySumList = new();
        List<double> yx1List = new();
        List<double> yx2List = new();
        List<double> yx1SumList = new();
        List<double> yx2SumList = new();
        List<double> yList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var y = inputList[i];
            tempList.AddRounded(y);

            double x1 = i;
            x1List.AddRounded(x1);

            var x2 = Pow(x1, 2);
            x2List.AddRounded(x2);

            var x1x2 = x1 * x2;
            x1x2List.AddRounded(x1x2);

            var yx1 = y * x1;
            yx1List.AddRounded(yx1);

            var yx2 = y * x2;
            yx2List.AddRounded(yx2);

            var x2Pow = Pow(x2, 2);
            x2PowList.AddRounded(x2Pow);

            var ySum = tempList.TakeLastExt(length).Sum();
            ySumList.AddRounded(ySum);

            var x1Sum = x1List.TakeLastExt(length).Sum();
            x1SumList.AddRounded(x1Sum);

            var x2Sum = x2List.TakeLastExt(length).Sum();
            x2SumList.AddRounded(x2Sum);

            var x1x2Sum = x1x2List.TakeLastExt(length).Sum();
            x1x2SumList.AddRounded(x1x2Sum);

            var yx1Sum = yx1List.TakeLastExt(length).Sum();
            yx1SumList.AddRounded(yx1Sum);

            var yx2Sum = yx2List.TakeLastExt(length).Sum();
            yx2SumList.AddRounded(yx2Sum);

            var x2PowSum = x2PowList.TakeLastExt(length).Sum();
            x2PowSumList.AddRounded(x2PowSum);
        }

        var max1List = GetMovingAverageList(stockData, maType, length, x1List);
        var max2List = GetMovingAverageList(stockData, maType, length, x2List);
        var mayList = GetMovingAverageList(stockData, maType, length, inputList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var x1Sum = x1SumList[i];
            var x2Sum = x2SumList[i];
            var x1x2Sum = x1x2SumList[i];
            var x2PowSum = x2PowSumList[i];
            var yx1Sum = yx1SumList[i];
            var yx2Sum = yx2SumList[i];
            var ySum = ySumList[i];
            var may = mayList[i];
            var max1 = max1List[i];
            var max2 = max2List[i];
            var x1 = x1List[i];
            var x2 = x2List[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var s11 = x2Sum - (Pow(x1Sum, 2) / length);
            var s12 = x1x2Sum - ((x1Sum * x2Sum) / length);
            var s22 = x2PowSum - (Pow(x2Sum, 2) / length);
            var sy1 = yx1Sum - ((ySum * x1Sum) / length);
            var sy2 = yx2Sum - ((ySum * x2Sum) / length);
            var bot = (s22 * s11) - Pow(s12, 2);
            var b2 = bot != 0 ? ((sy1 * s22) - (sy2 * s12)) / bot : 0;
            var b3 = bot != 0 ? ((sy2 * s11) - (sy1 * s12)) / bot : 0;
            var b1 = may - (b2 * max1) - (b3 * max2);

            var prevY = yList.LastOrDefault();
            var y = b1 + (b2 * x1) + (b3 * x2);
            yList.AddRounded(y);

            var signal = GetCompareSignal(currentValue - y, prevValue - prevY);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "QuadReg", yList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = yList;
        stockData.IndicatorName = IndicatorName.QuadraticRegression;

        return stockData;
    }

    /// <summary>
    /// Calculates the Linear Weighted Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateLinearWeightedMovingAverage(this StockData stockData, int length = 14)
    {
        List<double> lwmaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            double sum = 0, weightedSum = 0;
            for (var j = 0; j <= length - 1; j++)
            {
                double weight = length - j;
                var prevValue = i >= j ? inputList[i - j] : 0;

                sum += prevValue * weight;
                weightedSum += weight;
            }

            var prevLwma = lwmaList.LastOrDefault();
            var lwma = weightedSum != 0 ? sum / weightedSum : 0;
            lwmaList.AddRounded(lwma);

            var signal = GetCompareSignal(currentValue - lwma, prevVal - prevLwma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Lwma", lwmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = lwmaList;
        stockData.IndicatorName = IndicatorName.LinearWeightedMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Leo Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateLeoMovingAverage(this StockData stockData, int length = 14)
    {
        List<double> lmaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var wmaList = CalculateWeightedMovingAverage(stockData, length).CustomValuesList;
        var smaList = CalculateSimpleMovingAverage(stockData, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentWma = wmaList[i];
            var currentSma = smaList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevLma = lmaList.LastOrDefault();
            var lma = (2 * currentWma) - currentSma;
            lmaList.AddRounded(lma);

            var signal = GetCompareSignal(currentValue - lma, prevValue - prevLma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Lma", lmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = lmaList;
        stockData.IndicatorName = IndicatorName.LeoMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Light Least Squares Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateLightLeastSquaresMovingAverage(this StockData stockData,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 250)
    {
        List<double> yList = new();
        List<double> indexList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var length1 = MinOrMax((int)Math.Ceiling((double)length / 2));

        for (var i = 0; i < stockData.Count; i++)
        {
            double index = i;
            indexList.AddRounded(index);
        }

        var sma1List = GetMovingAverageList(stockData, maType, length, inputList);
        var sma2List = GetMovingAverageList(stockData, maType, length1, inputList);
        var stdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        stockData.CustomValuesList = indexList;
        var indexStdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        var indexSmaList = GetMovingAverageList(stockData, maType, length, indexList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var sma1 = sma1List[i];
            var sma2 = sma2List[i];
            var stdDev = stdDevList[i];
            var indexStdDev = indexStdDevList[i];
            var indexSma = indexSmaList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var c = stdDev != 0 ? (sma2 - sma1) / stdDev : 0;
            var z = indexStdDev != 0 && c != 0 ? (i - indexSma) / indexStdDev * c : 0;

            var prevY = yList.LastOrDefault();
            var y = sma1 + (z * stdDev);
            yList.AddRounded(y);

            var signal = GetCompareSignal(currentValue - y, prevValue - prevY);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Llsma", yList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = yList;
        stockData.IndicatorName = IndicatorName.LightLeastSquaresMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Linear Extrapolation
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateLinearExtrapolation(this StockData stockData, int length = 500)
    {
        List<double> extList = new();
        List<double> xList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevY = i >= 1 ? inputList[i - 1] : 0;
            var priorY = i >= length ? inputList[i - length] : 0;
            var priorY2 = i >= length * 2 ? inputList[i - (length * 2)] : 0;
            var priorX = i >= length ? xList[i - length] : 0;
            var priorX2 = i >= length * 2 ? xList[i - (length * 2)] : 0;

            double x = i;
            xList.AddRounded(i);

            var prevExt = extList.LastOrDefault();
            var ext = priorX2 - priorX != 0 && priorY2 - priorY != 0 ? priorY + ((x - priorX) / (priorX2 - priorX) * (priorY2 - priorY)) : priorY;
            extList.AddRounded(ext);

            var signal = GetCompareSignal(currentValue - ext, prevY - prevExt);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "LinExt", extList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = extList;
        stockData.IndicatorName = IndicatorName.LinearExtrapolation;

        return stockData;
    }

    /// <summary>
    /// Calculates the Linear Regression Line
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateLinearRegressionLine(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 14)
    {
        List<double> regList = new();
        List<double> corrList = new();
        List<double> yList = new();
        List<double> xList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var yMaList = GetMovingAverageList(stockData, maType, length, inputList);
        var myList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            yList.AddRounded(currentValue);

            double x = i;
            xList.AddRounded(x);

            var corr = GoodnessOfFit.R(yList.TakeLastExt(length).Select(x => (double)x), xList.TakeLastExt(length).Select(x => (double)x));
            corr = IsValueNullOrInfinity(corr) ? 0 : corr;
            corrList.AddRounded((double)corr);
        }

        var xMaList = GetMovingAverageList(stockData, maType, length, xList);
        stockData.CustomValuesList = xList;
        var mxList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList; ;
        for (var i = 0; i < stockData.Count; i++)
        {
            var my = myList[i];
            var mx = mxList[i];
            var corr = corrList[i];
            var yMa = yMaList[i];
            var xMa = xMaList[i];
            var x = xList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var slope = mx != 0 ? corr * (my / mx) : 0;
            var inter = yMa - (slope * xMa);

            var prevReg = regList.LastOrDefault();
            var reg = (x * slope) + inter;
            regList.AddRounded(reg);

            var signal = GetCompareSignal(currentValue - reg, prevValue - prevReg);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "LinReg", regList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = regList;
        stockData.IndicatorName = IndicatorName.LinearRegressionLine;

        return stockData;
    }

    /// <summary>
    /// Calculates the IIR Least Squares Estimate
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateIIRLeastSquaresEstimate(this StockData stockData, int length = 100)
    {
        List<double> sList = new();
        List<double> sEmaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var a = (double)4 / (length + 2);
        var halfLength = MinOrMax((int)Math.Ceiling((double)length / 2));

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevS = i >= 1 ? sList[i - 1] : currentValue;
            var prevSEma = sEmaList.LastOrDefault();
            var sEma = CalculateEMA(prevS, prevSEma, halfLength);
            sEmaList.AddRounded(prevSEma);

            var s = (a * currentValue) + prevS - (a * sEma);
            sList.AddRounded(s);

            var signal = GetCompareSignal(currentValue - s, prevValue - prevS);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "IIRLse", sList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = sList;
        stockData.IndicatorName = IndicatorName.IIRLeastSquaresEstimate;

        return stockData;
    }

    /// <summary>
    /// Calculates the Inverse Distance Weighted Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateInverseDistanceWeightedMovingAverage(this StockData stockData, int length = 14)
    {
        List<double> idwmaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            double sum = 0, weightedSum = 0;
            for (var j = 0; j <= length - 1; j++)
            {
                var prevValue = i >= j ? inputList[i - j] : 0;

                double weight = 0;
                for (var k = 0; k <= length - 1; k++)
                {
                    var prevValue2 = i >= k ? inputList[i - k] : 0;
                    weight += Math.Abs(prevValue - prevValue2);
                }

                sum += prevValue * weight;
                weightedSum += weight;
            }

            var prevIdwma = idwmaList.LastOrDefault();
            var idwma = weightedSum != 0 ? sum / weightedSum : 0;
            idwmaList.AddRounded(idwma);

            var signal = GetCompareSignal(currentValue - idwma, prevVal - prevIdwma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Idwma", idwmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = idwmaList;
        stockData.IndicatorName = IndicatorName.InverseDistanceWeightedMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Trimean
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateTrimean(this StockData stockData, int length = 14)
    {
        List<double> tempList = new();
        List<double> medianList = new();
        List<double> q1List = new();
        List<double> q3List = new();
        List<double> trimeanList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevValue = tempList.LastOrDefault();
            var currentValue = inputList[i];
            tempList.AddRounded(currentValue);

            var lookBackList = tempList.TakeLastExt(length);

            var q1 = lookBackList.PercentileNearestRank(25);
            q1List.AddRounded(q1);

            var median = lookBackList.PercentileNearestRank(50);
            medianList.AddRounded(median);

            var q3 = lookBackList.PercentileNearestRank(75);
            q3List.AddRounded(q3);

            var prevTrimean = trimeanList.LastOrDefault();
            var trimean = (q1 + (2 * median) + q3) / 4;
            trimeanList.AddRounded(trimean);

            var signal = GetCompareSignal(currentValue - trimean, prevValue - prevTrimean);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Trimean", trimeanList },
            { "Q1", q1List },
            { "Median", medianList },
            { "Q3", q3List }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = trimeanList;
        stockData.IndicatorName = IndicatorName.Trimean;

        return stockData;
    }

    /// <summary>
    /// Calculates the Optimal Weighted Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateOptimalWeightedMovingAverage(this StockData stockData, int length = 14)
    {
        List<double> tempList = new();
        List<double> owmaList = new();
        List<double> prevOwmaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevVal = tempList.LastOrDefault();
            var currentValue = inputList[i];
            tempList.AddRounded(currentValue);

            var prevOwma = i >= 1 ? owmaList[i - 1] : 0;
            prevOwmaList.AddRounded(prevOwma);

            var corr = GoodnessOfFit.R(tempList.TakeLastExt(length).Select(x => (double)x), prevOwmaList.TakeLastExt(length).Select(x => (double)x));
            corr = IsValueNullOrInfinity(corr) ? 0 : corr;

            double sum = 0, weightedSum = 0;
            for (var j = 0; j <= length - 1; j++)
            {
                var weight = Pow(length - j, (double)corr);
                var prevValue = i >= j ? inputList[i - j] : 0;

                sum += prevValue * weight;
                weightedSum += weight;
            }

            var owma = weightedSum != 0 ? sum / weightedSum : 0;
            owmaList.AddRounded(owma);

            var signal = GetCompareSignal(currentValue - owma, prevVal - prevOwma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Owma", owmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = owmaList;
        stockData.IndicatorName = IndicatorName.OptimalWeightedMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Overshoot Reduction Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateOvershootReductionMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 14)
    {
        List<double> indexList = new();
        List<double> bList = new();
        List<double> dList = new();
        List<double> bSmaList = new();
        List<double> corrList = new();
        List<double> tempList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var length1 = (int)Math.Ceiling((double)length / 2);

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

        var indexSmaList = GetMovingAverageList(stockData, maType, length, indexList);
        var smaList = GetMovingAverageList(stockData, maType, length, inputList);
        var stdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        stockData.CustomValuesList = indexList;
        var indexStdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var index = indexList[i];
            var indexSma = indexSmaList[i];
            var indexStdDev = indexStdDevList[i];
            var corr = corrList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevD = i >= 1 ? dList[i - 1] != 0 ? dList[i - 1] : prevValue : prevValue;
            var sma = smaList[i];
            var stdDev = stdDevList[i];
            var a = indexStdDev != 0 && corr != 0 ? (index - indexSma) / indexStdDev * corr : 0;

            var b = Math.Abs(prevD - currentValue);
            bList.AddRounded(b);

            var bSma = bList.TakeLastExt(length1).Average();
            bSmaList.AddRounded(bSma);

            var highest = bSmaList.TakeLastExt(length).Max();
            var c = highest != 0 ? b / highest : 0;

            var d = sma + (a * (stdDev * c));
            dList.AddRounded(d);

            var signal = GetCompareSignal(currentValue - d, prevValue - prevD);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Orma", dList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = dList;
        stockData.IndicatorName = IndicatorName.OvershootReductionMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Variable Index Dynamic Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateVariableIndexDynamicAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 14)
    {
        List<double> vidyaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var alpha = (double)2 / (length + 1);

        var cmoList = CalculateChandeMomentumOscillator(stockData, maType, length: length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentCmo = Math.Abs(cmoList[i] / 100);
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevVidya = vidyaList.LastOrDefault();
            var currentVidya = (currentValue * alpha * currentCmo) + (prevVidya * (1 - (alpha * currentCmo)));
            vidyaList.AddRounded(currentVidya);

            var signal = GetCompareSignal(currentValue - currentVidya, prevValue - prevVidya);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Vidya", vidyaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = vidyaList;
        stockData.IndicatorName = IndicatorName.VariableIndexDynamicAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Natural Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateNaturalMovingAverage(this StockData stockData, int length = 40)
    {
        List<double> lnList = new();
        List<double> nmaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var ln = currentValue > 0 ? Math.Log(currentValue) * 1000 : 0;
            lnList.AddRounded(ln);

            double num = 0, denom = 0;
            for (var j = 0; j < length; j++)
            {
                var currentLn = i >= j ? lnList[i - j] : 0;
                var prevLn = i >= j + 1 ? lnList[i - (j + 1)] : 0;
                var oi = Math.Abs(currentLn - prevLn);
                num += oi * (Sqrt(j + 1) - Sqrt(j));
                denom += oi;
            }

            var ratio = denom != 0 ? num / denom : 0;
            var prevNma = nmaList.LastOrDefault();
            var nma = (currentValue * ratio) + (prevValue * (1 - ratio));
            nmaList.AddRounded(nma);

            var signal = GetCompareSignal(currentValue - nma, prevValue - prevNma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Nma", nmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = nmaList;
        stockData.IndicatorName = IndicatorName.NaturalMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Symmetrically Weighted Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateSymmetricallyWeightedMovingAverage(this StockData stockData, int length = 14)
    {
        List<double> swmaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var floorLength = (int)Math.Floor((double)length / 2);
        var roundLength = (int)Math.Round((double)length / 2);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            double nr = 0, nl = 0, sr = 0, sl = 0;
            if (floorLength == roundLength)
            {
                for (var j = 0; j <= floorLength - 1; j++)
                {
                    double wr = (length - (length - 1 - j)) * length;
                    var prevVal = i >= j ? inputList[i - j] : 0;
                    nr += wr;
                    sr += prevVal * wr;
                }

                for (var j = floorLength; j <= length - 1; j++)
                {
                    double wl = (length - j) * length;
                    var prevVal = i >= j ? inputList[i - j] : 0;
                    nl += wl;
                    sl += prevVal * wl;
                }
            }
            else
            {
                for (var j = 0; j <= floorLength; j++)
                {
                    double wr = (length - (length - 1 - j)) * length;
                    var prevVal = i >= j ? inputList[i - j] : 0;
                    nr += wr;
                    sr += prevVal * wr;
                }

                for (var j = roundLength; j <= length - 1; j++)
                {
                    double wl = (length - j) * length;
                    var prevVal = i >= j ? inputList[i - j] : 0;
                    nl += wl;
                    sl += prevVal * wl;
                }
            }

            var prevSwma = swmaList.LastOrDefault();
            var swma = nr + nl != 0 ? (sr + sl) / (nr + nl) : 0;
            swmaList.AddRounded(swma);

            var signal = GetCompareSignal(currentValue - swma, prevValue - prevSwma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Swma", swmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = swmaList;
        stockData.IndicatorName = IndicatorName.SymmetricallyWeightedMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Generalized Double Exponential Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="factor"></param>
    /// <returns></returns>
    public static StockData CalculateGeneralizedDoubleExponentialMovingAverage(this StockData stockData,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 5, double factor = 0.7)
    {
        List<double> gdList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var ema1List = GetMovingAverageList(stockData, maType, length, inputList);
        var ema2List = GetMovingAverageList(stockData, maType, length, ema1List);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentEma1 = ema1List[i];
            var currentEma2 = ema2List[i];

            var prevGd = gdList.LastOrDefault();
            var gd = (currentEma1 * (1 + factor)) - (currentEma2 * factor);
            gdList.AddRounded(gd);

            var signal = GetCompareSignal(currentValue - gd, prevValue - prevGd);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Gdema", gdList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = gdList;
        stockData.IndicatorName = IndicatorName.GeneralizedDoubleExponentialMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the General Filter Estimator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="beta"></param>
    /// <param name="gamma"></param>
    /// <param name="zeta"></param>
    /// <returns></returns>
    public static StockData CalculateGeneralFilterEstimator(this StockData stockData, int length = 100, double beta = 5.25, double gamma = 1,
        double zeta = 1)
    {
        List<double> dList = new();
        List<double> bList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var p = beta != 0 ? (int)Math.Ceiling(length / beta) : 0;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var priorB = i >= p ? bList[i - p] : currentValue;
            var a = currentValue - priorB;

            var prevB = i >= 1 ? bList[i - 1] : currentValue;
            var b = prevB + (a / p * gamma);
            bList.AddRounded(b);

            var priorD = i >= p ? dList[i - p] : b;
            var c = b - priorD;

            var prevD = i >= 1 ? dList[i - 1] : currentValue;
            var d = prevD + (((zeta * a) + ((1 - zeta) * c)) / p * gamma);
            dList.AddRounded(d);

            var signal = GetCompareSignal(currentValue - d, prevValue - prevD);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Gfe", dList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = dList;
        stockData.IndicatorName = IndicatorName.GeneralFilterEstimator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Henderson Weighted Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateHendersonWeightedMovingAverage(this StockData stockData, int length = 7)
    {
        List<double> hwmaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var termMult = MinOrMax((int)Math.Floor((double)(length - 1) / 2));

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            double sum = 0, weightedSum = 0;
            for (var j = 0; j <= length - 1; j++)
            {
                var m = termMult;
                var n = j - termMult;
                var numerator = 315 * (Pow(m + 1, 2) - Pow(n, 2)) * (Pow(m + 2, 2) - Pow(n, 2)) * (Pow(m + 3, 2) -
                    Pow(n, 2)) * ((3 * Pow(m + 2, 2)) - (11 * Pow(n, 2)) - 16);
                var denominator = 8 * (m + 2) * (Pow(m + 2, 2) - 1) * ((4 * Pow(m + 2, 2)) - 1) * ((4 * Pow(m + 2, 2)) - 9) *
                                  ((4 * Pow(m + 2, 2)) - 25);
                var weight = denominator != 0 ? numerator / denominator : 0;
                var prevValue = i >= j ? inputList[i - j] : 0;

                sum += prevValue * weight;
                weightedSum += weight;
            }

            var prevHwma = hwmaList.LastOrDefault();
            var hwma = weightedSum != 0 ? sum / weightedSum : 0;
            hwmaList.AddRounded(hwma);

            var signal = GetCompareSignal(currentValue - hwma, prevVal - prevHwma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Hwma", hwmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = hwmaList;
        stockData.IndicatorName = IndicatorName.HendersonWeightedMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Holt Exponential Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="alphaLength"></param>
    /// <param name="gammaLength"></param>
    /// <returns></returns>
    public static StockData CalculateHoltExponentialMovingAverage(this StockData stockData, int alphaLength = 20, int gammaLength = 20)
    {
        List<double> hemaList = new();
        List<double> bList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var alpha = (double)2 / (alphaLength + 1);
        var gamma = (double)2 / (gammaLength + 1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevB = i >= 1 ? bList[i - 1] : currentValue;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevHema = hemaList.LastOrDefault();
            var hema = ((1 - alpha) * (prevHema + prevB)) + (alpha * currentValue);
            hemaList.AddRounded(hema);

            var b = ((1 - gamma) * prevB) + (gamma * (hema - prevHema));
            bList.AddRounded(b);

            var signal = GetCompareSignal(currentValue - hema, prevValue - prevHema);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Hema", hemaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = hemaList;
        stockData.IndicatorName = IndicatorName.HoltExponentialMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Hull Estimate
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateHullEstimate(this StockData stockData, int length = 50)
    {
        List<double> hemaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var maLength = MinOrMax((int)Math.Ceiling((double)length / 2));

        var wmaList = GetMovingAverageList(stockData, MovingAvgType.WeightedMovingAverage, maLength, inputList);
        var emaList = GetMovingAverageList(stockData, MovingAvgType.ExponentialMovingAverage, maLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentWma = wmaList[i];
            var currentEma = emaList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevHema = hemaList.LastOrDefault();
            var hema = (3 * currentWma) - (2 * currentEma);
            hemaList.AddRounded(hema);

            var signal = GetCompareSignal(currentValue - hema, prevValue - prevHema);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "He", hemaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = hemaList;
        stockData.IndicatorName = IndicatorName.HullEstimate;

        return stockData;
    }

    /// <summary>
    /// Calculates the Hampel Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="scalingFactor"></param>
    /// <returns></returns>
    public static StockData CalculateHampelFilter(this StockData stockData, int length = 14, double scalingFactor = 3)
    {
        List<double> tempList = new();
        List<double> absDiffList = new();
        List<double> hfList = new();
        List<double> hfEmaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var alpha = (double)2 / (length + 1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevValue = tempList.LastOrDefault();
            var currentValue = inputList[i];
            tempList.AddRounded(currentValue);

            var sampleMedian = tempList.TakeLastExt(length).Median();
            var absDiff = Math.Abs(currentValue - sampleMedian);
            absDiffList.AddRounded(absDiff);

            var mad = absDiffList.TakeLastExt(length).Median();
            var hf = absDiff <= scalingFactor * mad ? currentValue : sampleMedian;
            hfList.AddRounded(hf);

            var prevHfEma = hfEmaList.LastOrDefault();
            var hfEma = (alpha * hf) + ((1 - alpha) * prevHfEma);
            hfEmaList.AddRounded(hfEma);

            var signal = GetCompareSignal(currentValue - hfEma, prevValue - prevHfEma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Hf", hfEmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = hfEmaList;
        stockData.IndicatorName = IndicatorName.HampelFilter;

        return stockData;
    }

    /// <summary>
    /// Calculates the Hybrid Convolution Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateHybridConvolutionFilter(this StockData stockData, int length = 14)
    {
        List<double> outputList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            var prevOutput = i >= 1 ? outputList[i - 1] : currentValue;
            double output = 0;
            for (var j = 1; j <= length; j++)
            {
                var sign = 0.5 * (1 - Math.Cos(MinOrMax((double)j / length * Math.PI, 0.99, 0.01)));
                var d = sign - (0.5 * (1 - Math.Cos(MinOrMax((double)(j - 1) / length, 0.99, 0.01))));
                var prevValue = i >= j - 1 ? inputList[i - (j - 1)] : 0;
                output += ((sign * prevOutput) + ((1 - sign) * prevValue)) * d;
            }
            outputList.AddRounded(output);

            var signal = GetCompareSignal(currentValue - output, prevVal - prevOutput);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Hcf", outputList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = outputList;
        stockData.IndicatorName = IndicatorName.HybridConvolutionFilter;

        return stockData;
    }

    /// <summary>
    /// Calculates the Fibonacci Weighted Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateFibonacciWeightedMovingAverage(this StockData stockData, int length = 14)
    {
        List<double> fibonacciWmaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var phi = (1 + Sqrt(5)) / 2;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            double sum = 0, weightedSum = 0;
            for (var j = 0; j <= length - 1; j++)
            {
                var pow = Pow(phi, length - j);
                var weight = (pow - (Pow(-1, j) / pow)) / Sqrt(5);
                var prevValue = i >= j ? inputList[i - j] : 0;

                sum += prevValue * weight;
                weightedSum += weight;
            }

            var prevFwma = fibonacciWmaList.LastOrDefault();
            var fwma = weightedSum != 0 ? sum / weightedSum : 0;
            fibonacciWmaList.AddRounded(fwma);

            var signal = GetCompareSignal(currentValue - fwma, prevVal - prevFwma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Fwma", fibonacciWmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = fibonacciWmaList;
        stockData.IndicatorName = IndicatorName.FibonacciWeightedMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Farey Sequence Weighted Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateFareySequenceWeightedMovingAverage(this StockData stockData, int length = 5)
    {
        List<double> fswmaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var array = new double[4] { 0, 1, 1, length };
        List<double> resList = new();

        while (array[2] <= length)
        {
            var a = array[0];
            var b = array[1];
            var c = array[2];
            var d = array[3];
            var k = Math.Floor((length + b) / array[3]);

            array[0] = c;
            array[1] = d;
            array[2] = (k * c) - a;
            array[3] = (k * d) - b;

            var res = array[1] != 0 ? Math.Round(array[0] / array[1], 3) : 0;
            resList.Insert(0, res);
        }

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            double sum = 0, weightedSum = 0;
            for (var j = 0; j < resList.Count; j++)
            {
                var prevValue = i >= j ? inputList[i - j] : 0;
                var weight = resList[j];

                sum += prevValue * weight;
                weightedSum += weight;
            }

            var prevFswma = fswmaList.LastOrDefault();
            var fswma = weightedSum != 0 ? sum / weightedSum : 0;
            fswmaList.AddRounded(fswma);

            var signal = GetCompareSignal(currentValue - fswma, prevVal - prevFswma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Fswma", fswmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = fswmaList;
        stockData.IndicatorName = IndicatorName.FareySequenceWeightedMovingAverage;

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
        List<double> filterList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        var halfP = MinOrMax((int)Math.Ceiling((double)length / 2));

        var (highestList1, lowestList1) = GetMaxAndMinValuesList(highList, lowList, length);
        var (highestList2, lowestList2) = GetMaxAndMinValuesList(highList, lowList, halfP);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevFilter = i >= 1 ? filterList.LastOrDefault() : currentValue;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var highestHigh1 = highestList1[i];
            var lowestLow1 = lowestList1[i];
            var highestHigh2 = highestList2[i];
            var lowestLow2 = lowestList2[i];
            var highestHigh3 = highestList2[Math.Max(i - halfP, i)];
            var lowestLow3 = lowestList2[Math.Max(i - halfP, i)];
            var n3 = (highestHigh1 - lowestLow1) / length;
            var n1 = (highestHigh2 - lowestLow2) / halfP;
            var n2 = (highestHigh3 - lowestLow3) / halfP;
            var dm = n1 > 0 && n2 > 0 && n3 > 0 ? (Math.Log(n1 + n2) - Math.Log(n3)) / Math.Log(2) : 0;

            var alpha = MinOrMax(Exp(-4.6 * (dm - 1)), 1, 0.01);
            var filter = (alpha * currentValue) + ((1 - alpha) * prevFilter);
            filterList.AddRounded(filter);

            var signal = GetCompareSignal(currentValue - filter, prevValue - prevFilter);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Fama", filterList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = filterList;
        stockData.IndicatorName = IndicatorName.EhlersFractalAdaptiveMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Falling Rising Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateFallingRisingFilter(this StockData stockData, int length = 14)
    {
        List<double> tempList = new();
        List<double> aList = new();
        List<double> errorList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var alpha = (double)2 / (length + 1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevA = i >= 1 ? aList[i - 1] : 0;
            var prevError = i >= 1 ? errorList[i - 1] : 0;

            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            tempList.AddRounded(prevValue);

            var lbList = tempList.TakeLastExt(length).ToList();
            var beta = currentValue > lbList.Max() || currentValue < lbList.Min() ? 1 : alpha;
            var a = prevA + (alpha * prevError) + (beta * prevError);
            aList.AddRounded(a);

            var error = currentValue - a;
            errorList.AddRounded(error);

            var signal = GetCompareSignal(error, prevError);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Frf", aList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = aList;
        stockData.IndicatorName = IndicatorName.FallingRisingFilter;

        return stockData;
    }

    /// <summary>
    /// Calculates the Fisher Least Squares Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateFisherLeastSquaresMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 100)
    {
        List<double> bList = new();
        List<double> indexList = new();
        List<double> diffList = new();
        List<double> absDiffList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var stdDevSrcList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        var smaSrcList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            double index = i;
            indexList.AddRounded(index);
        }

        stockData.CustomValuesList = indexList;
        var indexStdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        var indexSmaList = GetMovingAverageList(stockData, maType, length, indexList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var stdDevSrc = stdDevSrcList[i];
            var indexStdDev = indexStdDevList[i];
            var currentValue = inputList[i];
            var prevB = i >= 1 ? bList[i - 1] : currentValue;
            var indexSma = indexSmaList[i];
            var sma = smaSrcList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var diff = currentValue - prevB;
            diffList.AddRounded(diff);

            var absDiff = Math.Abs(diff);
            absDiffList.AddRounded(absDiff);

            var e = absDiffList.TakeLastExt(length).Average();
            var z = e != 0 ? diffList.TakeLastExt(length).Average() / e : 0;
            var r = Exp(2 * z) + 1 != 0 ? (Exp(2 * z) - 1) / (Exp(2 * z) + 1) : 0;
            var a = indexStdDev != 0 && r != 0 ? (i - indexSma) / indexStdDev * r : 0;

            var b = sma + (a * stdDevSrc);
            bList.AddRounded(b);

            var signal = GetCompareSignal(currentValue - b, prevValue - prevB);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Flsma", bList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = bList;
        stockData.IndicatorName = IndicatorName.FisherLeastSquaresMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Kaufman Adaptive Least Squares Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateKaufmanAdaptiveLeastSquaresMovingAverage(this StockData stockData,
        MovingAvgType maType = MovingAvgType.KaufmanAdaptiveMovingAverage, int length = 100)
    {
        List<double> kalsmaList = new();
        List<double> indexList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var kamaList = CalculateKaufmanAdaptiveCorrelationOscillator(stockData, maType, length);
        var indexStList = kamaList.OutputValues["IndexSt"];
        var srcStList = kamaList.OutputValues["SrcSt"];
        var rList = kamaList.OutputValues["Kaco"];
        var srcMaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            double index = i;
            indexList.AddRounded(index);
        }

        var indexMaList = GetMovingAverageList(stockData, maType, length, indexList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var indexSt = indexStList[i];
            var srcSt = srcStList[i];
            var srcMa = srcMaList[i];
            var indexMa = indexMaList[i];
            var r = rList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var alpha = indexSt != 0 ? srcSt / indexSt * r : 0;
            var beta = srcMa - (alpha * indexMa);

            var prevKalsma = kalsmaList.LastOrDefault();
            var kalsma = (alpha * i) + beta;
            kalsmaList.AddRounded(kalsma);

            var signal = GetCompareSignal(currentValue - kalsma, prevValue - prevKalsma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Kalsma", kalsmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = kalsmaList;
        stockData.IndicatorName = IndicatorName.KaufmanAdaptiveLeastSquaresMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Kalman Smoother
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateKalmanSmoother(this StockData stockData, int length = 200)
    {
        List<double> veloList = new();
        List<double> kfList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevKf = i >= 1 ? kfList[i - 1] : currentValue;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var dk = currentValue - prevKf;
            var smooth = prevKf + (dk * Sqrt((double)length / 10000 * 2));

            var prevVelo = i >= 1 ? veloList[i - 1] : 0;
            var velo = prevVelo + ((double)length / 10000 * dk);
            veloList.AddRounded(velo);

            var kf = smooth + velo;
            kfList.AddRounded(kf);

            var signal = GetCompareSignal(currentValue - kf, prevValue - prevKf);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ks", kfList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = kfList;
        stockData.IndicatorName = IndicatorName.KalmanSmoother;

        return stockData;
    }

    /// <summary>
    /// Calculates the Volume Adjusted Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="factor"></param>
    /// <returns></returns>
    public static StockData CalculateVolumeAdjustedMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 14, double factor = 0.67)
    {
        List<double> priceVolumeRatioList = new();
        List<double> priceVolumeRatioSumList = new();
        List<double> vamaList = new();
        List<double> volumeRatioList = new();
        List<double> volumeRatioSumList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);

        var volumeSmaList = GetMovingAverageList(stockData, maType, length, volumeList); ;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentVolume = volumeList[i];
            var currentValue = inputList[i];
            var volumeSma = volumeSmaList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var volumeIncrement = volumeSma * factor;

            var volumeRatio = volumeIncrement != 0 ? currentVolume / volumeIncrement : 0;
            volumeRatioList.AddRounded(volumeRatio);

            var priceVolumeRatio = currentValue * volumeRatio;
            priceVolumeRatioList.AddRounded(priceVolumeRatio);

            var volumeRatioSum = volumeRatioList.TakeLastExt(length).Sum();
            volumeRatioSumList.AddRounded(volumeRatioSum);

            var priceVolumeRatioSum = priceVolumeRatioList.TakeLastExt(length).Sum();
            priceVolumeRatioSumList.AddRounded(priceVolumeRatioSum);

            var prevVama = vamaList.LastOrDefault();
            var vama = volumeRatioSum != 0 ? priceVolumeRatioSum / volumeRatioSum : 0;
            vamaList.AddRounded(vama);

            var signal = GetCompareSignal(currentValue - vama, prevValue - prevVama);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Vama", vamaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = vamaList;
        stockData.IndicatorName = IndicatorName.VolumeAdjustedMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Volatility Wave Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="kf"></param>
    /// <returns></returns>
    public static StockData CalculateVolatilityWaveMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length = 20, double kf = 2.5)
    {
        List<double> zlmapList = new();
        List<double> pmaList = new();
        List<double> pList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var s = MinOrMax((int)Math.Ceiling(Sqrt(length)));

        var stdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var stdDev = stdDevList[i];
            var currentValue = inputList[i];
            var sdPct = currentValue != 0 ? stdDev / currentValue * 100 : 0;

            var p = sdPct >= 0 ? MinOrMax(Sqrt(sdPct) * kf, 4, 1) : 1;
            pList.AddRounded(p);
        }

        for (var i = 0; i < stockData.Count; i++)
        {
            var p = pList[i];

            double sum = 0, weightedSum = 0;
            for (var j = 0; j <= length - 1; j++)
            {
                var weight = Pow(length - j, p);
                var prevValue = i >= j ? inputList[i - j] : 0;

                sum += prevValue * weight;
                weightedSum += weight;
            }

            var pma = weightedSum != 0 ? sum / weightedSum : 0;
            pmaList.AddRounded(pma);
        }

        var wmap1List = GetMovingAverageList(stockData, maType, s, pmaList);
        var wmap2List = GetMovingAverageList(stockData, maType, s, wmap1List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var wmap1 = wmap1List[i];
            var wmap2 = wmap2List[i];

            var prevZlmap = zlmapList.LastOrDefault();
            var zlmap = (2 * wmap1) - wmap2;
            zlmapList.AddRounded(zlmap);

            var signal = GetCompareSignal(currentValue - zlmap, prevValue - prevZlmap);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Vwma", zlmapList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = zlmapList;
        stockData.IndicatorName = IndicatorName.VolatilityWaveMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Variable Adaptive Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateVariableAdaptiveMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 14)
    {
        List<double> vmaList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, openList, _) = GetInputValuesList(stockData);

        var cList = GetMovingAverageList(stockData, maType, length, inputList);
        var oList = GetMovingAverageList(stockData, maType, length, openList);
        var hList = GetMovingAverageList(stockData, maType, length, highList);
        var lList = GetMovingAverageList(stockData, maType, length, lowList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var c = cList[i];
            var o = oList[i];
            var h = hList[i];
            var l = lList[i];
            var lv = h - l != 0 ? MinOrMax(Math.Abs(c - o) / (h - l), 0.99, 0.01) : 0;

            var prevVma = i >= 1 ? vmaList[i - 1] : currentValue;
            var vma = (lv * currentValue) + ((1 - lv) * prevVma);
            vmaList.AddRounded(vma);

            var signal = GetCompareSignal(currentValue - vma, prevValue - prevVma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Vama", vmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = vmaList;
        stockData.IndicatorName = IndicatorName.VariableAdaptiveMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Variable Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateVariableMovingAverage(this StockData stockData, int length = 6)
    {
        List<double> vmaList = new();
        List<double> pdmsList = new();
        List<double> pdisList = new();
        List<double> mdmsList = new();
        List<double> mdisList = new();
        List<double> isList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var k = (double)1 / length;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var pdm = Math.Max(MinPastValues(i, 1, currentValue - prevValue), 0);
            var mdm = Math.Max(MinPastValues(i, 1, prevValue - currentValue), 0);

            var prevPdms = pdmsList.LastOrDefault();
            var pdmS = ((1 - k) * prevPdms) + (k * pdm);
            pdmsList.AddRounded(pdmS);

            var prevMdms = mdmsList.LastOrDefault();
            var mdmS = ((1 - k) * prevMdms) + (k * mdm);
            mdmsList.AddRounded(mdmS);

            var s = pdmS + mdmS;
            var pdi = s != 0 ? pdmS / s : 0;
            var mdi = s != 0 ? mdmS / s : 0;

            var prevPdis = pdisList.LastOrDefault();
            var pdiS = ((1 - k) * prevPdis) + (k * pdi);
            pdisList.AddRounded(pdiS);

            var prevMdis = mdisList.LastOrDefault();
            var mdiS = ((1 - k) * prevMdis) + (k * mdi);
            mdisList.AddRounded(mdiS);

            var d = Math.Abs(pdiS - mdiS);
            var s1 = pdiS + mdiS;
            var dS1 = s1 != 0 ? d / s1 : 0;

            var prevIs = isList.LastOrDefault();
            var iS = ((1 - k) * prevIs) + (k * dS1);
            isList.AddRounded(iS);

            var lbList = isList.TakeLastExt(length).ToList();
            var hhv = lbList.Max();
            var llv = lbList.Min();
            var d1 = hhv - llv;
            var vI = d1 != 0 ? (iS - llv) / d1 : 0;

            var prevVma = vmaList.LastOrDefault();
            var vma = ((1 - k) * vI * prevVma) + (k * vI * currentValue);
            vmaList.AddRounded(vma);

            var signal = GetCompareSignal(currentValue - vma, prevValue - prevVma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Vma", vmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = vmaList;
        stockData.IndicatorName = IndicatorName.VariableMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Volatility Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="lbLength"></param>
    /// <param name="smoothLength"></param>
    /// <param name="stdDevMult"></param>
    /// <returns></returns>
    public static StockData CalculateVolatilityMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 20, int lbLength = 10, int smoothLength = 3)
    {
        List<double> kList = new();
        List<double> vma1List = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, lbLength, inputList);
        var stdDevList = CalculateStandardDeviationVolatility(stockData, maType, lbLength).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var sma = smaList[i];
            var currentValue = inputList[i];
            var dev = stdDevList[i];
            var upper = sma + dev;
            var lower = sma - dev;

            var k = upper - lower != 0 ? (currentValue - sma) / (upper - lower) * 100 * 2 : 0;
            kList.AddRounded(k);
        }

        var kMaList = GetMovingAverageList(stockData, maType, smoothLength, kList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var kMa = kMaList[i];
            var kNorm = Math.Min(Math.Max(kMa, -100), 100);
            var kAbs = Math.Round(Math.Abs(kNorm) / lbLength);
            var kRescaled = RescaleValue(kAbs, 10, 0, length, 0, true);
            var vLength = (int)Math.Round(Math.Max(kRescaled, 1));

            double sum = 0, weightedSum = 0;
            for (var j = 0; j <= vLength - 1; j++)
            {
                double weight = vLength - j;
                var prevValue = i >= j ? inputList[i - j] : 0;

                sum += prevValue * weight;
                weightedSum += weight;
            }

            var vma1 = weightedSum != 0 ? sum / weightedSum : 0;
            vma1List.AddRounded(vma1);
        }

        var vma2List = GetMovingAverageList(stockData, maType, smoothLength, vma1List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var vma = vma2List[i];
            var prevVma = i >= 1 ? vma2List[i - 1] : 0;

            var signal = GetCompareSignal(currentValue - vma, prevValue - prevVma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Vma", vma2List }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = vma2List;
        stockData.IndicatorName = IndicatorName.VolatilityMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Vertical Horizontal Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateVerticalHorizontalMovingAverage(this StockData stockData, int length = 50)
    {
        List<double> changeList = new();
        List<double> vhmaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(inputList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var priorValue = i >= length ? inputList[i - length] : 0;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var highest = highestList[i];
            var lowest = lowestList[i];

            var priceChange = Math.Abs(currentValue - priorValue);
            changeList.AddRounded(priceChange);

            var numerator = highest - lowest;
            var denominator = changeList.TakeLastExt(length).Sum();
            var vhf = denominator != 0 ? numerator / denominator : 0;

            var prevVhma = vhmaList.LastOrDefault();
            var vhma = prevVhma + (Pow(vhf, 2) * (currentValue - prevVhma));
            vhmaList.AddRounded(vhma);

            var signal = GetCompareSignal(currentValue - vhma, prevValue - prevVhma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Vhma", vhmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = vhmaList;
        stockData.IndicatorName = IndicatorName.VerticalHorizontalMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the McNicholl Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateMcNichollMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 20)
    {
        List<double> mnmaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var alpha = (double)2 / (length + 1);

        var ema1List = GetMovingAverageList(stockData, maType, length, inputList);
        var ema2List = GetMovingAverageList(stockData, maType, length, ema1List);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var ema1 = ema1List[i];
            var ema2 = ema2List[i];

            var prevMnma = mnmaList.LastOrDefault();
            var mnma = 1 - alpha != 0 ? (((2 - alpha) * ema1) - ema2) / (1 - alpha) : 0;
            mnmaList.AddRounded(mnma);

            var signal = GetCompareSignal(currentValue - mnma, prevValue - prevMnma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Mnma", mnmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = mnmaList;
        stockData.IndicatorName = IndicatorName.McNichollMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Compound Ratio Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateCompoundRatioMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage, 
        int length = 20)
    {
        List<double> coraRawList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var r = Pow(length, ((double)1 / (length - 1)) - 1);
        var smoothLength = Math.Max((int)Math.Round(Math.Sqrt(length)), 1);

        for (var i = 0; i < stockData.Count; i++)
        {
            double sum = 0, weightedSum = 0, bas = 1 + (r * 2);
            for (var j = 0; j <= length - 1; j++)
            {
                var weight = Pow(bas, length - i);
                var prevValue = i >= j ? inputList[i - j] : 0;

                sum += prevValue * weight;
                weightedSum += weight;
            }

            var coraRaw = weightedSum != 0 ? sum / weightedSum : 0;
            coraRawList.AddRounded(coraRaw);
        }

        var coraWaveList = GetMovingAverageList(stockData, maType, smoothLength, coraRawList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var coraWave = coraWaveList[i];
            var prevCoraWave = i >= 1 ? coraWaveList[i - 1] : 0;

            var signal = GetCompareSignal(currentValue - coraWave, prevValue - prevCoraWave);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Crma", coraWaveList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = coraWaveList;
        stockData.IndicatorName = IndicatorName.CompoundRatioMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Cubed Weighted Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateCubedWeightedMovingAverage(this StockData stockData, int length = 14)
    {
        List<double> cwmaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            double sum = 0, weightedSum = 0;
            for (var j = 0; j <= length - 1; j++)
            {
                var weight = Pow(length - j, 3);
                var prevValue = i >= j ? inputList[i - j] : 0;

                sum += prevValue * weight;
                weightedSum += weight;
            }

            var prevCwma = cwmaList.LastOrDefault();
            var cwma = weightedSum != 0 ? sum / weightedSum : 0;
            cwmaList.AddRounded(cwma);

            var signal = GetCompareSignal(currentValue - cwma, prevVal - prevCwma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Cwma", cwmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = cwmaList;
        stockData.IndicatorName = IndicatorName.CubedWeightedMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Corrected Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateCorrectedMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 35)
    {
        List<double> cmaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);
        var v1List = CalculateStandardDeviationVolatility(stockData, maType, length).OutputValues["Variance"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var sma = smaList[i];
            var prevCma = i >= 1 ? cmaList[i - 1] : sma;
            var v1 = v1List[i];
            var v2 = Pow(prevCma - sma, 2);
            var v3 = v1 == 0 || v2 == 0 ? 1 : v2 / (v1 + v2);

            double tolerance = Pow(10, -5), err = 1, kPrev = 1, k = 1;
            for (var j = 0; j <= 5000; j++)
            {
                if (err > tolerance)
                {
                    k = v3 * kPrev * (2 - kPrev);
                    err = kPrev - k;
                    kPrev = k;
                }
            }

            var cma = prevCma + (k * (sma - prevCma));
            cmaList.AddRounded(cma);

            var signal = GetCompareSignal(currentValue - cma, prevValue - prevCma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Cma", cmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = cmaList;
        stockData.IndicatorName = IndicatorName.CorrectedMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Double Exponential Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateDoubleExponentialMovingAverage(this StockData stockData, 
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14)
    {
        List<double> demaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var ema1List = GetMovingAverageList(stockData, maType, length, inputList);
        var ema2List = GetMovingAverageList(stockData, maType, length, ema1List);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentEma = ema1List[i];
            var currentEma2 = ema2List[i];

            var prevDema = demaList.LastOrDefault();
            var dema = (2 * currentEma) - currentEma2;
            demaList.AddRounded(dema);

            var signal = GetCompareSignal(currentValue - dema, prevValue - prevDema);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Dema", demaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = demaList;
        stockData.IndicatorName = IndicatorName.DoubleExponentialMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Pentuple Exponential Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculatePentupleExponentialMovingAverage(this StockData stockData, 
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 20)
    {
        List<double> pemaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var ema1List = GetMovingAverageList(stockData, maType, length, inputList);
        var ema2List = GetMovingAverageList(stockData, maType, length, ema1List);
        var ema3List = GetMovingAverageList(stockData, maType, length, ema2List);
        var ema4List = GetMovingAverageList(stockData, maType, length, ema3List);
        var ema5List = GetMovingAverageList(stockData, maType, length, ema4List);
        var ema6List = GetMovingAverageList(stockData, maType, length, ema5List);
        var ema7List = GetMovingAverageList(stockData, maType, length, ema6List);
        var ema8List = GetMovingAverageList(stockData, maType, length, ema7List);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var ema1 = ema1List[i];
            var ema2 = ema2List[i];
            var ema3 = ema3List[i];
            var ema4 = ema4List[i];
            var ema5 = ema5List[i];
            var ema6 = ema6List[i];
            var ema7 = ema7List[i];
            var ema8 = ema8List[i];

            var prevPema = pemaList.LastOrDefault();
            var pema = (8 * ema1) - (28 * ema2) + (56 * ema3) - (70 * ema4) + (56 * ema5) - (28 * ema6) + (8 * ema7) - ema8;
            pemaList.AddRounded(pema);

            var signal = GetCompareSignal(currentValue - pema, prevValue - prevPema);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Pema", pemaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = pemaList;
        stockData.IndicatorName = IndicatorName.PentupleExponentialMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Polynomial Least Squares Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculatePolynomialLeastSquaresMovingAverage(this StockData stockData, int length = 100)
    {
        List<double> sumPow3List = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            var prevSumPow3 = sumPow3List.LastOrDefault();
            double x1Pow1Sum, x2Pow1Sum, x1Pow2Sum, x2Pow2Sum, x1Pow3Sum, x2Pow3Sum, wPow1, wPow2, wPow3, sumPow1 = 0, sumPow2 = 0, sumPow3 = 0;
            for (var j = 1; j <= length; j++)
            {
                var prevValue = i >= j - 1 ? inputList[i - (j - 1)] : 0;
                var x1 = (double)j / length;
                var x2 = (double)(j - 1) / length;
                var ax1 = x1 * x1;
                var ax2 = x2 * x2;

                double b1Pow1Sum = 0, b2Pow1Sum = 0, b1Pow2Sum = 0, b2Pow2Sum = 0, b1Pow3Sum = 0, b2Pow3Sum = 0;
                for (var k = 1; k <= 3; k++)
                {
                    var b1 = (double)1 / k * Math.Sin(x1 * k * Math.PI);
                    var b2 = (double)1 / k * Math.Sin(x2 * k * Math.PI);

                    b1Pow1Sum += k == 1 ? b1 : 0;
                    b2Pow1Sum += k == 1 ? b2 : 0;
                    b1Pow2Sum += k <= 2 ? b1 : 0;
                    b2Pow2Sum += k <= 2 ? b2 : 0;
                    b1Pow3Sum += k <= 3 ? b1 : 0; //-V3022
                    b2Pow3Sum += k <= 3 ? b2 : 0; //-V3022
                }

                x1Pow1Sum = ax1 + b1Pow1Sum;
                x2Pow1Sum = ax2 + b2Pow1Sum;
                wPow1 = x1Pow1Sum - x2Pow1Sum;
                sumPow1 += prevValue * wPow1;
                x1Pow2Sum = ax1 + b1Pow2Sum;
                x2Pow2Sum = ax2 + b2Pow2Sum;
                wPow2 = x1Pow2Sum - x2Pow2Sum;
                sumPow2 += prevValue * wPow2;
                x1Pow3Sum = ax1 + b1Pow3Sum;
                x2Pow3Sum = ax2 + b2Pow3Sum;
                wPow3 = x1Pow3Sum - x2Pow3Sum;
                sumPow3 += prevValue * wPow3;
            }
            sumPow3List.AddRounded(sumPow3);

            var signal = GetCompareSignal(currentValue - sumPow3, prevVal - prevSumPow3);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Plsma", sumPow3List }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = sumPow3List;
        stockData.IndicatorName = IndicatorName.PolynomialLeastSquaresMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Parametric Corrective Linear Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="alpha"></param>
    /// <param name="per"></param>
    /// <returns></returns>
    public static StockData CalculateParametricCorrectiveLinearMovingAverage(this StockData stockData, int length = 50, double alpha = 1, 
        double per = 35)
    {
        List<double> w1List = new();
        List<double> w2List = new();
        List<double> vw1List = new();
        List<double> vw2List = new();
        List<double> rrma1List = new();
        List<double> rrma2List = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevValue = i >= length ? inputList[i - length] : 0;
            var p1 = i + 1 - (per / 100 * length);
            var p2 = i + 1 - ((100 - per) / 100 * length);

            var w1 = p1 >= 0 ? p1 : alpha * p1;
            w1List.AddRounded(w1);

            var w2 = p2 >= 0 ? p2 : alpha * p2;
            w2List.AddRounded(w2);

            var vw1 = prevValue * w1;
            vw1List.AddRounded(vw1);

            var vw2 = prevValue * w2;
            vw2List.AddRounded(vw2);

            var wSum1 = w1List.TakeLastExt(length).Sum();
            var wSum2 = w2List.TakeLastExt(length).Sum();
            var sum1 = vw1List.TakeLastExt(length).Sum();
            var sum2 = vw2List.TakeLastExt(length).Sum();

            var prevRrma1 = rrma1List.LastOrDefault();
            var rrma1 = wSum1 != 0 ? sum1 / wSum1 : 0;
            rrma1List.AddRounded(rrma1);

            var prevRrma2 = rrma2List.LastOrDefault();
            var rrma2 = wSum2 != 0 ? sum2 / wSum2 : 0;
            rrma2List.AddRounded(rrma2);

            var signal = GetCompareSignal(rrma1 - rrma2, prevRrma1 - prevRrma2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Pclma", rrma1List }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = rrma1List;
        stockData.IndicatorName = IndicatorName.ParametricCorrectiveLinearMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Parabolic Weighted Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateParabolicWeightedMovingAverage(this StockData stockData, int length = 14)
    {
        List<double> pwmaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            double sum = 0, weightedSum = 0;
            for (var j = 0; j <= length - 1; j++)
            {
                var weight = Pow(length - j, 2);
                var prevValue = i >= j ? inputList[i - j] : 0;

                sum += prevValue * weight;
                weightedSum += weight;
            }

            var prevPwma = pwmaList.LastOrDefault();
            var pwma = weightedSum != 0 ? sum / weightedSum : 0;
            pwmaList.AddRounded(pwma);

            var signal = GetCompareSignal(currentValue - pwma, prevVal - prevPwma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Pwma", pwmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = pwmaList;
        stockData.IndicatorName = IndicatorName.ParabolicWeightedMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Parametric Kalman Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateParametricKalmanFilter(this StockData stockData, int length = 50)
    {
        List<double> errList = new();
        List<double> estList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var priorEst = i >= length ? estList[i - length] : prevValue;
            var errMea = Math.Abs(priorEst - currentValue);
            var errPrv = Math.Abs(MinPastValues(i, 1, currentValue - prevValue) * -1);
            var prevErr = i >= 1 ? errList[i - 1] : errPrv;
            var kg = prevErr != 0 ? prevErr / (prevErr + errMea) : 0;
            var prevEst = i >= 1 ? estList[i - 1] : prevValue;

            var est = prevEst + (kg * (currentValue - prevEst));
            estList.AddRounded(est);

            var err = (1 - kg) * errPrv;
            errList.AddRounded(err);

            var signal = GetCompareSignal(currentValue - est, prevValue - prevEst);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Pkf", estList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = estList;
        stockData.IndicatorName = IndicatorName.ParametricKalmanFilter;

        return stockData;
    }

    /// <summary>
    /// Calculates the T Step Least Squares Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="sc"></param>
    /// <returns></returns>
    public static StockData CalculateTStepLeastSquaresMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 100, double sc = 0.5)
    {
        List<double> lsList = new();
        List<double> bList = new();
        List<double> chgList = new();
        List<double> tempList = new();
        List<double> corrList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var efRatioList = CalculateKaufmanAdaptiveMovingAverage(stockData, length: length).OutputValues["Er"];
        var stdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            tempList.AddRounded(currentValue);

            var efRatio = efRatioList[i];
            var prevB = i >= 1 ? bList[i - 1] : currentValue;
            var er = 1 - efRatio;

            var chg = Math.Abs(currentValue - prevB);
            chgList.AddRounded(chg);

            var a = chgList.Average() * (1 + er);
            var b = currentValue > prevB + a ? currentValue : currentValue < prevB - a ? currentValue : prevB;
            bList.AddRounded(b);

            var corr = GoodnessOfFit.R(bList.TakeLastExt(length).Select(x => (double)x), tempList.TakeLastExt(length).Select(x => (double)x));
            corr = IsValueNullOrInfinity(corr) ? 0 : corr;
            corrList.AddRounded((double)corr);
        }

        stockData.CustomValuesList = bList;
        var bStdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        var bSmaList = GetMovingAverageList(stockData, maType, length, bList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var corr = corrList[i];
            var stdDev = stdDevList[i];
            var bStdDev = bStdDevList[i];
            var bSma = bSmaList[i];
            var sma = smaList[i];
            var currentValue = inputList[i];
            var prevLs = i >= 1 ? lsList[i - 1] : currentValue;
            var b = bList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var tslsma = (sc * currentValue) + ((1 - sc) * prevLs);
            var alpha = bStdDev != 0 ? corr * stdDev / bStdDev : 0;
            var beta = sma - (alpha * bSma);

            var ls = (alpha * b) + beta;
            lsList.AddRounded(ls);

            var signal = GetCompareSignal(currentValue - ls, prevValue - prevLs);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Tslsma", lsList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = lsList;
        stockData.IndicatorName = IndicatorName.TStepLeastSquaresMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Tillson IE2
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateTillsonIE2(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 15)
    {
        List<double> ie2List = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);
        var linRegList = CalculateLinearRegression(stockData, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var sma = smaList[i];
            var a0 = linRegList[i];
            var a1 = i >= 1 ? linRegList[i - 1] : 0;
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var m = a0 - a1 + sma;

            var prevIe2 = ie2List.LastOrDefault();
            var ie2 = (m + a0) / 2;
            ie2List.AddRounded(ie2);

            var signal = GetCompareSignal(currentValue - ie2, prevValue - prevIe2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ie2", ie2List }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = ie2List;
        stockData.IndicatorName = IndicatorName.TillsonIE2;

        return stockData;
    }

    /// <summary>
    /// Calculates the R2 Adaptive Regression
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateR2AdaptiveRegression(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 100)
    {
        List<double> outList = new();
        List<double> tempList = new();
        List<double> x2List = new();
        List<double> x2PowList = new();
        List<double> y1List = new();
        List<double> y2List = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var linregList = CalculateLinearRegression(stockData, length).CustomValuesList;
        var stdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var stdDev = stdDevList[i];
            var sma = smaList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var currentValue = inputList[i];
            tempList.AddRounded(currentValue);

            var lbList = tempList.TakeLastExt(length).Select(x => (double)x);
            var y1 = linregList[i];
            y1List.AddRounded(y1);

            var x2 = i >= 1 ? outList[i - 1] : currentValue;
            x2List.AddRounded(x2);

            var x2LbList = x2List.TakeLastExt(length).Select(x => (double)x).ToList();
            var r2x2 = GoodnessOfFit.R(x2LbList, lbList);
            r2x2 = IsValueNullOrInfinity(r2x2) ? 0 : r2x2;
            var x2Avg = (double)x2LbList.TakeLastExt(length).Average();
            var x2Dev = x2 - x2Avg;

            var x2Pow = Pow(x2Dev, 2);
            x2PowList.AddRounded(x2Pow);

            var x2PowAvg = x2PowList.TakeLastExt(length).Average();
            var x2StdDev = x2PowAvg >= 0 ? Sqrt(x2PowAvg) : 0;
            var a = x2StdDev != 0 ? stdDev * (double)r2x2 / x2StdDev : 0;
            var b = sma - (a * x2Avg);

            var y2 = (a * x2) + b;
            y2List.AddRounded(y2);

            var ry1 = Math.Pow(GoodnessOfFit.R(y1List.TakeLastExt(length).Select(x => (double)x), lbList), 2);
            ry1 = IsValueNullOrInfinity(ry1) ? 0 : ry1;
            var ry2 = Math.Pow(GoodnessOfFit.R(y2List.TakeLastExt(length).Select(x => (double)x), lbList), 2);
            ry2 = IsValueNullOrInfinity(ry2) ? 0 : ry2;

            var prevOutVal = outList.LastOrDefault();
            var outval = ((double)ry1 * y1) + ((double)ry2 * y2) + ((1 - (double)(ry1 + ry2)) * x2);
            outList.AddRounded(outval);

            var signal = GetCompareSignal(currentValue - outval, prevValue - prevOutVal);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "R2ar", outList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = outList;
        stockData.IndicatorName = IndicatorName.R2AdaptiveRegression;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ratio OCHL Averager
    /// </summary>
    /// <param name="stockData"></param>
    /// <returns></returns>
    public static StockData CalculateRatioOCHLAverager(this StockData stockData)
    {
        List<double> dList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, openList, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentOpen = openList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var b = currentHigh - currentLow != 0 ? Math.Abs(currentValue - currentOpen) / (currentHigh - currentLow) : 0;
            var c = b > 1 ? 1 : b;

            var prevD = i >= 1 ? dList[i - 1] : currentValue;
            var d = (c * currentValue) + ((1 - c) * prevD);
            dList.AddRounded(d);

            var signal = GetCompareSignal(currentValue - d, prevValue - prevD);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Rochla", dList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = dList;
        stockData.IndicatorName = IndicatorName.RatioOCHLAverager;

        return stockData;
    }

    /// <summary>
    /// Calculates the Regularized Exponential Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="lambda"></param>
    /// <returns></returns>
    public static StockData CalculateRegularizedExponentialMovingAverage(this StockData stockData, int length = 14, double lambda = 0.5)
    {
        List<double> remaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var alpha = (double)2 / (length + 1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevRema1 = i >= 1 ? remaList[i - 1] : 0;
            var prevRema2 = i >= 2 ? remaList[i - 2] : 0;

            var rema = (prevRema1 + (alpha * (currentValue - prevRema1)) + (lambda * ((2 * prevRema1) - prevRema2))) / (lambda + 1);
            remaList.AddRounded(rema);

            var signal = GetCompareSignal(currentValue - rema, prevValue - prevRema1);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Rema", remaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = remaList;
        stockData.IndicatorName = IndicatorName.RegularizedExponentialMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Repulsion Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateRepulsionMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 100)
    {
        List<double> maList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var sma1List = GetMovingAverageList(stockData, maType, length, inputList);
        var sma2List = GetMovingAverageList(stockData, maType, length * 2, inputList);
        var sma3List = GetMovingAverageList(stockData, maType, length * 3, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var sma1 = sma1List[i];
            var sma2 = sma2List[i];
            var sma3 = sma3List[i];

            var prevMa = maList.LastOrDefault();
            var ma = sma3 + sma2 - sma1;
            maList.AddRounded(ma);

            var signal = GetCompareSignal(currentValue - ma, prevValue - prevMa);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Rma", maList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = maList;
        stockData.IndicatorName = IndicatorName.RepulsionMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Retention Acceleration Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateRetentionAccelerationFilter(this StockData stockData, int length = 50)
    {
        List<double> altmaList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList1, lowestList1) = GetMaxAndMinValuesList(highList, lowList, length);
        var (highestList2, lowestList2) = GetMaxAndMinValuesList(highList, lowList, length * 2);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var highest1 = highestList1[i];
            var lowest1 = lowestList1[i];
            var highest2 = highestList2[i];
            var lowest2 = lowestList2[i];
            var ar = 2 * (highest1 - lowest1);
            var br = 2 * (highest2 - lowest2);
            var k1 = ar != 0 ? (1 - ar) / ar : 0;
            var k2 = br != 0 ? (1 - br) / br : 0;
            var alpha = k1 != 0 ? k2 / k1 : 0;
            var r1 = alpha != 0 && highest1 >= 0 ? Sqrt(highest1) / 4 * ((alpha - 1) / alpha) * (k2 / (k2 + 1)) : 0;
            var r2 = highest2 >= 0 ? Sqrt(highest2) / 4 * (alpha - 1) * (k1 / (k1 + 1)) : 0;
            var factor = r1 != 0 ? r2 / r1 : 0;
            var altk = Pow(factor >= 1 ? 1 : factor, Sqrt(length)) * ((double)1 / length);

            var prevAltma = i >= 1 ? altmaList[i - 1] : currentValue;
            var altma = (altk * currentValue) + ((1 - altk) * prevAltma);
            altmaList.AddRounded(altma);

            var signal = GetCompareSignal(currentValue - altma, prevValue - prevAltma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Raf", altmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = altmaList;
        stockData.IndicatorName = IndicatorName.RetentionAccelerationFilter;

        return stockData;
    }

    /// <summary>
    /// Calculates the Reverse Engineering Relative Strength Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="rsiLevel"></param>
    /// <returns></returns>
    public static StockData CalculateReverseEngineeringRelativeStrengthIndex(this StockData stockData, int length = 14, double rsiLevel = 50)
    {
        List<double> aucList = new();
        List<double> adcList = new();
        List<double> revRsiList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        double expPeriod = (2 * length) - 1;
        var k = 2 / (expPeriod + 1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevAuc = i >= 1 ? aucList[i - 1] : 1;
            var prevAdc = i >= 1 ? adcList[i - 1] : 1;

            var auc = currentValue > prevValue ? (k * MinPastValues(i, 1, currentValue - prevValue)) + ((1 - k) * prevAuc) : (1 - k) * prevAuc;
            aucList.AddRounded(auc);

            var adc = currentValue > prevValue ? ((1 - k) * prevAdc) : (k * MinPastValues(i, 1, prevValue - currentValue)) + ((1 - k) * prevAdc);
            adcList.AddRounded(adc);

            var rsiValue = (length - 1) * ((adc * rsiLevel / (100 - rsiLevel)) - auc);
            var prevRevRsi = revRsiList.LastOrDefault();
            var revRsi = rsiValue >= 0 ? currentValue + rsiValue : currentValue + (rsiValue * (100 - rsiLevel) / rsiLevel);
            revRsiList.AddRounded(revRsi);

            var signal = GetCompareSignal(currentValue - revRsi, prevValue - prevRevRsi);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Rersi", revRsiList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = revRsiList;
        stockData.IndicatorName = IndicatorName.ReverseEngineeringRelativeStrengthIndex;

        return stockData;
    }

    /// <summary>
    /// Calculates the Right Sided Ricker Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="pctWidth"></param>
    /// <returns></returns>
    public static StockData CalculateRightSidedRickerMovingAverage(this StockData stockData, int length = 50, double pctWidth = 60)
    {
        List<double> rrmaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var width = pctWidth / 100 * length;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            double w = 0, vw = 0;
            for (var j = 0; j < length; j++)
            {
                var prevV = i >= j ? inputList[i - j] : 0;
                w += (1 - Pow(j / width, 2)) * Exp(-(Pow(j, 2) / (2 * Pow(width, 2))));
                vw += prevV * w;
            }
            
            var prevRrma = rrmaList.LastOrDefault();
            var rrma = w != 0 ? vw / w : 0;
            rrmaList.AddRounded(rrma);

            var signal = GetCompareSignal(currentValue - rrma, prevValue - prevRrma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Rsrma", rrmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = rrmaList;
        stockData.IndicatorName = IndicatorName.RightSidedRickerMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Recursive Moving Trend Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateRecursiveMovingTrendAverage(this StockData stockData, int length = 14)
    {
        List<double> botList = new();
        List<double> nResList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var alpha = (double)2 / (length + 1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevBot = i >= 1 ? botList[i - 1] : currentValue;
            var prevNRes = i >= 1 ? nResList[i - 1] : currentValue;

            var bot = ((1 - alpha) * prevBot) + currentValue;
            botList.AddRounded(bot);

            var nRes = ((1 - alpha) * prevNRes) + (alpha * (currentValue + bot - prevBot));
            nResList.AddRounded(nRes);

            var signal = GetCompareSignal(currentValue - nRes, prevValue - prevNRes);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Rmta", nResList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = nResList;
        stockData.IndicatorName = IndicatorName.RecursiveMovingTrendAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Reverse Moving Average Convergence Divergence
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="signalLength"></param>
    /// <param name="macdLevel"></param>
    /// <returns></returns>
    public static StockData CalculateReverseMovingAverageConvergenceDivergence(this StockData stockData,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int fastLength = 12, int slowLength = 26, int signalLength = 9,
        double macdLevel = 0)
    {
        List<double> pMacdLevelList = new();
        List<double> pMacdEqList = new();
        List<double> histogramList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var fastAlpha = (double)2 / (1 + fastLength);
        var slowAlpha = (double)2 / (1 + slowLength);

        var fastEmaList = GetMovingAverageList(stockData, maType, fastLength, inputList);
        var slowEmaList = GetMovingAverageList(stockData, maType, slowLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevFastEma = i >= 1 ? fastEmaList[i - 1] : 0;
            var prevSlowEma = i >= 1 ? slowEmaList[i - 1] : 0;

            var pMacdEq = fastAlpha - slowAlpha != 0 ? ((prevFastEma * fastAlpha) - (prevSlowEma * slowAlpha)) / (fastAlpha - slowAlpha) : 0;
            pMacdEqList.AddRounded(pMacdEq);

            var pMacdLevel = fastAlpha - slowAlpha != 0 ? (macdLevel - (prevFastEma * (1 - fastAlpha)) + (prevSlowEma * (1 - slowAlpha))) /
                                                          (fastAlpha - slowAlpha) : 0;
            pMacdLevelList.AddRounded(pMacdLevel);
        }

        var pMacdEqSignalList = GetMovingAverageList(stockData, maType, signalLength, pMacdEqList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var pMacdEq = pMacdEqList[i];
            var pMacdEqSignal = pMacdEqSignalList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevPMacdEq = i >= 1 ? pMacdEqList[i - 1] : 0;

            var macdHistogram = pMacdEq - pMacdEqSignal;
            histogramList.AddRounded(macdHistogram);

            var signal = GetCompareSignal(currentValue - pMacdEq, prevValue - prevPMacdEq);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Rmacd", pMacdEqList },
            { "Signal", pMacdEqSignalList },
            { "Histogram", histogramList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = pMacdEqList;
        stockData.IndicatorName = IndicatorName.ReverseMovingAverageConvergenceDivergence;

        return stockData;
    }

    /// <summary>
    /// Calculates the linear regression.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateLinearRegression(this StockData stockData, int length = 14)
    {
        List<double> slopeList = new();
        List<double> interceptList = new();
        List<double> predictedTomorrowList = new();
        List<double> predictedTodayList = new();
        List<double> xList = new();
        List<double> yList = new();
        List<double> xyList = new();
        List<double> x2List = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevValue = yList.LastOrDefault();
            var currentValue = inputList[i];
            yList.AddRounded(currentValue);

            double x = i;
            xList.AddRounded(x);

            var xy = x * currentValue;
            xyList.AddRounded(xy);

            var sumX = xList.TakeLastExt(length).Sum();
            var sumY = yList.TakeLastExt(length).Sum();
            var sumXY = xyList.TakeLastExt(length).Sum();
            var sumX2 = x2List.TakeLastExt(length).Sum();
            var top = (length * sumXY) - (sumX * sumY);
            var bottom = (length * sumX2) - Pow(sumX, 2);

            var b = bottom != 0 ? top / bottom : 0;
            slopeList.AddRounded(b);

            var a = length != 0 ? (sumY - (b * sumX)) / length : 0;
            interceptList.AddRounded(a);

            var predictedToday = a + (b * x);
            predictedTodayList.AddRounded(predictedToday);

            var prevPredictedNextDay = predictedTomorrowList.LastOrDefault();
            var predictedNextDay = a + (b * (x + 1));
            predictedTomorrowList.AddRounded(predictedNextDay);

            var signal = GetCompareSignal(currentValue - predictedNextDay, prevValue - prevPredictedNextDay, true);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "LinearRegression", predictedTodayList },
            { "PredictedTomorrow", predictedTomorrowList },
            { "Slope", slopeList },
            { "Intercept", interceptList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = predictedTodayList;
        stockData.IndicatorName = IndicatorName.LinearRegression;

        return stockData;
    }

    /// <summary>
    /// Calculates the Moving Average Adaptive Q
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="fastAlpha"></param>
    /// <param name="slowAlpha"></param>
    /// <returns></returns>
    public static StockData CalculateMovingAverageAdaptiveQ(this StockData stockData, int length = 10, double fastAlpha = 0.667, 
        double slowAlpha = 0.0645)
    {
        List<double> maaqList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var erList = CalculateKaufmanAdaptiveMovingAverage(stockData, length: length).OutputValues["Er"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevMaaq = i >= 1 ? maaqList[i - 1] : currentValue;
            var er = erList[i];
            var temp = (er * fastAlpha) + slowAlpha;

            var maaq = prevMaaq + (Pow(temp, 2) * (currentValue - prevMaaq));
            maaqList.AddRounded(maaq);

            var signal = GetCompareSignal(currentValue - maaq, prevValue - prevMaaq);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Maaq", maaqList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = maaqList;
        stockData.IndicatorName = IndicatorName.MovingAverageAdaptiveQ;

        return stockData;
    }

    /// <summary>
    /// Calculates the McGinley Dynamic Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="k"></param>
    /// <returns></returns>
    public static StockData CalculateMcGinleyDynamicIndicator(this StockData stockData, int length = 14, double k = 0.6)
    {
        List<double> mdiList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevMdi = i >= 1 ? mdiList.LastOrDefault() : currentValue;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var ratio = prevMdi != 0 ? currentValue / prevMdi : 0;
            var bottom = k * length * Pow(ratio, 4);

            var mdi = bottom != 0 ? prevMdi + ((currentValue - prevMdi) / Math.Max(bottom, 1)) : currentValue;
            mdiList.AddRounded(mdi);

            var signal = GetCompareSignal(currentValue - mdi, prevValue - prevMdi);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Mdi", mdiList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = mdiList;
        stockData.IndicatorName = IndicatorName.McGinleyDynamicIndicator;

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
        List<double> filterList = new();
        List<double> value2List = new();
        List<double> smthList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentPrice = inputList[i];
            var prevP1 = i >= 1 ? inputList[i - 1] : 0;
            var prevP2 = i >= 2 ? inputList[i - 2] : 0;
            var prevP3 = i >= 3 ? inputList[i - 3] : 0;

            var smth = (currentPrice + (2 * prevP1) + (2 * prevP2) + prevP3) / 6;
            smthList.AddRounded(smth);

            var len = length;
            double value3 = 0.2, value2 = 0, prevV2 = value2List.LastOrDefault(), alpha;
            while (value3 > threshold && len > 0)
            {
                alpha = (double)2 / (len + 1);
                var value1 = smthList.TakeLastExt(len).Median();
                value2 = (alpha * smth) + ((1 - alpha) * prevV2);
                value3 = value1 != 0 ? Math.Abs(value1 - value2) / value1 : value3;
                len -= 2;
            }
            value2List.AddRounded(value2);

            len = len < 3 ? 3 : len;
            alpha = (double)2 / (len + 1);

            var prevFilter = filterList.LastOrDefault();
            var filter = (alpha * smth) + ((1 - alpha) * prevFilter);
            filterList.AddRounded(filter);

            var signal = GetCompareSignal(currentPrice - filter, prevP1 - prevFilter);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Maaf", filterList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = filterList;
        stockData.IndicatorName = IndicatorName.EhlersMedianAverageAdaptiveFilter;

        return stockData;
    }

    /// <summary>
    /// Calculates the Middle High Low Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculateMiddleHighLowMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length1 = 14, int length2 = 10)
    {
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var mhlList = CalculateMidpoint(stockData, length2).CustomValuesList;
        var mhlMaList = GetMovingAverageList(stockData, maType, length1, mhlList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentMhlMa = mhlMaList[i];
            var prevMhlma = i >= 1 ? mhlMaList[i - 1] : 0;

            var signal = GetCompareSignal(currentValue - currentMhlMa, prevValue - prevMhlma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Mhlma", mhlMaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = mhlMaList;
        stockData.IndicatorName = IndicatorName.MiddleHighLowMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Moving Average V3
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculateMovingAverageV3(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 14, int length2 = 3)
    {
        List<double> nmaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var lamdaRatio = (double)length1 / length2;
        var alpha = length1 - lamdaRatio != 0 ? lamdaRatio * (length1 - 1) / (length1 - lamdaRatio) : 0;

        var ma1List = GetMovingAverageList(stockData, maType, length1, inputList);
        var ma2List = GetMovingAverageList(stockData, maType, length2, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var ma1 = ma1List[i];
            var ma2 = ma2List[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevNma = nmaList.LastOrDefault();
            var nma = ((1 + alpha) * ma1) - (alpha * ma2);
            nmaList.AddRounded(nma);

            var signal = GetCompareSignal(currentValue - nma, prevValue - prevNma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Mav3", nmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = nmaList;
        stockData.IndicatorName = IndicatorName.MovingAverageV3;

        return stockData;
    }

    /// <summary>
    /// Calculates the Multi Depth Zero Lag Exponential Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateMultiDepthZeroLagExponentialMovingAverage(this StockData stockData, int length = 50)
    {
        List<double> alpha1List = new();
        List<double> beta1List = new();
        List<double> alpha2List = new();
        List<double> beta2List = new();
        List<double> alpha3List = new();
        List<double> beta3List = new();
        List<double> mda1List = new();
        List<double> mda2List = new();
        List<double> mda3List = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var a1 = (double)2 / (length + 1);
        var a2 = Exp(-Sqrt(2) * Math.PI / length);
        var a3 = Exp(-Math.PI / length);
        var b2 = 2 * a2 * Math.Cos(Sqrt(2) * Math.PI / length);
        var b3 = 2 * a3 * Math.Cos(Sqrt(3) * Math.PI / length);
        var c = Exp(-2 * Math.PI / length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevAlpha1 = i >= 1 ? alpha1List[i - 1] : currentValue;
            var alpha1 = (a1 * currentValue) + ((1 - a1) * prevAlpha1);
            alpha1List.AddRounded(alpha1);

            var prevAlpha2 = i >= 1 ? alpha2List[i - 1] : currentValue;
            var priorAlpha2 = i >= 2 ? alpha2List[i - 2] : currentValue;
            var alpha2 = (b2 * prevAlpha2) - (a2 * a2 * priorAlpha2) + ((1 - b2 + (a2 * a2)) * currentValue);
            alpha2List.AddRounded(alpha2);

            var prevAlpha3 = i >= 1 ? alpha3List[i - 1] : currentValue;
            var prevAlpha3_2 = i >= 2 ? alpha3List[i - 2] : currentValue;
            var prevAlpha3_3 = i >= 3 ? alpha3List[i - 3] : currentValue;
            var alpha3 = ((b3 + c) * prevAlpha3) - ((c + (b3 * c)) * prevAlpha3_2) + (c * c * prevAlpha3_3) + ((1 - b3 + c) * (1 - c) * currentValue);
            alpha3List.AddRounded(alpha3);

            var detrend1 = currentValue - alpha1;
            var detrend2 = currentValue - alpha2;
            var detrend3 = currentValue - alpha3;

            var prevBeta1 = i >= 1 ? beta1List[i - 1] : 0;
            var beta1 = (a1 * detrend1) + ((1 - a1) * prevBeta1);
            beta1List.AddRounded(beta1);

            var prevBeta2 = i >= 1 ? beta2List[i - 1] : 0;
            var prevBeta2_2 = i >= 2 ? beta2List[i - 2] : 0;
            var beta2 = (b2 * prevBeta2) - (a2 * a2 * prevBeta2_2) + ((1 - b2 + (a2 * a2)) * detrend2);
            beta2List.AddRounded(beta2);

            var prevBeta3_2 = i >= 2 ? beta3List[i - 2] : 0;
            var prevBeta3_3 = i >= 3 ? beta3List[i - 3] : 0;
            var beta3 = ((b3 + c) * prevBeta3_2) - ((c + (b3 * c)) * prevBeta3_2) + (c * c * prevBeta3_3) + ((1 - b3 + c) * (1 - c) * detrend3);
            beta3List.AddRounded(beta3);

            var mda1 = alpha1 + ((double)1 / 1 * beta1);
            mda1List.AddRounded(mda1);

            var prevMda2 = mda2List.LastOrDefault();
            var mda2 = alpha2 + ((double)1 / 2 * beta2);
            mda2List.AddRounded(mda2);

            var mda3 = alpha3 + ((double)1 / 3 * beta3);
            mda3List.AddRounded(mda3);

            var signal = GetCompareSignal(currentValue - mda2, prevValue - prevMda2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Md2Pole", mda2List },
            { "Md1Pole", mda1List },
            { "Md3Pole", mda3List }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = mda2List;
        stockData.IndicatorName = IndicatorName.MultiDepthZeroLagExponentialMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Modular Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="beta"></param>
    /// <param name="z"></param>
    /// <returns></returns>
    public static StockData CalculateModularFilter(this StockData stockData, int length = 200, double beta = 0.8, double z = 0.5)
    {
        List<double> b2List = new();
        List<double> c2List = new();
        List<double> os2List = new();
        List<double> ts2List = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var alpha = (double)2 / (length + 1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevB2 = i >= 1 ? b2List[i - 1] : currentValue;
            var b2 = currentValue > (alpha * currentValue) + ((1 - alpha) * prevB2) ? currentValue : (alpha * currentValue) + ((1 - alpha) * prevB2);
            b2List.AddRounded(b2);

            var prevC2 = i >= 1 ? c2List[i - 1] : currentValue;
            var c2 = currentValue < (alpha * currentValue) + ((1 - alpha) * prevC2) ? currentValue : (alpha * currentValue) + ((1 - alpha) * prevC2);
            c2List.AddRounded(c2);

            var prevOs2 = os2List.LastOrDefault();
            var os2 = currentValue == b2 ? 1 : currentValue == c2 ? 0 : prevOs2;
            os2List.AddRounded(os2);

            var upper2 = (beta * b2) + ((1 - beta) * c2);
            var lower2 = (beta * c2) + ((1 - beta) * b2);

            var prevTs2 = ts2List.LastOrDefault();
            var ts2 = (os2 * upper2) + ((1 - os2) * lower2);
            ts2List.AddRounded(ts2);

            var signal = GetCompareSignal(currentValue - ts2, prevValue - prevTs2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Mf", ts2List }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = ts2List;
        stockData.IndicatorName = IndicatorName.ModularFilter;

        return stockData;
    }

    /// <summary>
    /// Calculates the Damped Sine Wave Weighted Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateDampedSineWaveWeightedFilter(this StockData stockData, int length = 50)
    {
        List<double> dswwfList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            double w, wSum = 0, wvSum = 0;
            for (var j = 1; j <= length; j++)
            {
                var prevValue = i >= j - 1 ? inputList[i - (j - 1)] : 0;

                w = Math.Sin(MinOrMax(2 * Math.PI * ((double)j / length), 0.99, 0.01)) / j;
                wvSum += w * prevValue;
                wSum += w;
            }

            var prevDswwf = dswwfList.LastOrDefault();
            var dswwf = wSum != 0 ? wvSum / wSum : 0;
            dswwfList.AddRounded(dswwf);

            var signal = GetCompareSignal(currentValue - dswwf, prevVal - prevDswwf);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Dswwf", dswwfList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = dswwfList;
        stockData.IndicatorName = IndicatorName.DampedSineWaveWeightedFilter;

        return stockData;
    }

    /// <summary>
    /// Calculates the Double Exponential Smoothing
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="alpha"></param>
    /// <param name="gamma"></param>
    /// <returns></returns>
    public static StockData CalculateDoubleExponentialSmoothing(this StockData stockData, double alpha = 0.01, double gamma = 0.9)
    {
        List<double> sList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var x = inputList[i];
            var prevX = i >= 1 ? inputList[i - 1] : 0;
            var prevS = i >= 1 ? sList[i - 1] : 0;
            var prevS2 = i >= 2 ? sList[i - 2] : 0;
            var sChg = prevS - prevS2;

            var s = (alpha * x) + ((1 - alpha) * (prevS + (gamma * (sChg + ((1 - gamma) * sChg)))));
            sList.AddRounded(s);

            var signal = GetCompareSignal(x - s, prevX - prevS);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Des", sList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = sList;
        stockData.IndicatorName = IndicatorName.DoubleExponentialSmoothing;

        return stockData;
    }

    /// <summary>
    /// Calculates the Distance Weighted Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateDistanceWeightedMovingAverage(this StockData stockData, int length = 14)
    {
        List<double> dwmaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            double sum = 0, weightedSum = 0;
            for (var j = 0; j < length; j++)
            {
                var prevValue = i >= j ? inputList[i - j] : 0;

                double distanceSum = 0;
                for (var k = 0; k < length; k++)
                {
                    var prevValue2 = i >= k ? inputList[i - k] : 0;

                    distanceSum += Math.Abs(prevValue - prevValue2);
                }

                var weight = distanceSum != 0 ? 1 / distanceSum : 0;

                sum += prevValue * weight;
                weightedSum += weight;
            }

            var prevDwma = dwmaList.LastOrDefault();
            var dwma = weightedSum != 0 ? sum / weightedSum : 0;
            dwmaList.AddRounded(dwma);

            var signal = GetCompareSignal(currentValue - dwma, prevVal - prevDwma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Dwma", dwmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = dwmaList;
        stockData.IndicatorName = IndicatorName.DistanceWeightedMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Dynamically Adjustable Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateDynamicallyAdjustableFilter(this StockData stockData, int length = 14)
    {
        List<double> outList = new();
        List<double> kList = new();
        List<double> srcList = new();
        List<double> srcDevList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevOut = i >= 1 ? outList[i - 1] : currentValue;
            var prevK = i >= 1 ? kList[i - 1] : 0;

            var src = currentValue + (currentValue - prevOut);
            srcList.AddRounded(src);

            var outVal = prevOut + (prevK * (src - prevOut));
            outList.AddRounded(outVal);

            var srcSma = srcList.TakeLastExt(length).Average();
            var srcDev = Pow(src - srcSma, 2);
            srcDevList.AddRounded(srcDev);

            var srcStdDev = Sqrt(srcDevList.TakeLastExt(length).Average());
            var k = src - outVal != 0 ? Math.Abs(src - outVal) / (Math.Abs(src - outVal) + (srcStdDev * length)) : 0;
            kList.AddRounded(k);

            var signal = GetCompareSignal(currentValue - outVal, prevValue - prevOut);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Daf", outList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = outList;
        stockData.IndicatorName = IndicatorName.DynamicallyAdjustableFilter;

        return stockData;
    }

    /// <summary>
    /// Calculates the Dynamically Adjustable Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <returns></returns>
    public static StockData CalculateDynamicallyAdjustableMovingAverage(this StockData stockData, int fastLength = 6, int slowLength = 200)
    {
        List<double> kList = new();
        List<double> amaList = new();
        List<double> tempList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var shortStdDevList = CalculateStandardDeviationVolatility(stockData, length: fastLength).CustomValuesList;
        var longStdDevList = CalculateStandardDeviationVolatility(stockData, length: slowLength).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var a = shortStdDevList[i];
            var b = longStdDevList[i];
            var v = a != 0 ? (b / a) + fastLength : fastLength;

            var prevValue = tempList.LastOrDefault();
            var currentValue = inputList[i];
            tempList.AddRounded(currentValue);

            var p = (int)Math.Round(MinOrMax(v, slowLength, fastLength));
            var prevK = i >= p ? kList[i - p] : 0;
            var k = tempList.Sum();
            kList.AddRounded(k);

            var prevAma = amaList.LastOrDefault();
            var ama = p != 0 ? (k - prevK) / p : 0;
            amaList.AddRounded(ama);

            var signal = GetCompareSignal(currentValue - ama, prevValue - prevAma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Dama", amaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = amaList;
        stockData.IndicatorName = IndicatorName.DynamicallyAdjustableMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Spencer 21 Point Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <returns></returns>
    public static StockData CalculateSpencer21PointMovingAverage(this StockData stockData)
    {
        List<double> spmaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            double sum = 0, weightedSum = 0;
            for (var j = 0; j <= 20; j++)
            {
                var weight = j switch
                {
                    0 => -1,
                    1 => -3,
                    2 => -5,
                    3 => -5,
                    4 => -2,
                    5 => 6,
                    6 => 18,
                    7 => 33,
                    8 => 47,
                    9 => 57,
                    10 => 60,
                    11 => 57,
                    12 => 47,
                    13 => 33,
                    14 => 18,
                    15 => 6,
                    16 => -2,
                    17 => -5,
                    18 => -5,
                    19 => -3,
                    20 => -1,
                    _ => 0,
                };
                var prevValue = i >= j ? inputList[i - j] : 0;

                sum += prevValue * weight;
                weightedSum += weight;
            }

            var prevSpma = spmaList.LastOrDefault();
            var spma = weightedSum != 0 ? sum / weightedSum : 0;
            spmaList.AddRounded(spma);

            var signal = GetCompareSignal(currentValue - spma, prevVal - prevSpma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "S21ma", spmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = spmaList;
        stockData.IndicatorName = IndicatorName.Spencer21PointMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Spencer 15 Point Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <returns></returns>
    public static StockData CalculateSpencer15PointMovingAverage(this StockData stockData)
    {
        List<double> spmaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            double sum = 0, weightedSum = 0;
            for (var j = 0; j <= 14; j++)
            {
                var weight = j switch
                {
                    0 => -3,
                    1 => -6,
                    2 => -5,
                    3 => 3,
                    4 => 21,
                    5 => 46,
                    6 => 67,
                    7 => 74,
                    8 => 67,
                    9 => 46,
                    10 => 21,
                    11 => 3,
                    12 => -5,
                    13 => -6,
                    14 => -3,
                    _ => 0,
                };
                var prevValue = i >= j ? inputList[i - j] : 0;

                sum += prevValue * weight;
                weightedSum += weight;
            }

            var prevSpma = spmaList.LastOrDefault();
            var spma = weightedSum != 0 ? sum / weightedSum : 0;
            spmaList.AddRounded(spma);

            var signal = GetCompareSignal(currentValue - spma, prevVal - prevSpma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "S15ma", spmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = spmaList;
        stockData.IndicatorName = IndicatorName.Spencer15PointMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Square Root Weighted Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateSquareRootWeightedMovingAverage(this StockData stockData, int length = 14)
    {
        List<double> srwmaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            double sum = 0, weightedSum = 0;
            for (var j = 0; j <= length - 1; j++)
            {
                var weight = Pow(length - j, 0.5);
                var prevValue = i >= j ? inputList[i - j] : 0;

                sum += prevValue * weight;
                weightedSum += weight;
            }

            var prevSrwma = srwmaList.LastOrDefault();
            var srwma = weightedSum != 0 ? sum / weightedSum : 0;
            srwmaList.AddRounded(srwma);

            var signal = GetCompareSignal(currentValue - srwma, prevVal - prevSrwma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Srwma", srwmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = srwmaList;
        stockData.IndicatorName = IndicatorName.SquareRootWeightedMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Shapeshifting Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateShapeshiftingMovingAverage(this StockData stockData, int length = 50)
    {
        List<double> filtXList = new();
        List<double> filtNList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            double sumX = 0, weightedSumX = 0, sumN = 0, weightedSumN = 0;
            for (var j = 0; j <= length - 1; j++)
            {
                var x = (double)j / (length - 1);
                var n = -1 + (x * 2);
                var wx = 1 - (2 * x / (Pow(x, 4) + 1));
                var wn = 1 - (2 * Pow(n, 2) / (Pow(n, 4 - (4 % 2)) + 1));
                var prevValue = i >= j ? inputList[i - j] : 0;

                sumX += prevValue * wx;
                weightedSumX += wx;
                sumN += prevValue * wn;
                weightedSumN += wn;
            }

            var prevFiltX = filtXList.LastOrDefault();
            var filtX = weightedSumX != 0 ? sumX / weightedSumX : 0;
            filtXList.AddRounded(filtX);

            var filtN = weightedSumN != 0 ? sumN / weightedSumN : 0;
            filtNList.AddRounded(filtN);

            var signal = GetCompareSignal(currentValue - filtX, prevVal - prevFiltX);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Sma", filtXList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = filtXList;
        stockData.IndicatorName = IndicatorName.ShapeshiftingMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Self Weighted Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateSelfWeightedMovingAverage(this StockData stockData, int length = 14)
    {
        List<double> wmaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            double sum = 0, weightSum = 0;
            for (var j = 0; j < length; j++)
            {
                var pValue = i >= j ? inputList[i - j] : 0;
                var weight = i >= length + j ? inputList[i - (length + j)] : 0;
                weightSum += weight;
                sum += weight * pValue;
            }

            var prevWma = wmaList.LastOrDefault();
            var wma = weightSum != 0 ? sum / weightSum : 0;
            wmaList.AddRounded(wma);

            var signal = GetCompareSignal(currentValue - wma, prevValue - prevWma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Swma", wmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = wmaList;
        stockData.IndicatorName = IndicatorName.SelfWeightedMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Sine Weighted Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateSineWeightedMovingAverage(this StockData stockData, int length = 14)
    {
        List<double> swmaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            double sum = 0, weightedSum = 0;
            for (var j = 0; j <= length - 1; j++)
            {
                var weight = Math.Sin((j + 1) * Math.PI / (length + 1));
                var prevValue = i >= j ? inputList[i - j] : 0;

                sum += prevValue * weight;
                weightedSum += weight;
            }

            var prevSwma = swmaList.LastOrDefault();
            var swma = weightedSum != 0 ? sum / weightedSum : 0;
            swmaList.AddRounded(swma);

            var signal = GetCompareSignal(currentValue - swma, prevVal - prevSwma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Swma", swmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = swmaList;
        stockData.IndicatorName = IndicatorName.SineWeightedMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Simplified Weighted Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateSimplifiedWeightedMovingAverage(this StockData stockData, int length = 14)
    {
        List<double> wmaList = new();
        List<double> cmlList = new();
        List<double> cmlSumList = new();
        List<double> tempList = new();
        List<double> sumList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentValue = inputList[i];
            tempList.AddRounded(currentValue);

            var cml = tempList.Sum();
            cmlList.AddRounded(cml);

            var prevCmlSum = i >= length ? cmlSumList[i - length] : 0;
            var cmlSum = cmlList.Sum();
            cmlSumList.AddRounded(cmlSum);

            var prevSum = sumList.LastOrDefault();
            var sum = cmlSum - prevCmlSum;
            sumList.AddRounded(sum);

            var prevWma = wmaList.LastOrDefault();
            var wma = ((length * cml) - prevSum) / (length * (double)(length + 1) / 2);
            wmaList.AddRounded(wma);

            var signal = GetCompareSignal(currentValue - wma, prevValue - prevWma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Swma", wmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = wmaList;
        stockData.IndicatorName = IndicatorName.SimplifiedWeightedMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Simplified Least Squares Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateSimplifiedLeastSquaresMovingAverage(this StockData stockData, int length = 14)
    {
        List<double> cmlList = new();
        List<double> cmlSumList = new();
        List<double> tempList = new();
        List<double> sumList = new();
        List<double> lsmaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentValue = inputList[i];
            tempList.AddRounded(currentValue);

            var prevCml = i >= length ? cmlList[i - length] : 0;
            var cml = tempList.Sum();
            cmlList.AddRounded(cml);

            var prevCmlSum = i >= length ? cmlSumList[i - length] : 0;
            var cmlSum = cmlList.Sum();
            cmlSumList.AddRounded(cmlSum);

            var prevSum = sumList.LastOrDefault();
            var sum = cmlSum - prevCmlSum;
            sumList.AddRounded(sum);

            var wma = ((length * cml) - prevSum) / (length * (double)(length + 1) / 2);
            var prevLsma = lsmaList.LastOrDefault();
            var lsma = length != 0 ? (3 * wma) - (2 * (cml - prevCml) / length) : 0;
            lsmaList.AddRounded(lsma);

            var signal = GetCompareSignal(currentValue - lsma, prevValue - prevLsma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Slsma", lsmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = lsmaList;
        stockData.IndicatorName = IndicatorName.SimplifiedLeastSquaresMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Sharp Modified Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateSharpModifiedMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14)
    {
        List<double> shmmaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentSma = smaList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            double slope = 0;
            for (var j = 1; j <= length; j++)
            {
                var prevValue = i >= j - 1 ? inputList[i - (j - 1)] : 0;
                double factor = 1 + (2 * (j - 1));
                slope += prevValue * (length - factor) / 2;
            }

            var prevShmma = shmmaList.LastOrDefault();
            var shmma = currentSma + (6 * slope / ((length + 1) * length));
            shmmaList.AddRounded(shmma);

            var signal = GetCompareSignal(currentValue - shmma, prevVal - prevShmma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Smma", shmmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = shmmaList;
        stockData.IndicatorName = IndicatorName.SharpModifiedMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Slow Smoothed Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateSlowSmoothedMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 15)
    {
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var w2 = MinOrMax((int)Math.Ceiling((double)length / 3));
        var w1 = MinOrMax((int)Math.Ceiling((double)(length - w2) / 2));
        var w3 = MinOrMax((int)Math.Floor((double)(length - w2) / 2));

        var l1List = GetMovingAverageList(stockData, maType, w1, inputList);
        var l2List = GetMovingAverageList(stockData, maType, w2, l1List);
        var l3List = GetMovingAverageList(stockData, maType, w3, l2List);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var l3 = l3List[i];
            var prevL3 = i >= 1 ? l3List[i - 1] : 0;

            var signal = GetCompareSignal(currentValue - l3, prevValue - prevL3);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ssma", l3List }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = l3List;
        stockData.IndicatorName = IndicatorName.SlowSmoothedMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Sequentially Filtered Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateSequentiallyFilteredMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 50)
    {
        List<double> sfmaList = new();
        List<double> signList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var sma = smaList[i];
            var prevSma = i >= 1 ? smaList[i - 1] : 0;

            double a = Math.Sign(sma - prevSma);
            signList.AddRounded(a);

            var sum = signList.TakeLastExt(length).Sum();
            double alpha = Math.Abs(sum) == length ? 1 : 0;
            var prevSfma = i >= 1 ? sfmaList[i - 1] : sma;
            var sfma = (alpha * sma) + ((1 - alpha) * prevSfma);
            sfmaList.AddRounded(sfma);

            var signal = GetCompareSignal(currentValue - sfma, prevValue - prevSfma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Sfma", sfmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = sfmaList;
        stockData.IndicatorName = IndicatorName.SequentiallyFilteredMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Svama
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateSvama(this StockData stockData, int length = 14)
    {
        List<double> hList = new();
        List<double> lList = new();
        List<double> cMaxList = new();
        List<double> cMinList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var a = volumeList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevH = i >= 1 ? hList[i - 1] : a;
            var h = a > prevH ? a : prevH;
            hList.AddRounded(h);

            var prevL = i >= 1 ? lList[i - 1] : a;
            var l = a < prevL ? a : prevL;
            lList.AddRounded(l);

            var bMax = h != 0 ? a / h : 0;
            var bMin = a != 0 ? l / a : 0;

            var prevCMax = i >= 1 ? cMaxList[i - 1] : currentValue;
            var cMax = (bMax * currentValue) + ((1 - bMax) * prevCMax);
            cMaxList.AddRounded(cMax);

            var prevCMin = i >= 1 ? cMinList[i - 1] : currentValue;
            var cMin = (bMin * currentValue) + ((1 - bMin) * prevCMin);
            cMinList.AddRounded(cMin);

            var signal = GetCompareSignal(currentValue - cMax, prevValue - prevCMax);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Svama", cMaxList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = cMaxList;
        stockData.IndicatorName = IndicatorName.Svama;

        return stockData;
    }

    /// <summary>
    /// Calculates the Setting Less Trend Step Filtering
    /// </summary>
    /// <param name="stockData"></param>
    /// <returns></returns>
    public static StockData CalculateSettingLessTrendStepFiltering(this StockData stockData)
    {
        List<double> chgList = new();
        List<double> aList = new();
        List<double> bList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevB = i >= 1 ? bList[i - 1] : currentValue;
            var prevA = aList.LastOrDefault();
            var sc = Math.Abs(currentValue - prevB) + prevA != 0 ? Math.Abs(currentValue - prevB) / (Math.Abs(currentValue - prevB) + prevA) : 0;
            var sltsf = (sc * currentValue) + ((1 - sc) * prevB);

            var chg = Math.Abs(sltsf - prevB);
            chgList.AddRounded(chg);

            var a = chgList.Average() * (1 + sc);
            aList.AddRounded(a);

            var b = sltsf > prevB + a ? sltsf : sltsf < prevB - a ? sltsf : prevB;
            bList.AddRounded(b);

            var signal = GetCompareSignal(currentValue - b, prevValue - prevB);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Sltsf", bList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = bList;
        stockData.IndicatorName = IndicatorName.SettingLessTrendStepFiltering;

        return stockData;
    }

    /// <summary>
    /// Calculates the Elastic Volume Weighted Moving Average V1
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="mult"></param>
    /// <returns></returns>
    public static StockData CalculateElasticVolumeWeightedMovingAverageV1(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 40, double mult = 20)
    {
        List<double> evwmaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);

        var volumeSmaList = GetMovingAverageList(stockData, maType, length, volumeList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentAvgVolume = volumeSmaList[i];
            var currentVolume = volumeList[i];
            var n = currentAvgVolume * mult;

            var prevEVWMA = i >= 1 ? evwmaList.LastOrDefault() : currentValue;
            var evwma = n > 0 ? (((n - currentVolume) * prevEVWMA) + (currentVolume * currentValue)) / n : 0; ;
            evwmaList.AddRounded(evwma);

            var signal = GetCompareSignal(currentValue - evwma, prevValue - prevEVWMA);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Evwma", evwmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = evwmaList;
        stockData.IndicatorName = IndicatorName.ElasticVolumeWeightedMovingAverageV1;

        return stockData;
    }

    /// <summary>
    /// Calculates the Elastic Volume Weighted Moving Average V2
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateElasticVolumeWeightedMovingAverageV2(this StockData stockData, int length = 14)
    {
        List<double> tempList = new();
        List<double> evwmaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var currentVolume = volumeList[i];
            tempList.AddRounded(currentVolume);

            var volumeSum = tempList.TakeLastExt(length).Sum();
            var prevEvwma = evwmaList.LastOrDefault();
            var evwma = volumeSum != 0 ? (((volumeSum - currentVolume) * prevEvwma) + (currentVolume * currentValue)) / volumeSum : 0;
            evwmaList.AddRounded(evwma);

            var signal = GetCompareSignal(currentValue - evwma, prevValue - prevEvwma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Evwma", evwmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = evwmaList;
        stockData.IndicatorName = IndicatorName.ElasticVolumeWeightedMovingAverageV2;

        return stockData;
    }

    /// <summary>
    /// Calculates the Equity Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateEquityMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14)
    {
        List<double> chgXList = new();
        List<double> chgXCumList = new();
        List<double> xList = new();
        List<double> eqmaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var sma = smaList[i];
            var prevEqma = i >= 1 ? eqmaList[i - 1] : currentValue;

            var prevX = xList.LastOrDefault();
            double x = Math.Sign(currentValue - sma);
            xList.AddRounded(x);

            var chgX = MinPastValues(i, 1, currentValue - prevValue) * prevX;
            chgXList.AddRounded(chgX);

            var chgXCum = MinPastValues(i, 1, currentValue - prevValue) * x;
            chgXCumList.AddRounded(chgXCum);

            var opteq = chgXCumList.Sum();
            var req = chgXList.TakeLastExt(length).Sum();
            var alpha = opteq != 0 ? MinOrMax(req / opteq, 0.99, 0.01) : 0.99;

            var eqma = (alpha * currentValue) + ((1 - alpha) * prevEqma);
            eqmaList.AddRounded(eqma);

            var signal = GetCompareSignal(currentValue - eqma, prevValue - prevEqma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Eqma", eqmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = eqmaList;
        stockData.IndicatorName = IndicatorName.EquityMovingAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Edge Preserving Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateEdgePreservingFilter(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 200, 
        int smoothLength = 50)
    {
        List<double> osList = new();
        List<double> absOsList = new();
        List<double> hList = new();
        List<double> aList = new();
        List<double> bList = new();
        List<double> cList = new();
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
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var p = pList[i];
            var highest = highestList[i];
            var os = osList[i];

            var prevH = hList.LastOrDefault();
            var h = highest != 0 ? p / highest : 0;
            hList.AddRounded(h);

            double cnd = h == 1 && prevH != 1 ? 1 : 0;
            double sign = cnd == 1 && os < 0 ? 1 : cnd == 1 && os > 0 ? -1 : 0;
            var condition = sign != 0;

            var prevA = i >= 1 ? aList[i - 1] : 1;
            var a = condition ? 1 : prevA + 1;
            aList.AddRounded(a);

            var prevB = i >= 1 ? bList[i - 1] : currentValue;
            var b = a == 1 ? currentValue : prevB + currentValue;
            bList.AddRounded(b);

            var prevC = cList.LastOrDefault();
            var c = a != 0 ? b / a : 0;
            cList.AddRounded(c);

            var signal = GetCompareSignal(currentValue - c, prevValue - prevC);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Epf", cList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = cList;
        stockData.IndicatorName = IndicatorName.EdgePreservingFilter;

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
        List<double> filtList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var a = Exp(MinOrMax(-1.414 * Math.PI / length, -0.01, -0.99));
        var b = 2 * a * Math.Cos(MinOrMax(1.414 * Math.PI / length, 0.99, 0.01));
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
            filtList.AddRounded(filt);

            var signal = GetCompareSignal(currentValue - filt, prevValue - prevFilter1);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "E2ssf", filtList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = filtList;
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
        List<double> filtList = new();
        List<Signal> signalsList = new();
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
            filtList.AddRounded(filt);

            var signal = GetCompareSignal(currentValue - filt, prevValue - prevFilter1);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "E3ssf", filtList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = filtList;
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
        List<double> filtList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var a = Exp(MinOrMax(-1.414 * Math.PI / length, -0.01, -0.99));
        var b = 2 * a * Math.Cos(MinOrMax(1.414 * 1.25 * Math.PI / length, 0.99, 0.01));
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
            filtList.AddRounded(filt);

            var signal = GetCompareSignal(currentValue - filt, prevValue - prevFilter1);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "E2bf", filtList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = filtList;
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
        List<double> filtList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var a = Exp(MinOrMax(-1.414 * Math.PI / length, -0.01, -0.99));
        var b = 2 * a * Math.Cos(MinOrMax(1.414 * Math.PI / length, 0.99, 0.01));
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
            filtList.AddRounded(filt);

            var signal = GetCompareSignal(currentValue - filt, prevValue1 - prevFilter1);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "E2bf", filtList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = filtList;
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
        List<double> filtList = new();
        List<Signal> signalsList = new();
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
            filtList.AddRounded(filt);

            var signal = GetCompareSignal(currentValue - filt, prevValue - prevFilter1);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "E3bf", filtList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = filtList;
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
        List<double> filtList = new();
        List<Signal> signalsList = new();
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
            filtList.AddRounded(filt);

            var signal = GetCompareSignal(currentValue - filt, prevValue1 - prevFilter1);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "E3bf", filtList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = filtList;
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
        List<double> gf1List = new();
        List<double> gf2List = new();
        List<double> gf3List = new();
        List<double> gf4List = new();
        List<Signal> signalsList = new();
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
            gf1List.AddRounded(gf1);

            var gf2 = (Pow(alpha2, 2) * currentValue) + (2 * (1 - alpha2) * prevGf2_1) - (Pow(1 - alpha2, 2) * prevGf2_2);
            gf2List.AddRounded(gf2);

            var gf3 = (Pow(alpha3, 3) * currentValue) + (3 * (1 - alpha3) * prevGf3_1) - (3 * Pow(1 - alpha3, 2) * prevGf3_2) +
                      (Pow(1 - alpha3, 3) * prevGf3_3);
            gf3List.AddRounded(gf3);

            var gf4 = (Pow(alpha4, 4) * currentValue) + (4 * (1 - alpha4) * prevGf4_1) - (6 * Pow(1 - alpha4, 2) * prevGf4_2) +
                (4 * Pow(1 - alpha4, 3) * prevGf4_3) - (Pow(1 - alpha4, 4) * prevGf4_4);
            gf4List.AddRounded(gf4);

            var signal = GetCompareSignal(currentValue - gf4, prevValue - prevGf4_1);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Egf1", gf1List },
            { "Egf2", gf2List },
            { "Egf3", gf3List },
            { "Egf4", gf4List }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = gf4List;
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
        List<double> tempList = new();
        List<double> rmfList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var alphaArg = MinOrMax(2 * Math.PI / length2, 0.99, 0.01);
        var alphaArgCos = Math.Cos(alphaArg);
        var alpha = alphaArgCos != 0 ? (alphaArgCos + Math.Sin(alphaArg) - 1) / alphaArgCos : 0;

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevValue = tempList.LastOrDefault();
            var currentValue = inputList[i];
            tempList.AddRounded(currentValue);

            var median = tempList.TakeLastExt(length1).Median();
            var prevRmf = rmfList.LastOrDefault();
            var rmf = (alpha * median) + ((1 - alpha) * prevRmf);
            rmfList.AddRounded(rmf);

            var signal = GetCompareSignal(currentValue - rmf, prevValue - prevRmf);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ermf", rmfList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = rmfList;
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
        List<double> filtList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var a1 = Exp(MinOrMax(-1.414 * Math.PI / length, -0.01, -0.99));
        var b1 = 2 * a1 * Math.Cos(MinOrMax(1.414 * Math.PI / length, 0.99, 0.01));
        var coeff2 = b1;
        var coeff3 = -1 * a1 * a1;
        var coeff1 = 1 - coeff2 - coeff3;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevFilter1 = i >= 1 ? filtList[i - 1] : 0;
            var prevFilter2 = i >= 2 ? filtList[i - 2] : 0;

            var prevFilt = filtList.LastOrDefault();
            var filt = (coeff1 * ((currentValue + prevValue) / 2)) + (coeff2 * prevFilter1) + (coeff3 * prevFilter2);
            filtList.AddRounded(filt);

            var signal = GetCompareSignal(currentValue - filt, prevValue - prevFilt);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Essf", filtList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = filtList;
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
        List<double> filtList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var a1 = Exp(MinOrMax(-1.414 * Math.PI / length, -0.01, -0.99));
        var b1 = 2 * a1 * Math.Cos(MinOrMax(1.414 * Math.PI / length, 0.99, 0.01));
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
            filtList.AddRounded(filt);

            var signal = GetCompareSignal(currentValue - filt, prevValue - prevFilter1);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Essf", filtList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = filtList;
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
        List<double> filtList = new();
        List<double> ssfList = new();
        List<double> e1List = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var a1 = Exp(MinOrMax(-1.414 * Math.PI / length, -0.01, -0.99));
        var b1 = 2 * a1 * Math.Cos(MinOrMax(1.414 * Math.PI / length, 0.99, 0.01));
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
            ssfList.AddRounded(ssf);

            var e1 = i < 3 ? 0 : (c1 * (currentValue - ssf)) + (c2 * prevE11) + (c3 * prevE12);
            e1List.AddRounded(e1);

            var prevFilt = filtList.LastOrDefault();
            var filt = ssf + e1;
            filtList.AddRounded(filt);

            var signal = GetCompareSignal(currentValue - filt, prevValue - prevFilt);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Eaef", filtList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = filtList;
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
        List<double> filterList = new();
        List<double> firList = new();
        List<double> l0List = new();
        List<double> l1List = new();
        List<double> l2List = new();
        List<double> l3List = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevP1 = i >= 1 ? inputList[i - 1] : 0;
            var prevP2 = i >= 2 ? inputList[i - 2] : 0;
            var prevP3 = i >= 3 ? inputList[i - 3] : 0;
            var prevL0 = i >= 1 ? l0List.LastOrDefault() : currentValue;
            var prevL1 = i >= 1 ? l1List.LastOrDefault() : currentValue;
            var prevL2 = i >= 1 ? l2List.LastOrDefault() : currentValue;
            var prevL3 = i >= 1 ? l3List.LastOrDefault() : currentValue;

            var l0 = (alpha * currentValue) + ((1 - alpha) * prevL0);
            l0List.AddRounded(l0);

            var l1 = (-1 * (1 - alpha) * l0) + prevL0 + ((1 - alpha) * prevL1);
            l1List.AddRounded(l1);

            var l2 = (-1 * (1 - alpha) * l1) + prevL1 + ((1 - alpha) * prevL2);
            l2List.AddRounded(l2);

            var l3 = (-1 * (1 - alpha) * l2) + prevL2 + ((1 - alpha) * prevL3);
            l3List.AddRounded(l3);

            var prevFilter = filterList.LastOrDefault();
            var filter = (l0 + (2 * l1) + (2 * l2) + l3) / 6;
            filterList.AddRounded(filter);

            var prevFir = firList.LastOrDefault();
            var fir = (currentValue + (2 * prevP1) + (2 * prevP2) + prevP3) / 6;
            firList.AddRounded(fir);

            var signal = GetCompareSignal(filter - fir, prevFilter - prevFir);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Elf", filterList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = filterList;
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
        List<double> filterList = new();
        List<double> l0List = new();
        List<double> l1List = new();
        List<double> l2List = new();
        List<double> l3List = new();
        List<double> diffList = new();
        List<double> midList = new();
        List<double> alphaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevL0 = i >= 1 ? l0List.LastOrDefault() : currentValue;
            var prevL1 = i >= 1 ? l1List.LastOrDefault() : currentValue;
            var prevL2 = i >= 1 ? l2List.LastOrDefault() : currentValue;
            var prevL3 = i >= 1 ? l3List.LastOrDefault() : currentValue;
            var prevFilter = i >= 1 ? filterList.LastOrDefault() : currentValue;

            var diff = Math.Abs(currentValue - prevFilter);
            diffList.AddRounded(diff);

            var list = diffList.TakeLastExt(length1).ToList();
            var highestHigh = list.Max();
            var lowestLow = list.Min();

            var mid = highestHigh - lowestLow != 0 ? (diff - lowestLow) / (highestHigh - lowestLow) : 0;
            midList.AddRounded(mid);

            var prevAlpha = i >= 1 ? alphaList.LastOrDefault() : (double)2 / (length1 + 1);
            var alpha = mid != 0 ? midList.TakeLastExt(length2).Median() : prevAlpha;
            alphaList.AddRounded(alpha);

            var l0 = (alpha * currentValue) + ((1 - alpha) * prevL0);
            l0List.AddRounded(l0);

            var l1 = (-1 * (1 - alpha) * l0) + prevL0 + ((1 - alpha) * prevL1);
            l1List.AddRounded(l1);

            var l2 = (-1 * (1 - alpha) * l1) + prevL1 + ((1 - alpha) * prevL2);
            l2List.AddRounded(l2);

            var l3 = (-1 * (1 - alpha) * l2) + prevL2 + ((1 - alpha) * prevL3);
            l3List.AddRounded(l3);

            var filter = (l0 + (2 * l1) + (2 * l2) + l3) / 6;
            filterList.AddRounded(filter);

            var signal = GetCompareSignal(currentValue - filter, prevValue - prevFilter);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ealf", filterList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = filterList;
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
        List<double> leadList = new();
        List<double> leadIndicatorList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevLead = leadList.LastOrDefault();
            var lead = (2 * currentValue) + ((alpha1 - 2) * prevValue) + ((1 - alpha1) * prevLead);
            leadList.AddRounded(lead);

            var prevLeadIndicator = leadIndicatorList.LastOrDefault();
            var leadIndicator = (alpha2 * lead) + ((1 - alpha2) * prevLeadIndicator);
            leadIndicatorList.AddRounded(leadIndicator);

            var signal = GetCompareSignal(currentValue - leadIndicator, prevValue - prevLeadIndicator);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Eli", leadIndicatorList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = leadIndicatorList;
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
        List<double> oefList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue1 = i >= 1 ? inputList[i - 1] : 0;
            var prevValue2 = i >= 2 ? inputList[i - 2] : 0;
            var prevOef1 = i >= 1 ? oefList[i - 1] : 0;
            var prevOef2 = i >= 2 ? oefList[i - 2] : 0;

            var oef = (0.13785 * currentValue) + (0.0007 * prevValue1) + (0.13785 * prevValue2) + (1.2103 * prevOef1) - (0.4867 * prevOef2);
            oefList.AddRounded(oef);

            var signal = GetCompareSignal(currentValue - oef, prevValue1 - prevOef1);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Emoef", oefList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = oefList;
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
        List<double> moefList = new();
        List<Signal> signalsList = new();
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
            moefList.AddRounded(moef);

            var signal = GetCompareSignal(currentValue - moef, prevValue1 - prevMoef1);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Emoef", moefList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = moefList;
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
        List<double> filterList = new();
        List<Signal> signalsList = new();
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

            var prevEhlersFilter = filterList.LastOrDefault();
            var ehlersFilter = sumC != 0 ? num / sumC : 0;
            filterList.AddRounded(ehlersFilter);

            var signal = GetCompareSignal(currentValue - ehlersFilter, prevValue - prevEhlersFilter);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ef", filterList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = filterList;
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
        List<double> filterList = new();
        List<Signal> signalsList = new();
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

            var prevFilter = filterList.LastOrDefault();
            var filter = coefSum != 0 ? srcSum / coefSum : 0;
            filterList.AddRounded(filter);

            var signal = GetCompareSignal(currentValue - filter, prevValue - prevFilter);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Edcf", filterList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = filterList;
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
        List<double> filterList = new();
        List<Signal> signalsList = new();
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

            var prevFilter = filterList.LastOrDefault();
            var filter = ((coef1 * currentValue) + (coef2 * prevValue1) + (coef3 * prevValue2) + (coef4 * prevValue3) + 
                          (coef5 * prevValue4) + (coef6 * prevValue5) + (coef7 * prevValue6)) / coefSum;
            filterList.AddRounded(filter);

            var signal = GetCompareSignal(currentValue - filter, prevValue1 - prevFilter);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Efirf", filterList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = filterList;
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
        List<double> filterList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var alpha = (double)2 / (length + 1);
        var lag = MinOrMax((int)Math.Ceiling((1 / alpha) - 1));

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= lag ? inputList[i - lag] : 0;
            var prevFilter1 = i >= 1 ? filterList[i - 1] : 0;

            var filter = (alpha * (currentValue + MinPastValues(i, lag, currentValue - prevValue))) + ((1 - alpha) * prevFilter1);
            filterList.AddRounded(filter);

            var signal = GetCompareSignal(currentValue - filter, prevValue - prevFilter1);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Eiirf", filterList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = filterList;
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
        List<double> edsma2PoleList = new();
        List<double> zerosList = new();
        List<double> avgZerosList = new();
        List<double> scaledFilter2PoleList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 2 ? inputList[i - 2] : 0;

            var prevZeros = zerosList.LastOrDefault();
            var zeros = MinPastValues(i, 2, currentValue - prevValue);
            zerosList.AddRounded(zeros);

            var avgZeros = (zeros + prevZeros) / 2;
            avgZerosList.AddRounded(avgZeros);
        }

        var ssf2PoleList = GetMovingAverageList(stockData, maType, fastLength, avgZerosList);
        stockData.CustomValuesList = ssf2PoleList;
        var ssf2PoleStdDevList = CalculateStandardDeviationVolatility(stockData, length: slowLength).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentSsf2Pole = ssf2PoleList[i];
            var currentSsf2PoleStdDev = ssf2PoleStdDevList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevScaledFilter2Pole = scaledFilter2PoleList.LastOrDefault();
            var scaledFilter2Pole = currentSsf2PoleStdDev != 0 ? currentSsf2Pole / currentSsf2PoleStdDev : prevScaledFilter2Pole;
            scaledFilter2PoleList.AddRounded(scaledFilter2Pole);

            var alpha2Pole = MinOrMax(5 * Math.Abs(scaledFilter2Pole) / slowLength, 0.99, 0.01);
            var prevEdsma2pole = edsma2PoleList.LastOrDefault();
            var edsma2Pole = (alpha2Pole * currentValue) + ((1 - alpha2Pole) * prevEdsma2pole);
            edsma2PoleList.AddRounded(edsma2Pole);

            var signal = GetCompareSignal(currentValue - edsma2Pole, prevValue - prevEdsma2pole);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Edsma", edsma2PoleList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = edsma2PoleList;
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
        List<double> filtList = new();
        List<Signal> signalsList = new();
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
            filtList.AddRounded(filt);

            var signal = GetCompareSignal(currentValue - filt, prevValue - prevFilt);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ehma", filtList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = filtList;
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
        List<double> momList = new();
        List<double> dsssList = new();
        List<double> filtPowList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var hannLength = (int)Math.Ceiling(length1 / 1.4m);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var priorValue = i >= length1 ? inputList[i - length1] : 0;

            var mom = currentValue - priorValue;
            momList.AddRounded(mom);
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
            filtPowList.AddRounded(filtPow);

            var filtPowMa = filtPowList.TakeLastExt(length2).Average();
            var rms = filtPowMa > 0 ? Sqrt(filtPowMa) : 0;
            var scaledFilt = rms != 0 ? filt / rms : 0;
            var a1 = Exp(-1.414 * Math.PI * Math.Abs(scaledFilt) / length1);
            var b1 = 2 * a1 * Math.Cos(1.414 * Math.PI * Math.Abs(scaledFilt) / length1);
            var c2 = b1;
            var c3 = -a1 * a1;
            var c1 = 1 - c2 - c3;

            var dsss = (c1 * ((currentValue + prevValue) / 2)) + (c2 * prevDsss1) + (c3 * prevDsss2);
            dsssList.AddRounded(dsss);

            var signal = GetCompareSignal(currentValue - dsss, prevValue - prevDsss1);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Edsss", dsssList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = dsssList;
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
        List<double> dList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var lag = MinOrMax((int)Math.Floor((double)(length - 1) / 2));

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= lag ? inputList[i - lag] : 0;

            var d = currentValue + MinPastValues(i, lag, currentValue - prevValue);
            dList.AddRounded(d);
        }

        var zemaList = GetMovingAverageList(stockData, maType, length, dList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var zema = zemaList[i];
            var prevZema = i >= 1 ? zemaList[i - 1] : 0;

            var signal = GetCompareSignal(currentValue - zema, prevValue - prevZema);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ezlema", zemaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = zemaList;
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
        List<double> vidyaList = new();
        List<double> longPowList = new();
        List<double> shortPowList = new();
        List<Signal> signalsList = new();
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
            shortPowList.AddRounded(shortPow);

            var shortMa = shortPowList.TakeLastExt(fastLength).Average();
            var shortRms = shortMa > 0 ? Sqrt(shortMa) : 0;

            var longPow = Pow(currentValue - longAvg, 2);
            longPowList.AddRounded(longPow);

            var longMa = longPowList.TakeLastExt(slowLength).Average();
            var longRms = longMa > 0 ? Sqrt(longMa) : 0;
            var kk = longRms != 0 ? MinOrMax(0.2 * shortRms / longRms, 0.99, 0.01) : 0;

            var prevVidya = vidyaList.LastOrDefault();
            var vidya = (kk * currentValue) + ((1 - kk) * prevVidya);
            vidyaList.AddRounded(vidya);

            var signal = GetCompareSignal(currentValue - vidya, prevValue - prevVidya);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Evidya", vidyaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = vidyaList;
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
        List<double> kamaList = new();
        List<Signal> signalsList = new();
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

            var prevKama = kamaList.LastOrDefault();
            var kama = (s * currentValue) + ((1 - s) * prevKama);
            kamaList.AddRounded(kama);

            var signal = GetCompareSignal(currentValue - kama, prevValue - prevKama);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ekama", kamaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = kamaList;
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
        List<double> phaserList = new();
        List<Signal> signalsList = new();
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
            phaserList.AddRounded(phaser);

            var signal = GetCompareSignal(currentValue - phaser, prevValue1 - prevPhaser1);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Eapps", phaserList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = phaserList;
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
        List<double> v1Neg2List = new();
        List<double> waveNeg2List = new();
        List<double> v1Neg1List = new();
        List<double> waveNeg1List = new();
        List<double> v10List = new();
        List<double> wave0List = new();
        List<double> v11List = new();
        List<double> wave1List = new();
        List<double> v12List = new();
        List<double> wave2List = new();
        List<double> v13List = new();
        List<double> wave3List = new();
        List<double> v14List = new();
        List<double> wave4List = new();
        List<double> v15List = new();
        List<double> wave5List = new();
        List<double> v16List = new();
        List<double> wave6List = new();
        List<Signal> signalsList = new();
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
            v1Neg2List.AddRounded(v1Neg2);

            var waveNeg2 = v1Neg2 + (0.513 * prevV1Neg2_1) + prevV1Neg2_2 + (0.451 * prevWaveNeg2_1) - (0.481 * prevWaveNeg2_2);
            waveNeg2List.AddRounded(waveNeg2);

            var v1Neg1 = (0.021394 * (currentValue + (1.777 * prevValue1) + prevValue2)) + (0.731 * prevV1Neg1_1) - (0.166 * prevV1Neg1_2);
            v1Neg1List.AddRounded(v1Neg1);

            var waveNeg1 = v1Neg1 + (0.977 * prevV1Neg1_1) + prevV1Neg1_2 + (1.008 * prevWaveNeg1_1) - (0.561 * prevWaveNeg1_2);
            waveNeg1List.AddRounded(waveNeg1);

            var v10 = (0.0095822 * (currentValue + (1.572 * prevValue1) + prevValue2)) + (1.026 * prevV10_1) - (0.282 * prevV10_2);
            v10List.AddRounded(v10);

            var wave0 = v10 + (0.356 * prevV10_1) + prevV10_2 + (1.329 * prevWave0_1) - (0.644 * prevWave0_2);
            wave0List.AddRounded(wave0);

            var v11 = (0.00461 * (currentValue + (1.192 * prevValue1) + prevValue2)) + (1.281 * prevV11_1) - (0.426 * prevV11_2);
            v11List.AddRounded(v11);

            var wave1 = v11 - (0.384 * prevV11_1) + prevV11_2 + (1.565 * prevWave1_1) - (0.729 * prevWave1_2);
            wave1List.AddRounded(wave1);

            var v12 = (0.0026947 * (currentValue + (0.681 * prevValue1) + prevValue2)) + (1.46 * prevV12_1) - (0.543 * prevV12_2);
            v12List.AddRounded(v12);

            var wave2 = v12 - (0.966 * prevV12_1) + prevV12_2 + (1.703 * prevWave2_1) - (0.793 * prevWave2_2);
            wave2List.AddRounded(wave2);

            var v13 = (0.0017362 * (currentValue + (0.012 * prevValue1) + prevValue2)) + (1.606 * prevV13_1) - (0.65 * prevV13_2);
            v13List.AddRounded(v13);

            var wave3 = v13 - (1.408 * prevV13_1) + prevV13_2 + (1.801 * prevWave3_1) - (0.848 * prevWave3_2);
            wave3List.AddRounded(wave3);

            var v14 = (0.0013738 * (currentValue - (0.669 * prevValue1) + prevValue2)) + (1.716 * prevV14_1) - (0.74 * prevV14_2);
            v14List.AddRounded(v14);

            var wave4 = v14 - (1.685 * prevV14_1) + prevV14_2 + (1.866 * prevWave4_1) - (0.89 * prevWave4_2);
            wave4List.AddRounded(wave4);

            var v15 = (0.0010794 * (currentValue - (1.226 * prevValue1) + prevValue2)) + (1.8 * prevV15_1) - (0.811 * prevV15_2);
            v15List.AddRounded(v15);

            var wave5 = v15 - (1.842 * prevV15_1) + prevV15_2 + (1.91 * prevWave5_1) - (0.922 * prevWave5_2);
            wave5List.AddRounded(wave5);

            var v16 = (0.001705 * (currentValue - (1.659 * prevValue1) + prevValue2)) + (1.873 * prevV16_1) - (0.878 * prevV16_2);
            v16List.AddRounded(v16);

            var wave6 = v16 - (1.957 * prevV16_1) + prevV16_2 + (1.946 * prevWave6_1) - (0.951 * prevWave6_2);
            wave6List.AddRounded(wave6);

            var signal = GetCompareSignal(currentValue - waveNeg2, prevValue1 - prevWaveNeg2_1);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Eclpf-2", waveNeg2List },
            { "Eclpf-1", waveNeg1List },
            { "Eclpf0", wave0List },
            { "Eclpf1", wave1List },
            { "Eclpf2", wave2List },
            { "Eclpf3", wave3List },
            { "Eclpf4", wave4List },
            { "Eclpf5", wave5List },
            { "Eclpf6", wave6List }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = waveNeg2List;
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
        List<double> emaList = new();
        List<double> bEmaList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var val = length != 0 ? Math.Cos(2 * Math.PI / length) + Math.Sin(2 * Math.PI / length) : 0;
        var alpha = val != 0 ? MinOrMax((val - 1) / val, 0.99, 0.01) : 0.01;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevEma1 = i >= 1 ? emaList[i - 1] : 0;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var ema = (alpha * currentValue) + ((1 - alpha) * prevEma1);
            emaList.AddRounded(ema);

            var prevBEma = bEmaList.LastOrDefault();
            var bEma = (alpha * ((currentValue + prevValue) / 2)) + ((1 - alpha) * prevEma1);
            bEmaList.AddRounded(bEma);

            var signal = GetCompareSignal(currentValue - bEma, prevValue - prevBEma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ebema", bEmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = bEmaList;
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
        List<double> filtList = new();
        List<Signal> signalsList = new();
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
            filtList.AddRounded(filt);

            var signal = GetCompareSignal(currentValue - filt, prevValue - prevFilt);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ehma", filtList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = filtList;
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
        List<double> filtList = new();
        List<Signal> signalsList = new();
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
            filtList.AddRounded(filt);

            var signal = GetCompareSignal(currentValue - filt, prevValue - prevFilt);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Etma", filtList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = filtList;
        stockData.IndicatorName = IndicatorName.EhlersTriangleMovingAverage;

        return stockData;
    }
}
