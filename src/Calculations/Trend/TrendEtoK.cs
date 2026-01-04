
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Gann Trend Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
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

