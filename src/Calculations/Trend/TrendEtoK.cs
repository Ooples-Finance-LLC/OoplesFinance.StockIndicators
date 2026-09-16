
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Highest High over a rolling window.
    /// </summary>
    /// <remarks>
    /// The highest high of the last <paramref name="length"/> bars. The window expands rather than warming up:
    /// before it is full, the highest high of the bars so far is still the highest high there is. When the
    /// caller supplies their own series, the high is the one the batch engine derives for that bar.
    /// </remarks>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateHighestHigh(this StockData stockData, int length = 14)
    {
        length = Math.Max(length, 1);
        var (_, highList, _, _, _) = GetInputValuesList(stockData);
        var count = highList.Count;
        List<double> highestHighList = new(count);
        List<Signal>? signalsList = CreateSignalsList(stockData, count);

        for (var i = 0; i < count; i++)
        {
            var start = Math.Max(0, i - length + 1);
            var highest = highList[start];
            for (var j = start + 1; j <= i; j++)
            {
                if (highList[j] > highest)
                {
                    highest = highList[j];
                }
            }

            highestHighList.Add(highest);

            var prevHighest1 = i >= 1 ? highestHighList[i - 1] : 0;
            var prevHighest2 = i >= 2 ? highestHighList[i - 2] : 0;
            var signal = GetCompareSignal(highest - prevHighest1, prevHighest1 - prevHighest2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "HighestHigh", highestHighList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(highestHighList);
        stockData.IndicatorName = IndicatorName.HighestHigh;

        return stockData;
    }

    /// <summary>
    /// Calculates the Gann Trend Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateGannTrendOscillator(this StockData stockData, int length = 3)
    {
        List<double> gannTrendOscillatorList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
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

            var prevGto = i >= 1 ? gannTrendOscillatorList[i - 1] : 0;
            var gto = prevHighest2 > prevHighest1 && highestHigh > prevHighest1 ? 1 : prevLowest2 < prevLowest1 && lowestLow < prevLowest1 ? -1 : prevGto;
            gannTrendOscillatorList.Add(gto);

            var signal = GetCompareSignal(gto, prevGto);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Gto", gannTrendOscillatorList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(gannTrendOscillatorList);
        stockData.IndicatorName = IndicatorName.GannTrendOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Grand Trend Forecasting
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="forecastLength"></param>
    /// <param name="mult"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateGrandTrendForecasting(this StockData stockData, int length = 100, int forecastLength = 200, double mult = 2)
    {
        List<double> upperList = new(stockData.Count);
        List<double> lowerList = new(stockData.Count);
        List<double> tList = new(stockData.Count);
        List<double> trendList = new(stockData.Count);
        List<double> chgList = new(stockData.Count);
        List<double> fcastList = new(stockData.Count);
        List<double> diffList = new(stockData.Count);
        List<double> bullSlopeList = new(stockData.Count);
        List<double> bearSlopeList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum tSumWindow = new();
        RollingSum diffSumWindow = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevT = i >= length ? tList[i - length] : currentValue;
            var priorT = i >= forecastLength ? tList[i - forecastLength] : 0;
            var prevFcast = i >= forecastLength ? fcastList[i - forecastLength] : 0;
            var prevChg = i >= length ? chgList[i - length] : currentValue;

            var chg = 0.9 * prevT;
            chgList.Add(chg);

            var t = (0.9 * prevT) + (0.1 * currentValue) + (chg - prevChg);
            tList.Add(t);
            tSumWindow.Add(t);

            var trend = tSumWindow.Average(length);
            trendList.Add(trend);

            var fcast = t + (t - priorT);
            fcastList.Add(fcast);

            var diff = Math.Abs(currentValue - prevFcast);
            diffList.Add(diff);
            diffSumWindow.Add(diff);

            var diffSma = diffSumWindow.Average(forecastLength);
            var dev = diffSma * mult;

            var upper = fcast + dev;
            upperList.Add(upper);

            var lower = fcast - dev;
            lowerList.Add(lower);

            var prevBullSlope = i >= 1 ? bullSlopeList[i - 1] : 0;
            var bullSlope = currentValue - Math.Max(fcast, Math.Max(t, trend));
            bullSlopeList.Add(bullSlope);

            var prevBearSlope = i >= 1 ? bearSlopeList[i - 1] : 0;
            var bearSlope = currentValue - Math.Min(fcast, Math.Min(t, trend));
            bearSlopeList.Add(bearSlope);

            var signal = GetBullishBearishSignal(bullSlope, prevBullSlope, bearSlope, prevBearSlope);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Gtf", trendList },
            { "UpperBand", upperList },
            { "MiddleBand", fcastList },
            { "LowerBand", lowerList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(trendList);
        stockData.IndicatorName = IndicatorName.GrandTrendForecasting;

        return stockData;
    }

}

