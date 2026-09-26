namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class BetterEmaWindow
{
    private readonly double _alpha;
    private double _previous, _ema;
    internal BetterEmaWindow(int length)
    {
        length = Math.Max(1, length); var angle = 2 * Math.PI / length;
        var value = Math.Cos(angle) + Math.Sin(angle);
        _alpha = value == 0 ? .01 : Math.Min(.99, Math.Max(.01, (value - 1) / value));
    }
    private double Blend(double price, double previous)
    {
        var sum = new ExactMeanAccumulator(); sum.Add(previous); sum.AddProduct(price, _alpha); sum.AddProduct(previous, -_alpha);
        return sum.Mean(1);
    }
    internal double Next(double price, bool commit)
    {
        var middle = HighLowAverageWindow.Midpoint(price, _previous);
        var output = Blend(middle, _ema); var ema = Blend(price, _ema);
        if (commit) { _previous = price; _ema = ema; }
        return output;
    }
    internal void Reset() { _previous = 0; _ema = 0; }
}
