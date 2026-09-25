namespace OoplesFinance.StockIndicators.Helpers;

internal static class RoundedImpulseOscillator
{
    // Round the exact typical-price mean once, before either EMA stage.
    internal static List<double> Input(StockData data, List<double> selected)
    {
        if (data.CustomValuesList.Count > 0) return selected;
        var values = new List<double>(data.Count);
        for (var i = 0; i < data.Count; i++)
            values.Add(Streaming.RollingMoneyFlowIndex.TypicalPrice(data.HighPrices[i], data.LowPrices[i], data.ClosePrices[i]));
        return values;
    }

    internal static double Line(double middle, double high, double low, bool percentage)
    {
        if (double.IsNaN(middle) || double.IsInfinity(middle) || double.IsNaN(high) || double.IsInfinity(high)
            || double.IsNaN(low) || double.IsInfinity(low)) return double.NaN;
        var boundary = middle > high ? high : middle < low ? low : middle;
        return percentage ? RoundedPercentageChange.Of(middle, boundary) : middle - boundary;
    }
}
