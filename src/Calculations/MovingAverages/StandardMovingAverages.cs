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
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        double sum = 0;
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            sum += currentValue;

            if (i >= length)
            {
                sum -= inputList[i - length];
            }

            var prevSma = smaList.LastOrDefault();
            var sma = i >= length - 1 ? sum / length : 0;
            smaList.Add(sma);

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

        double numerator = 0;
        double windowSum = 0;
        double weightedSumDenominator = (double)length * (length + 1) / 2;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;

            numerator += length * currentValue - windowSum;
            windowSum += currentValue;

            if (i >= length)
            {
                windowSum -= inputList[i - length];
            }

            var prevWma = wmaList.LastOrDefault();
            var wma = numerator / weightedSumDenominator;
            wmaList.Add(wma);

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
}
