using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Computes an input series over every bar and chains it, so the next indicator computes on it.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="series">
    /// The series: a preset such as <see cref="InputSeries.MedianPrice"/>, a windowed one such as
    /// <see cref="InputSeries.Midpoint"/>, or any <see cref="InputSeries.Of(System.Func{OhlcvBar, double})"/>.
    /// </param>
    /// <returns>The same <paramref name="stockData"/>, with the series chained.</returns>
    /// <remarks>
    /// <para>
    /// The replacement for passing an input name, and the batch counterpart of
    /// <see cref="CustomInputState"/> - the SAME preset objects work in both engines:
    /// <code>
    /// var rsi = stockData.UseInput(InputSeries.MedianPrice).CalculateRelativeStrengthIndex(14);
    /// var rsi = new CustomInputState(new RelativeStrengthIndexState(14), InputSeries.MedianPrice);
    /// </code>
    /// </para>
    /// <para>
    /// Like every Calculate method it chains onto this StockData and returns it. The series is computed
    /// on the bars' own prices, so it reads true opens, highs, lows and closes whatever was chained before.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Either argument is null.</exception>
    public static StockData UseInput(this StockData stockData, IInputSeries series)
    {
        if (stockData is null)
        {
            throw new ArgumentNullException(nameof(stockData));
        }

        if (series is null)
        {
            throw new ArgumentNullException(nameof(series));
        }

        series.Reset();

        var opens = stockData.OpenPrices;
        var highs = stockData.HighPrices;
        var lows = stockData.LowPrices;
        var closes = stockData.ClosePrices;
        var volumes = stockData.Volumes;
        var dates = stockData.Dates;

        var values = new List<double>(stockData.Count);
        for (var i = 0; i < stockData.Count; i++)
        {
            var date = i < dates.Count ? dates[i] : default;
            var bar = new OhlcvBar("BATCH", BarTimeframe.Tick, date, date,
                opens[i], highs[i], lows[i], closes[i], volumes[i], isFinal: true);
            values.Add(series.Next(bar, isFinal: true));
        }

        stockData.SetInputSeries(values);

        return stockData;
    }
}
