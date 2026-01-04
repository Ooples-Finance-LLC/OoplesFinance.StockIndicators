
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Dynamic Momentum Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculateDynamicMomentumOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 10,
        int length2 = 20)
    {
        List<double> dmoList = new(stockData.Count);
        List<double> highestList = new(stockData.Count);
        List<double> lowestList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var stochList = CalculateStochasticOscillator(stockData, maType, length: length1, smoothLength1: length1, smoothLength2: length2);
        var stochSmaList = stochList.OutputValues["FastD"];
        var smaValList = stochList.OutputValues["SlowD"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var smaVal = smaValList[i];
            var stochSma = stochSmaList[i];
            var prevDmo1 = i >= 1 ? dmoList[i - 1] : 0;
            var prevDmo2 = i >= 2 ? dmoList[i - 2] : 0;

            var prevHighest = GetLastOrDefault(highestList);
            var highest = stochSma > prevHighest ? stochSma : prevHighest;
            highestList.Add(highest);

            var prevLowest = i >= 1 ? GetLastOrDefault(lowestList) : double.MaxValue;
            var lowest = stochSma < prevLowest ? stochSma : prevLowest;
            lowestList.Add(lowest);

            var midpoint = MinOrMax((lowest + highest) / 2, 100, 0);
            var dmo = MinOrMax(midpoint - (smaVal - stochSma), 100, 0);
            dmoList.Add(dmo);

            var signal = GetRsiSignal(dmo - prevDmo1, prevDmo1 - prevDmo2, dmo, prevDmo1, 77, 23);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Dmo", dmoList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(dmoList);
        stockData.IndicatorName = IndicatorName.DynamicMomentumOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Anchored Momentum
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="smoothLength">Length of the smooth.</param>
    /// <param name="signalLength">Length of the signal.</param>
    /// <param name="momentumLength">Length of the momentum.</param>
    /// <returns></returns>
    public static StockData CalculateAnchoredMomentum(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int smoothLength = 7,
        int signalLength = 8, int momentumLength = 10)
    {
        List<double> tempList = new(stockData.Count);
        List<double> amomList = new(stockData.Count);
        List<double> amomsList = new(stockData.Count);
        var tempSumWindow = new RollingSum();
        var amomSumWindow = new RollingSum();
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var p = MinOrMax((2 * momentumLength) + 1);

        var emaList = GetMovingAverageList(stockData, maType, smoothLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentEma = emaList[i];

            var currentValue = inputList[i];
            tempList.Add(currentValue);
            tempSumWindow.Add(currentValue);

            var sma = tempSumWindow.Average(p);
            var prevAmom = GetLastOrDefault(amomList);
            var amom = sma != 0 ? 100 * ((currentEma / sma) - 1) : 0;
            amomList.Add(amom);
            amomSumWindow.Add(amom);

            var prevAmoms = GetLastOrDefault(amomsList);
            var amoms = amomSumWindow.Average(signalLength);
            amomsList.Add(amoms);

            var signal = GetCompareSignal(amom - amoms, prevAmom - prevAmoms);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Amom", amomList },
            { "Signal", amomsList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(amomList);
        stockData.IndicatorName = IndicatorName.AnchoredMomentum;

        return stockData;
    }


    /// <summary>
    /// Calculates the Compare Price Momentum Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="marketDataClass"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculateComparePriceMomentumOscillator(this StockData stockData, StockData marketDataClass,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 20, int length2 = 35, int signalLength = 10)
    {
        List<double> cpmoList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        if (stockData.Count == marketDataClass.InputValues.Count)
        {
            var pmoList = CalculatePriceMomentumOscillator(stockData, maType, length1, length2, signalLength).CustomValuesList;
            var spPmoList = CalculatePriceMomentumOscillator(marketDataClass, maType, length1, length2, signalLength).CustomValuesList;

            for (var i = 0; i < stockData.Count; i++)
            {
                var pmo = pmoList[i];
                var spPmo = spPmoList[i];

                var prevCpmo = GetLastOrDefault(cpmoList);
                var cpmo = pmo - spPmo;
                cpmoList.Add(cpmo);

                var signal = GetCompareSignal(cpmo, prevCpmo);
                signalsList?.Add(signal);
            }
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Cpmo", cpmoList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(cpmoList);
        stockData.IndicatorName = IndicatorName.ComparePriceMomentumOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Decision Point Price Momentum Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculateDecisionPointPriceMomentumOscillator(this StockData stockData,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 35, int length2 = 20, int signalLength = 10)
    {
        List<double> pmol2List = new(stockData.Count);
        List<double> pmolList = new(stockData.Count);
        List<double> dList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smPmol2 = (double)2 / length1;
        var smPmol = (double)2 / length2;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var ival = prevValue != 0 ? currentValue / prevValue * 100 : 100;
            var prevPmol = GetLastOrDefault(pmolList);
            var prevPmol2 = GetLastOrDefault(pmol2List);

            var pmol2 = ((ival - 100 - prevPmol2) * smPmol2) + prevPmol2;
            pmol2List.Add(pmol2);

            var pmol = (((10 * pmol2) - prevPmol) * smPmol) + prevPmol;
            pmolList.Add(pmol);
        }

        var pmolsList = GetMovingAverageList(stockData, maType, signalLength, pmolList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var pmol = pmolList[i];
            var pmols = pmolsList[i];

            var prevD = GetLastOrDefault(dList);
            var d = pmol - pmols;
            dList.Add(d);

            var signal = GetCompareSignal(d, prevD);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Dppmo", pmolList },
            { "Signal", pmolsList },
            { "Histogram", dList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(pmolList);
        stockData.IndicatorName = IndicatorName.DecisionPointPriceMomentumOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Dynamic Momentum Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="upLimit"></param>
    /// <param name="dnLimit"></param>
    /// <returns></returns>
    public static StockData CalculateDynamicMomentumIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 5,
        int length2 = 10, int length3 = 14, int upLimit = 30, int dnLimit = 5)
    {
        List<double> lossList = new(stockData.Count);
        List<double> gainList = new(stockData.Count);
        List<double> dmiSmaList = new(stockData.Count);
        List<double> dmiSignalSmaList = new(stockData.Count);
        List<double> dmiHistogramSmaList = new(stockData.Count);
        var lossSumWindow = new RollingSum();
        var gainSumWindow = new RollingSum();
        var dmiSumWindow = new RollingSum();
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var standardDeviationList = CalculateStandardDeviationVolatility(stockData, maType, length1).CustomValuesList;
        var stdDeviationSmaList = GetMovingAverageList(stockData, maType, length2, standardDeviationList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var asd = stdDeviationSmaList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            int dTime;
            try
            {
                dTime = asd != 0 ? Math.Min(upLimit, (int)Math.Ceiling(length3 / asd)) : 0;
            }
            catch
            {
                dTime = upLimit;
            }

            var dmiLength = Math.Max(Math.Min(dTime, upLimit), dnLimit);
            var priceChg = MinPastValues(i, 1, currentValue - prevValue);

            var loss = i >= 1 && priceChg < 0 ? Math.Abs(priceChg) : 0;
            lossList.Add(loss);
            lossSumWindow.Add(loss);

            var gain = i >= 1 && priceChg > 0 ? priceChg : 0;
            gainList.Add(gain);
            gainSumWindow.Add(gain);

            var avgGainSma = gainSumWindow.Average(dmiLength);
            var avgLossSma = lossSumWindow.Average(dmiLength);
            var rsSma = avgLossSma != 0 ? avgGainSma / avgLossSma : 0;

            var prevDmiSma = GetLastOrDefault(dmiSmaList);
            var dmiSma = avgLossSma == 0 ? 100 : avgGainSma == 0 ? 0 : 100 - (100 / (1 + rsSma));
            dmiSmaList.Add(dmiSma);
            dmiSumWindow.Add(dmiSma);

            var dmiSignalSma = dmiSumWindow.Average(dmiLength);
            dmiSignalSmaList.Add(dmiSignalSma);

            var prevDmiHistogram = GetLastOrDefault(dmiHistogramSmaList);
            var dmiHistogramSma = dmiSma - dmiSignalSma;
            dmiHistogramSmaList.Add(dmiHistogramSma);

            var signal = GetRsiSignal(dmiHistogramSma, prevDmiHistogram, dmiSma, prevDmiSma, 70, 30);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Dmi", dmiSmaList },
            { "Signal", dmiSignalSmaList },
            { "Histogram", dmiHistogramSmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(dmiSmaList);
        stockData.IndicatorName = IndicatorName.DynamicMomentumIndex;

        return stockData;
    }

}

