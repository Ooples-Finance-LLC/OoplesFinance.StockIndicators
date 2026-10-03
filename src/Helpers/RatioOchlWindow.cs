namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class RatioOchlWindow
{
    private double _previous;
    private bool _hasPrevious;
    internal double Next(double open, double high, double low, double close, bool commit)
    {
        var numerator = new ExactMeanAccumulator(); numerator.Add(close); numerator.Add(open, -1);
        if (numerator.Sign < 0) { var negative = numerator; numerator = default; numerator.Subtract(negative); }
        var denominator = new ExactMeanAccumulator(); denominator.Add(high); denominator.Add(low, -1);
        var gain = denominator.IsExactlyZero ? 0 : Math.Min(1, numerator.Ratio(denominator));
        var value = VidyaBlend.Compute(_hasPrevious ? _previous : close, close, gain);
        if (commit) { _previous = value; _hasPrevious = true; }
        return value;
    }
    internal void Reset() { _previous = 0; _hasPrevious = false; }
}
