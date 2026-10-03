using Fraction = OoplesFinance.StockIndicators.Helpers.UltimatePowerWeights.Fraction;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class UltimateBandWindow
{
    private readonly int _length;
    private readonly Fraction _multiplier;
    private readonly Queue<Fraction> _prices = new();
    private Fraction _sum, _squares;
    internal UltimateBandWindow(int length, double multiplier)
    { _length = Math.Max(1, length); _multiplier = Fraction.Of(multiplier); }

    internal (double Upper, double Middle, double Lower) Next(double price, UltimateAverageWindow.Point center, bool final)
    {
        var value = Fraction.Of(price); var full = _prices.Count == _length;
        Fraction expired = full ? _prices.Peek() : 0;
        var sum = _sum + value - expired; var squares = _squares + value * value - expired * expired;
        var variance = _prices.Count < _length - 1 ? (Fraction)0 : (squares * _length - sum * sum) / _length / _length;
        var widthSquare = _multiplier * _multiplier * variance;
        var upper = Publish(center, widthSquare, _multiplier.Sign);
        var lower = Publish(center, widthSquare, -_multiplier.Sign);
        if (final)
        {
            if (full) _prices.Dequeue(); _prices.Enqueue(value); _sum = sum; _squares = squares;
        }
        return (upper, center.Value, lower);
    }

    private static double Publish(UltimateAverageWindow.Point center, Fraction square, int direction)
    {
        if (square.Sign == 0 || direction == 0) return center.Value;
        for (var bits = 96; ; bits = checked(bits * 2))
        {
            var mean = center.Bounds(bits); var root = UltimatePowerWeights.Root(square, bits);
            var lower = mean.Lower + direction * (direction > 0 ? root.Lower : root.Upper);
            var upper = mean.Upper + direction * (direction > 0 ? root.Upper : root.Lower);
            var low = lower.Publish(); var high = upper.Publish();
            if (low == high) return low;
            var middle = (lower + upper) / 2;
            if (lower.Sign == upper.Sign && upper - lower <= middle.Abs() * Fraction.Grid(64)) return middle.Publish();
        }
    }

    internal void Reset() { _prices.Clear(); _sum = _squares = default; }

    internal static (double[] Upper, double[] Middle, double[] Lower, Signal[] Signals) Calculate(StockData data,
        MovingAvgType kind, int minimum, int maximum, double multiplier)
    {
        var window = new UltimateBandWindow(minimum, multiplier);
        var prices = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var upper = new double[prices.Count]; var lower = new double[prices.Count];
        var signals = new Signal[prices.Count]; Fraction previousDifference = 0;
        var middle = UltimateAverageWindow.Calculate(data, kind, minimum, maximum, 1, (i, point) =>
        {
            var bands = window.Next(prices[i], point, true); upper[i] = bands.Upper; lower[i] = bands.Lower;
            var difference = Fraction.Of(prices[i]) - Fraction.Of(bands.Middle);
            var previousPrice = i > 0 ? prices[i - 1] : 0;
            var previousUpper = i > 0 ? upper[i - 1] : 0; var previousLower = i > 0 ? lower[i - 1] : 0;
            signals[i] = difference.Sign > 0 && difference > previousDifference ? Signal.StrongBuy
                : difference.Sign < 0 && difference < previousDifference ? Signal.StrongSell
                : difference.Sign > 0 || previousPrice < previousLower && prices[i] > bands.Lower ? Signal.Buy
                : difference.Sign < 0 || previousPrice > previousUpper && prices[i] < bands.Upper ? Signal.Sell : Signal.None;
            previousDifference = difference;
        }).Values;
        return (upper, middle, lower, signals);
    }
}
