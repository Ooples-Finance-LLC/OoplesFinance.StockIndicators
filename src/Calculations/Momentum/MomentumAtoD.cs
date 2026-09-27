using OoplesFinance.StockIndicators.Compatibility;

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
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateDynamicMomentumOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 10,
        int length2 = 20)
    {
        List<double> dmoList = new(stockData.Count);
        List<double> highestList = new(stockData.Count);
        List<double> lowestList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var stochList = CalculateStochasticOscillator(stockData, maType, length: length1, smoothLength1: length1, smoothLength2: length2);
        var stochSmaList = stochList.ChainedOutputs["FastD"];
        var smaValList = stochList.ChainedOutputs["SlowD"];

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
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateAnchoredMomentum(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int smoothLength = 7,
        int signalLength = 8, int momentumLength = 10)
    {
        smoothLength=Math.Max(1,smoothLength);signalLength=Math.Max(1,signalLength);var input=stockData.ChainedValues.Count>0?stockData.ChainedValues:stockData.InputValues;List<double> values=new(input.Count),signal=new(input.Count);var signals=CreateSignalsList(stockData);using var window=new AnchoredMomentumWindow(maType,smoothLength,signalLength,momentumLength,input.Count);
        if(Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType))
        {
            var smooth=Builder.Compute.ComponentAverage.Take(SpanCompat.AsReadOnlySpan(input),smoothLength)?.ToList()??GetMovingAverageList(stockData,maType,smoothLength,input);
            for(var i=0;i<input.Count;i++){var r=window.Finish(input[i],smooth[i],true);values.Add(r.Value);signal.Add(r.Signal);}
        }
        else foreach(var price in input){var r=window.Next(price,true);values.Add(r.Value);signal.Add(r.Signal);}
        for(var i=0;i<input.Count;i++)signals?.Add(GetCompareSignal(values[i]-signal[i],i>0?values[i-1]-signal[i-1]:0));
        stockData.SetOutputValues(()=>new Dictionary<string,List<double>>{{"Amom",values},{"Signal",signal}});stockData.SetSignals(signals);stockData.SetCustomValues(values);stockData.IndicatorName=IndicatorName.AnchoredMomentum;return stockData;
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
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateComparePriceMomentumOscillator(this StockData stockData, StockData marketDataClass,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 20, int length2 = 35, int signalLength = 10)
    {
        List<double> cpmoList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        if (stockData.Count == marketDataClass.InputValues.Count)
        {
            var pmoList = CalculatePriceMomentumOscillator(stockData, maType, length1, length2, signalLength).ChainedValues;
            var spPmoList = CalculatePriceMomentumOscillator(marketDataClass, maType, length1, length2, signalLength).ChainedValues;

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
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateDecisionPointPriceMomentumOscillator(this StockData stockData,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 35, int length2 = 20, int signalLength = 10)
    {
        if (StrengthWindow.Supports(maType) && !Builder.Compute.ComponentAverage.HasOverrides)
        {
            var (stableInput, _, _, _, _) = GetInputValuesList(stockData);
            var stableLine = new List<double>(stockData.Count);
            var stableSignal = new List<double>(stockData.Count);
            var stableHistogram = new List<double>(stockData.Count);
            var stableSignals = CreateSignalsList(stockData);
            using var stableWindow = new PriceMomentumWindow(maType, length1, length2, signalLength, stockData.Count);
            double previousDifference = 0;
            foreach (var price in stableInput)
            {
                var next = stableWindow.Next(price, true);
                stableLine.Add(next.Value); stableSignal.Add(next.Signal); stableHistogram.Add(next.Histogram);
                stableSignals?.Add(GetCompareSignal(next.Histogram, previousDifference));
                previousDifference = next.Histogram;
            }
            stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Dppmo", stableLine }, { "Signal", stableSignal }, { "Histogram", stableHistogram } });
            stockData.SetSignals(stableSignals); stockData.SetCustomValues(stableLine);
            stockData.IndicatorName = IndicatorName.DecisionPointPriceMomentumOscillator;
            return stockData;
        }

        List<double> pmolList = new(stockData.Count);
        List<double> dList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        using var fixedStages = new PriceMomentumWindow(MovingAvgType.ExponentialMovingAverage, length1, length2, 1, stockData.Count);
        foreach (var price in inputList) pmolList.Add(fixedStages.Next(price, true).Value);

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
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
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

        // Normalize the window deviation by its own smoothed value before choosing the RSI period.
        var standardDeviationList = GetStandardDeviationList(inputList, length1);
        var stdDeviationSmaList = GetMovingAverageList(stockData, maType, length2, standardDeviationList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var asd = stdDeviationSmaList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var dmiLength = DynamicMomentumPeriod.Calculate(standardDeviationList[i], asd, length3, dnLimit, upLimit);
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

