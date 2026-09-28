
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Natural Stochastic Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateNaturalStochasticIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 20, int smoothLength = 10)
    {
        List<double> rawNstList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
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
                var frac = ExactRangePosition.Fraction(c, ll, hh);
                var ratio = 1 / Sqrt(j + 1);
                weightSum += frac * ratio;
                denomSum += ratio;
            }

            var rawNst = denomSum != 0 ? (200 * weightSum / denomSum) - 100 : 0;
            rawNstList.Add(rawNst);
        }

        var nstList = GetMovingAverageList(stockData, maType, smoothLength, rawNstList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var nst = nstList[i];
            var prevNst1 = i >= 1 ? nstList[i - 1] : 0;
            var prevNst2 = i >= 2 ? nstList[i - 2] : 0;

            var signal = GetCompareSignal(nst - prevNst1, prevNst1 - prevNst2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Nst", nstList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(nstList);
        stockData.IndicatorName = IndicatorName.NaturalStochasticIndicator;

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
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculatePremierStochasticOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 8, int smoothLength = 25)
    {
        List<double> nskList = new(stockData.Count);
        List<double> psoList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var len = MinOrMax((int)Math.Ceiling(Sqrt(smoothLength)));

        var stochasticRsiList = CalculateStochasticOscillator(stockData, maType, length).ChainedValues;

        for (var i = 0; i < stockData.Count; i++)
        {
            var sk = stochasticRsiList[i];

            var nsk = 0.1 * (sk - 50);
            nskList.Add(nsk);
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
            psoList.Add(pso);

            var signal = GetRsiSignal(pso - prevPso1, prevPso1 - prevPso2, pso, prevPso1, 0.9, -0.9);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Pso", psoList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(psoList);
        stockData.IndicatorName = IndicatorName.PremierStochasticOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Recursive Stochastic
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="alpha"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateRecursiveStochastic(this StockData stockData, int length = 200, double alpha = 0.1)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); var window = new RecursiveStochasticWindow(length, alpha); var line = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        foreach (var price in input)
        {
            var value = window.Next(price, true); var previous = line.Count > 0 ? line[line.Count - 1] : 0; var prior = line.Count > 1 ? line[line.Count - 2] : 0;
            signals?.Add(GetRsiSignal(value - previous, previous - prior, value, previous, 80, 20)); line.Add(value);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Rsto", line } }); stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.RecursiveStochastic; return stockData;
    }

}

