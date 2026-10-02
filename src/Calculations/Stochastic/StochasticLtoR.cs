
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
        var values = PremierStochasticWindow.Calculate(stockData, maType, length, smoothLength, false).ToList();
        List<Signal>? signals = CreateSignalsList(stockData);
        for (var i = 0; i < values.Count; i++)
        {
            var previous = i == 0 ? 0 : values[i - 1]; var before = i < 2 ? 0 : values[i - 2];
            signals?.Add(PremierStochasticWindow.Trade(values[i], previous, before));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Pso", values } });
        stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.PremierStochasticOscillator; return stockData;
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

