using OoplesFinance.StockIndicators.Compatibility;
using OoplesFinance.StockIndicators.Core;

namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
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
        List<double> advDiffList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var advancesSumWindow = new RollingSum();
        var declinesSumWindow = new RollingSum();

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            double advance = currentValue > prevValue ? 1 : 0;
            advancesSumWindow.Add(advance);

            double decline = currentValue < prevValue ? 1 : 0;
            declinesSumWindow.Add(decline);

            var advSum = advancesSumWindow.Sum(length);
            var decSum = declinesSumWindow.Sum(length);

            var advDiff = advSum + decSum != 0 ? advSum / (advSum + decSum) : 0;
            advDiffList.Add(advDiff);
        }

        var zmbtiList = GetMovingAverageList(stockData, maType, length, advDiffList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var prevZmbti1 = i >= 1 ? zmbtiList[i - 1] : 0;
            var prevZmbti2 = i >= 2 ? zmbtiList[i - 2] : 0;
            var zmbti = zmbtiList[i];

            var signal = GetRsiSignal(zmbti - prevZmbti1, prevZmbti1 - prevZmbti2, zmbti, prevZmbti1, 0.615, 0.4);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Zmbti", zmbtiList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(zmbtiList);
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
        List<double> zscoreList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var vwapList = GetMovingAverageList(stockData, maType, length, inputList);
        stockData.SetCustomValues(vwapList);
        var vwapSdList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevZScore1 = i >= 1 ? zscoreList[i - 1] : 0;
            var prevZScore2 = i >= 2 ? zscoreList[i - 2] : 0;
            var mean = vwapList[i];
            var vwapsd = vwapSdList[i];

            var zscore = vwapsd != 0 ? (currentValue - mean) / vwapsd : 0;
            zscoreList.Add(zscore);

            var signal = GetRsiSignal(zscore - prevZScore1, prevZScore1 - prevZScore2, zscore, prevZScore1, 2, -2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Zscore", zscoreList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(zscoreList);
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
        List<double> zScorePopulationList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);
        var stdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var sma = smaList[i];
            var dev = currentValue - sma;
            var stdDevPopulation = stdDevList[i];

            var prevZScorePopulation = GetLastOrDefault(zScorePopulationList);
            var zScorePopulation = stdDevPopulation != 0 ? dev / stdDevPopulation : 0;
            zScorePopulationList.Add(zScorePopulation);

            var signal = GetCompareSignal(zScorePopulation, prevZScorePopulation);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Zscore", zScorePopulationList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(zScorePopulationList);
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
        List<double> ax1List = new(stockData.Count);
        List<double> lx1List = new(stockData.Count);
        List<double> ax2List = new(stockData.Count);
        List<double> lx2List = new(stockData.Count);
        List<double> ax3List = new(stockData.Count);
        List<double> lcoList = new(stockData.Count);
        List<double> filterList = new(stockData.Count);
        List<double> lcoSma1List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var length1 = MinOrMax((int)Math.Ceiling((double)length / 2));
        var lcoSumWindow = new RollingSum();
        var lcoSma1SumWindow = new RollingSum();

        var linregList = CalculateLinearRegression(stockData, length).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var linreg = linregList[i];

            var ax1 = currentValue - linreg;
            ax1List.Add(ax1);
        }

        stockData.SetCustomValues(ax1List);
        var ax1LinregList = CalculateLinearRegression(stockData, length).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var ax1 = ax1List[i];
            var ax1Linreg = ax1LinregList[i];

            var lx1 = ax1 + (ax1 - ax1Linreg);
            lx1List.Add(lx1);
        }

        stockData.SetCustomValues(lx1List);
        var lx1LinregList = CalculateLinearRegression(stockData, length).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var lx1 = lx1List[i];
            var lx1Linreg = lx1LinregList[i];

            var ax2 = lx1 - lx1Linreg;
            ax2List.Add(ax2);
        }

        stockData.SetCustomValues(ax2List);
        var ax2LinregList = CalculateLinearRegression(stockData, length).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var ax2 = ax2List[i];
            var ax2Linreg = ax2LinregList[i];

            var lx2 = ax2 + (ax2 - ax2Linreg);
            lx2List.Add(lx2);
        }

        stockData.SetCustomValues(lx2List);
        var lx2LinregList = CalculateLinearRegression(stockData, length).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var lx2 = lx2List[i];
            var lx2Linreg = lx2LinregList[i];

            var ax3 = lx2 - lx2Linreg;
            ax3List.Add(ax3);
        }

        stockData.SetCustomValues(ax3List);
        var ax3LinregList = CalculateLinearRegression(stockData, length).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var ax3 = ax3List[i];
            var ax3Linreg = ax3LinregList[i];

            var prevLco = GetLastOrDefault(lcoList);
            var lco = ax3 + (ax3 - ax3Linreg);
            lcoList.Add(lco);

            lcoSumWindow.Add(lco);
            var lcoSma1 = lcoSumWindow.Average(length1);
            lcoSma1List.Add(lcoSma1);

            lcoSma1SumWindow.Add(lcoSma1);
            var lcoSma2 = lcoSma1SumWindow.Average(length1);
            var prevFilter = GetLastOrDefault(filterList);
            var filter = -lcoSma2 * 2;
            filterList.Add(filter);

            var signal = GetCompareSignal(lco - filter, prevLco - prevFilter);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Lco", lcoList },
            { "Filter", filterList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(filterList);
        stockData.IndicatorName = IndicatorName.ZeroLagSmoothedCycle;

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
        List<double> dxList = new(stockData.Count);
        List<double> dxiList = new(stockData.Count);
        List<double> trList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, openList, volumeList) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, rangeLength);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var prevClose = i >= 1 ? inputList[i - 1] : 0;

            var tr = CalculateTrueRange(currentHigh, currentLow, prevClose);
            trList.Add(tr);
        }

        stockData.SetCustomValues(trList);
        var trStoList = CalculateStochasticOscillator(stockData, maType, length: lbLength).CustomValuesList;
        stockData.SetCustomValues(volumeList);
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
            dxList.Add(dx);

            var dxi = (2 * (100 - (100 / (1 + dx)))) - 100;
            dxiList.Add(dxi);
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
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Uto", dxisList },
            { "Signal", dxissList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(dxisList);
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
        List<double> cmaList = new(stockData.Count);
        List<double> ctsList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);
        var varList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var sma = smaList[i];
            var prevVar = i >= length ? varList[i - length] : 0;
            var prevCma = i >= 1 ? GetLastOrDefault(cmaList) : currentValue;
            var prevCts = i >= 1 ? GetLastOrDefault(ctsList) : currentValue;
            var secma = Pow(sma - prevCma, 2);
            var sects = Pow(currentValue - prevCts, 2);
            var ka = prevVar < secma && secma != 0 ? 1 - (prevVar / secma) : 0;
            var kb = prevVar < sects && sects != 0 ? 1 - (prevVar / sects) : 0;

            var cma = (ka * sma) + ((1 - ka) * prevCma);
            cmaList.Add(cma);

            var cts = (kb * currentValue) + ((1 - kb) * prevCts);
            ctsList.Add(cts);

            var signal = GetCompareSignal(cts - cma, prevCts - prevCma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Cts", ctsList },
            { "Cma", cmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.UhlMaCrossoverSystem;

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
        List<double> histogramList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var cciList = CalculateCommodityChannelIndex(stockData, maType: maType, length: slowLength).CustomValuesList;
        var turboCciList = CalculateCommodityChannelIndex(stockData, maType: maType, length: fastLength).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var cci = cciList[i];
            var cciTurbo = turboCciList[i];

            var prevCciHistogram = GetLastOrDefault(histogramList);
            var cciHistogram = cciTurbo - cci;
            histogramList.Add(cciHistogram);

            var signal = GetCompareSignal(cciHistogram, prevCciHistogram);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "FastCci", turboCciList },
            { "SlowCci", cciList },
            { "Histogram", histogramList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
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
        List<double> upFractalList = new(stockData.Count);
        List<double> dnFractalList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
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

            var prevUpFractal = GetLastOrDefault(upFractalList);
            double upFractal = (prevHigh4 < prevHigh2 && prevHigh3 < prevHigh2 && prevHigh1 < prevHigh2 && prevHigh < prevHigh2) ||
                (prevHigh5 < prevHigh2 && prevHigh4 < prevHigh2 && prevHigh3 == prevHigh2 && prevHigh1 < prevHigh2) ||
                (prevHigh6 < prevHigh2 && prevHigh5 < prevHigh2 && prevHigh4 == prevHigh2 && prevHigh3 <= prevHigh2 && prevHigh1 < prevHigh2 &&
                prevHigh < prevHigh2) || (prevHigh7 < prevHigh2 && prevHigh6 < prevHigh2 && prevHigh5 == prevHigh2 && prevHigh4 == prevHigh2 &&
                prevHigh3 <= prevHigh2 && prevHigh1 < prevHigh2 && prevHigh < prevHigh2) || (prevHigh8 < prevHigh2 && prevHigh7 < prevHigh2 &&
                prevHigh6 == prevHigh2 && prevHigh5 <= prevHigh2 && prevHigh4 == prevHigh2 && prevHigh3 <= prevHigh2 && prevHigh1 < prevHigh2 &&
                prevHigh < prevHigh2) ? 1 : 0;
            upFractalList.Add(upFractal);

            var prevDnFractal = GetLastOrDefault(dnFractalList);
            double dnFractal = (prevLow4 > prevLow2 && prevLow3 > prevLow2 && prevLow1 > prevLow2 && prevLow > prevLow2) || (prevLow5 > prevLow2 &&
                prevLow4 > prevLow2 && prevLow3 == prevLow2 && prevLow1 > prevLow2 && prevLow > prevLow2) || (prevLow6 > prevLow2 &&
                prevLow5 > prevLow2 && prevLow4 == prevLow2 && prevLow3 >= prevLow2 && prevLow1 > prevLow2 && prevLow > prevLow2) ||
                (prevLow7 > prevLow2 && prevLow6 > prevLow2 && prevLow5 == prevLow2 && prevLow4 == prevLow2 && prevLow3 >= prevLow2 &&
                prevLow1 > prevLow2 && prevLow > prevLow2) || (prevLow8 > prevLow2 && prevLow7 > prevLow2 && prevLow6 == prevLow2 &&
                prevLow5 >= prevLow2 && prevLow4 == prevLow2 && prevLow3 >= prevLow2 && prevLow1 > prevLow2 && prevLow > prevLow2) ? 1 : 0;
            dnFractalList.Add(dnFractal);

            var signal = GetCompareSignal(upFractal - dnFractal, prevUpFractal - prevDnFractal);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpFractal", upFractalList },
            { "DnFractal", dnFractalList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
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
        List<double> wadList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var close = inputList[i];
            var prevClose = i >= 1 ? inputList[i - 1] : 0;
            var prevLow = i >= 1 ? lowList[i - 1] : 0;
            var prevHigh = i >= 1 ? highList[i - 1] : 0;

            var prevWad = GetLastOrDefault(wadList);
            var wad = close > prevClose ? prevWad + close - prevLow : close < prevClose ? prevWad + close - prevHigh : 0;
            wadList.Add(wad);

            var signal = GetCompareSignal(wad, prevWad);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Wad", wadList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(wadList);
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
        List<double> diffList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var diff = MinPastValues(i, 1, currentValue - prevValue);
            diffList.Add(diff);
        }

        var wma1List = GetMovingAverageList(stockData, MovingAvgType.WeightedMovingAverage, length2, diffList);
        var ema2List = GetMovingAverageList(stockData, maType, length1, wma1List);
        var wamiList = GetMovingAverageList(stockData, maType, length1, ema2List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var wami = wamiList[i];
            var prevWami = i >= 1 ? wamiList[i - 1] : 0;

            var signal = GetCompareSignal(wami, prevWami);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Wami", wamiList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(wamiList);
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
        List<double> t1List = new(stockData.Count);
        List<double> t2List = new(stockData.Count);
        List<double> e1List = new(stockData.Count);
        List<double> temp1List = new(stockData.Count);
        List<double> temp2List = new(stockData.Count);
        List<double> temp3List = new(stockData.Count);
        List<double> trendUpList = new(stockData.Count);
        List<double> trendDnList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var macd1List = CalculateMovingAverageConvergenceDivergence(stockData, fastLength: fastLength, slowLength: slowLength).CustomValuesList;
        var bbList = CalculateBollingerBands(stockData, length: fastLength);
        var upperBollingerBandList = bbList.OutputValues["UpperBand"];
        var lowerBollingerBandList = bbList.OutputValues["LowerBand"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevValue1 = i >= 1 ? inputList[i - 1] : 0;
            temp1List.Add(prevValue1);

            var prevValue2 = i >= 2 ? inputList[i - 2] : 0;
            temp2List.Add(prevValue2);

            var prevValue3 = i >= 3 ? inputList[i - 3] : 0;
            temp3List.Add(prevValue3);
        }

        stockData.SetCustomValues(temp1List);
        var macd2List = CalculateMovingAverageConvergenceDivergence(stockData, fastLength: fastLength, slowLength: slowLength).CustomValuesList;
        stockData.SetCustomValues(temp2List);
        var macd3List = CalculateMovingAverageConvergenceDivergence(stockData, fastLength: fastLength, slowLength: slowLength).CustomValuesList;
        stockData.SetCustomValues(temp3List);
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
            t1List.Add(t1);

            var t2 = (currentMacd3 - currentMacd4) * sensitivity;
            t2List.Add(t2);

            var prevE1 = GetLastOrDefault(e1List);
            var e1 = currentUpperBB - currentLowerBB;
            e1List.Add(e1);

            var prevTrendUp = GetLastOrDefault(trendUpList);
            var trendUp = (t1 >= 0) ? t1 : 0;
            trendUpList.Add(trendUp);

            var trendDown = (t1 < 0) ? (-1 * t1) : 0;
            trendDnList.Add(trendDown);

            var signal = GetConditionSignal(trendUp > prevTrendUp && trendUp > e1 && e1 > prevE1 && trendUp > fastLength && e1 > fastLength,
                trendUp < e1);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "T1", t1List },
            { "T2", t2List },
            { "E1", e1List },
            { "TrendUp", trendUpList },
            { "TrendDn", trendDnList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.WaddahAttarExplosion;

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
        List<double> iBuff116List = new(stockData.Count);
        List<double> iBuff112List = new(stockData.Count);
        List<double> iBuff109List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var tempSumWindow = new RollingSum();
        var rangeSumWindow = new RollingSum();

        var wmaList = GetMovingAverageList(stockData, maType, length2, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var wma = wmaList[i];
            var prevBuff109_1 = i >= 1 ? iBuff109List[i - 1] : 0;
            var prevBuff109_2 = i >= 2 ? iBuff109List[i - 2] : 0;

            var medianPrice = inputList[i];
            tempSumWindow.Add(medianPrice);

            var range = currentHigh - currentLow;
            rangeSumWindow.Add(range);

            var gd120 = tempSumWindow.Sum(length1);
            var gd128 = gd120 * 0.2;
            var gd121 = rangeSumWindow.Sum(length1);
            var gd136 = gd121 * 0.2 * 0.2;

            var prevIBuff116 = GetLastOrDefault(iBuff116List);
            var iBuff116 = gd136 != 0 ? (currentLow - gd128) / gd136 : 0;
            iBuff116List.Add(iBuff116);

            var prevIBuff112 = GetLastOrDefault(iBuff112List);
            var iBuff112 = gd136 != 0 ? (currentHigh - gd128) / gd136 : 0;
            iBuff112List.Add(iBuff112);

            double iBuff108 = iBuff112 > level && currentHigh > wma ? 90 : iBuff116 < -level && currentLow < wma ? -90 : 0;
            var iBuff109 = (iBuff112 > level && prevIBuff112 > level) || (iBuff116 < -level && prevIBuff116 < -level) ? 0 : iBuff108;
            iBuff109List.Add(iBuff109);

            var signal = GetRsiSignal(iBuff109 - prevBuff109_1, prevBuff109_1 - prevBuff109_2, iBuff109, prevBuff109_1, 80, -80);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Vi", iBuff109List }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(iBuff109List);
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
        List<double> vOpenList = new(stockData.Count);
        List<double> vHighList = new(stockData.Count);
        List<double> vLowList = new(stockData.Count);
        List<double> vCloseList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
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
            vCloseList.Add(vClose);

            var vOpen = lRange != 0 ? (currentOpen - mba) / lRange : 0;
            vOpenList.Add(vOpen);

            var vHigh = lRange != 0 ? (currentHigh - mba) / lRange : 0;
            vHighList.Add(vHigh);

            var vLow = lRange != 0 ? (currentLow - mba) / lRange : 0;
            vLowList.Add(vLow);
        }

        var vValueEmaList = GetMovingAverageList(stockData, maType, length, vCloseList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var vValue = vCloseList[i];
            var vValueEma = vValueEmaList[i];
            var prevVvalue = i >= 1 ? vCloseList[i - 1] : 0;
            var prevVValueEma = i >= 1 ? vValueEmaList[i - 1] : 0;

            var signal = GetRsiSignal(vValue - vValueEma, prevVvalue - prevVValueEma, vValue, prevVvalue, 4, -4);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "vClose", vCloseList },
            { "vOpen", vOpenList },
            { "vHigh", vHighList },
            { "vLow", vLowList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
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
        List<double> rainbowList = new(stockData.Count);
        List<double> zlrbList = new(stockData.Count);
        List<double> zlrbpercbList = new(stockData.Count);
        List<double> rbcList = new(stockData.Count);
        List<double> fastKList = new(stockData.Count);
        List<double> skList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, closeList, _) = GetInputValuesList(inputName, stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length2);
        var rbcWindow = new RollingMinMax(length2);
        var fastKSumWindow = new RollingSum();

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
            rainbowList.Add(rainbow);
        }

        var ema1List = GetMovingAverageList(stockData, MovingAvgType.ExponentialMovingAverage, smoothLength, rainbowList);
        var ema2List = GetMovingAverageList(stockData, MovingAvgType.ExponentialMovingAverage, smoothLength, ema1List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var ema1 = ema1List[i];
            var ema2 = ema2List[i];

            var zlrb = (2 * ema1) - ema2;
            zlrbList.Add(zlrb);
        }

        var tzList = GetMovingAverageList(stockData, MovingAvgType.TripleExponentialMovingAverage, smoothLength, zlrbList);
        stockData.SetCustomValues(tzList);
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

            var prevZlrbpercb = GetLastOrDefault(zlrbpercbList);
            var zlrbpercb = hwidth != 0 ? (tz + (stdDevMult * hwidth) - wmatz) / (2 * stdDevMult * hwidth * 100) : 0;
            zlrbpercbList.Add(zlrbpercb);

            var rbc = (rainbow + currentTypicalPrice) / 2;
            rbcList.Add(rbc);

            rbcWindow.Add(rbc);
            var lowestRbc = rbcWindow.Min;
            var nom = rbc - lowest;
            var den = highest - lowestRbc;

            var fastK = den != 0 ? MinOrMax(100 * nom / den, 100, 0) : 0;
            fastKList.Add(fastK);

            var prevSk = GetLastOrDefault(skList);
            fastKSumWindow.Add(fastK);
            var sk = fastKSumWindow.Average(smoothLength);
            skList.Add(sk);

            var signal = GetConditionSignal(sk > prevSk && zlrbpercb > prevZlrbpercb, sk < prevSk && zlrbpercb < prevZlrbpercb);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Vso", zlrbpercbList },
            { "Sk", skList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(zlrbpercbList);
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
        List<double> haoList = new(stockData.Count);
        List<double> hacList = new(stockData.Count);
        List<double> medianPriceList = new(stockData.Count);
        List<bool> keepN1List = new(stockData.Count);
        List<bool> keepAll1List = new(stockData.Count);
        List<bool> keepN2List = new(stockData.Count);
        List<bool> keepAll2List = new(stockData.Count);
        List<bool> utrList = new(stockData.Count);
        List<bool> dtrList = new(stockData.Count);
        List<double> hacoList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, openList, closeList, _) = GetInputValuesList(inputName, stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevHao = GetLastOrDefault(haoList);
            var hao = (prevValue + prevHao) / 2;
            haoList.Add(hao);

            var hac = (currentValue + hao + Math.Max(currentHigh, hao) + Math.Min(currentLow, hao)) / 4;
            hacList.Add(hac);

            var medianPrice = (currentHigh + currentLow) / 2;
            medianPriceList.Add(medianPrice);
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
            var prevKeepN1 = GetLastOrDefault(keepN1List);
            var keepN1 = ((hac >= hao) && (prevHac >= prevHao)) || currentClose >= hac || currentHigh > prevHigh || currentLow > prevLow || hl2Smooth >= hacSmooth;
            keepN1List.Add(keepN1);

            var prevKeepAll1 = GetLastOrDefault(keepAll1List);
            var keepAll1 = keepN1 || (prevKeepN1 && (currentClose >= currentOpen || currentClose >= prevClose));
            keepAll1List.Add(keepAll1);

            var keep13 = shortCandle && currentHigh >= prevLow;
            var prevUtr = GetLastOrDefault(utrList);
            var utr = keepAll1 || (prevKeepAll1 && keep13);
            utrList.Add(utr);

            var prevKeepN2 = GetLastOrDefault(keepN2List);
            var keepN2 = (hac < hao && prevHac < prevHao) || hl2Smooth < hacSmooth;
            keepN2List.Add(keepN2);

            var keep23 = shortCandle && currentLow <= prevHigh;
            var prevKeepAll2 = GetLastOrDefault(keepAll2List);
            var keepAll2 = keepN2 || (prevKeepN2 && (currentClose < currentOpen || currentClose < prevClose));
            keepAll2List.Add(keepAll2);

            var prevDtr = GetLastOrDefault(dtrList);
            var dtr = (keepAll2 || prevKeepAll2) && keep23;
            dtrList.Add(dtr);

            var upw = dtr == false && prevDtr && utr;
            var dnw = utr == false && prevUtr && dtr;
            var prevHaco = GetLastOrDefault(hacoList);
            var haco = upw ? 1 : dnw ? -1 : prevHaco;
            hacoList.Add(haco);

            var signal = GetCompareSignal(haco, prevHaco);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Vhaltco", hacoList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(hacoList);
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
        List<double> countList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var vixts = smaList[i];

            var prevCount = GetLastOrDefault(countList);
            var count = currentValue > vixts && prevCount >= 0 ? prevCount + 1 : currentValue <= vixts && prevCount <= 0 ?
                prevCount - 1 : prevCount;
            countList.Add(count);

            var signal = GetBullishBearishSignal(count - maxCount - 1, prevCount - maxCount - 1, count - minCount + 1,
                prevCount - minCount + 1, true);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Vix", countList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(countList);
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
        length = Math.Max(1, length);
        var count = stockData.Count;
        List<double> dvoList = new(count);
        List<double> ratioList = new(count);
        List<Signal>? signalsList = CreateSignalsList(stockData, count);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        const int SmallWindowThreshold = 32;
        var useLinearWindow = length <= SmallWindowThreshold;
        using RollingOrderStatistic? aWindow = useLinearWindow ? null : new RollingOrderStatistic(length);
        double[]? aRing = useLinearWindow ? new double[length] : null;
        var aRingCount = 0;
        var aRingIndex = 0;

        for (var i = 0; i < count; i++)
        {
            var currentValue = inputList[i];
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var median = (currentHigh + currentLow) / 2;

            var ratio = median != 0 ? currentValue / median : 0;
            ratioList.Add(ratio);
        }

        List<double> aList;
        switch (maType)
        {
            case MovingAvgType.SimpleMovingAverage:
            {
                var ratioSpan = SpanCompat.AsReadOnlySpan(ratioList);
                var outputBuffer = SpanCompat.CreateOutputBuffer(count);
                MovingAverageCore.SimpleMovingAverage(ratioSpan, outputBuffer.Span, length);
                aList = outputBuffer.ToList();
                break;
            }
            case MovingAvgType.WeightedMovingAverage:
            {
                var ratioSpan = SpanCompat.AsReadOnlySpan(ratioList);
                var outputBuffer = SpanCompat.CreateOutputBuffer(count);
                MovingAverageCore.WeightedMovingAverage(ratioSpan, outputBuffer.Span, length);
                aList = outputBuffer.ToList();
                break;
            }
            case MovingAvgType.ExponentialMovingAverage:
            {
                var ratioSpan = SpanCompat.AsReadOnlySpan(ratioList);
                var outputBuffer = SpanCompat.CreateOutputBuffer(count);
                MovingAverageCore.ExponentialMovingAverage(ratioSpan, outputBuffer.Span, length);
                aList = outputBuffer.ToList();
                break;
            }
            case MovingAvgType.WildersSmoothingMethod:
            {
                var ratioSpan = SpanCompat.AsReadOnlySpan(ratioList);
                var outputBuffer = SpanCompat.CreateOutputBuffer(count);
                MovingAverageCore.WellesWilderMovingAverage(ratioSpan, outputBuffer.Span, length);
                aList = outputBuffer.ToList();
                break;
            }
            default:
                aList = GetMovingAverageList(stockData, maType, length, ratioList);
                break;
        }

        for (var i = 0; i < count; i++)
        {
            var a = aList[i];
            var prevDvo1 = i >= 1 ? dvoList[i - 1] : 0;
            var prevDvo2 = i >= 2 ? dvoList[i - 2] : 0;

            var prevA = i >= 1 ? aList[i - 1] : 0;
            int countLe;
            if (useLinearWindow)
            {
                aRing![aRingIndex] = prevA;
                aRingIndex++;
                if (aRingIndex == length)
                {
                    aRingIndex = 0;
                }
                if (aRingCount < length)
                {
                    aRingCount++;
                }

                countLe = 0;
                for (var j = 0; j < aRingCount; j++)
                {
                    if (aRing[j] <= a)
                    {
                        countLe++;
                    }
                }
            }
            else
            {
                aWindow!.Add(prevA);
                countLe = aWindow.CountLessThanOrEqual(a);
            }

            var dvo = MinOrMax(countLe / (double)length * 100, 100, 0);
            dvoList.Add(dvo);

            var signal = GetRsiSignal(dvo - prevDvo1, prevDvo1 - prevDvo2, dvo, prevDvo1, 80, 20);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Vo", dvoList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(dvoList);
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
        var count = stockData.Count;
        List<double> osList = new(count);
        List<double> fList = new(count);
        List<double> dosList = new(count);
        List<Signal>? signalsList = CreateSignalsList(stockData, count);
        var (inputList, _, _, openList, _) = GetInputValuesList(stockData);     

        for (var i = 0; i < count; i++)
        {
            var currentOpen = openList[i];
            var currentValue = inputList[i];
            var prevValue1 = i >= 1 ? inputList[i - 1] : 0;
            var prevValue2 = i >= 2 ? inputList[i - 2] : 0;
            var prevValue3 = i >= 3 ? inputList[i - 3] : 0;
            double up = prevValue3 > prevValue2 && prevValue1 > prevValue2 && currentValue < prevValue2 ? 1 : 0;
            double dn = prevValue3 < prevValue2 && prevValue1 < prevValue2 && currentValue > prevValue2 ? 1 : 0;

            var prevOs = i >= 1 ? osList[i - 1] : 0;
            var os = up == 1 ? 1 : dn == 1 ? 0 : prevOs;
            osList.Add(os);

            var prevF = i >= 1 ? fList[i - 1] : 0;
            var f = os == 1 && currentValue > currentOpen ? 1 : os == 0 && currentValue < currentOpen ? 0 : prevF;
            fList.Add(f);

            var prevDos = i >= 1 ? dosList[i - 1] : 0;
            var dos = os - prevOs;
            dosList.Add(dos);

            var signal = GetCompareSignal(dos, prevDos);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Vabcd", dosList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(dosList);
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
        List<double> haoList = new(stockData.Count);
        List<double> hacList = new(stockData.Count);
        List<double> medianPriceList = new(stockData.Count);
        List<bool> dnKeepingList = new(stockData.Count);
        List<bool> dnKeepAllList = new(stockData.Count);
        List<bool> dnTrendList = new(stockData.Count);
        List<bool> upKeepingList = new(stockData.Count);
        List<bool> upKeepAllList = new(stockData.Count);
        List<bool> upTrendList = new(stockData.Count);
        List<double> hacoList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, openList, closeList, _) = GetInputValuesList(inputName, stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevHao = GetLastOrDefault(haoList);
            var hao = (prevValue + prevHao) / 2;
            haoList.Add(hao);

            var hac = (currentValue + hao + Math.Max(currentHigh, hao) + Math.Min(currentLow, hao)) / 4;
            hacList.Add(hac);

            var medianPrice = (currentHigh + currentLow) / 2;
            medianPriceList.Add(medianPrice);
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

            var prevDnKeeping = GetLastOrDefault(dnKeepingList);
            var dnKeeping = dnKeep1 || dnKeep2;
            dnKeepingList.Add(dnKeeping);

            var prevDnKeepAll = GetLastOrDefault(dnKeepAllList);
            var dnKeepAll = (dnKeeping || prevDnKeeping) && ((currentClose < currentOpen) || (currentClose < prevClose));
            dnKeepAllList.Add(dnKeepAll);

            var prevDnTrend = GetLastOrDefault(dnTrendList);
            var dnTrend = dnKeepAll || (prevDnKeepAll && dnKeep3);
            dnTrendList.Add(dnTrend);

            var upKeep1 = hac >= hao && prevHac >= prevHao;
            var upKeep2 = zlDiff >= 0;
            var upKeep3 = Math.Abs(currentClose - currentOpen) < (currentHigh - currentLow) * 0.35 && currentHigh >= prevLow;

            var prevUpKeeping = GetLastOrDefault(upKeepingList);
            var upKeeping = upKeep1 || upKeep2;
            upKeepingList.Add(upKeeping);

            var prevUpKeepAll = GetLastOrDefault(upKeepAllList);
            var upKeepAll = (upKeeping || prevUpKeeping) && ((currentClose >= currentOpen) || (currentClose >= prevClose));
            upKeepAllList.Add(upKeepAll);

            var prevUpTrend = GetLastOrDefault(upTrendList);
            var upTrend = upKeepAll || (prevUpKeepAll && upKeep3);
            upTrendList.Add(upTrend);

            var upw = dnTrend == false && prevDnTrend && upTrend;
            var dnw = upTrend == false && prevUpTrend && dnTrend;

            var prevHaco = GetLastOrDefault(hacoList);
            var haco = upw ? 1 : dnw ? -1 : prevHaco;
            hacoList.Add(haco);

            var signal = GetCompareSignal(haco, prevHaco);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Vhaco", hacoList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(hacoList);
        stockData.IndicatorName = IndicatorName.VervoortHeikenAshiCandlestickOscillator;

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
        List<double> avgList = new(stockData.Count);
        List<double> hyList = new(stockData.Count);
        List<double> ylList = new(stockData.Count);
        List<double> abList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
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
            avgList.Add(avg);
        }

        var yList = GetMovingAverageList(stockData, maType, length, avgList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var y = yList[i];
            var h = hList[i];
            var l = lList[i];

            var hy = h - y;
            hyList.Add(hy);

            var yl = y - l;
            ylList.Add(yl);
        }

        var aList = GetMovingAverageList(stockData, maType, length, hyList);
        var bList = GetMovingAverageList(stockData, maType, length, ylList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var a = aList[i];
            var b = bList[i];

            var ab = a - b;
            abList.Add(ab);
        }

        var oscList = GetMovingAverageList(stockData, maType, length, abList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var osc = oscList[i];
            var prevOsc = i >= 1 ? oscList[i - 1] : 0;
            var a = aList[i];
            var prevA = i >= 1 ? aList[i - 1] : 0;

            var signal = GetCompareSignal(osc - a, prevOsc - prevA);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "BullLine", aList },
            { "Trigger", oscList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
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
        List<double> totalPowerList = new(stockData.Count);
        List<double> adjBullCountList = new(stockData.Count);
        List<double> adjBearCountList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var bullCountSumWindow = new RollingSum();
        var bearCountSumWindow = new RollingSum();

        var elderPowerList = CalculateElderRayIndex(stockData, maType, length2);
        var bullPowerList = elderPowerList.OutputValues["BullPower"];
        var bearPowerList = elderPowerList.OutputValues["BearPower"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var bullPower = bullPowerList[i];
            var bearPower = bearPowerList[i];

            double bullCount = bullPower > 0 ? 1 : 0;
            bullCountSumWindow.Add(bullCount);

            double bearCount = bearPower < 0 ? 1 : 0;
            bearCountSumWindow.Add(bearCount);

            var bullCountSum = bullCountSumWindow.Sum(length1);
            var bearCountSum = bearCountSumWindow.Sum(length1);

            var totalPower = length1 != 0 ? 100 * Math.Abs(bullCountSum - bearCountSum) / length1 : 0;
            totalPowerList.Add(totalPower);

            var prevAdjBullCount = GetLastOrDefault(adjBullCountList);
            var adjBullCount = length1 != 0 ? 100 * bullCountSum / length1 : 0;
            adjBullCountList.Add(adjBullCount);

            var prevAdjBearCount = GetLastOrDefault(adjBearCountList);
            var adjBearCount = length1 != 0 ? 100 * bearCountSum / length1 : 0;
            adjBearCountList.Add(adjBearCount);

            var signal = GetCompareSignal(adjBullCount - adjBearCount, prevAdjBullCount - prevAdjBearCount);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "TotalPower", totalPowerList },
            { "BullCount", adjBullCountList },
            { "BearCount", adjBearCountList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(totalPowerList);
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
        List<double> aList = new(stockData.Count);
        List<double> bList = new(stockData.Count);
        List<double> smoList = new(stockData.Count);
        List<double> smoSmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var smoWindow = new RollingMinMax(length);
        var smoSmaWindow = new RollingMinMax(length);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);
        var sma2List = GetMovingAverageList(stockData, maType, length, smaList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var sma = smaList[i];
            var sma2 = sma2List[i];

            var smoSma = (alpha * sma) + ((1 - alpha) * sma2);
            smoSmaList.Add(smoSma);
            smoSmaWindow.Add(smoSma);

            var smo = (alpha * currentValue) + ((1 - alpha) * sma);
            smoList.Add(smo);
            smoWindow.Add(smo);

            var smoSmaHighest = smoSmaWindow.Max;
            var smoSmaLowest = smoSmaWindow.Min;
            var smoHighest = smoWindow.Max;
            var smoLowest = smoWindow.Min;

            var a = smoHighest - smoLowest != 0 ? (currentValue - smoLowest) / (smoHighest - smoLowest) : 0;
            aList.Add(a);

            var b = smoSmaHighest - smoSmaLowest != 0 ? (sma - smoSmaLowest) / (smoSmaHighest - smoSmaLowest) : 0;
            bList.Add(b);
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
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ts", aList },
            { "Trigger", bList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(aList);
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
        List<double> trList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
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
            trList.Add(tr);

            var signal = GetCompareSignal(tr - prevTr1, prevTr1 - prevTr2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Tr", trList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(trList);
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
        List<double> uList = new(stockData.Count);
        List<double> oList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var sList = CalculateLinearRegression(stockData, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var s = sList[i];
            var prevS = i >= 1 ? sList[i - 1] : 0;
            var wa = Math.Asin(Math.Sign(s - prevS)) * 2;
            var wb = Math.Asin(Math.Sign(1)) * 2;

            var u = wa + (2 * Math.PI * Math.Round((wa - wb) / (2 * Math.PI)));
            uList.Add(u);
        }

        stockData.SetCustomValues(uList);
        var uLinregList = CalculateLinearRegression(stockData, length).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var u = uLinregList[i];
            var prevO1 = i >= 1 ? oList[i - 1] : 0;
            var prevO2 = i >= 2 ? oList[i - 2] : 0;

            var o = Math.Atan(u);
            oList.Add(o);

            var signal = GetRsiSignal(o - prevO1, prevO1 - prevO2, o, prevO1, 1, -1);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "To", oList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(oList);
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
        List<double> bufHistNoList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

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

            var prevBufHistNo = GetLastOrDefault(bufHistNoList);
            var bufHistNo = bufHistUp - bufHistDn;
            bufHistNoList.Add(bufHistNo);

            var signal = GetCompareSignal(bufHistNo, prevBufHistNo);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Tmmso", bufHistNoList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(bufHistNoList);
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
        List<double> upList = new(stockData.Count);
        List<double> dnList = new(stockData.Count);
        List<double> midList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var rsiList = CalculateRelativeStrengthIndex(stockData, maType, length1, length2);
        var rList = rsiList.CustomValuesList;
        var maList = rsiList.OutputValues["Signal"];
        stockData.SetCustomValues(rList);
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

            var prevUp = GetLastOrDefault(upList);
            var up = rsiSma + offs;
            upList.Add(up);

            var prevDn = GetLastOrDefault(dnList);
            var dn = rsiSma - offs;
            dnList.Add(dn);

            var mid = (up + dn) / 2;
            midList.Add(mid);

            var signal = GetBollingerBandsSignal(mab - mbb, prevMab - prevMbb, mab, prevMab, up, prevUp, dn, prevDn);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", upList },
            { "MiddleBand", midList },
            { "LowerBand", dnList },
            { "Tdi", mabList },
            { "Signal", mbbList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(mabList);
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
        List<double> bList = new(stockData.Count);
        List<double> cList = new(stockData.Count);
        List<double> upList = new(stockData.Count);
        List<double> dnList = new(stockData.Count);
        List<double> osList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var emaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var a = emaList[i];
            var prevA = i >= 1 ? emaList[i - 1] : 0;

            var b = a > prevA ? a : 0;
            bList.Add(b);

            var c = a < prevA ? a : 0;
            cList.Add(c);
        }

        stockData.SetCustomValues(bList);
        var bStdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        stockData.SetCustomValues(cList);
        var cStdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var a = emaList[i];
            var b = bStdDevList[i];
            var c = cStdDevList[i];

            var prevUp = GetLastOrDefault(upList);
            var up = a + b != 0 ? a / (a + b) : 0;
            upList.Add(up);

            var prevDn = GetLastOrDefault(dnList);
            var dn = a + c != 0 ? a / (a + c) : 0;
            dnList.Add(dn);

            double os = prevUp == 1 && up != 1 ? 1 : prevDn == 1 && dn != 1 ? -1 : 0;
            osList.Add(os);

            var signal = GetConditionSignal(os > 0, os < 0);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Tabf", osList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(osList);
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
        List<double> bullsList = new(stockData.Count);
        List<double> bearsList = new(stockData.Count);
        List<double> netList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
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
            bullsList.Add(bulls);

            var bears = range != 0 ? Math.Max((hidn + lodn) / range, -1) * -100 : 0;
            bearsList.Add(bears);
        }

        var avgBullsList = GetMovingAverageList(stockData, maType, length1, bullsList);
        var avgBearsList = GetMovingAverageList(stockData, maType, length1, bearsList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var avgBulls = avgBullsList[i];
            var avgBears = avgBearsList[i];

            var net = avgBulls - avgBears;
            netList.Add(net);
        }

        var tpxList = GetMovingAverageList(stockData, maType, smoothLength, netList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var tpx = tpxList[i];
            var prevTpx = i >= 1 ? tpxList[i - 1] : 0;

            var signal = GetCompareSignal(tpx, prevTpx);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Tpx", tpxList },
            { "Bulls", avgBullsList },
            { "Bears", avgBearsList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(tpxList);
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
        List<double> maRatingList = new(stockData.Count);
        List<double> oscRatingList = new(stockData.Count);
        List<double> totalRatingList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
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
        stockData.Clear();
        var adxItemsList = CalculateAverageDirectionalIndex(stockData, length: adxLength);
        var adxList = adxItemsList.CustomValuesList;
        var adxPlusList = adxItemsList.OutputValues["DiPlus"];
        var adxMinusList = adxItemsList.OutputValues["DiMinus"];
        var cciList = CalculateCommodityChannelIndex(stockData, length: cciLength).CustomValuesList;
        var bullBearPowerList = CalculateElderRayIndex(stockData, length: bullBearLength);
        var bullPowerList = bullBearPowerList.OutputValues["BullPower"];
        var bearPowerList = bullBearPowerList.OutputValues["BearPower"];
        stockData.Clear();
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
        stockData.SetCustomValues(rsiList);
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
            maRatingList.Add(maRating);

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
            oscRatingList.Add(oscRating);

            var totalRating = (maRating + oscRating) / 2;
            totalRatingList.Add(totalRating);

            var signal = GetConditionSignal(totalRating > 0.1, totalRating < -0.1);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Tr", totalRatingList },
            { "Or", oscRatingList },
            { "Mr", maRatingList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(totalRatingList);
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
        List<double> buySellSwitchList = new(stockData.Count);
        List<double> sbsList = new(stockData.Count);
        List<double> clrsList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
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

            var prevBuySellSwitch = GetLastOrDefault(buySellSwitchList);
            var buySellSwitch = triggerSell == 1 ? 1 : triggerBuy == 1 ? 0 : prevBuySellSwitch;
            buySellSwitchList.Add(buySellSwitch);

            var prevSbs = GetLastOrDefault(sbsList);
            var sbs = triggerSell == 1 && prevBuySellSwitch == 0 ? high : triggerBuy == 1 && prevBuySellSwitch == 1 ? low : prevSbs;
            sbsList.Add(sbs);

            var prevClrs = GetLastOrDefault(clrsList);
            var clrs = triggerSell == 1 && prevBuySellSwitch == 0 ? 1 : triggerBuy == 1 && prevBuySellSwitch == 1 ? -1 : prevClrs;
            clrsList.Add(clrs);

            var signal = GetCompareSignal(clrs, prevClrs);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Sbs", sbsList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(sbsList);
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
        List<double> tetherLineList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var highest = highestList[i];
            var lowest = lowestList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevTetherLine = GetLastOrDefault(tetherLineList);
            var tetherLine = (highest + lowest) / 2;
            tetherLineList.Add(tetherLine);

            var signal = GetCompareSignal(currentValue - tetherLine, prevValue - prevTetherLine);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Tether", tetherLineList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(tetherLineList);
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
        List<double> v1List = new(stockData.Count);
        List<double> stochList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var v1Window = new RollingMinMax(length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var tr = CalculateTrueRange(currentHigh, currentLow, prevValue);

            var v1 = i >= 1 && currentValue > prevValue ? tr / MinPastValues(i, 1, currentValue - prevValue) : tr;
            v1List.Add(v1);

            v1Window.Add(v1);
            var v2 = v1Window.Min;
            var v3 = v1Window.Max;

            var stoch = v3 - v2 != 0 ? MinOrMax(100 * (v1 - v2) / (v3 - v2), 100, 0) : MinOrMax(100 * (v1 - v2), 100, 0);
            stochList.Add(stoch);
        }

        var triList = GetMovingAverageList(stockData, maType, length, stochList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var tri = triList[i];
            var prevTri1 = i >= 1 ? triList[i - 1] : 0;
            var prevTri2 = i >= 2 ? triList[i - 2] : 0;

            var signal = GetRsiSignal(tri - prevTri1, prevTri1 - prevTri2, tri, prevTri1, 80, 20);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Tri", triList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(triList);
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
        List<double> upperList = new(stockData.Count);
        List<double> lowerList = new(stockData.Count);
        List<double> risingList = new(stockData.Count);
        List<double> fallingList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (_, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var highest = i >= 1 ? highestList[i - 1] : 0;
            var lowest = i >= 1 ? lowestList[i - 1] : 0;

            double rising = currentHigh > highest ? 1 : 0;
            risingList.Add(rising);

            double falling = currentLow < lowest ? 1 : 0;
            fallingList.Add(falling);

            double a = i - risingList.LastIndexOf(1);
            double b = i - fallingList.LastIndexOf(1);

            var prevUpper = GetLastOrDefault(upperList);
            var upper = length != 0 ? ((a > length ? length : a) / length) - 0.5 : 0;
            upperList.Add(upper);

            var prevLower = GetLastOrDefault(lowerList);
            var lower = length != 0 ? ((b > length ? length : b) / length) - 0.5 : 0;
            lowerList.Add(lower);

            var signal = GetCompareSignal((lower * -1) - (upper * -1), (prevLower * -1) - (prevUpper * -1));
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", upperList },
            { "LowerBand", lowerList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.TimePriceIndicator;

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
        List<double> bList = new(stockData.Count);
        List<double> cList = new(stockData.Count);
        List<double> absChgList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= length ? inputList[i - length] : 0;
            var sma = smaList[i];
            var prevSma = i >= length ? smaList[i - length] : 0;

            var absChg = Math.Abs(MinPastValues(i, length, currentValue - prevValue));
            absChgList.Add(absChg);

            var b = MinPastValues(i, length, sma - prevSma);
            bList.Add(b);
        }

        var aList = GetMovingAverageList(stockData, maType, length, absChgList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var a = aList[i];
            var b = bList[i];
            var prevC1 = i >= 1 ? cList[i - 1] : 0;
            var prevC2 = i >= 2 ? cList[i - 2] : 0;

            var c = a != 0 ? MinOrMax(b / a, 1, 0) : 0;
            cList.Add(c);

            var signal = GetRsiSignal(c - prevC1, prevC1 - prevC2, c, prevC1, 0.8, 0.2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Sdro", cList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(cList);
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
        List<double> sroList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
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
            sroList.Add(sro);

            var signal = GetRsiSignal(sro - prevSro1, prevSro1 - prevSro2, sro, prevSro1, 0.7, 0.3);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Sro", sroList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(sroList);
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
        List<double> extList = new(stockData.Count);
        List<double> yList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var sma = smaList[i];
            var prevY = i >= length ? yList[i - length] : 0;
            var prevY2 = i >= length * 2 ? yList[i - (length * 2)] : 0;

            var y = currentValue - sma;
            yList.Add(y);

            var ext = ((2 * prevY) - prevY2) / 2;
            extList.Add(ext);
        }

        stockData.SetCustomValues(extList);
        var oscList = CalculateStochasticOscillator(stockData, maType, length: length * 2).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var osc = oscList[i];
            var prevOsc1 = i >= 1 ? oscList[i - 1] : 0;
            var prevOsc2 = i >= 2 ? oscList[i - 2] : 0;

            var signal = GetRsiSignal(osc - prevOsc1, prevOsc1 - prevOsc2, osc, prevOsc1, 80, 20);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Selo", oscList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(oscList);
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
        List<double> v3List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
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
            v3List.Add(v3);
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
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Sgi", sgiList },
            { "Signal", sgiEmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(sgiList);
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
        List<double> aaSeList = new(stockData.Count);
        List<double> sSeList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= length2 - 1 ? inputList[i - (length2 - 1)] : 0;
            var moveSe = MinPastValues(i, length2 - 1, currentValue - prevValue);
            var avgMoveSe = moveSe / (length2 - 1);

            var aaSe = prevValue != 0 ? avgMoveSe / prevValue : 0;
            aaSeList.Add(aaSe);
        }

        var bList = GetMovingAverageList(stockData, maType, length1, aaSeList);
        stockData.SetCustomValues(bList);
        var stoList = CalculateStochasticOscillator(stockData, maType, length: length1).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var bSto = stoList[i];

            var sSe = (bSto * 2) - 100;
            sSeList.Add(sSe);
        }

        var ssSeList = GetMovingAverageList(stockData, maType, smoothingLength, sSeList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var ssSe = ssSeList[i];
            var prevSsse = i >= 1 ? ssSeList[i - 1] : 0;

            var signal = GetCompareSignal(ssSe, prevSsse);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Som", ssSeList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(ssSeList);
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
        List<double> coefCorrList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var windowLimit = Math.Max(1, length);
        var window = new double[windowLimit];
        var windowCount = 0;
        var windowStart = 0;
        var sortedWindow = new List<double>(windowLimit);
        var rankByValue = new Dictionary<double, double>(windowLimit);
        var rankY = new double[windowLimit];

        static void InsertSorted(List<double> list, double value)
        {
            var index = list.BinarySearch(value);
            if (index < 0)
            {
                index = ~index;
            }

            list.Insert(index, value);
        }

        static void RemoveSorted(List<double> list, double value)
        {
            var index = list.BinarySearch(value);
            if (index >= 0)
            {
                list.RemoveAt(index);
            }
        }

        static double CalculateSpearman(
            IReadOnlyList<double> sorted,
            double[] windowValues,
            int startIndex,
            int count,
            Dictionary<double, double> rankByValue,
            double[] rankY)
        {
            if (count <= 1)
            {
                return 0;
            }

            rankByValue.Clear();
            double sumY = 0;
            double sumY2 = 0;
            var rank = 1;
            for (var i = 0; i < count; )
            {
                var value = sorted[i];
                var j = i + 1;
                while (j < count && sorted[j] == value)
                {
                    j++;
                }

                var span = j - i;
                var avgRank = (rank + (rank + span - 1)) / 2.0;
                rankByValue[value] = avgRank;
                sumY += avgRank * span;
                sumY2 += avgRank * avgRank * span;
                for (var k = i; k < j; k++)
                {
                    rankY[k] = avgRank;
                }

                rank += span;
                i = j;
            }

            double sumXY = 0;
            for (var i = 0; i < count; i++)
            {
                var valueX = windowValues[(startIndex + i) % windowValues.Length];
                var rankX = rankByValue[valueX];
                sumXY += rankX * rankY[i];
            }

            var n = (double)count;
            var numerator = (n * sumXY) - (sumY * sumY);
            var denomLeft = (n * sumY2) - (sumY * sumY);
            var denomRight = (n * sumY2) - (sumY * sumY);
            var denom = Math.Sqrt(denomLeft * denomRight);
            return denom != 0 ? numerator / denom : 0;
        }

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            if (windowCount == windowLimit)
            {
                var removed = window[windowStart];
                window[windowStart] = currentValue;
                windowStart = (windowStart + 1) % windowLimit;
                RemoveSorted(sortedWindow, removed);
            }
            else
            {
                window[(windowStart + windowCount) % windowLimit] = currentValue;
                windowCount++;
            }

            InsertSorted(sortedWindow, currentValue);

            var sc = CalculateSpearman(sortedWindow, window, windowStart, windowCount, rankByValue, rankY);
            sc = IsValueNullOrInfinity(sc) ? 0 : sc;
            coefCorrList.Add((double)sc * 100);
        }

        var sigList = GetMovingAverageList(stockData, maType, signalLength, coefCorrList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var sc = coefCorrList[i];
            var prevSc = i >= 1 ? coefCorrList[i - 1] : 0;
            var sig = sigList[i];
            var prevSig = i >= 1 ? sigList[i - 1] : 0;

            var signal = GetCompareSignal(sc - sig, prevSc - prevSig);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Si", coefCorrList },
            { "Signal", sigList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(coefCorrList);
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
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var wadList = CalculateWilliamsAccumulationDistribution(stockData).CustomValuesList;
        var wadSignalList = GetMovingAverageList(stockData, maType, length, wadList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var wad = wadList[i];
            var wadSma = wadSignalList[i];
            var prevWad = i >= 1 ? wadList[i - 1] : 0;
            var prevWadSma = i >= 1 ? wadSignalList[i - 1] : 0;

            var signal = GetCompareSignal(wad - wadSma, prevWad - prevWadSma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Swad", wadList },
            { "Signal", wadSignalList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(wadList);
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
        List<double> srocList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var maList = GetMovingAverageList(stockData, maType, smoothingLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentMa = maList[i];
            var prevMa = i >= length ? maList[i - length] : 0;
            var mom = currentMa - prevMa;

            var prevSroc = GetLastOrDefault(srocList);
            var sroc = prevMa != 0 ? 100 * mom / prevMa : 100;
            srocList.Add(sroc);

            var signal = GetCompareSignal(sroc, prevSroc);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Sroc", srocList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(srocList);
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
        List<double> rList = new(stockData.Count);
        List<double> szoList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            double r = currentValue > prevValue ? 1 : -1;
            rList.Add(r);
        }

        var spList = GetMovingAverageList(stockData, maType, fastLength, rList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var sp = spList[i];

            var szo = fastLength != 0 ? 100 * sp / fastLength : 0;
            szoList.Add(szo);
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
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Szo", szoList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(szoList);
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
        List<double> srcList = new(stockData.Count);
        List<double> cEmaList = new(stockData.Count);
        List<double> cList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var a = (double)1 / length;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevC1 = i >= 1 ? cList[i - 1] : 0;
            var prevC2 = i >= 2 ? cList[i - 2] : 0;
            var prevSrc = i >= length ? srcList[i - length] : 0;

            var src = currentValue + prevC1;
            srcList.Add(src);

            var cEma = CalculateEMA(prevC1, GetLastOrDefault(cEmaList), length);
            cEmaList.Add(cEma);

            var b = prevC1 - cEma;
            var c = (a * (src - prevSrc)) + ((1 - a) * b);
            cList.Add(c);

            var signal = GetCompareSignal(c - prevC1, prevC1 - prevC2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Sc", cList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(cList);
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
        List<double> stiffValueList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var aboveSumWindow = new RollingSum();

        var smaList = GetMovingAverageList(stockData, maType, length1, inputList);
        var stdDevList = CalculateStandardDeviationVolatility(stockData, maType, length1).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var sma = smaList[i];
            var stdDev = stdDevList[i];
            var bound = sma - (0.2 * stdDev);

            double above = currentValue > bound ? 1 : 0;
            aboveSumWindow.Add(above);

            var aboveSum = aboveSumWindow.Sum(length2);
            var stiffValue = length2 != 0 ? aboveSum * 100 / length2 : 0;
            stiffValueList.Add(stiffValue);
        }

        var stiffnessList = GetMovingAverageList(stockData, MovingAvgType.ExponentialMovingAverage, smoothingLength, stiffValueList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var stiffness = stiffnessList[i];
            var prevStiffness = i >= 1 ? stiffnessList[i - 1] : 0;

            var signal = GetCompareSignal(stiffness - threshold, prevStiffness - threshold);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Si", stiffnessList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(stiffnessList);
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
        List<double> tList = new(stockData.Count);
        List<double> srcList = new(stockData.Count);
        List<double> trendUpList = new(stockData.Count);
        List<double> trendDnList = new(stockData.Count);
        List<double> trendList = new(stockData.Count);
        List<double> tslList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
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
            tList.Add(t);

            var prevSrc = GetLastOrDefault(srcList);
            var src = (factor * prevTsl1) + ((1 - factor) * currentValue);
            srcList.Add(src);

            var up = prevTsl1 - t;
            var dn = prevTsl1 + t;

            var prevTrendUp = GetLastOrDefault(trendUpList);
            var trendUp = prevSrc > prevTrendUp ? Math.Max(up, prevTrendUp) : up;
            trendUpList.Add(trendUp);

            var prevTrendDn = GetLastOrDefault(trendDnList);
            var trendDn = prevSrc < prevTrendDn ? Math.Min(dn, prevTrendDn) : dn;
            trendDnList.Add(trendDn);

            var prevTrend = i >= 1 ? trendList[i - 1] : 1;
            var trend = src > prevTrendDn ? 1 : src < prevTrendUp ? -1 : prevTrend;
            trendList.Add(trend);

            var tsl = trend == 1 ? trendDn : trendUp;
            tslList.Add(tsl);

            var signal = GetCompareSignal(tsl - prevTsl1, prevTsl1 - prevTsl2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Stf", tslList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(tslList);
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
        List<double> pcList = new(stockData.Count);
        List<double> absPCList = new(stockData.Count);
        List<double> smiList = new(stockData.Count);
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

        var pcSmooth1List = GetMovingAverageList(stockData, maType, fastLength, pcList); 
        var pcSmooth2List = GetMovingAverageList(stockData, maType, slowLength, pcSmooth1List);
        var absPCSmooth1List = GetMovingAverageList(stockData, maType, fastLength, absPCList);
        var absPCSmooth2List = GetMovingAverageList(stockData, maType, slowLength, absPCSmooth1List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var absSmooth2PC = absPCSmooth2List[i];
            var smooth2PC = pcSmooth2List[i];

            var smi = absSmooth2PC != 0 ? MinOrMax(100 * smooth2PC / absSmooth2PC, 100, -100) : 0;
            smiList.Add(smi);
        }

        var smiSignalList = GetMovingAverageList(stockData, maType, signalLength, smiList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var smi = smiList[i];
            var smiSignal = smiSignalList[i];
            var prevSmi = i >= 1 ? smiList[i - 1] : 0;
            var prevSmiSignal = i >= 1 ? smiSignalList[i - 1] : 0;

            var signal = GetRsiSignal(smi - smiSignal, prevSmi - prevSmiSignal, smi, prevSmi, 10, -10);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Smi", smiList },
            { "Signal", smiSignalList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(smiList);
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
        List<double> aList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
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
            aList.Add(a);

            var signal = GetCompareSignal(a - prevA, prevA - prevA2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Sl", aList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(aList);
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
        List<double> oscList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

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
                oscList.Add(osc);
            }

            var oscEmaList = GetMovingAverageList(stockData, maType, length1, oscList);
            for (var i = 0; i < stockData.Count; i++)
            {
                var oscEma = oscEmaList[i];
                var prevOscEma1 = i >= 1 ? oscEmaList[i - 1] : 0;
                var prevOscEma2 = i >= 2 ? oscEmaList[i - 2] : 0;

                var signal = GetCompareSignal(oscEma - prevOscEma1, prevOscEma1 - prevOscEma2);
                signalsList?.Add(signal);
            }

            stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
                { "Srm", oscList },
                { "Signal", oscEmaList }
            });
            stockData.SetSignals(signalsList);
            stockData.SetCustomValues(oscList);
            stockData.IndicatorName = IndicatorName.SectorRotationModel;
        }

        return stockData;
    }

}

