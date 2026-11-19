using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;

namespace OoplesFinance.StockIndicators.Helpers;

// Every state encloses the exact rational recurrence. Publish only when both
// endpoints round to the same binary64 value and both signal signs are proved.
internal sealed class CertifiedTrueRangeAdjusted(int length, double multiplier)
{
    private static readonly Number Padding = Number.Of(Math.Pow(2, -78));
    private readonly Number _multiplier = Number.Of(multiplier);
    private Ball _sum, _average, _line, _slope;
    private Number _previousPrice;
    private long _count;
    private double _first;
    private bool _constant = true;
    internal static bool Supports(int length, double multiplier) => length >= 3 && multiplier >= 0;
    private static Number Abs(Number n) => n.Sign < 0 ? default(Number) - n : n;
    private static Number Min(Number a, Number b) => (a - b).Sign <= 0 ? a : b;
    private static Number Max(Number a, Number b) => (a - b).Sign >= 0 ? a : b;
    private static Number Upper(Number n) { var r = n.Round(80); return r + r * Padding; }

    private readonly struct Ball
    {
        internal readonly Number Center, Radius;
        private Ball(Number center, Number radius) { Center = center; Radius = radius; }
        internal Number Lower => Center - Radius;
        internal Number Higher => Center + Radius;
        internal bool IsZero => Center.Sign == 0 && Radius.Sign == 0;
        private static Ball Rounded(Number center, Number radius)
        {
            var rounded = center.Round(160);
            return new(rounded, Upper(radius + Abs(center - rounded)));
        }
        internal static Ball Exact(Number n) => Rounded(n, default);
        internal static Ball Bounds(Number lower, Number higher) => Rounded((lower + higher).Divide(2), (higher - lower).Divide(2));
        public static Ball operator +(Ball a, Ball b) => Rounded(a.Center + b.Center, a.Radius + b.Radius);
        public static Ball operator -(Ball a, Ball b) => Rounded(a.Center - b.Center, a.Radius + b.Radius);
        internal Ball Times(long n) => Rounded(Center.Times(n), Radius.Times(n));
        internal Ball Divide(long n) => Rounded(Center.Divide(n), Radius.Divide(n));
        internal bool TrySign(out int sign)
        { sign = Center.Sign; return Radius.Sign == 0 || (Abs(Center) - Radius).Sign > 0; }
        internal bool TryPublish(out double value)
        { value = Lower.Publish(); return BitConverter.DoubleToInt64Bits(value) == BitConverter.DoubleToInt64Bits(Higher.Publish()); }
    }

    internal bool TryNext(double price, double high, double low, bool final, out (double Line, Signal Trade) result)
    {
        result = default;
        var current = Number.Of(price); var previousPrice = _count == 0 ? current : _previousPrice;
        var range = Max(Number.Of(high) - Number.Of(low), Max(Abs(Number.Of(high) - previousPrice), Abs(Number.Of(low) - previousPrice)));
        var sum = _sum; Ball average;
        if (_count < length) { sum += Ball.Exact(range); average = sum.Divide(_count + 1); }
        else average = (_average.Times(length - 1L) + Ball.Exact(range).Times(2)).Divide(length + 1L);
        Number gainLow, gainHigh;
        if (average.IsZero) gainLow = gainHigh = _multiplier;
        else
        {
            var lower = average.Lower;
            if (lower.Sign <= 0) return false;
            gainLow = range.Divide(average.Higher) * _multiplier;
            gainHigh = range.Divide(lower) * _multiplier;
        }
        gainLow = Min(gainLow, Number.Of(2)).Times(2).Divide(length + 1L);
        gainHigh = Min(gainHigh, Number.Of(2)).Times(2).Divide(length + 1L);
        var constant = _count == 0 || _constant && price == _first; // NOSONAR: S1244 - Exact constant input certifies the recurrence; a tolerance would change numerical behavior.
        Ball line;
        if (constant) line = Ball.Exact(current);
        else
        {
            // 0 <= gain <= 1: for fixed gain the update is monotone in the
            // previous line; for fixed line it is affine in gain. Evaluate the
            // four corners rather than losing correlation in line+gain*(p-line).
            Number Update(Number old, Number gain) => old + gain * (current - old);
            var ll = Update(_line.Lower, gainLow); var lh = Update(_line.Lower, gainHigh);
            var hl = Update(_line.Higher, gainLow); var hh = Update(_line.Higher, gainHigh);
            line = Ball.Bounds(Min(ll, lh), Max(hl, hh));
        }
        var slope = constant && _count != 0 ? default : line - _line;
        var acceleration = slope - _slope;
        if (!line.TryPublish(out var value) || !slope.TrySign(out var direction) || !acceleration.TrySign(out var change)) return false;
        var trade = direction > 0 && change > 0 ? Signal.StrongBuy : direction < 0 && change < 0 ? Signal.StrongSell
            : direction > 0 ? Signal.Buy : direction < 0 ? Signal.Sell : Signal.None;
        if (final)
        {
            _sum = sum; _average = average; _line = line; _slope = slope; _previousPrice = current;
            _constant = constant; if (_count == 0) _first = price; _count++;
        }
        result = (value, trade); return true;
    }
}
