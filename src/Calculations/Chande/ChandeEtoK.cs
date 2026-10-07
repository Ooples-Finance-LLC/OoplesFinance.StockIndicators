
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Chande Kroll Rsquared Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateChandeKrollRSquaredIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 14, int smoothLength = 3)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); var external = Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType);
        var component = external ? ChandeKrollWindow.Component(stockData, input, maType, length, smoothLength) : null; using var window = new ChandeKrollWindow(maType, length, smoothLength, external);
        var values = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++) { var point = window.Next(input[i], true, component?[i]); values.Add(point.Value); signals?.Add(point.Signal); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Ckrsi", values } }); stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.ChandeKrollRSquaredIndex; return stockData;
    }


    /// <summary>
    /// Calculates the Chande Forecast Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateChandeForecastOscillator(this StockData stockData, int length = 14)
    {
        List<double> pfList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        using var regression = new ExactLinearFitWindow(length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];

            var prevPf = GetLastOrDefault(pfList);
            var pf = regression.Next(currentValue, true).PercentResidual(currentValue);
            pfList.Add(pf);

            var signal = GetCompareSignal(pf, prevPf);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Cfo", pfList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(pfList);
        stockData.IndicatorName = IndicatorName.ChandeForecastOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Chande Intraday Momentum Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateChandeIntradayMomentumIndex(this StockData stockData, int length = 14)
    {
        List<double> imiUnfilteredList = new(stockData.Count);
        using var window = new IntradayGainLossWindow(length, stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, openList, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentClose = inputList[i];
            var currentOpen = openList[i];
            var prevImi1 = i >= 1 ? imiUnfilteredList[i - 1] : 0;
            var prevImi2 = i >= 2 ? imiUnfilteredList[i - 2] : 0;

            var imiUnfiltered = window.Next(currentClose, currentOpen, true);
            imiUnfilteredList.Add(imiUnfiltered);

            var signal = GetRsiSignal(imiUnfiltered - prevImi1, prevImi1 - prevImi2, imiUnfiltered, prevImi1, 70, 30);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Cimi", imiUnfilteredList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(imiUnfilteredList);
        stockData.IndicatorName = IndicatorName.ChandeIntradayMomentumIndex;

        return stockData;
    }

}

